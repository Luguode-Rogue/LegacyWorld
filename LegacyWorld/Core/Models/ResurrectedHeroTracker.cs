using System.Collections.Generic;

namespace LegacyWorld.Core.Models
{
    /// <summary>
    /// 运行时登记表：记录本模组在新游戏中复刻出的英雄。
    /// 仅用于 MCM 验证按钮"列出已复刻英雄"，不参与任何游戏逻辑或存档。
    /// 进程重启（新游戏）后自动清空。
    /// </summary>
    public static class ResurrectedHeroTracker
    {
        public class Entry
        {
            public string HeroStringId;   // 复刻英雄在游戏中的 StringId（可经 Hero.Find 取回对象）
            public string Name;           // 原档案姓名
            public string Source;         // player / companion
            public string CultureId;      // 原文化
            public int Level;             // 原等级
            public string WorldId;        // 遗产来源世界 WorldId（用于跨进程持久化去重）
            public string Status = "成功"; // 成功 / 失败原因
        }

        private static readonly List<Entry> _entries = new List<Entry>();

        /// <summary>
        /// 已复刻的 (Name, Source) 键集合，用于防止同一存档内重复复刻出多个同名英雄。
        /// </summary>
        private static readonly HashSet<string> _keys = new HashSet<string>();

        public static IReadOnlyList<Entry> Entries => _entries;

        public static void Register(Entry entry)
        {
            if (entry == null) return;
            if (entry.Status == null) entry.Status = "成功";
            _entries.Add(entry);
            _keys.Add(MakeKey(entry.Name, entry.Source));
        }

        /// <summary>
        /// 是否已复刻过该 (Name, Source) 组合。用于复刻前查重，避免复制出重复 NPC。
        /// </summary>
        public static bool Contains(string name, string source)
            => _keys.Contains(MakeKey(name, source));

        private static string MakeKey(string name, string source)
            => $"{name ?? ""}|{source ?? ""}";

        public static void Clear()
        {
            _entries.Clear();
            _keys.Clear();
        }
    }
}
