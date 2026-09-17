using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LegacyWorld.BannerlordAdapter.Factories;
using LegacyWorld.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace LegacyWorld.BannerlordAdapter.Patches
{
    /// <summary>
    /// 把玩家装备迁移接入现有英雄导出/复活流程，而不改动原有流程本身。
    /// SubModule 已统一调用 Harmony.PatchAll()，因此无需额外注册。
    /// </summary>
    [HarmonyPatch(typeof(BannerlordGameAdapter), "BuildHeroProfile")]
    internal static class PlayerEquipmentCapturePatch
    {
        private static void Postfix(Hero hero, string source, HeroProfile __result)
        {
            if (hero == null || __result == null ||
                !string.Equals(source, "player", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                HeroEquipmentTransferFactory.Capture(hero, __result);
            }
            catch (Exception ex)
            {
                // 装备快照失败不能阻断整个遗产导出。
                __result.HasEquipmentSnapshot = false;
                __result.BattleEquipment?.Clear();
                __result.CivilianEquipment?.Clear();
                Debug.Print($"[LegacyWorld] 玩家装备记录失败，继续导出其它英雄数据: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 复活流程是 void 且内部创建 Hero。Prefix 先记录当前所有存活英雄，Postfix 只查找
    /// 本次调用新创建出来的同名玩家遗产英雄，因此同档防二重身/重复导入命中时不会误换装。
    /// </summary>
    [HarmonyPatch(typeof(HeroResurrectionFactory), nameof(HeroResurrectionFactory.Resurrect))]
    internal static class PlayerEquipmentRestorePatch
    {
        private static void Prefix(HeroProfile profile, out HashSet<Hero> __state)
        {
            __state = null;
            if (!ShouldRestore(profile))
                return;

            try
            {
                __state = new HashSet<Hero>();
                var alive = Hero.AllAliveHeroes;
                if (alive == null)
                    return;

                foreach (Hero hero in alive)
                {
                    if (hero != null)
                        __state.Add(hero);
                }
            }
            catch (Exception ex)
            {
                __state = null;
                Debug.Print($"[LegacyWorld] 无法建立复活前英雄快照，跳过装备恢复: {ex.Message}");
            }
        }

        private static void Postfix(HeroProfile profile, HashSet<Hero> __state)
        {
            if (!ShouldRestore(profile) || __state == null)
                return;

            try
            {
                var alive = Hero.AllAliveHeroes;
                if (alive == null)
                    return;

                Hero restoredHero = alive.FirstOrDefault(hero =>
                    hero != null &&
                    !__state.Contains(hero) &&
                    hero.IsAlive &&
                    hero.Occupation == Occupation.Wanderer &&
                    string.Equals(hero.Name?.ToString(), profile.Name, StringComparison.Ordinal) &&
                    (string.IsNullOrEmpty(profile.CultureId) ||
                     hero.Culture == null ||
                     string.Equals(hero.Culture.StringId, profile.CultureId, StringComparison.Ordinal)));

                if (restoredHero == null)
                {
                    Debug.Print($"[LegacyWorld] 未找到本次新复活的玩家遗产英雄，未执行装备恢复: {profile.Name}");
                    return;
                }

                HeroEquipmentTransferFactory.Restore(restoredHero, profile);
                Debug.Print($"[LegacyWorld] 已恢复玩家遗产英雄装备: {restoredHero.Name}");
            }
            catch (Exception ex)
            {
                // 装备缺失/旧 Mod 缺失不应让英雄复活整体失败。
                Debug.Print($"[LegacyWorld] 玩家装备恢复失败但保留复活英雄: {profile?.Name}, error={ex.Message}");
            }
        }

        private static bool ShouldRestore(HeroProfile profile)
        {
            return profile != null &&
                   profile.HasEquipmentSnapshot &&
                   string.Equals(profile.Source, "player", StringComparison.OrdinalIgnoreCase);
        }
    }
}
