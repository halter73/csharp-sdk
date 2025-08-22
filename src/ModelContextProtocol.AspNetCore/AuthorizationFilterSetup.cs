using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.AspNetCore;

/// <summary>
/// Evaluates authorization policies from endpoint metadata.
/// </summary>
internal sealed class AuthorizationFilterSetup : IConfigureOptions<McpServerOptions>
{
    private static readonly Task<AuthorizationPolicy?> s_nullPolicyTask = Task.FromResult<AuthorizationPolicy?>(null);

    public void Configure(McpServerOptions options)
    {
    }

    /// <summary>
    /// Combines authorization policies and requirements from endpoint metadata without considering <see cref="IAllowAnonymous"/>.
    /// </summary>
    /// <param name="policyProvider">The authorization policy provider.</param>
    /// <param name="endpointMetadata">The endpoint metadata collection.</param>
    /// <returns>The combined authorization policy, or null if no authorization is required.</returns>
    private static async Task<AuthorizationPolicy?> CombineAsync(IAuthorizationPolicyProvider policyProvider, IEnumerable<object> endpointMetadata)
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
    public static Task<AuthorizationPolicy?> GetEffectivePolicyAsync(IAuthorizationPolicyProvider policyProvider, IEnumerable<object> endpointMetadata)
    {
        if (endpointMetadata.Any(static m => m is IAllowAnonymous))
        {
            return s_nullPolicyTask;
        }

        return CombineAsync(policyProvider, endpointMetadata);
    }

}
