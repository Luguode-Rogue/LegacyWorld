# LegacyWorld

LegacyWorld 是一个《Mount & Blade II: Bannerlord》单人战役 Mod，用于把上一局的部分世界状态和人物遗产带入新战役。

## 功能概览

- 保存并恢复王国统治者、家族所属关系与部分家族经济数据。
- 保存并恢复城镇/城堡归属与繁荣度；旧玩家领地无法安全映射时可转为叛军。
- 累积保存玩家本体与存活同伴的人物模板，在新世界中复刻为可遇见、可招募的游荡英雄。
- 玩家本体额外继承战斗装备、便装、物品词缀、外观物品以及玩家自锻武器配方。
- 复刻玩家的招募价格直接使用 Bannerlord 原版模型，因此恢复后的战斗装备和便装价值会自然计入招募费。
- 支持 Bannerlord 高级开局兼容：默认保留高级开局生成的政治/领地结构，只恢复不冲突的数据。
- 提供 MCM 开关、手动导出、手动应用和复刻英雄验证功能。

## 数据与可靠性

运行时数据写在 Mod 模块根目录：

- `Legacy.json`：当前最近一次导出的世界状态，覆盖写。
- `LegacyHeroes.json`：跨世界累积的人物遗产、已应用世界以及复刻记录。
- `LegacyWorld.log`：调试日志。

`Legacy.json` 与 `LegacyHeroes.json` 是运行时生成数据，不进入版本库。正常导出时两者作为同一快照提交；导入结果区分完整应用、部分应用、跳过和失败，部分失败不会直接标记为完整应用，并可利用 `LegacyId + TargetWorldId + HeroStringId` 避免重试时重复创建已经成功的英雄。

## 代码结构

- `Core/`：数据模型、导入/导出、序列化、存储和设置。
- `Adapter/`：与游戏实现解耦的接口。
- `BannerlordAdapter/`：TaleWorlds API 适配、英雄复刻、装备迁移、状态变更和 Harmony 接线。
- `Bannerlord/`：战役生命周期入口与服务编排。
- `Compatibility/`：针对 Bannerlord 特殊开局/原版边界的兼容补丁。

更详细的实现、数据结构、兼容策略和测试清单见 [`LegacyWorld_Features.md`](LegacyWorld_Features.md)。

## 当前已知限制

- `CreateMissingClans` 目前仍是预留/未完整实现能力，缺失家族不会真正重建。
- 家族 Tier 当前会被导出，但尚未执行恢复。
- 自锻武器持久化需要反射调用 Bannerlord 的 `CraftingCampaignBehavior` 私有登记方法，游戏版本变化后需要重点回归验证。
