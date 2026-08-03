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

        public bool RestoreKingdoms = true;
        public bool RestoreClans = true;
        public bool RestoreSettlements = true;
        public bool RestoreClanEconomy = true;
        public bool CreateMissingClans = false;
        public bool RestoreHeroes = false;
    }
}
