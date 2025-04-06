using Microsoft.AspNetCore.Builder;
using ModelContextProtocol.Tests.Utils;

namespace ModelContextProtocol.Tests.Server;

public class MapMcpTests(ITestOutputHelper testOutputHelper) : KestrelInMemoryTest(testOutputHelper)
{
    [Fact]
    public async Task Test_InMemory_Transport()
    {
        await using var app = Builder.Build();

        app.MapGet("/", () => "Hello World!");

        await app.StartAsync(TestContext.Current.CancellationToken);

        using var httpClient = GetHttpClient();
        var response = await httpClient.GetAsync("http://localhost/", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Equal("Hello World!", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
