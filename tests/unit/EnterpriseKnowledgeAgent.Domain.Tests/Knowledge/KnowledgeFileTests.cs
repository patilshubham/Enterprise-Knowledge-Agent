using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Domain.Tests.Knowledge;

/// <summary>TASK-KNW-01: unit tests for the KnowledgeFile aggregate's guarded state-transition rules.</summary>
public sealed class KnowledgeFileTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid ApplicationId = Guid.NewGuid();

    private static KnowledgeFile CreateFile() => KnowledgeFile.Create(
        ApplicationId, "notes.md", "hash-1", 1024, "text/markdown", "utf-8", "keycloak-sub-1", Now);

    [Fact]
    public void Create_sets_queued_status_and_provided_fields()
    {
        var file = CreateFile();

        file.ApplicationId.Should().Be(ApplicationId);
        file.DisplayName.Should().Be("notes.md");
        file.ContentHash.Should().Be("hash-1");
        file.FileSize.Should().Be(1024);
        file.MimeType.Should().Be("text/markdown");
        file.Encoding.Should().Be("utf-8");
        file.UploadedBy.Should().Be("keycloak-sub-1");
        file.UploadedAt.Should().Be(Now);
        file.Status.Should().Be(UserFacingFileStatus.Queued);
        file.IsEnabled.Should().BeTrue();
        file.ObjectKey.Should().BeEmpty();
        file.EffectiveStatus.Should().Be(UserFacingFileStatus.Queued);
    }

    [Fact]
    public void SetObjectKey_assigns_the_key()
    {
        var file = CreateFile();

        file.SetObjectKey("applications/app/files/file/source");

        file.ObjectKey.Should().Be("applications/app/files/file/source");
    }

    [Fact]
    public void MarkProcessing_from_Queued_succeeds()
    {
        var file = CreateFile();

        file.MarkProcessing(Now);

        file.Status.Should().Be(UserFacingFileStatus.Processing);
    }

    [Fact]
    public void MarkProcessing_from_Ready_throws()
    {
        var file = CreateFile();
        file.MarkProcessing(Now);
        file.MarkReady(Now);

        var act = () => file.MarkProcessing(Now);

        act.Should().Throw<KnowledgeFileStateConflictException>();
    }

    [Fact]
    public void MarkReady_from_Processing_sets_last_processed_at()
    {
        var file = CreateFile();
        file.MarkProcessing(Now);

        var completedAt = Now.AddMinutes(5);
        file.MarkReady(completedAt);

        file.Status.Should().Be(UserFacingFileStatus.Ready);
        file.LastProcessedAt.Should().Be(completedAt);
    }

    [Fact]
    public void MarkReady_from_Queued_throws()
    {
        var file = CreateFile();

        var act = () => file.MarkReady(Now);

        act.Should().Throw<KnowledgeFileStateConflictException>();
    }

    [Theory]
    [InlineData(UserFacingFileStatus.Queued)]
    [InlineData(UserFacingFileStatus.Processing)]
    public void MarkFailed_from_Queued_or_Processing_succeeds(UserFacingFileStatus initial)
    {
        var file = CreateFile();
        if (initial == UserFacingFileStatus.Processing)
        {
            file.MarkProcessing(Now);
        }

        file.MarkFailed(Now);

        file.Status.Should().Be(UserFacingFileStatus.Failed);
    }

    [Fact]
    public void MarkFailed_from_Ready_throws()
    {
        var file = CreateFile();
        file.MarkProcessing(Now);
        file.MarkReady(Now);

        var act = () => file.MarkFailed(Now);

        act.Should().Throw<KnowledgeFileStateConflictException>();
    }

    [Fact]
    public void Disable_from_Ready_succeeds_and_is_idempotent()
    {
        var file = CreateFile();
        file.MarkProcessing(Now);
        file.MarkReady(Now);

        file.Disable(Now);
        file.Disable(Now); // idempotent

        file.IsEnabled.Should().BeFalse();
        file.EffectiveStatus.Should().Be(UserFacingFileStatus.Disabled);
        file.Status.Should().Be(UserFacingFileStatus.Ready); // underlying status preserved
    }

    [Fact]
    public void Disable_from_Queued_throws()
    {
        var file = CreateFile();

        var act = () => file.Disable(Now);

        act.Should().Throw<KnowledgeFileStateConflictException>();
    }

    [Fact]
    public void Enable_is_idempotent()
    {
        var file = CreateFile();
        file.MarkProcessing(Now);
        file.MarkReady(Now);
        file.Disable(Now);

        file.Enable(Now);
        file.Enable(Now); // idempotent

        file.IsEnabled.Should().BeTrue();
        file.EffectiveStatus.Should().Be(UserFacingFileStatus.Ready);
    }

    [Fact]
    public void MarkDeleting_is_idempotent_from_any_status()
    {
        var file = CreateFile();

        file.MarkDeleting(Now);
        file.MarkDeleting(Now); // idempotent no-op

        file.Status.Should().Be(UserFacingFileStatus.Deleting);
    }

    [Theory]
    [InlineData(UserFacingFileStatus.Ready)]
    [InlineData(UserFacingFileStatus.Failed)]
    public void Reprocess_from_Ready_or_Failed_requeues(UserFacingFileStatus initial)
    {
        var file = CreateFile();
        file.MarkProcessing(Now);
        if (initial == UserFacingFileStatus.Ready)
        {
            file.MarkReady(Now);
        }
        else
        {
            file.MarkFailed(Now);
        }

        file.Reprocess(Now);

        file.Status.Should().Be(UserFacingFileStatus.Queued);
    }

    [Fact]
    public void Reprocess_from_Queued_throws()
    {
        var file = CreateFile();

        var act = () => file.Reprocess(Now);

        act.Should().Throw<KnowledgeFileStateConflictException>();
    }

    private static KnowledgeFile CreateReadyFile()
    {
        var file = CreateFile();
        file.MarkProcessing(Now);
        file.MarkReady(Now);
        return file;
    }

    [Fact]
    public void BeginReplacement_from_Ready_stages_metadata_without_touching_current_content()
    {
        var file = CreateReadyFile();

        file.BeginReplacement("applications/app/files/file/replacement/r1", "hash-2", 2048, "text/plain", "utf-8", Now);

        file.ReplacementObjectKey.Should().Be("applications/app/files/file/replacement/r1");
        file.ReplacementStatus.Should().Be(ReplacementStatus.Processing);
        file.ReplacementContentHash.Should().Be("hash-2");
        file.ReplacementFileSize.Should().Be(2048);
        file.ReplacementMimeType.Should().Be("text/plain");
        file.ReplacementEncoding.Should().Be("utf-8");

        // Current content is untouched and stays Ready throughout.
        file.Status.Should().Be(UserFacingFileStatus.Ready);
        file.ContentHash.Should().Be("hash-1");
        file.ObjectKey.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData(UserFacingFileStatus.Queued)]
    [InlineData(UserFacingFileStatus.Processing)]
    [InlineData(UserFacingFileStatus.Failed)]
    public void BeginReplacement_when_not_Ready_throws(UserFacingFileStatus initial)
    {
        var file = CreateFile();
        if (initial != UserFacingFileStatus.Queued)
        {
            file.MarkProcessing(Now);
        }

        if (initial == UserFacingFileStatus.Failed)
        {
            file.MarkFailed(Now);
        }

        var act = () => file.BeginReplacement("key", "hash", 1, "text/plain", "utf-8", Now);

        act.Should().Throw<KnowledgeFileStateConflictException>();
    }

    [Fact]
    public void BeginReplacement_when_a_replacement_is_already_Processing_throws()
    {
        var file = CreateReadyFile();
        file.BeginReplacement("key-1", "hash-2", 1, "text/plain", "utf-8", Now);

        var act = () => file.BeginReplacement("key-2", "hash-3", 1, "text/plain", "utf-8", Now);

        act.Should().Throw<KnowledgeFileStateConflictException>();
    }

    [Fact]
    public void CompleteReplacement_swaps_staged_fields_into_live_fields_and_returns_previous_object_key()
    {
        var file = CreateReadyFile();
        file.SetObjectKey("applications/app/files/file/source");
        file.BeginReplacement("applications/app/files/file/replacement/r1", "hash-2", 2048, "text/plain", "utf-8", Now);

        var completedAt = Now.AddMinutes(10);
        var previousObjectKey = file.CompleteReplacement(completedAt);

        previousObjectKey.Should().Be("applications/app/files/file/source");
        file.ObjectKey.Should().Be("applications/app/files/file/replacement/r1");
        file.ContentHash.Should().Be("hash-2");
        file.FileSize.Should().Be(2048);
        file.MimeType.Should().Be("text/plain");
        file.Encoding.Should().Be("utf-8");
        file.LastProcessedAt.Should().Be(completedAt);
        file.Status.Should().Be(UserFacingFileStatus.Ready);

        // Staging is cleared.
        file.ReplacementObjectKey.Should().BeNull();
        file.ReplacementStatus.Should().BeNull();
        file.ReplacementContentHash.Should().BeNull();
        file.ReplacementFileSize.Should().BeNull();
        file.ReplacementMimeType.Should().BeNull();
        file.ReplacementEncoding.Should().BeNull();
    }

    [Fact]
    public void CompleteReplacement_without_a_Processing_replacement_throws()
    {
        var file = CreateReadyFile();

        var act = () => file.CompleteReplacement(Now);

        act.Should().Throw<KnowledgeFileStateConflictException>();
    }

    [Fact]
    public void FailReplacement_clears_staging_and_leaves_current_content_and_Status_untouched()
    {
        var file = CreateReadyFile();
        file.SetObjectKey("applications/app/files/file/source");
        file.BeginReplacement("applications/app/files/file/replacement/r1", "hash-2", 2048, "text/plain", "utf-8", Now);

        file.FailReplacement(Now);

        file.ReplacementObjectKey.Should().BeNull();
        file.ReplacementContentHash.Should().BeNull();
        file.ReplacementFileSize.Should().BeNull();
        file.ReplacementMimeType.Should().BeNull();
        file.ReplacementEncoding.Should().BeNull();
        file.ReplacementStatus.Should().Be(ReplacementStatus.Failed);

        // Current content/status are never touched by a failed replacement.
        file.Status.Should().Be(UserFacingFileStatus.Ready);
        file.ObjectKey.Should().Be("applications/app/files/file/source");
        file.ContentHash.Should().Be("hash-1");
    }

    [Fact]
    public void MarkDeleted_sets_Deleted_status_and_timestamp_and_is_idempotent()
    {
        var file = CreateReadyFile();

        var deletedAt = Now.AddDays(1);
        file.MarkDeleted(deletedAt);
        file.MarkDeleted(deletedAt.AddMinutes(5)); // idempotent - second call is a no-op

        file.Status.Should().Be(UserFacingFileStatus.Deleted);
        file.DeletedAt.Should().Be(deletedAt);
        file.EffectiveStatus.Should().Be(UserFacingFileStatus.Deleted);
    }

    [Fact]
    public void EffectiveStatus_is_not_masked_by_Disabled_when_Deleting_or_Deleted()
    {
        var deletingFile = CreateReadyFile();
        deletingFile.Disable(Now);
        deletingFile.MarkDeleting(Now);
        deletingFile.EffectiveStatus.Should().Be(UserFacingFileStatus.Deleting);

        var deletedFile = CreateReadyFile();
        deletedFile.Disable(Now);
        deletedFile.MarkDeleted(Now);
        deletedFile.EffectiveStatus.Should().Be(UserFacingFileStatus.Deleted);
    }
}
