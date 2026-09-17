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
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Library;

namespace LegacyWorld.Bannerlord
{
    /// <summary>LegacyWorld 静态服务入口。</summary>
    public static class LegacyService
    {
        private static IGameAdapter _adapter;
        private static string _pendingCompatibilityNotice;

        public static void Initialize()
        {
            _adapter = new BannerlordGameAdapter();
            AffixLogger.Info("SERVICE", "LegacyService 初始化完成");
        }

        public static bool Export(bool force = false)
        {
            try
            {
                if (!_adapterIsReady()) return false;
                if (!LegacyWorldSettingsManager.Settings.Enabled) { AffixLogger.Info("SERVICE", "系统未启用，跳过导出"); return false; }
                if (!force && !LegacyWorldSettingsManager.Settings.AutoExportOnSave) { AffixLogger.Info("SERVICE", "自动导出已关闭，跳过"); return false; }

                AffixLogger.Info("SERVICE", "开始导出...");
                var legacyData = LegacyExporter.ExportWorld(_adapter);
                var heroList = LegacyExporter.ExportHeroes(_adapter);
                string json = LegacySerializer.Serialize(legacyData);
                string heroJson = LegacySerializer.SerializeHeroes(heroList);

                if (!LegacyStorage.WriteSnapshot(json, heroJson))
                {
                    AffixLogger.Error("SERVICE", "导出失败：世界状态与人物遗产快照未能完整写入");
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[LegacyWorld] 导出失败：数据文件写入失败，旧快照已尽量保留",
                        Colors.Red));
                    return false;
                }

                AffixLogger.Info("SERVICE", $"导出完成: {legacyData.Kingdoms.Count} 王国, {legacyData.Clans.Count} 家族, {legacyData.Settlements.Count} 定居点 | 玩家人物遗产累计 {heroList.Profiles.Count} 个");
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[LegacyWorld] 世界状态已导出\n{legacyData.Kingdoms.Count} 王国 / {legacyData.Clans.Count} 家族 / {legacyData.Settlements.Count} 定居点\n玩家人物遗产累计 {heroList.Profiles.Count} 个",
                    Colors.Green));
                return true;
            }
            catch (Exception ex)
            {
                AffixLogger.Error("SERVICE", "导出失败", ex);
                return false;
            }
        }

        public static LegacyOperationResult Import()
        {
            try
            {
                if (!_adapterIsReady()) return LegacyOperationResult.Failed("战役或适配器未就绪");
                var (legacyData, heroes) = LoadCombined();
                if (legacyData == null) return LegacyOperationResult.Skipped("没有可用的 Legacy.json");
                string currentWorldId = _adapter.GetWorldId();

                var applied = new HashSet<string>(heroes?.AppliedWorldIds ?? new List<string>());
                var foreignWorlds = CollectForeignWorlds(legacyData, currentWorldId);

                if (foreignWorlds.Count == 0 && legacyData.WorldId == currentWorldId)
                {
                    AffixLogger.Warn("SERVICE", "人物和世界状态都来自当前世界，整体跳过");
                    return LegacyOperationResult.Skipped("遗产数据来自当前世界");
                }

                if (foreignWorlds.Count > 0 && foreignWorlds.Any(w => applied.Contains(w)))
                {
                    AffixLogger.Info("SERVICE", "部分来源世界曾被应用过；自动新游戏导入仍按当前目标世界执行，具体重复由稳定身份和当前世界复刻记录处理");
                }

                return ExecuteImport(legacyData, heroes, foreignWorlds);
            }
            catch (Exception ex)
            {
                AffixLogger.Error("SERVICE", "导入失败", ex);
                return LegacyOperationResult.Failed(ex.Message);
            }
        }

        /// <summary>手动应用。返回真实 Applied / Partial / Skipped / Failed 状态。</summary>
        public static LegacyOperationResult ForceImport()
        {
            try
            {
                if (!_adapterIsReady()) return LegacyOperationResult.Failed("战役或适配器未就绪");
                var (legacyData, heroes) = LoadCombined();
                if (legacyData == null) return LegacyOperationResult.Skipped("找不到 Legacy.json");
                string currentWorldId = _adapter.GetWorldId();
                var applied = new HashSet<string>(heroes?.AppliedWorldIds ?? new List<string>());
                var foreignWorlds = CollectForeignWorlds(legacyData, currentWorldId);

                if (foreignWorlds.Count > 0 && foreignWorlds.Any(w => applied.Contains(w)))
                {
                    AffixLogger.Warn("SERVICE", $"这些遗产来源世界已记录为应用过（{string.Join(",", foreignWorlds)}），手动应用跳过以避免重复创建");
                    return LegacyOperationResult.Skipped("该批遗产来源世界已应用过");
                }
                if (!string.IsNullOrEmpty(legacyData.WorldId) && applied.Contains(legacyData.WorldId))
                {
                    AffixLogger.Warn("SERVICE", $"世界状态来源 {legacyData.WorldId} 已导入过，手动应用跳过");
                    return LegacyOperationResult.Skipped("世界状态来源已应用过");
                }

                return ExecuteImport(legacyData, heroes, foreignWorlds);
            }
            catch (Exception ex)
            {
                AffixLogger.Error("SERVICE", "强制导入失败", ex);
                return LegacyOperationResult.Failed(ex.Message);
            }
        }

        internal static string ConsumePendingCompatibilityNotice()
        {
            string notice = _pendingCompatibilityNotice;
            _pendingCompatibilityNotice = null;
            return notice;
        }

        public static string GetCurrentWorldId() => _adapter?.GetWorldId() ?? "unknown";

        private static HashSet<string> CollectForeignWorlds(LegacyData legacyData, string currentWorldId)
        {
            var worlds = new HashSet<string>();
            if (legacyData?.HeroProfiles == null) return worlds;
            foreach (var profile in legacyData.HeroProfiles)
            {
                if (profile != null && !string.IsNullOrEmpty(profile.WorldId) && profile.WorldId != currentWorldId)
                    worlds.Add(profile.WorldId);
            }
            return worlds;
        }

        private static LegacyOperationResult ExecuteImport(LegacyData legacyData, HeroProfileList heroes, HashSet<string> foreignWorlds)
        {
            string currentWorldId = _adapter.GetWorldId();
            var persistenceHeroes = heroes ?? new HeroProfileList
            {
                Profiles = legacyData.HeroProfiles != null
                    ? new List<HeroProfile>(legacyData.HeroProfiles)
                    : new List<HeroProfile>()
            };
            if (persistenceHeroes.Profiles == null) persistenceHeroes.Profiles = new List<HeroProfile>();
            if (persistenceHeroes.AppliedWorldIds == null) persistenceHeroes.AppliedWorldIds = new List<string>();
            if (persistenceHeroes.ResurrectedHeroes == null) persistenceHeroes.ResurrectedHeroes = new List<ResurrectedHeroRecord>();

            int retrySkipped = SkipAlreadyRestoredHeroesForCurrentWorld(legacyData, persistenceHeroes, currentWorldId);
            if (retrySkipped > 0)
                AffixLogger.Info("SERVICE", $"部分失败重试：当前世界已有 {retrySkipped} 个成功复刻英雄，跳过重复创建");

            ResurrectedHeroTracker.Clear();
            var importSettings = BuildEffectiveImportSettings();
            AffixLogger.Info("SERVICE", $"开始导入：世界状态来源={legacyData.WorldId}, 人物模板={legacyData.HeroProfiles?.Count ?? 0} 个（其它来源世界 {foreignWorlds.Count} 个）");
            ImportResult result = LegacyImporter.Apply(_adapter, legacyData, importSettings);
            AffixLogger.Info("SERVICE",
                $"导入完成: {result.KingdomsRestored} 王国 / {result.ClansRestored} 家族 / {result.SettlementsRestored} 定居点 / {result.HeroesResurrected} 英雄(新建) / {result.HeroesResurrectFailed} 英雄异常");

            bool recordsSaved = PersistResurrectedHeroRecords(persistenceHeroes, currentWorldId);
            if (result.HeroesResurrectFailed > 0)
            {
                AffixLogger.Warn("SERVICE", "导入部分完成：存在英雄复刻异常，不写入 AppliedWorldIds，允许后续重试");
                return LegacyOperationResult.Partial($"导入部分完成：{result.HeroesResurrectFailed} 个英雄复刻失败");
            }
            if (!recordsSaved)
            {
                AffixLogger.Warn("SERVICE", "导入部分完成：复刻记录未能持久化，不写入 AppliedWorldIds");
                return LegacyOperationResult.Partial("导入已执行，但复刻记录持久化失败");
            }

            if (!SaveAppliedWorlds(persistenceHeroes, foreignWorlds, legacyData.WorldId))
                return LegacyOperationResult.Partial("导入已执行，但已应用世界标记写入失败");

            return LegacyOperationResult.Applied("世界遗产已完整应用");
        }

        /// <summary>
        /// 部分失败后再次应用时，已经成功复刻且仍存在于当前目标世界的 LegacyId 不再重复创建。
        /// 只使用带 TargetWorldId 的新记录；旧记录没有目标世界信息，不参与该优化。
        /// </summary>
        private static int SkipAlreadyRestoredHeroesForCurrentWorld(LegacyData legacyData, HeroProfileList heroes, string currentWorldId)
        {
            if (legacyData?.HeroProfiles == null || heroes?.ResurrectedHeroes == null || string.IsNullOrEmpty(currentWorldId))
                return 0;

            var aliveHeroIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero != null && hero.IsAlive && !string.IsNullOrWhiteSpace(hero.StringId))
                    aliveHeroIds.Add(hero.StringId);
            }

            var restoredLegacyIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in heroes.ResurrectedHeroes)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.LegacyId) || string.IsNullOrWhiteSpace(record.HeroStringId)) continue;
                if (!string.Equals(record.TargetWorldId, currentWorldId, StringComparison.Ordinal)) continue;
                if (aliveHeroIds.Contains(record.HeroStringId)) restoredLegacyIds.Add(record.LegacyId);
            }

            if (restoredLegacyIds.Count == 0) return 0;
            int before = legacyData.HeroProfiles.Count;
            legacyData.HeroProfiles = legacyData.HeroProfiles
                .Where(p => p == null || string.IsNullOrWhiteSpace(p.LegacyId) || !restoredLegacyIds.Contains(p.LegacyId))
                .ToList();
            return before - legacyData.HeroProfiles.Count;
        }

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
                if (heroes?.Profiles != null)
                    legacyData.HeroProfiles = new List<HeroProfile>(heroes.Profiles);
            }
            if (legacyData.HeroProfiles == null) legacyData.HeroProfiles = new List<HeroProfile>();
            return (legacyData, heroes);
        }

        private static bool SaveAppliedWorlds(HeroProfileList heroes, HashSet<string> foreignWorlds, string stateWorldId = null)
        {
            try
            {
                if (heroes == null) return false;
                if (heroes.AppliedWorldIds == null) heroes.AppliedWorldIds = new List<string>();
                bool changed = false;
                foreach (var w in foreignWorlds)
                {
                    if (string.IsNullOrEmpty(w)) continue;
                    if (!heroes.AppliedWorldIds.Contains(w)) { heroes.AppliedWorldIds.Add(w); changed = true; }
                }
                if (!string.IsNullOrEmpty(stateWorldId) && !heroes.AppliedWorldIds.Contains(stateWorldId))
                {
                    heroes.AppliedWorldIds.Add(stateWorldId); changed = true;
                }

                if (!changed) return true;
                bool ok = LegacyStorage.WriteHeroes(LegacySerializer.SerializeHeroes(heroes));
                if (ok)
                    AffixLogger.Info("SERVICE", $"已持久化已导入世界: {string.Join(",", heroes.AppliedWorldIds)}");
                else
                    AffixLogger.Error("SERVICE", "已导入世界标记写入失败；下次启动仍会允许重试");
                return ok;
            }
            catch (Exception ex)
            {
                AffixLogger.Error("SERVICE", "持久化已导入世界失败", ex);
                return false;
            }
        }

        private static LegacySettings BuildEffectiveImportSettings()
        {
            LegacyWorldSettingsManager.RefreshSettings();
            var importSettings = LegacyWorldSettingsManager.ToImportSettings();
            var compatibility = LegacyWorldSettingsManager.Settings;

            if (!compatibility.RespectAdvancedStartOptions)
                return importSettings;

            var advancedStart = Campaign.Current?.AdvancedStartData;
            if (advancedStart == null)
                return importSettings;

            string scenario = advancedStart.GetScenario();
            string startType = advancedStart.GetStartType();

            bool hasWorldScenario = !string.IsNullOrEmpty(scenario)
                                    && !string.Equals(scenario, "none", StringComparison.OrdinalIgnoreCase);
            bool hasPoliticalStartType = IsPoliticalAdvancedStartType(startType);

            if (!hasWorldScenario && !hasPoliticalStartType)
                return importSettings;

            var skipped = new List<string>();

            if (compatibility.AdvancedStartSkipKingdomRulers && importSettings.RestoreKingdoms)
            {
                importSettings.RestoreKingdoms = false;
                skipped.Add("王国统治者");
            }

            if (compatibility.AdvancedStartSkipClanKingdoms && importSettings.RestoreClans)
            {
                importSettings.RestoreClanKingdomMembership = false;
                skipped.Add("家族所属王国");
            }

            if (compatibility.AdvancedStartSkipSettlementOwners && importSettings.RestoreSettlements)
            {
                importSettings.RestoreSettlementOwnership = false;
                skipped.Add("领地所有权");
            }

            if (skipped.Count > 0)
            {
                var source = new List<string>();
                if (hasWorldScenario) source.Add($"场景={scenario}");
                if (hasPoliticalStartType) source.Add($"身份={startType}");

                string message =
                    $"检测到高级开局（{string.Join("，", source)}）。\n\n" +
                    "LegacyWorld 将以游戏开局选项为准，本次不会覆盖：\n" +
                    $"• {string.Join("\n• ", skipped)}\n\n" +
                    "其它已启用的非冲突导入项目仍会正常执行。\n" +
                    "可在 MCM「高级开局兼容」中单独调整这些跳过项。";

                _pendingCompatibilityNotice = message;
                AffixLogger.Warn("COMPAT", message.Replace("\n", " | "));
            }

            return importSettings;
        }

        private static bool IsPoliticalAdvancedStartType(string startType)
        {
            if (string.IsNullOrEmpty(startType)) return false;
            return string.Equals(startType, "king", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(startType, "vassal", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(startType, "mercenary", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(startType, "fleetadmiral", StringComparison.OrdinalIgnoreCase);
        }

        private static bool PersistResurrectedHeroRecords(HeroProfileList heroes, string targetWorldId)
        {
            try
            {
                if (heroes == null) return false;
                if (heroes.ResurrectedHeroes == null) heroes.ResurrectedHeroes = new List<ResurrectedHeroRecord>();
                bool changed = false;

                foreach (var e in ResurrectedHeroTracker.Entries)
                {
                    if (e.Status != "成功") continue;

                    ResurrectedHeroRecord existing;
                    if (!string.IsNullOrWhiteSpace(e.LegacyId))
                    {
                        existing = heroes.ResurrectedHeroes.FirstOrDefault(r =>
                            r != null &&
                            string.Equals(r.LegacyId, e.LegacyId, StringComparison.Ordinal) &&
                            string.Equals(r.TargetWorldId, targetWorldId, StringComparison.Ordinal));
                    }
                    else
                    {
                        existing = heroes.ResurrectedHeroes.FirstOrDefault(r =>
                            r != null &&
                            string.IsNullOrWhiteSpace(r.LegacyId) &&
                            r.Name == e.Name &&
                            r.WorldId == e.WorldId &&
                            string.Equals(r.TargetWorldId, targetWorldId, StringComparison.Ordinal));
                    }

                    if (existing == null)
                    {
                        existing = new ResurrectedHeroRecord();
                        heroes.ResurrectedHeroes.Add(existing);
                    }

                    existing.LegacyId = e.LegacyId;
                    existing.HeroStringId = e.HeroStringId;
                    existing.TargetWorldId = targetWorldId;
                    existing.Name = e.Name;
                    existing.Source = e.Source;
                    existing.WorldId = e.WorldId;
                    existing.Level = e.Level;
                    existing.CultureId = e.CultureId;
                    existing.RestoredAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    changed = true;
                }

                if (!changed) return true;
                bool ok = LegacyStorage.WriteHeroes(LegacySerializer.SerializeHeroes(heroes));
                if (!ok) AffixLogger.Warn("SERVICE", "复刻英雄记录写入失败，部分失败重试保护不可用");
                return ok;
            }
            catch (Exception ex)
            {
                AffixLogger.Warn("SERVICE", $"持久化复刻记录失败: {ex.Message}");
                return false;
            }
        }

        private static bool _adapterIsReady()
        {
            if (_adapter == null) { AffixLogger.Error("SERVICE", "适配器未初始化，请先调用 Initialize()"); return false; }
            if (Campaign.Current == null) { AffixLogger.Warn("SERVICE", "当前无进行中的战役，跳过（Campaign 未就绪）"); return false; }
            return true;
        }
    }
}
