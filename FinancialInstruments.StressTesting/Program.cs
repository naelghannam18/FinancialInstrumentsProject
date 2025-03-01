using NBomber.CSharp;
using System;
using System.Net.WebSockets;
using System.Text;

namespace FinancialInstruments.StressTesting;

class Program
{
    static async Task Main(string[] args)
    {
        // Update this with your actual WebSocket server URI.
        var wsUri = new Uri("wss://localhost:7056/ws");

        // The subscription message to be sent by each connection.
        var subscribeMessage = "{ \"action\": \"subscribe\", \"instruments\": [\"btcusdt\", \"xrpusdt\"] }";

        // Define a scenario where each virtual user opens a connection,
        // sends the subscription message, and stays connected for 30 seconds.
        var scenario = Scenario.Create("websocket_subscribe", async context =>
        {
            using (var client = new ClientWebSocket())
            {
                try
                {
                    // 3) Continuously read messages
                    var buffer = new byte[1024 * 4];
                    // Connect to the WebSocket server.
                    await client.ConnectAsync(wsUri, CancellationToken.None);

                    if (client.State == WebSocketState.Open)
                    {
                        // Send the subscribe message.
                        var messageBytes = Encoding.UTF8.GetBytes(subscribeMessage);
                        await client.SendAsync(new ArraySegment<byte>(messageBytes),
                                               WebSocketMessageType.Text,
                                               true,
                                               CancellationToken.None);
                    }

                    await Task.Delay(30000);
                    var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    Console.WriteLine($"Received Message {message}");

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    return Response.Fail();
                }
                finally
                {
                    // Close the WebSocket connection gracefully.
                    if (client.State == WebSocketState.Open || client.State == WebSocketState.CloseReceived)
                    {
                        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", CancellationToken.None);
                    }
                }
            }

            return Response.Ok();
        })
        // This simulation will create 1000 concurrent virtual users (connections)
        // and keep them alive for 5 minutes.
        .WithLoadSimulations(new[]
        {
            Simulation.KeepConstant(copies: 1000, during: TimeSpan.FromMinutes(5))
        });

        // Run the NBomber test.
        var result = NBomberRunner.RegisterScenarios(scenario)
                                  .Run();

        Console.WriteLine("Load test completed.");
    }
}
