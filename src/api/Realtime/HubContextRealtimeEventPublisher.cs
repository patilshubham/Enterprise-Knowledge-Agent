using EnterpriseKnowledgeAgent.Application.Realtime;
using EnterpriseKnowledgeAgent.Shared.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Api.Realtime;

/// <summary>
/// TASK-SIG-03/04: the in-process <see cref="IRealtimeEventPublisher"/> used
/// only inside the Api itself - job/run *creation* (<c>FileQueued</c>/
/// <c>RunQueued</c>) happens synchronously during an API request
/// (<c>JobCreationService</c>), in the same process that hosts
/// <see cref="RealtimeHub"/>, so it can push directly via
/// <see cref="IHubContext{THub}"/> with no HTTP hop. Contrast with
/// <c>HttpRealtimeEventPublisher</c> (Infrastructure), used by the separate
/// document-worker/agent-worker processes.
/// </summary>
public sealed class HubContextRealtimeEventPublisher(
    IHubContext<RealtimeHub> hubContext, ILogger<HubContextRealtimeEventPublisher> logger) : IRealtimeEventPublisher
{
    public async Task PublishAsync<TPayload>(
        string eventType,
        IReadOnlyCollection<string> groups,
        SignalREvent<TPayload> envelope,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await hubContext.Clients.Groups(groups).SendAsync(eventType, envelope, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort (ASDD §18.4): SignalR delivery is never the source of truth -
            // a publish failure must not fail the caller's already-committed work.
            logger.RealtimePublishThrew(ex, eventType, envelope.EntityId);
        }
    }
}
