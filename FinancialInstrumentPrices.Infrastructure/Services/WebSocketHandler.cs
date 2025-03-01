using FinancialInstrumentPrices.Domain.Constants;
using FinancialInstrumentPrices.Domain.Contracts;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace FinancialInstrumentPrices.Infrastructure.Services;

public class WebSocketHandler
    (
        ILogger<WebSocketHandler> logger
    ) : IWebSocketHandler
{
    #region Private Readonly Fields
    private readonly ILogger<WebSocketHandler> _logger = logger;
    // Mapping instrument name to connected WebSocket clients
    // We're using ConcurrentDictionary<WebSocket, bool> as the second generic parameter instead of a ConcurrentBag<WebSocket>
    // Because we need to remove websockets from the collection when they disconnect
    // A ConcurrentBag does not allow for easy removal of items
    // the bool parameter is not used, but this data structure allows us to remove websockets
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<WebSocket, bool>> _subscriptions = new();
    private const int BATCH_SIZE = 100;
    #endregion

    #region Public Methods
    /// <summary>
    /// Handles a new WebSocket connection.
    /// Clients are expected to send subscription messages in JSON format:
    ///   { "action": "subscribe", "instruments": ["btcusdt", "xrpusdt"] }
    /// or unsubscribe with "unsubscribe".
    /// </summary>
    public async Task HandleWebSocketAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        _logger.LogInformation("New WebSocket connection established.");
        var buffer = new byte[1024 * 4];

        // Track which instruments this client has subscribed to
        var clientSubscriptions = new HashSet<string>();

        while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogInformation("WebSocket connection closed by client.");
                break;
            }
            else
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                try
                {
                    using var doc = JsonDocument.Parse(message);
                    var root = doc.RootElement;
                    if (root.TryGetProperty(ApplicationConstants.WebSocketRequestProperties.Action, out var actionElement) &&
                        root.TryGetProperty(ApplicationConstants.WebSocketRequestProperties.Instruments, out var instrumentElement))
                    {
                        var action = actionElement.GetString()?.ToLower();
                        var instruments = instrumentElement.EnumerateArray().ToArray();
                        if (action == ApplicationConstants.WebSocketsCommands.Subscribe && instruments is not null && instruments.Length > 0)
                        {
                            foreach (var instrument in instruments)
                            {
                                var instrumentValue = instrument.GetString();

                                if (string.IsNullOrWhiteSpace(instrumentValue)) continue;

                                Subscribe(instrumentValue, webSocket);
                                clientSubscriptions.Add(instrumentValue);
                                _logger.LogInformation("Client subscribed to {instrumentValue}", instrumentValue);
                            }
                        }
                        else if (action == ApplicationConstants.WebSocketsCommands.Unsubscribe && instruments is not null && instruments.Length > 0)
                        {
                            foreach (var instrument in instruments)
                            {
                                var instrumentValue = instrument.GetString();

                                if (string.IsNullOrWhiteSpace(instrumentValue)) continue;

                                Subscribe(instrumentValue, webSocket);
                                clientSubscriptions.Add(instrumentValue);
                                _logger.LogInformation("Client subscribed to {instrumentValue}", instrumentValue);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing subscription message.");
                    continue;
                }
            }
        }

        // Clean up: remove the closed sockets from all subscriptions
        foreach (var instrument in clientSubscriptions)
        {
            Unsubscribe(instrument, webSocket);
        }
    }

    public async Task BroadcastPriceUpdateAsync(string instrument, decimal price, DateTime timesptamp)
    {
        if (_subscriptions.TryGetValue(instrument, out var socketsDict) && !socketsDict.IsEmpty)
        {
            var payload = new { instrument, price, timestamp = timesptamp };
            var message = JsonSerializer.Serialize(payload);
            var messageBuffer = Encoding.UTF8.GetBytes(message);

            var sockets = socketsDict.Keys;
            var socketsSnapShot = sockets.ToList();

            for (var i = 0; i < socketsSnapShot.Count; i += BATCH_SIZE)
            {
                var tasks = new List<Task>();

                // Take up to 'BatchSize' items
                var batch = socketsSnapShot.Skip(i).Take(BATCH_SIZE);

                foreach (var socket in batch)
                {
                    if (socket.State == WebSocketState.Open)
                    {
                        // Here is where we efficiently handle 1,000+ subscribers using async sends.
                        // We are batching so we dont overwhelm the system with 1,000+ async tasks.
                        tasks.Add(socket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None));
                    }
                }

                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error broadcasting price update.");
                }
            }
        }
    }
    #endregion

    #region Private Methods
    private void Subscribe(string instrument, WebSocket socket)
    {
        var bag = _subscriptions.GetOrAdd(instrument.ToLower(), _ => new ConcurrentDictionary<WebSocket, bool>());
        bag.TryAdd(socket, true);
    }

    private void Unsubscribe(string instrument, WebSocket socket)
    {
        var bag = _subscriptions.GetOrAdd(instrument.ToLower(), _ => new ConcurrentDictionary<WebSocket, bool>());
        bag.Remove(socket, out _);
    }
    #endregion
}
