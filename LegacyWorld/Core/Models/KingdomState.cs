using Newtonsoft.Json;

namespace LegacyWorld.Core.Models
{
    public class KingdomState
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("ruler_clan_id")] public string RulerClanId { get; set; }
        [JsonProperty("culture")] public string Culture { get; set; }
    }
}
