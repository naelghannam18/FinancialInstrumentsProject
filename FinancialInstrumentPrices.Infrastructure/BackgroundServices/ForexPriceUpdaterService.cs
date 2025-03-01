using FinancialInstrumentPrices.Domain.Constants;
using FinancialInstrumentPrices.Domain.Contracts;
using FinancialInstrumentPrices.Domain.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace FinancialInstrumentPrices.Infrastructure.BackgroundServices;

public class ForexPriceUpdaterService(ILogger<ForexPriceUpdaterService> _logger, IServiceScopeFactory _serviceScopeFactory) : BackgroundService
{
    private const int TICKER_ARRAY_INDEX = 1;
    private const int TIMESTAMP_ARRAY_INDEX = 2;
    private const int ASK_PRICE_ARRAY_INDEX = 6;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ForexPriceUpdaterService starting...");
        var clientWebSocket = new ClientWebSocket();

        try
        {
            // Initialize service scope
            using var scope = _serviceScopeFactory.CreateScope();
            // Initialize services
            var instrumentRepository = scope.ServiceProvider.GetRequiredService<IInstrumentRepository>();
            var webSocketHandler = scope.ServiceProvider.GetRequiredService<IWebSocketHandler>();
            var dataSourceOptions = scope.ServiceProvider.GetRequiredService<IOptions<DataSourcesOptions>>().Value;

            // 1) Connect
            await clientWebSocket.ConnectAsync(new Uri(dataSourceOptions.TiingoFxWsUri), stoppingToken);
            _logger.LogInformation("Connected to Tiingo Forex WebSocket.");

            // 2) Subscribe to multiple streams at once
            var subscribePayload = new
            {
                eventName = ApplicationConstants.WebSocketsCommands.Subscribe,
                authorization = dataSourceOptions.TiingoToken,
                eventData = new
                {
                    thresholdLevel = 5,
                    tickers = instrumentRepository.GetForexInstruments()
                },
            };

            string json = JsonSerializer.Serialize(subscribePayload);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await clientWebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, stoppingToken);


            // 3) Continuously read messages
            var buffer = new byte[1024 * 4];

            while (!stoppingToken.IsCancellationRequested && clientWebSocket.State == WebSocketState.Open)
            {
                var result = await clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), stoppingToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogWarning("Tiingo WS closed, reconnecting..");
                    await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, ApplicationConstants.WebsocketClosingDescription.Closing, stoppingToken);
                    // break, loop around, and try reconnect
                    break;
                }

                // Parse the message
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                HandleTiingoForexUpdate(message, instrumentRepository, webSocketHandler);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in ForexPriceUpdaterService.");
        }
        finally
        {
            clientWebSocket?.Dispose();
        }
    }

    private void HandleTiingoForexUpdate(string json, IInstrumentRepository instrumentRepository, IWebSocketHandler webSocketHandler)
    {
        // Crypto response payload:
        /*
         
            {
                "messageType":"A", 
                "service":"fx",
                "data":[
                    "Q",
                    "eurusd",
                    "2023-06-14T14:25:50.432000+00:00",
                    1000000.0,
                    1.0847,
                    1.084745,
                    1000000.0,
                    1.08479
                ]
            }
        
         */
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty(ApplicationConstants.TiingoResponsePropertyNames.Data, out var data))
            {
                var dataArray = data.EnumerateArray().ToArray();
                var ticker = dataArray[TICKER_ARRAY_INDEX].GetString();
                var askPrice = dataArray[ASK_PRICE_ARRAY_INDEX].GetDecimal();
                var timestamp = dataArray[TIMESTAMP_ARRAY_INDEX].GetDateTime();
                // 4) Update in-memory store
                instrumentRepository.UpdatePrice(ticker, new (askPrice, timestamp));
                // 5) Broadcast update
                webSocketHandler.BroadcastPriceUpdateAsync(ticker, askPrice, timestamp)
                          .ConfigureAwait(false); // asynchronous fire-and-forget
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing tiingo message.");
        }
    }
}
