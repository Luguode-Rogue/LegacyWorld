using System.Xml.Serialization;

namespace LegacyWorld.Core.Settings
{
    /// <summary>
    /// MCM 设置数据层（第1层）。纯 POCO，XML 序列化载体。
    /// </summary>
    [XmlRoot("LegacyWorldSettings")]
    public class LegacyWorldSettingsData
    {
        public bool Enabled = true;
        public bool AutoExportOnSave = true;
        public bool LogEnabled = true;

        // Bannerlord 1.5 高级开局兼容：默认以游戏开局选项为准。
        public bool RespectAdvancedStartOptions = true;
        public bool AdvancedStartSkipKingdomRulers = true;
        public bool AdvancedStartSkipClanKingdoms = true;
        public bool AdvancedStartSkipSettlementOwners = true;

        public bool RestoreKingdoms = true;
        public bool RestoreClans = true;
        public bool RestoreSettlements = true;
        public bool RestoreClanEconomy = true;
        public bool CreateMissingClans = false;
        public bool RestoreHeroes = false;
    }
}
