using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EnterpriseKnowledgeAgent.Shared.Correlation;

/// <summary>
/// A strongly typed correlation identifier propagated across HTTP requests,
/// queue messages, log entries, and SignalR events so a single user-facing
/// operation can be traced end-to-end (ASDD §22, FR-PRC-008).
/// </summary>
[JsonConverter(typeof(CorrelationIdJsonConverter))]
public readonly struct CorrelationId : IEquatable<CorrelationId>
{
    private readonly Guid _value;

    private CorrelationId(Guid value) => _value = value;

    /// <summary>Creates a new, random correlation id.</summary>
    public static CorrelationId New() => new(Guid.NewGuid());

    /// <summary>Wraps an existing value (e.g. parsed from a header or message attribute).</summary>
    public static CorrelationId From(Guid value) => new(value);

    /// <summary>Attempts to parse a correlation id from its string form.</summary>
    public static bool TryParse(string? value, out CorrelationId correlationId)
    {
        if (Guid.TryParse(value, out var guid))
        {
            correlationId = new CorrelationId(guid);
            return true;
        }

        correlationId = default;
        return false;
    }

    public bool Equals(CorrelationId other) => _value.Equals(other._value);

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is CorrelationId other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public override string ToString() => _value.ToString();

    public static bool operator ==(CorrelationId left, CorrelationId right) => left.Equals(right);

    public static bool operator !=(CorrelationId left, CorrelationId right) => !left.Equals(right);
}
