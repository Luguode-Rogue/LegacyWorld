using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using LegacyWorld.Bannerlord;
using LegacyWorld.Core.Settings;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem;
using HarmonyLib;

namespace LegacyWorld
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            // 1. 加载/初始化设置（从 XML 或默认值）
            LegacyWorldSettingsManager.Load();

            // 2. 创建游戏适配器（隔离 TaleWorlds API）
            LegacyService.Initialize();

            // 3. 应用 Harmony 兼容性补丁（如 SettlementValueModelNullGuardPatch）。
            //    删除补丁类或此行即可彻底还原，不修改任何原版程序集。
            new Harmony("LegacyWorld.Compatibility").PatchAll();

            Debug.Print("[LegacyWorld] SubModule 已加载");
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            EnsurePopupTestMenuOption();
            Debug.Print("[LegacyWorld] Mod 已启动");
        }

        /// <summary>
        /// 在游戏启动后的原版主菜单加入一个临时测试按钮。
        /// InitialMenuVM 会直接读取 Module.CurrentModule.GetInitialStateOptions() 生成主菜单按钮。
        /// </summary>
        private static void EnsurePopupTestMenuOption()
        {
            const string optionId = "LegacyWorld.PopupTest";
            if (Module.CurrentModule.GetInitialStateOptionWithId(optionId) != null)
                return;

            Module.CurrentModule.AddInitialStateOption(
                new InitialStateOption(
                    optionId,
                    new TextObject("LegacyWorld 弹窗测试"),
                    9900,
                    ShowPopupTest,
                    () => new System.ValueTuple<bool, TextObject>(false, TextObject.GetEmpty()),
                    new TextObject("测试 LegacyWorld 使用原版 InquiryData 弹窗")));
        }

        private static void ShowPopupTest()
        {
            const string message =
                "这是 LegacyWorld 的原版弹窗测试。\n\n" +
                "如果你能看到这个窗口，说明 InformationManager.ShowInquiry + InquiryData 在游戏主菜单环境下工作正常。\n\n" +
                "之后可以继续排查为什么新游戏自动兼容提示没有显示。";

            InformationManager.ShowInquiry(
                new InquiryData(
                    "LegacyWorld - 弹窗测试",
                    message,
                    true,
                    false,
                    "确定",
                    string.Empty,
                    null,
                    null),
                pauseGameActiveState: false,
                prioritize: true);
        }

        protected override void InitializeGameStarter(Game game, IGameStarter starter)
        {
            if (game.GameType is Campaign)
            {
                ((CampaignGameStarter)starter).AddBehavior(new LegacyBehavior());
                Debug.Print("[LegacyWorld] LegacyBehavior 已注册");
            }
        }

        protected override void OnGameStart(Game game, IGameStarter starter)
        {
            base.OnGameStart(game, starter);
        }
    }
}
