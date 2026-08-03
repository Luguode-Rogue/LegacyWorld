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
                if (kingdom == null) { AffixLogger.Warn("KINGDOMIMP", $"找不到王国 {ks.Id}，跳过"); continue; }
                var ruler = adapter.FindClan(ks.RulerClanId);
                if (ruler == null) { AffixLogger.Warn("KINGDOMIMP", $"王国 {ks.Id} 的统治者 {ks.RulerClanId} 不存在，跳过"); continue; }
                adapter.SetKingdomRuler(kingdom, ruler);
                count++;
                AffixLogger.Info("KINGDOMIMP", $"恢复王国 {ks.Name} 统治者 → {ruler.Name}");
            }
            AffixLogger.Info("KINGDOMIMP", $"王国恢复完成: {count}/{data.Kingdoms.Count}");
            return count;
        }
    }
}
