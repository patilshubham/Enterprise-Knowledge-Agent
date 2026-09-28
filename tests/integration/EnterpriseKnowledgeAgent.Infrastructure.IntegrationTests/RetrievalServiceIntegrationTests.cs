using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Retrieval;
using EnterpriseKnowledgeAgent.Application.Tokenization;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Pgvector;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>
/// TASK-RET-01..05: end-to-end integration tests for <see cref="RetrievalService"/>
/// against a real Postgres/pgvector container - exercises the actual cosine
/// search + FTS SQL (TASK-PG-04/06) together with the service's own
/// scoping/threshold/fusion logic. <see cref="IEmbeddingService"/> is faked
/// (deterministic, caller-supplied vectors) since no live Azure OpenAI call
/// is available in CI; every other dependency is real.
/// </summary>
public sealed class RetrievalServiceIntegrationTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private const int Dimensions = 1536;
    private const string ConfiguredDeployment = "test-embedding-deployment";

    private sealed class FakeEmbeddingService(float[] vector) : IEmbeddingService
    {
        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, string deployment, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<float[]>>([.. inputs.Select(_ => vector)]);

        public Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class CharacterCountTokenEstimator : ITokenEstimator
    {
        public int EstimateTokenCount(string text, string? deployment = null) => text.Length;
    }

    private static Vector Embedding(float x, float y)
    {
        var values = new float[Dimensions];
        values[0] = x;
        values[1] = y;
        return new Vector(values);
    }

    /// <summary>Query vector for the fake embedding service - must match the pgvector column width (<see cref="Dimensions"/>), same as <see cref="Embedding"/> above.</summary>
    private static float[] QueryVector(float x, float y)
    {
        var values = new float[Dimensions];
        values[0] = x;
        values[1] = y;
        return values;
    }

    private static RetrievalService CreateService(
        EnterpriseKnowledgeAgentDbContext dbContext, float[] queryVector, string? embeddingDeployment = null) =>
        new(
            new KnowledgeChunkRepository(dbContext),
            new KnowledgeFileRepository(dbContext),
            new FakeEmbeddingService(queryVector),
            new CharacterCountTokenEstimator(),
            Options.Create(new AoiOptions
            {
                Endpoint = "https://placeholder.openai.azure.com/",
                ChatDeployment = "chat-deployment",
                EmbeddingDeployment = embeddingDeployment ?? ConfiguredDeployment,
                EmbeddingDimensions = Dimensions,
            }));

    private static RetrievalSettings DefaultSettings(
        int topK = 10,
        double minimumSimilarity = 0.0,
        double semanticWeight = 1.0,
        double keywordWeight = 0.0,
        bool includeNeighbors = false,
        int evidenceTokenBudget = 100_000,
        bool strictGrounding = true,
        RetrievalMode mode = RetrievalMode.Semantic) =>
        new(topK, minimumSimilarity, semanticWeight, keywordWeight, includeNeighbors, evidenceTokenBudget, strictGrounding, mode);

    private static async Task<KnowledgeFile> CreateReadyEnabledFileAsync(EnterpriseKnowledgeAgentDbContext dbContext, Guid applicationId, string displayName = "doc.md")
    {
        var file = KnowledgeFile.Create(applicationId, displayName, Guid.NewGuid().ToString(), 10, "text/markdown", "utf-8", "tester", DateTimeOffset.UtcNow);
        file.SetObjectKey($"applications/{applicationId}/files/{file.Id}/source");
        file.MarkProcessing(DateTimeOffset.UtcNow);
        file.MarkReady(DateTimeOffset.UtcNow);

        var repository = new KnowledgeFileRepository(dbContext);
        await repository.AddAsync(file);
        await repository.SaveChangesAsync();
        return file;
    }

    private static KnowledgeChunk NewActiveChunk(
        Guid applicationId, Guid fileId, int sequenceNumber, string headingPath, string content, Vector embedding, string deployment)
    {
        var chunk = KnowledgeChunk.Create(applicationId, fileId, sequenceNumber, headingPath, content, content.Length, 0, content.Length, Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);
        chunk.SetEmbedding(embedding, deployment, Dimensions);
        chunk.Activate();
        return chunk;
    }

    [Fact]
    public async Task RetrieveAsync_only_returns_evidence_from_the_requested_application()
    {
        var applicationId = Guid.NewGuid();
        var otherApplicationId = Guid.NewGuid();

        await using var setupContext = fixture.CreateDbContext();
        var file = await CreateReadyEnabledFileAsync(setupContext, applicationId);
        var otherFile = await CreateReadyEnabledFileAsync(setupContext, otherApplicationId);

        var chunk = NewActiveChunk(applicationId, file.Id, 0, "H1", "Content that belongs to application A.", Embedding(1, 0), ConfiguredDeployment);
        var otherChunk = NewActiveChunk(otherApplicationId, otherFile.Id, 0, "H1", "Content that belongs to application B.", Embedding(1, 0), ConfiguredDeployment);

        var chunkRepository = new KnowledgeChunkRepository(setupContext);
        await chunkRepository.AddRangeAsync([chunk, otherChunk]);
        await chunkRepository.SaveChangesAsync();

        await using var readContext = fixture.CreateDbContext();
        var service = CreateService(readContext, QueryVector(1, 0));

        var result = await service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings()));

        result.Sources.Should().ContainSingle();
        result.Sources[0].FileId.Should().Be(file.Id);
        result.Sources[0].Content.Should().Be("Content that belongs to application A.");
    }

    [Fact]
    public async Task RetrieveAsync_in_hybrid_mode_never_leaks_a_keyword_match_from_another_application()
    {
        var applicationId = Guid.NewGuid();
        var otherApplicationId = Guid.NewGuid();

        await using var setupContext = fixture.CreateDbContext();
        var file = await CreateReadyEnabledFileAsync(setupContext, applicationId);
        var otherFile = await CreateReadyEnabledFileAsync(setupContext, otherApplicationId);

        var chunk = NewActiveChunk(applicationId, file.Id, 0, "H1", "The zephyrology report covers application A findings.", Embedding(1, 0), ConfiguredDeployment);
        var otherChunk = NewActiveChunk(otherApplicationId, otherFile.Id, 0, "H1", "The zephyrology report covers application B findings.", Embedding(1, 0), ConfiguredDeployment);

        var chunkRepository = new KnowledgeChunkRepository(setupContext);
        await chunkRepository.AddRangeAsync([chunk, otherChunk]);
        await chunkRepository.SaveChangesAsync();

        await using var readContext = fixture.CreateDbContext();
        var service = CreateService(readContext, QueryVector(1, 0));

        var result = await service.RetrieveAsync(new RetrievalRequest(
            applicationId, "zephyrology", DefaultSettings(mode: RetrievalMode.Hybrid, semanticWeight: 0.5, keywordWeight: 0.5)));

        result.Sources.Should().ContainSingle();
        result.Sources[0].FileId.Should().Be(file.Id);
    }

    [Fact]
    public async Task RetrieveAsync_throws_a_configuration_exception_when_the_corpus_embedding_deployment_differs_from_the_configured_one()
    {
        var applicationId = Guid.NewGuid();

        await using var setupContext = fixture.CreateDbContext();
        var file = await CreateReadyEnabledFileAsync(setupContext, applicationId);
        var chunk = NewActiveChunk(applicationId, file.Id, 0, "H1", "Some content.", Embedding(1, 0), "old-deployment");

        var chunkRepository = new KnowledgeChunkRepository(setupContext);
        await chunkRepository.AddRangeAsync([chunk]);
        await chunkRepository.SaveChangesAsync();

        await using var readContext = fixture.CreateDbContext();
        var service = CreateService(readContext, QueryVector(1, 0), embeddingDeployment: "new-deployment");

        var act = () => service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings()));

        await act.Should().ThrowAsync<RetrievalConfigurationException>();
    }

    [Fact]
    public async Task RetrieveAsync_excludes_a_selected_file_that_is_not_ready_and_enabled_and_reports_it_as_excluded()
    {
        var applicationId = Guid.NewGuid();

        await using var setupContext = fixture.CreateDbContext();
        var readyFile = await CreateReadyEnabledFileAsync(setupContext, applicationId, "ready.md");
        var queuedFile = KnowledgeFile.Create(applicationId, "queued.md", Guid.NewGuid().ToString(), 10, "text/markdown", "utf-8", "tester", DateTimeOffset.UtcNow);
        var fileRepository = new KnowledgeFileRepository(setupContext);
        await fileRepository.AddAsync(queuedFile);
        await fileRepository.SaveChangesAsync();

        var chunk = NewActiveChunk(applicationId, readyFile.Id, 0, "H1", "Ready file content.", Embedding(1, 0), ConfiguredDeployment);
        var chunkRepository = new KnowledgeChunkRepository(setupContext);
        await chunkRepository.AddRangeAsync([chunk]);
        await chunkRepository.SaveChangesAsync();

        await using var readContext = fixture.CreateDbContext();
        var service = CreateService(readContext, QueryVector(1, 0));

        var result = await service.RetrieveAsync(new RetrievalRequest(
            applicationId, "query", DefaultSettings(), SelectedFileIds: [readyFile.Id, queuedFile.Id]));

        result.ExcludedFileIds.Should().ContainSingle().Which.Should().Be(queuedFile.Id);
        result.Sources.Should().ContainSingle();
        result.Sources[0].FileId.Should().Be(readyFile.Id);
    }

    [Fact]
    public async Task RetrieveAsync_assembles_an_evidence_package_with_source_ids_display_name_and_heading_path()
    {
        var applicationId = Guid.NewGuid();

        await using var setupContext = fixture.CreateDbContext();
        var file = await CreateReadyEnabledFileAsync(setupContext, applicationId, "handbook.md");
        var chunk = NewActiveChunk(applicationId, file.Id, 0, "Chapter 3 > Leave Policy", "Employees accrue leave monthly.", Embedding(1, 0), ConfiguredDeployment);

        var chunkRepository = new KnowledgeChunkRepository(setupContext);
        await chunkRepository.AddRangeAsync([chunk]);
        await chunkRepository.SaveChangesAsync();

        await using var readContext = fixture.CreateDbContext();
        var service = CreateService(readContext, QueryVector(1, 0));

        var result = await service.RetrieveAsync(new RetrievalRequest(applicationId, "leave policy", DefaultSettings()));

        result.HasEvidence.Should().BeTrue();
        var source = result.Sources.Should().ContainSingle().Subject;
        source.SourceId.Should().Be("SOURCE-001");
        source.DisplayName.Should().Be("handbook.md");
        source.HeadingPath.Should().Be("Chapter 3 > Leave Policy");
        source.Content.Should().Be("Employees accrue leave monthly.");
        source.TokenCount.Should().Be("Employees accrue leave monthly.".Length);
    }

    [Fact]
    public async Task RetrieveAsync_returns_no_evidence_when_the_application_has_no_ready_enabled_content()
    {
        var applicationId = Guid.NewGuid();

        await using var readContext = fixture.CreateDbContext();
        var service = CreateService(readContext, QueryVector(1, 0));

        var result = await service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings()));

        result.NoEvidence.Should().BeTrue();
        result.Sources.Should().BeEmpty();
    }
}
