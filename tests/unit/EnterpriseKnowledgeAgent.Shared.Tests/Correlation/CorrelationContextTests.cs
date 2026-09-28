using EnterpriseKnowledgeAgent.Shared.Correlation;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Shared.Tests.Correlation;

/// <summary>TASK-OBS-01: the ambient (AsyncLocal) correlation-id holder shared by the Api and both workers.</summary>
public sealed class CorrelationContextTests
{
    [Fact]
    public void CorrelationId_defaults_to_null_when_unset()
    {
        // A fresh AsyncLocal flow (this test method's own async context) has
        // never had CorrelationContext.CorrelationId assigned.
        CorrelationContext.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void CorrelationId_returns_whatever_was_set()
    {
        var value = CorrelationId.New();

        CorrelationContext.CorrelationId = value;

        CorrelationContext.CorrelationId.Should().Be(value);
    }

    [Fact]
    public void CorrelationIdOrNew_returns_the_ambient_value_when_set()
    {
        var value = CorrelationId.New();
        CorrelationContext.CorrelationId = value;

        CorrelationContext.CorrelationIdOrNew().Should().Be(value);
    }

    [Fact]
    public void CorrelationIdOrNew_generates_a_fresh_value_each_time_when_unset()
    {
        CorrelationContext.CorrelationId = null;

        var first = CorrelationContext.CorrelationIdOrNew();
        var second = CorrelationContext.CorrelationIdOrNew();

        first.Should().NotBe(second);
    }

    [Fact]
    public void HeaderName_is_the_expected_correlation_header()
    {
        CorrelationContext.HeaderName.Should().Be("X-Correlation-Id");
    }
}
