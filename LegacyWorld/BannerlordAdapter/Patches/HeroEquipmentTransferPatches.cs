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
    /// BuildHeroProfile 的统一补充：所有人物写入稳定 LegacyId；玩家本体额外抓取装备快照。
    /// </summary>
    [HarmonyPatch(typeof(BannerlordGameAdapter), "BuildHeroProfile")]
    internal static class HeroProfileCapturePatch
    {
        private const string Separator = "\u001F";

        private static void Postfix(Hero hero, string source, HeroProfile __result)
        {
            if (hero == null || __result == null)
                return;

            try
            {
                if (string.IsNullOrWhiteSpace(__result.LegacyId) && !string.IsNullOrWhiteSpace(hero.StringId))
                {
                    string worldId = string.IsNullOrWhiteSpace(__result.WorldId)
                        ? Campaign.Current?.UniqueGameId ?? "0"
                        : __result.WorldId;
                    __result.LegacyId = worldId + Separator + (source ?? string.Empty) + Separator + hero.StringId;
                }

                if (!string.Equals(source, "player", StringComparison.OrdinalIgnoreCase))
                    return;

                HeroEquipmentTransferFactory.Capture(hero, __result);
            }
            catch (Exception ex)
            {
                // 稳定身份失败不阻断普通英雄数据；装备失败时仅关闭装备快照。
                if (string.Equals(source, "player", StringComparison.OrdinalIgnoreCase))
                {
                    __result.HasEquipmentSnapshot = false;
                    __result.BattleEquipment?.Clear();
                    __result.CivilianEquipment?.Clear();
                }
                Debug.Print($"[LegacyWorld] 英雄档案补充失败，继续导出其它数据: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 复活流程是 void 且内部创建 Hero。Prefix 记录复活前对象，Postfix 只对本次新建的玩家遗产英雄恢复装备。
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
