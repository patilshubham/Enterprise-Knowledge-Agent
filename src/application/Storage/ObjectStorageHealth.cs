namespace EnterpriseKnowledgeAgent.Application.Storage;

/// <summary>Result of <see cref="IObjectStorage.CheckHealthAsync"/> (TASK-S3-04).</summary>
public sealed record ObjectStorageHealth(bool IsAvailable, string? Detail);
