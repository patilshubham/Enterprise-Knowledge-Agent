using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Embeddings;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Tokenization;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Pgvector;

namespace EnterpriseKnowledgeAgent.Application.Tests.Embeddings;

/// <summary>TASK-EMB-01/02/03: unit tests for the real Azure OpenAI-backed embedding pipeline stage.</summary>
public sealed class AzureOpenAiEmbeddingPipelineStageTests
{
    private const int Dimensions = 4;

    private sealed class FakeChunkRepository : IKnowledgeChunkRepository
    {
        public readonly List<KnowledgeChunk> Chunks = [];
        public bool Activated;
        public int SaveChangesCallCount;

        public Task AddRangeAsync(IEnumerable<KnowledgeChunk> chunks, CancellationToken cancellationToken = default)
        {
            Chunks.AddRange(chunks);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<KnowledgeChunk>> ListByFileAsync(Guid fileId, bool activeOnly = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeChunk>>(Chunks.Where(c => c.FileId == fileId && (!activeOnly || c.IsActive)).ToList());

        public Task<IReadOnlySet<string>> ListContentHashesByFileAsync(Guid fileId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(Chunks.Where(c => c.FileId == fileId).Select(c => c.ContentHash).ToHashSet());

        public Task ActivateAllForFileAsync(Guid fileId, CancellationToken cancellationToken = default)
        {
            Activated = true;
            foreach (var chunk in Chunks.Where(c => c.FileId == fileId))
            {
                chunk.Activate();
            }

            return Task.CompletedTask;
        }

        public Task DeleteAllForFileAsync(Guid fileId, CancellationToken cancellationToken = default)
        {
            Chunks.RemoveAll(c => c.FileId == fileId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<KnowledgeChunkSearchResult>> SearchByCosineSimilarityAsync(
            Guid applicationId, Vector queryEmbedding, int topK, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeChunkSearchResult>>([]);

        public Task DeleteInactiveForFileAsync(Guid fileId, CancellationToken cancellationToken = default)
        {
            Chunks.RemoveAll(c => c.FileId == fileId && !c.IsActive);
            return Task.CompletedTask;
        }

        public Task SwapReplacementAsync(Guid fileId, CancellationToken cancellationToken = default)
        {
            Chunks.RemoveAll(c => c.FileId == fileId && c.IsActive);
            foreach (var chunk in Chunks.Where(c => c.FileId == fileId && !c.IsActive))
            {
                chunk.Activate();
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlySet<string>> ListDistinctEmbeddingDeploymentsAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(Chunks.Where(c => c.ApplicationId == applicationId && c.IsActive && c.EmbeddingDeployment is not null)
                .Select(c => c.EmbeddingDeployment!).ToHashSet());

        public Task<IReadOnlyList<KnowledgeChunkKeywordSearchResult>> SearchByFullTextAsync(
            Guid applicationId, string query, int topK, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeChunkKeywordSearchResult>>([]);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref SaveChangesCallCount);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEmbeddingService(Func<IReadOnlyList<string>, IReadOnlyList<float[]>>? respond = null, Exception? throwOn = null) : IEmbeddingService
    {
        public readonly List<IReadOnlyList<string>> Calls = [];

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, string deployment, CancellationToken cancellationToken = default)
        {
            lock (Calls)
            {
                Calls.Add(inputs);
            }

            if (throwOn is not null)
            {
                throw throwOn;
            }

            var vectors = respond?.Invoke(inputs) ?? inputs.Select(_ => new float[Dimensions]).ToList();
            return Task.FromResult<IReadOnlyList<float[]>>(vectors);
        }

        public Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class WordCountTokenEstimator : ITokenEstimator
    {
        public int EstimateTokenCount(string text, string? deployment = null) => 1;
    }

    private static KnowledgeChunk NewChunk(Guid fileId, int sequenceNumber, string content) =>
        KnowledgeChunk.Create(Guid.NewGuid(), fileId, sequenceNumber, "H1", content, content.Length, 0, content.Length, $"hash-{fileId}-{sequenceNumber}", DateTimeOffset.UtcNow);

    private static AzureOpenAiEmbeddingPipelineStage CreateStage(
        FakeChunkRepository repository, FakeEmbeddingService embeddingService, EmbeddingOptions? embeddingOptions = null) =>
        new(
            repository,
            embeddingService,
            new WordCountTokenEstimator(),
            Options.Create(new AoiOptions
            {
                Endpoint = "https://placeholder.openai.azure.com/",
                ChatDeployment = "gpt-4o",
                EmbeddingDeployment = "text-embedding-3-small",
                EmbeddingDimensions = Dimensions,
            }),
            Options.Create(embeddingOptions ?? new EmbeddingOptions()));

    [Fact]
    public async Task RunAsync_generates_and_persists_embeddings_then_activates_all_chunks()
    {
        var fileId = Guid.NewGuid();
        var repository = new FakeChunkRepository();
        repository.Chunks.Add(NewChunk(fileId, 0, "first chunk"));
        repository.Chunks.Add(NewChunk(fileId, 1, "second chunk"));
        var embeddingService = new FakeEmbeddingService();
        var stage = CreateStage(repository, embeddingService);

        await stage.RunAsync(Guid.NewGuid(), fileId);

        repository.Chunks.Should().OnlyContain(c => c.Embedding != null);
        repository.Chunks.Should().OnlyContain(c => c.IsActive);
        repository.Activated.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_is_idempotent_and_skips_chunks_that_already_have_an_embedding()
    {
        var fileId = Guid.NewGuid();
        var repository = new FakeChunkRepository();
        var alreadyEmbedded = NewChunk(fileId, 0, "already done");
        alreadyEmbedded.SetEmbedding(new Vector(new float[Dimensions]), "previous-deployment", Dimensions);
        repository.Chunks.Add(alreadyEmbedded);
        repository.Chunks.Add(NewChunk(fileId, 1, "still pending"));
        var embeddingService = new FakeEmbeddingService();
        var stage = CreateStage(repository, embeddingService);

        await stage.RunAsync(Guid.NewGuid(), fileId);

        embeddingService.Calls.Should().ContainSingle();
        embeddingService.Calls[0].Should().Equal("still pending");
        alreadyEmbedded.EmbeddingDeployment.Should().Be("previous-deployment");
    }

    [Fact]
    public async Task RunAsync_activates_without_calling_the_embedding_service_when_every_chunk_is_already_embedded()
    {
        var fileId = Guid.NewGuid();
        var repository = new FakeChunkRepository();
        var chunk = NewChunk(fileId, 0, "done");
        chunk.SetEmbedding(new Vector(new float[Dimensions]), "previous-deployment", Dimensions);
        repository.Chunks.Add(chunk);
        var embeddingService = new FakeEmbeddingService();
        var stage = CreateStage(repository, embeddingService);

        var progressReports = new List<double>();
        await stage.RunAsync(Guid.NewGuid(), fileId, (fraction, _) =>
        {
            progressReports.Add(fraction);
            return Task.CompletedTask;
        });

        embeddingService.Calls.Should().BeEmpty();
        repository.Activated.Should().BeTrue();
        progressReports.Should().Equal(1.0);
    }

    [Fact]
    public async Task RunAsync_reports_progress_once_per_batch()
    {
        var fileId = Guid.NewGuid();
        var repository = new FakeChunkRepository();
        for (var i = 0; i < 4; i++)
        {
            repository.Chunks.Add(NewChunk(fileId, i, $"chunk {i}"));
        }

        var embeddingService = new FakeEmbeddingService();
        var stage = CreateStage(repository, embeddingService, new EmbeddingOptions { MaxInputsPerBatch = 1, MaxConcurrentBatches = 1 });

        var progressReports = new List<double>();
        await stage.RunAsync(Guid.NewGuid(), fileId, (fraction, _) =>
        {
            progressReports.Add(fraction);
            return Task.CompletedTask;
        });

        progressReports.Should().HaveCount(4);
        progressReports.Should().Equal(0.25, 0.5, 0.75, 1.0);
    }

    [Fact]
    public async Task RunAsync_throws_EmbeddingValidationException_when_the_service_returns_the_wrong_vector_count()
    {
        var fileId = Guid.NewGuid();
        var repository = new FakeChunkRepository();
        repository.Chunks.Add(NewChunk(fileId, 0, "one"));
        repository.Chunks.Add(NewChunk(fileId, 1, "two"));
        var embeddingService = new FakeEmbeddingService(inputs => [new float[Dimensions]]);
        var stage = CreateStage(repository, embeddingService);

        var act = () => stage.RunAsync(Guid.NewGuid(), fileId);

        await act.Should().ThrowAsync<EmbeddingValidationException>();
        repository.Activated.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_throws_EmbeddingValidationException_when_a_returned_vector_has_the_wrong_dimension()
    {
        var fileId = Guid.NewGuid();
        var repository = new FakeChunkRepository();
        repository.Chunks.Add(NewChunk(fileId, 0, "one"));
        var embeddingService = new FakeEmbeddingService(inputs => inputs.Select(_ => new float[Dimensions + 1]).ToList());
        var stage = CreateStage(repository, embeddingService);

        var act = () => stage.RunAsync(Guid.NewGuid(), fileId);

        await act.Should().ThrowAsync<EmbeddingValidationException>();
        repository.Activated.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_throws_EmbeddingValidationException_when_a_returned_vector_contains_a_non_finite_value()
    {
        var fileId = Guid.NewGuid();
        var repository = new FakeChunkRepository();
        repository.Chunks.Add(NewChunk(fileId, 0, "one"));
        var embeddingService = new FakeEmbeddingService(inputs => inputs.Select(_ => new[] { float.NaN, 0f, 0f, 0f }).ToList());
        var stage = CreateStage(repository, embeddingService);

        var act = () => stage.RunAsync(Guid.NewGuid(), fileId);

        await act.Should().ThrowAsync<EmbeddingValidationException>();
        repository.Activated.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_propagates_AzureOpenAiException_from_the_embedding_service_without_activating()
    {
        var fileId = Guid.NewGuid();
        var repository = new FakeChunkRepository();
        repository.Chunks.Add(NewChunk(fileId, 0, "one"));
        var embeddingService = new FakeEmbeddingService(throwOn: new AzureOpenAiException(
            EnterpriseKnowledgeAgent.Shared.Errors.ErrorCategory.DependencyUnavailable, "boom"));
        var stage = CreateStage(repository, embeddingService);

        var act = () => stage.RunAsync(Guid.NewGuid(), fileId);

        await act.Should().ThrowAsync<AzureOpenAiException>();
        repository.Activated.Should().BeFalse();
    }
}
