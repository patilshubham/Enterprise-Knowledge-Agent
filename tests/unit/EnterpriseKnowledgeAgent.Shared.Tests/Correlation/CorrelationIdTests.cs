using System.Text.Json;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Shared.Tests.Correlation;

public class CorrelationIdTests
{
    [Fact]
    public void New_ShouldProduceDistinctValues()
    {
        var first = CorrelationId.New();
        var second = CorrelationId.New();

        first.Should().NotBe(second);
    }

    [Fact]
    public void TryParse_WithValidGuidString_ShouldSucceedAndRoundTrip()
    {
        var guid = Guid.NewGuid();

        var parsed = CorrelationId.TryParse(guid.ToString(), out var correlationId);

        parsed.Should().BeTrue();
        correlationId.ToString().Should().Be(guid.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void TryParse_WithInvalidInput_ShouldFail(string? value)
    {
        var parsed = CorrelationId.TryParse(value, out _);

        parsed.Should().BeFalse();
    }

    [Fact]
    public void Json_RoundTrip_ShouldPreserveValue()
    {
        var original = CorrelationId.From(Guid.NewGuid());

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<CorrelationId>(json);

        deserialized.Should().Be(original);
    }
}
