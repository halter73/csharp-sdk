using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using AspNetCoreMcpServer.Tools;
using AspNetCoreMcpServer.Resources;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<EchoTool>()
    .WithTools<SampleLlmTool>()
    .WithResources<SimpleResourceType>()
    .UseListToolsFilter(next => async (context, cancellationToken) =>
    {
        // Example filter that logs the list tools request
        Console.WriteLine($"Listing tools with params: {context.Params}");
        var result = await next(context, cancellationToken);
        Console.WriteLine($"Listed tools: {string.Join(", ", result.Tools.Select(t => t.McpServerTool?.Metadata))}");
        return result;
    })
    .UseCallToolFilter(next => (context, cancellationToken) =>
    {
        // Example filter that logs the call
        Console.WriteLine($"Calling tool '{context.MatchedPrimitive?.Id}' with params: {context.Params}");
        return next(context, cancellationToken);
    });

builder.Services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource("*")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(b => b.AddMeter("*")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithLogging()
    .UseOtlpExporter();

var app = builder.Build();

app.MapMcp();

app.Run();
