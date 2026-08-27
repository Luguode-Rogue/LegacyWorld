using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
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
                    // 边界：归属未变化则跳过（Town.OwnerClan setter 也会跳过 ChangeClanInternal，
                    // 这里提前 return 避免无谓的日志/通知）。
                    if (before == newOwner)
                    {
                        AffixLogger.Info("FACTORY", $"跳过（归属未变化）: {settlement.Name} 已是 {newOwner.Name}");
                        return;
                    }
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
            // 边界：繁荣度有合法下限（防止负数/0 导致经济异常），钳制到 >= 1。
            int clamped = prosperity < 1 ? 1 : prosperity;
            settlement.Town.Prosperity = clamped;
            AffixLogger.Info("FACTORY", $"SetProsperity: {settlement.Name} 繁荣度 -> {clamped}");
        }

        /// <summary>
        /// 将定居点变为叛军所有（游戏原生 CreateSettlementRebelClan 机制）。
        /// 叛军家族 IsRebelClan=true，游戏内 RebellionsCampaignBehavior 会在约 30 天后自动转正。
        /// 用于“旧档玩家家族在新档不存在”的兜底：让城池归属叛军，而非白给玩家或无主消失。
        /// 村庄通过绑定城镇处理。
        /// </summary>
        public static void MakeRebel(Settlement settlement)
        {
            if (settlement == null) return;
            try
            {
                // 村庄：对其绑定城镇执行叛军化（村庄归属随母城镇）。
                if (settlement.Village != null)
                {
                    var bound = settlement.Village.Bound;
                    if (bound?.Town != null)
                    {
                        AffixLogger.Info("FACTORY", $"村庄 {settlement.Name} 通过绑定城镇 {bound.Name} 叛军化");
                        MakeRebelInternal(bound);
                    }
                    else
                    {
                        AffixLogger.Warn("FACTORY", $"村庄 {settlement.Name} 绑定城镇无效，无法叛军化");
                    }
                    return;
                }
                MakeRebelInternal(settlement);
            }
            catch (Exception ex)
            {
                AffixLogger.Error("FACTORY", $"将 {settlement?.Name} 变为叛军失败", ex);
                InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] ✗ {settlement?.Name} 叛军化失败: {ex.Message}", Colors.Red));
            }
        }

        private static void MakeRebelInternal(Settlement settlement)
        {
            if (settlement?.Town == null) return;
            // 边界：已经是叛军家族所有，无需重复创建。
            if (settlement.OwnerClan != null && settlement.OwnerClan.IsRebelClan)
            {
                AffixLogger.Info("FACTORY", $"{settlement.Name} 已属叛军，跳过");
                return;
            }
            // 叛军领袖：优先用当前城主；当前城主为空时回退到玩家本体（保证 SetLeader 非空）。
            Hero owner = settlement.OwnerClan?.Leader ?? Hero.MainHero;
            if (owner == null)
            {
                AffixLogger.Warn("FACTORY", $"{settlement.Name} 无可用叛军领袖（无城主且非玩家战役），跳过叛军化");
                return;
            }
            // CreateSettlementRebelClan 内部会读取 settlement.MapFaction 取旗帜颜色，需确保地图派系非空。
            if (settlement.MapFaction == null)
            {
                AffixLogger.Warn("FACTORY", $"{settlement.Name} 地图派系为空，无法叛军化");
                return;
            }
            var rebelClan = Clan.CreateSettlementRebelClan(settlement, owner);
            AffixLogger.Info("FACTORY", $"已将 {settlement.Name} 变为叛军: {rebelClan?.Name}");
            InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] ⚔ {settlement.Name} 已归叛军所有", Colors.Yellow));

            // 边界修复（#3 叛军永久卡死）：
            // RebellionsCampaignBehavior.DailyTickClan 仅在叛军 clan 登记到其私有字典
            // _rebelClansAndDaysPassedAfterCreation 时才会约 30 天后自动转正。
            // 外部调用 Clan.CreateSettlementRebelClan 不会注入该字典，导致叛军永远卡死、
            // 旧日疆土变成永久叛旗（玩家反而更难收复）。这里通过反射把新建叛军登记进去，
            // 初始天数设为 1，使其按正常节奏转正。
            if (rebelClan != null)
                RegisterRebelForAutoPromotion(rebelClan);
        }

        /// <summary>
        /// 将叛军家族登记进 RebellionsCampaignBehavior 的计时字典，使原生行为能在约 30 天后自动转正。
        /// 因原数字段为 private，使用反射注入；反射失败时仅记录警告（不影响叛军已创建的事实）。
        /// </summary>
        private static void RegisterRebelForAutoPromotion(Clan rebelClan)
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
                    dict[rebelClan] = 1; // 初始已过去 1 天，正常 30 天后转正
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
            // 边界：家族可能尚未有 Leader（极早期/未完全初始化），直接 return 避免 NRE。
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
            // 必须用直接赋值：BT 的 Clan.AddRenown 对负值增量会被钳制（声望只增不减），
            // 当遗产声望低于当前值时无法下降。直接赋值可同时支持升/降。
            // 边界确认（#4 声望/影响力持久）：Clan.Renown 标记为 [SaveableProperty(88)]、
            // Clan.Influence 同理为自动序列化属性，直接赋值会被存档系统持久化，
            // 读档后值仍然保留，无需额外缓存刷新。
            clan.Renown = renown;
            AffixLogger.Info("FACTORY", $"SetClanRenown: {clan.Name} 声望 {before:F1} -> {clan.Renown:F1} (目标 {renown:F1})");
        }

        public static void SetClanInfluence(Clan clan, float influence)
        {
            if (clan == null) return;
            // 见 #4 说明：Influence 为自动序列化属性，直接赋值即持久化。
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
