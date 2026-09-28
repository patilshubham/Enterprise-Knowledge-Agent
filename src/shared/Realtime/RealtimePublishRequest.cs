using System.Text.Json;

namespace EnterpriseKnowledgeAgent.Shared.Realtime;

/// <summary>
/// TASK-SIG-03/04: wire contract for the internal, non-proxied
/// <c>POST /internal/realtime/publish</c> endpoint (Api layer) - the relay
/// point document-worker/agent-worker (separate OS processes from the Api,
/// so they cannot use <c>IHubContext</c> directly) call, post-commit, to fan
/// an already-built <see cref="SignalREvent{TPayload}"/> out to the correct
/// SignalR groups. <see cref="Envelope"/> is captured as a
/// <see cref="JsonElement"/> (rather than a generic/<c>object</c> property)
/// so it round-trips through <c>System.Text.Json</c> both when the worker
/// serializes the request AND when the Api model-binds it back, with no
/// polymorphic-type ambiguity - the Api never needs to know the payload's
/// concrete .NET type, it only relays the already-shaped JSON to clients.
/// </summary>
public sealed record RealtimePublishRequest(string EventType, IReadOnlyCollection<string> Groups, JsonElement Envelope);
