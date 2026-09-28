using ApplicationEntity = EnterpriseKnowledgeAgent.Domain.Applications.Application;

namespace EnterpriseKnowledgeAgent.Application.Applications;

/// <summary>
/// TASK-APP-02: the application-layer orchestration behind the CRUD +
/// lifecycle endpoints - the Api layer only maps HTTP to these calls and
/// maps thrown exceptions to Problem Details; every actual business rule
/// (uniqueness, state transitions) lives in the Domain entity or here.
/// </summary>
public sealed class ApplicationsService(IApplicationRepository repository, TimeProvider timeProvider)
{
    public async Task<ApplicationResponse> CreateAsync(CreateApplicationRequest request, string createdBy, CancellationToken cancellationToken = default)
    {
        if (await repository.NameExistsAsync(request.Name, cancellationToken: cancellationToken))
        {
            throw new ApplicationNameConflictException(request.Name);
        }

        var application = ApplicationEntity.Create(
            request.Name,
            request.Description,
            createdBy,
            timeProvider.GetUtcNow(),
            request.AllowViewerExecution);

        await repository.AddAsync(application, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return ToResponse(application);
    }

    /// <summary>There is no login/authorization in this app - every application is visible to every caller.</summary>
    public async Task<IReadOnlyList<ApplicationResponse>> ListForCallerAsync(CancellationToken cancellationToken = default)
    {
        var all = await repository.ListAsync(cancellationToken);
        return [.. all.Select(ToResponse)];
    }

    public async Task<ApplicationResponse> GetAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await repository.GetAsync(applicationId, cancellationToken)
            ?? throw new ApplicationNotFoundException(applicationId);

        return ToResponse(application);
    }

    public async Task<ApplicationResponse> UpdateAsync(Guid applicationId, UpdateApplicationRequest request, CancellationToken cancellationToken = default)
    {
        var application = await repository.GetAsync(applicationId, cancellationToken)
            ?? throw new ApplicationNotFoundException(applicationId);

        if (await repository.NameExistsAsync(request.Name, excludeApplicationId: applicationId, cancellationToken))
        {
            throw new ApplicationNameConflictException(request.Name);
        }

        var now = timeProvider.GetUtcNow();
        application.Update(request.Name, request.Description, now);
        application.SetAllowViewerExecution(request.AllowViewerExecution, now);
        await repository.SaveChangesAsync(cancellationToken);

        return ToResponse(application);
    }

    public async Task<ApplicationResponse> ArchiveAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await repository.GetAsync(applicationId, cancellationToken)
            ?? throw new ApplicationNotFoundException(applicationId);

        application.Archive(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return ToResponse(application);
    }

    public async Task<ApplicationResponse> RestoreAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await repository.GetAsync(applicationId, cancellationToken)
            ?? throw new ApplicationNotFoundException(applicationId);

        application.Restore(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return ToResponse(application);
    }

    /// <summary>Idempotent (TASK-APP-02): deleting an already-deleted application succeeds without error.</summary>
    public async Task<ApplicationResponse> DeleteAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await repository.GetAsync(applicationId, cancellationToken)
            ?? throw new ApplicationNotFoundException(applicationId);

        application.Delete(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return ToResponse(application);
    }

    private static ApplicationResponse ToResponse(ApplicationEntity application) => new(
        application.Id,
        application.Name,
        application.Description,
        application.Status.ToString(),
        application.AllowViewerExecution,
        application.CreatedBy,
        application.CreatedAt,
        application.UpdatedAt,
        application.ArchivedAt,
        application.DeletedAt);
}
