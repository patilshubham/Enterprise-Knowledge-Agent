using EnterpriseKnowledgeAgent.Application.Chunking;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Chunking;

/// <summary>TASK-CHK-02: unit tests for assigning sequence numbers/content hashes and materializing inactive chunk entities.</summary>
public sealed class ChunkPersistencePreparerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Prepare_assigns_sequential_sequence_numbers_in_order()
    {
        var drafts = new[]
        {
            new ChunkDraft("H1", "first", 1, 0, 5),
            new ChunkDraft("H1", "second", 1, 5, 11),
            new ChunkDraft("H1", "third", 1, 11, 16),
        };

        var chunks = ChunkPersistencePreparer.Prepare(Guid.NewGuid(), Guid.NewGuid(), drafts, Now);

        chunks.Select(c => c.SequenceNumber).Should().Equal(0, 1, 2);
    }

    [Fact]
    public void Prepare_creates_chunks_inactive()
    {
        var drafts = new[] { new ChunkDraft("H1", "content", 1, 0, 7) };

        var chunks = ChunkPersistencePreparer.Prepare(Guid.NewGuid(), Guid.NewGuid(), drafts, Now);

        chunks.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public void Prepare_computes_a_stable_content_hash_for_identical_content()
    {
        var hashA = ChunkPersistencePreparer.ComputeContentHash("identical content");
        var hashB = ChunkPersistencePreparer.ComputeContentHash("identical content");

        hashA.Should().Be(hashB);
    }

    [Fact]
    public void Prepare_computes_different_hashes_for_different_content()
    {
        var hashA = ChunkPersistencePreparer.ComputeContentHash("content A");
        var hashB = ChunkPersistencePreparer.ComputeContentHash("content B");

        hashA.Should().NotBe(hashB);
    }

    [Fact]
    public void Prepare_carries_through_draft_fields()
    {
        var applicationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var draft = new ChunkDraft("H1 > H2", "some content", 42, 10, 22);

        var chunk = ChunkPersistencePreparer.Prepare(applicationId, fileId, [draft], Now).Single();

        chunk.ApplicationId.Should().Be(applicationId);
        chunk.FileId.Should().Be(fileId);
        chunk.HeadingPath.Should().Be("H1 > H2");
        chunk.Content.Should().Be("some content");
        chunk.TokenCount.Should().Be(42);
        chunk.CharacterStart.Should().Be(10);
        chunk.CharacterEnd.Should().Be(22);
        chunk.CreatedAt.Should().Be(Now);
        chunk.ContentHash.Should().Be(ChunkPersistencePreparer.ComputeContentHash("some content"));
    }
}
