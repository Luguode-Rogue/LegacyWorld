using System.IO;
using System.Xml.Serialization;
using LegacyWorld.Core;

namespace LegacyWorld.Core.Settings
{
    /// <summary>
    /// 设置管理层（第2层）。负责 XML 加载/保存、MCM↔Data 同步、手动操作标志。
    /// </summary>
    public static class LegacyWorldSettingsManager
    {
        private static readonly string _xmlPath =
            Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "Settings", "LegacyWorldSettings.xml");

        public static LegacyWorldSettingsData Settings { get; private set; } = new LegacyWorldSettingsData();

        private static bool _manualExportRequested;
        private static bool _manualApplyRequested;

        public static void Load()
        {
            try
            {
                if (File.Exists(_xmlPath))
                {
                    var serializer = new XmlSerializer(typeof(LegacyWorldSettingsData));
                    using var fs = new FileStream(_xmlPath, FileMode.Open);
                    Settings = (LegacyWorldSettingsData)serializer.Deserialize(fs);
                    AffixLogger.Info("SETTINGS", $"已从 XML 加载设置: {_xmlPath}");
                }
                else
                {
                    Settings = new LegacyWorldSettingsData();
                    Save();
                    AffixLogger.Info("SETTINGS", "XML 不存在，创建默认设置");
                }
            }
            catch (System.Exception ex)
            {
                AffixLogger.Error("SETTINGS", "加载设置失败（XML 损坏/缺字段），使用默认值并重建", ex);
                Settings = new LegacyWorldSettingsData();
                // 边界：损坏的 XML 保留为 .bak，避免下次加载再次失败且便于排查。
                try { if (File.Exists(_xmlPath)) File.Copy(_xmlPath, _xmlPath + ".bak", true); } catch { }
                Save();
            }
        }

        public static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_xmlPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var serializer = new XmlSerializer(typeof(LegacyWorldSettingsData));
                using var fs = new FileStream(_xmlPath, FileMode.Create);
                serializer.Serialize(fs, Settings);
            }
            catch (System.Exception ex) { AffixLogger.Error("SETTINGS", "保存设置失败", ex); }
        }

        public static void SyncFromMCM(LegacyWorldMCMSettings mcm)
        {
            if (mcm == null) { AffixLogger.Warn("SETTINGS", "SyncFromMCM: mcm 为空（MCM 未加载），跳过"); return; }
            Settings.Enabled = mcm.Enabled;
            Settings.AutoExportOnSave = mcm.AutoExportOnSave;
            Settings.LogEnabled = mcm.LogEnabled;
            Settings.RespectAdvancedStartOptions = mcm.RespectAdvancedStartOptions;
            Settings.AdvancedStartSkipKingdomRulers = mcm.AdvancedStartSkipKingdomRulers;
            Settings.AdvancedStartSkipClanKingdoms = mcm.AdvancedStartSkipClanKingdoms;
            Settings.AdvancedStartSkipSettlementOwners = mcm.AdvancedStartSkipSettlementOwners;
            Settings.RestoreKingdoms = mcm.RestoreKingdoms;
            Settings.RestoreClans = mcm.RestoreClans;
            Settings.RestoreSettlements = mcm.RestoreSettlements;
            Settings.RestoreClanEconomy = mcm.RestoreClanEconomy;
            Settings.CreateMissingClans = mcm.CreateMissingClans;
            Settings.RestoreHeroes = mcm.RestoreHeroes;
            Save();
            AffixLogger.Info("SETTINGS", "MCM → Data 同步完成");
        }

        public static LegacySettings ToImportSettings() => new LegacySettings
        {
            RestoreKingdoms = Settings.RestoreKingdoms,
            RestoreClans = Settings.RestoreClans,
            RestoreClanKingdomMembership = true,
            RestoreSettlements = Settings.RestoreSettlements,
            RestoreSettlementOwnership = true,
            RestoreClanEconomy = Settings.RestoreClanEconomy,
            CreateMissingClans = Settings.CreateMissingClans,
            RestoreHeroes = Settings.RestoreHeroes
        };

        public static void RefreshSettings()
        {
            AffixLogger.Info("SETTINGS", "刷新导入配置（从 MCM 数据层）");
        }

        // 由 Bannerlord 层（LegacyBehavior）在初始化时注入真正执行逻辑，
        // 使 MCM 按钮点击即可即时执行，无需等待 HourlyTickEvent。
        public static System.Action RunManualExport { get; set; }
        public static System.Action RunManualApply { get; set; }
        public static System.Action RunListResurrected { get; set; }

        public static void RequestManualExport()
        {
            _manualExportRequested = true;
            AffixLogger.Info("SETTINGS", "手动导出请求已注册");
            RunManualExport?.Invoke(); // 立即执行（若有注入）
        }

        public static void RequestManualApply()
        {
            _manualApplyRequested = true;
            AffixLogger.Info("SETTINGS", "手动应用请求已注册");
            RunManualApply?.Invoke(); // 立即执行（若有注入）
        }

        public static void RequestListResurrected()
        {
            AffixLogger.Info("SETTINGS", "列出已复刻英雄（验证）请求已注册");
            RunListResurrected?.Invoke(); // 立即执行（若有注入）
        }

        public static bool TryConsumeManualExport()
        {
            if (!_manualExportRequested) return false;
            _manualExportRequested = false;
            return true;
        }

        public static bool TryConsumeManualApply()
        {
            if (!_manualApplyRequested) return false;
            _manualApplyRequested = false;
            return true;
        }
    }
}
