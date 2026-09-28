using System.Security.Cryptography;
using System.Text;
using EnterpriseKnowledgeAgent.Api.Realtime;
using EnterpriseKnowledgeAgent.Infrastructure.Realtime;
using EnterpriseKnowledgeAgent.Shared.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// TASK-SIG-03/04: the single relay point document-worker/agent-worker
/// (separate OS processes from this Api, so they cannot use
/// <see cref="IHubContext{THub}"/> directly) call, post-commit, to fan an
/// already-built event envelope out to the hub's groups. Deliberately NOT
/// mapped under <c>/api/</c> and NOT proxied by nginx (frontend/nginx.conf
/// only forwards <c>/api/</c>, <c>/hubs/</c>, <c>/health/</c>) and has no
/// <c>RequireAuthorization()</c> (workers hold no browser session cookie) -
/// instead protected by a constant-time shared-secret header comparison.
/// This is required, not optional defense-in-depth: docker-compose still
/// publishes this container's port directly to the host for local dev
/// (<c>API_HTTP_PORT</c>), so "only reachable from other containers" cannot
/// be assumed (OWASP A01 - broken access control).
/// </summary>
public static class RealtimeEndpoints
{
    private const string InternalTokenHeader = "X-Internal-Token";

    public static IEndpointRouteBuilder MapRealtimeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/realtime/publish", async (
            HttpContext context,
            RealtimePublishRequest request,
            IHubContext<RealtimeHub> hubContext,
            IOptions<RealtimeOptions> options,
            CancellationToken cancellationToken) =>
        {
            if (!IsAuthorized(context, options.Value.InternalToken))
            {
                return Results.Unauthorized();
            }

            if (request.Groups.Count == 0 || string.IsNullOrWhiteSpace(request.EventType))
            {
                return Results.BadRequest();
            }

            await hubContext.Clients.Groups(request.Groups).SendAsync(request.EventType, request.Envelope, cancellationToken);
            return Results.Accepted();
        });

        return endpoints;
    }

    private static bool IsAuthorized(HttpContext context, string expectedToken)
    {
        if (string.IsNullOrEmpty(expectedToken)
            || !context.Request.Headers.TryGetValue(InternalTokenHeader, out var providedValues))
        {
            return false;
        }

        var provided = providedValues.ToString();
        // Constant-time comparison: this is a shared secret, not a public
        // identifier, so a naive `==` would leak timing information about
        // how many leading bytes matched (OWASP A02/A07).
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expectedToken));
    }
}
