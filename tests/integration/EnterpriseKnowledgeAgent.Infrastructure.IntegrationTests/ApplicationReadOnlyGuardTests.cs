using EnterpriseKnowledgeAgent.Application.Applications;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-APP-04: the shared archived/deleted precondition, exercised against a real Postgres container.</summary>
public sealed class ApplicationReadOnlyGuardTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private const string Actor = "keycloak-sub-1";

    [Fact]
    public async Task EnsureMutableAsync_ActiveApplication_DoesNotThrow()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = new ApplicationsService(new Persistence.Repositories.ApplicationRepository(dbContext), TimeProvider.System);
        var guard = new ApplicationReadOnlyGuard(new Persistence.Repositories.ApplicationRepository(dbContext));
        var created = await service.CreateAsync(new CreateApplicationRequest($"App {Guid.NewGuid()}", null), Actor);

        var act = () => guard.EnsureMutableAsync(created.Id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureMutableAsync_ArchivedApplication_ThrowsReadOnly()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = new ApplicationsService(new Persistence.Repositories.ApplicationRepository(dbContext), TimeProvider.System);
        var guard = new ApplicationReadOnlyGuard(new Persistence.Repositories.ApplicationRepository(dbContext));
        var created = await service.CreateAsync(new CreateApplicationRequest($"App {Guid.NewGuid()}", null), Actor);
        await service.ArchiveAsync(created.Id);

        var act = () => guard.EnsureMutableAsync(created.Id);

        await act.Should().ThrowAsync<ApplicationReadOnlyException>();
    }

    [Fact]
    public async Task EnsureMutableAsync_DeletedApplication_ThrowsReadOnly()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = new ApplicationsService(new Persistence.Repositories.ApplicationRepository(dbContext), TimeProvider.System);
        var guard = new ApplicationReadOnlyGuard(new Persistence.Repositories.ApplicationRepository(dbContext));
        var created = await service.CreateAsync(new CreateApplicationRequest($"App {Guid.NewGuid()}", null), Actor);
        await service.DeleteAsync(created.Id);

        var act = () => guard.EnsureMutableAsync(created.Id);

        await act.Should().ThrowAsync<ApplicationReadOnlyException>();
    }

    [Fact]
    public async Task EnsureMutableAsync_UnknownApplication_ThrowsNotFound()
    {
        await using var dbContext = fixture.CreateDbContext();
        var guard = new ApplicationReadOnlyGuard(new Persistence.Repositories.ApplicationRepository(dbContext));

        var act = () => guard.EnsureMutableAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<ApplicationNotFoundException>();
    }
}
