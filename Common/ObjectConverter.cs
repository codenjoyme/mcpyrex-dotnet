using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpDotnet.Common
{
    /// <summary>
    /// Custom JSON converter to handle JsonElement properly and convert to native .NET objects
    /// </summary>
    public class ObjectConverter : JsonConverter<object>
    {
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                case JsonTokenType.False:
                    return reader.GetBoolean();
                case JsonTokenType.Number:
                    if (reader.TryGetInt32(out int intValue))
                        return intValue;
                    if (reader.TryGetDouble(out double doubleValue))
                        return doubleValue;
                    return reader.GetDecimal();
                case JsonTokenType.String:
                    return reader.GetString()!;
                case JsonTokenType.StartObject:
                    var dict = new Dictionary<string, object>();
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndObject)
                            return dict;
                        if (reader.TokenType != JsonTokenType.PropertyName)
                            throw new JsonException("Expected property name");
                        string propertyName = reader.GetString()!;
                        reader.Read();
                        dict[propertyName] = Read(ref reader, typeof(object), options);
                    }
                    throw new JsonException("Unexpected end of JSON");
                case JsonTokenType.StartArray:
                    var list = new List<object>();
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndArray)
                            return list;
                        list.Add(Read(ref reader, typeof(object), options));
                    }
                    throw new JsonException("Unexpected end of JSON");
                case JsonTokenType.Null:
                    return null!;
                default:
                    throw new JsonException($"Unexpected token type: {reader.TokenType}");
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
