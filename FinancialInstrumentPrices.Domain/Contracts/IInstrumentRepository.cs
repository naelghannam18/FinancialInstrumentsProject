using FinancialInstrumentPrices.Domain.Models;

namespace FinancialInstrumentPrices.Domain.Contracts;

public interface IInstrumentRepository
{
    IEnumerable<string> GetInstruments();

    IEnumerable<string> GetCryptoInstruments();

    IEnumerable<string> GetForexInstruments();

    void UpdatePrice(string instrument, PriceDetails priceData);

    PriceDetails GetLatestPrice(string instrument);
}
