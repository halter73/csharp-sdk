using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace ModelContextProtocol.AspNetCore;

/// <summary>
/// Evaluates authorization policies from endpoint metadata.
/// </summary>
internal static class AuthorizationPolicyMetadataEvaluator
{
    /// <summary>
    /// Combines authorization policies and requirements from endpoint metadata.
    /// </summary>
    /// <param name="policyProvider">The authorization policy provider.</param>
    /// <param name="endpointMetadata">The endpoint metadata collection.</param>
    /// <returns>The combined authorization policy, or null if no authorization is required.</returns>
    public static async Task<AuthorizationPolicy?> CombineAsync(IAuthorizationPolicyProvider policyProvider, IEnumerable<object> endpointMetadata)
    {
        // https://github.com/dotnet/aspnetcore/issues/63365 tracks adding this as public API to AuthorizationPolicy itself.
        // Copied from https://github.com/dotnet/aspnetcore/blob/9f2977bf9cfb539820983bda3bedf81c8cda9f20/src/Security/Authorization/Policy/src/AuthorizationMiddleware.cs#L116-L138
        // TODO: Cache policies using the IMcpServerPrimitive object as the key.
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
}
