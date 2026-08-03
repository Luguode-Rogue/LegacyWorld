using Newtonsoft.Json;

namespace LegacyWorld.Core.Models
{
    public class SettlementState
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("owner_clan_id")] public string OwnerClanId { get; set; }
        [JsonProperty("owner_kingdom_id")] public string OwnerKingdomId { get; set; }
        [JsonProperty("culture")] public string Culture { get; set; }
        [JsonProperty("prosperity")] public float Prosperity { get; set; }
    }
}
