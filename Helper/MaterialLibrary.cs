using System;
using System.Collections.Generic;
// using System.Drawing; // Replaced with Eto.Drawing
using System.IO;
using System.Linq;
using L1MapViewer.CLI;
using L1MapViewer.Models;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// 素材庫管理
    /// </summary>
    public class MaterialLibrary
    {
        /// <summary>
        /// 素材庫路徑
        /// </summary>
        public string LibraryPath { get; set; }

        /// <summary>
        /// 設定檔路徑
        /// </summary>
        private static string SettingsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "L1MapViewer", "recent_materials.txt");

        /// <summary>
        /// 最大最近使用數量
        /// </summary>
        public int MaxRecentCount { get; set; } = 10;

        /// <summary>
        /// 素材索引快取
        /// </summary>
        private Dictionary<string, Fs3pInfo> _indexCache = new Dictionary<string, Fs3pInfo>();

        public MaterialLibrary()
        {
            // 預設路徑
            LibraryPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "L1MapViewer", "Materials");
        }

        public MaterialLibrary(string libraryPath)
        {
            LibraryPath = libraryPath;
        }

        /// <summary>
        /// 確保素材庫目錄存在
        /// </summary>
        public void EnsureDirectoryExists()
        {
            if (!Directory.Exists(LibraryPath))
            {
                Directory.CreateDirectory(LibraryPath);
            }
        }

        /// <summary>
        /// 確保設定檔目錄存在
        /// </summary>
        private static void EnsureSettingsDirectoryExists()
        {
            var dir = Path.GetDirectoryName(SettingsFilePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        /// <summary>
        /// 從設定檔讀取最近素材列表
        /// </summary>
        private static List<string> LoadRecentFromSettings()
        {
            var result = new List<string>();
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var lines = File.ReadAllLines(SettingsFilePath);
                    foreach (var line in lines)
                    {
                        var path = line.Trim();
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            result.Add(path);
                        }
                    }
                }
            }
            catch
            {
                // 忽略讀取錯誤
            }
            return result;
        }

        /// <summary>
        /// 儲存最近素材列表到設定檔
        /// </summary>
        private static void SaveRecentToSettings(List<string> recentList)
        {
            try
            {
                EnsureSettingsDirectoryExists();
                File.WriteAllLines(SettingsFilePath, recentList);
            }
            catch
            {
                // 忽略寫入錯誤
            }
        }

        /// <summary>
        /// 取得所有素材檔案資訊
        /// </summary>
        public List<Fs3pInfo> GetAllMaterials()
        {
            EnsureDirectoryExists();

            var materials = new List<Fs3pInfo>();
            var files = Directory.GetFiles(LibraryPath, "*.fs32p", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                try
                {
                    if (_indexCache.TryGetValue(file, out var cached))
                    {
                        materials.Add(cached);
                    }
                    else
                    {
                        var info = Fs3pParser.GetInfo(file);
                        if (info != null)
                        {
                            _indexCache[file] = info;
                            materials.Add(info);
                        }
                    }
                }
                catch
                {
                    // 忽略無效檔案
                }
            }

            return materials;
        }

        /// <summary>
        /// 取得最近的素材（從設定檔讀取，不存在的會跳過）
        /// </summary>
        public List<Fs3pInfo> GetRecentMaterials()
        {
            var result = new List<Fs3pInfo>();
            var recentPaths = LoadRecentFromSettings();

            foreach (var path in recentPaths)
            {
                try
                {
                    Fs3pInfo info;
                    if (_indexCache.TryGetValue(path, out var cached))
                    {
                        info = cached;
                    }
                    else
                    {
                        info = Fs3pParser.GetInfo(path);
                        if (info != null)
                        {
                            _indexCache[path] = info;
                        }
                    }

                    if (info != null)
                    {
                        result.Add(info);
                    }
                }
                catch
                {
                    // 忽略無效檔案
                }
            }

            return result;
        }

        /// <summary>
        /// 新增到最近使用（儲存到設定檔）
        /// </summary>
        public void AddToRecent(string filePath)
        {
            // 從設定檔讀取現有列表
            var recentList = LoadRecentFromSettings();

            // 移除已存在的（會重新加到最前面）
            recentList.Remove(filePath);

            // 加到最前面
            recentList.Insert(0, filePath);

            // 限制數量
            while (recentList.Count > MaxRecentCount)
            {
                recentList.RemoveAt(recentList.Count - 1);
            }

            // 儲存回設定檔
            SaveRecentToSettings(recentList);
        }

        /// <summary>
        /// 儲存素材到素材庫
        /// </summary>
        public string SaveMaterial(Fs3pData material)
        {
            EnsureDirectoryExists();

            // 產生檔案名稱
            string safeName = GetSafeFileName(material.Name);
            string fileName = $"{safeName}.fs32p";
            string filePath = Path.Combine(LibraryPath, fileName);

            // 如果檔案已存在，加上數字
            int counter = 1;
            while (File.Exists(filePath))
            {
                fileName = $"{safeName}_{counter}.fs32p";
                filePath = Path.Combine(LibraryPath, fileName);
                counter++;
            }

            // 寫入檔案
            Fs3pWriter.Write(material, filePath);

            // 加到最近使用
            AddToRecent(filePath);

            // 更新快取
            var info = Fs3pParser.GetInfo(filePath);
            if (info != null)
            {
                _indexCache[filePath] = info;
            }

            return filePath;
        }

        /// <summary>
        /// 載入素材
        /// </summary>
        public Fs3pData LoadMaterial(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            var material = Fs3pParser.ParseFile(filePath);

            // 加到最近使用
            AddToRecent(filePath);

            return material;
        }

        /// <summary>
        /// 刪除素材
        /// </summary>
        public bool DeleteMaterial(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // 從設定檔中移除
                var recentList = LoadRecentFromSettings();
                if (recentList.Remove(filePath))
                {
                    SaveRecentToSettings(recentList);
                }

                _indexCache.Remove(filePath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 搜尋素材
        /// </summary>
        public List<Fs3pInfo> Search(string keyword, List<string> tags = null)
        {
            var allMaterials = GetAllMaterials();

            var result = allMaterials.AsEnumerable();

            // 關鍵字搜尋
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                result = result.Where(m =>
                    m.Name != null && m.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            // TODO: 標籤搜尋（需要完整載入 fs3p 才能取得標籤）

            return result.ToList();
        }

        /// <summary>
        /// 取得縮圖
        /// </summary>
        public Bitmap GetThumbnail(Fs3pInfo info)
        {
            if (info.ThumbnailPng == null || info.ThumbnailPng.Length == 0)
                return null;

            try
            {
                using (var ms = new MemoryStream(info.ThumbnailPng))
                {
                    return new Bitmap(ms);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 清除快取
        /// </summary>
        public void ClearCache()
        {
            _indexCache.Clear();
        }

        /// <summary>
        /// 產生安全的檔案名稱
        /// </summary>
        private string GetSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "material";

            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = new string(name.Where(c => !invalid.Contains(c)).ToArray());

            if (string.IsNullOrWhiteSpace(safe))
                return "material";

            return safe.Trim();
        }
    }
}
