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
            Debug.Print("[LegacyWorld] Mod 已启动");
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
