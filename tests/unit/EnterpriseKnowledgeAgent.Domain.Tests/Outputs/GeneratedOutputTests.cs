using EnterpriseKnowledgeAgent.Domain.Outputs;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Domain.Tests.Outputs;

/// <summary>TASK-OUT-01: unit tests for <see cref="GeneratedOutput"/>'s factory.</summary>
public sealed class GeneratedOutputTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_sets_current_markdown_to_the_original_and_marks_it_unedited()
    {
        var applicationId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();

        var output = GeneratedOutput.Create(applicationId, agentRunId, "Q3 Summary", "# Original\n\nBody.", "keycloak-sub-1", Now);

        output.Id.Should().NotBeEmpty();
        output.ApplicationId.Should().Be(applicationId);
        output.AgentRunId.Should().Be(agentRunId);
        output.Title.Should().Be("Q3 Summary");
        output.OriginalMarkdown.Should().Be("# Original\n\nBody.");
        output.CurrentMarkdown.Should().Be(output.OriginalMarkdown);
        output.IsEdited.Should().BeFalse();
        output.IsDeleted.Should().BeFalse();
        output.CreatedBy.Should().Be("keycloak-sub-1");
        output.CreatedAt.Should().Be(Now);
        output.LastEditedBy.Should().BeNull();
        output.LastEditedAt.Should().BeNull();
    }

    [Fact]
    public void Edit_updates_current_markdown_but_leaves_the_original_untouched()
    {
        var output = GeneratedOutput.Create(Guid.NewGuid(), Guid.NewGuid(), "Q3 Summary", "# Original\n\nBody.", "keycloak-sub-1", Now);
        var editedAt = Now.AddHours(2);

        output.Edit("# Edited\n\nNew body.", "keycloak-sub-2", editedAt);

        output.CurrentMarkdown.Should().Be("# Edited\n\nNew body.");
        output.OriginalMarkdown.Should().Be("# Original\n\nBody.");
        output.IsEdited.Should().BeTrue();
        output.LastEditedBy.Should().Be("keycloak-sub-2");
        output.LastEditedAt.Should().Be(editedAt);
    }

    [Fact]
    public void Edit_stays_marked_as_edited_even_if_a_later_edit_restores_the_original_text()
    {
        var output = GeneratedOutput.Create(Guid.NewGuid(), Guid.NewGuid(), "Q3 Summary", "# Original\n\nBody.", "keycloak-sub-1", Now);
        output.Edit("# Edited\n\nNew body.", "keycloak-sub-2", Now.AddHours(1));

        output.Edit("# Original\n\nBody.", "keycloak-sub-2", Now.AddHours(2));

        output.CurrentMarkdown.Should().Be(output.OriginalMarkdown);
        output.IsEdited.Should().BeTrue();
    }

    [Fact]
    public void SoftDelete_marks_the_output_deleted()
    {
        var output = GeneratedOutput.Create(Guid.NewGuid(), Guid.NewGuid(), "Q3 Summary", "# Original\n\nBody.", "keycloak-sub-1", Now);

        output.SoftDelete();

        output.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void SoftDelete_is_idempotent_when_called_more_than_once()
    {
        var output = GeneratedOutput.Create(Guid.NewGuid(), Guid.NewGuid(), "Q3 Summary", "# Original\n\nBody.", "keycloak-sub-1", Now);

        output.SoftDelete();
        var act = output.SoftDelete;

        act.Should().NotThrow();
        output.IsDeleted.Should().BeTrue();
    }
}
