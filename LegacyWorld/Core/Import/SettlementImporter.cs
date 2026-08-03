using LegacyWorld.Adapter;
using LegacyWorld.Core;
using LegacyWorld.Core.Models;
using LegacyWorld.Core.Settings;

namespace LegacyWorld.Core.Import
{
    /// <summary>
    /// 定居点所有权恢复。包括拥有者家族、所属王国、繁荣度。
    /// </summary>
    public static class SettlementImporter
    {
        public static int Restore(IGameAdapter adapter, LegacyData data, LegacySettings settings)
        {
            if (!settings.RestoreSettlements) { AffixLogger.Info("SETTLEIMP", "【恢复领地所有权】已关闭，跳过"); return 0; }
            int count = 0;
            foreach (var ss in data.Settlements)
            {
                if (ss == null) continue;
                var settlement = adapter.FindSettlement(ss.Id);
                if (settlement == null) { AffixLogger.Warn("SETTLEIMP", $"找不到定居点 {ss.Id}，跳过"); continue; }

                if (!string.IsNullOrEmpty(ss.OwnerClanId))
                {
                    var newOwner = adapter.FindClan(ss.OwnerClanId);
                    if (newOwner != null) adapter.ChangeSettlementOwner(settlement, newOwner);
                    else AffixLogger.Warn("SETTLEIMP", $"定居点 {ss.Id} 的目标家族 {ss.OwnerClanId} 不存在，跳过所有权变更");
                }

                if (ss.Prosperity > 0) adapter.SetSettlementProsperity(settlement, ss.Prosperity);
                count++;
                AffixLogger.Info("SETTLEIMP", $"恢复定居点 {ss.Name} 归属 → {ss.OwnerClanId}");
            }
            AffixLogger.Info("SETTLEIMP", $"定居点恢复完成: {count}/{data.Settlements.Count}");
            return count;
        }
    }
}
