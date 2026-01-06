using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;

namespace L1MapViewer.Models
{
    /// <summary>
    /// 渲染快取 - 管理各種 Bitmap 和 Tile 資料快取
    /// </summary>
    public class RenderCache : IDisposable
    {
        /// <summary>
        /// Viewport Bitmap 鎖
        /// </summary>
        public readonly object ViewportBitmapLock = new object();

        /// <summary>
        /// 當前渲染的 Viewport Bitmap
        /// </summary>
        public Bitmap ViewportBitmap { get; set; }

        // 小地圖 Bitmap 已移至 MiniMapControl 內部管理

        /// <summary>
        /// Layer8 SPR 動畫快取 - key: sprId, value: frames
        /// </summary>
        public Dictionary<int, List<Image>> Layer8SprCache { get; } = new Dictionary<int, List<Image>>();

        /// <summary>
        /// Layer8 動畫幀索引 - key: (s32Path, index), value: current frame
        /// </summary>
        public Dictionary<(string, int), int> Layer8AnimFrame { get; } = new Dictionary<(string, int), int>();

        /// <summary>
        /// S32 區塊渲染快取 - key: "s32Path_zoomLevel", value: rendered bitmap
        /// </summary>
        public ConcurrentDictionary<string, Bitmap> S32BlockCache { get; } = new ConcurrentDictionary<string, Bitmap>();

        /// <summary>
        /// Tile 資料快取 - key: "tileId_indexId", value: tile bytes
        /// </summary>
        public ConcurrentDictionary<string, byte[]> TileDataCache { get; } = new ConcurrentDictionary<string, byte[]>();

        /// <summary>
        /// 整個 .til 檔案快取 - key: tileId, value: parsed tile array
        /// </summary>
        public ConcurrentDictionary<int, List<byte[]>> TilFileCache { get; } = new ConcurrentDictionary<int, List<byte[]>>();

        /// <summary>
        /// R 版 tile 快取 - key: tileId, value: isRemaster
        /// </summary>
        public ConcurrentDictionary<int, bool> TilRemasterCache { get; } = new ConcurrentDictionary<int, bool>();

        /// <summary>
        /// Tile Override 快取 - 暫時替換 til 顯示（不存檔）
        /// key: tileId, value: parsed tile array
        /// </summary>
        public Dictionary<int, List<byte[]>> TileOverrideCache { get; } = new Dictionary<int, List<byte[]>>();

        /// <summary>
        /// 檢查是否有 Tile Override
        /// </summary>
        public bool HasTileOverride(int tileId) => TileOverrideCache.ContainsKey(tileId);

        /// <summary>
        /// 取得 til 資料（優先使用 Override）
        /// </summary>
        public List<byte[]> GetTilArray(int tileId, Func<int, List<byte[]>> loadFromPak)
        {
            // 優先檢查 Override
            if (TileOverrideCache.TryGetValue(tileId, out var overrideArray))
                return overrideArray;

            // 否則從快取/PAK載入
            return TilFileCache.GetOrAdd(tileId, loadFromPak);
        }

        /// <summary>
        /// 清除 Tile Override 快取
        /// </summary>
        public void ClearTileOverride()
        {
            TileOverrideCache.Clear();
        }

        /// <summary>
        /// 快取命中次數
        /// </summary>
        public int CacheHits { get; set; }

        /// <summary>
        /// 快取未命中次數
        /// </summary>
        public int CacheMisses { get; set; }

        /// <summary>
        /// 清除 Viewport Bitmap（線程安全）
        /// </summary>
        public void ClearViewportBitmap()
        {
            lock (ViewportBitmapLock)
            {
                ViewportBitmap?.Dispose();
                ViewportBitmap = null;
            }
        }

        // ClearMiniMapBitmap 已移除 - 小地圖由 MiniMapControl 管理

        /// <summary>
        /// 清除 S32 區塊快取
        /// </summary>
        public void ClearS32BlockCache()
        {
            foreach (var kvp in S32BlockCache)
            {
                kvp.Value?.Dispose();
            }
            S32BlockCache.Clear();
        }

        /// <summary>
        /// 清除 Layer8 SPR 快取
        /// </summary>
        public void ClearLayer8Cache()
        {
            foreach (var kvp in Layer8SprCache)
            {
                foreach (var img in kvp.Value)
                {
                    img?.Dispose();
                }
            }
            Layer8SprCache.Clear();
            Layer8AnimFrame.Clear();
        }

        /// <summary>
        /// 清除所有快取
        /// </summary>
        public void ClearAll()
        {
            ClearViewportBitmap();
            ClearS32BlockCache();
            ClearLayer8Cache();
            TileDataCache.Clear();
            TilFileCache.Clear();
            TilRemasterCache.Clear();
            CacheHits = 0;
            CacheMisses = 0;
        }

        /// <summary>
        /// 取得快取統計資訊
        /// </summary>
        public string GetCacheStats()
        {
            int total = CacheHits + CacheMisses;
            double hitRate = total > 0 ? (double)CacheHits / total * 100 : 0;
            return $"Hits: {CacheHits}, Misses: {CacheMisses}, Rate: {hitRate:F1}%";
        }

        public void Dispose()
        {
            ClearAll();
        }
    }
}
