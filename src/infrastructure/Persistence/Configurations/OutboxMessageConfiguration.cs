using EnterpriseKnowledgeAgent.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>TASK-JOB-04: maps <see cref="OutboxMessage"/> to <c>operations.outbox_messages</c>.</summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", "operations");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MessageType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(m => m.PayloadJson).IsRequired();
        builder.Property(m => m.Delivered).IsRequired();
        builder.Property(m => m.DeliveryAttempts).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(2000);
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.DeliveredAt);

        // TASK-JOB-04/05: publisher/reconciler poll for undelivered rows.
        builder.HasIndex(m => new { m.Delivered, m.CreatedAt });
    }
}
