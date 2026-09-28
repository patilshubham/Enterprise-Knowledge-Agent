using EnterpriseKnowledgeAgent.Application.Applications;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>
/// TASK-APP-02: CRUD + lifecycle integration tests for <see cref="ApplicationsService"/>
/// against a real Postgres container (not a fake repository) - this is the
/// orchestration layer the Api's endpoints call directly, so exercising it
/// end-to-end here covers the endpoints' actual behavior; the endpoints
/// themselves are a thin, compiler-checked HTTP-to-service mapping (route
/// param -> service call -> exception-to-Problem-Details switch) with no
/// independent logic worth re-testing through a full HTTP harness (deferred
/// - see /memories/session/plan.md). There is no login/authorization in
/// this app.
/// </summary>
public sealed class ApplicationsServiceTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private const string Actor = "keycloak-sub-1";

    private static ApplicationsService CreateService(Persistence.EnterpriseKnowledgeAgentDbContext dbContext) =>
        new(new Persistence.Repositories.ApplicationRepository(dbContext), TimeProvider.System);

    [Fact]
    public async Task CreateAsync_persists_new_application()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = CreateService(dbContext);
        var name = $"App {Guid.NewGuid()}";

        var created = await service.CreateAsync(new CreateApplicationRequest(name, "desc", AllowViewerExecution: true), Actor);

        created.Name.Should().Be(name);
        created.Description.Should().Be("desc");
        created.Status.Should().Be("Active");
        created.AllowViewerExecution.Should().BeTrue();
        created.CreatedBy.Should().Be(Actor);
    }

    [Fact]
    public async Task CreateAsync_duplicate_name_throws_conflict()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = CreateService(dbContext);
        var name = $"App {Guid.NewGuid()}";
        await service.CreateAsync(new CreateApplicationRequest(name, null), Actor);

        var act = () => service.CreateAsync(new CreateApplicationRequest(name.ToUpperInvariant(), null), Actor);

        await act.Should().ThrowAsync<ApplicationNameConflictException>();
    }

    [Fact]
    public async Task GetAsync_unknown_id_throws_not_found()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = CreateService(dbContext);

        var act = () => service.GetAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<ApplicationNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_changes_name_description_and_viewer_execution_flag()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(new CreateApplicationRequest($"App {Guid.NewGuid()}", "old"), Actor);

        var updated = await service.UpdateAsync(created.Id, new UpdateApplicationRequest("Renamed", "new", AllowViewerExecution: true));

        updated.Name.Should().Be("Renamed");
        updated.Description.Should().Be("new");
        updated.AllowViewerExecution.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_to_a_name_already_used_by_another_application_throws_conflict()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = CreateService(dbContext);
        var existingName = $"App {Guid.NewGuid()}";
        await service.CreateAsync(new CreateApplicationRequest(existingName, null), Actor);
        var second = await service.CreateAsync(new CreateApplicationRequest($"App {Guid.NewGuid()}", null), Actor);

        var act = () => service.UpdateAsync(second.Id, new UpdateApplicationRequest(existingName, null, AllowViewerExecution: false));

        await act.Should().ThrowAsync<ApplicationNameConflictException>();
    }

    [Fact]
    public async Task ArchiveAsync_then_RestoreAsync_round_trips_status()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(new CreateApplicationRequest($"App {Guid.NewGuid()}", null), Actor);

        var archived = await service.ArchiveAsync(created.Id);
        archived.Status.Should().Be("Archived");

        var restored = await service.RestoreAsync(created.Id);
        restored.Status.Should().Be("Active");
    }

    [Fact]
    public async Task ArchiveAsync_already_archived_throws_state_conflict()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(new CreateApplicationRequest($"App {Guid.NewGuid()}", null), Actor);
        await service.ArchiveAsync(created.Id);

        var act = () => service.ArchiveAsync(created.Id);

        await act.Should().ThrowAsync<Domain.Applications.ApplicationStateConflictException>();
    }

    [Fact]
    public async Task RestoreAsync_while_active_throws_state_conflict()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(new CreateApplicationRequest($"App {Guid.NewGuid()}", null), Actor);

        var act = () => service.RestoreAsync(created.Id);

        await act.Should().ThrowAsync<Domain.Applications.ApplicationStateConflictException>();
    }

    [Fact]
    public async Task DeleteAsync_is_idempotent()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(new CreateApplicationRequest($"App {Guid.NewGuid()}", null), Actor);

        var first = await service.DeleteAsync(created.Id);
        var second = await service.DeleteAsync(created.Id);

        first.Status.Should().Be("Deleted");
        second.Status.Should().Be("Deleted");
    }

    [Fact]
    public async Task DeleteAsync_frees_the_name_for_reuse()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = CreateService(dbContext);
        var name = $"App {Guid.NewGuid()}";
        var created = await service.CreateAsync(new CreateApplicationRequest(name, null), Actor);
        await service.DeleteAsync(created.Id);

        var recreated = await service.CreateAsync(new CreateApplicationRequest(name, null), Actor);

        recreated.Name.Should().Be(name);
        recreated.Id.Should().NotBe(created.Id);
    }

    [Fact]
    public async Task ListForCallerAsync_returns_every_application()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = CreateService(dbContext);
        var first = await service.CreateAsync(new CreateApplicationRequest($"App {Guid.NewGuid()}", null), Actor);
        var second = await service.CreateAsync(new CreateApplicationRequest($"App {Guid.NewGuid()}", null), Actor);

        var list = await service.ListForCallerAsync();

        list.Should().Contain(a => a.Id == first.Id);
        list.Should().Contain(a => a.Id == second.Id);
    }
}
