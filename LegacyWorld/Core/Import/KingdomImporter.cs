using LegacyWorld.Adapter;
using LegacyWorld.Core;
using LegacyWorld.Core.Models;
using LegacyWorld.Core.Settings;

namespace LegacyWorld.Core.Import
{
    /// <summary>
    /// 王国状态恢复。设置王国统治者家族引用。
    /// </summary>
    public static class KingdomImporter
    {
        public static int Restore(IGameAdapter adapter, LegacyData data, LegacySettings settings)
        {
            if (!settings.RestoreKingdoms) { AffixLogger.Info("KINGDOMIMP", "【恢复王国结构】已关闭，跳过"); return 0; }
            int count = 0;
            foreach (var ks in data.Kingdoms)
            {
                if (ks == null) continue;
                var kingdom = adapter.FindKingdom(ks.Id);
                if (kingdom == null) { AffixLogger.Warn("KINGDOMIMP", $"找不到王国 {ks.Id}（可能已解体或地图 Mod 改动），跳过"); continue; }
                // 边界：目标王国已解体（Eliminated），恢复一个死王国会扰乱外交/地图，跳过。
                if (kingdom.IsDestroyed) { AffixLogger.Warn("KINGDOMIMP", $"王国 {ks.Id} 已解体，跳过恢复"); continue; }

                var ruler = adapter.FindClan(ks.RulerClanId);
                if (ruler == null)
                {
                    // 边界：统治者家族不存在（旧档玩家家族/自定义家族在新档无对应）。
                    // 不再整体跳过——仅跳过统治者设置，保留王国本身，避免连带丢失王国结构。
                    AffixLogger.Warn("KINGDOMIMP", $"王国 {ks.Name} 的统治者 {ks.RulerClanId} 不存在（CreateMissingClans 未开启或 CreateClan 未实现），跳过统治者设置但保留王国");
                    count++;
                    continue;
                }
                adapter.SetKingdomRuler(kingdom, ruler);
                count++;
                AffixLogger.Info("KINGDOMIMP", $"恢复王国 {ks.Name} 统治者 → {ruler.Name}");
            }
            AffixLogger.Info("KINGDOMIMP", $"王国恢复完成: {count}/{data.Kingdoms.Count}");
            return count;
        }
    }
}
