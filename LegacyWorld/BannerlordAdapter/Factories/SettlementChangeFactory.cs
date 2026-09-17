using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using LegacyWorld.Core;

namespace LegacyWorld.BannerlordAdapter.Factories
{
    /// <summary>定居点、家族和王国状态变更工厂。</summary>
    public static class SettlementChangeFactory
    {
        public static void ChangeOwner(Settlement settlement, Clan newOwner)
        {
            AffixLogger.Info("FACTORY", $"ChangeOwner: settlement={settlement?.Name}({settlement?.StringId}), newOwner={newOwner?.Name}({newOwner?.StringId})");
            if (settlement == null || newOwner == null) return;
            try
            {
                if (settlement.Town != null)
                {
                    var before = settlement.Town.OwnerClan;
                    if (before == newOwner)
                    {
                        AffixLogger.Info("FACTORY", $"跳过（归属未变化）: {settlement.Name} 已是 {newOwner.Name}");
                        return;
                    }
                    settlement.Town.OwnerClan = newOwner;
                    AffixLogger.Info("FACTORY", $"变更完成: {settlement.Name} 所有者={settlement.Town.OwnerClan?.Name}");
                    InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] ✓ {settlement.Name} 归属已设为 {newOwner.Name}", Colors.Green));
                    return;
                }
                if (settlement.Village != null)
                {
                    Settlement bound = settlement.Village.Bound;
                    if (bound?.Town != null)
                    {
                        bound.Town.OwnerClan = newOwner;
                        InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] ✓ {settlement.Name} 归属已设为 {newOwner.Name}（通过绑定定居点）", Colors.Green));
                        return;
                    }
                    AffixLogger.Error("FACTORY", $"村庄 {settlement.Name} 的绑定定居点无效或没有 Town 组件");
                }
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
            int clamped = prosperity < 1 ? 1 : prosperity;
            settlement.Town.Prosperity = clamped;
            AffixLogger.Info("FACTORY", $"SetProsperity: {settlement.Name} 繁荣度 -> {clamped}");
        }

        /// <summary>
        /// 当前实际叛军创建由 BannerlordGameAdapter.CreateRebelClanForSettlement 负责。
        /// 本类只保留其需要的原版 RebellionsCampaignBehavior 计时登记，避免维护两套创建逻辑。
        /// </summary>
        internal static void RegisterRebelForAutoPromotion(Clan rebelClan)
        {
            try
            {
                var behavior = Campaign.Current?.GetCampaignBehavior<RebellionsCampaignBehavior>();
                if (behavior == null)
                {
                    AffixLogger.Warn("FACTORY", "无法获取 RebellionsCampaignBehavior，叛军转正登记跳过（将永久卡死）");
                    return;
                }
                var dictField = typeof(RebellionsCampaignBehavior)
                    .GetField("_rebelClansAndDaysPassedAfterCreation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (dictField == null)
                {
                    AffixLogger.Warn("FACTORY", "未找到 _rebelClansAndDaysPassedAfterCreation 字段（版本差异），叛军转正登记跳过");
                    return;
                }
                var dict = dictField.GetValue(behavior) as System.Collections.IDictionary;
                if (dict == null)
                {
                    AffixLogger.Warn("FACTORY", "叛军计时字典为空，无法登记");
                    return;
                }
                if (!dict.Contains(rebelClan))
                {
                    dict[rebelClan] = 1;
                    AffixLogger.Info("FACTORY", $"已将叛军 {rebelClan.Name} 登记进自动转正计时字典");
                }
            }
            catch (Exception ex)
            {
                AffixLogger.Warn("FACTORY", $"叛军转正登记失败（反射）: {ex.Message}");
            }
        }

        public static void SetClanGold(Clan clan, int gold)
        {
            if (clan == null) return;
            if (clan.Leader == null)
            {
                AffixLogger.Warn("FACTORY", $"SetClanGold 跳过：家族 {clan.Name} 的 Leader 为空");
                return;
            }
            try
            {
                int diff = gold - clan.Leader.Gold;
                clan.Leader.ChangeHeroGold(diff);
            }
            catch (Exception ex)
            {
                AffixLogger.Error("FACTORY", $"设置家族 {clan.Name} 金币失败", ex);
            }
        }

        public static void SetClanRenown(Clan clan, float renown)
        {
            if (clan == null) return;
            float before = clan.Renown;
            clan.Renown = renown;
            AffixLogger.Info("FACTORY", $"SetClanRenown: {clan.Name} 声望 {before:F1} -> {clan.Renown:F1} (目标 {renown:F1})");
        }

        public static void SetClanInfluence(Clan clan, float influence)
        {
            if (clan == null) return;
            clan.Influence = influence;
        }

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
