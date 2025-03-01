using FinancialInstrumentPrices.Domain.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using FinancialInstrumentPrices.Domain.Options;
using FinancialInstrumentPrices.Domain.Constants;

namespace FinancialInstrumentPrices.Infrastructure.BackgroundServices;

public class CryptoPriceUpdaterService(ILogger<CryptoPriceUpdaterService> _logger, IServiceScopeFactory _serviceScopeFactory) : BackgroundService
{
    private const int TICKER_ARRAY_INDEX = 1;
    private const int TIMESTAMP_ARRAY_INDEX = 2;
    private const int LAST_PRICE_ARRAY_INDEX = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        _logger.LogInformation("CryptoPriceUpdaterService starting...");
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
            await clientWebSocket.ConnectAsync(new Uri(dataSourceOptions.TiingoCryptoWsUri), stoppingToken);
            _logger.LogInformation("Connected to Tiingo Crypto WebSocket.");


            // 2) Subscribe to multiple streams at once
            var subscribePayload = new
            {
                eventName = ApplicationConstants.WebSocketsCommands.Subscribe,
                authorization = dataSourceOptions.TiingoToken,
                eventData = new 
                {
                    thresholdLevel = 5,
                    tickers = instrumentRepository.GetCryptoInstruments()
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
                HandleTiingoCryptoUpdate(message, instrumentRepository, webSocketHandler);
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in CryptoPriceUpdaterService.");
        }
        finally
        {
            clientWebSocket?.Dispose();
        }
    }

    private void HandleTiingoCryptoUpdate(string json, IInstrumentRepository instrumentRepository, IWebSocketHandler webSocketHandler)
    {
        // Crypto response payload:
        /*
         
        {
            "service": "crypto_data",
            "messageType": "A",
            "data": [
                "T",
                "xrpusdt",
                "2025-02-28T18:46:38.675000+00:00",
                "binance",
                6080.0,
                2.1467452467105277
            ]
        }
        
         */
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if(root.TryGetProperty(ApplicationConstants.TiingoResponsePropertyNames.Data, out var data))
            {
                var dataArray = data.EnumerateArray().ToArray();
                var ticker = dataArray[TICKER_ARRAY_INDEX].GetString();
                var lastPrice = dataArray[LAST_PRICE_ARRAY_INDEX].GetDecimal();
                var timestamp = dataArray[TIMESTAMP_ARRAY_INDEX].GetDateTime();
                // 4) Update in-memory store
                instrumentRepository.UpdatePrice(ticker, new (lastPrice, timestamp));
                // 5) Broadcast update
                webSocketHandler.BroadcastPriceUpdateAsync(ticker, lastPrice, timestamp)
                          .ConfigureAwait(false); // asynchronous fire-and-forget
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing tiingo message.");
        }
    }
}
