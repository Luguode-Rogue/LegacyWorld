using System;
using LegacyWorld.Adapter;
using LegacyWorld.Core;
using LegacyWorld.Core.Models;
using LegacyWorld.Core.Settings;

namespace LegacyWorld.Core.Import
{
    /// <summary>
    /// 导入编排器。三阶段顺序：Kingdom → Clan → Settlement。
    /// 顺序满足依赖约束（定居点需要所属家族已存在）。
    /// </summary>
    public static class LegacyImporter
    {
        public static ImportResult Apply(IGameAdapter adapter, LegacyData data, LegacySettings settings)
        {
            AffixLogger.Info("IMPORT", "================ 导入开始 ================");
            AffixLogger.Info("IMPORT", $"遗产世界: {data.WorldId} | 版本 {data.Version} | 家族 {data.Clans.Count} 个");
            DumpCurrentState(adapter, "【导入前】当前游戏状态");

            var result = new ImportResult();
            result.KingdomsRestored = KingdomImporter.Restore(adapter, data, settings);
            result.ClansRestored = ClanImporter.Restore(adapter, data, settings);
            result.SettlementsRestored = SettlementImporter.Restore(adapter, data, settings);

            result.HeroesResurrected = 0;
            result.HeroesResurrectFailed = 0;
            if (settings.RestoreHeroes && data.HeroProfiles != null && data.HeroProfiles.Count > 0)
            {
                AffixLogger.Info("IMPORT", $"开始复刻英雄模板: {data.HeroProfiles.Count} 个");
                foreach (var profile in data.HeroProfiles)
                {
                    if (profile == null) continue;
                    try
                    {
                        adapter.ResurrectHero(profile);
                        result.HeroesResurrected++;
                    }
                    catch (Exception ex)
                    {
                        result.HeroesResurrectFailed++;
                        AffixLogger.Error("IMPORT", $"复刻英雄失败: {profile.Name} ({profile.Source})", ex);
                    }
                }
                AffixLogger.Info("IMPORT", $"英雄模板复刻完成: 成功 {result.HeroesResurrected} 个 / 失败 {result.HeroesResurrectFailed} 个");
            }
            else if (settings.RestoreHeroes)
            {
                AffixLogger.Info("IMPORT", "复原英雄已启用，但遗产数据中无英雄模板记录");
            }
            else
            {
                AffixLogger.Info("IMPORT", "复原英雄未启用（设置 RestoreHeroes=false），跳过英雄复刻");
            }

            DumpCurrentState(adapter, "【导入后】当前游戏状态");
            AffixLogger.Info("IMPORT", $"导入编排完成: {result.KingdomsRestored} 王国 / {result.ClansRestored} 家族 / {result.SettlementsRestored} 定居点 / {result.HeroesResurrected} 英雄");
            AffixLogger.Info("IMPORT", "================ 导入结束 ================");
            return result;
        }

        /// <summary>
        /// 输出当前游戏状态快照（家族声望/金币/影响力/所属王国 + 定居点归属），
        /// 用于导入前后对比，方便排查数据是否被真正写入。
        /// </summary>
        private static void DumpCurrentState(IGameAdapter adapter, string title)
        {
            AffixLogger.Info("DUMP", title);
            foreach (var c in adapter.GetAllClans())
            {
                if (c == null) continue;
                AffixLogger.Info("DUMP",
                    $"  家族 {c.Name}({c.Id}): 王国={c.Kingdom?.Name ?? "无"} | 等级T{c.Tier} | 金币{c.Gold} | 声望{c.Renown:F1} | 影响力{c.Influence:F1}");
            }
            foreach (var s in adapter.GetAllSettlements())
            {
                if (s == null) continue;
                AffixLogger.Info("DUMP",
                    $"  定居点 {s.Name}({s.Id})[{s.Type}]: 归属={s.OwnerClan?.Name ?? "无"} | 王国={s.OwnerKingdom?.Name ?? "无"}");
            }
        }
    }

    public class ImportResult
    {
        public int KingdomsRestored { get; set; }
        public int ClansRestored { get; set; }
        public int SettlementsRestored { get; set; }
        public int HeroesResurrected { get; set; }
        public int HeroesResurrectFailed { get; set; }
    }
}
