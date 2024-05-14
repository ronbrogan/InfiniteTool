using InfiniteTool.Credentials;
using InfiniteTool.GameInterop;
using Superintendent.Core.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace InfiniteTool.Formats
{
    [JsonSourceGenerationOptions(WriteIndented = true, Converters = [typeof(GameInterop.Internal.JsonOffsetProvider.NintConverter), typeof(HexStringNintConverter), typeof(HexStringFuncConverter), typeof(HexStringPointerConverter)], ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(InfiniteOffsets))]
    [JsonSerializable(typeof(Tags))]
    [JsonSerializable(typeof(Credentials.Credential))]
    [JsonSerializable(typeof(List<Credentials.Credential>))]
    public partial class Json : JsonSerializerContext
    {

        public static Tags DeserializeTags(Stream json)
        {
            return JsonSerializer.Deserialize(json, Default.Tags);
        }

        public static InfiniteOffsets DeserializeOffsets(Stream json)
        {
            return JsonSerializer.Deserialize(json, Default.InfiniteOffsets);
        }

        public static List<Credential> DeserializeCredentials(Stream json)
        {
            return JsonSerializer.Deserialize(json, Default.ListCredential);
        }

        public static string Serialize(List<Credential> cred)
        {
            return JsonSerializer.Serialize(cred, Default.ListCredential);
        }
    }
}
