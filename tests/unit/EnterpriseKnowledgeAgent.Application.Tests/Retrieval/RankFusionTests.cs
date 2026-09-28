using EnterpriseKnowledgeAgent.Application.Retrieval;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Retrieval;

/// <summary>TASK-RET-02: unit tests for normalized rank fusion.</summary>
public sealed class RankFusionTests
{
    [Fact]
    public void Fuse_combines_normalized_semantic_and_keyword_scores_using_the_configured_weights()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();

        // Semantic: A is the best match (1.0), B is the worst (0.0) after normalization.
        var semantic = new List<(Guid Id, double Score)> { (idA, 0.9), (idB, 0.1) };

        // Keyword: B is the best match (1.0), A is the worst (0.0) after normalization.
        var keyword = new List<(Guid Id, double Score)> { (idA, 1.0), (idB, 10.0) };

        var fused = RankFusion.Fuse(semantic, keyword, semanticWeight: 0.5, keywordWeight: 0.5);

        fused.Should().HaveCount(2);
        // Both fused scores should be 0.5 * 1.0 + 0.5 * 0.0 = 0.5 for one and the mirror for the other.
        fused.Single(f => f.Id == idA).FusedScore.Should().BeApproximately(0.5, 1e-9);
        fused.Single(f => f.Id == idB).FusedScore.Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void Fuse_favors_the_higher_weighted_source_when_the_two_sources_disagree()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();

        var semantic = new List<(Guid Id, double Score)> { (idA, 1.0), (idB, 0.0) };
        var keyword = new List<(Guid Id, double Score)> { (idA, 0.0), (idB, 1.0) };

        var fused = RankFusion.Fuse(semantic, keyword, semanticWeight: 0.9, keywordWeight: 0.1);

        fused[0].Id.Should().Be(idA);
        fused[0].FusedScore.Should().BeApproximately(0.9, 1e-9);
        fused[1].Id.Should().Be(idB);
        fused[1].FusedScore.Should().BeApproximately(0.1, 1e-9);
    }

    [Fact]
    public void Fuse_treats_an_id_present_in_only_one_list_as_zero_for_the_other_lists_contribution()
    {
        var semanticOnlyId = Guid.NewGuid();
        var keywordOnlyId = Guid.NewGuid();

        var semantic = new List<(Guid Id, double Score)> { (semanticOnlyId, 0.8) };
        var keyword = new List<(Guid Id, double Score)> { (keywordOnlyId, 5.0) };

        var fused = RankFusion.Fuse(semantic, keyword, semanticWeight: 0.6, keywordWeight: 0.4);

        // Each list has only one candidate, so its normalized score is its own
        // raw score clamped to 0..1 (0.8 stays 0.8; 5.0 clamps to 1.0).
        fused.Should().HaveCount(2);
        fused.Single(f => f.Id == semanticOnlyId).FusedScore.Should().BeApproximately(0.6 * 0.8, 1e-9);
        fused.Single(f => f.Id == keywordOnlyId).FusedScore.Should().BeApproximately(0.4 * 1.0, 1e-9);
    }

    [Fact]
    public void Fuse_normalizes_a_list_of_identical_scores_to_the_clamped_raw_score_instead_of_dividing_by_zero()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();

        var semantic = new List<(Guid Id, double Score)> { (idA, 0.42), (idB, 0.42) };
        var keyword = new List<(Guid Id, double Score)>();

        var fused = RankFusion.Fuse(semantic, keyword, semanticWeight: 1.0, keywordWeight: 0.0);

        fused.Should().HaveCount(2);
        fused.Should().OnlyContain(f => f.FusedScore == 0.42);
    }

    [Fact]
    public void Fuse_preserves_a_single_candidates_raw_score_rather_than_forcing_full_relevance()
    {
        var idA = Guid.NewGuid();

        var semantic = new List<(Guid Id, double Score)> { (idA, 0.05) };

        var fused = RankFusion.Fuse(semantic, [], semanticWeight: 1.0, keywordWeight: 0.0);

        fused.Should().ContainSingle();
        fused[0].FusedScore.Should().Be(0.05);
    }

    [Fact]
    public void Fuse_returns_an_empty_list_when_both_inputs_are_empty()
    {
        var fused = RankFusion.Fuse([], [], 0.5, 0.5);

        fused.Should().BeEmpty();
    }

    [Fact]
    public void Fuse_with_an_empty_keyword_list_reduces_to_semantic_only_ordering()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var idC = Guid.NewGuid();

        var semantic = new List<(Guid Id, double Score)> { (idA, 0.9), (idB, 0.5), (idC, 0.1) };

        var fused = RankFusion.Fuse(semantic, [], semanticWeight: 1.0, keywordWeight: 0.0);

        fused.Select(f => f.Id).Should().ContainInOrder(idA, idB, idC);
    }
}
