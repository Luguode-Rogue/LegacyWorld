using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;
using LegacyWorld.Core;
using LegacyWorld.Core.Models;
using LegacyWorld.Core.Settings;
using TaleWorlds.Library;

namespace LegacyWorld.Bannerlord
{
    /// <summary>CampaignBehavior 集成层：保存导出、新游戏导入、MCM 手动操作与验证。</summary>
    public class LegacyBehavior : CampaignBehaviorBase
    {
        private bool _applied = false;
        private string _appliedWorldId = null;

        public override void RegisterEvents()
        {
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, OnBeforeSave);
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnCampaignTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnTick);

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
            if (Campaign.Current == null) return;
            try { LegacyService.Export(); }
            catch (Exception ex) { AffixLogger.Error("BEHAVIOR", $"OnBeforeSave 导出异常: {ex.Message}", ex); }
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            try
            {
                if (!LegacyWorldSettingsManager.Settings.Enabled)
                {
                    AffixLogger.Info("BEHAVIOR", "系统未启用，跳过新游戏导入");
                    return;
                }

                LegacyOperationResult result = LegacyService.Import();
                _applied = result.FullyApplied;
                _appliedWorldId = result.FullyApplied ? LegacyService.GetCurrentWorldId() : null;
                AffixLogger.Info("BEHAVIOR", $"新游戏导入结果: {result.Status} | {result.Message}");

                if (result.Status == LegacyOperationStatus.Partial)
                    InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] 遗产部分应用：{result.Message}", Colors.Yellow));
                else if (result.Status == LegacyOperationStatus.Failed)
                    InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] 遗产应用失败：{result.Message}", Colors.Red));
            }
            catch (Exception ex) { AffixLogger.Error("BEHAVIOR", $"OnNewGameCreated 导入异常: {ex.Message}", ex); }
        }

        private void DoManualExport()
        {
            if (Campaign.Current == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[LegacyWorld] 需在游戏中执行导出", Colors.Yellow));
                return;
            }
            try
            {
                if (!LegacyService.Export(force: true))
                    InformationManager.DisplayMessage(new InformationMessage("[LegacyWorld] 手动导出未完成，请查看 LegacyWorld.log", Colors.Yellow));
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
                LegacyOperationResult result = LegacyService.ForceImport();
                switch (result.Status)
                {
                    case LegacyOperationStatus.Applied:
                        InformationManager.DisplayMessage(new InformationMessage("[LegacyWorld] 已手动应用世界遗产", Colors.Green));
                        break;
                    case LegacyOperationStatus.Partial:
                        InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] 部分应用：{result.Message}", Colors.Yellow));
                        break;
                    case LegacyOperationStatus.Skipped:
                        InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] 未应用：{result.Message}", Colors.Yellow));
                        break;
                    default:
                        InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] 应用失败：{result.Message}", Colors.Red));
                        break;
                }
            }
            catch (Exception ex)
            {
                AffixLogger.Error("BEHAVIOR", "手动应用异常", ex);
                InformationManager.DisplayMessage(new InformationMessage($"[LegacyWorld] 手动应用失败: {ex.Message}", Colors.Red));
            }
        }

        private void DoListResurrected()
        {
            if (Campaign.Current == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[LegacyWorld] 需在游戏中执行", Colors.Yellow));
                return;
            }
            var entries = ResurrectedHeroTracker.Entries;
            if (entries.Count == 0)
            {
                if (TryListResurrectedFromDisk()) return;
                InformationManager.DisplayMessage(new InformationMessage("[LegacyWorld] 当前没有已复刻的英雄记录。", Colors.Yellow));
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
                    status = e.Status;
                }
                else
                {
                    var hero = FindWanderer(e.HeroStringId, e.Name);
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
            InformationManager.DisplayMessage(new InformationMessage(sb.ToString(), Colors.Green));
        }

        private Hero FindWanderer(string heroStringId, string name)
        {
            foreach (var h in Hero.AllAliveHeroes)
            {
                if (h == null || !h.IsActive || !h.IsWanderer) continue;
                if (!string.IsNullOrWhiteSpace(heroStringId) && string.Equals(h.StringId, heroStringId, StringComparison.Ordinal))
                    return h;
            }

            if (string.IsNullOrEmpty(name)) return null;
            foreach (var h in Hero.AllAliveHeroes)
            {
                if (h == null || !h.IsActive || !h.IsWanderer) continue;
                if (h.Name != null && h.Name.ToString() == name) return h;
            }
            return null;
        }

        private bool TryListResurrectedFromDisk()
        {
            try
            {
                var json = LegacyWorld.Core.Storage.LegacyStorage.ReadHeroes();
                if (string.IsNullOrEmpty(json)) return false;
                var heroes = LegacyWorld.Core.Serialization.LegacySerializer.DeserializeHeroes(json);
                var allRecords = heroes?.ResurrectedHeroes;
                if (allRecords == null || allRecords.Count == 0) return false;

                string currentWorldId = LegacyService.GetCurrentWorldId();
                var records = allRecords
                    .Where(r => r != null && (string.IsNullOrEmpty(r.TargetWorldId) || r.TargetWorldId == currentWorldId))
                    .ToList();
                if (records.Count == 0) return false;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[LegacyWorld] 当前世界共有 {records.Count} 条已复刻记录：");
                int aliveNow = 0;
                foreach (var r in records)
                {
                    var h = FindWanderer(r.HeroStringId, r.Name);
                    bool exists = h != null;
                    bool isAlive = exists && h.IsAlive;
                    if (isAlive) aliveNow++;
                    string status = !exists ? "对象不在当前世界" : (isAlive ? "存活中" : "已死亡");
                    string line = $"  • {r.Name}（{r.Source}, Lv{r.Level}, 文化={r.CultureId}）→ {status} [导入于 {r.RestoredAt}]";
                    sb.AppendLine(line);
                    AffixLogger.Info("VERIFY", line);
                }
                sb.AppendLine($"仍存活于当前世界：{aliveNow}/{records.Count}");
                InformationManager.DisplayMessage(new InformationMessage(sb.ToString(), Colors.Green));
                return true;
            }
            catch (Exception ex)
            {
                AffixLogger.Warn("BEHAVIOR", $"读取持久化复刻记录失败: {ex.Message}");
                return false;
            }
        }

        private void OnCampaignTick(float dt)
        {
            if (!(GameStateManager.Current?.ActiveState is MapState)) return;
            string notice = LegacyService.ConsumePendingCompatibilityNotice();
            if (string.IsNullOrEmpty(notice)) return;

            InformationManager.ShowInquiry(
                new InquiryData(
                    "LegacyWorld - 高级开局兼容",
                    notice,
                    true,
                    false,
                    "确定",
                    string.Empty,
                    null,
                    null),
                pauseGameActiveState: false,
                prioritize: true);
        }

        private void OnTick()
        {
            if (LegacyWorldSettingsManager.RunManualExport == null && LegacyWorldSettingsManager.TryConsumeManualExport())
                DoManualExport();
            if (LegacyWorldSettingsManager.RunManualApply == null && LegacyWorldSettingsManager.TryConsumeManualApply())
                DoManualApply();
        }
    }
}
