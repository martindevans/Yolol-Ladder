using System;
using Newtonsoft.Json;
using Yolol.Execution;
using Type = System.Type;

namespace YololCompetition.Serialization.Json
{
    public class YololValueConverter
        : JsonConverter<Value>
    {
        public override void WriteJson(JsonWriter writer, Value value, JsonSerializer serializer)
        {
            if (value.Type == Yolol.Execution.Type.String)
                writer.WriteValue(value.String.ToString());
            else
                writer.WriteValue((decimal)value.Number);
        }

        public override Value ReadJson(JsonReader reader, Type objectType, Value existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return reader.TokenType switch {
                JsonToken.String => new Value((string)reader.Value!),
                JsonToken.Integer => new Value((long)reader.Value!),
                JsonToken.Float => new Value((decimal)reader.Value!),
                _ => throw new InvalidOperationException($"Unexpected token type `{reader.TokenType}` for Yolol value")
            };
        }

        public override bool CanRead => true;

        public override bool CanWrite => true;
    }
}
