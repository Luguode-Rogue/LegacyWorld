using LegacyWorld.Adapter;
using LegacyWorld.Core;
using LegacyWorld.Core.Models;
using LegacyWorld.Core.Settings;

namespace LegacyWorld.Core.Import
{
    /// <summary>
    /// 家族状态恢复。包括所属王国、等级、金币、声望、影响力。
    /// </summary>
    public static class ClanImporter
    {
        public static int Restore(IGameAdapter adapter, LegacyData data, LegacySettings settings)
        {
            if (!settings.RestoreClans) { AffixLogger.Info("CLANIMP", "【恢复家族数据】已关闭，跳过"); return 0; }
            int count = 0;
            foreach (var cs in data.Clans)
            {
                if (cs == null) continue;
                var clan = adapter.FindClan(cs.Id);
                if (clan == null)
                {
                    if (settings.CreateMissingClans)
                    {
                        clan = adapter.CreateClan(cs.Id, cs.Name, cs.Culture);
                        if (clan == null) { AffixLogger.Warn("CLANIMP", $"无法创建家族 {cs.Id}（CreateClan 未实现），跳过"); continue; }
                        AffixLogger.Info("CLANIMP", $"创建缺失家族 {cs.Name}");
                    }
                    else { AffixLogger.Warn("CLANIMP", $"找不到家族 {cs.Id}，跳过（CreateMissingClans=false）"); continue; }
                }

                if (!string.IsNullOrEmpty(cs.KingdomId))
                {
                    var kingdom = adapter.FindKingdom(cs.KingdomId);
                    if (kingdom != null) adapter.SetClanKingdom(clan, kingdom);
                    else AffixLogger.Warn("CLANIMP", $"家族 {cs.Id} 的所属王国 {cs.KingdomId} 不存在");
                }

                if (settings.RestoreClanEconomy)
                {
                    adapter.SetClanGold(clan, cs.Gold);
                    adapter.SetClanRenown(clan, cs.Renown);
                    adapter.SetClanInfluence(clan, cs.Influence);
                }

                count++;
                AffixLogger.Info("CLANIMP", $"恢复家族 {cs.Name} (T{cs.Tier}, 金币 {cs.Gold})");
            }
            AffixLogger.Info("CLANIMP", $"家族恢复完成: {count}/{data.Clans.Count}");
            return count;
        }
    }
}
