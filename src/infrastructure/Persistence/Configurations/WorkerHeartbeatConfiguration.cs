using EnterpriseKnowledgeAgent.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>TASK-JOB-06: maps <see cref="WorkerHeartbeat"/> to <c>operations.worker_heartbeats</c>.</summary>
public sealed class WorkerHeartbeatConfiguration : IEntityTypeConfiguration<WorkerHeartbeat>
{
    public void Configure(EntityTypeBuilder<WorkerHeartbeat> builder)
    {
        builder.ToTable("worker_heartbeats", "operations");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.WorkerId).HasMaxLength(200).IsRequired();
        builder.Property(h => h.WorkerKind).HasMaxLength(100).IsRequired();
        builder.Property(h => h.StartedAt).IsRequired();
        builder.Property(h => h.LastHeartbeatAt).IsRequired();

        builder.HasIndex(h => h.WorkerId).IsUnique();
        builder.HasIndex(h => h.LastHeartbeatAt);
    }
}
