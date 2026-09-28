using EnterpriseKnowledgeAgent.Domain.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>Module CHAT: maps <see cref="ConversationMessage"/> to <c>chat.conversation_messages</c>.</summary>
public sealed class ConversationMessageConfiguration : IEntityTypeConfiguration<ConversationMessage>
{
    public void Configure(EntityTypeBuilder<ConversationMessage> builder)
    {
        builder.ToTable("conversation_messages", "chat");

        builder.HasKey(m => m.Id);

        // The domain assigns message ids (Guid.NewGuid) up front. Without this,
        // EF's default convention treats the Guid key as store-generated; when a
        // new message is added to an already-tracked conversation's collection,
        // EF's graph-attach then sees "a store-generated key that already has a
        // value" and marks the message Modified (emitting an UPDATE that matches
        // zero rows -> DbUpdateConcurrencyException) instead of Added/INSERT.
        // Declaring the key client-supplied makes new messages track as Added.
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.ConversationId).IsRequired();

        builder.Property(m => m.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Assistant answers are unbounded Markdown; the domain caps user input.
        builder.Property(m => m.Content).IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Sequence).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.CompletedAt);

        builder.Property(m => m.CitedSourceIdsJson);

        builder.Property(m => m.FailureCategory)
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(m => m.FailureMessage)
            .HasMaxLength(1000);

        builder.Ignore(m => m.CitedSourceIds);

        builder.HasIndex(m => new { m.ConversationId, m.Sequence }).IsUnique();
    }
}
