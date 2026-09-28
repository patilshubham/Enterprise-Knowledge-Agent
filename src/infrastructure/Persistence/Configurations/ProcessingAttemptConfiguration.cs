using EnterpriseKnowledgeAgent.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>TASK-JOB-01: maps <see cref="ProcessingAttempt"/> to <c>operations.processing_attempts</c>.</summary>
public sealed class ProcessingAttemptConfiguration : IEntityTypeConfiguration<ProcessingAttempt>
{
    public void Configure(EntityTypeBuilder<ProcessingAttempt> builder)
    {
        builder.ToTable("processing_attempts", "operations");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ProcessingJobId).IsRequired();
        builder.Property(a => a.AttemptNumber).IsRequired();
        builder.Property(a => a.WorkerId).HasMaxLength(200).IsRequired();
        builder.Property(a => a.StartedAt).IsRequired();
        builder.Property(a => a.EndedAt);
        builder.Property(a => a.Outcome).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.FailureCategory).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.FailureMessage).HasMaxLength(2000);

        builder.HasIndex(a => a.ProcessingJobId);
    }
}
