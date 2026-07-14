using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Common;

namespace Infrastructure.Persistence.Outbox.Converters;

public class ValueObjectJsonConverter<T> : JsonConverter<T> where T : ValueObjectBase
{
    private static readonly Func<Guid, T> FromDatabase;
    private static readonly Func<T, Guid> GetValue;

    static ValueObjectJsonConverter()
    {
        var fromDbMethod = typeof(T).GetMethod("FromDatabase", BindingFlags.Static | BindingFlags.Public, [typeof(Guid)])
            ?? throw new InvalidOperationException($"Type {typeof(T).Name} must have a public static FromDatabase(Guid) method.");
        FromDatabase = guid => (T)fromDbMethod.Invoke(null, [guid])!;

        var valueProp = typeof(T).GetProperty("Value", typeof(Guid))
            ?? throw new InvalidOperationException($"Type {typeof(T).Name} must have a public instance Value property of type Guid.");
        GetValue = obj => (Guid)valueProp.GetValue(obj)!;
    }

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && Guid.TryParse(reader.GetString(), out var guid))
            return FromDatabase(guid);

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            if (doc.RootElement.TryGetProperty("value", out var prop) && prop.ValueKind == JsonValueKind.String)
                return FromDatabase(Guid.Parse(prop.GetString()!));
        }

        throw new JsonException($"Cannot deserialize {typeof(T).Name} from token {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(GetValue(value));
    }
}
