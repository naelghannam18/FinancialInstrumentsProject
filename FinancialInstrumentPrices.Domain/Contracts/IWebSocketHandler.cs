using System.Net.WebSockets;

namespace FinancialInstrumentPrices.Domain.Contracts;

public interface IWebSocketHandler
{
    Task HandleWebSocketAsync(WebSocket webSocket, CancellationToken cancellationToken);

    Task BroadcastPriceUpdateAsync(string instrument, decimal price, DateTime timestamp);
}
