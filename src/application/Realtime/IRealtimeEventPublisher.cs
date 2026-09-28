using EnterpriseKnowledgeAgent.Shared.Realtime;

namespace EnterpriseKnowledgeAgent.Application.Realtime;

/// <summary>
/// TASK-SIG-03/04: the single publish abstraction both orchestrators (this
/// layer) and API-layer job/run creation call after every relevant
/// post-commit <c>SaveChangesAsync</c> (ASDD §18.4 - never before
/// persistence). Two implementations exist: an in-process one used inside
/// the Api itself (job/run creation happens synchronously in an API
/// request, so it can call <c>IHubContext</c> directly), and an HTTP-relay
/// one used by the document-worker/agent-worker processes (which cannot use
/// <c>IHubContext</c> since they don't host the hub). Callers never see
/// which; a failed/unreachable publish is always swallowed internally
/// (logged, not thrown) since SignalR delivery is never the source of truth
/// - the caller's already-committed work must never be affected by it.
/// </summary>
public interface IRealtimeEventPublisher
{
    Task PublishAsync<TPayload>(
        string eventType,
        IReadOnlyCollection<string> groups,
        SignalREvent<TPayload> envelope,
        CancellationToken cancellationToken = default);
}
