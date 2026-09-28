using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-KNW-01/03/04: persistence and query tests for <see cref="KnowledgeFileRepository"/> against a real Postgres container.</summary>
public sealed class KnowledgeFileRepositoryTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private static KnowledgeFile NewFile(Guid? applicationId = null, string displayName = "notes.txt", string contentHash = "hash-1") =>
        KnowledgeFile.Create(
            applicationId ?? Guid.NewGuid(), displayName, contentHash, 100, "text/plain", "utf-8", "sub", DateTimeOffset.UtcNow);

    [Fact]
    public async Task AddAsync_then_GetAsync_round_trips_all_fields()
    {
        var file = NewFile();
        file.SetObjectKey($"applications/{file.ApplicationId}/files/{file.Id}/source");

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeFileRepository(writeContext);
            await repository.AddAsync(file);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var loaded = await new KnowledgeFileRepository(readContext).GetAsync(file.Id);

        loaded.Should().NotBeNull();
        loaded!.ApplicationId.Should().Be(file.ApplicationId);
        loaded.DisplayName.Should().Be("notes.txt");
        loaded.ContentHash.Should().Be("hash-1");
        loaded.ObjectKey.Should().Be(file.ObjectKey);
        loaded.Status.Should().Be(UserFacingFileStatus.Queued);
        loaded.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_returns_null_for_unknown_id()
    {
        await using var dbContext = fixture.CreateDbContext();

        var loaded = await new KnowledgeFileRepository(dbContext).GetAsync(Guid.NewGuid());

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_scopes_results_to_the_requested_application()
    {
        var applicationId = Guid.NewGuid();
        var otherApplicationId = Guid.NewGuid();
        var mine = NewFile(applicationId, "mine.txt", "hash-mine");
        var other = NewFile(otherApplicationId, "other.txt", "hash-other");

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeFileRepository(writeContext);
            await repository.AddAsync(mine);
            await repository.AddAsync(other);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var results = await new KnowledgeFileRepository(readContext).ListAsync(applicationId);

        results.Should().ContainSingle(f => f.Id == mine.Id);
        results.Should().NotContain(f => f.Id == other.Id);
    }

    [Fact]
    public async Task ListAsync_filters_by_search_term_case_insensitively()
    {
        var applicationId = Guid.NewGuid();
        var matching = NewFile(applicationId, "Quarterly Report.txt", "hash-search-1");
        var nonMatching = NewFile(applicationId, "unrelated.txt", "hash-search-2");

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeFileRepository(writeContext);
            await repository.AddAsync(matching);
            await repository.AddAsync(nonMatching);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var results = await new KnowledgeFileRepository(readContext).ListAsync(applicationId, search: "quarterly");

        results.Should().ContainSingle(f => f.Id == matching.Id);
    }

    [Fact]
    public async Task ListAsync_filters_by_status()
    {
        var applicationId = Guid.NewGuid();
        var queued = NewFile(applicationId, "queued.txt", "hash-status-1");
        var ready = NewFile(applicationId, "ready.txt", "hash-status-2");
        ready.MarkProcessing(DateTimeOffset.UtcNow);
        ready.MarkReady(DateTimeOffset.UtcNow);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeFileRepository(writeContext);
            await repository.AddAsync(queued);
            await repository.AddAsync(ready);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var results = await new KnowledgeFileRepository(readContext).ListAsync(applicationId, status: UserFacingFileStatus.Ready);

        results.Should().ContainSingle(f => f.Id == ready.Id);
    }

    [Fact]
    public async Task ExistsActiveHashAsync_true_for_matching_hash_in_same_application()
    {
        var applicationId = Guid.NewGuid();
        var file = NewFile(applicationId, "dup.txt", "hash-dup-check");

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeFileRepository(writeContext);
            await repository.AddAsync(file);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var exists = await new KnowledgeFileRepository(readContext).ExistsActiveHashAsync(applicationId, "hash-dup-check");

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsActiveHashAsync_false_for_a_different_application()
    {
        var applicationId = Guid.NewGuid();
        var otherApplicationId = Guid.NewGuid();
        var file = NewFile(applicationId, "dup.txt", "hash-dup-scope");

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeFileRepository(writeContext);
            await repository.AddAsync(file);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var exists = await new KnowledgeFileRepository(readContext).ExistsActiveHashAsync(otherApplicationId, "hash-dup-scope");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsActiveHashAsync_false_once_the_matching_file_is_deleting()
    {
        var applicationId = Guid.NewGuid();
        var file = NewFile(applicationId, "deleting.txt", "hash-deleting");
        file.MarkDeleting(DateTimeOffset.UtcNow);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeFileRepository(writeContext);
            await repository.AddAsync(file);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var exists = await new KnowledgeFileRepository(readContext).ExistsActiveHashAsync(applicationId, "hash-deleting");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsActiveHashAsync_false_once_the_matching_file_is_deleted()
    {
        var applicationId = Guid.NewGuid();
        var file = NewFile(applicationId, "deleted.txt", "hash-deleted");
        file.MarkDeleted(DateTimeOffset.UtcNow);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeFileRepository(writeContext);
            await repository.AddAsync(file);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var exists = await new KnowledgeFileRepository(readContext).ExistsActiveHashAsync(applicationId, "hash-deleted");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task SaveChangesAsync_persists_status_transitions()
    {
        var file = NewFile();

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeFileRepository(writeContext);
            await repository.AddAsync(file);
            await repository.SaveChangesAsync();
        }

        await using (var updateContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeFileRepository(updateContext);
            var loaded = await repository.GetAsync(file.Id);
            loaded!.MarkProcessing(DateTimeOffset.UtcNow);
            loaded.MarkReady(DateTimeOffset.UtcNow);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var reloaded = await new KnowledgeFileRepository(readContext).GetAsync(file.Id);

        reloaded!.Status.Should().Be(UserFacingFileStatus.Ready);
        reloaded.LastProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CountByStatusAsync_groups_by_effective_status_and_honors_application_scope()
    {
        var applicationId = Guid.NewGuid();
        var otherApplicationId = Guid.NewGuid();
        var queuedFile = NewFile(applicationId, "queued.txt", "hash-queued");
        var readyDisabledFile = NewFile(applicationId, "ready-disabled.txt", "hash-ready-disabled");
        var otherAppFile = NewFile(otherApplicationId, "other.txt", "hash-other");

        var now = DateTimeOffset.UtcNow;
        readyDisabledFile.MarkProcessing(now);
        readyDisabledFile.MarkReady(now);
        readyDisabledFile.Disable(now);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new KnowledgeFileRepository(writeContext);
            await repository.AddAsync(queuedFile);
            await repository.AddAsync(readyDisabledFile);
            await repository.AddAsync(otherAppFile);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var repository2 = new KnowledgeFileRepository(readContext);

        var scoped = await repository2.CountByStatusAsync(applicationId);
        scoped[UserFacingFileStatus.Queued].Should().Be(1);
        // The underlying Status is Ready, but IsEnabled=false projects it to Disabled - never both.
        scoped[UserFacingFileStatus.Disabled].Should().Be(1);
        scoped.Should().NotContainKey(UserFacingFileStatus.Ready);

        var platformWide = await repository2.CountByStatusAsync(applicationId: null);
        platformWide[UserFacingFileStatus.Queued].Should().BeGreaterThanOrEqualTo(2); // includes otherAppFile
        platformWide[UserFacingFileStatus.Disabled].Should().BeGreaterThanOrEqualTo(1);
    }
}
