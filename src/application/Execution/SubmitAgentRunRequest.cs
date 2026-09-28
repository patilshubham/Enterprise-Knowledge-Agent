namespace EnterpriseKnowledgeAgent.Application.Execution;

/// <summary>TASK-RUN-01: the HTTP request body for <c>POST /api/applications/{appId}/agents/{id}/runs</c>.</summary>
public sealed record SubmitAgentRunRequest(string UserRequest, string? OutputTitle, string IdempotencyKey);
