using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Retrieval;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Application.Tests.Retrieval;

/// <summary>TASK-RET-01..05: unit tests for <see cref="RetrievalService"/> orchestration.</summary>
public sealed class RetrievalServiceTests
{
    private const string ConfiguredDeployment = "text-embedding-3-small";

    private sealed class Harness
    {
        public FakeRetrievalChunkRepository ChunkRepository { get; } = new();
        public FakeRetrievalFileRepository FileRepository { get; } = new();
        public FakeRetrievalEmbeddingService EmbeddingService { get; } = new();
        public FakeRetrievalTokenEstimator TokenEstimator { get; } = new();

        public RetrievalService CreateService() => new(
            ChunkRepository,
            FileRepository,
            EmbeddingService,
            TokenEstimator,
            Options.Create(new AoiOptions
            {
                Endpoint = "https://placeholder.openai.azure.com/",
                ChatDeployment = "gpt-4o",
                EmbeddingDeployment = ConfiguredDeployment,
                EmbeddingDimensions = 4,
            }));
    }

    private static RetrievalSettings DefaultSettings(
        int topK = 10,
        double minimumSimilarity = 0.0,
        double semanticWeight = 1.0,
        double keywordWeight = 0.0,
        bool includeNeighbors = false,
        int evidenceTokenBudget = 10_000,
        bool strictGrounding = true,
        RetrievalMode mode = RetrievalMode.Semantic) =>
        new(topK, minimumSimilarity, semanticWeight, keywordWeight, includeNeighbors, evidenceTokenBudget, strictGrounding, mode);

    [Fact]
    public async Task RetrieveAsync_throws_a_configuration_exception_when_the_corpus_embedding_deployment_differs_from_the_configured_one()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        harness.ChunkRepository.DistinctDeployments = new HashSet<string> { "some-other-deployment" };
        var service = harness.CreateService();

        var act = () => service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings()));

        await act.Should().ThrowAsync<RetrievalConfigurationException>();
    }

    [Fact]
    public async Task RetrieveAsync_throws_a_configuration_exception_when_the_corpus_has_more_than_one_distinct_embedding_deployment()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        harness.ChunkRepository.DistinctDeployments = new HashSet<string> { ConfiguredDeployment, "some-other-deployment" };
        var service = harness.CreateService();

        var act = () => service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings()));

        await act.Should().ThrowAsync<RetrievalConfigurationException>();
    }

    [Fact]
    public async Task RetrieveAsync_does_not_throw_when_the_corpus_has_no_embedded_chunks_yet()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        harness.ChunkRepository.DistinctDeployments = new HashSet<string>();
        var service = harness.CreateService();

        var result = await service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings()));

        result.NoEvidence.Should().BeTrue();
    }

    [Fact]
    public async Task RetrieveAsync_does_not_throw_when_the_corpus_deployment_matches_the_configured_one()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        harness.ChunkRepository.DistinctDeployments = new HashSet<string> { ConfiguredDeployment };
        var service = harness.CreateService();

        var result = await service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings()));

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RetrieveAsync_returns_no_evidence_when_no_candidates_are_found()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        harness.ChunkRepository.SemanticResults = [];
        var service = harness.CreateService();

        var result = await service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings()));

        result.NoEvidence.Should().BeTrue();
        result.HasEvidence.Should().BeFalse();
        result.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_scopes_the_semantic_and_deployment_queries_to_the_requested_application()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        var service = harness.CreateService();

        await service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings()));

        harness.ChunkRepository.LastSemanticSearchApplicationId.Should().Be(applicationId);
        harness.ChunkRepository.LastDeploymentsApplicationId.Should().Be(applicationId);
    }

    [Fact]
    public async Task RetrieveAsync_embeds_the_query_using_the_configured_embedding_deployment()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        var service = harness.CreateService();

        await service.RetrieveAsync(new RetrievalRequest(applicationId, "what is the refund policy?", DefaultSettings()));

        harness.EmbeddingService.Calls.Should().ContainSingle();
        harness.EmbeddingService.Calls[0].Inputs.Should().ContainSingle("what is the refund policy?");
        harness.EmbeddingService.Calls[0].Deployment.Should().Be(ConfiguredDeployment);
    }

    [Fact]
    public async Task RetrieveAsync_assembles_evidence_sources_with_sequential_source_ids_and_file_metadata()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        var file = RetrievalTestFileFactory.CreateReadyFile(applicationId, "policy.pdf");
        harness.FileRepository.Files.Add(file);

        var chunk = RetrievalTestFileFactory.CreateChunk(applicationId, file.Id, 0, "Chapter 1", "Refunds are processed within 30 days.", 0, 40);
        harness.ChunkRepository.SemanticResults = [new(chunk, Distance: 0.1)];

        var service = harness.CreateService();
        var result = await service.RetrieveAsync(new RetrievalRequest(applicationId, "refund policy", DefaultSettings()));

        result.HasEvidence.Should().BeTrue();
        result.Sources.Should().ContainSingle();
        var source = result.Sources[0];
        source.SourceId.Should().Be("SOURCE-001");
        source.FileId.Should().Be(file.Id);
        source.DisplayName.Should().Be("policy.pdf");
        source.HeadingPath.Should().Be("Chapter 1");
        source.Content.Should().Be("Refunds are processed within 30 days.");
    }

    [Fact]
    public async Task RetrieveAsync_only_searches_the_selected_files_when_selected_file_ids_are_supplied()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        var fileA = RetrievalTestFileFactory.CreateReadyFile(applicationId, "a.txt");
        var fileB = RetrievalTestFileFactory.CreateReadyFile(applicationId, "b.txt");
        harness.FileRepository.Files.Add(fileA);
        harness.FileRepository.Files.Add(fileB);

        var chunkA = RetrievalTestFileFactory.CreateChunk(applicationId, fileA.Id, 0, "H1", "Content from file A.", 0, 20);
        var chunkB = RetrievalTestFileFactory.CreateChunk(applicationId, fileB.Id, 0, "H1", "Content from file B.", 0, 20);
        harness.ChunkRepository.SemanticResults = [new(chunkA, Distance: 0.2), new(chunkB, Distance: 0.1)];

        var service = harness.CreateService();
        var result = await service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings(), SelectedFileIds: [fileA.Id]));

        result.Sources.Should().ContainSingle();
        result.Sources[0].FileId.Should().Be(fileA.Id);
    }

    [Fact]
    public async Task RetrieveAsync_reports_excluded_files_that_are_in_scope_but_not_ready()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        var readyFile = RetrievalTestFileFactory.CreateReadyFile(applicationId, "ready.txt");
        var processingFile = KnowledgeFile.Create(applicationId, "processing.txt", Guid.NewGuid().ToString("N"), 10, "text/plain", "utf-8", "tester", DateTimeOffset.UtcNow);
        harness.FileRepository.Files.Add(readyFile);
        harness.FileRepository.Files.Add(processingFile);

        var service = harness.CreateService();
        var result = await service.RetrieveAsync(new RetrievalRequest(
            applicationId, "query", DefaultSettings(), SelectedFileIds: [readyFile.Id, processingFile.Id]));

        result.ExcludedFileIds.Should().ContainSingle().Which.Should().Be(processingFile.Id);
    }

    [Fact]
    public async Task RetrieveAsync_reports_excluded_files_that_are_disabled()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        var disabledFile = RetrievalTestFileFactory.CreateReadyFile(applicationId, "disabled.txt");
        disabledFile.Disable(DateTimeOffset.UtcNow);
        harness.FileRepository.Files.Add(disabledFile);

        var service = harness.CreateService();
        var result = await service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings(), SelectedFileIds: [disabledFile.Id]));

        result.ExcludedFileIds.Should().ContainSingle().Which.Should().Be(disabledFile.Id);
    }

    [Fact]
    public async Task RetrieveAsync_does_not_call_full_text_search_in_semantic_only_mode()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        var service = harness.CreateService();

        await service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings(mode: RetrievalMode.Semantic)));

        harness.ChunkRepository.FullTextSearchWasCalled.Should().BeFalse();
    }

    [Fact]
    public async Task RetrieveAsync_calls_full_text_search_in_hybrid_mode_and_fuses_the_results()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        var file = RetrievalTestFileFactory.CreateReadyFile(applicationId);
        harness.FileRepository.Files.Add(file);

        var semanticChunk = RetrievalTestFileFactory.CreateChunk(applicationId, file.Id, 0, "H1", "Semantic match content.", 0, 30);
        var keywordChunk = RetrievalTestFileFactory.CreateChunk(applicationId, file.Id, 5, "H2", "Keyword match content.", 500, 530);
        harness.ChunkRepository.SemanticResults = [new(semanticChunk, Distance: 0.1)];
        harness.ChunkRepository.KeywordResults = [new(keywordChunk, Rank: 0.9)];

        var service = harness.CreateService();
        var result = await service.RetrieveAsync(new RetrievalRequest(
            applicationId, "query", DefaultSettings(mode: RetrievalMode.Hybrid, semanticWeight: 0.5, keywordWeight: 0.5)));

        harness.ChunkRepository.FullTextSearchWasCalled.Should().BeTrue();
        result.Sources.Select(s => s.Content).Should().BeEquivalentTo(
            ["Semantic match content.", "Keyword match content."]);
    }

    [Fact]
    public async Task RetrieveAsync_excludes_candidates_below_the_minimum_similarity_threshold()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        var file = RetrievalTestFileFactory.CreateReadyFile(applicationId);
        harness.FileRepository.Files.Add(file);

        // Distance 0.95 -> similarity 0.05, below a 0.5 threshold.
        var chunk = RetrievalTestFileFactory.CreateChunk(applicationId, file.Id, 0, "H1", "Weak match.", 0, 20);
        harness.ChunkRepository.SemanticResults = [new(chunk, Distance: 0.95)];

        var service = harness.CreateService();
        var result = await service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings(minimumSimilarity: 0.5)));

        result.NoEvidence.Should().BeTrue();
        result.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_suppresses_exact_duplicate_content_across_candidates()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        var file = RetrievalTestFileFactory.CreateReadyFile(applicationId);
        harness.FileRepository.Files.Add(file);

        var sharedHash = "duplicate-hash";
        var best = RetrievalTestFileFactory.CreateChunk(applicationId, file.Id, 0, "H1", "Duplicated content.", 0, 20, sharedHash);
        var worse = RetrievalTestFileFactory.CreateChunk(applicationId, file.Id, 9, "H9", "Duplicated content.", 900, 920, sharedHash);
        harness.ChunkRepository.SemanticResults = [new(best, Distance: 0.05), new(worse, Distance: 0.4)];

        var service = harness.CreateService();
        var result = await service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings()));

        result.Sources.Should().ContainSingle();
    }

    [Fact]
    public async Task RetrieveAsync_includes_neighboring_chunks_when_include_neighbors_is_set()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        var file = RetrievalTestFileFactory.CreateReadyFile(applicationId);
        harness.FileRepository.Files.Add(file);

        var matched = RetrievalTestFileFactory.CreateChunk(applicationId, file.Id, 5, "H1", "Matched chunk.", 500, 520);
        var neighbor = RetrievalTestFileFactory.CreateChunk(applicationId, file.Id, 6, "H1", "Neighbor chunk.", 520, 540);
        harness.ChunkRepository.SemanticResults = [new(matched, Distance: 0.1)];
        harness.ChunkRepository.ChunksByFile[file.Id] = [matched, neighbor];

        var service = harness.CreateService();
        var result = await service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings(includeNeighbors: true)));

        result.Sources.Select(s => s.Content).Should().Contain(["Matched chunk.", "Neighbor chunk."]);
    }

    [Fact]
    public async Task RetrieveAsync_does_not_fetch_neighbors_when_include_neighbors_is_not_set()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        var file = RetrievalTestFileFactory.CreateReadyFile(applicationId);
        harness.FileRepository.Files.Add(file);

        var matched = RetrievalTestFileFactory.CreateChunk(applicationId, file.Id, 5, "H1", "Matched chunk.", 500, 520);
        var neighbor = RetrievalTestFileFactory.CreateChunk(applicationId, file.Id, 6, "H1", "Neighbor chunk.", 520, 540);
        harness.ChunkRepository.SemanticResults = [new(matched, Distance: 0.1)];
        harness.ChunkRepository.ChunksByFile[file.Id] = [matched, neighbor];

        var service = harness.CreateService();
        var result = await service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings(includeNeighbors: false)));

        result.Sources.Should().ContainSingle();
        result.Sources[0].Content.Should().Be("Matched chunk.");
    }

    [Fact]
    public async Task RetrieveAsync_stops_including_candidates_once_the_evidence_token_budget_is_exhausted()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        var file = RetrievalTestFileFactory.CreateReadyFile(applicationId);
        harness.FileRepository.Files.Add(file);

        var fits = RetrievalTestFileFactory.CreateChunk(applicationId, file.Id, 0, "H1", new string('a', 10), 0, 10);
        var tooBig = RetrievalTestFileFactory.CreateChunk(applicationId, file.Id, 1, "H2", new string('b', 100), 100, 200);
        harness.ChunkRepository.SemanticResults = [new(fits, Distance: 0.05), new(tooBig, Distance: 0.06)];

        var service = harness.CreateService();
        var result = await service.RetrieveAsync(new RetrievalRequest(applicationId, "query", DefaultSettings(evidenceTokenBudget: 10)));

        result.Sources.Should().ContainSingle();
        result.Sources[0].Content.Should().Be(new string('a', 10));
    }
}
