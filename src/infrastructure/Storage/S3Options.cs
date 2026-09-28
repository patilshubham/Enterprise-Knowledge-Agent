namespace EnterpriseKnowledgeAgent.Infrastructure.Storage;

/// <summary>TASK-S3-01: bucket names (bound from the <c>S3</c> config section).</summary>
public sealed class S3Options
{
    public const string SectionName = "S3";

    /// <summary>The private bucket backing <see cref="Application.Storage.IObjectStorage"/> (ASDD §11.1). The generated-exports bucket is out of scope until asynchronous exports are needed.</summary>
    public required string SourceBucket { get; init; }
}
