using System;
using System.Collections.Concurrent;
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
        /// </summary>
        public static int? FindTileByMd5(byte[] md5Hash)
        {
            string hexHash = Md5ToHex(md5Hash);
            if (_md5ToTileId.TryGetValue(hexHash, out int tileId))
            {
                return tileId;
            }
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
    }
}
