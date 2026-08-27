using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace LegacyWorld.Compatibility
{
    /// <summary>
    /// 兼容性补丁：修复原版在「玩家家族 FactionMidSettlement 尚未初始化（为 null）」这一特殊开局下，
    /// 角色创建选文化（ApplyCulture → Clan.ResetPlayerHomeAndFactionMidSettlement）时崩溃的问题。
    ///
    /// 原版链路：
    ///   Clan.ResetPlayerHomeAndFactionMidSettlement 先把 _midSettlement 置 null，
    ///   再调用 SettlementValueModel.FindMostSuitableHomeSettlement，
    ///   后者依赖 clan.MapFaction.FactionMidSettlement 做评分（GetSettlementScoreForBeingHomeSettlementOfClan
    ///   第81行 GetDistance(settlement, factionMidSettlement) 在 mid 为 null 时直接 NRE）。
    ///
    /// 注意：早期尝试 patch FindFarthestDistanceBetweenSettlementsInClan 让其返回 float.MinValue 是错误方向——
    ///   该值会作为 maxDistance 传入评分，反而把负无穷往下传并触发第二处 NRE。
    ///
    /// 正确做法：在 FindMostSuitableHomeSettlement 入口判断，若 mid 尚未初始化，直接短路返回一个
    /// 合理的候选主城（InitialHomeSettlement / HomeSettlement / 第一个据点），根本不进入依赖 mid 的评分。
    /// 返回有效定居点后，外层 SetInitialHomeSettlement + CalculateMidSettlement 便能正常算出 mid，
    /// 后续循环不再为 null，原版逻辑自然恢复。
    ///
    /// 删除本文件并从 SubModule 去掉 PatchAll 即可彻底还原，不修改任何原版程序集。
    /// </summary>
    [HarmonyPatch(typeof(DefaultSettlementValueModel), "FindMostSuitableHomeSettlement")]
    internal static class SettlementValueModelNullGuardPatch
    {
        // 原方法签名：public override Settlement FindMostSuitableHomeSettlement(Clan clan)
        // 当 clan 的 FactionMidSettlement 为 null（尚未初始化）时，短路返回合理候选主城，避免 NRE。
        private static bool Prefix(Clan clan, ref Settlement __result)
        {
            if (clan == null || clan.MapFaction == null)
                return true; // 交给原方法按它自己的判空处理

            if (clan.MapFaction.FactionMidSettlement == null)
            {
                __result = clan.InitialHomeSettlement
                           ?? clan.HomeSettlement
                           ?? clan.MapFaction.Settlements.FirstOrDefault(s => s.IsFortification)
                           ?? clan.MapFaction.Settlements.FirstOrDefault();
                return false; // 跳过原方法，避免解引用 null 的 mid
            }

            return true;
        }
    }
}
