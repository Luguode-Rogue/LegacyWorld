# LegacyWorld 工程说明

## 1. 项目目标

LegacyWorld 为 Bannerlord 单人战役提供“世界遗产”机制：旧战役保存部分世界结构和人物状态，新战役再按设置恢复。核心目标不是复制完整存档，而是在新世界中保留势力格局、旧角色与玩家装备等连续性。

当前数据版本为 `0.4.0`。

## 2. 主要功能

### 世界状态

- 王国：记录王国、统治者家族与文化；导入时恢复已有王国的统治者。
- 家族：记录所属王国、金币、声望、影响力、Tier 等数据；当前实际恢复所属王国与经济数据。
- 定居点：记录城镇/城堡/村庄归属、文化与繁荣度；导入时村庄归属跟随母城。
- 旧玩家领地在新档不能安全映射时，可使用原版叛军 Clan 机制接管，并登记到原版叛军转正计时。

### 人物遗产

导出：

- `Hero.MainHero` 作为 `player`。
- `Clan.PlayerClan.Companions` 中仍存活的同伴作为 `companion`。
- 保存姓名、文化、性别、等级、外貌、技能和特性。
- 新档案记录稳定 `LegacyId = WorldId + Source + 原 Hero.StringId`。

导入：

- 使用文化匹配的 Wanderer 模板创建独立游荡英雄。
- 恢复姓名、性别、外貌、技能、特性和等级。
- 将新英雄激活并放入合适定居点，使其能在世界中出现和被招募。
- 新格式优先按 `LegacyId` 去重，因此同一旧英雄改名后仍更新原档案，不会新增一份；不同世界中同名同文化英雄也不会互相阻挡。
- 成功复刻记录额外保存 `TargetWorldId + HeroStringId`，用于当前目标世界的验证和部分失败重试。
- 旧版没有 `LegacyId` 的数据继续使用 `WorldId + Name + Source` 和姓名/文化查重作为兼容回退。

### 玩家装备迁移

仅 `player` 档案保存完整装备快照：战斗装备、便装、`ItemModifier`、`CosmeticItem`，以及玩家自锻武器的模板、部件、缩放、名称与文化。任务物品明确跳过。

恢复时先清空新 Wanderer 的模板装备，再填入快照，避免模板装备混入招募估价。同一把自锻武器如果同时用于战斗/便装，会共享同一个重建 ItemObject。

Bannerlord 原版 `CompanionHiringPriceCalculationModel` 会读取英雄的战斗装备和便装，将装备城镇价值合计的 1/2 加入招募价格，因此本项目不额外修改招募价格模型。

## 3. 数据文件与写入

数据位于模块根目录：

- `Legacy.json`：最近一次世界状态快照，覆盖写。
- `LegacyHeroes.json`：人物遗产累积文件，包括 `profiles`、`applied_world_ids`、`resurrected_heroes`。
- `LegacyWorld.log`：运行日志。

正常导出先生成两份 JSON，再由 `LegacyStorage.WriteSnapshot` 写入两个临时文件；只有两份临时文件都成功后才替换正式文件。如果第二个替换失败，会尝试回滚第一个，减少跨代快照。

单独更新 `LegacyHeroes.json` 也使用临时文件 + `File.Replace/File.Move` 的原子文件替换。写入函数返回真实成功状态，上层不会在写入失败后继续显示绿色成功。

## 4. 导入结果与重试

导入状态分为：

- `Applied`：本次完整执行并成功持久化 AppliedWorldIds。
- `Partial`：已经执行，但英雄复刻存在异常或持久化步骤失败。
- `Skipped`：数据来自当前世界、已经应用过或没有可用数据，因此未执行。
- `Failed`：战役未就绪或发生未处理异常。

只有 `Applied` 才把当前 CampaignBehavior 标为 `_applied=true`。`Partial` 不写入新的 AppliedWorldIds，因此允许后续重试。

为避免部分失败重试把已经成功的英雄再创建一次，每条新复刻记录保存：

- `LegacyId`：来源英雄稳定身份。
- `TargetWorldId`：这次复刻进入的目标世界。
- `HeroStringId`：目标世界中新建 Hero 的 StringId。

重试前会检查同一 TargetWorldId 中仍存活的 HeroStringId，并从本轮待复刻模板中排除对应 LegacyId。

## 5. 导入/导出流程

导出：

`LegacyBehavior.OnBeforeSave` → `LegacyService.Export` → `LegacyExporter.ExportWorld/ExportHeroes` → JSON → `LegacyStorage.WriteSnapshot`

导入：

`OnNewGameCreatedPartialFollowUpEndEvent` → `LegacyService.Import` → `LegacyImporter.Apply` → Kingdom → Clan → Settlement → Hero

MCM “立即应用”调用 `LegacyService.ForceImport()`，UI 根据真实状态显示完整成功、部分成功、跳过或失败，不再固定显示成功。

## 6. 高级开局兼容

默认 `RespectAdvancedStartOptions=true`。检测到高级开局场景或政治身份时，可分别跳过王国统治者、家族所属王国和领地所有权恢复；家族经济、繁荣度、英雄遗产等非冲突项仍按设置执行。提示在地图状态激活后通过原版 Inquiry 弹窗显示。

## 7. 代码结构

- `LegacyWorld/Core/Models`：JSON 数据模型与运行时追踪结构。
- `LegacyWorld/Core/Export`：导出编排、人物档案累积与归一化。
- `LegacyWorld/Core/Import`：王国、家族、定居点和英雄的导入编排。
- `LegacyWorld/Core/Storage`：双文件快照、原子写入和读取。
- `LegacyWorld/Core/Settings`：MCM 数据与设置持久化。
- `LegacyWorld/Adapter`：游戏抽象接口。
- `LegacyWorld/BannerlordAdapter`：Bannerlord API 实现、英雄复刻、装备迁移、叛军与 Harmony Patch。
- `LegacyWorld/Bannerlord`：战役事件、服务入口与导入结果模型。
- `LegacyWorld/Compatibility`：兼容性补丁。

## 8. 清理后的单一实现路径

- 装备迁移只有 `HeroEquipmentTransferFactory` 一套 Capture/Restore。
- 人物稳定身份由 `HeroProfile.LegacyId` 统一负责；旧的 `(Name, Source)` 运行时 key 已删除。
- 叛军创建只有 `BannerlordGameAdapter.CreateRebelClanForSettlement` 一套；`SettlementChangeFactory` 只保留通用状态修改和原版叛军转正计时登记。
- `LegacyHeroes.json` 属于运行时数据，已从版本库移除并加入 `.gitignore`。

## 9. 当前已知限制

- `CreateMissingClans` 在适配层仍未完整实现，开关打开也无法真正重建缺失家族。
- `ClanState.Tier` 会导出，但当前导入流程尚未恢复 Tier。
- 自锻武器持久化通过反射调用 Bannerlord 私有 `CraftingCampaignBehavior.OnNewItemCrafted`，游戏版本更新后需要实机回归。
- 旧版人物档案没有 `LegacyId`，第一次迁移只能按旧姓名键匹配；若用户在升级前已改名，旧记录无法凭空推断为同一 Hero。

## 10. 建议回归测试

1. 同一存档连续导出两次，确认人物档案数量不增长。
2. 玩家改名后再次导出，确认 `LegacyId` 相同且原档案被刷新。
3. 两个不同 WorldId 使用同名同文化玩家，确认新档可分别复刻。
4. 玩家同时穿战斗装备和便装，确认两套装备完整恢复。
5. 同一自锻武器同时用于战斗/便装，确认只重建一个 ItemObject，并在保存/读档后仍存在。
6. 模拟第二个快照文件写入失败，确认不会显示绿色导出成功，并尝试回滚第一个文件。
7. 连续点击“立即应用”，第二次应显示 Skipped，而不是成功。
8. 制造一个英雄复刻异常，确认结果为 Partial、AppliedWorldIds 不新增；修复条件后重试，已经成功的英雄不重复创建。
9. 读档后使用“列出已复刻英雄”，新记录按当前 TargetWorldId 过滤并优先用 HeroStringId 查找对象。
