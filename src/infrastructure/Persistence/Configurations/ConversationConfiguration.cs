using EnterpriseKnowledgeAgent.Domain.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>Module CHAT: maps <see cref="Conversation"/> to <c>chat.conversations</c>.</summary>
public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations", "chat");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ApplicationId).IsRequired();

        builder.Property(c => c.Title)
            .HasMaxLength(Conversation.MaxTitleLength)
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);
        builder.Property(c => c.LastMessageAt).IsRequired();
        builder.Property(c => c.IsDeleted).IsRequired();

        builder.Property(c => c.KnowledgeScopeType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.SelectedFileIdsJson);

        builder.Property(c => c.GenerationDeployment)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Temperature).IsRequired();

        builder.Property(c => c.RetrievalMode)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.RetrievalCount).IsRequired();
        builder.Property(c => c.MinimumSimilarity).IsRequired();
        builder.Property(c => c.MaximumEvidenceTokens).IsRequired();
        builder.Property(c => c.IncludeNeighbors).IsRequired();
        builder.Property(c => c.CitationsRequired).IsRequired();

        builder.Property(c => c.SystemPrompt)
            .HasMaxLength(Conversation.MaxSystemPromptLength);

        // Postgres' xmin system column (see AgentConfiguration) - optimistic
        // concurrency for renames/deletes.
        builder.Property(c => c.ConcurrencyToken)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Ignore(c => c.SelectedFileIds);

        // The message list is exposed read-only over a private backing field.
        builder.HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Conversation.Messages))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Sidebar listing is scoped to (application, not-deleted) ordered by recency.
        builder.HasIndex(c => new { c.ApplicationId, c.IsDeleted, c.LastMessageAt });
    }
}
