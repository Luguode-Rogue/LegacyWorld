using System;
using System.Collections.Generic;
using System.Linq;
using LegacyWorld.Adapter;
using LegacyWorld.BannerlordAdapter;
using LegacyWorld.Core;
using LegacyWorld.Core.Export;
using LegacyWorld.Core.Import;
using LegacyWorld.Core.Models;
using LegacyWorld.Core.Serialization;
using LegacyWorld.Core.Settings;
using LegacyWorld.Core.Storage;
using TaleWorlds.Library;

namespace LegacyWorld.Bannerlord
{
    /// <summary>
    /// LegacyWorld 静态服务入口。
    /// 聚合适配器、导出器和导入器，对 Bannerlord 层提供统一 API。
    /// </summary>
    public static class LegacyService
    {
        private static IGameAdapter _adapter;

        // 已复刻过的遗产世界持久化在 LegacyHeroes.json 的 AppliedWorldIds 中（跨进程生效），
        // 不再使用纯内存集合，避免重开游戏后重复添加遗留 NPC。

        public static void Initialize()
        {
            _adapter = new BannerlordGameAdapter();
            AffixLogger.Info("SERVICE", "LegacyService 初始化完成");
        }

        public static void Export(bool force = false)
        {
            try
            {
                if (!_adapterIsReady()) return;
                if (!LegacyWorldSettingsManager.Settings.Enabled) { AffixLogger.Info("SERVICE", "系统未启用，跳过导出"); return; }
                if (!force && !LegacyWorldSettingsManager.Settings.AutoExportOnSave) { AffixLogger.Info("SERVICE", "自动导出已关闭，跳过"); return; }

                AffixLogger.Info("SERVICE", "开始导出...");
                // 世界状态覆盖写 Legacy.json
                var legacyData = LegacyExporter.ExportWorld(_adapter);
                string json = LegacySerializer.Serialize(legacyData);
                LegacyStorage.Write(json);
                // 玩家人物遗产累积写 LegacyHeroes.json
                var heroList = LegacyExporter.ExportHeroes(_adapter);
                string heroJson = LegacySerializer.SerializeHeroes(heroList);
                LegacyStorage.WriteHeroes(heroJson);
                AffixLogger.Info("SERVICE", $"导出完成: {legacyData.Kingdoms.Count} 王国, {legacyData.Clans.Count} 家族, {legacyData.Settlements.Count} 定居点 | 玩家人物遗产累计 {heroList.Profiles.Count} 个");
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[LegacyWorld] 世界状态已导出\n{legacyData.Kingdoms.Count} 王国 / {legacyData.Clans.Count} 家族 / {legacyData.Settlements.Count} 定居点\n玩家人物遗产累计 {heroList.Profiles.Count} 个",
                    Colors.Green));
            }
            catch (Exception ex) { AffixLogger.Error("SERVICE", "导出失败", ex); }
        }

        public static bool Import()
        {
            try
            {
                if (!_adapterIsReady()) return false;
                var (legacyData, heroes) = LoadCombined();
                if (legacyData == null) return false;
                string currentWorldId = _adapter.GetWorldId();

                var applied = new HashSet<string>(heroes?.AppliedWorldIds ?? new List<string>());

                // 取英雄遗产中"非当前世界"的来源世界集合，作为去重依据
                var foreignWorlds = new HashSet<string>();
                if (heroes != null)
                    foreach (var h in heroes.Profiles)
                        if (h != null && h.WorldId != currentWorldId) foreignWorlds.Add(h.WorldId);

                if (foreignWorlds.Count == 0)
                {
                    AffixLogger.Warn("SERVICE", $"遗产中无其它世界的玩家人物（当前世界={currentWorldId}），跳过英雄导入");
                    // 仍允许世界状态恢复（若世界状态来自其它世界）
                    if (legacyData.WorldId == currentWorldId)
                    {
                        AffixLogger.Warn("SERVICE", "世界状态也来自当前世界，整体跳过");
                        return false;
                    }
                }
                else
                {
                    // 若该批来源世界已复刻过（持久化记录），则跳过英雄复刻以避免重复
                    bool alreadyApplied = foreignWorlds.Any(w => applied.Contains(w));
                    if (alreadyApplied)
                    {
                        AffixLogger.Warn("SERVICE", $"这些遗产世界已导入过（{string.Join(",", foreignWorlds)}），跳过以避免重复复刻");
                        if (legacyData.WorldId == currentWorldId) return false;
                    }
                }

                bool ok = ApplyImport(legacyData, heroes, foreignWorlds);
                if (ok) SaveAppliedWorlds(heroes, foreignWorlds);
                return ok;
            }
            catch (Exception ex) { AffixLogger.Error("SERVICE", "导入失败", ex); return false; }
        }

        public static void ForceImport()
        {
            try
            {
                if (!_adapterIsReady()) return;
                var (legacyData, heroes) = LoadCombined();
                if (legacyData == null) { AffixLogger.Warn("SERVICE", "强制导入：找不到 Legacy.json"); return; }
                string currentWorldId = _adapter.GetWorldId();
                var applied = new HashSet<string>(heroes?.AppliedWorldIds ?? new List<string>());
                var foreignWorlds = new HashSet<string>();
                if (heroes != null)
                    foreach (var h in heroes.Profiles)
                        if (h != null && h.WorldId != currentWorldId) foreignWorlds.Add(h.WorldId);

                if (foreignWorlds.Count > 0 && foreignWorlds.Any(w => applied.Contains(w)))
                {
                    AffixLogger.Warn("SERVICE", $"这些遗产世界已导入过（{string.Join(",", foreignWorlds)}），强制导入跳过以避免重复复刻");
                    return;
                }
                if (ApplyImport(legacyData, heroes, foreignWorlds))
                    SaveAppliedWorlds(heroes, foreignWorlds);
            }
            catch (Exception ex) { AffixLogger.Error("SERVICE", "强制导入失败", ex); }
        }

        public static string GetCurrentWorldId() => _adapter?.GetWorldId() ?? "unknown";

        /// <summary>
        /// 读取 Legacy.json（世界状态）与 LegacyHeroes.json（玩家人物遗产），
        /// 将玩家人物遗产合并进 LegacyData.HeroProfiles 供导入使用。
        /// </summary>
        private static (LegacyData, HeroProfileList) LoadCombined()
        {
            string json = LegacyStorage.Read();
            if (string.IsNullOrEmpty(json)) { AffixLogger.Warn("SERVICE", "找不到 Legacy.json，跳过"); return (null, null); }
            var legacyData = LegacySerializer.Deserialize(json);
            if (legacyData == null) { AffixLogger.Error("SERVICE", "Legacy.json 解析失败"); return (null, null); }

            HeroProfileList heroes = null;
            string heroJson = LegacyStorage.ReadHeroes();
            if (!string.IsNullOrEmpty(heroJson))
            {
                heroes = LegacySerializer.DeserializeHeroes(heroJson);
                if (heroes != null && heroes.Profiles != null)
                {
                    legacyData.HeroProfiles = new List<HeroProfile>(heroes.Profiles);
                }
            }
            return (legacyData, heroes);
        }

        /// <summary>
        /// 把本次成功导入的遗产来源世界持久化进 LegacyHeroes.json 的 AppliedWorldIds，
        /// 跨进程生效，避免重开游戏后再次导入时重复复刻同一世界的遗留 NPC。
        /// </summary>
        private static void SaveAppliedWorlds(HeroProfileList heroes, HashSet<string> foreignWorlds)
        {
            try
            {
                if (heroes == null) return;
                if (heroes.AppliedWorldIds == null) heroes.AppliedWorldIds = new List<string>();
                bool changed = false;
                foreach (var w in foreignWorlds)
                {
                    if (!heroes.AppliedWorldIds.Contains(w)) { heroes.AppliedWorldIds.Add(w); changed = true; }
                }
                if (changed)
                {
                    LegacyStorage.WriteHeroes(LegacySerializer.SerializeHeroes(heroes));
                    AffixLogger.Info("SERVICE", $"已持久化已导入世界: {string.Join(",", heroes.AppliedWorldIds)}");
                }
            }
            catch (Exception ex) { AffixLogger.Error("SERVICE", "持久化已导入世界失败", ex); }
        }

        private static bool ApplyImport(LegacyData legacyData, HeroProfileList heroes, HashSet<string> foreignWorlds)
        {
            ResurrectedHeroTracker.Clear();
            LegacyWorldSettingsManager.RefreshSettings();
            AffixLogger.Info("SERVICE", $"开始导入：世界状态来源={legacyData.WorldId}, 玩家人物遗产={heroes?.Profiles.Count ?? 0} 个（含其它世界 {foreignWorlds.Count} 个）");
            var result = LegacyImporter.Apply(_adapter, legacyData, LegacyWorldSettingsManager.ToImportSettings());
            AffixLogger.Info("SERVICE",
                $"导入完成: {result.KingdomsRestored} 王国 / {result.ClansRestored} 家族 / {result.SettlementsRestored} 定居点 / {result.HeroesResurrected} 英雄(新建)");
            return true;
        }

        private static bool _adapterIsReady()
        {
            if (_adapter == null) { AffixLogger.Error("SERVICE", "适配器未初始化，请先调用 Initialize()"); return false; }
            return true;
        }
    }
}
