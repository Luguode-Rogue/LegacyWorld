using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using LegacyWorld.Core;

namespace LegacyWorld.BannerlordAdapter.Factories
{
    /// <summary>
    /// 定居点所有权变更工厂。
    /// 通过游戏官方 API（Town.OwnerClan setter）变更所有权，确保完整的
    /// OnFortificationAdded/Removed 通知链和地图视觉更新。
    /// </summary>
    public static class SettlementChangeFactory
    {
        public static void ChangeOwner(Settlement settlement, Clan newOwner)
        {
            AffixLogger.Info("FACTORY", $"ChangeOwner: settlement={settlement?.Name}({settlement?.StringId}), newOwner={newOwner?.Name}({newOwner?.StringId})");
            if (settlement == null || newOwner == null)
            {
                AffixLogger.Info("FACTORY", $"ChangeOwner 跳过: settlement==null={settlement == null}, newOwner==null={newOwner == null}");
                return;
            }
            try
            {
                if (settlement.Town != null)
                {
                    var before = settlement.Town.OwnerClan;
                    AffixLogger.Info("FACTORY", $"变更前(Town.OwnerClan): {settlement.Name} 所有者={before?.Name}({before?.StringId})");
                    settlement.Town.OwnerClan = newOwner;
                    var after = settlement.Town.OwnerClan;
                    AffixLogger.Info("FACTORY", $"变更完成: {settlement.Name} 所有者={after?.Name}({after?.StringId}), 期望={newOwner.Name}({newOwner.StringId})");
                    InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] ✓ {settlement.Name} 归属已设为 {newOwner.Name}", Colors.Green));
                    return;
                }
                if (settlement.Village != null)
                {
                    Settlement bound = settlement.Village.Bound;
                    if (bound?.Town != null)
                    {
                        AffixLogger.Info("FACTORY", $"{settlement.Name} 是村庄，通过绑定定居点 {bound.Name} 变更");
                        var before = bound.Town.OwnerClan;
                        AffixLogger.Info("FACTORY", $"变更前(绑定Town): {bound.Name} 所有者={before?.Name}({before?.StringId})");
                        bound.Town.OwnerClan = newOwner;
                        var after = settlement.OwnerClan;
                        AffixLogger.Info("FACTORY", $"变更完成(村庄跟随): {settlement.Name} 所有者={after?.Name}({after?.StringId})");
                        InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] ✓ {settlement.Name} 归属已设为 {newOwner.Name}（通过绑定定居点）", Colors.Green));
                        return;
                    }
                    AffixLogger.Error("FACTORY", $"村庄 {settlement.Name} 的绑定定居点无效或没有 Town 组件");
                }
                AffixLogger.Info("FACTORY", $"无法处理的定居点类型: {settlement.Name}, IsTown={settlement.IsTown}, IsCastle={settlement.IsCastle}, IsVillage={settlement.IsVillage}, IsHideout={settlement.IsHideout}");
                InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] ⚠ {settlement.Name} 无法变更（未知类型）", Colors.Yellow));
            }
            catch (Exception ex)
            {
                AffixLogger.Error("FACTORY", $"变更 {settlement.Name} 所有权失败", ex);
                InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] ✗ {settlement.Name} 所有权变更失败: {ex.Message}", Colors.Red));
            }
        }

        public static void SetProsperity(Settlement settlement, int prosperity)
        {
            if (settlement?.Town == null) return;
            settlement.Town.Prosperity = prosperity;
        }

        public static void SetClanGold(Clan clan, int gold)
        {
            if (clan?.Leader == null) return;
            int diff = gold - clan.Leader.Gold;
            clan.Leader.ChangeHeroGold(diff);
        }

        public static void SetClanRenown(Clan clan, float renown)
        {
            if (clan == null) return;
            float before = clan.Renown;
            // 必须用直接赋值：BT 的 Clan.AddRenown 对负值增量会被钳制（声望只增不减），
            // 当遗产声望低于当前值时无法下降。直接赋值可同时支持升/降。
            clan.Renown = renown;
            AffixLogger.Info("FACTORY", $"SetClanRenown: {clan.Name} 声望 {before:F1} -> {clan.Renown:F1} (目标 {renown:F1})");
        }

        public static void SetClanInfluence(Clan clan, float influence) { if (clan != null) clan.Influence = influence; }

        public static void SetKingdomRuler(Kingdom kingdom, Clan rulerClan)
        {
            if (kingdom == null || rulerClan == null) return;
            kingdom.RulingClan = rulerClan;
        }

        public static void SetClanKingdom(Clan clan, Kingdom kingdom)
        {
            if (clan == null || kingdom == null) return;
            clan.Kingdom = kingdom;
        }
    }
}
