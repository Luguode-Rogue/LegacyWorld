using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using LegacyWorld.Adapter;
using LegacyWorld.BannerlordAdapter.Factories;
using LegacyWorld.Core;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;

namespace LegacyWorld.BannerlordAdapter
{
    /// <summary>
    /// Bannerlord v1.2.x 适配器实现。
    /// 负责将游戏内部对象映射为 Adapter 信息接口，并委托给 SettlementChangeFactory 执行变更。
    /// </summary>
    public class BannerlordGameAdapter : IGameAdapter
    {
        public string GetWorldId()
        {
            return Campaign.Current?.UniqueGameId ?? "0";
        }

        public string GetCurrentGameTime() => CampaignTime.Now.ToString();
        public string GetDominantCulture() => Campaign.Current?.Kingdoms?.FirstOrDefault()?.Culture?.StringId ?? "unknown";
        public string GetGameVersion() => ApplicationVersion.FromParametersFile().ToString();

        public IEnumerable<IKingdomInfo> GetAllKingdoms()
        {
            if (Campaign.Current?.Kingdoms == null)
                return Enumerable.Empty<IKingdomInfo>();

            return Campaign.Current.Kingdoms
                .Where(k => !k.IsEliminated)
                .Select(k => (IKingdomInfo)new KingdomInfoWrapper(k))
                .ToList();
        }

        public IKingdomInfo FindKingdom(string id)
        {
            var kingdom = ObjectFinder.FindKingdomById(id);
            return kingdom != null ? new KingdomInfoWrapper(kingdom) : null;
        }

        public void SetKingdomRuler(IKingdomInfo kingdom, IClanInfo ruler)
        {
            if (kingdom == null || ruler == null) return;
            var k = ObjectFinder.FindKingdomById(kingdom.Id);
            var c = ObjectFinder.FindClanById(ruler.Id);
            if (k != null && c != null) SettlementChangeFactory.SetKingdomRuler(k, c);
        }

        public IEnumerable<IClanInfo> GetAllClans()
        {
            if (Campaign.Current?.Clans == null)
                return Enumerable.Empty<IClanInfo>();

            return Campaign.Current.Clans
                .Where(c => !c.IsMinorFaction && !c.IsBanditFaction)
                .Select(c => (IClanInfo)new ClanInfoWrapper(c))
                .ToList();
        }

        public IClanInfo FindClan(string id)
        {
            var clan = ObjectFinder.FindClanById(id);
            return clan != null ? new ClanInfoWrapper(clan) : null;
        }

        public IClanInfo CreateClan(string id, string name, string culture)
        {
            AffixLogger.Warn("ADAPTER", $"CreateClan 尚未完全实现: {id}");
            return null;
        }

        public void SetClanKingdom(IClanInfo clan, IKingdomInfo kingdom)
        {
            var c = ObjectFinder.FindClanById(clan?.Id);
            var k = ObjectFinder.FindKingdomById(kingdom?.Id);
            if (c != null && k != null) SettlementChangeFactory.SetClanKingdom(c, k);
        }

        public void SetClanGold(IClanInfo clan, long gold)
        {
            var c = ObjectFinder.FindClanById(clan?.Id);
            if (c != null) SettlementChangeFactory.SetClanGold(c, (int)gold);
        }

        public void SetClanRenown(IClanInfo clan, float renown)
        {
            var c = ObjectFinder.FindClanById(clan?.Id);
            if (c != null) SettlementChangeFactory.SetClanRenown(c, renown);
        }

        public void SetClanInfluence(IClanInfo clan, float influence)
        {
            var c = ObjectFinder.FindClanById(clan?.Id);
            if (c != null) SettlementChangeFactory.SetClanInfluence(c, influence);
        }

        public IEnumerable<ISettlementInfo> GetAllSettlements()
        {
            return Settlement.All
                .Where(s => s.IsTown || s.IsCastle || s.IsVillage)
                .Select(s => new SettlementInfoWrapper(s));
        }

        public ISettlementInfo FindSettlement(string id)
        {
            var s = ObjectFinder.FindSettlementById(id);
            return s != null ? new SettlementInfoWrapper(s) : null;
        }

        public void ChangeSettlementOwner(ISettlementInfo settlement, IClanInfo newOwner)
        {
            var s = ObjectFinder.FindSettlementById(settlement?.Id);
            var c = ObjectFinder.FindClanById(newOwner?.Id);
            if (s != null && c != null) SettlementChangeFactory.ChangeOwner(s, c);
        }

        public void SetSettlementProsperity(ISettlementInfo settlement, float prosperity)
        {
            var s = ObjectFinder.FindSettlementById(settlement?.Id);
            if (s != null) SettlementChangeFactory.SetProsperity(s, (int)prosperity);
        }

        // ===== 英雄模板（A 方案：玩家本体 + 玩家招募过且存活的非固定名 NPC）=====
        public IEnumerable<LegacyWorld.Core.Models.HeroProfile> GetHeroProfiles()
        {
            var result = new List<LegacyWorld.Core.Models.HeroProfile>();

            var mainHero = Hero.MainHero;
            if (mainHero != null)
            {
                result.Add(BuildHeroProfile(mainHero, "player"));
                AffixLogger.Info("HERO", $"导出英雄: {mainHero.Name} (player, Lv{mainHero.Level})");
            }

            var playerClan = Clan.PlayerClan;
            if (playerClan != null)
            {
                foreach (var hero in playerClan.Companions)
                {
                    if (hero == null || !hero.IsAlive) continue;
                    result.Add(BuildHeroProfile(hero, "companion"));
                    AffixLogger.Info("HERO", $"导出英雄: {hero.Name} (companion, Lv{hero.Level})");
                }
            }

            return result;
        }

        public void ResurrectHero(LegacyWorld.Core.Models.HeroProfile profile, string currentWorldId)
        {
            HeroResurrectionFactory.Resurrect(profile, currentWorldId);
        }

        private static LegacyWorld.Core.Models.HeroProfile BuildHeroProfile(Hero hero, string source)
        {
            var bp = hero.BodyProperties;
            var profile = new LegacyWorld.Core.Models.HeroProfile
            {
                Source = source,
                WorldId = Campaign.Current?.UniqueGameId ?? "0",
                Name = hero.Name?.ToString(),
                FirstName = hero.FirstName?.ToString(),
                CultureId = hero.CharacterObject?.Culture?.StringId,
                IsFemale = hero.IsFemale,
                Level = hero.Level,
                Occupation = hero.Occupation.ToString(),
                StaticBodyProperties = bp.ToString(),
                Weight = hero.Weight,
                Build = hero.Build
            };

            foreach (var skill in Skills.All)
            {
                if (skill == null) continue;
                profile.Skills[skill.StringId] = hero.GetSkillValue(skill);
            }

            foreach (var trait in TraitObject.All)
            {
                int value = hero.GetTraitLevel(trait);
                if (value != 0) profile.Traits[trait.StringId] = value;
            }

            return profile;
        }

        private class KingdomInfoWrapper : IKingdomInfo
        {
            private readonly Kingdom _k;
            public KingdomInfoWrapper(Kingdom k) => _k = k;
            public string Id => _k.StringId;
            public string Name => _k.Name.ToString();
            public IClanInfo RulerClan => _k.RulingClan != null ? new ClanInfoWrapper(_k.RulingClan) : null;
            public string Culture => _k.Culture?.StringId ?? "unknown";
        }

        private class ClanInfoWrapper : IClanInfo
        {
            private readonly Clan _c;
            public ClanInfoWrapper(Clan c) => _c = c;
            public string Id => _c.StringId;
            public string Name => _c.Name.ToString();
            public IKingdomInfo Kingdom => _c.Kingdom != null ? new KingdomInfoWrapper(_c.Kingdom) : null;
            public int Tier => _c.Tier;
            public long Gold => _c.Gold;
            public float Renown => _c.Renown;
            public float Influence => _c.Influence;
            public bool IsDestroyed => _c.IsEliminated;
        }

        private class SettlementInfoWrapper : ISettlementInfo
        {
            private readonly Settlement _s;
            public SettlementInfoWrapper(Settlement s) => _s = s;
            public string Id => _s.StringId;
            public string Name => _s.Name.ToString();
            public string Type => _s.IsTown ? "Town" : _s.IsCastle ? "Castle" : "Village";
            public IClanInfo OwnerClan => _s.OwnerClan != null ? new ClanInfoWrapper(_s.OwnerClan) : null;
            public IKingdomInfo OwnerKingdom => _s.OwnerClan?.Kingdom != null ? new KingdomInfoWrapper(_s.OwnerClan.Kingdom) : null;
            public string Culture => _s.Culture?.StringId ?? "unknown";
            public float Prosperity => _s.Town?.Prosperity ?? 0f;
        }
    }
}
