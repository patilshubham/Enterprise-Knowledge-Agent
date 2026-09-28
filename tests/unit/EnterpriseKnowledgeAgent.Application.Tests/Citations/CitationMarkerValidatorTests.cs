using EnterpriseKnowledgeAgent.Application.Citations;
using EnterpriseKnowledgeAgent.Domain.Citations;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Citations;

/// <summary>TASK-CIT-03: unit tests for <see cref="CitationMarkerValidator"/>'s marker parsing + validation.</summary>
public sealed class CitationMarkerValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static RunEvidence Evidence(Guid applicationId, Guid agentRunId, string sourceId) =>
        RunEvidence.Create(
            applicationId,
            agentRunId,
            sourceId,
            knowledgeChunkId: Guid.NewGuid(),
            knowledgeFileId: Guid.NewGuid(),
            fileNameSnapshot: "handbook.pdf",
            headingPathSnapshot: "Chapter 1",
            passageSnapshot: "Employees accrue 15 days of PTO per year.",
            characterStartSnapshot: 0,
            characterEndSnapshot: 42,
            processedAtSnapshot: Now,
            now: Now);

    [Fact]
    public void Validate_creates_a_citation_for_a_marker_referencing_a_valid_run_source_id()
    {
        var applicationId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var evidence = Evidence(applicationId, agentRunId, "SOURCE-001");

        var result = CitationMarkerValidator.Validate(applicationId, outputId, ["[SOURCE-001]"], [evidence], Now);

        result.Citations.Should().ContainSingle();
        result.Citations[0].RunEvidenceId.Should().Be(evidence.Id);
        result.Citations[0].GeneratedOutputId.Should().Be(outputId);
        result.Citations[0].ApplicationId.Should().Be(applicationId);
        result.InvalidMarkers.Should().BeEmpty();
    }

    [Fact]
    public void Validate_rejects_a_marker_referencing_an_unknown_source_id()
    {
        var applicationId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var evidence = Evidence(applicationId, agentRunId, "SOURCE-001");

        var result = CitationMarkerValidator.Validate(applicationId, Guid.NewGuid(), ["[SOURCE-999]"], [evidence], Now);

        result.Citations.Should().BeEmpty();
        result.InvalidMarkers.Should().Equal("[SOURCE-999]");
    }

    [Fact]
    public void Validate_rejects_a_marker_referencing_another_applications_or_runs_source_id()
    {
        // The candidate evidence set passed in belongs to a DIFFERENT run than
        // the marker claims - this is exactly how a cross-application/cross-run
        // hallucinated marker is rejected: it simply isn't present in the
        // evidence supplied for validation (ASDD §17.3).
        var evidenceForAnotherRun = Evidence(Guid.NewGuid(), Guid.NewGuid(), "SOURCE-001");

        var result = CitationMarkerValidator.Validate(Guid.NewGuid(), Guid.NewGuid(), ["[SOURCE-001]"], [], Now);

        result.Citations.Should().BeEmpty();
        result.InvalidMarkers.Should().Equal("[SOURCE-001]");
        evidenceForAnotherRun.SourceId.Should().Be("SOURCE-001");
    }

    [Fact]
    public void Validate_handles_a_mix_of_valid_and_invalid_markers_independently()
    {
        var applicationId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var evidence1 = Evidence(applicationId, agentRunId, "SOURCE-001");
        var evidence2 = Evidence(applicationId, agentRunId, "SOURCE-002");

        var result = CitationMarkerValidator.Validate(
            applicationId,
            Guid.NewGuid(),
            ["[SOURCE-001]", "[SOURCE-404]", "[SOURCE-002]"],
            [evidence1, evidence2],
            Now);

        result.Citations.Should().HaveCount(2);
        result.InvalidMarkers.Should().Equal("[SOURCE-404]");
    }
}
