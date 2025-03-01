using FinancialInstrumentPrices.Domain.Contracts;
using FinancialInstrumentPrices.Domain.Options;
using FinancialInstrumentPrices.Infrastructure.BackgroundServices;
using FinancialInstrumentPrices.Infrastructure.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

#region Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File($"logs/{DateTime.UtcNow:dd-MM-yyyy}.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();
#endregion

#region Swagger Service and controller resolving
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
#endregion

#region Register infrasturcture Services
builder.Services.AddSingleton<IInstrumentRepository, InstrumentRepository>();
builder.Services.AddSingleton<IWebSocketHandler, WebSocketHandler>();
builder.Services.AddHostedService<CryptoPriceUpdaterService>();
builder.Services.AddHostedService<ForexPriceUpdaterService>();
#endregion

#region Register Options
builder.Services.Configure<DataSourcesOptions>(builder.Configuration.GetSection("DataSources"));
#endregion

var app = builder.Build();

app.UseWebSockets();
// Here Im keeping the swagger UI for testing purposes
#region Swagger
app.UseSwagger();
app.UseSwaggerUI();
#endregion

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

#region WebSocket Configuration
// WebSocket endpoint for live price updates.
// Clients should connect to ws://<host>:<port>/ws
app.Map("/ws", async (HttpContext context, IWebSocketHandler wsHandler) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await wsHandler.HandleWebSocketAsync(webSocket, context.RequestAborted);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
}); 
#endregion

app.Run();
