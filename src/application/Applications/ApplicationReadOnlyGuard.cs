namespace EnterpriseKnowledgeAgent.Application.Applications;

/// <inheritdoc cref="IApplicationReadOnlyGuard"/>
public sealed class ApplicationReadOnlyGuard(IApplicationRepository repository) : IApplicationReadOnlyGuard
{
    public async Task EnsureMutableAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await repository.GetAsync(applicationId, cancellationToken)
            ?? throw new ApplicationNotFoundException(applicationId);

        if (application.IsReadOnly)
        {
            throw new ApplicationReadOnlyException(applicationId, application.Status.ToString());
        }
    }
}
