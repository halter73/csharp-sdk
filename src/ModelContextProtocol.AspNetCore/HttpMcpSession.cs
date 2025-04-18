using ModelContextProtocol.Server;
using System.Security.Claims;

namespace ModelContextProtocol.AspNetCore;

internal sealed class HttpMcpSession<TTransport>(string sessionId, TTransport transport, ClaimsPrincipal user)
{
    private int _referenceCount;

    public string Id { get; } = sessionId;
    public TTransport Transport { get; } = transport;
    public (string Type, string Value, string Issuer)? UserIdClaim { get; } = GetUserIdClaim(user);

    public bool IsActive => _referenceCount > 0;
    public long LastActivityTicks { get; private set; } = Environment.TickCount64;

    public IMcpServer? Server { get; init; }
    public Task? ServerRunTask { get; init; }

    public IDisposable AcquireReference()
    {
        Interlocked.Increment(ref _referenceCount);
        return new UnreferenceDisposable(this);
    }

    public bool HasSameUserId(ClaimsPrincipal user)
        => UserIdClaim == GetUserIdClaim(user);

    // SignalR only checks for ClaimTypes.NameIdentifier in HttpConnectionDispatcher, but AspNetCore.Antiforgery checks that plus the sub and UPN claims.
    // However, we short-circuit unlike antiforgery since we expect to call this to verify MCP messages a lot more frequently than
    // verifying antiforgery tokens from <form> posts.
    private static (string Type, string Value, string Issuer)? GetUserIdClaim(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var claim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.Upn);

        if (claim is { } idClaim)
        {
            return (idClaim.Type, idClaim.Value, idClaim.Issuer);
        }

        return null;
    }

    private sealed class UnreferenceDisposable(HttpMcpSession<TTransport> session) : IDisposable
    {
        public void Dispose()
        {
            if (Interlocked.Decrement(ref session._referenceCount) == 0)
            {
                session.LastActivityTicks = Environment.TickCount64;
            }
        }
    }
}
