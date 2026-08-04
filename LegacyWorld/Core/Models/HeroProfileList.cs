using System.Collections.Generic;
using Newtonsoft.Json;

namespace LegacyWorld.Core.Models
{
    /// <summary>
    /// 玩家人物遗产列表。独立于世界状态（Legacy.json）单独存储于 LegacyHeroes.json，
    /// 采用累积写策略：每个世界导出的玩家/队友/游荡英雄模板都追加进去（按 WorldId+Name+Source 去重），
    /// 从而形成跨越多个世界的遗产链（A->B->C 时，C 能同时拿到 A 与 B 的遗留人物）。
    /// </summary>
    public class HeroProfileList
    {
        [JsonProperty("version")] public string Version { get; set; } = "0.4.0";
        [JsonProperty("profiles")] public List<HeroProfile> Profiles { get; set; } = new List<HeroProfile>();

        /// <summary>
        /// 已复刻（导入）过的遗产来源世界 WorldId 列表，持久化到文件。
        /// 跨进程生效：重开游戏后再次点导入时，已导入过的世界不再重复复刻，
        /// 从而避免反复添加重复的遗留玩家 NPC。
        /// </summary>
        [JsonProperty("applied_world_ids")] public List<string> AppliedWorldIds { get; set; } = new List<string>();
    }
}
