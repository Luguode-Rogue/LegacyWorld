using System.Collections.Generic;

namespace LegacyWorld.Core.Models
{
    /// <summary>
    /// 英雄模板档案（A 方案：英雄模板复刻）。
    /// 记录玩家本体（Hero.MainHero）以及玩家招募过且仍然存活的非固定名 NPC，
    /// 以便在新游戏中以“游荡英雄”的形式复刻出来（遇到原来的自己/伙伴的效果）。
    /// 记录外貌、姓名、文化、技能、特性等模板数据；玩家本体额外记录战斗/便装装备，
    /// 但不迁移身份、阵营等运行时状态。
    /// </summary>
    public class HeroProfile
    {
        /// <summary>模板来源：player=玩家本体，companion=玩家招募过的 NPC。</summary>
        public string Source { get; set; }

        /// <summary>模板来源存档的世界 Id（Campaign.UniqueGameId）。</summary>
        public string WorldId { get; set; }

        /// <summary>
        /// 跨导出稳定身份。新档案使用 WorldId + Source + 原 Hero.StringId 组成，
        /// 用于在同一世界中即使英雄改名也能刷新原档案，并避免跨世界同名英雄互相误判。
        /// 旧版本档案没有此字段时继续回退到 WorldId + Name + Source。
        /// </summary>
        public string LegacyId { get; set; }

        /// <summary>完整姓名。</summary>
        public string Name { get; set; }

        /// <summary>名字（名）。</summary>
        public string FirstName { get; set; }

        /// <summary>文化 StringId（用于新游戏里挑选同族模板以还原外貌种族）。</summary>
        public string CultureId { get; set; }

        /// <summary>是否为女性。</summary>
        public bool IsFemale { get; set; }

        /// <summary>等级。</summary>
        public int Level { get; set; }

        /// <summary>职业（Occupation 枚举名，如 Wanderer）。</summary>
        public string Occupation { get; set; }

        /// <summary>静止外观属性（StaticBodyProperties 的序列化字符串）。</summary>
        public string StaticBodyProperties { get; set; }

        /// <summary>体重（用于还原动态外观）。</summary>
        public float Weight { get; set; }

        /// <summary>体型（用于还原动态外观）。</summary>
        public float Build { get; set; }

        /// <summary>技能值：技能 StringId -> 值。</summary>
        public Dictionary<string, int> Skills { get; set; } = new Dictionary<string, int>();

        /// <summary>特性值：特性 StringId -> 等级。</summary>
        public Dictionary<string, int> Traits { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// 是否包含完整装备快照。用于区分“旧版本档案没有装备字段”和“玩家确实裸装”两种情况。
        /// </summary>
        public bool HasEquipmentSnapshot { get; set; }

        /// <summary>玩家本体的战斗装备。旧档案缺少此字段时按空列表处理。</summary>
        public List<EquipmentSlotProfile> BattleEquipment { get; set; } = new List<EquipmentSlotProfile>();

        /// <summary>玩家本体的第一套便装。旧档案缺少此字段时按空列表处理。</summary>
        public List<EquipmentSlotProfile> CivilianEquipment { get; set; } = new List<EquipmentSlotProfile>();
    }
}
