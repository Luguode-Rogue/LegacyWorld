using System;
using System.Collections.Generic;
using LegacyWorld.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace LegacyWorld.BannerlordAdapter.Factories
{
    /// <summary>
    /// 玩家英雄装备跨世界迁移。
    /// 普通装备按 StringId 重取；玩家锻造武器按 WeaponDesign 配方在新世界重新生成。
    /// </summary>
    internal static class HeroEquipmentTransferFactory
    {
        private const int CraftedPieceTypeCount = 4;
        private const string PlayerSource = "player";

        public static void Capture(Hero hero, HeroProfile profile)
        {
            if (hero == null || profile == null ||
                !string.Equals(profile.Source, PlayerSource, StringComparison.OrdinalIgnoreCase))
                return;

            profile.HasEquipmentSnapshot = true;
            profile.BattleEquipment = CaptureEquipment(hero.BattleEquipment, "战斗");
            profile.CivilianEquipment = CaptureEquipment(hero.CivilianEquipment, "便装");

            Debug.Print($"[LegacyWorld] 已记录玩家装备: battle={profile.BattleEquipment.Count}, civilian={profile.CivilianEquipment.Count}");
        }

        public static void Restore(Hero hero, HeroProfile profile)
        {
            if (hero == null || profile == null || !profile.HasEquipmentSnapshot ||
                !string.Equals(profile.Source, PlayerSource, StringComparison.OrdinalIgnoreCase))
                return;

            RestoreEquipment(hero, hero.BattleEquipment, profile.BattleEquipment, "战斗");
            RestoreEquipment(hero, hero.CivilianEquipment, profile.CivilianEquipment, "便装");
        }

        private static List<EquipmentSlotProfile> CaptureEquipment(Equipment equipment, string setName)
        {
            var result = new List<EquipmentSlotProfile>();
            if (equipment == null)
                return result;

            for (int i = (int)EquipmentIndex.WeaponItemBeginSlot; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                EquipmentElement element = equipment[(EquipmentIndex)i];
                ItemObject item = element.Item;
                if (item == null)
                    continue;

                // 任务物品属于当前战役状态，不应跨世界复制。
                if (element.IsQuestItem)
                {
                    Debug.Print($"[LegacyWorld] 跳过任务装备: set={setName}, slot={i}, item={item.StringId}");
                    continue;
                }

                var slot = new EquipmentSlotProfile
                {
                    Slot = i,
                    ItemId = item.StringId ?? string.Empty,
                    ItemModifierId = element.ItemModifier?.StringId ?? string.Empty,
                    CosmeticItemId = element.CosmeticItem?.StringId ?? string.Empty
                };

                if (item.IsCraftedByPlayer && item.WeaponDesign != null)
                    slot.CraftedItem = CaptureCraftedItem(item);

                result.Add(slot);
            }

            return result;
        }

        private static CraftedItemProfile CaptureCraftedItem(ItemObject item)
        {
            WeaponDesign design = item.WeaponDesign;
            var crafted = new CraftedItemProfile
            {
                CraftingTemplateId = design?.Template?.StringId ?? string.Empty,
                WeaponName = design?.WeaponName?.ToString() ?? item.Name?.ToString() ?? string.Empty,
                CultureId = item.Culture?.StringId ?? string.Empty
            };

            if (design?.UsedPieces == null)
                return crafted;

            foreach (WeaponDesignElement piece in design.UsedPieces)
            {
                if (piece == null || !piece.IsValid || piece.CraftingPiece == null)
                    continue;

                crafted.Pieces.Add(new CraftedPieceProfile
                {
                    PieceType = (int)piece.CraftingPiece.PieceType,
                    PieceId = piece.CraftingPiece.StringId ?? string.Empty,
                    ScalePercentage = piece.ScalePercentage
                });
            }

            return crafted;
        }

        private static void RestoreEquipment(Hero hero, Equipment target, List<EquipmentSlotProfile> saved, string setName)
        {
            if (target == null)
                return;

            // 新建的 wanderer 自带模板装备。既然存在装备快照，先完全清空，避免模板装备混入招募估价。
            for (int i = (int)EquipmentIndex.WeaponItemBeginSlot; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                target[(EquipmentIndex)i] = EquipmentElement.Invalid;

            if (saved == null)
                return;

            foreach (EquipmentSlotProfile slot in saved)
            {
                if (slot == null || slot.Slot < (int)EquipmentIndex.WeaponItemBeginSlot || slot.Slot >= (int)EquipmentIndex.NumEquipmentSetSlots)
                    continue;

                try
                {
                    ItemObject item = slot.CraftedItem != null
                        ? RebuildCraftedItem(hero, slot.CraftedItem, slot.ItemModifierId)
                        : FindObject<ItemObject>(slot.ItemId);

                    if (item == null)
                    {
                        Debug.Print($"[LegacyWorld] 装备恢复跳过: set={setName}, slot={slot.Slot}, item={slot.ItemId}, 原因=物品不存在");
                        continue;
                    }

                    ItemModifier modifier = FindObject<ItemModifier>(slot.ItemModifierId);
                    if (!string.IsNullOrWhiteSpace(slot.ItemModifierId) && modifier == null)
                        Debug.Print($"[LegacyWorld] 装备词缀缺失，按无词缀恢复: {slot.ItemModifierId}");

                    ItemObject cosmeticItem = FindObject<ItemObject>(slot.CosmeticItemId);
                    if (!string.IsNullOrWhiteSpace(slot.CosmeticItemId) && cosmeticItem == null)
                        Debug.Print($"[LegacyWorld] 外观装备缺失，忽略外观物品: {slot.CosmeticItemId}");

                    target[(EquipmentIndex)slot.Slot] = new EquipmentElement(item, modifier, cosmeticItem, false);
                }
                catch (Exception ex)
                {
                    Debug.Print($"[LegacyWorld] 装备恢复失败但继续导入: set={setName}, slot={slot.Slot}, item={slot.ItemId}, error={ex.Message}");
                }
            }
        }

        private static ItemObject RebuildCraftedItem(Hero hero, CraftedItemProfile saved, string itemModifierId)
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.CraftingTemplateId))
                return null;

            CraftingTemplate template = CraftingTemplate.GetTemplateFromId(saved.CraftingTemplateId);
            if (template == null)
            {
                Debug.Print($"[LegacyWorld] 锻造武器恢复跳过: 找不到模板 {saved.CraftingTemplateId}");
                return null;
            }

            var pieces = new WeaponDesignElement[CraftedPieceTypeCount];
            for (int i = 0; i < pieces.Length; i++)
                pieces[i] = WeaponDesignElement.GetInvalidPieceForType((CraftingPiece.PieceTypes)i);

            if (saved.Pieces == null || saved.Pieces.Count == 0)
            {
                Debug.Print($"[LegacyWorld] 锻造武器恢复跳过: {saved.WeaponName} 没有部件数据");
                return null;
            }

            foreach (CraftedPieceProfile savedPiece in saved.Pieces)
            {
                if (savedPiece == null || savedPiece.PieceType < 0 || savedPiece.PieceType >= pieces.Length)
                    continue;

                CraftingPiece piece = FindObject<CraftingPiece>(savedPiece.PieceId);
                if (piece == null)
                {
                    Debug.Print($"[LegacyWorld] 锻造武器恢复跳过: {saved.WeaponName} 缺少部件 {savedPiece.PieceId}");
                    return null;
                }

                pieces[savedPiece.PieceType] = WeaponDesignElement.CreateUsablePiece(piece, savedPiece.ScalePercentage);
            }

            TextObject weaponName = new TextObject(string.IsNullOrWhiteSpace(saved.WeaponName) ? "Legacy Weapon" : saved.WeaponName);
            string customId = "legacyworld_crafted_" + Guid.NewGuid().ToString("N");
            var design = new WeaponDesign(template, weaponName, pieces, customId);

            ItemObject craftedItem = null;
            CultureObject itemCulture = FindObject<CultureObject>(saved.CultureId) ?? hero?.Culture;
            Crafting.GenerateItem(design, weaponName, itemCulture, template.ItemModifierGroup, ref craftedItem, customId);
            if (craftedItem == null)
            {
                Debug.Print($"[LegacyWorld] 锻造武器恢复失败: Crafting.GenerateItem 返回空 ({saved.WeaponName})");
                return null;
            }

            // 标记为玩家锻造，并注册为真正的运行时 MBObject；随后通知原版 CraftingCampaignBehavior，
            // 让它把设计数据写进自己的持久化字典，确保后续保存/读档仍可重建该武器。
            ItemObject.InitAsPlayerCraftedItem(ref craftedItem);
            craftedItem = MBObjectManager.Instance.RegisterObject(craftedItem);

            ItemModifier modifier = FindObject<ItemModifier>(itemModifierId);
            CampaignEventDispatcher.Instance?.OnNewItemCrafted(craftedItem, modifier, false);

            Debug.Print($"[LegacyWorld] 已重建锻造武器: {saved.WeaponName} -> {craftedItem.StringId}");
            return craftedItem;
        }

        private static T FindObject<T>(string stringId) where T : MBObjectBase
        {
            if (string.IsNullOrWhiteSpace(stringId) || MBObjectManager.Instance == null)
                return null;

            return MBObjectManager.Instance.GetObject<T>(stringId);
        }
    }
}
