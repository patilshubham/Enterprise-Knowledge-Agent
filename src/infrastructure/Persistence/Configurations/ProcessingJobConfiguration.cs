using EnterpriseKnowledgeAgent.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>TASK-JOB-01: maps <see cref="ProcessingJob"/> to <c>operations.processing_jobs</c>.</summary>
public sealed class ProcessingJobConfiguration : IEntityTypeConfiguration<ProcessingJob>
{
    public void Configure(EntityTypeBuilder<ProcessingJob> builder)
    {
        builder.ToTable("processing_jobs", "operations");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.ApplicationId).IsRequired();
        builder.Property(j => j.FileId).IsRequired();
        builder.Property(j => j.OutboxMessageId);

        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(j => j.Stage).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(j => j.Progress).IsRequired();

        builder.Property(j => j.WorkerId).HasMaxLength(200);
        builder.Property(j => j.StartedAt);
        builder.Property(j => j.LeaseExpiresAt);
        builder.Property(j => j.AttemptNumber).IsRequired();
        builder.Property(j => j.HeartbeatAt);

        builder.Property(j => j.FailureCategory).HasConversion<string>().HasMaxLength(30);
        builder.Property(j => j.FailureMessage).HasMaxLength(2000);

        builder.Property(j => j.CorrelationId)
            .HasConversion(id => id.ToString(), value => ParseCorrelationId(value))
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(j => j.CreatedAt).IsRequired();
        builder.Property(j => j.CompletedAt);

        // TASK-SIG-02: per-entity monotonic sequence for SignalR stale-event rejection (module 25).
        builder.Property(j => j.EventSequence).IsRequired();

        // ASDD §10: processing_jobs(application_id, status, created_at).
        builder.HasIndex(j => new { j.ApplicationId, j.Status, j.CreatedAt });

        // TASK-JOB-06: the lease-expiry reaper scans running jobs by lease.
        builder.HasIndex(j => new { j.Status, j.LeaseExpiresAt });
    }

    private static Shared.Correlation.CorrelationId ParseCorrelationId(string value) =>
        Shared.Correlation.CorrelationId.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException($"Invalid correlation id '{value}' stored for a processing job.");
}
