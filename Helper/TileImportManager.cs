using System.Collections.Generic;
using L1MapViewer.Models;
using L1MapViewer.Reader;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// Tile 匯入管理器 - 處理 Tile 對碰
    /// </summary>
    public class TileImportManager
    {
        /// <summary>
        /// 新 Tile 搜尋起始編號
        /// </summary>
        public int StartSearchId { get; set; } = 10000;

        /// <summary>
        /// idx 類型 (預設 "Tile")
        /// </summary>
        public string IdxType { get; set; } = "Tile";

        /// <summary>
        /// 本次批次匯入中已分配的 ID（用於避免重複分配）
        /// </summary>
        private HashSet<int> _assignedIds = new HashSet<int>();

        /// <summary>
        /// 處理打包的 Tiles，返回 ID 對應結果
        /// </summary>
        public TileMappingResult ProcessTiles(Dictionary<int, TilePackageData> packageTiles)
        {
            var result = new TileMappingResult();

            foreach (var kvp in packageTiles)
            {
                int originalId = kvp.Key;
                TilePackageData packageData = kvp.Value;

                ProcessSingleTile(originalId, packageData, result);
            }

            return result;
        }

        /// <summary>
        /// 處理單一 Tile
        /// </summary>
        private void ProcessSingleTile(int originalId, TilePackageData packageData, TileMappingResult result)
        {
            byte[] packageMd5 = packageData.Md5Hash;

            // 1. 檢查 Tile.pak 中是否有相同 originalId
            byte[] existingTilData = L1PakReader.UnPack(IdxType, $"{originalId}.til");

            if (existingTilData != null)
            {
                // 有相同 ID 的 Tile
                byte[] existingMd5 = TileHashManager.CalculateMd5(existingTilData);

                if (TileHashManager.CompareMd5(existingMd5, packageMd5))
                {
                    // MD5 一致 → 直接使用現有 Tile
                    result.AddMapping(originalId, originalId, TileMatchType.Exact);
                    TileHashManager.RegisterTileMd5(originalId, existingMd5);
                }
                else
                {
                    // MD5 不同 → 需要找新編號匯入
                    int newId = FindAvailableTileId();
                    ImportTile(newId, packageData.TilData);
                    result.AddMapping(originalId, newId, TileMatchType.Remapped);
                    TileHashManager.RegisterTileMd5(newId, packageMd5);
                }
            }
            else
            {
                // 不存在 originalId
                // 2. 搜尋是否有相同 MD5 的其他 Tile
                int? existingIdByMd5 = TileHashManager.FindTileByMd5(packageMd5);

                if (existingIdByMd5.HasValue)
                {
                    // 找到相同 MD5 → 使用該 ID
                    result.AddMapping(originalId, existingIdByMd5.Value, TileMatchType.MergedByMd5);
                }
                else
                {
                    // 完全不存在 → 匯入新 Tile
                    if (IsTileIdAvailable(originalId))
                    {
                        // 使用原始 ID
                        ImportTile(originalId, packageData.TilData);
                        result.AddMapping(originalId, originalId, TileMatchType.NewOriginal);
                        TileHashManager.RegisterTileMd5(originalId, packageMd5);
                    }
                    else
                    {
                        // 原始 ID 被占用，使用新 ID
                        int newId = FindAvailableTileId();
                        ImportTile(newId, packageData.TilData);
                        result.AddMapping(originalId, newId, TileMatchType.NewRemapped);
                        TileHashManager.RegisterTileMd5(newId, packageMd5);
                    }
                }
            }
        }

        /// <summary>
        /// 檢查 TileId 是否可用（檢查 pak 檔和本次已分配的 ID）
        /// </summary>
        private bool IsTileIdAvailable(int tileId)
        {
            // 檢查是否已在本次批次中分配
            if (_assignedIds.Contains(tileId))
                return false;

            // 檢查 pak 檔案中是否存在
            return !L1PakWriter.FileExists(IdxType, $"{tileId}.til");
        }

        /// <summary>
        /// 找到可用的 TileId（跳過 pak 中已存在的和本次已分配的）
        /// </summary>
        private int FindAvailableTileId()
        {
            int id = StartSearchId;
            // 同時檢查 pak 檔案和本次已分配的 ID
            while (L1PakWriter.FileExists(IdxType, $"{id}.til") || _assignedIds.Contains(id))
            {
                id++;
            }
            // 更新起始位置，下次從這裡繼續找
            StartSearchId = id + 1;
            return id;
        }

        /// <summary>
        /// 標記 ID 已被分配（用於本次批次）
        /// </summary>
        private void MarkIdAsAssigned(int tileId)
        {
            _assignedIds.Add(tileId);
        }

        /// <summary>
        /// 匯入 Tile 到 pak
        /// </summary>
        private void ImportTile(int tileId, byte[] tilData)
        {
            L1PakWriter.AppendFile(IdxType, $"{tileId}.til", tilData);
        }

        /// <summary>
        /// 批次匯入 Tiles (效能優化)
        /// </summary>
        public TileMappingResult ProcessTilesBatch(Dictionary<int, TilePackageData> packageTiles)
        {
            var result = new TileMappingResult();
            var tilesToImport = new Dictionary<string, byte[]>();

            // 清除上次批次的已分配 ID 記錄
            _assignedIds.Clear();

            foreach (var kvp in packageTiles)
            {
                int originalId = kvp.Key;
                TilePackageData packageData = kvp.Value;
                byte[] packageMd5 = packageData.Md5Hash;

                // 檢查現有 Tile
                byte[] existingTilData = L1PakReader.UnPack(IdxType, $"{originalId}.til");

                if (existingTilData != null)
                {
                    byte[] existingMd5 = TileHashManager.CalculateMd5(existingTilData);

                    if (TileHashManager.CompareMd5(existingMd5, packageMd5))
                    {
                        result.AddMapping(originalId, originalId, TileMatchType.Exact);
                        TileHashManager.RegisterTileMd5(originalId, existingMd5);
                    }
                    else
                    {
                        int newId = FindAvailableTileId();
                        MarkIdAsAssigned(newId);  // 標記已分配
                        tilesToImport[$"{newId}.til"] = packageData.TilData;
                        result.AddMapping(originalId, newId, TileMatchType.Remapped);
                        TileHashManager.RegisterTileMd5(newId, packageMd5);
                    }
                }
                else
                {
                    int? existingIdByMd5 = TileHashManager.FindTileByMd5(packageMd5);

                    if (existingIdByMd5.HasValue)
                    {
                        result.AddMapping(originalId, existingIdByMd5.Value, TileMatchType.MergedByMd5);
                    }
                    else
                    {
                        if (IsTileIdAvailable(originalId))
                        {
                            MarkIdAsAssigned(originalId);  // 標記已分配
                            tilesToImport[$"{originalId}.til"] = packageData.TilData;
                            result.AddMapping(originalId, originalId, TileMatchType.NewOriginal);
                            TileHashManager.RegisterTileMd5(originalId, packageMd5);
                        }
                        else
                        {
                            int newId = FindAvailableTileId();
                            MarkIdAsAssigned(newId);  // 標記已分配
                            tilesToImport[$"{newId}.til"] = packageData.TilData;
                            result.AddMapping(originalId, newId, TileMatchType.NewRemapped);
                            TileHashManager.RegisterTileMd5(newId, packageMd5);
                        }
                    }
                }
            }

            // 批次寫入
            if (tilesToImport.Count > 0)
            {
                L1PakWriter.AppendFiles(IdxType, tilesToImport);
            }

            return result;
        }
    }
}
