using System.Collections.Generic;

namespace LegacyWorld.Adapter
{
    /// <summary>
    /// 游戏适配器抽象接口。
    /// 隔离所有 TaleWorlds.* 依赖，使 Core 层可独立测试、版本可替换。
    /// </summary>
    public interface IGameAdapter
    {
        // ===== 世界信息 =====
        string GetWorldId();
        string GetCurrentGameTime();
        string GetDominantCulture();
        string GetGameVersion();

        // ===== 王国操作 =====
        IEnumerable<IKingdomInfo> GetAllKingdoms();
        IKingdomInfo FindKingdom(string id);
        void SetKingdomRuler(IKingdomInfo kingdom, IClanInfo ruler);

        // ===== 家族操作 =====
        IEnumerable<IClanInfo> GetAllClans();
        IClanInfo FindClan(string id);
        IClanInfo CreateClan(string id, string name, string culture);
        void SetClanKingdom(IClanInfo clan, IKingdomInfo kingdom);
        void SetClanGold(IClanInfo clan, long gold);
        void SetClanRenown(IClanInfo clan, float renown);
        void SetClanInfluence(IClanInfo clan, float influence);

        // ===== 定居点操作 =====
        IEnumerable<ISettlementInfo> GetAllSettlements();
        ISettlementInfo FindSettlement(string id);
        void ChangeSettlementOwner(ISettlementInfo settlement, IClanInfo newOwner);
        void SetSettlementProsperity(ISettlementInfo settlement, float prosperity);

        // ===== 英雄模板（A 方案：玩家本体 + 玩家招募过且存活的非固定名 NPC）=====
        IEnumerable<LegacyWorld.Core.Models.HeroProfile> GetHeroProfiles();
        void ResurrectHero(LegacyWorld.Core.Models.HeroProfile profile);
    }

    public interface IKingdomInfo
    {
        string Id { get; }
        string Name { get; }
        IClanInfo RulerClan { get; }
        string Culture { get; }
    }

    public interface IClanInfo
    {
        string Id { get; }
        string Name { get; }
        IKingdomInfo Kingdom { get; }
        int Tier { get; }
        long Gold { get; }
        float Renown { get; }
        float Influence { get; }
        bool IsDestroyed { get; }
    }

    public interface ISettlementInfo
    {
        string Id { get; }
        string Name { get; }
        string Type { get; }
        IClanInfo OwnerClan { get; }
        IKingdomInfo OwnerKingdom { get; }
        string Culture { get; }
        float Prosperity { get; }
    }
}
