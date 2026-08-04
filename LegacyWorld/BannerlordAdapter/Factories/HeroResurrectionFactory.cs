using System;
using System.Linq;
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

namespace LegacyWorld.BannerlordAdapter.Factories
{
    /// <summary>
    /// 英雄模板复刻工厂（A 方案）。
    /// 依据导出的 HeroProfile，在新游戏中创建一个属性/外貌相近的"游荡英雄"，
    /// 实现"遇到原来的自己/伙伴"的效果。
    /// 仅复刻模板数据（姓名/文化/性别/外貌/技能/特性），不迁移身份或阵营归属。
    /// </summary>
    public static class HeroResurrectionFactory
    {
        public static void Resurrect(HeroProfile profile, string currentWorldId = null)
        {
            if (profile == null) return;
            try
            {
                // 0. 防本存档二重身：player/companion 模板指向"自己或当前队友"。
                //    关键：仅当"当前存档 WorldId == 遗产模板 WorldId"（即同一个存档）时，
                //    才判断原 Hero 是否仍存活；命中则跳过，避免在本存档内生成二重身。
                //    若是跨存档（A 导出、B 导入，WorldId 不同）——即使姓名雷同也只是不同生命，
                //    必须放行复刻（"遇到原来的自己"），绝不能靠名字误拦。
                if (!string.IsNullOrEmpty(currentWorldId) && currentWorldId == profile.WorldId && IsStillAliveInCurrentSave(profile))
                {
                    AffixLogger.Info("HERO", $"防二重身：模板来源 Source={profile.Source} 的「{profile.Name}」在当前存档中仍然存活（自己/当前队友），跳过复刻以避免本存档二重身");
                    ResurrectedHeroTracker.Register(new ResurrectedHeroTracker.Entry
                    {
                        Name = profile.Name,
                        Source = profile.Source,
                        CultureId = profile.CultureId,
                        Level = profile.Level,
                        Status = "跳过：当前存档仍存活的自己/队友（防二重身）"
                    });
                    return;
                }

                // 1. 查重：扫描当前游戏内已有的游荡英雄，若存在同名同文化的，说明已经复刻过，跳过。
                //    依赖游戏内实际对象而非进程内存，因此跨不同世界存档切换、重开游戏、多次点导入都不会复制出重复 NPC。
                var existing = FindExistingWanderer(profile.Name, profile.CultureId);
                if (existing != null)
                {
                    AffixLogger.Info("HERO", $"查重命中：游戏内已存在同名游荡英雄「{existing.Name}」(StringId={existing.StringId})，跳过复刻以避免重复: Source={profile.Source}, Name={profile.Name}");
                    ResurrectedHeroTracker.Register(new ResurrectedHeroTracker.Entry
                    {
                        HeroStringId = existing.StringId,
                        Name = profile.Name,
                        Source = profile.Source,
                        CultureId = profile.CultureId,
                        Level = profile.Level,
                        Status = "跳过：游戏内已存在同名游荡英雄"
                    });
                    return;
                }

                AffixLogger.Info("HERO", $"开始复刻英雄模板: Source={profile.Source}, Name={profile.Name}, Culture={profile.CultureId}");

                // 1. 选定与文化匹配的游荡英雄模板（决定种族，避免外观错位）
                var culture = !string.IsNullOrEmpty(profile.CultureId)
                    ? MBObjectManager.Instance.GetObjectTypeList<CultureObject>()
                        .FirstOrDefault(c => c.StringId == profile.CultureId)
                    : null;
                var template = CharacterObject.FindAll(c =>
                        c.Occupation == Occupation.Wanderer &&
                        (culture == null || c.Culture == culture))
                    .FirstOrDefault() ?? CharacterObject.FindAll(c => c.Occupation == Occupation.Wanderer).FirstOrDefault();

                if (template == null)
                {
                    AffixLogger.Error("HERO", "未找到可用的游荡英雄模板，复刻中止");
                    ResurrectedHeroTracker.Register(new ResurrectedHeroTracker.Entry
                    {
                        Name = profile.Name,
                        Source = profile.Source,
                        CultureId = profile.CultureId,
                        Level = profile.Level,
                        Status = "失败：无可用模板"
                    });
                    return;
                }

                // 2. 创建英雄（CreateSpecialHero 内部会随机生成外观，稍后覆盖）
                var bornSettlement = Settlement.All.FirstOrDefault(s => s.IsTown) ?? Settlement.All.FirstOrDefault();
                int age = CalculateAge(profile.Level);
                var hero = HeroCreator.CreateSpecialHero(template, bornSettlement, null, null, age);
                if (hero == null)
                {
                    AffixLogger.Error("HERO", "CreateSpecialHero 返回 null，复刻中止");
                    ResurrectedHeroTracker.Register(new ResurrectedHeroTracker.Entry
                    {
                        Name = profile.Name,
                        Source = profile.Source,
                        CultureId = profile.CultureId,
                        Level = profile.Level,
                        Status = "失败：CreateSpecialHero 返回 null"
                    });
                    return;
                }

                // 3. 还原姓名
                if (!string.IsNullOrEmpty(profile.Name) || !string.IsNullOrEmpty(profile.FirstName))
                {
                    var full = new TextObject(profile.Name ?? profile.FirstName);
                    var first = new TextObject(profile.FirstName ?? profile.Name);
                    hero.SetName(full, first);
                }

                // 4. 还原性别
                hero.IsFemale = profile.IsFemale;

                // 5. 还原职业（游荡者，出现在酒馆供招募）。
                // 注意：Hero 没有可写的 Culture 属性，文化由 CreateSpecialHero 选用的模板决定，
                // 上面已优先挑选与 profile.CultureId 匹配的模板，因此无需手动设置文化。
                hero.SetNewOccupation(Occupation.Wanderer);

                // 6. 还原外貌（静态外观 + 体重/体型）
                if (!string.IsNullOrEmpty(profile.StaticBodyProperties) &&
                    BodyProperties.FromString(profile.StaticBodyProperties, out var bp))
                {
                    hero.StaticBodyProperties = bp.StaticProperties;
                    hero.Weight = profile.Weight > 0 ? profile.Weight : bp.DynamicProperties.Weight;
                    hero.Build = profile.Build > 0 ? profile.Build : bp.DynamicProperties.Build;
                }

                // 7. 还原技能
                if (profile.Skills != null)
                {
                    foreach (var kv in profile.Skills)
                    {
                        var skill = Skills.All.FirstOrDefault(s => s.StringId == kv.Key);
                        if (skill != null) hero.SetSkillValue(skill, kv.Value);
                    }
                }

                // 8. 还原特性
                if (profile.Traits != null)
                {
                    foreach (var kv in profile.Traits)
                    {
                        var trait = TraitObject.All.FirstOrDefault(t => t.StringId == kv.Key);
                        if (trait != null) hero.SetTraitLevel(trait, kv.Value);
                    }
                }

                // 9. 还原等级
                hero.Level = profile.Level > 0 ? profile.Level : hero.Level;

                AffixLogger.Info("HERO", $"复刻完成: {hero.Name} (性别={(hero.IsFemale ? "女" : "男")}, 等级={hero.Level}, 职业={hero.Occupation})");
                InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] ✓ 复刻英雄 {hero.Name}（{profile.Source}）", Colors.Green));

                // 登记到运行时追踪表，供 MCM 验证按钮"列出已复刻英雄"读取
                ResurrectedHeroTracker.Register(new ResurrectedHeroTracker.Entry
                {
                    HeroStringId = hero.StringId,
                    Name = profile.Name,
                    Source = profile.Source,
                    CultureId = profile.CultureId,
                    Level = hero.Level
                });
            }
            catch (Exception ex)
            {
                AffixLogger.Error("HERO", $"复刻英雄 {profile?.Name} 失败", ex);
                InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] ✗ 复刻英雄 {profile?.Name} 失败: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// 判断模板对应的原 Hero 在当前存档中是否仍然存活。
        /// 用于"防本存档二重身"：player 模板比对 Hero.MainHero（当前本人），
        /// companion 模板在 PlayerClan.Companions 中比对（当前队友）。
        /// 命中即说明"现在的自己/队友"还在游戏里，不应再复刻出一个游荡英雄。
        /// 只有在换了新档（原自己/队友已不在当前游戏）时才返回 false，从而允许复刻（遇到原来的自己）。
        /// </summary>
        private static bool IsStillAliveInCurrentSave(HeroProfile profile)
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
                if (playerClan != null && playerClan.Companions != null)
                {
                    foreach (var c in playerClan.Companions)
                    {
                        if (c != null && c.IsAlive && c.Name != null && c.Name.ToString() == profile.Name)
                            return true;
                    }
                }
                return false;
            }

            // 其它来源（wanderer/other）按姓名在游荡英雄中查重即可，不属于"自己/队友"范畴
            return false;
        }

        private static int CalculateAge(int level)
        {
            // 经验公式：以 18 岁为起点，每级约 0.3 岁，粗略还原年龄
            if (level <= 0) return 25;
            return 18 + (int)(level * 0.3);
        }

        /// <summary>
        /// 在当前游戏内查找是否已存在同名且文化匹配的游荡英雄。
        /// 用于复刻前查重，避免在不同世界存档间来回导入时复制出重复 NPC。
        /// </summary>
        private static Hero FindExistingWanderer(string name, string cultureId)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var h in Hero.AllAliveHeroes)
            {
                if (h == null || !h.IsActive || !h.IsWanderer) continue;
                if (h.Name == null || h.Name.ToString() != name) continue;
                // 文化匹配（Culture 可能为 null，仅当 profile 指定了文化时才校验）
                if (!string.IsNullOrEmpty(cultureId) && h.Culture != null && h.Culture.StringId != cultureId) continue;
                return h;
            }
            return null;
        }
    }
}
