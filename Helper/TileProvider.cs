using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using L1MapViewer.Converter;
using L1MapViewer.Reader;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// Tile 變化事件參數
    /// </summary>
    public class TileChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 變化的 TileId 列表
        /// </summary>
        public List<int> TileIds { get; }

        /// <summary>
        /// 是否為設定 Override
        /// </summary>
        public bool IsOverride { get; }

        /// <summary>
        /// 是否為清除操作
        /// </summary>
        public bool IsCleared { get; }

        public TileChangedEventArgs(int tileId, bool isOverride, bool isCleared)
        {
            TileIds = new List<int> { tileId };
            IsOverride = isOverride;
            IsCleared = isCleared;
        }

        public TileChangedEventArgs(List<int> tileIds, bool isOverride, bool isCleared)
        {
            TileIds = tileIds ?? new List<int>();
            IsOverride = isOverride;
            IsCleared = isCleared;
        }
    }

    /// <summary>
    /// Tile 提供者 - 統一處理 tile 載入、快取和 override
    /// </summary>
    public class TileProvider
    {
        /// <summary>
        /// 全域單例
        /// </summary>
        public static TileProvider Instance { get; } = new TileProvider();

        /// <summary>
        /// Tile 變化事件（設定或清除 override 時觸發）
        /// </summary>
        public event EventHandler<TileChangedEventArgs> TileChanged;

        // Tile 檔案快取 - key: tileId, value: parsed tile array
        private readonly ConcurrentDictionary<int, List<byte[]>> _tilFileCache = new ConcurrentDictionary<int, List<byte[]>>();

        // R 版 tile 快取 - key: tileId, value: isRemaster
        private readonly ConcurrentDictionary<int, bool> _tilRemasterCache = new ConcurrentDictionary<int, bool>();

        // Tile Override 快取 - 暫時替換 til 顯示（不存檔）
        private readonly Dictionary<int, List<byte[]>> _tileOverrideCache = new Dictionary<int, List<byte[]>>();

        // Override 鎖
        private readonly object _overrideLock = new object();

        /// <summary>
        /// 取得 til 資料（優先使用 Override）
        /// </summary>
        public List<byte[]> GetTilArray(int tileId)
        {
            // 優先檢查 Override
            lock (_overrideLock)
            {
                if (_tileOverrideCache.TryGetValue(tileId, out var overrideArray))
                    return overrideArray;
            }

            // 否則從快取/PAK載入
            return _tilFileCache.GetOrAdd(tileId, LoadTilFromPak);
        }

        /// <summary>
        /// 取得 til 資料，帶備援機制
        /// </summary>
        public List<byte[]> GetTilArrayWithFallback(int tileId, int indexId, int pixelX, out int adjustedIndexId)
        {
            adjustedIndexId = indexId;
            var tilArray = GetTilArray(tileId);

            // 備援機制：當 tilArray 為 null 或 indexId 越界時
            if (tilArray == null || indexId >= tilArray.Count)
            {
                if (tileId != 0)
                {
                    // 載入 0.til 作為預設填補（備援不檢查 override）
                    tilArray = _tilFileCache.GetOrAdd(0, LoadTilFromPak);
                    if (tilArray == null || tilArray.Count == 0)
                    {
                        adjustedIndexId = -1;
                        return null;
                    }
                    // 使用 187 或 188 作為預設 indexId
                    adjustedIndexId = 187 + ((pixelX / 24) & 1);
                    if (adjustedIndexId >= tilArray.Count)
                        adjustedIndexId = adjustedIndexId % tilArray.Count;
                }
                else
                {
                    // TileId=0 時，對 tilArray.Count 取模
                    if (tilArray != null && tilArray.Count > 0)
                        adjustedIndexId = indexId % tilArray.Count;
                    else
                    {
                        adjustedIndexId = -1;
                        return null;
                    }
                }
            }

            return tilArray;
        }

        /// <summary>
        /// 檢查 tile 是否為 Remaster 版本
        /// </summary>
        public bool IsRemaster(int tileId)
        {
            // Override 的情況，需要檢查 override 資料
            lock (_overrideLock)
            {
                if (_tileOverrideCache.ContainsKey(tileId))
                {
                    // Override 的資料，暫時視為非 Remaster
                    return false;
                }
            }

            return _tilRemasterCache.GetOrAdd(tileId, id =>
            {
                byte[] rawData = L1PakReader.UnPack("Tile", $"{id}.til");
                return rawData != null && L1Til.IsRemaster(rawData);
            });
        }

        /// <summary>
        /// 從 PAK 載入 til 檔案
        /// </summary>
        private List<byte[]> LoadTilFromPak(int tileId)
        {
            string key = $"{tileId}.til";
            byte[] data = L1PakReader.UnPack("Tile", key);
            if (data == null) return null;
            return L1Til.Parse(data);
        }

        #region Override 管理

        /// <summary>
        /// 設定 Tile Override
        /// </summary>
        public void SetOverride(int tileId, List<byte[]> tilArray)
        {
            lock (_overrideLock)
            {
                _tileOverrideCache[tileId] = tilArray;
            }

            // 清除該 tileId 的快取（讓下次重新載入時使用 override）
            _tilFileCache.TryRemove(tileId, out _);
            _tilRemasterCache.TryRemove(tileId, out _);

            // 觸發事件
            OnTileChanged(new TileChangedEventArgs(tileId, true, false));
        }

        /// <summary>
        /// 清除指定 Tile Override
        /// </summary>
        public void ClearOverride(int tileId)
        {
            lock (_overrideLock)
            {
                _tileOverrideCache.Remove(tileId);
            }

            // 清除快取讓它從 PAK 重新載入
            _tilFileCache.TryRemove(tileId, out _);
            _tilRemasterCache.TryRemove(tileId, out _);

            // 觸發事件
            OnTileChanged(new TileChangedEventArgs(tileId, false, true));
        }

        /// <summary>
        /// 清除所有 Tile Override
        /// </summary>
        public List<int> ClearAllOverrides()
        {
            List<int> clearedIds;
            lock (_overrideLock)
            {
                clearedIds = new List<int>(_tileOverrideCache.Keys);
                _tileOverrideCache.Clear();
            }

            // 清除這些 tile 的快取
            foreach (var tileId in clearedIds)
            {
                _tilFileCache.TryRemove(tileId, out _);
                _tilRemasterCache.TryRemove(tileId, out _);
            }

            // 觸發事件（傳遞清除的 tileId 列表）
            if (clearedIds.Count > 0)
            {
                OnTileChanged(new TileChangedEventArgs(clearedIds, false, true));
            }

            return clearedIds;
        }

        /// <summary>
        /// 取得目前 Override 的 TileId 列表
        /// </summary>
        public List<int> GetOverrideIds()
        {
            lock (_overrideLock)
            {
                return new List<int>(_tileOverrideCache.Keys);
            }
        }

        /// <summary>
        /// 取得 Override 數量
        /// </summary>
        public int OverrideCount
        {
            get
            {
                lock (_overrideLock)
                {
                    return _tileOverrideCache.Count;
                }
            }
        }

        /// <summary>
        /// 檢查是否有 Override
        /// </summary>
        public bool HasOverride(int tileId)
        {
            lock (_overrideLock)
            {
                return _tileOverrideCache.ContainsKey(tileId);
            }
        }

        #endregion

        #region 快取管理

        /// <summary>
        /// 清除所有快取（不包括 Override）
        /// </summary>
        public void ClearCache()
        {
            _tilFileCache.Clear();
            _tilRemasterCache.Clear();
        }

        /// <summary>
        /// 清除指定 tileId 的快取
        /// </summary>
        public void ClearCache(int tileId)
        {
            _tilFileCache.TryRemove(tileId, out _);
            _tilRemasterCache.TryRemove(tileId, out _);
        }

        /// <summary>
        /// 預載入 tile
        /// </summary>
        public void Preload(int tileId)
        {
            GetTilArray(tileId);
        }

        #endregion

        #region 事件

        /// <summary>
        /// 觸發 TileChanged 事件
        /// </summary>
        protected virtual void OnTileChanged(TileChangedEventArgs e)
        {
            TileChanged?.Invoke(this, e);
        }

        #endregion
    }
}
