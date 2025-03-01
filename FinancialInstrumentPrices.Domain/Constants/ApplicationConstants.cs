namespace FinancialInstrumentPrices.Domain.Constants;

public static class ApplicationConstants
{
    public struct WebSocketsCommands
    {
        public const string Subscribe = "subscribe";
        public const string Unsubscribe = "unsubscribe";
    }

    public struct WebSocketRequestProperties
    {
        public const string Action = "action";
        public const string Instruments = "instruments";
    }

    public struct WebsocketClosingDescription 
    {
        public const string Closing = nameof(Closing);
    }

    public struct TiingoResponsePropertyNames
    {
        public const string Data = "data";
    }

    public struct InstrumentNames
    {
        public const string XRPUSDT = "xrpusdt";
        public const string DOGEUSDT = "dogeusdt";
        public const string BTCUSDT = "btcusdt";
        public const string EURUSD = "eurusd";
        public const string JPYUSD = "jpyusd";
    }
}
