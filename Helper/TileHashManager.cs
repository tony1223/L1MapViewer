using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using L1MapViewer.Reader;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// Tile MD5 計算與快取管理
    /// </summary>
    public static class TileHashManager
    {
        // 快取：TileId -> MD5 Hash
        private static readonly ConcurrentDictionary<int, byte[]> _tileHashCache = new ConcurrentDictionary<int, byte[]>();

        // 反向快取：MD5 Hex -> TileId (用於快速查找相同 MD5)
        private static readonly ConcurrentDictionary<string, int> _md5ToTileId = new ConcurrentDictionary<string, int>();

        /// <summary>
        /// 計算資料的 MD5
        /// </summary>
        public static byte[] CalculateMd5(byte[] data)
        {
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(data);
            }
        }

        /// <summary>
        /// MD5 byte[] 轉換為 hex 字串
        /// </summary>
        public static string Md5ToHex(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// hex 字串轉換為 MD5 byte[]
        /// </summary>
        public static byte[] HexToMd5(string hex)
        {
            byte[] result = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return result;
        }

        /// <summary>
        /// 取得 Tile 的 MD5 (從快取或計算)
        /// </summary>
        public static byte[] GetTileMd5(int tileId, string idxType = "Tile")
        {
            if (_tileHashCache.TryGetValue(tileId, out byte[] cached))
            {
                return cached;
            }

            // 從 pak 讀取並計算
            byte[] tilData = L1PakReader.UnPack(idxType, $"{tileId}.til");
            if (tilData == null)
            {
                return null;
            }

            byte[] hash = CalculateMd5(tilData);
            _tileHashCache[tileId] = hash;

            // 同時更新反向快取
            string hexHash = Md5ToHex(hash);
            _md5ToTileId.TryAdd(hexHash, tileId);

            return hash;
        }

        /// <summary>
        /// 根據 MD5 查找現有的 TileId
        /// 策略：先查快取，快取沒有再掃描整個 tile.idx
        /// </summary>
        public static int? FindTileByMd5(byte[] md5Hash, string idxType = "Tile")
        {
            string hexHash = Md5ToHex(md5Hash);

            // 1. 先檢查快取（快速路徑）
            if (_md5ToTileId.TryGetValue(hexHash, out int cachedId))
            {
                return cachedId;
            }

            // 2. 快取沒有，掃描整個 tile.idx
            try
            {
                var idxData = L1IdxReader.GetAll(idxType);
                if (idxData == null || idxData.Count == 0)
                    return null;

                foreach (var entry in idxData)
                {
                    string fileName = entry.Key;
                    if (fileName.EndsWith(".til", StringComparison.OrdinalIgnoreCase) && fileName != "list.til")
                    {
                        string idStr = fileName.Substring(0, fileName.Length - 4);
                        if (int.TryParse(idStr, out int id))
                        {
                            // 跳過已經在快取中的 tile
                            if (_tileHashCache.ContainsKey(id))
                                continue;

                            // 讀取此 tile 並計算 MD5
                            byte[] tilData = L1PakReader.UnPack(idxType, fileName);
                            if (tilData != null)
                            {
                                byte[] tileMd5 = CalculateMd5(tilData);

                                // 更新快取
                                _tileHashCache[id] = tileMd5;
                                string tileHex = Md5ToHex(tileMd5);
                                _md5ToTileId.TryAdd(tileHex, id);

                                // 比對 MD5
                                if (tileHex == hexHash)
                                {
                                    return id;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略錯誤
            }

            // 3. 掃描完全部還是沒有，返回 null（需要新增）
            return null;
        }

        /// <summary>
        /// 註冊 Tile 的 MD5 到快取
        /// </summary>
        public static void RegisterTileMd5(int tileId, byte[] md5Hash)
        {
            _tileHashCache[tileId] = md5Hash;
            string hexHash = Md5ToHex(md5Hash);
            _md5ToTileId.TryAdd(hexHash, tileId);
        }

        /// <summary>
        /// 比較兩個 MD5 是否相同
        /// </summary>
        public static bool CompareMd5(byte[] hash1, byte[] hash2)
        {
            if (hash1 == null || hash2 == null)
                return false;
            if (hash1.Length != hash2.Length)
                return false;

            for (int i = 0; i < hash1.Length; i++)
            {
                if (hash1[i] != hash2[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 清除快取
        /// </summary>
        public static void ClearCache()
        {
            _tileHashCache.Clear();
            _md5ToTileId.Clear();
        }

        /// <summary>
        /// 預載入指定範圍的 Tile MD5 到快取
        /// </summary>
        public static void PreloadTileHashes(int startId, int endId, string idxType = "Tile")
        {
            for (int tileId = startId; tileId <= endId; tileId++)
            {
                GetTileMd5(tileId, idxType);
            }
        }

        /// <summary>
        /// 讀取 list.til 中的 Tile 上限值
        /// </summary>
        /// <returns>上限值，若無法讀取則返回 -1</returns>
        public static int GetTileLimit()
        {
            try
            {
                byte[] data = L1PakReader.UnPack("Tile", "list.til");
                if (data == null || data.Length == 0)
                    return -1;

                string text = System.Text.Encoding.ASCII.GetString(data).Trim();
                if (int.TryParse(text, out int limit))
                    return limit;

                return -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// 更新 list.til 中的 Tile 上限值
        /// </summary>
        /// <param name="newLimit">新的上限值</param>
        /// <returns>是否成功</returns>
        public static bool UpdateTileLimit(int newLimit)
        {
            try
            {
                string text = newLimit.ToString();
                byte[] data = System.Text.Encoding.ASCII.GetBytes(text);
                return L1PakWriter.UpdateFile("Tile", "list.til", data);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 檢查 Tile ID 是否超過上限
        /// </summary>
        /// <param name="tileId">要檢查的 Tile ID</param>
        /// <returns>是否超過上限</returns>
        public static bool IsTileIdOverLimit(int tileId)
        {
            int limit = GetTileLimit();
            if (limit <= 0)
                return false; // 無法讀取上限，不檢查

            return tileId > limit;
        }

        /// <summary>
        /// 檢查多個 Tile ID 中最大值是否超過上限
        /// </summary>
        /// <param name="tileIds">要檢查的 Tile ID 列表</param>
        /// <returns>(是否超過, 最大TileId, 目前上限)</returns>
        public static (bool IsOver, int MaxTileId, int CurrentLimit) CheckTileIdsOverLimit(IEnumerable<int> tileIds)
        {
            int limit = GetTileLimit();
            if (limit <= 0)
                return (false, 0, limit);

            int maxId = 0;
            foreach (int id in tileIds)
            {
                if (id > maxId) maxId = id;
            }

            return (maxId > limit, maxId, limit);
        }
    }
}
