# LegacyWorld 模组功能文档

> 说明：本工作区中现存 `.md` 文档（位于 `ProjectileTrajectorySystem/`）均为另一个项目的资料，
> 与 LegacyWorld 无关；此前也没有 LegacyWorld 大功能文档。本文档为本次新增，
> 用于记录 LegacyWorld「世界状态继承」相关的已实现功能，便于维护与测试。

---

## 功能概览：世界状态继承（Legacy Inheritance）

LegacyWorld 让玩家在「新游戏 / 新战役」中继承上一局存档（Legacy）的部分世界状态，
实现"换了个新世界，却还能遇到原来熟悉的人和势力"的效果。

当前已实现的数据继承类别（由 MCM 设置 `导入数据类别` 控制）：

| 类别 | 设置项 | 说明 |
|------|--------|------|
| 王国 | RestoreKingdoms | 复刻原王国的名称、文化、旗帜、颜色 |
| 家族 | RestoreClans | 复刻原家族（含玩家家族）的名称、文化、旗帜、颜色 |
| 定居点 | RestoreSettlements | 复刻原定居点的归属与状态 |
| **英雄模板复刻** | **RestoreHeroes** | **复刻玩家本体与已招募存活 NPC 为游荡英雄（见下）** |

---

## 新功能：英雄模板复刻（Hero Profile Resurrection）

### 设计目标
实现"在新档遇到原来的自己 / 原来的伙伴"的体验：
上一局存档中**玩家本体（Hero.MainHero）** 与 **玩家家族中仍存活的同伴（Clan.PlayerClan.Companions 且 IsAlive）**，
在开启本功能后，会在新游戏中以**同名、同文化、同外观、同技能/特性的独立游荡英雄**形式重新出现，
可在酒馆偶遇并被招募。

### 边界与限制（重要）
- **英雄跨存档 ID 不稳定**：BT 英雄使用 StringId，但新游戏会重新生成英雄，原对象不存在。
  因此本功能属于"模板复刻"（重建一个很像的游荡英雄），而非"按 ID 找回原对象"。
- **只复刻玩家本体 + 已招募且存活的 companion**，不复制家族全部成员、不复制固定名 NPC。
- **复刻英雄是独立游荡 NPC，不绑定玩家家族**（不会出现"自己人里多了一个自己"的冲突）。
- 玩家英雄本人在新游戏里仍是新玩家自己；复刻出的"原玩家"是一个可被遇到的流浪英雄。

### 实现链路
1. **导出**（`BannerlordGameAdapter.GetHeroProfiles`）：遍历玩家本体 + 存活 companion，
   构造 `HeroProfile`（姓名、文化、性别、等级、外貌 BodyProperties、技能、特性、来源标记 player/companion）。
2. **序列化**（`LegacyData.HeroProfiles`，JSON 字段 `hero_profiles`）：随 `Legacy.json` 落盘。
3. **导入**（`LegacyImporter.RestoreHeroes` → `HeroResurrectionFactory.Resurrect`）：
   - 用「同文化游荡英雄模板」（`CharacterObject.FindAll` 筛选 `IsHero && IsWanderer`）经
     `HeroCreator.CreateSpecialHero` 创建新英雄；
   - 还原姓名（`SetName`）、性别、外观（`StaticBodyProperties` + `Weight`/`Build`）、
     技能（`SetSkillValue`）、特性（`SetTraitLevel`）、职业（`SetNewOccupation(Wanderer)`）。
   - 复刻成功/失败均登记到 `ResurrectedHeroTracker`（运行时追踪表，供验证按钮读取）。

### 相关文件
- `Core/Models/HeroProfile.cs` — 英雄档案模型
- `Core/Models/ResurrectedHeroTracker.cs` — 复刻英雄运行时追踪表
- `BannerlordAdapter/BannerlordGameAdapter.cs` — 导出 `GetHeroProfiles` / 构建 `BuildHeroProfile`
- `BannerlordAdapter/Factories/HeroResurrectionFactory.cs` — 复刻工厂 `Resurrect`
- `Core/Import/LegacyImporter.cs` — 导入编排 `RestoreHeroes`
- `Core/Settings/LegacyWorldSettingsManager.cs` — `RestoreHeroes` 开关与验证按钮委托
- `Bannerlord/LegacyBehavior.cs` — 注入验证按钮 `DoListResurrected`

### 日志
日志统一写入模块根目录 `LegacyWorld.log`（经 `AffixLogger`），新功能相关标签：

| 阶段 | 标签 | 示例 |
|------|------|------|
| 导出（汇总） | `EXPORT` | `导出 N 英雄模板` |
| 导出（逐条） | `HERO` | `导出英雄: 你的名字 (player, Lv5)` / `导出英雄: 同伴A (companion, Lv8)` |
| 导入（开始/汇总） | `IMPORT` | `开始复刻英雄模板: N 个` / `英雄模板复刻完成: 成功 X 个 / 失败 Y 个` |
| 导入（逐条成功） | `HERO` | `复刻完成: xxx (性别=男, 等级=N, 职业=Wanderer)` |
| 导入（失败） | `IMPORT` / `HERO` | `复刻英雄失败: xxx (companion)` / `未找到可用的游荡英雄模板，复刻中止` |
| 验证按钮 | `VERIFY` / `BEHAVIOR` | `验证：已复刻英雄 N 名，成功且存活 X 名` |

### 测试方式
1. **导出**：旧档保存（`存档时自动导出` 默认开），或 MCM「手动导出世界状态」。
   确认 `Documents\Mount & Blade II Bannerlord\LegacyWorld\Legacy.json` 含 `hero_profiles`。
2. **导入**：新开战役（自动导入），或当前档点 MCM「手动应用世界状态」。
   需提前在 MCM 勾选 `导入数据类别 → 复原英雄(RestoreHeroes)`（默认关）。
3. **快速验证**：MCM → 操作 → **「列出已复刻英雄（验证）」** → 点「查看」，
   游戏信息栏与日志会列出所有复刻英雄的姓名/来源/等级/文化/存活状态，无需去酒馆挨个找。

---

## 版本记录
- 新增「英雄模板复刻」功能（导出/导入/复刻工厂 + MCM 验证按钮 + 完善日志）。
