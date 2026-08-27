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
                        // 边界：CreateClan 在适配层尚未实现（返回 null）。明确提示并跳过，
                        // 避免后续对 null clan 调用 SetClanGold 等导致 NRE。
                        if (clan == null) { AffixLogger.Warn("CLANIMP", $"无法创建家族 {cs.Id}（CreateClan 未实现），跳过"); continue; }
                        AffixLogger.Info("CLANIMP", $"创建缺失家族 {cs.Name}");
                    }
                    else { AffixLogger.Warn("CLANIMP", $"找不到家族 {cs.Id}，跳过（CreateMissingClans=false）"); continue; }
                }

                // 边界：家族已解体（Eliminated），仅恢复其经济/归属引用意义不大且可能异常，跳过。
                if (clan.IsDestroyed) { AffixLogger.Warn("CLANIMP", $"家族 {cs.Name} 已解体，跳过恢复"); continue; }

                if (!string.IsNullOrEmpty(cs.KingdomId))
                {
                    var kingdom = adapter.FindKingdom(cs.KingdomId);
                    if (kingdom != null) adapter.SetClanKingdom(clan, kingdom);
                    else AffixLogger.Warn("CLANIMP", $"家族 {cs.Id} 的所属王国 {cs.KingdomId} 不存在，跳过王国归属");
                }

                if (settings.RestoreClanEconomy)
                {
                    // SetClanGold/Renown/Influence 在适配层已做 Leader 空保护与 try/catch，
                    // 这里直接调用；负 Renown/Influence 由工厂直接赋值兜底（引擎 AddXxx 会钳制负值）。
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
