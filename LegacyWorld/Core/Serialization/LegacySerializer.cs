using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using LegacyWorld.Core.Models;

namespace LegacyWorld.Core.Serialization
{
    /// <summary>
    /// JSON 序列化器。使用 snake_case 命名策略，便于手动调试。
    /// </summary>
    public static class LegacySerializer
    {
        private static readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            }
        };

        public static string Serialize(LegacyData data) => JsonConvert.SerializeObject(data, _settings);
        public static LegacyData Deserialize(string json)
            => string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<LegacyData>(json, _settings);
    }
}
