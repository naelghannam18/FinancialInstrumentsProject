using FinancialInstrumentPrices.Domain.Constants;
using FinancialInstrumentPrices.Domain.Contracts;
using FinancialInstrumentPrices.Domain.Models;
using System.Collections.Concurrent;

namespace FinancialInstrumentPrices.Infrastructure.Services;

public class InstrumentRepository : IInstrumentRepository
{
    #region Private Fields
    private ConcurrentDictionary<string, PriceDetails> _prices = new();
    private readonly List<string> CryptoInstruments =
        [
            ApplicationConstants.InstrumentNames.XRPUSDT,
            ApplicationConstants.InstrumentNames.DOGEUSDT,
            ApplicationConstants.InstrumentNames.BTCUSDT
        ];

    private readonly List<string> ForexInstruments =
        [
            ApplicationConstants.InstrumentNames.EURUSD,
            ApplicationConstants.InstrumentNames.JPYUSD
        ];
    #endregion

    #region Constructor
    public InstrumentRepository() => InitializePlaceholderData();
    #endregion

    #region Public Methods
    public IEnumerable<string> GetInstruments() => _prices.Keys;

    public IEnumerable<string> GetForexInstruments() => ForexInstruments;

    public IEnumerable<string> GetCryptoInstruments() => CryptoInstruments;

    public PriceDetails GetLatestPrice(string instrument) => _prices.TryGetValue(instrument.ToLower(), out var price) ? price : new(0, DateTime.UtcNow);

    public void UpdatePrice(string instrument, PriceDetails priceData) => _prices.AddOrUpdate(instrument.ToLower(), priceData, (_, oldValue) => priceData);
    #endregion

    #region Private Methods
    private void InitializePlaceholderData()
    {
        // Initialize with a few instruments.
        CryptoInstruments.ForEach(instrument =>
        {
            _prices.TryAdd(instrument, new(0m, DateTime.UtcNow));

        });

        ForexInstruments.ForEach(instrument =>
        {
            _prices.TryAdd(instrument, new(0m, DateTime.UtcNow));

        });
    }
    #endregion
}
