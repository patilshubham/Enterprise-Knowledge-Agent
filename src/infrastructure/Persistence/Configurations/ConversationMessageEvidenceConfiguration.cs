using EnterpriseKnowledgeAgent.Domain.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>Module CHAT: maps <see cref="ConversationMessageEvidence"/> to <c>chat.conversation_message_evidence</c> (mirrors <c>RunEvidence</c>: snapshot, no FK).</summary>
public sealed class ConversationMessageEvidenceConfiguration : IEntityTypeConfiguration<ConversationMessageEvidence>
{
    public void Configure(EntityTypeBuilder<ConversationMessageEvidence> builder)
    {
        builder.ToTable("conversation_message_evidence", "chat");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ConversationId).IsRequired();
        builder.Property(e => e.ConversationMessageId).IsRequired();
        builder.Property(e => e.SourceId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.KnowledgeChunkId).IsRequired();
        builder.Property(e => e.KnowledgeFileId).IsRequired();
        builder.Property(e => e.FileNameSnapshot).HasMaxLength(500).IsRequired();
        builder.Property(e => e.HeadingPathSnapshot).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.PassageSnapshot).IsRequired();
        builder.Property(e => e.CharacterStartSnapshot).IsRequired();
        builder.Property(e => e.CharacterEndSnapshot).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        // One SOURCE ID per assistant message; detail lookups are by message or conversation.
        builder.HasIndex(e => new { e.ConversationMessageId, e.SourceId }).IsUnique();
        builder.HasIndex(e => e.ConversationId);
    }
}
