using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using LegacyWorld.Adapter;
using LegacyWorld.BannerlordAdapter.Factories;
using LegacyWorld.Core;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using System;
using Helpers;

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

        public void MakeSettlementRebel(ISettlementInfo settlement)
        {
            var s = ObjectFinder.FindSettlementById(settlement?.Id);
            if (s != null) CreateRebelClanForSettlement(s);
        }

        /// <summary>
        /// 主动为定居点创建独立叛军家族（不依赖 RebellionsCampaignBehavior 的每日 tick 时序）。
        /// 复刻原版 CreateRebelPartyAndClan 的核心步骤：造叛军领袖 → 造叛军家族 → 向原阵营宣战 → 把城归属叛军。
        /// 这样在导入阶段（新游戏初始化时 tick 尚未开始）也能立即生成叛军，而非把城挂起或归还原国家。
        /// </summary>
        private static void CreateRebelClanForSettlement(Settlement settlement)
        {
            try
            {
                CultureObject culture = settlement.Culture;
                if (culture == null)
                {
                    AffixLogger.Warn("REBEL", $"定居点 {settlement.Name} 无文化，跳过叛军生成");
                    return;
                }

                // 1) 取一个叛军英雄模板（优先 RebelliousHeroTemplates，缺失则退回 BasicTroop）。
                CharacterObject leaderTemplate = null;
                var templates = culture.RebelliousHeroTemplates;
                if (templates != null)
                {
                    var enumerable = templates as IEnumerable<CharacterObject>;
                    if (enumerable != null)
                    {
                        var list = enumerable as List<CharacterObject> ?? enumerable.ToList();
                        if (list.Count > 0)
                            leaderTemplate = list[MBRandom.RandomInt(0, list.Count - 1)];
                    }
                }
                if (leaderTemplate == null)
                    leaderTemplate = culture.BasicTroop;

                if (leaderTemplate == null)
                {
                    AffixLogger.Warn("REBEL", $"定居点 {settlement.Name} 无可用英雄模板，跳过叛军生成");
                    return;
                }

                // 2) 造叛军领袖英雄（与原版 CreateRebelHeroInternal 一致）。
                Hero leader = HeroCreator.CreateSpecialHero(leaderTemplate, settlement, null, null, MBRandom.RandomInt(25, 40));

                // 3) 造叛军家族（Clan.CreateSettlementRebelClan 内部会 SetLeader/SetInitialHomeSettlement/CalculateMidSettlement）。
                int iconId = -1;
                if (culture.PossibleClanBannerIconsIDs != null && culture.PossibleClanBannerIconsIDs.Count > 0)
                    iconId = culture.PossibleClanBannerIconsIDs.GetRandomElement();

                Clan rebelClan = Clan.CreateSettlementRebelClan(settlement, leader, iconId);
                rebelClan.IsNoble = true;

                // 原版 RebellionsCampaignBehavior 的每日 Tick 会直接按 rebelClan 索引私有计时字典。
                // CreateSettlementRebelClan 本身不会完成这一步，因此必须在转移城镇所有权前登记。
                SettlementChangeFactory.RegisterRebelForAutoPromotion(rebelClan);

                // 4) 向原所属阵营宣战（与原版一致）。
                IFaction mapFaction = settlement.MapFaction;
                if (mapFaction != null && mapFaction != rebelClan)
                    DeclareWarAction.ApplyByRebellion(rebelClan, mapFaction);

                // 5) 把城归属叛军家族（原版 ChangeOwnerOfSettlementAction.ApplyByRebellion）。
                ChangeOwnerOfSettlementAction.ApplyByRebellion(leader, settlement);

                // 6) 生成一支领主队伍驻守，避免叛军家族无队伍。
                MobilePartyHelper.SpawnLordParty(leader, settlement);

                AffixLogger.Info("REBEL", $"为定居点 {settlement.Name} 主动创建叛军家族 {rebelClan.Name}（领袖 {leader.Name}）");
            }
            catch (Exception ex)
            {
                AffixLogger.Warn("REBEL", $"主动创建叛军家族失败（{settlement?.Name}）：{ex.Message}");
            }
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
                // 边界：BodyProperties 为值类型（struct），不能用 ?. 运算符；此处直接取串，
                // 异常时兜底为空串避免 NRE/断言中断。
                StaticBodyProperties = SafeBodyProperties(bp),
                Weight = hero.Weight,
                Build = hero.Build
            };

            // 边界：Skills.All / TraitObject.All 在战役早期可能为空，判空避免 NRE。
            var allSkills = Skills.All;
            if (allSkills != null)
            {
                foreach (var skill in allSkills)
                {
                    if (skill == null) continue;
                    profile.Skills[skill.StringId] = hero.GetSkillValue(skill);
                }
            }

            var allTraits = TraitObject.All;
            if (allTraits != null)
            {
                foreach (var trait in allTraits)
                {
                    if (trait == null) continue;
                    int value = hero.GetTraitLevel(trait);
                    if (value != 0) profile.Traits[trait.StringId] = value;
                }
            }

            return profile;
        }

        /// <summary>
        /// 安全地将 BodyProperties 转为字符串。BodyProperties 为值类型，无效/异常时兜底为空串。
        /// </summary>
        private static string SafeBodyProperties(BodyProperties bp)
        {
            try
            {
                var s = bp.ToString();
                return string.IsNullOrEmpty(s) ? string.Empty : s;
            }
            catch
            {
                return string.Empty;
            }
        }

        private class KingdomInfoWrapper : IKingdomInfo
        {
            private readonly Kingdom _k;
            public KingdomInfoWrapper(Kingdom k) => _k = k;
            public string Id => _k.StringId;
            public string Name => _k.Name.ToString();
            public IClanInfo RulerClan => _k.RulingClan != null ? new ClanInfoWrapper(_k.RulingClan) : null;
            public string Culture => _k.Culture?.StringId ?? "unknown";
            public bool IsDestroyed => _k.IsEliminated;
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
