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
        /// <summary>
        /// 将定居点变为叛军所有（游戏原生 CreateSettlementRebelClan 机制）。
        /// 用于在旧档玩家家族于新档不存在时，让城池归属叛军而非凭空消失或白给玩家本体。
        /// </summary>
        void MakeSettlementRebel(ISettlementInfo settlement);

        // ===== 英雄模板（A 方案：玩家本体 + 玩家招募过且存活的非固定名 NPC）=====
        IEnumerable<LegacyWorld.Core.Models.HeroProfile> GetHeroProfiles();
        void ResurrectHero(LegacyWorld.Core.Models.HeroProfile profile, string currentWorldId);
    }

    public interface IKingdomInfo
    {
        string Id { get; }
        string Name { get; }
        IClanInfo RulerClan { get; }
        string Culture { get; }
        bool IsDestroyed { get; }
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
