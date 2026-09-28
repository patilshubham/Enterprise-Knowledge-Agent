using System.Reflection;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Shared.Tests.Messaging;

/// <summary>
/// Enforces FR-PRC-001 ("File content shall not be placed in the queue
/// message") at the type level: no queue message may expose a property that
/// looks like it carries raw content, prompt text, or secrets. New message
/// types are picked up automatically via reflection over the Shared assembly.
/// </summary>
public class QueueMessageContentSafetyTests
{
    private static readonly string[] ForbiddenPropertyNameFragments =
    [
        "content",
        "prompt",
        "secret",
        "password",
        "token",
        "apikey",
    ];

    public static IEnumerable<object[]> QueueMessageTypes()
    {
        var assembly = typeof(IQueueMessage).Assembly;

        return assembly.GetTypes()
            .Where(t => typeof(IQueueMessage).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(t => new object[] { t });
    }

    [Theory]
    [MemberData(nameof(QueueMessageTypes))]
    public void QueueMessage_ShouldNotExposeContentOrSecretProperties(Type messageType)
    {
        var offendingProperties = messageType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => ForbiddenPropertyNameFragments.Any(fragment =>
                p.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .Select(p => p.Name)
            .ToList();

        offendingProperties.Should().BeEmpty(
            $"queue message '{messageType.Name}' must carry identifiers only, never raw content/prompt/secret data");
    }

    [Fact]
    public void QueueMessageTypes_ShouldIncludeAllKnownMessages()
    {
        var discovered = QueueMessageTypes().Select(args => (Type)args[0]).ToList();

        discovered.Should().Contain(
        [
            typeof(DocumentProcessingMessage),
            typeof(AgentExecutionMessage),
            typeof(DeletionMessage),
        ]);
    }
}
