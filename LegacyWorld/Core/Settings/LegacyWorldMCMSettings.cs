using System.ComponentModel;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using LegacyWorld.Core.Settings;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using System;

namespace LegacyWorld.Core.Settings
{
    /// <summary>
    /// MCM 设置模型（第3层）。继承 AttributeGlobalSettings，提供 MCM 面板。
    /// FolderName 默认 "LegacyWorld"（与本 Mod 同组）。
    /// </summary>
    public class LegacyWorldMCMSettings : AttributeGlobalSettings<LegacyWorldMCMSettings>
    {
        public override string Id => "LegacyWorld_v4";
        public override string FolderName => "LegacyWorld";
        public override string DisplayName => "LegacyWorld";
        public override string FormatType => "xml";

        [SettingPropertyBool("启用世界状态继承系统", Order = 0, RequireRestart = false, IsToggle = true,
            HintText = "总开关。关闭后本模组完全不读写世界状态（同随机新开局）。")]
        [SettingPropertyGroup("基础控制", GroupOrder = 0)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyBool("存档时自动导出", Order = 1, RequireRestart = false,
            HintText = "保存游戏时自动把当前世界状态写入 Legacy.json。关闭后仅手动导出按钮可用。")]
        [SettingPropertyGroup("基础控制", GroupOrder = 0)]
        public bool AutoExportOnSave { get; set; } = true;

        [SettingPropertyBool("启用调试日志", Order = 2, RequireRestart = false,
            HintText = "在游戏日志中输出本模组的导出/导入/同步细节，排查问题时开启。")]
        [SettingPropertyGroup("基础控制", GroupOrder = 0)]
        public bool LogEnabled { get; set; } = true;

        [SettingPropertyBool("恢复王国结构", Order = 0, RequireRestart = false,
            HintText = "新游戏开局时继承王国的统治者与政治格局。")]
        [SettingPropertyGroup("导入数据类别", GroupOrder = 1)]
        public bool RestoreKingdoms { get; set; } = true;

        [SettingPropertyBool("恢复家族数据", Order = 1, RequireRestart = false,
            HintText = "继承家族的所属王国、等级与声望。")]
        [SettingPropertyGroup("导入数据类别", GroupOrder = 1)]
        public bool RestoreClans { get; set; } = true;

        [SettingPropertyBool("恢复领地所有权", Order = 2, RequireRestart = false,
            HintText = "继承城镇 / 城堡 / 村庄的归属关系。")]
        [SettingPropertyGroup("导入数据类别", GroupOrder = 1)]
        public bool RestoreSettlements { get; set; } = true;

        [SettingPropertyBool("恢复家族经济", Order = 3, RequireRestart = false,
            HintText = "继承家族的金币与影响力。")]
        [SettingPropertyGroup("导入数据类别", GroupOrder = 1)]
        public bool RestoreClanEconomy { get; set; } = true;

        [SettingPropertyBool("创建缺失家族", Order = 4, RequireRestart = false,
            HintText = "若继承数据中的家族在游戏中已不存在，则重新创建它（而非跳过）。")]
        [SettingPropertyGroup("导入数据类别", GroupOrder = 1)]
        public bool CreateMissingClans { get; set; } = false;

        [SettingPropertyBool("复刻英雄模板", Order = 5, RequireRestart = false,
            HintText = "新游戏开局时，把上一份存档中的玩家本体与招募过且存活的 NPC 以游荡英雄形式复刻出来（遇到原来的自己/伙伴）。")]
        [SettingPropertyGroup("导入数据类别", GroupOrder = 1)]
        public bool RestoreHeroes { get; set; } = false;

        // 真正的行为按钮：参照 MCM 指南第四章 [SettingPropertyButton] + Action。
        // MCMv5 要求按钮属性必须有 setter，故绑定到只读委托字段（setter 直接忽略传入值）。
        [SettingPropertyButton("手动导出世界状态", Content = "立即导出", Order = 0,
            HintText = "将当前世界状态写入 Legacy.json（无视「存档时自动导出」开关）。")]
        [SettingPropertyGroup("操作", GroupOrder = 2)]
        public Action ManualExportButton
        {
            get => _manualExportAction;
            set { }
        }

        [SettingPropertyButton("手动应用世界状态", Content = "立即应用", Order = 1,
            HintText = "将已导出的 Legacy.json 应用到当前新游戏（跳过同世界检测）。")]
        [SettingPropertyGroup("操作", GroupOrder = 2)]
        public Action ManualApplyButton
        {
            get => _manualApplyAction;
            set { }
        }

        [SettingPropertyButton("列出已复刻英雄（验证）", Content = "查看", Order = 2,
            HintText = "在游戏信息栏与日志中列出本模组复刻到当前世界的英雄及其存活/游荡状态，用于快速验证功能。")]
        [SettingPropertyGroup("操作", GroupOrder = 2)]
        public Action ListResurrectedButton
        {
            get => _listResurrectedAction;
            set { }
        }

        private readonly Action _manualExportAction = () => LegacyWorldSettingsManager.RequestManualExport();
        private readonly Action _manualApplyAction = () => LegacyWorldSettingsManager.RequestManualApply();
        private readonly Action _listResurrectedAction = () => LegacyWorldSettingsManager.RequestListResurrected();

        public LegacyWorldMCMSettings()
        {
            var data = LegacyWorldSettingsManager.Settings;
            Enabled = data.Enabled;
            AutoExportOnSave = data.AutoExportOnSave;
            LogEnabled = data.LogEnabled;
            RestoreKingdoms = data.RestoreKingdoms;
            RestoreClans = data.RestoreClans;
            RestoreSettlements = data.RestoreSettlements;
            RestoreClanEconomy = data.RestoreClanEconomy;
            CreateMissingClans = data.CreateMissingClans;
            RestoreHeroes = data.RestoreHeroes;
        }

        public override void OnPropertyChanged(string propertyName)
        {
            base.OnPropertyChanged(propertyName);
            // 同步业务设置到数据层并持久化（手动按钮走 Action，不经过此处）
            LegacyWorldSettingsManager.SyncFromMCM(this);
        }
    }
}
