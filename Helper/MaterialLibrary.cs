using System;
using System.Collections.Generic;
using System.Drawing;
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
        /// 最近使用的素材 (檔案路徑)
        /// </summary>
        public List<string> RecentMaterials { get; } = new List<string>();

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
        /// 取得所有素材檔案資訊
        /// </summary>
        public List<Fs3pInfo> GetAllMaterials()
        {
            EnsureDirectoryExists();

            var materials = new List<Fs3pInfo>();
            var files = Directory.GetFiles(LibraryPath, "*.fs3p", SearchOption.AllDirectories);

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
        /// 取得最近使用的素材
        /// </summary>
        public List<Fs3pInfo> GetRecentMaterials()
        {
            var result = new List<Fs3pInfo>();

            foreach (var path in RecentMaterials)
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    if (_indexCache.TryGetValue(path, out var cached))
                    {
                        result.Add(cached);
                    }
                    else
                    {
                        var info = Fs3pParser.GetInfo(path);
                        if (info != null)
                        {
                            _indexCache[path] = info;
                            result.Add(info);
                        }
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
        /// 新增到最近使用
        /// </summary>
        public void AddToRecent(string filePath)
        {
            // 移除已存在的（會重新加到最前面）
            RecentMaterials.Remove(filePath);

            // 加到最前面
            RecentMaterials.Insert(0, filePath);

            // 限制數量
            while (RecentMaterials.Count > MaxRecentCount)
            {
                RecentMaterials.RemoveAt(RecentMaterials.Count - 1);
            }
        }

        /// <summary>
        /// 儲存素材到素材庫
        /// </summary>
        public string SaveMaterial(Fs3pData material)
        {
            EnsureDirectoryExists();

            // 產生檔案名稱
            string safeName = GetSafeFileName(material.Name);
            string fileName = $"{safeName}.fs3p";
            string filePath = Path.Combine(LibraryPath, fileName);

            // 如果檔案已存在，加上數字
            int counter = 1;
            while (File.Exists(filePath))
            {
                fileName = $"{safeName}_{counter}.fs3p";
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

                RecentMaterials.Remove(filePath);
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
