using EnterpriseKnowledgeAgent.Domain.Knowledge;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Domain.Tests.Knowledge;

/// <summary>TASK-CHK-02: unit tests for the KnowledgeChunk entity.</summary>
public sealed class KnowledgeChunkTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_is_inactive_by_default()
    {
        var chunk = KnowledgeChunk.Create(
            Guid.NewGuid(), Guid.NewGuid(), 0, "H1 > H2", "content", 42, 0, 7, "hash", Now);

        chunk.IsActive.Should().BeFalse();
        chunk.SequenceNumber.Should().Be(0);
        chunk.HeadingPath.Should().Be("H1 > H2");
        chunk.Content.Should().Be("content");
        chunk.TokenCount.Should().Be(42);
        chunk.CharacterStart.Should().Be(0);
        chunk.CharacterEnd.Should().Be(7);
        chunk.ContentHash.Should().Be("hash");
        chunk.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Activate_sets_is_active_true()
    {
        var chunk = KnowledgeChunk.Create(Guid.NewGuid(), Guid.NewGuid(), 0, string.Empty, "c", 1, 0, 1, "h", Now);

        chunk.Activate();

        chunk.IsActive.Should().BeTrue();
    }
}
