namespace LegacyWorld.Core.Settings
{
    /// <summary>
    /// 导入配置 POCO。由 MCM 数据层填充，供 Importer 读取。
    /// 保留为纯数据类，确保导入模块可独立引用。
    /// </summary>
    public class LegacySettings
    {
        public bool RestoreKingdoms { get; set; } = true;
        public bool RestoreClans { get; set; } = true;
        public bool RestoreClanKingdomMembership { get; set; } = true;
        public bool RestoreSettlements { get; set; } = true;
        public bool RestoreSettlementOwnership { get; set; } = true;
        public bool RestoreClanEconomy { get; set; } = true;
        public bool CreateMissingClans { get; set; }
        public bool RestoreHeroes { get; set; } = false;
    }
}
