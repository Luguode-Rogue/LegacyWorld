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
        public static void Resurrect(HeroProfile profile)
        {
            if (profile == null) return;
            try
            {
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

        private static int CalculateAge(int level)
        {
            // 经验公式：以 18 岁为起点，每级约 0.3 岁，粗略还原年龄
            if (level <= 0) return 25;
            return 18 + (int)(level * 0.3);
        }
    }
}
