using EnterpriseKnowledgeAgent.Domain.Applications;
using EnterpriseKnowledgeAgent.Shared.Applications;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Domain.Tests.Applications;

/// <summary>TASK-APP-01: unit tests for the Application aggregate's guarded state-transition rules.</summary>
public sealed class ApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_sets_active_status_and_provided_fields()
    {
        var app = Application.Create("Contoso Support", "Support knowledge base", "keycloak-sub-1", Now);

        app.Name.Should().Be("Contoso Support");
        app.Description.Should().Be("Support knowledge base");
        app.Status.Should().Be(ApplicationStatus.Active);
        app.AllowViewerExecution.Should().BeFalse();
        app.CreatedBy.Should().Be("keycloak-sub-1");
        app.CreatedAt.Should().Be(Now);
        app.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public void Create_trims_name_and_description()
    {
        var app = Application.Create("  Contoso  ", "  desc  ", "sub", Now);

        app.Name.Should().Be("Contoso");
        app.Description.Should().Be("desc");
    }

    [Fact]
    public void Create_normalizes_blank_description_to_null()
    {
        var app = Application.Create("Contoso", "   ", "sub", Now);

        app.Description.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_name(string? name)
    {
        var act = () => Application.Create(name!, null, "sub", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_name_over_max_length()
    {
        var tooLong = new string('a', Application.MaxNameLength + 1);

        var act = () => Application.Create(tooLong, null, "sub", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Archive_from_active_moves_to_archived_and_readonly()
    {
        var app = Application.Create("Contoso", null, "sub", Now);

        app.Archive(Now.AddHours(1));

        app.Status.Should().Be(ApplicationStatus.Archived);
        app.ArchivedAt.Should().Be(Now.AddHours(1));
        app.IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public void Archive_from_archived_throws_conflict()
    {
        var app = Application.Create("Contoso", null, "sub", Now);
        app.Archive(Now);

        var act = () => app.Archive(Now);

        act.Should().Throw<ApplicationStateConflictException>();
    }

    [Fact]
    public void Archive_from_deleted_throws_conflict()
    {
        var app = Application.Create("Contoso", null, "sub", Now);
        app.Delete(Now);

        var act = () => app.Archive(Now);

        act.Should().Throw<ApplicationStateConflictException>();
    }

    [Fact]
    public void Restore_from_archived_moves_to_active()
    {
        var app = Application.Create("Contoso", null, "sub", Now);
        app.Archive(Now);

        app.Restore(Now.AddHours(1));

        app.Status.Should().Be(ApplicationStatus.Active);
        app.ArchivedAt.Should().BeNull();
        app.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public void Restore_from_active_throws_conflict()
    {
        var app = Application.Create("Contoso", null, "sub", Now);

        var act = () => app.Restore(Now);

        act.Should().Throw<ApplicationStateConflictException>();
    }

    [Fact]
    public void Restore_from_deleted_throws_conflict()
    {
        var app = Application.Create("Contoso", null, "sub", Now);
        app.Delete(Now);

        var act = () => app.Restore(Now);

        act.Should().Throw<ApplicationStateConflictException>();
    }

    [Fact]
    public void Delete_from_active_is_terminal()
    {
        var app = Application.Create("Contoso", null, "sub", Now);

        app.Delete(Now);

        app.Status.Should().Be(ApplicationStatus.Deleted);
        app.DeletedAt.Should().Be(Now);
        app.IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public void Delete_from_archived_is_terminal()
    {
        var app = Application.Create("Contoso", null, "sub", Now);
        app.Archive(Now);

        app.Delete(Now.AddHours(1));

        app.Status.Should().Be(ApplicationStatus.Deleted);
    }

    [Fact]
    public void Delete_is_idempotent_when_already_deleted()
    {
        var app = Application.Create("Contoso", null, "sub", Now);
        app.Delete(Now);

        var act = () => app.Delete(Now.AddHours(1));

        act.Should().NotThrow();
        app.DeletedAt.Should().Be(Now); // unchanged by the second, no-op call
    }

    [Fact]
    public void Update_changes_name_and_description_while_active()
    {
        var app = Application.Create("Contoso", "old", "sub", Now);

        app.Update("Contoso Renamed", "new", Now.AddHours(1));

        app.Name.Should().Be("Contoso Renamed");
        app.Description.Should().Be("new");
        app.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Update_allowed_while_archived()
    {
        var app = Application.Create("Contoso", null, "sub", Now);
        app.Archive(Now);

        var act = () => app.Update("Renamed", null, Now.AddHours(1));

        act.Should().NotThrow();
        app.Name.Should().Be("Renamed");
    }

    [Fact]
    public void Update_after_delete_throws_conflict()
    {
        var app = Application.Create("Contoso", null, "sub", Now);
        app.Delete(Now);

        var act = () => app.Update("Renamed", null, Now.AddHours(1));

        act.Should().Throw<ApplicationStateConflictException>();
    }

    [Fact]
    public void SetAllowViewerExecution_toggles_flag()
    {
        var app = Application.Create("Contoso", null, "sub", Now);

        app.SetAllowViewerExecution(true, Now.AddHours(1));

        app.AllowViewerExecution.Should().BeTrue();
        app.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void SetAllowViewerExecution_after_delete_throws_conflict()
    {
        var app = Application.Create("Contoso", null, "sub", Now);
        app.Delete(Now);

        var act = () => app.SetAllowViewerExecution(true, Now.AddHours(1));

        act.Should().Throw<ApplicationStateConflictException>();
    }
}
