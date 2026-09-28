namespace EnterpriseKnowledgeAgent.Shared.Realtime;

/// <summary>
/// TASK-SIG-01: the fixed SignalR group-name catalog (ASDD §18.2). Group
/// membership is managed server-side by <c>RealtimeHub</c> (Api layer) via
/// the join methods that call these builders - there is no
/// login/authorization in this app.
/// </summary>
public static class RealtimeGroups
{
    /// <summary>Restricted to <c>SuperAdmin</c>/<c>SuperViewer</c> platform roles (TASK-AUTHZ-03).</summary>
    public const string OperationsGroup = "platform:operations";

    /// <summary>Auto-bound on hub connect from the authenticated principal's own subject claim - never joined via a client-supplied id.</summary>
    public static string User(Guid userId) => $"user:{userId}";

    public static string Application(Guid applicationId) => $"application:{applicationId}";

    public static string Run(Guid runId) => $"run:{runId}";
}
