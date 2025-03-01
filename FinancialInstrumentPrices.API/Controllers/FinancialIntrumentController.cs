using FinancialInstrumentPrices.Domain.Contracts;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace FinancialInstrumentPrices.API.Controllers;

[ApiController]
[Route("[controller]")]
public class FinancialIntrumentController(IInstrumentRepository _instrumentRepository) : ControllerBase
{
    [HttpGet]
    [Route("available")]
    public IActionResult GetAvailableFinancialInstruments()
    
        => Ok(_instrumentRepository.GetInstruments());
    

    [HttpGet]
    [Route("instrument/{instrument:MinLength(1)}/latest-price")]
    public IActionResult GetAvailableFinancialInstruments(string instrument)
    
        => Ok(_instrumentRepository.GetLatestPrice(instrument));
    
}
