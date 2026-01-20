using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using L1FlyMapViewer;
using L1MapViewer.CLI;
using L1MapViewer.Models;
using L1MapViewer.Reader;
using NLog;
using static L1MapViewer.Other.Struct;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// Tile 清理工具 - 掃描所有地圖檔案，找出未使用的高編號 Tiles
    /// </summary>
    public static class TileCleanupHelper
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 掃描結果
        /// </summary>
        public class ScanResult
        {
            /// <summary>
            /// 所有在地圖中使用的 Tile ID（來自 L6 層）
            /// </summary>
            public HashSet<int> UsedTileIds { get; set; } = new HashSet<int>();

            /// <summary>
            /// tile.idx 中所有的 Tile ID
            /// </summary>
            public HashSet<int> AllTileIds { get; set; } = new HashSet<int>();

            /// <summary>
            /// 未使用且編號 > threshold 的 Tile ID
            /// </summary>
            public List<int> UnusedHighTileIds { get; set; } = new List<int>();

            /// <summary>
            /// 掃描的地圖資料夾數量
            /// </summary>
            public int MapFolderCount { get; set; }

            /// <summary>
            /// 掃描的 S32/Seg 檔案數量
            /// </summary>
            public int FileCount { get; set; }

            /// <summary>
            /// 掃描過程中的錯誤訊息
            /// </summary>
            public List<string> Errors { get; set; } = new List<string>();

            /// <summary>
            /// 使用的門檻值
            /// </summary>
            public int Threshold { get; set; }
        }

        /// <summary>
        /// 掃描所有地圖檔案，找出未使用的 Tiles
        /// </summary>
        /// <param name="threshold">編號門檻值（預設 5000），只清理超過此值的 Tile</param>
        /// <param name="progressCallback">進度回調 (currentFile, totalFiles, message)</param>
        /// <returns>掃描結果</returns>
        public static ScanResult ScanUnusedTiles(int threshold = 5000, Action<int, int, string> progressCallback = null)
        {
            var result = new ScanResult { Threshold = threshold };

            _logger.Info($"[TileCleanup] Starting scan with threshold {threshold}");

            // 1. 找出 map 資料夾
            string mapPath = Path.Combine(Share.LineagePath, "map");
            if (!Directory.Exists(mapPath))
            {
                result.Errors.Add($"找不到 map 資料夾: {mapPath}");
                _logger.Error($"[TileCleanup] Map folder not found: {mapPath}");
                return result;
            }

            // 2. 取得所有地圖子資料夾
            var mapFolders = Directory.GetDirectories(mapPath);
            result.MapFolderCount = mapFolders.Length;
            _logger.Info($"[TileCleanup] Found {mapFolders.Length} map folders");

            // 3. 收集所有 S32 和 Seg 檔案
            var allFiles = new List<string>();
            foreach (var folder in mapFolders)
            {
                allFiles.AddRange(Directory.GetFiles(folder, "*.s32"));
                allFiles.AddRange(Directory.GetFiles(folder, "*.seg"));
            }
            result.FileCount = allFiles.Count;
            _logger.Info($"[TileCleanup] Found {allFiles.Count} S32/Seg files to scan");

            // 4. 平行掃描每個檔案，收集使用的 Tile ID
            int processedCount = 0;
            object lockObj = new object();

            System.Threading.Tasks.Parallel.ForEach(allFiles, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, filePath =>
            {
                string fileName = Path.GetFileName(filePath);
                HashSet<int> localTileIds = new HashSet<int>();
                string localError = null;

                try
                {
                    byte[] data = File.ReadAllBytes(filePath);
                    S32Data s32Data;

                    if (filePath.EndsWith(".seg", StringComparison.OrdinalIgnoreCase))
                    {
                        s32Data = SegParser.Parse(data);
                    }
                    else
                    {
                        s32Data = S32Parser.Parse(data);
                    }

                    if (s32Data != null)
                    {
                        // 收集 L6 層使用的 Tile ID
                        foreach (int tileId in s32Data.Layer6)
                        {
                            localTileIds.Add(tileId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    localError = $"解析 {fileName} 時發生錯誤: {ex.Message}";
                    _logger.Warn(ex, $"[TileCleanup] Error parsing {filePath}");
                }

                // 合併結果（需要鎖定）
                lock (lockObj)
                {
                    int current = ++processedCount;
                    progressCallback?.Invoke(current, allFiles.Count, fileName);

                    foreach (int tileId in localTileIds)
                    {
                        result.UsedTileIds.Add(tileId);
                    }

                    if (localError != null)
                    {
                        result.Errors.Add(localError);
                    }
                }
            });

            _logger.Info($"[TileCleanup] Found {result.UsedTileIds.Count} unique used tile IDs");

            // 5. 讀取 tile.idx 中所有的 Tile ID
            try
            {
                var idxData = L1IdxReader.GetAll("Tile");
                if (idxData != null)
                {
                    foreach (var entry in idxData)
                    {
                        string fileName = entry.Key;
                        if (fileName.EndsWith(".til", StringComparison.OrdinalIgnoreCase) && fileName != "list.til")
                        {
                            string idStr = fileName.Substring(0, fileName.Length - 4);
                            if (int.TryParse(idStr, out int tileId))
                            {
                                result.AllTileIds.Add(tileId);
                            }
                        }
                    }
                }
                _logger.Info($"[TileCleanup] Found {result.AllTileIds.Count} tiles in tile.idx");
            }
            catch (Exception ex)
            {
                result.Errors.Add($"讀取 tile.idx 時發生錯誤: {ex.Message}");
                _logger.Error(ex, "[TileCleanup] Error reading tile.idx");
            }

            // 6. 找出未使用且編號 > threshold 的 Tile ID
            result.UnusedHighTileIds = result.AllTileIds
                .Where(id => id > threshold && !result.UsedTileIds.Contains(id))
                .OrderBy(id => id)
                .ToList();

            _logger.Info($"[TileCleanup] Found {result.UnusedHighTileIds.Count} unused tiles with ID > {threshold}");

            return result;
        }

        /// <summary>
        /// 從 tile.idx 中移除指定的 Tile
        /// </summary>
        /// <param name="tileIdsToRemove">要移除的 Tile ID 列表</param>
        /// <param name="createBackup">是否建立備份</param>
        /// <returns>(成功數量, 錯誤訊息列表)</returns>
        public static (int successCount, List<string> errors) RemoveTiles(
            IEnumerable<int> tileIdsToRemove,
            bool createBackup = true)
        {
            var errors = new List<string>();
            var tilesToRemove = new HashSet<int>(tileIdsToRemove);

            if (tilesToRemove.Count == 0)
            {
                return (0, errors);
            }

            _logger.Info($"[TileCleanup] Removing {tilesToRemove.Count} tiles from tile.idx");

            string idxFilePath = Path.Combine(Share.LineagePath, "Tile.idx");
            string pakFilePath = Path.Combine(Share.LineagePath, "Tile.pak");

            if (!File.Exists(idxFilePath))
            {
                errors.Add("找不到 Tile.idx 檔案");
                return (0, errors);
            }

            // 建立備份
            string backupSuffix = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string idxBackup = idxFilePath + $".backup_{backupSuffix}";
            string pakBackup = pakFilePath + $".backup_{backupSuffix}";

            if (createBackup)
            {
                try
                {
                    File.Copy(idxFilePath, idxBackup);
                    _logger.Info($"[TileCleanup] Created idx backup: {idxBackup}");

                    if (File.Exists(pakFilePath))
                    {
                        File.Copy(pakFilePath, pakBackup);
                        _logger.Info($"[TileCleanup] Created pak backup: {pakBackup}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"建立備份時發生錯誤: {ex.Message}");
                    _logger.Error(ex, "[TileCleanup] Error creating backup");
                    return (0, errors);
                }
            }

            try
            {
                // 讀取現有的 idx 結構
                IdxType structType = L1PakWriter.GetIdxType(idxFilePath);
                byte[] idxData = File.ReadAllBytes(idxFilePath);

                int headerSize = (structType == IdxType.OLD) ? 4 : 8;
                int recordSize = GetRecordSize(structType);

                var newRecords = new List<byte[]>();
                int removedCount = 0;

                using (var br = new BinaryReader(new MemoryStream(idxData)))
                {
                    br.BaseStream.Seek(headerSize, SeekOrigin.Begin);

                    while (br.BaseStream.Position + recordSize <= idxData.Length)
                    {
                        long recordStart = br.BaseStream.Position;
                        byte[] recordBytes = br.ReadBytes(recordSize);

                        // 從記錄中解析檔案名稱
                        string fileName = ExtractFileName(recordBytes, structType);

                        // 檢查是否要移除
                        bool shouldRemove = false;
                        if (fileName.EndsWith(".til", StringComparison.OrdinalIgnoreCase) && fileName != "list.til")
                        {
                            string idStr = fileName.Substring(0, fileName.Length - 4);
                            if (int.TryParse(idStr, out int tileId) && tilesToRemove.Contains(tileId))
                            {
                                shouldRemove = true;
                                removedCount++;
                                _logger.Debug($"[TileCleanup] Removing tile {tileId}");
                            }
                        }

                        if (!shouldRemove)
                        {
                            newRecords.Add(recordBytes);
                        }
                    }
                }

                // 重建 idx 檔案
                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    // 寫入 header
                    if (structType == IdxType.EXT)
                    {
                        bw.Write(Encoding.Default.GetBytes("_EXT"));
                        bw.Write(newRecords.Count);
                    }
                    else if (structType == IdxType.RMS)
                    {
                        bw.Write(Encoding.Default.GetBytes("_RMS"));
                        bw.Write(newRecords.Count);
                    }
                    else
                    {
                        bw.Write(newRecords.Count);
                    }

                    // 寫入所有保留的記錄
                    foreach (var record in newRecords)
                    {
                        bw.Write(record);
                    }

                    File.WriteAllBytes(idxFilePath, ms.ToArray());
                }

                // 清除緩存
                if (Share.IdxDataList.ContainsKey("Tile"))
                {
                    Share.IdxDataList.Remove("Tile");
                }

                _logger.Info($"[TileCleanup] Successfully removed {removedCount} tiles");
                return (removedCount, errors);
            }
            catch (Exception ex)
            {
                errors.Add($"移除 Tiles 時發生錯誤: {ex.Message}");
                _logger.Error(ex, "[TileCleanup] Error removing tiles");

                // 嘗試還原備份
                if (createBackup)
                {
                    try
                    {
                        if (File.Exists(idxBackup))
                        {
                            File.Copy(idxBackup, idxFilePath, true);
                            _logger.Info("[TileCleanup] Restored idx from backup");
                        }
                    }
                    catch (Exception restoreEx)
                    {
                        errors.Add($"還原備份時發生錯誤: {restoreEx.Message}");
                        _logger.Error(restoreEx, "[TileCleanup] Error restoring backup");
                    }
                }

                return (0, errors);
            }
        }

        private static int GetRecordSize(IdxType structType)
        {
            switch (structType)
            {
                case IdxType.EXT: return 128;
                case IdxType.RMS: return 276;
                default: return 28;
            }
        }

        private static string ExtractFileName(byte[] recordBytes, IdxType structType)
        {
            int nameOffset;
            int nameLength;

            switch (structType)
            {
                case IdxType.EXT:
                    nameOffset = 16; // Position(4) + Size(4) + CompressSize(4) + CompressType(4)
                    nameLength = 112;
                    break;
                case IdxType.RMS:
                    nameOffset = 16;
                    nameLength = 260;
                    break;
                default: // OLD
                    nameOffset = 4; // Position(4)
                    nameLength = 20;
                    break;
            }

            return Encoding.Default.GetString(recordBytes, nameOffset, nameLength).TrimEnd('\0');
        }
    }
}
