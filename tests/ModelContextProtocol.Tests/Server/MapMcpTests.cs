using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Tests.Utils;

namespace ModelContextProtocol.Tests.Server;

public class MapMcpTests(ITestOutputHelper testOutputHelper) : LoggedTest(testOutputHelper)
{
    [Fact]
    public async Task Test_InMemory_Transport()
    {
        await using var inMemoryTransport = new KestrelInMemoryTransport();

        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton<IConnectionListenerFactory>(inMemoryTransport);
        builder.Services.AddSingleton(LoggerProvider);
        await using var app = builder.Build();

        app.MapGet("/", () => "Hello World!");

        await app.StartAsync(TestContext.Current.CancellationToken);

        using var socketsHttpHandler = new SocketsHttpHandler()
        {
            ConnectCallback = (context, token) =>
            {
                var connection = inMemoryTransport.CreateConnection();
                return new(connection.ClientStream);
            },
        };

        using var httpClient = new HttpClient(socketsHttpHandler);
        var response = await httpClient.GetAsync("http://localhost/", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Equal("Hello World!", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
