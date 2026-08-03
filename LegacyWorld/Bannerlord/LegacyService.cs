using System;
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

                AffixLogger.Info("SERVICE", "开始导出世界状态...");
                var legacyData = LegacyExporter.Export(_adapter);
                string json = LegacySerializer.Serialize(legacyData);
                LegacyStorage.Write(json);
                AffixLogger.Info("SERVICE", $"导出完成: {legacyData.Kingdoms.Count} 王国, {legacyData.Clans.Count} 家族, {legacyData.Settlements.Count} 定居点");
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[LegacyWorld] 世界状态已导出\n{legacyData.Kingdoms.Count} 王国 / {legacyData.Clans.Count} 家族 / {legacyData.Settlements.Count} 定居点",
                    Colors.Green));
            }
            catch (Exception ex) { AffixLogger.Error("SERVICE", "导出失败", ex); }
        }

        public static bool Import()
        {
            try
            {
                if (!_adapterIsReady()) return false;
                var legacyData = LoadLegacy();
                if (legacyData == null) return false;
                string currentWorldId = _adapter.GetWorldId();
                if (currentWorldId == legacyData.WorldId)
                {
                    AffixLogger.Warn("SERVICE", $"同世界遗产（当前世界={currentWorldId}），跳过导入");
                    return false;
                }
                return ApplyImport(legacyData);
            }
            catch (Exception ex) { AffixLogger.Error("SERVICE", "导入失败", ex); return false; }
        }

        public static void ForceImport()
        {
            try
            {
                if (!_adapterIsReady()) return;
                var legacyData = LoadLegacy();
                if (legacyData == null) { AffixLogger.Warn("SERVICE", "强制导入：找不到 Legacy.json"); return; }
                ApplyImport(legacyData);
            }
            catch (Exception ex) { AffixLogger.Error("SERVICE", "强制导入失败", ex); }
        }

        public static string GetCurrentWorldId() => _adapter?.GetWorldId() ?? "unknown";

        private static LegacyData LoadLegacy()
        {
            string json = LegacyStorage.Read();
            if (string.IsNullOrEmpty(json)) { AffixLogger.Warn("SERVICE", "找不到 Legacy.json，跳过"); return null; }
            var legacyData = LegacySerializer.Deserialize(json);
            if (legacyData == null) { AffixLogger.Error("SERVICE", "Legacy.json 解析失败"); return null; }
            return legacyData;
        }

        private static bool ApplyImport(LegacyData legacyData)
        {
            LegacyWorldSettingsManager.RefreshSettings();
            AffixLogger.Info("SERVICE", $"开始导入：来源世界={legacyData.WorldId}, 版本={legacyData.Version}");
            var result = LegacyImporter.Apply(_adapter, legacyData, LegacyWorldSettingsManager.ToImportSettings());
            AffixLogger.Info("SERVICE",
                $"导入完成: {result.KingdomsRestored} 王国 / {result.ClansRestored} 家族 / {result.SettlementsRestored} 定居点");
            return true;
        }

        private static bool _adapterIsReady()
        {
            if (_adapter == null) { AffixLogger.Error("SERVICE", "适配器未初始化，请先调用 Initialize()"); return false; }
            return true;
        }
    }
}
