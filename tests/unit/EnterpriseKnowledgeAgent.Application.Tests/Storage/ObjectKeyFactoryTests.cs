using EnterpriseKnowledgeAgent.Application.Storage;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Storage;

/// <summary>
/// TASK-S3-02: canonical object-key generation and the cross-application
/// manipulation guard (FR-SEC-003).
/// </summary>
public sealed class ObjectKeyFactoryTests
{
    [Fact]
    public void Source_produces_canonical_key_shape()
    {
        var applicationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var fileId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var key = ObjectKeyFactory.Source(applicationId, fileId);

        key.Should().Be($"applications/{applicationId}/files/{fileId}/source");
    }

    [Fact]
    public void Replacement_produces_canonical_key_shape()
    {
        var applicationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var replacementId = Guid.NewGuid();

        var key = ObjectKeyFactory.Replacement(applicationId, fileId, replacementId);

        key.Should().Be($"applications/{applicationId}/files/{fileId}/replacement/{replacementId}");
    }

    [Fact]
    public void TryGetApplicationId_parses_the_embedded_application_id()
    {
        var applicationId = Guid.NewGuid();
        var key = ObjectKeyFactory.Source(applicationId, Guid.NewGuid());

        var success = ObjectKeyFactory.TryGetApplicationId(key, out var parsed);

        success.Should().BeTrue();
        parsed.Should().Be(applicationId);
    }

    [Theory]
    [InlineData("not-a-key")]
    [InlineData("applications//files/x/source")]
    [InlineData("applications/not-a-guid/files/x/source")]
    [InlineData("")]
    public void TryGetApplicationId_rejects_malformed_keys(string key)
    {
        var success = ObjectKeyFactory.TryGetApplicationId(key, out var parsed);

        success.Should().BeFalse();
        parsed.Should().Be(Guid.Empty);
    }

    [Fact]
    public void RequireApplication_succeeds_when_key_belongs_to_the_authorized_application()
    {
        var applicationId = Guid.NewGuid();
        var key = ObjectKeyFactory.Source(applicationId, Guid.NewGuid());

        var act = () => ObjectKeyFactory.RequireApplication(key, applicationId);

        act.Should().NotThrow();
    }

    [Fact]
    public void RequireApplication_throws_when_key_belongs_to_a_different_application()
    {
        var ownerApplicationId = Guid.NewGuid();
        var attackerApplicationId = Guid.NewGuid();
        var key = ObjectKeyFactory.Source(ownerApplicationId, Guid.NewGuid());

        var act = () => ObjectKeyFactory.RequireApplication(key, attackerApplicationId);

        act.Should().Throw<CrossApplicationObjectAccessException>()
            .Which.Key.Should().Be(key);
    }

    [Fact]
    public void RequireApplication_throws_for_a_forged_key_that_does_not_match_the_canonical_pattern()
    {
        var authorizedApplicationId = Guid.NewGuid();
        const string forgedKey = "../../etc/passwd";

        var act = () => ObjectKeyFactory.RequireApplication(forgedKey, authorizedApplicationId);

        act.Should().Throw<CrossApplicationObjectAccessException>();
    }
}
