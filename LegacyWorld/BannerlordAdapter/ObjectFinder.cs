using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using LegacyWorld.Adapter;

namespace LegacyWorld.BannerlordAdapter
{
    /// <summary>
    /// Bannerlord 游戏对象查找辅助类。
    /// </summary>
    public static class ObjectFinder
    {
        public static Kingdom FindKingdomById(string id) => Campaign.Current?.Kingdoms?.Find(k => k.StringId == id);
        public static Clan FindClanById(string id) => Campaign.Current?.Clans?.Find(c => c.StringId == id);
        public static Settlement FindSettlementById(string id) => Settlement.Find(id);
    }
}
