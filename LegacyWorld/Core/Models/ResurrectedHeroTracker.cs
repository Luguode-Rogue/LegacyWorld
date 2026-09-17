using System;
using System.Collections.Generic;

namespace LegacyWorld.Core.Models
{
    /// <summary>
    /// 当前进程内的复刻追踪表。稳定档案优先按 LegacyId 去重；
    /// 旧档案没有 LegacyId 时由复刻工厂继续使用旧版姓名回退逻辑。
    /// </summary>
    public static class ResurrectedHeroTracker
    {
        public class Entry
        {
            public string LegacyId;
            public string HeroStringId;
            public string Name;
            public string Source;
            public string CultureId;
            public int Level;
            public string WorldId;
            public string Status = "成功";
        }

        private static readonly List<Entry> _entries = new List<Entry>();
        private static readonly HashSet<string> _legacyIds = new HashSet<string>(StringComparer.Ordinal);

        public static IReadOnlyList<Entry> Entries => _entries;

        public static void Register(Entry entry)
        {
            if (entry == null) return;
            if (entry.Status == null) entry.Status = "成功";
            _entries.Add(entry);
            if (!string.IsNullOrWhiteSpace(entry.LegacyId))
                _legacyIds.Add(entry.LegacyId);
        }

        public static bool ContainsLegacyId(string legacyId)
            => !string.IsNullOrWhiteSpace(legacyId) && _legacyIds.Contains(legacyId);

        public static void Clear()
        {
            _entries.Clear();
            _legacyIds.Clear();
        }
    }
}
