using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace InfiniteTool.GameInterop
{
    public class Tags
    {
        public List<TagInfo> Weapons { get; set; }
        public List<TagInfo> WeaponConfigs { get; set; }

        public static Tags LoadWeaponTags()
        {
            return JsonSerializer.Deserialize<Tags>(File.ReadAllText("Data/weapons.json"))!;
        }
    }

    public record TagInfo(string Type, string Name, uint Id);
}
