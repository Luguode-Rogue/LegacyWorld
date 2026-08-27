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
                //    边界：若 profile.WorldId 缺失（旧版本未写入），无法信任 WorldId 比对，
                //    退化为"按姓名在当前存档查重即跳过"，宁可少复刻也不生成二重身。
                bool sameWorld = !string.IsNullOrEmpty(currentWorldId) && currentWorldId == profile.WorldId;
                bool legacyNoWorldId = !string.IsNullOrEmpty(currentWorldId) && string.IsNullOrEmpty(profile.WorldId);
                if ((sameWorld && IsStillAliveInCurrentSave(profile)) ||
                    (legacyNoWorldId && IsStillAliveByName(profile)))
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
                    ? MBObjectManager.Instance?.GetObjectTypeList<CultureObject>()
                        ?.FirstOrDefault(c => c.StringId == profile.CultureId)
                    : null;
                // 先取全部游荡英雄模板（只查一次，避免 FindAll 重复断言），再按文化过滤；
                // 文化匹配不到时回退到任意游荡英雄。注意 FindAll 返回的是集合，不能用 ?? 接单个对象。
                var wanderers = CharacterObject.FindAll(c => c.Occupation == Occupation.Wanderer);
                var template = (culture != null
                        ? wanderers.FirstOrDefault(c => c.Culture == culture)
                        : null)
                    ?? wanderers.FirstOrDefault();

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
                //    出生地选择：优先找文化匹配的城镇，让复刻出的 NPC "回到故乡"；
                //    文化匹配不到时回退到任意城镇。CultureObject 可能来自存档时的旧 ID，
                //    若当前游戏不存在该文化则为 null，此时直接用任意城镇兜底。
                var bornSettlement = PickHomeSettlementByCulture(culture);
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
                //    边界：BodyProperties.FromString 对旧格式/截断字符串会触发引擎级
                //    Debug.FailedAssert（甚至弹窗）。这里先做格式前缀预检，避免触发断言。
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

                // 7. 还原技能（钳制到合法区间 0..10 级对应的技能值上限，避免越界异常）
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

                // 8. 还原特性（钳制到 [-2, 2] 合法区间）
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

                // 9. 还原等级
                hero.Level = profile.Level > 0 ? profile.Level : hero.Level;

                AffixLogger.Info("HERO", $"复刻完成: {hero.Name} (性别={(hero.IsFemale ? "女" : "男")}, 等级={hero.Level}, 职业={hero.Occupation})");
                InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] ✓ 复刻英雄 {hero.Name}（{profile.Source}）", Colors.Green));

                // 边界：刚创建的英雄默认状态可能不是 Active（未真正 spawn 进世界），
                // 导致「复刻成功」为假象（验证时按姓名查不到）。显式置为 Active 确保真正进入世界。
                try
                {
                    if (hero.HeroState != Hero.CharacterStates.Active)
                        hero.ChangeState(Hero.CharacterStates.Active);
                }
                catch (Exception ex) { AffixLogger.Warn("HERO", $"ChangeState(Active) 失败: {ex.Message}"); }

                // 边界：ChangeState(Active) 仅让英雄进入 Hero.AllAliveHeroes（数据层可见），
                // 但游荡英雄不会真正出现在世界里——酒馆看不到、地图不显示。必须显式：
                //   1) EnterSettlementAction 让英雄"进入"指定定居点（生成到该城）；
                //   2) StayingInSettlement 标记为驻留（常驻酒馆供招募）；
                //   3) IsVisible = true 设为可见（仅旧版引擎存在该属性）。
                // 否则复刻只是"数据层幽灵"，验证能查到名字，游戏里却人不在。
                // 注意：StayingInSettlement / IsVisible 在不同游戏版本间签名不同
                // （旧版 StayingInSettlement 为 bool、IsVisible 存在；新版为 Settlement、IsVisible 已移除），
                // 这里改用运行时反射探测属性存在性与类型，做到旧版可用、新版兼容，且 net472/net6 均可编译。
                try
                {
                    var settle = bornSettlement ?? PickHomeSettlementByCulture(culture);
                    if (settle != null)
                    {
                        EnterSettlementAction.ApplyForCharacterOnly(hero, settle);

                        // StayingInSettlement：旧版为 bool（=true 表示常驻），
                        // 新版为 Settlement（=settle 表示常驻该城）。按属性类型分支赋值。
                        PropertyInfo stayProp = typeof(Hero).GetProperty("StayingInSettlement");
                        if (stayProp != null && stayProp.CanWrite)
                        {
                            if (stayProp.PropertyType == typeof(bool))
                                stayProp.SetValue(hero, true);
                            else if (stayProp.PropertyType == typeof(Settlement))
                                stayProp.SetValue(hero, settle);
                        }
                    }

                    // IsVisible：仅旧版引擎存在该可写属性；新版（1.4+）已移除，
                    // 英雄可见性由 HeroState=Active + 进入定居点保证，探测为 null 时跳过即可。
                    PropertyInfo visibleProp = typeof(Hero).GetProperty("IsVisible");
                    if (visibleProp != null && visibleProp.CanWrite)
                        visibleProp.SetValue(hero, true);
                }
                catch (Exception ex) { AffixLogger.Warn("HERO", $"生成进世界失败: {ex.Message}"); }

                // 登记到运行时追踪表，供 MCM 验证按钮"列出已复刻英雄"读取
                ResurrectedHeroTracker.Register(new ResurrectedHeroTracker.Entry
                {
                    HeroStringId = hero.StringId,
                    Name = profile.Name,
                    Source = profile.Source,
                    CultureId = profile.CultureId,
                    Level = hero.Level,
                    WorldId = profile.WorldId
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

        /// <summary>
        /// 旧版本兜底：当 profile.WorldId 缺失、无法信任 WorldId 比对时，
        /// 退化为"按姓名在当前存档内查重"。命中说明当前存档里仍有同名且存活的
        /// 自己/队友，跳过复刻以避免二重身（宁可少复刻也不重复生成）。
        /// </summary>
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
                return false;
            }
            return false;
        }

        private static int CalculateAge(int level)
        {
            // 经验公式：以 18 岁为起点，每级约 0.3 岁，粗略还原年龄
            if (level <= 0) return 25;
            return 18 + (int)(level * 0.3);
        }

        /// <summary>
        /// 按文化挑选一个定居点作为复刻英雄的"故乡"，让 NPC 落在该文化对应的城镇里。
        /// 优先匹配文化相同的城镇（城镇/城堡皆可）；匹配不到时回退到任意城镇；
        /// 极早期无任何定居点时返回 null（交给调用方兜底）。
        /// </summary>
        private static Settlement PickHomeSettlementByCulture(CultureObject culture)
        {
            var all = Settlement.All;
            if (all == null || !all.Any()) return null;

            // 文化匹配：优先城镇（游荡英雄驻留酒馆），其次城堡
            if (culture != null)
            {
                var byCultureTown = all.FirstOrDefault(s => s.IsTown && s.Culture == culture);
                if (byCultureTown != null) return byCultureTown;
                var byCultureCastle = all.FirstOrDefault(s => (s.IsCastle || s.IsTown) && s.Culture == culture);
                if (byCultureCastle != null) return byCultureCastle;
            }

            // 回退：任意城镇；再不行任意定居点
            return all.FirstOrDefault(s => s.IsTown)
                ?? all.FirstOrDefault();
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
