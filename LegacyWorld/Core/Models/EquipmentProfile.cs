using System.Collections.Generic;

namespace LegacyWorld.Core.Models
{
    /// <summary>
    /// 英雄装备槽位档案。普通物品直接记录 StringId；玩家锻造武器额外记录完整锻造设计。
    /// </summary>
    public class EquipmentSlotProfile
    {
        public int Slot { get; set; }
        public string ItemId { get; set; }
        public string ItemModifierId { get; set; }
        public string CosmeticItemId { get; set; }
        public CraftedItemProfile CraftedItem { get; set; }
    }

    /// <summary>
    /// 玩家锻造武器档案。跨存档时按模板、部件和缩放比例重新生成新的 ItemObject。
    /// </summary>
    public class CraftedItemProfile
    {
        public string CraftingTemplateId { get; set; }
        public string WeaponName { get; set; }
        public string CultureId { get; set; }
        public List<CraftedPieceProfile> Pieces { get; set; } = new List<CraftedPieceProfile>();
    }

    public class CraftedPieceProfile
    {
        public int PieceType { get; set; }
        public string PieceId { get; set; }
        public int ScalePercentage { get; set; } = 100;
    }
}
