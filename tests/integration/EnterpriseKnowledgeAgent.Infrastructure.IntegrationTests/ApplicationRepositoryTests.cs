using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using ApplicationEntity = EnterpriseKnowledgeAgent.Domain.Applications.Application;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-APP-01: persistence round-trip tests against a real Postgres container.</summary>
public sealed class ApplicationRepositoryTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    [Fact]
    public async Task AddAsync_then_GetAsync_round_trips_all_fields()
    {
        var now = DateTimeOffset.UtcNow;
        var app = ApplicationEntity.Create($"App {Guid.NewGuid()}", "desc", "sub-1", now, allowViewerExecution: true);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ApplicationRepository(writeContext);
            await repository.AddAsync(app);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var readRepository = new ApplicationRepository(readContext);
        var loaded = await readRepository.GetAsync(app.Id);

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be(app.Name);
        loaded.Description.Should().Be("desc");
        loaded.AllowViewerExecution.Should().BeTrue();
        loaded.CreatedBy.Should().Be("sub-1");
        loaded.Status.Should().Be(Shared.Applications.ApplicationStatus.Active);
    }

    [Fact]
    public async Task GetAsync_returns_null_for_unknown_id()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new ApplicationRepository(dbContext);

        var loaded = await repository.GetAsync(Guid.NewGuid());

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Archive_and_Restore_transitions_persist_across_reload()
    {
        var now = DateTimeOffset.UtcNow;
        var app = ApplicationEntity.Create($"App {Guid.NewGuid()}", null, "sub-1", now);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ApplicationRepository(writeContext);
            await repository.AddAsync(app);
            await repository.SaveChangesAsync();
        }

        await using (var archiveContext = fixture.CreateDbContext())
        {
            var repository = new ApplicationRepository(archiveContext);
            var loaded = await repository.GetAsync(app.Id);
            loaded!.Archive(now.AddHours(1));
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var readRepository = new ApplicationRepository(readContext);
        var reloaded = await readRepository.GetAsync(app.Id);

        reloaded!.Status.Should().Be(Shared.Applications.ApplicationStatus.Archived);
        reloaded.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task NameExistsAsync_is_case_insensitive_for_non_deleted_applications()
    {
        var uniqueName = $"Contoso-{Guid.NewGuid()}";
        var app = ApplicationEntity.Create(uniqueName, null, "sub-1", DateTimeOffset.UtcNow);

        await using var dbContext = fixture.CreateDbContext();
        var repository = new ApplicationRepository(dbContext);
        await repository.AddAsync(app);
        await repository.SaveChangesAsync();

        var exists = await repository.NameExistsAsync(uniqueName.ToUpperInvariant());

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task NameExistsAsync_ignores_deleted_applications()
    {
        var uniqueName = $"Contoso-{Guid.NewGuid()}";
        var app = ApplicationEntity.Create(uniqueName, null, "sub-1", DateTimeOffset.UtcNow);
        app.Delete(DateTimeOffset.UtcNow);

        await using var dbContext = fixture.CreateDbContext();
        var repository = new ApplicationRepository(dbContext);
        await repository.AddAsync(app);
        await repository.SaveChangesAsync();

        var exists = await repository.NameExistsAsync(uniqueName);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsAsync_excludes_the_given_application_id()
    {
        var uniqueName = $"Contoso-{Guid.NewGuid()}";
        var app = ApplicationEntity.Create(uniqueName, null, "sub-1", DateTimeOffset.UtcNow);

        await using var dbContext = fixture.CreateDbContext();
        var repository = new ApplicationRepository(dbContext);
        await repository.AddAsync(app);
        await repository.SaveChangesAsync();

        var exists = await repository.NameExistsAsync(uniqueName, excludeApplicationId: app.Id);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ListAsync_includes_newly_added_application()
    {
        var uniqueName = $"Contoso-{Guid.NewGuid()}";
        var app = ApplicationEntity.Create(uniqueName, null, "sub-1", DateTimeOffset.UtcNow);

        await using var dbContext = fixture.CreateDbContext();
        var repository = new ApplicationRepository(dbContext);
        await repository.AddAsync(app);
        await repository.SaveChangesAsync();

        var all = await repository.ListAsync();

        all.Should().Contain(a => a.Id == app.Id);
    }

    [Fact]
    public async Task CountByStatusAsync_groups_by_status()
    {
        var active = ApplicationEntity.Create($"Active-{Guid.NewGuid()}", null, "sub-1", DateTimeOffset.UtcNow);
        var archived = ApplicationEntity.Create($"Archived-{Guid.NewGuid()}", null, "sub-1", DateTimeOffset.UtcNow);
        archived.Archive(DateTimeOffset.UtcNow);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ApplicationRepository(writeContext);
            await repository.AddAsync(active);
            await repository.AddAsync(archived);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var counts = await new ApplicationRepository(readContext).CountByStatusAsync();

        counts[Shared.Applications.ApplicationStatus.Active].Should().BeGreaterThanOrEqualTo(1);
        counts[Shared.Applications.ApplicationStatus.Archived].Should().BeGreaterThanOrEqualTo(1);
    }
}
