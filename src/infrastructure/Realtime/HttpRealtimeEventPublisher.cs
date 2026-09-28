using System.Net.Http.Json;
using System.Text.Json;
using EnterpriseKnowledgeAgent.Application.Realtime;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Realtime;
using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Infrastructure.Realtime;

/// <summary>
/// TASK-SIG-03/04: worker-side (document-worker/agent-worker) publisher -
/// these run as separate OS processes from the Api and so cannot use
/// <c>IHubContext</c> directly. Instead, each publish is relayed via a
/// short-timeout HTTP POST to the Api's internal, non-nginx-proxied
/// <c>/internal/realtime/publish</c> endpoint, authenticated with a shared-
/// secret header (see <see cref="RealtimeOptions.InternalToken"/>) since
/// docker-compose still publishes the Api container's port directly for
/// local dev - network topology alone cannot be trusted as access control
/// (OWASP A01). Failures are logged and swallowed, never thrown: per ASDD
/// §18.4, SignalR delivery is best-effort and must never fail or retry the
/// caller's already-committed work (lost events are recovered by the
/// frontend's REST refresh, TASK-SIG-06).
/// </summary>
public sealed class HttpRealtimeEventPublisher(HttpClient httpClient, ILogger<HttpRealtimeEventPublisher> logger) : IRealtimeEventPublisher
{
    private const string PublishPath = "internal/realtime/publish";

    public async Task PublishAsync<TPayload>(
        string eventType,
        IReadOnlyCollection<string> groups,
        SignalREvent<TPayload> envelope,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new RealtimePublishRequest(eventType, groups, JsonSerializer.SerializeToElement(envelope));
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, PublishPath)
            {
                Content = JsonContent.Create(request),
            };

            // TASK-OBS-01: forward this worker message's correlation id so
            // the Api's CorrelationIdMiddleware picks it up for the relay
            // request instead of minting an unrelated one - keeps the
            // "Worker → SignalR event" hop traceable end-to-end.
            var correlationId = CorrelationContext.CorrelationId;
            if (correlationId is not null)
            {
                requestMessage.Headers.Add(CorrelationContext.HeaderName, correlationId.Value.ToString());
            }

            using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.RealtimePublishFailedWithStatus(eventType, envelope.EntityId, (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.RealtimePublishThrew(ex, eventType, envelope.EntityId);
        }
    }
}
