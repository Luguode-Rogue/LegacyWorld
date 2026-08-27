using LegacyWorld.Adapter;
using LegacyWorld.Core;
using LegacyWorld.Core.Models;
using LegacyWorld.Core.Settings;
using TaleWorlds.CampaignSystem;

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
                if (settlement == null) { AffixLogger.Warn("SETTLEIMP", $"找不到定居点 {ss.Id}（可能地图 Mod 改动或 id 不符），跳过"); continue; }

                // 边界：村庄没有城防组件，所有权由母城镇决定，跳过（由母城镇逻辑统一处理）。
                if (settlement.Type == "Village")
                {
                    AffixLogger.Info("SETTLEIMP", $"跳过村庄 {ss.Name}（所有权跟随母城镇）");
                    continue;
                }

                if (!string.IsNullOrEmpty(ss.OwnerClanId))
                {
                    var newOwner = adapter.FindClan(ss.OwnerClanId);

                    // 边界：旧档玩家家族的城市/城堡。新档里当前玩家家族的 StringId 通常与旧档一致
                    // （例如都是 "player_faction"），导致 FindClan 命中“新档当前玩家家族”，
                    // 从而把旧档玩家的领土白送给新玩家。需求：旧档玩家城市/城堡应直接变为叛军，
                    // 而非归还新玩家。故当目标家族就是当前玩家家族时，也兜底变为叛军。
                    bool isOwnerCurrentPlayerClan = newOwner != null
                                                    && Clan.PlayerClan != null
                                                    && newOwner.Id == Clan.PlayerClan.StringId;

                    if (newOwner == null || isOwnerCurrentPlayerClan)
                    {
                        // 边界：目标家族在新档不存在（最常见是旧档玩家家族 StringId 不同），
                        // 或目标家族即新档当前玩家家族（旧档玩家领土）→ 不让玩家白捡、也不凭空消失，
                        // 直接变为叛军所有（游戏原生 CreateSettlementRebelClan 机制，约 30 天后自动转正）。
                        AffixLogger.Warn("SETTLEIMP", isOwnerCurrentPlayerClan
                            ? $"定居点 {ss.Name} 属旧档玩家家族（命中新档玩家 clan {ss.OwnerClanId}）→ 兜底变为叛军所有"
                            : $"定居点 {ss.Name} 的目标家族 {ss.OwnerClanId} 在新档不存在 → 兜底变为叛军所有");
                        adapter.MakeSettlementRebel(settlement);
                        count++;
                        continue;
                    }

                    // 边界：归属未变化（同一家族已是城主）时，Town.OwnerClan setter 会跳过 ChangeClanInternal，
                    // 这里先比对实际归属，避免日志误报“恢复成功”而实际无操作。
                    var currentOwnerId = settlement.OwnerClan?.Id;
                    if (currentOwnerId == newOwner.Id)
                    {
                        AffixLogger.Info("SETTLEIMP", $"定居点 {ss.Name} 已归属 {newOwner.Name}，无需变更");
                        count++;
                        continue;
                    }

                    adapter.ChangeSettlementOwner(settlement, newOwner);
                    AffixLogger.Info("SETTLEIMP", $"恢复定居点 {ss.Name} 归属 → {newOwner.Name}");
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
