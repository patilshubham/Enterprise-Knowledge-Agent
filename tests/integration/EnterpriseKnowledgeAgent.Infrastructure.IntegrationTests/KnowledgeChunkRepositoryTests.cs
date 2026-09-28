using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using FluentAssertions;
using Pgvector;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-CHK-02: persistence and query tests for <see cref="KnowledgeChunkRepository"/> against a real Postgres container.</summary>
public sealed class KnowledgeChunkRepositoryTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private static KnowledgeChunk NewChunk(
        Guid applicationId, Guid fileId, int sequenceNumber, string content, string contentHash) =>
        KnowledgeChunk.Create(
            applicationId, fileId, sequenceNumber, "H1", content, content.Length, 0, content.Length, contentHash, DateTimeOffset.UtcNow);

    /// <summary>TASK-PG-02: the column is <c>vector(1536)</c> - every test vector must have exactly that many dimensions. Only the first two components vary; the rest are zero-padding.</summary>
    private static Vector Embedding(float x, float y)
    {
        var values = new float[KnowledgeChunkConfigurationDimensions];
        values[0] = x;
        values[1] = y;
        return new Vector(values);
    }

    private const int KnowledgeChunkConfigurationDimensions = 1536;

    private static async Task<KnowledgeFile> CreateReadyEnabledFileAsync(EnterpriseKnowledgeAgentDbContext dbContext, Guid applicationId)
    {
        var file = KnowledgeFile.Create(applicationId, "doc.md", Guid.NewGuid().ToString(), 10, "text/markdown", "utf-8", "sub", DateTimeOffset.UtcNow);
        file.SetObjectKey($"applications/{applicationId}/files/{file.Id}/source");
        file.MarkProcessing(DateTimeOffset.UtcNow);
        file.MarkReady(DateTimeOffset.UtcNow);

        var repository = new KnowledgeFileRepository(dbContext);
        await repository.AddAsync(file);
        await repository.SaveChangesAsync();
        return file;
    }

    [Fact]
    public async Task AddRangeAsync_persists_chunks_inactive_by_default()
    {
        var applicationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var chunks = new[]
        {
            NewChunk(applicationId, fileId, 0, "first", "hash-a1"),
            NewChunk(applicationId, fileId, 1, "second", "hash-a2"),
        };

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeChunkRepository(writeContext);
            await repository.AddRangeAsync(chunks);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var loaded = await new KnowledgeChunkRepository(readContext).ListByFileAsync(fileId);

        loaded.Should().HaveCount(2);
        loaded.Should().OnlyContain(c => !c.IsActive);
    }

    [Fact]
    public async Task ListByFileAsync_returns_chunks_ordered_by_sequence_number()
    {
        var applicationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var chunks = new[]
        {
            NewChunk(applicationId, fileId, 2, "third", "hash-b3"),
            NewChunk(applicationId, fileId, 0, "first", "hash-b1"),
            NewChunk(applicationId, fileId, 1, "second", "hash-b2"),
        };

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeChunkRepository(writeContext);
            await repository.AddRangeAsync(chunks);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var loaded = await new KnowledgeChunkRepository(readContext).ListByFileAsync(fileId);

        loaded.Select(c => c.SequenceNumber).Should().Equal(0, 1, 2);
    }

    [Fact]
    public async Task ListByFileAsync_activeOnly_excludes_inactive_chunks()
    {
        var applicationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var active = NewChunk(applicationId, fileId, 0, "active", "hash-c1");
        active.Activate();
        var inactive = NewChunk(applicationId, fileId, 1, "inactive", "hash-c2");

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeChunkRepository(writeContext);
            await repository.AddRangeAsync([active, inactive]);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var loaded = await new KnowledgeChunkRepository(readContext).ListByFileAsync(fileId, activeOnly: true);

        loaded.Should().ContainSingle(c => c.ContentHash == "hash-c1");
    }

    [Fact]
    public async Task ListContentHashesByFileAsync_returns_hashes_of_inactive_chunks_for_the_file()
    {
        var applicationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var chunks = new[]
        {
            NewChunk(applicationId, fileId, 0, "one", "hash-d1"),
            NewChunk(applicationId, fileId, 1, "two", "hash-d2"),
        };

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeChunkRepository(writeContext);
            await repository.AddRangeAsync(chunks);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var hashes = await new KnowledgeChunkRepository(readContext).ListContentHashesByFileAsync(fileId);

        hashes.Should().BeEquivalentTo(["hash-d1", "hash-d2"]);
    }

    /// <summary>
    /// TASK-REP-02: this scoping is what lets replacement processing safely
    /// re-create chunks for content that is unchanged between file versions -
    /// old chunks are always active, so they never suppress the new
    /// generation's re-creation of matching content.
    /// </summary>
    [Fact]
    public async Task ListContentHashesByFileAsync_excludes_active_chunk_hashes()
    {
        var applicationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var active = NewChunk(applicationId, fileId, 0, "old-active", "hash-d3");
        active.Activate();
        var inactive = NewChunk(applicationId, fileId, 1, "new-inactive", "hash-d4");

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeChunkRepository(writeContext);
            await repository.AddRangeAsync([active, inactive]);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var hashes = await new KnowledgeChunkRepository(readContext).ListContentHashesByFileAsync(fileId);

        hashes.Should().BeEquivalentTo(["hash-d4"]);
    }

    [Fact]
    public async Task DeleteInactiveForFileAsync_removes_only_inactive_chunks_for_that_file()
    {
        var applicationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var active = NewChunk(applicationId, fileId, 0, "active", "hash-j1");
        active.Activate();
        var inactive = NewChunk(applicationId, fileId, 1, "inactive", "hash-j2");
        var otherFileId = Guid.NewGuid();
        var otherFileInactive = NewChunk(applicationId, otherFileId, 0, "other", "hash-j3");

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeChunkRepository(writeContext);
            await repository.AddRangeAsync([active, inactive, otherFileInactive]);
            await repository.SaveChangesAsync();
        }

        await using (var deleteContext = fixture.CreateDbContext())
        {
            await new KnowledgeChunkRepository(deleteContext).DeleteInactiveForFileAsync(fileId);
        }

        await using var readContext = fixture.CreateDbContext();
        var repository2 = new KnowledgeChunkRepository(readContext);
        (await repository2.ListByFileAsync(fileId)).Should().ContainSingle(c => c.ContentHash == "hash-j1");
        (await repository2.ListByFileAsync(otherFileId)).Should().ContainSingle();
    }

    [Fact]
    public async Task SwapReplacementAsync_deletes_old_active_generation_and_activates_new_inactive_generation()
    {
        var applicationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var oldActive = NewChunk(applicationId, fileId, 0, "old", "hash-k1");
        oldActive.Activate();
        var newInactive = NewChunk(applicationId, fileId, 0, "new", "hash-k2");
        var otherFileId = Guid.NewGuid();
        var otherActive = NewChunk(applicationId, otherFileId, 0, "other-active", "hash-k3");
        otherActive.Activate();

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeChunkRepository(writeContext);
            await repository.AddRangeAsync([oldActive, newInactive, otherActive]);
            await repository.SaveChangesAsync();
        }

        await using (var swapContext = fixture.CreateDbContext())
        {
            await new KnowledgeChunkRepository(swapContext).SwapReplacementAsync(fileId);
        }

        await using var readContext = fixture.CreateDbContext();
        var repository2 = new KnowledgeChunkRepository(readContext);
        var afterSwap = await repository2.ListByFileAsync(fileId);
        afterSwap.Should().ContainSingle(c => c.ContentHash == "hash-k2" && c.IsActive);

        // Untouched: the other file's active chunk is not affected by an unrelated file's swap.
        (await repository2.ListByFileAsync(otherFileId)).Should().ContainSingle(c => c.IsActive);
    }

    [Fact]
    public async Task ListDistinctEmbeddingDeploymentsAsync_returns_distinct_deployments_for_active_chunks_in_the_application()
    {
        var applicationId = Guid.NewGuid();
        var otherApplicationId = Guid.NewGuid();

        await using var setupContext = fixture.CreateDbContext();
        var file = await CreateReadyEnabledFileAsync(setupContext, applicationId);

        var chunkA = NewChunk(applicationId, file.Id, 0, "a", "hash-l1");
        chunkA.SetEmbedding(Embedding(1, 0), "deployment-a", KnowledgeChunkConfigurationDimensions);
        chunkA.Activate();

        var chunkB = NewChunk(applicationId, file.Id, 1, "b", "hash-l2");
        chunkB.SetEmbedding(Embedding(0, 1), "deployment-a", KnowledgeChunkConfigurationDimensions);
        chunkB.Activate();

        var chunkC = NewChunk(applicationId, file.Id, 2, "c", "hash-l3");
        chunkC.SetEmbedding(Embedding(-1, 0), "deployment-b", KnowledgeChunkConfigurationDimensions);
        chunkC.Activate();

        var inactiveChunk = NewChunk(applicationId, file.Id, 3, "d", "hash-l4");
        inactiveChunk.SetEmbedding(Embedding(0, -1), "deployment-c", KnowledgeChunkConfigurationDimensions);
        // Not activated - excluded.

        var otherApplicationFile = await CreateReadyEnabledFileAsync(setupContext, otherApplicationId);
        var otherApplicationChunk = NewChunk(otherApplicationId, otherApplicationFile.Id, 0, "e", "hash-l5");
        otherApplicationChunk.SetEmbedding(Embedding(1, 0), "deployment-other-app", KnowledgeChunkConfigurationDimensions);
        otherApplicationChunk.Activate();

        var chunkRepository = new KnowledgeChunkRepository(setupContext);
        await chunkRepository.AddRangeAsync([chunkA, chunkB, chunkC, inactiveChunk, otherApplicationChunk]);
        await chunkRepository.SaveChangesAsync();

        await using var readContext = fixture.CreateDbContext();
        var deployments = await new KnowledgeChunkRepository(readContext).ListDistinctEmbeddingDeploymentsAsync(applicationId);

        deployments.Should().BeEquivalentTo(["deployment-a", "deployment-b"]);
    }

    [Fact]
    public async Task SearchByFullTextAsync_ranks_the_best_keyword_match_first_and_excludes_ineligible_chunks()
    {
        var applicationId = Guid.NewGuid();
        var otherApplicationId = Guid.NewGuid();

        await using var setupContext = fixture.CreateDbContext();
        var file = await CreateReadyEnabledFileAsync(setupContext, applicationId);

        var stronglyMatching = KnowledgeChunk.Create(
            applicationId, file.Id, 0, "H1", "elephants elephants elephants roam the savanna", 8, 0, 40, "hash-m1", DateTimeOffset.UtcNow);
        stronglyMatching.Activate();

        var weaklyMatching = KnowledgeChunk.Create(
            applicationId, file.Id, 1, "H1", "a single elephant was seen once", 6, 0, 30, "hash-m2", DateTimeOffset.UtcNow);
        weaklyMatching.Activate();

        var noMatch = KnowledgeChunk.Create(
            applicationId, file.Id, 2, "H1", "completely unrelated content about oceans", 6, 0, 40, "hash-m3", DateTimeOffset.UtcNow);
        noMatch.Activate();

        var inactive = KnowledgeChunk.Create(
            applicationId, file.Id, 3, "H1", "elephants elephants elephants elephants", 4, 0, 40, "hash-m4", DateTimeOffset.UtcNow);
        // Not activated - excluded.

        var otherApplicationFile = await CreateReadyEnabledFileAsync(setupContext, otherApplicationId);
        var otherApplicationChunk = KnowledgeChunk.Create(
            otherApplicationId, otherApplicationFile.Id, 0, "H1", "elephants elephants elephants elephants", 4, 0, 40, "hash-m5", DateTimeOffset.UtcNow);
        otherApplicationChunk.Activate();

        var chunkRepository = new KnowledgeChunkRepository(setupContext);
        await chunkRepository.AddRangeAsync([stronglyMatching, weaklyMatching, noMatch, inactive, otherApplicationChunk]);
        await chunkRepository.SaveChangesAsync();

        await using var readContext = fixture.CreateDbContext();
        var results = await new KnowledgeChunkRepository(readContext).SearchByFullTextAsync(applicationId, "elephants", topK: 10);

        results.Select(r => r.Chunk.ContentHash).Should().Equal("hash-m1", "hash-m2");
        results.Select(r => r.Rank).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task ActivateAllForFileAsync_flips_only_that_files_inactive_chunks()
    {
        var applicationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var otherFileId = Guid.NewGuid();
        var mine = NewChunk(applicationId, fileId, 0, "mine", "hash-e1");
        var other = NewChunk(applicationId, otherFileId, 0, "other", "hash-e2");

        await using (var writeContext = fixture.CreateDbContext())
        {
            var writeRepository = new KnowledgeChunkRepository(writeContext);
            await writeRepository.AddRangeAsync([mine, other]);
            await writeRepository.SaveChangesAsync();
        }

        await using (var activateContext = fixture.CreateDbContext())
        {
            await new KnowledgeChunkRepository(activateContext).ActivateAllForFileAsync(fileId);
        }

        await using var readContext = fixture.CreateDbContext();
        var repository = new KnowledgeChunkRepository(readContext);
        (await repository.ListByFileAsync(fileId)).Should().OnlyContain(c => c.IsActive);
        (await repository.ListByFileAsync(otherFileId)).Should().OnlyContain(c => !c.IsActive);
    }

    [Fact]
    public async Task DeleteAllForFileAsync_removes_every_chunk_for_that_file_only()
    {
        var applicationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var otherFileId = Guid.NewGuid();
        var mine = NewChunk(applicationId, fileId, 0, "mine", "hash-f1");
        var other = NewChunk(applicationId, otherFileId, 0, "other", "hash-f2");

        await using (var writeContext = fixture.CreateDbContext())
        {
            var writeRepository = new KnowledgeChunkRepository(writeContext);
            await writeRepository.AddRangeAsync([mine, other]);
            await writeRepository.SaveChangesAsync();
        }

        await using (var deleteContext = fixture.CreateDbContext())
        {
            await new KnowledgeChunkRepository(deleteContext).DeleteAllForFileAsync(fileId);
        }

        await using var readContext = fixture.CreateDbContext();
        var repository = new KnowledgeChunkRepository(readContext);
        (await repository.ListByFileAsync(fileId)).Should().BeEmpty();
        (await repository.ListByFileAsync(otherFileId)).Should().ContainSingle();
    }

    [Fact]
    public async Task SearchByCosineSimilarityAsync_orders_results_by_ascending_cosine_distance()
    {
        var applicationId = Guid.NewGuid();

        await using var setupContext = fixture.CreateDbContext();
        var file = await CreateReadyEnabledFileAsync(setupContext, applicationId);

        var near = NewChunk(applicationId, file.Id, 0, "near", "hash-g1");
        near.SetEmbedding(Embedding(1, 0), "embed-dep", KnowledgeChunkConfigurationDimensions);
        near.Activate();
        var orthogonal = NewChunk(applicationId, file.Id, 1, "orthogonal", "hash-g2");
        orthogonal.SetEmbedding(Embedding(0, 1), "embed-dep", KnowledgeChunkConfigurationDimensions);
        orthogonal.Activate();
        var opposite = NewChunk(applicationId, file.Id, 2, "opposite", "hash-g3");
        opposite.SetEmbedding(Embedding(-1, 0), "embed-dep", KnowledgeChunkConfigurationDimensions);
        opposite.Activate();

        var chunkRepository = new KnowledgeChunkRepository(setupContext);
        await chunkRepository.AddRangeAsync([opposite, orthogonal, near]);
        await chunkRepository.SaveChangesAsync();

        await using var readContext = fixture.CreateDbContext();
        var results = await new KnowledgeChunkRepository(readContext)
            .SearchByCosineSimilarityAsync(applicationId, Embedding(1, 0), topK: 10);

        results.Select(r => r.Chunk.ContentHash).Should().Equal("hash-g1", "hash-g2", "hash-g3");
        results.Select(r => r.Distance).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task SearchByCosineSimilarityAsync_respects_topK()
    {
        var applicationId = Guid.NewGuid();

        await using var setupContext = fixture.CreateDbContext();
        var file = await CreateReadyEnabledFileAsync(setupContext, applicationId);

        var chunks = Enumerable.Range(0, 5).Select(i =>
        {
            var chunk = NewChunk(applicationId, file.Id, i, $"chunk-{i}", $"hash-h{i}");
            chunk.SetEmbedding(Embedding(1, 0), "embed-dep", KnowledgeChunkConfigurationDimensions);
            chunk.Activate();
            return chunk;
        }).ToArray();

        var chunkRepository = new KnowledgeChunkRepository(setupContext);
        await chunkRepository.AddRangeAsync(chunks);
        await chunkRepository.SaveChangesAsync();

        await using var readContext = fixture.CreateDbContext();
        var results = await new KnowledgeChunkRepository(readContext)
            .SearchByCosineSimilarityAsync(applicationId, Embedding(1, 0), topK: 2);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchByCosineSimilarityAsync_excludes_inactive_unembedded_disabled_non_ready_and_other_application_chunks()
    {
        var applicationId = Guid.NewGuid();
        var otherApplicationId = Guid.NewGuid();

        await using var setupContext = fixture.CreateDbContext();
        var readyFile = await CreateReadyEnabledFileAsync(setupContext, applicationId);

        var disabledFile = await CreateReadyEnabledFileAsync(setupContext, applicationId);
        disabledFile.Disable(DateTimeOffset.UtcNow);
        await setupContext.SaveChangesAsync();

        var processingFile = KnowledgeFile.Create(applicationId, "processing.md", Guid.NewGuid().ToString(), 10, "text/markdown", "utf-8", "sub", DateTimeOffset.UtcNow);
        processingFile.SetObjectKey($"applications/{applicationId}/files/{processingFile.Id}/source");
        processingFile.MarkProcessing(DateTimeOffset.UtcNow);
        var fileRepository = new KnowledgeFileRepository(setupContext);
        await fileRepository.AddAsync(processingFile);
        await fileRepository.SaveChangesAsync();

        var matching = NewChunk(applicationId, readyFile.Id, 0, "matching", "hash-i1");
        matching.SetEmbedding(Embedding(1, 0), "embed-dep", KnowledgeChunkConfigurationDimensions);
        matching.Activate();

        var inactive = NewChunk(applicationId, readyFile.Id, 1, "inactive", "hash-i2");
        inactive.SetEmbedding(Embedding(1, 0), "embed-dep", KnowledgeChunkConfigurationDimensions);
        // Not activated - excluded by IsActive filter.

        var unembedded = NewChunk(applicationId, readyFile.Id, 2, "unembedded", "hash-i3");
        unembedded.Activate();
        // No SetEmbedding call - Embedding remains null, excluded by the not-null filter.

        var inDisabledFile = NewChunk(applicationId, disabledFile.Id, 0, "disabled-file", "hash-i4");
        inDisabledFile.SetEmbedding(Embedding(1, 0), "embed-dep", KnowledgeChunkConfigurationDimensions);
        inDisabledFile.Activate();

        var inProcessingFile = NewChunk(applicationId, processingFile.Id, 0, "processing-file", "hash-i5");
        inProcessingFile.SetEmbedding(Embedding(1, 0), "embed-dep", KnowledgeChunkConfigurationDimensions);
        inProcessingFile.Activate();

        var otherApplicationFile = await CreateReadyEnabledFileAsync(setupContext, otherApplicationId);
        var otherApplicationChunk = NewChunk(otherApplicationId, otherApplicationFile.Id, 0, "other-app", "hash-i6");
        otherApplicationChunk.SetEmbedding(Embedding(1, 0), "embed-dep", KnowledgeChunkConfigurationDimensions);
        otherApplicationChunk.Activate();

        var chunkRepository = new KnowledgeChunkRepository(setupContext);
        await chunkRepository.AddRangeAsync([matching, inactive, unembedded, inDisabledFile, inProcessingFile, otherApplicationChunk]);
        await chunkRepository.SaveChangesAsync();

        await using var readContext = fixture.CreateDbContext();
        var results = await new KnowledgeChunkRepository(readContext)
            .SearchByCosineSimilarityAsync(applicationId, Embedding(1, 0), topK: 10);

        results.Should().ContainSingle(r => r.Chunk.ContentHash == "hash-i1");
    }
}

