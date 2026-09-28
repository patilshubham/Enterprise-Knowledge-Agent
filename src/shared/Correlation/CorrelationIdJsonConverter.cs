using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnterpriseKnowledgeAgent.Shared.Correlation;

/// <summary>
/// Serializes a <see cref="CorrelationId"/> as its plain GUID string form so
/// it round-trips cleanly through queue message payloads, API responses, and
/// SignalR events.
/// </summary>
public sealed class CorrelationIdJsonConverter : JsonConverter<CorrelationId>
{
    public override CorrelationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (!CorrelationId.TryParse(value, out var correlationId))
        {
            throw new JsonException($"'{value}' is not a valid correlation id.");
        }

        return correlationId;
    }

    public override void Write(Utf8JsonWriter writer, CorrelationId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
