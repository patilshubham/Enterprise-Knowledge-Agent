namespace EnterpriseKnowledgeAgent.Infrastructure.Storage;

/// <summary>
/// TASK-S3-01: LocalStack/AWS connection settings shared by S3 and SQS
/// (bound from the <c>Aws</c> config section - see docker-compose.yml's
/// <c>Aws__*</c> environment variables, sourced from .env's LOCALSTACK_*/
/// AWS_* values per TASK-FND-07).
/// </summary>
public sealed class AwsOptions
{
    public const string SectionName = "Aws";

    /// <summary>LocalStack endpoint, e.g. <c>http://localstack:4566</c>. Path-style addressing is required for LocalStack.</summary>
    public required string ServiceUrl { get; init; }

    public required string Region { get; init; }

    public required string AccessKey { get; init; }

    public required string SecretKey { get; init; }
}
