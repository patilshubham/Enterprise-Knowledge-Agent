using EnterpriseKnowledgeAgent.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// TASK-CHK-02/TASK-PG-02: maps <see cref="KnowledgeChunk"/> to
/// <c>knowledge.knowledge_chunks</c>, including the pgvector
/// <see cref="KnowledgeChunk.Embedding"/> column and the DB-maintained
/// <c>search_vector tsvector</c> generated column (weighted: heading path
/// 'A', content 'B' - TASK-PG-04 heading-path weighting) with its GIN index.
/// The embedding column's dimension (1536, matching the .env.example
/// <c>AZURE_OPENAI_EMBEDDING_DIMENSIONS</c> default/D10) is fixed at the
/// database schema level - changing the configured embedding model's
/// dimension count requires a new migration to alter this column.
/// </summary>
public sealed class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    /// <summary>Must match the configured Azure OpenAI embedding deployment's output dimension (D10).</summary>
    public const int EmbeddingDimensions = 1536;

    public void Configure(EntityTypeBuilder<KnowledgeChunk> builder)
    {
        builder.ToTable("knowledge_chunks", "knowledge");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ApplicationId).IsRequired();
        builder.Property(c => c.FileId).IsRequired();
        builder.Property(c => c.SequenceNumber).IsRequired();
        builder.Property(c => c.HeadingPath).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.Content).IsRequired();
        builder.Property(c => c.TokenCount).IsRequired();
        builder.Property(c => c.CharacterStart).IsRequired();
        builder.Property(c => c.CharacterEnd).IsRequired();
        builder.Property(c => c.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();

        builder.Property(c => c.Embedding).HasColumnName("embedding").HasColumnType($"vector({EmbeddingDimensions})");
        builder.Property(c => c.EmbeddingDeployment).HasColumnName("embedding_deployment").HasMaxLength(200);
        builder.Property(c => c.EmbeddingDimensions).HasColumnName("embedding_dimensions");

        // TASK-PG-02/04: generated tsvector over heading path (weight A,
        // higher relevance) + content (weight B), maintained by Postgres
        // itself (STORED generated column) - never written from the app.
        // Column names are double-quoted because this project does not use
        // a snake_case naming convention: EF Core's default (and this
        // project's) column names are the verbatim (PascalCase) CLR
        // property names, which Postgres always stores/matches case-
        // sensitively once quoted.
        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                "setweight(to_tsvector('english', coalesce(\"HeadingPath\", '')), 'A') || " +
                "setweight(to_tsvector('english', coalesce(\"Content\", '')), 'B')",
                stored: true);

        // Listing/retrieval by file, in sequence order.
        builder.HasIndex(c => new { c.FileId, c.SequenceNumber });

        // ASDD §13.3: retrieval only ever queries active chunks scoped by application.
        builder.HasIndex(c => new { c.ApplicationId, c.IsActive });

        // TASK-PG-02: explicit (application_id, file_id, is_active) composite per spec.
        builder.HasIndex(c => new { c.ApplicationId, c.FileId, c.IsActive });

        // TASK-PG-02/04: GIN index so FTS queries use the index instead of a sequential scan.
        builder.HasIndex("SearchVector").HasMethod("GIN");
    }
}
