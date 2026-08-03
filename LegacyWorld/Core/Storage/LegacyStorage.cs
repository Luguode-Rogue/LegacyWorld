using System;
using System.IO;
using System.Reflection;
using LegacyWorld.Core;

namespace LegacyWorld.Core.Storage
{
    /// <summary>
    /// 文件存储。Legacy.json 位于模块根目录，与 LegacyWorld.log 同级。
    /// </summary>
    public static class LegacyStorage
    {
        private static readonly string _folder = GetModuleRoot();
        private static readonly string _filePath = Path.Combine(_folder, "Legacy.json");

        private static string GetModuleRoot()
        {
            try
            {
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                string dir = Path.GetDirectoryName(assemblyLocation);
                // dll 通常位于 <Module>/bin/Win64_Shipping_Client/，向上两级回到 Module 根目录
                return dir != null
                    ? Path.GetFullPath(Path.Combine(dir, "..", ".."))
                    : AppDomain.CurrentDomain.BaseDirectory;
            }
            catch
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        public static void Write(string json)
        {
            try
            {
                if (!Directory.Exists(_folder)) Directory.CreateDirectory(_folder);
                File.WriteAllText(_filePath, json);
                AffixLogger.Info("STORAGE", $"已写入: {_filePath}（模块根目录，与 LegacyWorld.log 同级）");
            }
            catch (Exception ex) { AffixLogger.Error("STORAGE", "写入 Legacy.json 失败", ex); }
        }

        public static string Read()
        {
            try
            {
                if (!File.Exists(_filePath)) { AffixLogger.Warn("STORAGE", $"文件不存在: {_filePath}"); return null; }
                return File.ReadAllText(_filePath);
            }
            catch (Exception ex) { AffixLogger.Error("STORAGE", "读取 Legacy.json 失败", ex); return null; }
        }
    }
}
