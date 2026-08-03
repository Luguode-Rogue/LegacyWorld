using System.Collections.Generic;
using LegacyWorld.Adapter;
using LegacyWorld.Core;
using LegacyWorld.Core.Models;

namespace LegacyWorld.Core.Export
{
    /// <summary>
    /// 世界状态导出器。遍历所有 Kingdom / Clan / Settlement 并组装为 LegacyData。
    /// 业务逻辑层，不依赖 TaleWorlds.*。
    /// </summary>
    public static class LegacyExporter
    {
        public static LegacyData Export(IGameAdapter adapter)
        {
            AffixLogger.Info("EXPORT", "导出开始");
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

            foreach (var hero in adapter.GetHeroProfiles())
            {
                if (hero == null) continue;
                legacyData.HeroProfiles.Add(hero);
            }

            AffixLogger.Info("EXPORT", $"导出完成: {legacyData.Kingdoms.Count} 王国, {legacyData.Clans.Count} 家族, {legacyData.Settlements.Count} 定居点, {legacyData.HeroProfiles.Count} 英雄模板");
            return legacyData;
        }
    }
}
