using System;
using System.IO;
using System.Reflection;
using LegacyWorld.Core.Settings;

namespace LegacyWorld.Core
{
    /// <summary>
    /// 跨 Module 兼容的调试日志工具。
    /// 日志文件位置：自动探测 Assembly 所在目录下的 affix_debug.log。
    /// 运行时开关由 MCM 设置（LegacyWorldSettingsManager.Settings.LogEnabled）控制。
    /// </summary>
    public static class AffixLogger
    {
        private static readonly string _logPath;
        private static readonly object _lock = new object();

        static AffixLogger()
        {
            try
            {
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                string dir = Path.GetDirectoryName(assemblyLocation);
                // dll 通常位于 <Module>/bin/Win64_Shipping_Client/，向上两级回到 Module 根目录
                string moduleRoot = dir != null
                    ? Path.GetFullPath(Path.Combine(dir, "..", ".."))
                    : AppDomain.CurrentDomain.BaseDirectory;
                _logPath = Path.Combine(moduleRoot, "LegacyWorld.log");
            }
            catch { _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LegacyWorld.log"); }
        }

        public static bool LogEnabled
        {
            get { try { return LegacyWorldSettingsManager.Settings.LogEnabled; } catch { return true; } }
        }

        public static void Info(string tag, string message) => WriteLog("INFO", tag, message);
        public static void Warn(string tag, string message) => WriteLog("WARN", tag, message);
        public static void Error(string tag, string message, Exception ex = null)
            => WriteLog("ERROR", tag, $"{message} | {(ex != null ? ex.ToString() : "")}");

        private static void WriteLog(string level, string tag, string message)
        {
            if (!LogEnabled) return;
            try
            {
                lock (_lock)
                {
                    string line = $"[{DateTime.Now:HH:mm:ss}][{level}][{tag}] {message}{Environment.NewLine}";
                    File.AppendAllText(_logPath, line);
                }
            }
            catch { /* 日志失败不影响主流程 */ }
        }
    }
}
