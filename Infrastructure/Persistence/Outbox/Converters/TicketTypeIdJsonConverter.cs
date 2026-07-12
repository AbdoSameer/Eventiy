using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Aggregates.EventAggregate.ValueObject;

namespace Infrastructure.Persistence.Outbox.Converters;

public sealed class TicketTypeIdJsonConverter : JsonConverter<TicketTypeId>
{
    public override TicketTypeId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && Guid.TryParse(reader.GetString(), out var guid))
            return TicketTypeId.FromDatabase(guid);

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            if (doc.RootElement.TryGetProperty("value", out var prop) && prop.ValueKind == JsonValueKind.String)
                return TicketTypeId.FromDatabase(Guid.Parse(prop.GetString()!));
        }

        throw new JsonException($"Cannot deserialize TicketTypeId from token {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, TicketTypeId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
