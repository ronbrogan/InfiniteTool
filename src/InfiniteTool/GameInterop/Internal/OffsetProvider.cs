using Superintendent.Core.Serialization;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace InfiniteTool.GameInterop.Internal
{
    public interface IOffsetProvider
    {
        InfiniteOffsets GetOffsets(string version);
    }

    public class JsonOffsetProvider : IOffsetProvider
    {
        private JsonSerializerOptions jsonOptions;

        public JsonOffsetProvider()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new NintConverter());
            options.WriteIndented = true;
            options.ReadCommentHandling = JsonCommentHandling.Skip;

            SerializationOptions.AddJsonConverters(options);

            jsonOptions = options;
        }


        public InfiniteOffsets GetOffsets(string version)
        {
            var file = Path.Combine(Environment.CurrentDirectory, "Data", version, "offsets.json");

            try
            {
                var json = File.ReadAllText(file);
                return JsonSerializer.Deserialize<InfiniteOffsets>(json, jsonOptions) ?? InfiniteOffsets.Unknown;
            }
            catch (Exception ex)
            {
                return InfiniteOffsets.Unknown;
            }
        }

        public class NintConverter : JsonConverter<nint>
        {
            public override nint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var stringVal = reader.GetString();
                    if (stringVal.StartsWith("0x"))
                    {
                        var address = Convert.ToInt64(stringVal[2..], 16);
                        return (nint)address;
                    }
                    else if (long.TryParse(stringVal, out var address))
                    {
                        return (nint)address;
                    }
                }
                else if (reader.TokenType == JsonTokenType.Number)
                {
                    return (nint)reader.GetInt64();
                }

                throw new JsonException($"Unable to decode native integer from {reader.GetString()}");
            }

            public override void Write(Utf8JsonWriter writer, nint value, JsonSerializerOptions options)
            {
                writer.WriteStringValue("0x" + Convert.ToString(value, 16));
            }
        }
    }
}