namespace EnterpriseKnowledgeAgent.Domain.Citations;

/// <summary>
/// TASK-CIT-01: links a generated output to one validated <see cref="RunEvidence"/>
/// entry (ASDD §17.1). Only created for markers that TASK-CIT-03 validated
/// against the run's own evidence set - unknown/cross-application markers
/// never reach this constructor. <see cref="GeneratedOutputId"/> is a plain,
/// unenforced reference (no FK) to <c>GeneratedOutput</c> (module 23,
/// TASK-OUT-01), matching this codebase's existing convention of
/// unconstrained indexed reference columns rather than enforced EF
/// relationships (see <see cref="RunEvidenceId"/> and
/// <c>ProcessingAttemptConfiguration</c>).
/// </summary>
public sealed class Citation
{
    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid GeneratedOutputId { get; private set; }

    public Guid RunEvidenceId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618 // required by EF Core materialization; all properties are always populated via Create/mapping.
    private Citation()
    {
    }
#pragma warning restore CS8618

    public static Citation Create(
        Guid applicationId,
        Guid generatedOutputId,
        Guid runEvidenceId,
        DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            GeneratedOutputId = generatedOutputId,
            RunEvidenceId = runEvidenceId,
            CreatedAt = now,
        };
}
