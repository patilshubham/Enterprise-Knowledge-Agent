namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// Source-generated log messages for the TASK-AGT-02 agent lifecycle audit
/// hooks (mirrors the TASK-APP-02 pattern in <see cref="ApplicationAuditLog"/>).
/// TASK-AUD-01 formalizes these into the durable audit schema/store; until
/// then this is the auditable trail the endpoints emit.
/// </summary>
internal static partial class AgentAuditLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "agent.created applicationId={ApplicationId} agentId={AgentId} name={Name} actor={Actor}")]
    public static partial void Created(this ILogger logger, Guid applicationId, Guid agentId, string name, string actor);

    [LoggerMessage(Level = LogLevel.Information, Message = "agent.updated applicationId={ApplicationId} agentId={AgentId} actor={Actor}")]
    public static partial void Updated(this ILogger logger, Guid applicationId, Guid agentId, string actor);

    [LoggerMessage(Level = LogLevel.Information, Message = "agent.enabled applicationId={ApplicationId} agentId={AgentId} actor={Actor}")]
    public static partial void Enabled(this ILogger logger, Guid applicationId, Guid agentId, string actor);

    [LoggerMessage(Level = LogLevel.Information, Message = "agent.disabled applicationId={ApplicationId} agentId={AgentId} actor={Actor}")]
    public static partial void Disabled(this ILogger logger, Guid applicationId, Guid agentId, string actor);

    [LoggerMessage(Level = LogLevel.Information, Message = "agent.deleted applicationId={ApplicationId} agentId={AgentId} actor={Actor}")]
    public static partial void Deleted(this ILogger logger, Guid applicationId, Guid agentId, string actor);
}
