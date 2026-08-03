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
            public string Status = "成功"; // 成功 / 失败原因
        }

        private static readonly List<Entry> _entries = new List<Entry>();

        public static IReadOnlyList<Entry> Entries => _entries;

        public static void Register(Entry entry)
        {
            if (entry == null) return;
            if (entry.Status == null) entry.Status = "成功";
            _entries.Add(entry);
        }

        public static void Clear() => _entries.Clear();
    }
}
