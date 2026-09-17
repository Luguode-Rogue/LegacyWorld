using System.Collections.Generic;
using Newtonsoft.Json;

namespace LegacyWorld.Core.Models
{
    /// <summary>
    /// 玩家人物遗产列表。独立于世界状态（Legacy.json）单独存储于 LegacyHeroes.json，
    /// 采用累积写策略：不同世界继续累积，同一人物按稳定身份刷新。
    /// </summary>
    public class HeroProfileList
    {
        [JsonProperty("version")] public string Version { get; set; } = "0.4.0";
        [JsonProperty("profiles")] public List<HeroProfile> Profiles { get; set; } = new List<HeroProfile>();

        /// <summary>已经应用过的遗产来源世界 WorldId，跨进程防重复导入。</summary>
        [JsonProperty("applied_world_ids")] public List<string> AppliedWorldIds { get; set; } = new List<string>();

        /// <summary>已复刻英雄的持久化快照，供验证 UI 和部分失败重试使用。</summary>
        [JsonProperty("resurrected_heroes")] public List<ResurrectedHeroRecord> ResurrectedHeroes { get; set; } = new List<ResurrectedHeroRecord>();
    }

    /// <summary>单个已复刻英雄的持久化记录。</summary>
    public class ResurrectedHeroRecord
    {
        [JsonProperty("legacy_id")] public string LegacyId { get; set; }
        [JsonProperty("hero_string_id")] public string HeroStringId { get; set; }
        [JsonProperty("target_world_id")] public string TargetWorldId { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("source")] public string Source { get; set; }
        [JsonProperty("world_id")] public string WorldId { get; set; }
        [JsonProperty("level")] public int Level { get; set; }
        [JsonProperty("culture_id")] public string CultureId { get; set; }
        [JsonProperty("restored_at")] public string RestoredAt { get; set; }
    }
}
