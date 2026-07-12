using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Aggregates.BookingAggregate.ValueObject;

namespace Infrastructure.Persistence.Outbox.Converters;

public sealed class BookingIdJsonConverter : JsonConverter<BookingId>
{
    public override BookingId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && Guid.TryParse(reader.GetString(), out var guid))
            return BookingId.FromDatabase(guid);

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            if (doc.RootElement.TryGetProperty("value", out var prop) && prop.ValueKind == JsonValueKind.String)
                return BookingId.FromDatabase(Guid.Parse(prop.GetString()!));
        }

        throw new JsonException($"Cannot deserialize BookingId from token {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, BookingId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
