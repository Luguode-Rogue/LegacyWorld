using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using LegacyWorld.Core;
using LegacyWorld.Core.Models;
using LegacyWorld.Core.Settings;
using TaleWorlds.Library;

namespace LegacyWorld.Bannerlord
{
    /// <summary>
    /// LegacyWorld 的 CampaignBehavior 集成层。
    /// 负责挂钩存档/新游戏/小时事件，但不包含业务逻辑（委托给 LegacyService）。
    /// </summary>
    public class LegacyBehavior : CampaignBehaviorBase
    {
        private bool _applied = false;          // 当前存档是否已导入
        private string _appliedWorldId = null;  // 已应用的世界 ID（备份用）

        public override void RegisterEvents()
        {
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, OnBeforeSave);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnTick);

            // 注入手动按钮的执行体，使 MCM 点击即时生效（无需等待 HourlyTick）
            LegacyWorldSettingsManager.RunManualExport = DoManualExport;
            LegacyWorldSettingsManager.RunManualApply = DoManualApply;
            LegacyWorldSettingsManager.RunListResurrected = DoListResurrected;
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_applied", ref _applied);
            dataStore.SyncData("_appliedWorldId", ref _appliedWorldId);
        }

        private void OnBeforeSave()
        {
            // 仅在有活动战役时导出，避免主菜单保存导致空世界文件
            if (Campaign.Current == null) return;
            try { LegacyService.Export(); }
            catch (Exception ex) { AffixLogger.Error("BEHAVIOR", $"OnBeforeSave 导出异常: {ex.Message}", ex); }
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            try
            {
                if (LegacyWorldSettingsManager.Settings.Enabled)
                {
                    LegacyService.Import();
                    _applied = true;
                    _appliedWorldId = LegacyService.GetCurrentWorldId();
                    AffixLogger.Info("BEHAVIOR", $"新游戏自动导入完成，标记 _applied=true (world={_appliedWorldId})");
                }
                else
                {
                    AffixLogger.Info("BEHAVIOR", "系统未启用，跳过新游戏导入");
                }
            }
            catch (Exception ex) { AffixLogger.Error("BEHAVIOR", $"OnNewGameCreated 导入异常: {ex.Message}", ex); }
        }

        // 手动按钮即时执行体（注入到 SettingsManager）
        private void DoManualExport()
        {
            if (Campaign.Current == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[LegacyWorld] 需在游戏中执行导出", Colors.Yellow));
                return;
            }
            try
            {
                LegacyService.Export(force: true);
                InformationManager.DisplayMessage(new InformationMessage("[LegacyWorld] 已手动导出世界状态", Colors.Green));
            }
            catch (Exception ex)
            {
                AffixLogger.Error("BEHAVIOR", "手动导出异常", ex);
                InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] 手动导出失败: {ex.Message}", Colors.Red));
            }
        }

        private void DoManualApply()
        {
            if (Campaign.Current == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[LegacyWorld] 需在游戏中执行应用", Colors.Yellow));
                return;
            }
            try
            {
                LegacyService.ForceImport();
                InformationManager.DisplayMessage(new InformationMessage("[LegacyWorld] 已手动应用世界遗产", Colors.Green));
            }
            catch (Exception ex)
            {
                AffixLogger.Error("BEHAVIOR", "手动应用异常", ex);
                InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] 手动应用失败: {ex.Message}", Colors.Red));
            }
        }

        // 验证按钮：列出本模组复刻到当前世界的英雄（控制台级快速核查）
        private void DoListResurrected()
        {
            if (Campaign.Current == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[LegacyWorld] 需在游戏中执行", Colors.Yellow));
                return;
            }
            var entries = LegacyWorld.Core.Models.ResurrectedHeroTracker.Entries;
            if (entries.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("[LegacyWorld] 当前没有已复刻的英雄记录。可先「手动应用世界状态」或开新档。", Colors.Yellow));
                AffixLogger.Info("BEHAVIOR", "验证：复刻英雄表为空");
                return;
            }

            int alive = 0;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[LegacyWorld] 已复刻英雄 {entries.Count} 名：");
            foreach (var e in entries)
            {
                string status;
                if (e.Status != "成功")
                {
                    status = e.Status; // 复刻失败原因
                }
                else
                {
                    // 不依赖可能随存档失效的 HeroStringId，改用姓名在当前游戏的 Hero.All 中查找游荡英雄。
                    var hero = FindWandererByName(e.Name);
                    bool exists = hero != null;
                    bool isAlive = exists && hero.IsAlive;
                    bool isWanderer = exists && hero.IsWanderer;
                    if (isAlive) alive++;
                    status = !exists ? "对象缺失(可能已死亡或不在当前世界)" : (!isAlive ? "已死亡" : (isWanderer ? "游荡中" : $"职业={hero.Occupation}"));
                }
                string line = $"  • {e.Name}（{e.Source}, Lv{e.Level}, 文化={e.CultureId}）→ {status}";
                sb.AppendLine(line);
                AffixLogger.Info("VERIFY", line);
            }
            sb.AppendLine($"成功存活/游荡中：{alive}/{entries.Count}");
            InformationManager.DisplayMessage(new InformationMessage(sb.ToString(), TaleWorlds.Library.Colors.Green));
            AffixLogger.Info("BEHAVIOR", $"验证：已复刻英雄 {entries.Count} 名，成功且存活 {alive} 名");
        }

        /// <summary>
        /// 在当前游戏中按姓名查找复刻出的游荡英雄。
        /// 使用 Hero.AllAliveHeroes 而非可能随存读档失效的 StringId，避免误报“对象缺失”。
        /// </summary>
        private TaleWorlds.CampaignSystem.Hero FindWandererByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var h in TaleWorlds.CampaignSystem.Hero.AllAliveHeroes)
            {
                if (h == null || !h.IsActive) continue;
                if (h.IsWanderer && h.Name != null && h.Name.ToString() == name) return h;
            }
            return null;
        }

        // 兜底：若 runner 未被注入（极少见），仍由 HourlyTick 消费标志执行
        private void OnTick()
        {
            if (LegacyWorldSettingsManager.RunManualExport == null && LegacyWorldSettingsManager.TryConsumeManualExport())
            {
                DoManualExport();
            }
            if (LegacyWorldSettingsManager.RunManualApply == null && LegacyWorldSettingsManager.TryConsumeManualApply())
            {
                DoManualApply();
            }
        }
    }
}
