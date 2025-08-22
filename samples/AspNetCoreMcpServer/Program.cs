using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using AspNetCoreMcpServer.Tools;
using AspNetCoreMcpServer.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ModelContextProtocol.Protocol;

var builder = WebApplication.CreateBuilder(args);

// Add authentication and authorization services
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddAuthorization();

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<EchoTool>()
    .WithTools<SampleLlmTool>()
    .WithResources<SimpleResourceType>()
    .UseListToolsFilter(next => async (context, cancellationToken) =>
    {
        // Authorization filter for list tools
        var result = await next(context, cancellationToken);

        // Filter out tools that the user is not authorized to see
        var authorizedTools = new List<Tool>();
        var policyProvider = context.Server.Services?.GetService<IAuthorizationPolicyProvider>();
        var authService = context.Server.Services?.GetService<IAuthorizationService>();
        var user = context.JsonRpcRequest.Context.User;

        if (policyProvider != null && authService != null && user != null)
        {
            foreach (var tool in result.Tools)
            {
                if (tool.McpServerTool?.Metadata != null)
                {
                    var authorizeData = tool.McpServerTool.Metadata.OfType<IAuthorizeData>().ToArray();
                    var allowAnonymous = tool.McpServerTool.Metadata.OfType<IAllowAnonymous>().Any();

                    // If the tool has AllowAnonymous, include it
                    if (allowAnonymous)
                    {
                        authorizedTools.Add(tool);
                        continue;
                    }

                    // If the tool has authorization requirements, check them
                    if (authorizeData.Length > 0)
                    {
                        try
                        {
                            var policy = await AuthorizationPolicy.CombineAsync(policyProvider!, authorizeData);
                            if (policy != null)
                            {
                                var authResult = await authService.AuthorizeAsync(user, policy);
                                if (authResult.Succeeded)
                                {
                                    authorizedTools.Add(tool);
                                }
                                // If authorization fails, omit the tool from the listing
                            }
                        }
                        catch
                        {
                            // If policy creation fails, exclude the tool
                            continue;
                        }
                    }
                    else
                    {
                        // No authorization metadata, include the tool
                        authorizedTools.Add(tool);
                    }
                }
                else
                {
                    // No metadata, include the tool
                    authorizedTools.Add(tool);
                }
            }
        }
        else
        {
            // No authorization provider, return all tools
            authorizedTools.AddRange(result.Tools);
        }

        Console.WriteLine($"Authorization filter: Listed {authorizedTools.Count} of {result.Tools?.Count ?? 0} tools");
        return new ListToolsResult { Tools = authorizedTools, NextCursor = result.NextCursor };
    })
    .UseCallToolFilter(next => async (context, cancellationToken) =>
    {
        // Authorization filter for call tool
        if (context.MatchedPrimitive?.Metadata != null)
        {
            var authorizeData = context.MatchedPrimitive.Metadata.OfType<IAuthorizeData>().ToArray();
            var allowAnonymous = context.MatchedPrimitive.Metadata.OfType<IAllowAnonymous>().Any();

            // If the tool allows anonymous access, proceed
            if (allowAnonymous)
            {
                Console.WriteLine($"Tool '{context.Params?.Name}' allows anonymous access");
                return await next(context, cancellationToken);
            }

            // If the tool has authorization requirements, check them
            if (authorizeData.Length > 0)
            {
                var policyProvider = context.Server.Services?.GetService<IAuthorizationPolicyProvider>();
                var authService = context.Server.Services?.GetService<IAuthorizationService>();
                var user = context.JsonRpcRequest.Context.User;

                if (policyProvider != null && authService != null && user != null)
                {
                    try
                    {
                        var policy = await AuthorizationPolicy.CombineAsync(policyProvider!, authorizeData);
                        if (policy != null)
                        {
                            var authResult = await authService.AuthorizeAsync(user, policy);
                            if (!authResult.Succeeded)
                            {
                                Console.WriteLine($"Access denied to tool '{context.Params?.Name}' - authorization failed");
                                return new CallToolResult
                                {
                                    Content = new[]
                                    {
                                        new TextContentBlock { Text = "Access forbidden: This tool requires authorization." }
                                    },
                                    IsError = true
                                };
                            }
                            Console.WriteLine($"Access granted to tool '{context.Params?.Name}' for user '{user.Identity?.Name}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Authorization error for tool '{context.Params?.Name}': {ex.Message}");
                        return new CallToolResult
                        {
                            Content = new[]
                            {
                                new TextContentBlock { Text = "Access forbidden: Authorization error occurred." }
                            },
                            IsError = true
                        };
                    }
                }
                else
                {
                    Console.WriteLine($"Access denied to tool '{context.Params?.Name}' - no authorization context");
                    return new CallToolResult
                    {
                        Content = new[]
                        {
                            new TextContentBlock { Text = "Access forbidden: No authorization context available." }
                        },
                        IsError = true
                    };
                }
            }
        }

        // No authorization metadata or no authorization provider, proceed with the call
        Console.WriteLine($"Calling tool '{context.Params?.Name}' - no authorization restrictions");
        return await next(context, cancellationToken);
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

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapMcp();

app.Run();
