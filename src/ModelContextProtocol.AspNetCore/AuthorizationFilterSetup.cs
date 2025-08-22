using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.AspNetCore;

/// <summary>
/// Evaluates authorization policies from endpoint metadata.
/// </summary>
internal sealed class AuthorizationFilterSetup(IAuthorizationPolicyProvider policyProvider) : IConfigureOptions<McpServerOptions>
{
    public void Configure(McpServerOptions options)
    {
        // Configure authorization filters for tools
        ConfigureListToolsFilter(options);
        ConfigureCallToolFilter(options);

        // Configure authorization filters for resources
        ConfigureListResourcesFilter(options);
        ConfigureListResourceTemplatesFilter(options);
        ConfigureReadResourceFilter(options);

        // Configure authorization filters for prompts
        ConfigureListPromptsFilter(options);
        ConfigureGetPromptFilter(options);
    }

    private void ConfigureListToolsFilter(McpServerOptions options)
    {
        options.Filters.ListToolsFilters.Add(next => async (context, cancellationToken) =>
        {
            var result = await next(context, cancellationToken);

            // Filter out tools that the user is not authorized to see
            var authorizedTools = new List<Tool>();
            var authService = context.Server.Services?.GetService<IAuthorizationService>();
            var user = context.User;

            if (policyProvider != null && authService != null && user != null)
            {
                foreach (var tool in result.Tools)
                {
                    if (tool.McpServerTool?.Metadata != null)
                    {
                        var policy = await GetEffectivePolicyAsync(policyProvider, tool.McpServerTool.Metadata);
                        if (policy == null)
                        {
                            // No authorization required or AllowAnonymous
                            authorizedTools.Add(tool);
                        }
                        else
                        {
                            var authResult = await authService.AuthorizeAsync(user, context, policy);
                            if (authResult.Succeeded)
                            {
                                authorizedTools.Add(tool);
                            }
                            // If authorization fails, omit the tool from the listing
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

            return new ListToolsResult { Tools = authorizedTools, NextCursor = result.NextCursor };
        });
    }

    private void ConfigureCallToolFilter(McpServerOptions options)
    {
        options.Filters.CallToolFilters.Add(next => async (context, cancellationToken) =>
        {
            // Authorization filter for call tool
            if (context.MatchedPrimitive?.Metadata != null)
            {
                var authService = context.Server.Services?.GetService<IAuthorizationService>();
                var user = context.User;

                if (policyProvider != null && authService != null && user != null)
                {
                    var policy = await GetEffectivePolicyAsync(policyProvider, context.MatchedPrimitive.Metadata);
                    if (policy != null)
                    {
                        var authResult = await authService.AuthorizeAsync(user, context, policy);
                        if (!authResult.Succeeded)
                        {
                            return new CallToolResult
                            {
                                Content = new[]
                                {
                                    new TextContentBlock { Text = "Access forbidden: This tool requires authorization." }
                                },
                                IsError = true
                            };
                        }
                    }
                }
            }

            return await next(context, cancellationToken);
        });
    }

    private void ConfigureListResourcesFilter(McpServerOptions options)
    {
        options.Filters.ListResourcesFilters.Add(next => async (context, cancellationToken) =>
        {
            var result = await next(context, cancellationToken);

            // Filter out resources that the user is not authorized to see
            var authorizedResources = new List<Resource>();
            var authService = context.Server.Services?.GetService<IAuthorizationService>();
            var user = context.User;

            if (policyProvider != null && authService != null && user != null)
            {
                foreach (var resource in result.Resources)
                {
                    if (resource.McpServerResource?.Metadata != null)
                    {
                        var policy = await GetEffectivePolicyAsync(policyProvider, resource.McpServerResource.Metadata);
                        if (policy == null)
                        {
                            // No authorization required or AllowAnonymous
                            authorizedResources.Add(resource);
                        }
                        else
                        {
                            var authResult = await authService.AuthorizeAsync(user, context, policy);
                            if (authResult.Succeeded)
                            {
                                authorizedResources.Add(resource);
                            }
                            // If authorization fails, omit the resource from the listing
                        }
                    }
                    else
                    {
                        // No metadata, include the resource
                        authorizedResources.Add(resource);
                    }
                }
            }
            else
            {
                // No authorization provider, return all resources
                authorizedResources.AddRange(result.Resources);
            }

            return new ListResourcesResult { Resources = authorizedResources, NextCursor = result.NextCursor };
        });
    }

    private void ConfigureListResourceTemplatesFilter(McpServerOptions options)
    {
        options.Filters.ListResourceTemplatesFilters.Add(next => async (context, cancellationToken) =>
        {
            var result = await next(context, cancellationToken);

            // Filter out resource templates that the user is not authorized to see
            var authorizedResourceTemplates = new List<ResourceTemplate>();
            var authService = context.Server.Services?.GetService<IAuthorizationService>();
            var user = context.User;

            if (policyProvider != null && authService != null && user != null)
            {
                foreach (var resourceTemplate in result.ResourceTemplates)
                {
                    if (resourceTemplate.McpServerResource?.Metadata != null)
                    {
                        var policy = await GetEffectivePolicyAsync(policyProvider, resourceTemplate.McpServerResource.Metadata);
                        if (policy == null)
                        {
                            // No authorization required or AllowAnonymous
                            authorizedResourceTemplates.Add(resourceTemplate);
                        }
                        else
                        {
                            var authResult = await authService.AuthorizeAsync(user, context, policy);
                            if (authResult.Succeeded)
                            {
                                authorizedResourceTemplates.Add(resourceTemplate);
                            }
                            // If authorization fails, omit the resource template from the listing
                        }
                    }
                    else
                    {
                        // No metadata, include the resource template
                        authorizedResourceTemplates.Add(resourceTemplate);
                    }
                }
            }
            else
            {
                // No authorization provider, return all resource templates
                authorizedResourceTemplates.AddRange(result.ResourceTemplates);
            }

            return new ListResourceTemplatesResult { ResourceTemplates = authorizedResourceTemplates, NextCursor = result.NextCursor };
        });
    }

    private void ConfigureReadResourceFilter(McpServerOptions options)
    {
        options.Filters.ReadResourceFilters.Add(next => async (context, cancellationToken) =>
        {
            // Authorization filter for read resource
            if (context.MatchedPrimitive?.Metadata != null)
            {
                var authService = context.Server.Services?.GetService<IAuthorizationService>();
                var user = context.User;

                if (policyProvider != null && authService != null && user != null)
                {
                    var policy = await GetEffectivePolicyAsync(policyProvider, context.MatchedPrimitive.Metadata);
                    if (policy != null)
                    {
                        var authResult = await authService.AuthorizeAsync(user, context, policy);
                        if (!authResult.Succeeded)
                        {
                            return new ReadResourceResult
                            {
                                Contents = new[]
                                {
                                    new TextResourceContents { Text = "Access forbidden: This resource requires authorization." }
                                }
                            };
                        }
                    }
                }
            }

            return await next(context, cancellationToken);
        });
    }

    private void ConfigureListPromptsFilter(McpServerOptions options)
    {
        options.Filters.ListPromptsFilters.Add(next => async (context, cancellationToken) =>
        {
            var result = await next(context, cancellationToken);

            // Filter out prompts that the user is not authorized to see
            var authorizedPrompts = new List<Prompt>();
            var authService = context.Server.Services?.GetService<IAuthorizationService>();
            var user = context.User;

            if (policyProvider != null && authService != null && user != null)
            {
                foreach (var prompt in result.Prompts)
                {
                    if (prompt.McpServerPrompt?.Metadata != null)
                    {
                        var policy = await GetEffectivePolicyAsync(policyProvider, prompt.McpServerPrompt.Metadata);
                        if (policy == null)
                        {
                            // No authorization required or AllowAnonymous
                            authorizedPrompts.Add(prompt);
                        }
                        else
                        {
                            var authResult = await authService.AuthorizeAsync(user, context, policy);
                            if (authResult.Succeeded)
                            {
                                authorizedPrompts.Add(prompt);
                            }
                            // If authorization fails, omit the prompt from the listing
                        }
                    }
                    else
                    {
                        // No metadata, include the prompt
                        authorizedPrompts.Add(prompt);
                    }
                }
            }
            else
            {
                // No authorization provider, return all prompts
                authorizedPrompts.AddRange(result.Prompts);
            }

            return new ListPromptsResult { Prompts = authorizedPrompts, NextCursor = result.NextCursor };
        });
    }

    private void ConfigureGetPromptFilter(McpServerOptions options)
    {
        options.Filters.GetPromptFilters.Add(next => async (context, cancellationToken) =>
        {
            // Authorization filter for get prompt
            if (context.MatchedPrimitive?.Metadata != null)
            {
                var authService = context.Server.Services?.GetService<IAuthorizationService>();
                var user = context.User;

                if (policyProvider != null && authService != null && user != null)
                {
                    var policy = await GetEffectivePolicyAsync(policyProvider, context.MatchedPrimitive.Metadata);
                    if (policy != null)
                    {
                        var authResult = await authService.AuthorizeAsync(user, context, policy);
                        if (!authResult.Succeeded)
                        {
                            return new GetPromptResult
                            {
                                Description = "Access forbidden: This prompt requires authorization.",
                                Messages = new[]
                                {
                                    new PromptMessage
                                    {
                                        Role = Role.User,
                                        Content = new TextContentBlock { Text = "Access forbidden: This prompt requires authorization." }
                                    }
                                }
                            };
                        }
                    }
                }
            }

            return await next(context, cancellationToken);
        });
    }

    private async ValueTask<AuthorizationResult> GetAuthorizationResultAsync<TParams>(RequestContext<TParams> context)
    {
        var policy = await GetEffectivePolicyAsync(policyProvider, context.MatchedPrimitive?.Metadata ?? []);
        if (policy is not null)
        {
            if (context.Services is null)
            {
                throw new InvalidOperationException("Request context does not have services available for authorization.");
            }

            // ASP.NET Core's AuthorizationMiddleware resolves the IAuthorizationService from scoped request services, so we do the same.
            var authService = context.Services.GetRequiredService<IAuthorizationService>();
            return await authService.AuthorizeAsync(context.User ?? new ClaimsPrincipal(new ClaimsIdentity()), context, policy);
        }

        return AuthorizationResult.Success();
    }

    /// <summary>
    /// Combines authorization policies and requirements from endpoint metadata without considering <see cref="IAllowAnonymous"/>.
    /// </summary>
    /// <param name="policyProvider">The authorization policy provider.</param>
    /// <param name="endpointMetadata">The endpoint metadata collection.</param>
    /// <returns>The combined authorization policy, or null if no authorization is required.</returns>
    private static async ValueTask<AuthorizationPolicy?> CombineAsync(IAuthorizationPolicyProvider policyProvider, IEnumerable<object> endpointMetadata)
    {
        // https://github.com/dotnet/aspnetcore/issues/63365 tracks adding this as public API to AuthorizationPolicy itself.
        // Copied from https://github.com/dotnet/aspnetcore/blob/9f2977bf9cfb539820983bda3bedf81c8cda9f20/src/Security/Authorization/Policy/src/AuthorizationMiddleware.cs#L116-L138
        var authorizeData = endpointMetadata.OfType<IAuthorizeData>().ToArray();
        var policies = endpointMetadata.OfType<AuthorizationPolicy>().ToArray();

        var policy = await AuthorizationPolicy.CombineAsync(policyProvider, authorizeData, policies);

        var requirementData = endpointMetadata.OfType<IAuthorizationRequirementData>().ToArray();
        if (requirementData.Length > 0)
        {
            var reqPolicy = new AuthorizationPolicyBuilder();
            foreach (var rd in requirementData)
            {
                foreach (var r in rd.GetRequirements())
                {
                    reqPolicy.AddRequirements(r);
                }
            }

            // Combine policy with requirements or just use requirements if no policy
            policy = (policy is null)
                ? reqPolicy.Build()
                : AuthorizationPolicy.Combine(policy, reqPolicy.Build());
        }

        return policy;
    }

    /// <summary>
    /// Gets the effective authorization policy for the specified endpoint metadata. This will return null if metadata contains <see cref="IAllowAnonymous"/>.
    /// </summary>
    /// <param name="policyProvider">The authorization policy provider.</param>
    /// <param name="endpointMetadata">The endpoint metadata collection.</param>
    /// <returns>The combined authorization policy, or null if no authorization is required.</returns>
    private static ValueTask<AuthorizationPolicy?> GetEffectivePolicyAsync(IAuthorizationPolicyProvider policyProvider, IEnumerable<object> endpointMetadata)
    {
        if (endpointMetadata.Any(static m => m is IAllowAnonymous))
        {
            return default;
        }

        return CombineAsync(policyProvider, endpointMetadata);
    }
}
