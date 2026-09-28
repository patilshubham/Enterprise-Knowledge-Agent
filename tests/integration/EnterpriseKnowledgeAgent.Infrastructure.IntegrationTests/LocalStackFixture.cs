using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>
/// TASK-S3-01/SQS-01: connects to the LocalStack instance already running via
/// docker-compose's "diagnostics" profile (<c>docker compose --profile diagnostics up -d localstack</c>),
/// exposed on the host at <c>http://localhost:4566</c> (see docker-compose.override.yml).
/// Unlike <see cref="PostgresDatabaseFixture"/>, this does not spin up a fresh Testcontainers
/// instance per test run - Testcontainers.LocalStack 4.x requires Testcontainers &gt;= 4.0.0,
/// which conflicts with the centrally-pinned Testcontainers 3.10.0 used by
/// Testcontainers.PostgreSql (NU1109 package downgrade). Reusing the compose LocalStack (whose
/// buckets/queues are already provisioned by deploy/localstack/init, TASK-FND-08) avoids that
/// version conflict; these tests require the compose stack to be up first. A plain static class
/// (not IClassFixture) since there is no per-test-run resource to create or dispose.
/// </summary>
public static class LocalStackFixture
{
    public const string SourceBucket = "rag-source-files";

    public const string DeletionJobsQueue = "deletion-jobs";

    private const string ServiceUrl = "http://localhost:4566";
    private const string Region = "us-east-1";
    private const string AccessKey = "test";
    private const string SecretKey = "test";

    public static IAmazonS3 CreateS3Client() => new AmazonS3Client(new BasicAWSCredentials(AccessKey, SecretKey), new AmazonS3Config
    {
        ServiceURL = ServiceUrl,
        AuthenticationRegion = Region,
        ForcePathStyle = true,
    });

    public static IAmazonSQS CreateSqsClient() => new AmazonSQSClient(new BasicAWSCredentials(AccessKey, SecretKey), new AmazonSQSConfig
    {
        ServiceURL = ServiceUrl,
        AuthenticationRegion = Region,
    });
}
