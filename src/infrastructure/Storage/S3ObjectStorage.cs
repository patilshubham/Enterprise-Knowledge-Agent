using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EnterpriseKnowledgeAgent.Application.Storage;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Infrastructure.Storage;

/// <summary>
/// TASK-S3-01/S3-04: <see cref="IObjectStorage"/> backed by the AWS SDK S3
/// client configured for LocalStack (path-style addressing, custom
/// ServiceURL, fake credentials - ASDD §11).
/// </summary>
public sealed class S3ObjectStorage(IAmazonS3 s3Client, IOptions<S3Options> options) : IObjectStorage
{
    private readonly string bucket = options.Value.SourceBucket;

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            // Private by default (no ACL set) - LocalStack/S3 buckets never grant public access (TASK-S3-01).
            AutoCloseStream = false,
        };

        await s3Client.PutObjectAsync(request, cancellationToken);
    }

    public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await s3Client.GetObjectAsync(bucket, key, cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new ObjectNotFoundException(key);
        }
    }

    /// <summary>Idempotent: S3's DeleteObject succeeds (204) even when the key does not exist (TASK-S3-04).</summary>
    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        await s3Client.DeleteObjectAsync(bucket, key, cancellationToken);

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await s3Client.GetObjectMetadataAsync(bucket, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<ObjectStorageHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await s3Client.GetBucketLocationAsync(bucket, cancellationToken);
            return new ObjectStorageHealth(IsAvailable: true, Detail: null);
        }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException or HttpRequestException)
        {
            return new ObjectStorageHealth(IsAvailable: false, Detail: ex.Message);
        }
    }
}
