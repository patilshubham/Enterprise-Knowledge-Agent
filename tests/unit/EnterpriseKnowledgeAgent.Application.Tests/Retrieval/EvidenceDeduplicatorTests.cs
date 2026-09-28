using EnterpriseKnowledgeAgent.Application.Retrieval;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Retrieval;

/// <summary>TASK-RET-03: unit tests for duplicate/overlap suppression and neighbor inclusion.</summary>
public sealed class EvidenceDeduplicatorTests
{
    private static EvidenceDeduplicator.Candidate Candidate(
        Guid fileId, int sequenceNumber, string contentHash, int start, int end, double score) =>
        new(Guid.NewGuid(), fileId, sequenceNumber, contentHash, start, end, score);

    [Fact]
    public void Deduplicate_keeps_only_the_highest_scored_candidate_among_exact_content_duplicates()
    {
        var fileId = Guid.NewGuid();
        var best = Candidate(fileId, 0, "hash-1", 0, 100, score: 0.9);
        var worse = Candidate(fileId, 5, "hash-1", 500, 600, score: 0.4);

        var result = EvidenceDeduplicator.Deduplicate([worse, best]);

        result.Should().ContainSingle();
        result[0].Score.Should().Be(0.9);
    }

    [Fact]
    public void Deduplicate_suppresses_a_lower_scored_candidate_whose_character_range_overlaps_a_higher_scored_one_in_the_same_file()
    {
        var fileId = Guid.NewGuid();
        var best = Candidate(fileId, 0, "hash-a", start: 0, end: 100, score: 0.9);
        var overlapping = Candidate(fileId, 1, "hash-b", start: 50, end: 150, score: 0.5);

        var result = EvidenceDeduplicator.Deduplicate([overlapping, best]);

        result.Should().ContainSingle();
        result[0].ContentHash.Should().Be("hash-a");
    }

    [Fact]
    public void Deduplicate_keeps_non_overlapping_candidates_in_the_same_file()
    {
        var fileId = Guid.NewGuid();
        var first = Candidate(fileId, 0, "hash-a", start: 0, end: 100, score: 0.9);
        var second = Candidate(fileId, 1, "hash-b", start: 100, end: 200, score: 0.5);

        var result = EvidenceDeduplicator.Deduplicate([first, second]);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Deduplicate_never_suppresses_overlapping_offsets_across_different_files()
    {
        var fileA = Guid.NewGuid();
        var fileB = Guid.NewGuid();
        var a = Candidate(fileA, 0, "hash-a", start: 0, end: 100, score: 0.9);
        var b = Candidate(fileB, 0, "hash-b", start: 0, end: 100, score: 0.5);

        var result = EvidenceDeduplicator.Deduplicate([a, b]);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Deduplicate_returns_candidates_ordered_by_score_descending()
    {
        var fileId = Guid.NewGuid();
        var low = Candidate(fileId, 0, "hash-a", 0, 10, score: 0.1);
        var high = Candidate(fileId, 1, "hash-b", 100, 110, score: 0.9);
        var mid = Candidate(fileId, 2, "hash-c", 200, 210, score: 0.5);

        var result = EvidenceDeduplicator.Deduplicate([low, high, mid]);

        result.Select(c => c.Score).Should().ContainInOrder(0.9, 0.5, 0.1);
    }

    private static EvidenceDeduplicator.FileChunk FileChunk(int sequenceNumber, string contentHash = "n-hash") =>
        new(Guid.NewGuid(), sequenceNumber, contentHash, sequenceNumber * 100, (sequenceNumber * 100) + 100);

    [Fact]
    public void IncludeNeighbors_adds_the_previous_and_next_sequence_chunks_from_the_same_file()
    {
        var fileId = Guid.NewGuid();
        var selected = new[] { Candidate(fileId, 5, "hash-selected", 500, 600, score: 0.8) };
        var chunksByFile = new Dictionary<Guid, IReadOnlyList<EvidenceDeduplicator.FileChunk>>
        {
            [fileId] = [FileChunk(4), FileChunk(5), FileChunk(6)],
        };

        var result = EvidenceDeduplicator.IncludeNeighbors(selected, chunksByFile);

        result.Should().HaveCount(3);
        result.Select(c => c.SequenceNumber).Should().Contain([4, 5, 6]);
    }

    [Fact]
    public void IncludeNeighbors_gives_added_neighbors_the_parents_score_so_they_still_pass_the_threshold()
    {
        var fileId = Guid.NewGuid();
        var selected = new[] { Candidate(fileId, 5, "hash-selected", 500, 600, score: 0.77) };
        var chunksByFile = new Dictionary<Guid, IReadOnlyList<EvidenceDeduplicator.FileChunk>>
        {
            [fileId] = [FileChunk(4), FileChunk(5)],
        };

        var result = EvidenceDeduplicator.IncludeNeighbors(selected, chunksByFile);

        result.Single(c => c.SequenceNumber == 4).Score.Should().Be(0.77);
    }

    [Fact]
    public void IncludeNeighbors_does_not_duplicate_a_neighbor_that_is_already_selected()
    {
        var fileId = Guid.NewGuid();
        var selected = new[]
        {
            Candidate(fileId, 5, "hash-5", 500, 600, score: 0.8),
            Candidate(fileId, 6, "hash-6", 600, 700, score: 0.7),
        };
        var chunksByFile = new Dictionary<Guid, IReadOnlyList<EvidenceDeduplicator.FileChunk>>
        {
            [fileId] = [FileChunk(4), FileChunk(5), FileChunk(6), FileChunk(7)],
        };

        var result = EvidenceDeduplicator.IncludeNeighbors(selected, chunksByFile);

        result.Should().HaveCount(4);
        result.Select(c => c.SequenceNumber).Should().BeEquivalentTo([4, 5, 6, 7]);
    }

    [Fact]
    public void IncludeNeighbors_never_crosses_into_a_different_file_even_if_no_lookup_entry_exists_for_the_candidates_file()
    {
        var fileId = Guid.NewGuid();
        var otherFileId = Guid.NewGuid();
        var selected = new[] { Candidate(fileId, 5, "hash-5", 500, 600, score: 0.8) };
        var chunksByFile = new Dictionary<Guid, IReadOnlyList<EvidenceDeduplicator.FileChunk>>
        {
            [otherFileId] = [FileChunk(4), FileChunk(6)],
        };

        var result = EvidenceDeduplicator.IncludeNeighbors(selected, chunksByFile);

        result.Should().ContainSingle();
    }

    [Fact]
    public void IncludeNeighbors_skips_a_sequence_number_that_does_not_exist_in_the_file()
    {
        var fileId = Guid.NewGuid();
        var selected = new[] { Candidate(fileId, 0, "hash-0", 0, 100, score: 0.8) };
        var chunksByFile = new Dictionary<Guid, IReadOnlyList<EvidenceDeduplicator.FileChunk>>
        {
            [fileId] = [FileChunk(0)],
        };

        var result = EvidenceDeduplicator.IncludeNeighbors(selected, chunksByFile);

        result.Should().ContainSingle();
    }
}
