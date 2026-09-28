using EnterpriseKnowledgeAgent.Application.Citations;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Domain.Outputs;
using EnterpriseKnowledgeAgent.Shared.Jobs;

namespace EnterpriseKnowledgeAgent.Application.Outputs;

/// <summary>
/// TASK-OUT-02/03/04/05 + TASK-CIT-04: the application-layer orchestration
/// behind the output detail/list/edit/delete/regenerate endpoints and the
/// citation evidence-drawer endpoints - mirrors <c>AgentsService</c>'s
/// convention (Api layer only maps HTTP to these calls and maps thrown
/// exceptions to Problem Details).
/// </summary>
public sealed class OutputsService(
    IGeneratedOutputRepository outputRepository,
    IAgentRunRepository agentRunRepository,
    ICitationRepository citationRepository,
    IRunEvidenceRepository runEvidenceRepository,
    IKnowledgeFileRepository knowledgeFileRepository,
    AgentRunSubmissionService agentRunSubmissionService,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<OutputSummaryResponse>> ListAsync(
        Guid applicationId,
        string? search,
        Guid? agentId,
        string? createdBy,
        DateTimeOffset? from,
        DateTimeOffset? to,
        JobStatus? runStatus,
        CancellationToken cancellationToken = default)
    {
        var outputs = await outputRepository.ListAsync(applicationId, search, agentId, createdBy, from, to, runStatus, cancellationToken);

        var results = new List<OutputSummaryResponse>(outputs.Count);
        foreach (var output in outputs)
        {
            var run = await agentRunRepository.GetAsync(output.AgentRunId, cancellationToken);
            results.Add(new OutputSummaryResponse(
                output.Id,
                output.Title,
                run?.AgentId ?? Guid.Empty,
                run?.AgentNameSnapshot ?? "(unknown agent)",
                output.CreatedBy,
                output.CreatedAt,
                output.IsEdited));
        }

        return results;
    }

    public async Task<OutputDetailResponse> GetDetailAsync(Guid applicationId, Guid outputId, CancellationToken cancellationToken = default)
    {
        var output = await GetVisibleOutputAsync(applicationId, outputId, cancellationToken);
        var run = await agentRunRepository.GetAsync(output.AgentRunId, cancellationToken);
        var citations = await BuildCitationDetailsAsync(output, cancellationToken);

        return new OutputDetailResponse(
            output.Id,
            output.ApplicationId,
            output.AgentRunId,
            output.Title,
            output.OriginalMarkdown,
            output.CurrentMarkdown,
            output.IsEdited,
            output.CreatedBy,
            output.CreatedAt,
            output.LastEditedBy,
            output.LastEditedAt,
            run?.AgentId ?? Guid.Empty,
            run?.AgentNameSnapshot ?? "(unknown agent)",
            run?.DeploymentSnapshot,
            run?.RequestedParametersJson,
            run?.EffectiveParametersJson,
            run?.RetrievalSettingsJson,
            citations);
    }

    public async Task<OutputDetailResponse> EditAsync(
        Guid applicationId, Guid outputId, string currentMarkdown, string editedBy, CancellationToken cancellationToken = default)
    {
        var output = await GetVisibleOutputAsync(applicationId, outputId, cancellationToken);
        output.Edit(currentMarkdown, editedBy, timeProvider.GetUtcNow());
        await outputRepository.SaveChangesAsync(cancellationToken);

        return await GetDetailAsync(applicationId, outputId, cancellationToken);
    }

    /// <summary>TASK-OUT-04/FR-OUT-005: soft-delete. Idempotent - a repeated delete of an already-deleted output still succeeds (see <see cref="GeneratedOutput.SoftDelete"/>).</summary>
    public async Task DeleteAsync(Guid applicationId, Guid outputId, CancellationToken cancellationToken = default)
    {
        var output = await outputRepository.GetAsync(outputId, cancellationToken);
        if (output is null || output.ApplicationId != applicationId)
        {
            throw new OutputNotFoundException(outputId);
        }

        output.SoftDelete();
        await outputRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// TASK-OUT-04: creates a brand-new run referencing the same agent (a
    /// fresh snapshot of its current configuration, via
    /// <see cref="AgentRunSubmissionService"/>) with a new idempotency key -
    /// the original run/output are left completely untouched.
    /// </summary>
    public async Task<RegenerateOutputResponse> RegenerateAsync(
        Guid applicationId, Guid outputId, string requestedBy, CancellationToken cancellationToken = default)
    {
        var output = await GetVisibleOutputAsync(applicationId, outputId, cancellationToken);
        var originalRun = await agentRunRepository.GetAsync(output.AgentRunId, cancellationToken)
            ?? throw new OutputNotFoundException(outputId);

        var request = new SubmitAgentRunRequest(originalRun.UserRequest, output.Title, Guid.NewGuid().ToString());
        var newRun = await agentRunSubmissionService.SubmitAsync(applicationId, originalRun.AgentId, requestedBy, request, cancellationToken);

        return new RegenerateOutputResponse(newRun.Id);
    }

    public async Task<IReadOnlyList<CitationDetailResponse>> GetCitationsAsync(
        Guid applicationId, Guid outputId, CancellationToken cancellationToken = default)
    {
        var output = await GetVisibleOutputAsync(applicationId, outputId, cancellationToken);
        return await BuildCitationDetailsAsync(output, cancellationToken);
    }

    public async Task<CitationDetailResponse> GetCitationAsync(
        Guid applicationId, Guid outputId, Guid citationId, CancellationToken cancellationToken = default)
    {
        var output = await GetVisibleOutputAsync(applicationId, outputId, cancellationToken);
        var citations = await BuildCitationDetailsAsync(output, cancellationToken);
        return citations.FirstOrDefault(c => c.Id == citationId) ?? throw new CitationNotFoundException(citationId);
    }

    private async Task<GeneratedOutput> GetVisibleOutputAsync(Guid applicationId, Guid outputId, CancellationToken cancellationToken)
    {
        var output = await outputRepository.GetAsync(outputId, cancellationToken);
        if (output is null || output.ApplicationId != applicationId || output.IsDeleted)
        {
            throw new OutputNotFoundException(outputId);
        }

        return output;
    }

    /// <summary>
    /// TASK-CIT-04: builds each citation's evidence detail purely from the
    /// immutable <c>RunEvidence</c> snapshot (FR-CIT-004 - never depends on
    /// the live chunk/file), plus a live <see cref="IKnowledgeFileRepository.GetAsync"/>
    /// lookup (cached per file within this call) to flag deleted sources.
    /// </summary>
    private async Task<IReadOnlyList<CitationDetailResponse>> BuildCitationDetailsAsync(GeneratedOutput output, CancellationToken cancellationToken)
    {
        var citations = await citationRepository.ListByOutputAsync(output.Id, cancellationToken);
        if (citations.Count == 0)
        {
            return [];
        }

        var evidence = await runEvidenceRepository.ListByRunAsync(output.AgentRunId, cancellationToken);
        var evidenceById = evidence.ToDictionary(e => e.Id);
        var fileAvailability = new Dictionary<Guid, bool>();

        var results = new List<CitationDetailResponse>(citations.Count);
        foreach (var citation in citations)
        {
            if (!evidenceById.TryGetValue(citation.RunEvidenceId, out var evidenceEntry))
            {
                continue;
            }

            if (!fileAvailability.TryGetValue(evidenceEntry.KnowledgeFileId, out var available))
            {
                var file = await knowledgeFileRepository.GetAsync(evidenceEntry.KnowledgeFileId, cancellationToken);
                available = file is not null;
                fileAvailability[evidenceEntry.KnowledgeFileId] = available;
            }

            results.Add(new CitationDetailResponse(
                citation.Id,
                evidenceEntry.SourceId,
                evidenceEntry.FileNameSnapshot,
                evidenceEntry.HeadingPathSnapshot,
                evidenceEntry.PassageSnapshot,
                evidenceEntry.CharacterStartSnapshot,
                evidenceEntry.CharacterEndSnapshot,
                evidenceEntry.ProcessedAtSnapshot,
                SourceUnavailable: !available));
        }

        return results;
    }
}
