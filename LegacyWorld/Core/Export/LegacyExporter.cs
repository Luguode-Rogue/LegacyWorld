using System;
using System.Collections.Generic;
using LegacyWorld.Adapter;
using LegacyWorld.Core;
using LegacyWorld.Core.Models;
using LegacyWorld.Core.Serialization;
using LegacyWorld.Core.Storage;

namespace LegacyWorld.Core.Export
{
    /// <summary>世界状态与人物遗产导出器。</summary>
    public static class LegacyExporter
    {
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
        /// 累积导出人物遗产。新档案优先使用 LegacyId；旧档案继续用 WorldId + Name + Source 兼容。
        /// 同一英雄之后即使改名，也会用当前快照覆盖原档案。
        /// </summary>
        public static HeroProfileList ExportHeroes(IGameAdapter adapter)
        {
            AffixLogger.Info("EXPORT", "导出玩家人物遗产开始");
            string currentWorldId = adapter.GetWorldId();

            var list = LoadHeroes() ?? new HeroProfileList();
            if (list.Profiles == null) list.Profiles = new List<HeroProfile>();
            if (list.AppliedWorldIds == null) list.AppliedWorldIds = new List<string>();
            if (list.ResurrectedHeroes == null) list.ResurrectedHeroes = new List<ResurrectedHeroRecord>();

            int removedDuplicates = NormalizeHeroProfiles(list.Profiles);
            int added = 0, updated = 0;

            foreach (var hero in adapter.GetHeroProfiles())
            {
                if (hero == null) continue;

                int existingIndex = list.Profiles.FindIndex(p => SameHeroIdentity(p, hero));
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

            // 本轮更新后再归一化一次，可顺便迁移“旧无 LegacyId + 新有 LegacyId”的同名记录。
            removedDuplicates += NormalizeHeroProfiles(list.Profiles);

            AffixLogger.Info("EXPORT", $"玩家人物遗产导出完成: 新增 {added} 个 / 刷新 {updated} 个 / 清理历史重复 {removedDuplicates} 个 / 累计 {list.Profiles.Count} 个（当前世界={currentWorldId}）");
            return list;
        }

        private static int NormalizeHeroProfiles(List<HeroProfile> profiles)
        {
            if (profiles == null || profiles.Count == 0)
                return 0;

            int removed = 0;
            var kept = new List<HeroProfile>();

            // 倒序保留最新记录；SameHeroIdentity 同时兼容新旧身份格式。
            for (int i = profiles.Count - 1; i >= 0; i--)
            {
                HeroProfile profile = profiles[i];
                if (profile == null)
                {
                    profiles.RemoveAt(i);
                    removed++;
                    continue;
                }

                bool duplicate = false;
                foreach (HeroProfile newer in kept)
                {
                    if (SameHeroIdentity(profile, newer))
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                {
                    profiles.RemoveAt(i);
                    removed++;
                }
                else
                {
                    kept.Add(profile);
                }
            }

            return removed;
        }

        private static bool SameHeroIdentity(HeroProfile left, HeroProfile right)
        {
            if (left == null || right == null)
                return false;

            if (!string.IsNullOrWhiteSpace(left.LegacyId) && !string.IsNullOrWhiteSpace(right.LegacyId))
                return string.Equals(left.LegacyId, right.LegacyId, StringComparison.Ordinal);

            // 旧档案兼容：至少有一侧没有 LegacyId 时使用旧键迁移。
            return string.Equals(left.WorldId, right.WorldId, StringComparison.Ordinal)
                && string.Equals(left.Name, right.Name, StringComparison.Ordinal)
                && string.Equals(left.Source, right.Source, StringComparison.Ordinal);
        }

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
