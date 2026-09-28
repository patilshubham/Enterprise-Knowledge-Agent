using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AgentEntity = EnterpriseKnowledgeAgent.Domain.Agents.Agent;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>TASK-AGT-01: maps <see cref="AgentEntity"/> to <c>agents.agents</c>.</summary>
public sealed class AgentConfiguration : IEntityTypeConfiguration<AgentEntity>
{
    public void Configure(EntityTypeBuilder<AgentEntity> builder)
    {
        builder.ToTable("agents", "agents");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ApplicationId).IsRequired();

        builder.Property(a => a.Name)
            .HasMaxLength(AgentEntity.MaxNameLength)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasMaxLength(AgentEntity.MaxDescriptionLength);

        builder.Property(a => a.Prompt)
            .HasMaxLength(AgentEntity.MaxPromptLength)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.KnowledgeScopeType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.SelectedFileIdsJson);

        builder.Property(a => a.GenerationDeployment)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.Temperature).IsRequired();
        builder.Property(a => a.MaximumOutputTokens).IsRequired();
        builder.Property(a => a.TopP);

        builder.Property(a => a.RetrievalMode)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.RetrievalCount).IsRequired();
        builder.Property(a => a.MinimumSimilarity).IsRequired();
        builder.Property(a => a.MaximumEvidenceTokens).IsRequired();
        builder.Property(a => a.IncludeNeighbors).IsRequired();
        builder.Property(a => a.StrictGrounding).IsRequired();
        builder.Property(a => a.CitationsRequired).IsRequired();

        builder.Property(a => a.CreatedBy)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.CreatedAt).IsRequired();

        builder.Property(a => a.UpdatedBy)
            .HasMaxLength(255);

        builder.Property(a => a.UpdatedAt);

        // TASK-AGT-01's "ConcurrencyToken" (ASDD §9.3): Postgres' xmin system
        // column mapped to a real (not shadow) property so it round-trips
        // into API responses - EF bumps it automatically on every UPDATE and
        // uses it to detect lost updates (a concurrent edit throws
        // DbUpdateConcurrencyException, mapped by the Api layer to 409).
        builder.Property(a => a.ConcurrencyToken)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Ignore(a => a.SelectedFileIds);

        builder.HasIndex(a => new { a.ApplicationId, a.Status });
    }
}
