using System;
using System.Collections.Generic;
using System.Linq;
using LegacyWorld.Adapter;
using LegacyWorld.Core;
using LegacyWorld.Core.Models;
using LegacyWorld.Core.Serialization;
using LegacyWorld.Core.Storage;

namespace LegacyWorld.Core.Export
{
    /// <summary>
    /// 世界状态导出器。遍历所有 Kingdom / Clan / Settlement 并组装为 LegacyData。
    /// 业务逻辑层，不依赖 TaleWorlds.*。
    /// </summary>
    public static class LegacyExporter
    {
        /// <summary>
        /// 导出世界状态（王国/家族/定居点）到 Legacy.json，覆盖写（仅反映当前存档最新状态）。
        /// 玩家人物遗产由 <see cref="ExportHeroes"/> 单独累积写入 LegacyHeroes.json。
        /// </summary>
        public static LegacyData ExportWorld(IGameAdapter adapter)
        {
            AffixLogger.Info("EXPORT", "导出世界状态开始");
            var legacyData = new LegacyData
            {
                Version = "0.4.0",
                WorldId = adapter.GetWorldId(),
                CreatedAt = adapter.GetCurrentGameTime(),
                GameVersion = adapter.GetGameVersion(),
                DominantCulture = adapter.GetDominantCulture(),
                Kingdoms = new List<KingdomState>(),
                Clans = new List<ClanState>(),
                Settlements = new List<SettlementState>()
            };

            foreach (var kingdom in adapter.GetAllKingdoms())
            {
                if (kingdom == null) continue;
                legacyData.Kingdoms.Add(new KingdomState
                {
                    Id = kingdom.Id,
                    Name = kingdom.Name,
                    RulerClanId = kingdom.RulerClan?.Id,
                    Culture = kingdom.Culture
                });
            }

            foreach (var clan in adapter.GetAllClans())
            {
                if (clan == null) continue;
                legacyData.Clans.Add(new ClanState
                {
                    Id = clan.Id,
                    Name = clan.Name,
                    KingdomId = clan.Kingdom?.Id,
                    Tier = clan.Tier,
                    Gold = clan.Gold,
                    Renown = clan.Renown,
                    Influence = clan.Influence,
                    IsDestroyed = clan.IsDestroyed
                });
            }

            foreach (var settlement in adapter.GetAllSettlements())
            {
                if (settlement == null) continue;
                legacyData.Settlements.Add(new SettlementState
                {
                    Id = settlement.Id,
                    Name = settlement.Name,
                    Type = settlement.Type,
                    OwnerClanId = settlement.OwnerClan?.Id,
                    OwnerKingdomId = settlement.OwnerKingdom?.Id,
                    Culture = settlement.Culture,
                    Prosperity = settlement.Prosperity
                });
            }

            AffixLogger.Info("EXPORT", $"世界状态导出完成: {legacyData.Kingdoms.Count} 王国, {legacyData.Clans.Count} 家族, {legacyData.Settlements.Count} 定居点");
            return legacyData;
        }

        /// <summary>
        /// 导出当前存档的玩家人物（player/companion/wanderer）到 LegacyHeroes.json，累积写。
        /// 不同世界的模板继续累积保留；同一世界同一人物则用当前最新快照替换旧快照，
        /// 这样等级、技能以及玩家装备变化会在后续导出时得到刷新，同时不会产生重复记录。
        /// 例：B 世界导出会保留 A 的遗留；C 世界导入时可同时拿到 A 与 B 的遗留玩家人物。
        /// </summary>
        public static HeroProfileList ExportHeroes(IGameAdapter adapter)
        {
            AffixLogger.Info("EXPORT", "导出玩家人物遗产开始");
            string currentWorldId = adapter.GetWorldId();

            // 基于已加载的列表更新（保留已持久化的 AppliedWorldIds / ResurrectedHeroes），
            // 而不是每次新建；同一来源人物使用当前快照替换，避免装备/属性永远停留在首次导出状态。
            var list = LoadHeroes() ?? new HeroProfileList();
            if (list.Profiles == null) list.Profiles = new List<HeroProfile>();
            if (list.AppliedWorldIds == null) list.AppliedWorldIds = new List<string>();
            if (list.ResurrectedHeroes == null) list.ResurrectedHeroes = new List<ResurrectedHeroRecord>();

            int added = 0, updated = 0;
            foreach (var hero in adapter.GetHeroProfiles())
            {
                if (hero == null) continue;

                int existingIndex = list.Profiles.FindIndex(p =>
                    p != null &&
                    p.WorldId == hero.WorldId &&
                    p.Name == hero.Name &&
                    p.Source == hero.Source);

                if (existingIndex >= 0)
                {
                    list.Profiles[existingIndex] = hero;
                    updated++;
                }
                else
                {
                    list.Profiles.Add(hero);
                    added++;
                }
            }

            AffixLogger.Info("EXPORT", $"玩家人物遗产导出完成: 新增 {added} 个 / 刷新 {updated} 个 / 累计 {list.Profiles.Count} 个（当前世界={currentWorldId}）");
            return list;
        }

        /// <summary>
        /// 读取已累积的玩家人物遗产文件。文件不存在或损坏返回 null。
        /// </summary>
        private static HeroProfileList LoadHeroes()
        {
            try
            {
                string json = LegacyStorage.ReadHeroes();
                if (string.IsNullOrWhiteSpace(json)) return null;
                return LegacySerializer.DeserializeHeroes(json);
            }
            catch (Exception ex)
            {
                AffixLogger.Warn("EXPORT", "读取旧玩家人物遗产失败，将重新累积" + (ex != null ? " | " + ex : ""));
                return null;
            }
        }
    }
}
