using EnterpriseKnowledgeAgent.Application.Retrieval;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Retrieval;

/// <summary>TASK-RET-04: unit tests for the similarity threshold + evidence-token budget fill.</summary>
public sealed class EvidenceBudgetPlannerTests
{
    private static EvidenceBudgetPlanner.CandidateForBudget Candidate(string content, double score) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "file.txt", "H1", content, CharacterStart: 0, CharacterEnd: content.Length, score);

    private static int TokensPerCharacter(string content) => content.Length;

    [Fact]
    public void Plan_excludes_candidates_below_the_minimum_similarity_threshold()
    {
        var candidates = new[]
        {
            Candidate("above threshold", score: 0.8),
            Candidate("below threshold", score: 0.2),
        };

        var result = EvidenceBudgetPlanner.Plan(candidates, minimumSimilarity: 0.5, tokenBudget: 1_000, TokensPerCharacter);

        result.Should().ContainSingle();
        result[0].Content.Should().Be("above threshold");
    }

    [Fact]
    public void Plan_assigns_sequential_source_ids_in_the_order_sources_are_included()
    {
        var candidates = new[] { Candidate("first", 0.9), Candidate("second", 0.8), Candidate("third", 0.7) };

        var result = EvidenceBudgetPlanner.Plan(candidates, minimumSimilarity: 0.0, tokenBudget: 1_000, TokensPerCharacter);

        result.Select(s => s.SourceId).Should().ContainInOrder("SOURCE-001", "SOURCE-002", "SOURCE-003");
    }

    [Fact]
    public void Plan_stops_including_candidates_once_the_token_budget_is_exhausted_but_keeps_trying_smaller_later_candidates()
    {
        // Budget of 12: "1234567890" (10) fits, "abcdefghij" (10) would push to 20 so it's skipped, "ab" (2) still fits after.
        var candidates = new[] { Candidate("1234567890", 0.9), Candidate("abcdefghij", 0.8), Candidate("ab", 0.7) };

        var result = EvidenceBudgetPlanner.Plan(candidates, minimumSimilarity: 0.0, tokenBudget: 12, TokensPerCharacter);

        result.Select(s => s.Content).Should().ContainInOrder("1234567890", "ab");
    }

    [Fact]
    public void Plan_returns_an_empty_list_when_no_candidate_fits_the_budget()
    {
        var candidates = new[] { Candidate("way too long for the budget", 0.9) };

        var result = EvidenceBudgetPlanner.Plan(candidates, minimumSimilarity: 0.0, tokenBudget: 1, TokensPerCharacter);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Plan_returns_an_empty_list_when_no_candidates_are_supplied()
    {
        var result = EvidenceBudgetPlanner.Plan([], minimumSimilarity: 0.0, tokenBudget: 1_000, TokensPerCharacter);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Plan_records_the_estimated_token_count_and_score_on_each_included_source()
    {
        var candidates = new[] { Candidate("twelve chars", 0.65) };

        var result = EvidenceBudgetPlanner.Plan(candidates, minimumSimilarity: 0.0, tokenBudget: 1_000, TokensPerCharacter);

        result.Single().TokenCount.Should().Be("twelve chars".Length);
        result.Single().Score.Should().Be(0.65);
    }
}
