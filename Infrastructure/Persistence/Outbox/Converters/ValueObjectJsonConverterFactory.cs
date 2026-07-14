using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Common;

namespace Infrastructure.Persistence.Outbox.Converters;

public class ValueObjectJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsSubclassOf(typeof(ValueObjectBase)) || typeToConvert.IsAbstract)
            return false;

        var valueProp = typeToConvert.GetProperty("Value", typeof(Guid));
        if (valueProp is null || !valueProp.CanRead)
            return false;

        var fromDbMethod = typeToConvert.GetMethod("FromDatabase", BindingFlags.Static | BindingFlags.Public, [typeof(Guid)]);
        if (fromDbMethod is null || fromDbMethod.ReturnType != typeToConvert)
            return false;

        return true;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(ValueObjectJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}
