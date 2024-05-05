using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace InfiniteTool.GameInterop
{
    public class Tags
    {
        public List<TagInfo> Weapons { get; set; }
        public List<TagInfo> WeaponConfigs { get; set; }
        public List<TagInfo> Vehicles { get; set; }
        public List<TagInfo> Characters { get; set; }
        public List<TagInfo> Bipeds { get; set; }

        public static Tags LoadTags()
        {
            return JsonSerializer.Deserialize<Tags>(File.ReadAllText("Data/tags.json"), SourceGenerationContext.Default.Tags)!;
        }
    }

    public record TagInfo(string Type, string Name, uint Id)
    {
        public override string ToString()
        {
            return Name;
        }
    }
}
