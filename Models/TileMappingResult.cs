using System.Collections.Generic;

namespace L1MapViewer.Models
{
    /// <summary>
    /// Tile 對碰處理結果
    /// </summary>
    public class TileMappingResult
    {
        /// <summary>
        /// ID 對應表 (OriginalId -> NewId)
        /// </summary>
        public Dictionary<int, int> IdMapping { get; } = new Dictionary<int, int>();

        /// <summary>詳細對應資訊</summary>
        public List<TileMapping> Details { get; } = new List<TileMapping>();

        /// <summary>直接使用現有的數量 (MD5 一致)</summary>
        public int ReuseCount { get; set; }

        /// <summary>重新分配編號的數量 (ID 衝突)</summary>
        public int RemappedCount { get; set; }

        /// <summary>新匯入的數量</summary>
        public int ImportedCount { get; set; }

        /// <summary>
        /// 新增對應
        /// </summary>
        public void AddMapping(int originalId, int newId, TileMatchType matchType)
        {
            IdMapping[originalId] = newId;
            Details.Add(new TileMapping(originalId, newId, matchType));

            switch (matchType)
            {
                case TileMatchType.Exact:
                case TileMatchType.MergedByMd5:
                    ReuseCount++;
                    break;
                case TileMatchType.Remapped:
                    RemappedCount++;
                    break;
                case TileMatchType.NewOriginal:
                case TileMatchType.NewRemapped:
                    ImportedCount++;
                    break;
            }
        }

        /// <summary>
        /// 取得新的 TileId
        /// </summary>
        public int GetNewTileId(int originalId)
        {
            return IdMapping.TryGetValue(originalId, out int newId) ? newId : originalId;
        }
    }

    /// <summary>
    /// 單一 Tile 的對應資訊
    /// </summary>
    public class TileMapping
    {
        public int OriginalId { get; }
        public int NewId { get; }
        public TileMatchType MatchType { get; }

        public TileMapping(int originalId, int newId, TileMatchType matchType)
        {
            OriginalId = originalId;
            NewId = newId;
            MatchType = matchType;
        }
    }

    /// <summary>
    /// Tile 對碰類型
    /// </summary>
    public enum TileMatchType
    {
        /// <summary>ID 和 MD5 都一致，直接使用</summary>
        Exact,

        /// <summary>ID 相同但 MD5 不同，已重新分配編號</summary>
        Remapped,

        /// <summary>找到相同 MD5 的現有 Tile，使用該 ID</summary>
        MergedByMd5,

        /// <summary>新匯入，使用原始 ID</summary>
        NewOriginal,

        /// <summary>新匯入，原始 ID 被占用，使用新 ID</summary>
        NewRemapped
    }
}
