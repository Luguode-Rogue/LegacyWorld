using System;
using LegacyWorld.Adapter;
using LegacyWorld.Core;
using LegacyWorld.Core.Models;
using LegacyWorld.Core.Settings;
using TaleWorlds.CampaignSystem;

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
            string currentWorldId = adapter.GetWorldId();
            AffixLogger.Info("IMPORT", $"遗产世界: {data.WorldId} | 版本 {data.Version}");
            AffixLogger.Info("IMPORT", $"[NPC] 当前游戏世界: {adapter.GetWorldId()}");
            AffixLogger.Info("IMPORT", $"[NPC] 设置 RestoreHeroes={settings.RestoreHeroes} | 遗产英雄模板数={data.HeroProfiles?.Count ?? 0}");

            var result = new ImportResult();
            result.KingdomsRestored = KingdomImporter.Restore(adapter, data, settings);
            result.ClansRestored = ClanImporter.Restore(adapter, data, settings);
            result.SettlementsRestored = SettlementImporter.Restore(adapter, data, settings);

            result.HeroesResurrected = 0;
            result.HeroesResurrectFailed = 0;
            if (settings.RestoreHeroes && data.HeroProfiles != null && data.HeroProfiles.Count > 0)
            {
                AffixLogger.Info("IMPORT", $"[NPC] 开始复刻英雄模板: 共 {data.HeroProfiles.Count} 个");
                int skipped = 0;
                foreach (var profile in data.HeroProfiles)
                {
                    if (profile == null) continue;
                    AffixLogger.Info("IMPORT",
                        $"[NPC] 处理模板 #{result.HeroesResurrected + skipped + 1}: Name={profile.Name}, Source={profile.Source}, Culture={profile.CultureId}, Level={profile.Level}");
                    int before = CountWanderers(adapter);
                    try
                    {
                        adapter.ResurrectHero(profile, currentWorldId);
                        int after = CountWanderers(adapter);
                        if (after > before)
                        {
                            result.HeroesResurrected++;
                            AffixLogger.Info("IMPORT", $"[NPC] → 实际新建了 1 个游荡英雄（当前游荡英雄总数={after}）");
                        }
                        else
                        {
                            skipped++;
                            AffixLogger.Info("IMPORT", $"[NPC] → 未新建（已存在同名，跳过）。当前游荡英雄总数={after}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.HeroesResurrectFailed++;
                        AffixLogger.Error("IMPORT", $"[NPC] 复刻英雄异常: {profile.Name} ({profile.Source})", ex);
                    }
                }
                AffixLogger.Info("IMPORT", $"[NPC] 英雄复刻结束: 新建 {result.HeroesResurrected} 个 / 跳过(已存在) {skipped} 个 / 异常 {result.HeroesResurrectFailed} 个");
            }
            else if (settings.RestoreHeroes)
            {
                AffixLogger.Info("IMPORT", "[NPC] 复原英雄已启用，但遗产数据中无英雄模板记录，跳过");
            }
            else
            {
                AffixLogger.Info("IMPORT", "[NPC] 复原英雄未启用（RestoreHeroes=false），跳过英雄复刻");
            }

            AffixLogger.Info("IMPORT", $"导入编排完成: {result.KingdomsRestored} 王国 / {result.ClansRestored} 家族 / {result.SettlementsRestored} 定居点 / {result.HeroesResurrected} 英雄(新建)");
            AffixLogger.Info("IMPORT", "================ 导入结束 ================");
            return result;
        }

        /// <summary>
        /// 统计当前游戏内游荡英雄数量，用于 NPC 复刻前后对比，确认是否真的新建了对象。
        /// </summary>
        private static int CountWanderers(IGameAdapter adapter)
        {
            int n = 0;
            foreach (var h in Hero.AllAliveHeroes)
            {
                if (h != null && h.IsWanderer) n++;
            }
            return n;
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
