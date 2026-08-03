using System.Collections.Generic;
using Newtonsoft.Json;

namespace LegacyWorld.Core.Models
{
    public class LegacyData
    {
        [JsonProperty("version")] public string Version { get; set; }
        [JsonProperty("world_id")] public string WorldId { get; set; }
        [JsonProperty("created_at")] public string CreatedAt { get; set; }
        [JsonProperty("game_version")] public string GameVersion { get; set; }
        [JsonProperty("culture")] public string DominantCulture { get; set; }
        [JsonProperty("kingdoms")] public List<KingdomState> Kingdoms { get; set; }
        [JsonProperty("clans")] public List<ClanState> Clans { get; set; }
        [JsonProperty("settlements")] public List<SettlementState> Settlements { get; set; }
        [JsonProperty("hero_profiles")] public List<HeroProfile> HeroProfiles { get; set; } = new List<HeroProfile>();
    }
}
