using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using LegacyWorld.Core;
using LegacyWorld.Core.Models;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Actions;

namespace LegacyWorld.BannerlordAdapter.Factories
{
    /// <summary>依据 HeroProfile 在新世界创建独立游荡英雄。</summary>
    public static class HeroResurrectionFactory
    {
        private const char LegacyIdSeparator = '\u001F';

        public static void Resurrect(HeroProfile profile, string currentWorldId = null)
        {
            if (profile == null) return;
            try
            {
                bool sameWorld = !string.IsNullOrEmpty(currentWorldId) && currentWorldId == profile.WorldId;
                bool legacyNoWorldId = !string.IsNullOrEmpty(currentWorldId) && string.IsNullOrEmpty(profile.WorldId);
                if ((sameWorld && IsStillAliveInCurrentSave(profile)) ||
                    (legacyNoWorldId && IsStillAliveByName(profile)))
                {
                    AffixLogger.Info("HERO", $"防二重身：模板来源 Source={profile.Source} 的「{profile.Name}」在当前存档中仍然存活，跳过复刻");
                    RegisterResult(profile, null, "跳过：当前存档仍存活的自己/队友（防二重身）");
                    return;
                }

                // 新档案按稳定 LegacyId 去重，不再用姓名阻挡来自不同世界的同名英雄。
                if (!string.IsNullOrWhiteSpace(profile.LegacyId))
                {
                    if (ResurrectedHeroTracker.ContainsLegacyId(profile.LegacyId))
                    {
                        AffixLogger.Info("HERO", $"稳定身份查重命中，跳过重复复刻: {profile.LegacyId}");
                        RegisterResult(profile, null, "跳过：本次导入已处理同一 LegacyId");
                        return;
                    }
                }
                else
                {
                    // 旧档案没有稳定身份，只能保留旧版“姓名 + 文化”兜底。
                    var existing = FindExistingWanderer(profile.Name, profile.CultureId);
                    if (existing != null)
                    {
                        AffixLogger.Info("HERO", $"旧档案查重命中：游戏内已存在同名游荡英雄「{existing.Name}」(StringId={existing.StringId})，跳过复刻");
                        RegisterResult(profile, existing, "跳过：旧档案同名游荡英雄已存在");
                        return;
                    }
                }

                AffixLogger.Info("HERO", $"开始复刻英雄模板: Source={profile.Source}, Name={profile.Name}, Culture={profile.CultureId}, LegacyId={profile.LegacyId}");

                var culture = !string.IsNullOrEmpty(profile.CultureId)
                    ? MBObjectManager.Instance?.GetObjectTypeList<CultureObject>()
                        ?.FirstOrDefault(c => c.StringId == profile.CultureId)
                    : null;
                var wanderers = CharacterObject.FindAll(c => c.Occupation == Occupation.Wanderer);
                var template = (culture != null
                        ? wanderers.FirstOrDefault(c => c.Culture == culture)
                        : null)
                    ?? wanderers.FirstOrDefault();

                if (template == null)
                {
                    AffixLogger.Error("HERO", "未找到可用的游荡英雄模板，复刻中止");
                    RegisterResult(profile, null, "失败：无可用模板");
                    return;
                }

                var bornSettlement = PickHomeSettlementByCulture(culture);
                int age = CalculateAge(profile.Level);
                var hero = HeroCreator.CreateSpecialHero(template, bornSettlement, null, null, age);
                if (hero == null)
                {
                    AffixLogger.Error("HERO", "CreateSpecialHero 返回 null，复刻中止");
                    RegisterResult(profile, null, "失败：CreateSpecialHero 返回 null");
                    return;
                }

                if (!string.IsNullOrEmpty(profile.Name) || !string.IsNullOrEmpty(profile.FirstName))
                {
                    var full = new TextObject(profile.Name ?? profile.FirstName);
                    var first = new TextObject(profile.FirstName ?? profile.Name);
                    hero.SetName(full, first);
                }

                hero.IsFemale = profile.IsFemale;
                hero.SetNewOccupation(Occupation.Wanderer);

                if (!string.IsNullOrEmpty(profile.StaticBodyProperties) &&
                    (profile.StaticBodyProperties.StartsWith("<BodyProperties", StringComparison.OrdinalIgnoreCase) ||
                     profile.StaticBodyProperties.StartsWith("<BodyPropertiesMax", StringComparison.OrdinalIgnoreCase)) &&
                    BodyProperties.FromString(profile.StaticBodyProperties, out var bp))
                {
                    hero.StaticBodyProperties = bp.StaticProperties;
                    hero.Weight = profile.Weight > 0 ? profile.Weight : bp.DynamicProperties.Weight;
                    hero.Build = profile.Build > 0 ? profile.Build : bp.DynamicProperties.Build;
                }
                else if (!string.IsNullOrEmpty(profile.StaticBodyProperties))
                {
                    AffixLogger.Warn("HERO", $"外貌字符串格式无法解析（旧格式或截断），跳过外观还原: {profile.Name}");
                }

                if (profile.Skills != null)
                {
                    var allSkills = Skills.All;
                    if (allSkills != null)
                    {
                        foreach (var kv in profile.Skills)
                        {
                            var skill = allSkills.FirstOrDefault(s => s.StringId == kv.Key);
                            if (skill != null)
                            {
                                int clamped = kv.Value < 0 ? 0 : (kv.Value > 300 ? 300 : kv.Value);
                                hero.SetSkillValue(skill, clamped);
                            }
                        }
                    }
                }

                if (profile.Traits != null)
                {
                    var allTraits = TraitObject.All;
                    if (allTraits != null)
                    {
                        foreach (var kv in profile.Traits)
                        {
                            var trait = allTraits.FirstOrDefault(t => t.StringId == kv.Key);
                            if (trait != null)
                            {
                                int clamped = kv.Value < -2 ? -2 : (kv.Value > 2 ? 2 : kv.Value);
                                hero.SetTraitLevel(trait, clamped);
                            }
                        }
                    }
                }

                hero.Level = profile.Level > 0 ? profile.Level : hero.Level;

                AffixLogger.Info("HERO", $"复刻完成: {hero.Name} (性别={(hero.IsFemale ? "女" : "男")}, 等级={hero.Level}, 职业={hero.Occupation})");
                InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] ✓ 复刻英雄 {hero.Name}（{profile.Source}）", Colors.Green));

                try
                {
                    if (hero.HeroState != Hero.CharacterStates.Active)
                        hero.ChangeState(Hero.CharacterStates.Active);
                }
                catch (Exception ex) { AffixLogger.Warn("HERO", $"ChangeState(Active) 失败: {ex.Message}"); }

                try
                {
                    var settle = bornSettlement ?? PickHomeSettlementByCulture(culture);
                    if (settle != null)
                    {
                        EnterSettlementAction.ApplyForCharacterOnly(hero, settle);

                        PropertyInfo stayProp = typeof(Hero).GetProperty("StayingInSettlement");
                        if (stayProp != null && stayProp.CanWrite)
                        {
                            if (stayProp.PropertyType == typeof(bool))
                                stayProp.SetValue(hero, true);
                            else if (stayProp.PropertyType == typeof(Settlement))
                                stayProp.SetValue(hero, settle);
                        }
                    }

                    PropertyInfo visibleProp = typeof(Hero).GetProperty("IsVisible");
                    if (visibleProp != null && visibleProp.CanWrite)
                        visibleProp.SetValue(hero, true);
                }
                catch (Exception ex) { AffixLogger.Warn("HERO", $"生成进世界失败: {ex.Message}"); }

                RegisterResult(profile, hero, "成功");
            }
            catch (Exception ex)
            {
                AffixLogger.Error("HERO", $"复刻英雄 {profile?.Name} 失败", ex);
                InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] ✗ 复刻英雄 {profile?.Name} 失败: {ex.Message}", Colors.Red));
            }
        }

        private static void RegisterResult(HeroProfile profile, Hero hero, string status)
        {
            ResurrectedHeroTracker.Register(new ResurrectedHeroTracker.Entry
            {
                LegacyId = profile?.LegacyId,
                HeroStringId = hero?.StringId,
                Name = profile?.Name,
                Source = profile?.Source,
                CultureId = profile?.CultureId,
                Level = hero?.Level ?? profile?.Level ?? 0,
                WorldId = profile?.WorldId,
                Status = status
            });
        }

        private static bool IsStillAliveInCurrentSave(HeroProfile profile)
        {
            if (profile == null) return false;

            if (profile.Source == "player")
            {
                var main = Hero.MainHero;
                return main != null && main.IsAlive && MatchesOriginalHero(main, profile);
            }

            if (profile.Source == "companion")
            {
                var playerClan = Clan.PlayerClan;
                if (playerClan?.Companions != null)
                {
                    foreach (var companion in playerClan.Companions)
                    {
                        if (companion != null && companion.IsAlive && MatchesOriginalHero(companion, profile))
                            return true;
                    }
                }
            }

            return false;
        }

        private static bool MatchesOriginalHero(Hero hero, HeroProfile profile)
        {
            if (hero == null || profile == null) return false;

            string originalStringId = ExtractOriginalHeroStringId(profile.LegacyId);
            if (!string.IsNullOrWhiteSpace(originalStringId))
                return string.Equals(hero.StringId, originalStringId, StringComparison.Ordinal);

            return !string.IsNullOrEmpty(profile.Name)
                && hero.Name != null
                && string.Equals(hero.Name.ToString(), profile.Name, StringComparison.Ordinal);
        }

        private static string ExtractOriginalHeroStringId(string legacyId)
        {
            if (string.IsNullOrWhiteSpace(legacyId)) return null;
            int index = legacyId.LastIndexOf(LegacyIdSeparator);
            return index >= 0 && index + 1 < legacyId.Length ? legacyId.Substring(index + 1) : null;
        }

        private static bool IsStillAliveByName(HeroProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.Name)) return false;
            if (profile.Source == "player")
            {
                var main = Hero.MainHero;
                return main != null && main.IsAlive && main.Name != null && main.Name.ToString() == profile.Name;
            }
            if (profile.Source == "companion")
            {
                var playerClan = Clan.PlayerClan;
                if (playerClan?.Companions != null)
                {
                    foreach (var c in playerClan.Companions)
                    {
                        if (c != null && c.IsAlive && c.Name != null && c.Name.ToString() == profile.Name)
                            return true;
                    }
                }
            }
            return false;
        }

        private static int CalculateAge(int level)
        {
            if (level <= 0) return 25;
            return 18 + (int)(level * 0.3);
        }

        private static Settlement PickHomeSettlementByCulture(CultureObject culture)
        {
            var all = Settlement.All;
            if (all == null || !all.Any()) return null;

            if (culture != null)
            {
                var byCultureTown = all.FirstOrDefault(s => s.IsTown && s.Culture == culture);
                if (byCultureTown != null) return byCultureTown;
                var byCultureCastle = all.FirstOrDefault(s => (s.IsCastle || s.IsTown) && s.Culture == culture);
                if (byCultureCastle != null) return byCultureCastle;
            }

            return all.FirstOrDefault(s => s.IsTown) ?? all.FirstOrDefault();
        }

        /// <summary>仅供旧版无 LegacyId 档案使用的姓名/文化查重。</summary>
        private static Hero FindExistingWanderer(string name, string cultureId)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var h in Hero.AllAliveHeroes)
            {
                if (h == null || !h.IsActive || !h.IsWanderer) continue;
                if (h.Name == null || h.Name.ToString() != name) continue;
                if (!string.IsNullOrEmpty(cultureId) && h.Culture != null && h.Culture.StringId != cultureId) continue;
                return h;
            }
            return null;
        }
    }
}
