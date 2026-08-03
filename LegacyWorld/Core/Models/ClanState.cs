using Newtonsoft.Json;

namespace LegacyWorld.Core.Models
{
    public class ClanState
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("kingdom_id")] public string KingdomId { get; set; }
        [JsonProperty("tier")] public int Tier { get; set; }
        [JsonProperty("gold")] public long Gold { get; set; }
        [JsonProperty("renown")] public float Renown { get; set; }
        [JsonProperty("influence")] public float Influence { get; set; }
        [JsonProperty("is_destroyed")] public bool IsDestroyed { get; set; }
        [JsonProperty("culture")] public string Culture { get; set; }
    }
}
