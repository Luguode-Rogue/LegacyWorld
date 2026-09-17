using System;
using System.IO;
using System.Reflection;
using LegacyWorld.Core;

namespace LegacyWorld.Core.Storage
{
    /// <summary>Legacy.json 与 LegacyHeroes.json 的文件存储。</summary>
    public static class LegacyStorage
    {
        private static readonly string _folder = GetModuleRoot();
        private static readonly string _filePath = Path.Combine(_folder, "Legacy.json");
        private static readonly string _heroesFilePath = Path.Combine(_folder, "LegacyHeroes.json");

        private static string GetModuleRoot()
        {
            try
            {
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                string dir = Path.GetDirectoryName(assemblyLocation);
                return dir != null
                    ? Path.GetFullPath(Path.Combine(dir, "..", ".."))
                    : AppDomain.CurrentDomain.BaseDirectory;
            }
            catch
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        public static bool Write(string json)
            => WriteAtomicFile(_filePath, json, "Legacy.json");

        public static bool WriteHeroes(string json)
            => WriteAtomicFile(_heroesFilePath, json, "LegacyHeroes.json");

        /// <summary>
        /// 导出时把世界状态与人物遗产作为一个快照提交。
        /// 两个临时文件都写好后才替换正式文件；第二个替换失败时回滚第一个。
        /// </summary>
        public static bool WriteSnapshot(string worldJson, string heroesJson)
        {
            if (worldJson == null || heroesJson == null)
            {
                AffixLogger.Error("STORAGE", "写入快照失败：序列化结果为空");
                return false;
            }

            string token = Guid.NewGuid().ToString("N");
            string worldTemp = _filePath + "." + token + ".tmp";
            string heroesTemp = _heroesFilePath + "." + token + ".tmp";
            string worldBackup = _filePath + "." + token + ".bak";
            string heroesBackup = _heroesFilePath + "." + token + ".bak";
            bool worldExisted = false;
            bool heroesExisted = false;
            bool worldCommitted = false;
            bool heroesCommitted = false;

            try
            {
                Directory.CreateDirectory(_folder);
                worldExisted = File.Exists(_filePath);
                heroesExisted = File.Exists(_heroesFilePath);
                File.WriteAllText(worldTemp, worldJson);
                File.WriteAllText(heroesTemp, heroesJson);

                CommitStagedFile(worldTemp, _filePath, worldBackup, worldExisted);
                worldCommitted = true;
                CommitStagedFile(heroesTemp, _heroesFilePath, heroesBackup, heroesExisted);
                heroesCommitted = true;

                SafeDelete(worldBackup);
                SafeDelete(heroesBackup);
                AffixLogger.Info("STORAGE", $"快照写入完成: {_filePath} + {_heroesFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                if (heroesCommitted)
                    RollbackFile(_heroesFilePath, heroesBackup, heroesExisted);
                if (worldCommitted)
                    RollbackFile(_filePath, worldBackup, worldExisted);
                AffixLogger.Error("STORAGE", "写入世界快照失败，已尝试回滚", ex);
                return false;
            }
            finally
            {
                SafeDelete(worldTemp);
                SafeDelete(heroesTemp);
                SafeDelete(worldBackup);
                SafeDelete(heroesBackup);
            }
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

        public static string ReadHeroes()
        {
            try
            {
                if (!File.Exists(_heroesFilePath)) { AffixLogger.Warn("STORAGE", $"玩家人物遗产文件不存在: {_heroesFilePath}"); return null; }
                return File.ReadAllText(_heroesFilePath);
            }
            catch (Exception ex) { AffixLogger.Error("STORAGE", "读取 LegacyHeroes.json 失败", ex); return null; }
        }

        private static bool WriteAtomicFile(string path, string json, string label)
        {
            if (json == null)
            {
                AffixLogger.Error("STORAGE", $"写入 {label} 失败：内容为空");
                return false;
            }

            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string backup = temp + ".bak";
            bool existed = false;
            try
            {
                Directory.CreateDirectory(_folder);
                existed = File.Exists(path);
                File.WriteAllText(temp, json);
                CommitStagedFile(temp, path, backup, existed);
                SafeDelete(backup);
                AffixLogger.Info("STORAGE", $"已原子写入: {path}");
                return true;
            }
            catch (Exception ex)
            {
                AffixLogger.Error("STORAGE", $"写入 {label} 失败", ex);
                return false;
            }
            finally
            {
                SafeDelete(temp);
                SafeDelete(backup);
            }
        }

        private static void CommitStagedFile(string temp, string target, string backup, bool targetExisted)
        {
            SafeDelete(backup);
            if (targetExisted)
                File.Replace(temp, target, backup, true);
            else
                File.Move(temp, target);
        }

        private static void RollbackFile(string target, string backup, bool originallyExisted)
        {
            try
            {
                if (originallyExisted && File.Exists(backup))
                    File.Copy(backup, target, true);
                else if (!originallyExisted && File.Exists(target))
                    File.Delete(target);
            }
            catch (Exception ex)
            {
                AffixLogger.Error("STORAGE", $"回滚文件失败: {target}", ex);
            }
        }

        private static void SafeDelete(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }
}
