using System;
using System.Collections.Generic;
using L1MapViewer.Other;

namespace L1MapViewer.Models
{
    /// <summary>
    /// S32 檔案資訊類別
    /// </summary>
    public class S32FileItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public Struct.L1MapSeg SegInfo { get; set; }
        public bool IsChecked { get; set; } = true;

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// S32 資料結構
    /// </summary>
    public class S32Data
    {
        /// <summary>
        /// 標準寬度 (Layer1 本地座標)
        /// </summary>
        public const int StandardWidth = 128;

        /// <summary>
        /// 標準高度 (Layer1 本地座標)
        /// </summary>
        public const int StandardHeight = 64;

        // 第一層（地板）- 64x128
        public TileCell[,] Layer1 { get; set; } = new TileCell[64, 128];

        // 第二層資料
        public List<Layer2Item> Layer2 { get; set; } = new List<Layer2Item>();

        // 第三層（地圖屬性）- 64x64
        public MapAttribute[,] Layer3 { get; set; } = new MapAttribute[64, 64];

        // 第四層（物件）
        public List<ObjectTile> Layer4 { get; set; } = new List<ObjectTile>();

        // 使用的所有 tile（不重複）
        public Dictionary<int, TileInfo> UsedTiles { get; set; } = new Dictionary<int, TileInfo>();

        // 保存原始文件內容，用於安全保存
        public byte[] OriginalFileData { get; set; } = Array.Empty<byte>();

        // 記錄各層在文件中的位置
        public int Layer1Offset { get; set; }
        public int Layer2Offset { get; set; }
        public int Layer3Offset { get; set; }
        public int Layer4Offset { get; set; }
        public int Layer4EndOffset { get; set; }

        // 第5-8層的原始資料（未解析）
        public byte[] Layer5to8Data { get; set; } = Array.Empty<byte>();

        // 第5層 - 可透明化的圖塊 (X, Y, R, G, B)
        public List<Layer5Item> Layer5 { get; set; } = new List<Layer5Item>();

        // 第6層 - 使用的 til
        public List<int> Layer6 { get; set; } = new List<int>();

        // 第7層 - 傳送點、入口點
        public List<Layer7Item> Layer7 { get; set; } = new List<Layer7Item>();

        // 第8層 - 特效、裝飾品
        public List<Layer8Item> Layer8 { get; set; } = new List<Layer8Item>();

        // 第8層擴展資訊
        public bool Layer8HasExtendedData { get; set; } = false;

        // 檔案路徑和 SegInfo
        public string FilePath { get; set; } = string.Empty;
        public Struct.L1MapSeg SegInfo { get; set; }

        // 是否已修改
        public bool IsModified { get; set; }

        /// <summary>
        /// 實際資料寬度 - Layer1 本地座標系統
        /// 包含 L2, L4, L5, L8 超出標準範圍的資料
        /// </summary>
        public int RealLocalWidth { get; set; } = StandardWidth;

        /// <summary>
        /// 實際資料高度 - Layer1 本地座標系統
        /// </summary>
        public int RealLocalHeight { get; set; } = StandardHeight;

        /// <summary>
        /// 實際資料寬度 - 遊戲座標系統
        /// </summary>
        public int RealGameWidth => (RealLocalWidth + 1) / 2;

        /// <summary>
        /// 實際資料高度 - 遊戲座標系統
        /// </summary>
        public int RealGameHeight => RealLocalHeight;

        /// <summary>
        /// 實際遊戲座標終點 X
        /// </summary>
        public int RealGameEndX => SegInfo != null ? SegInfo.nLinBeginX + RealGameWidth - 1 : -1;

        /// <summary>
        /// 實際遊戲座標終點 Y
        /// </summary>
        public int RealGameEndY => SegInfo != null ? SegInfo.nLinBeginY + RealGameHeight - 1 : -1;

        /// <summary>
        /// 計算實際邊界 (根據各層資料的最大 X, Y)
        /// </summary>
        public void CalculateRealBounds()
        {
            int maxX = StandardWidth - 1;   // 127
            int maxY = StandardHeight - 1;  // 63

            // Layer2 (byte X, Y)
            foreach (var item in Layer2)
            {
                if (item.X > maxX) maxX = item.X;
                if (item.Y > maxY) maxY = item.Y;
            }

            // Layer4 (int X, Y - 從 byte 解析)
            foreach (var item in Layer4)
            {
                if (item.X > maxX) maxX = item.X;
                if (item.Y > maxY) maxY = item.Y;
            }

            // Layer5 (byte X, Y)
            foreach (var item in Layer5)
            {
                if (item.X > maxX) maxX = item.X;
                if (item.Y > maxY) maxY = item.Y;
            }

            // Layer8 (ushort X, Y - 可能很大)
            foreach (var item in Layer8)
            {
                if (item.X > maxX) maxX = item.X;
                if (item.Y > maxY) maxY = item.Y;
            }

            RealLocalWidth = maxX + 1;
            RealLocalHeight = maxY + 1;
        }

        /// <summary>
        /// 檢查遊戲座標是否在實際範圍內
        /// </summary>
        public bool ContainsGameCoord(int gameX, int gameY)
        {
            if (SegInfo == null) return false;
            return gameX >= SegInfo.nLinBeginX && gameX <= RealGameEndX &&
                   gameY >= SegInfo.nLinBeginY && gameY <= RealGameEndY;
        }

        /// <summary>
        /// 檢查本地座標是否在實際範圍內
        /// </summary>
        public bool ContainsLocalCoord(int localX, int localY)
        {
            return localX >= 0 && localX < RealLocalWidth &&
                   localY >= 0 && localY < RealLocalHeight;
        }
    }

    /// <summary>
    /// 第二層項目 - X(BYTE), Y(BYTE), IndexId(BYTE), TileId(USHORT), UK(BYTE)
    /// </summary>
    public class Layer2Item
    {
        public byte X { get; set; }
        public byte Y { get; set; }
        public byte IndexId { get; set; }
        public ushort TileId { get; set; }
        public byte UK { get; set; }
    }

    /// <summary>
    /// 地圖屬性（第三層）
    /// </summary>
    public class MapAttribute
    {
        public short Attribute1 { get; set; }
        public short Attribute2 { get; set; }
    }

    /// <summary>
    /// 物件 Tile（第四層）
    /// </summary>
    public class ObjectTile
    {
        public int GroupId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Layer { get; set; }
        public int IndexId { get; set; }
        public int TileId { get; set; }
    }

    /// <summary>
    /// 第五層項目 - 事件
    /// </summary>
    public class Layer5Item
    {
        public byte X { get; set; }
        public byte Y { get; set; }
        public ushort ObjectIndex { get; set; }
        public byte Type { get; set; }
    }

    /// <summary>
    /// 第七層項目 - 傳送點、入口點
    /// </summary>
    public class Layer7Item
    {
        public string Name { get; set; } = string.Empty;
        public byte X { get; set; }
        public byte Y { get; set; }
        public ushort TargetMapId { get; set; }
        public int PortalId { get; set; }
    }

    /// <summary>
    /// 第八層項目 - 特效、裝飾品
    /// </summary>
    public class Layer8Item
    {
        public ushort SprId { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public int ExtendedData { get; set; }  // 僅當 HasExtendedData 為 true 時使用
    }

    /// <summary>
    /// S32Data 的 Layer8 擴展資訊
    /// </summary>
    public class Layer8Info
    {
        public bool HasExtendedData { get; set; }  // 數量高位為 1 時為 true
        public List<Layer8Item> Items { get; set; } = new List<Layer8Item>();
    }

    /// <summary>
    /// 格子資料
    /// </summary>
    public class TileCell
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int TileId { get; set; }
        public int IndexId { get; set; }
        public bool IsModified { get; set; }
    }

    /// <summary>
    /// Tile 資訊
    /// </summary>
    public class TileInfo
    {
        public int TileId { get; set; }
        public int IndexId { get; set; }
        public Bitmap? Thumbnail { get; set; }
        public int UsageCount { get; set; }
    }

    /// <summary>
    /// 選中的格子
    /// </summary>
    public class SelectedCell
    {
        public S32Data S32Data { get; set; } = null!;
        public int LocalX { get; set; }
        public int LocalY { get; set; }

        /// <summary>
        /// 取得此格子對應的 Layer3 資訊（正確的 S32 和本地座標）
        /// </summary>
        /// <param name="allS32Files">所有 S32 檔案的字典</param>
        /// <returns>包含正確 S32 和本地座標的 Layer3Info，如果找不到則返回 null</returns>
        public Layer3Info? GetLayer3Info(Dictionary<string, S32Data> allS32Files)
        {
            if (S32Data == null) return null;

            // 計算相對於來源 S32 的 Layer3 座標
            int layer3X = LocalX / 2;
            int layer3Y = LocalY;

            // 計算遊戲座標
            int gameX = S32Data.SegInfo.nLinBeginX + layer3X;
            int gameY = S32Data.SegInfo.nLinBeginY + layer3Y;

            // 找到包含此遊戲座標的正確 S32
            S32Data? targetS32 = null;

            // 先檢查當前 S32 是否包含此座標
            if (gameX >= S32Data.SegInfo.nLinBeginX && gameX < S32Data.SegInfo.nLinBeginX + 64 &&
                gameY >= S32Data.SegInfo.nLinBeginY && gameY < S32Data.SegInfo.nLinBeginY + 64)
            {
                targetS32 = S32Data;
            }
            else
            {
                // 查找其他 S32
                foreach (var s32 in allS32Files.Values)
                {
                    if (gameX >= s32.SegInfo.nLinBeginX && gameX < s32.SegInfo.nLinBeginX + 64 &&
                        gameY >= s32.SegInfo.nLinBeginY && gameY < s32.SegInfo.nLinBeginY + 64)
                    {
                        targetS32 = s32;
                        break;
                    }
                }
            }

            if (targetS32 == null) return null;

            // 計算在目標 S32 中的本地座標
            int targetLocalX = gameX - targetS32.SegInfo.nLinBeginX;
            int targetLocalY = gameY - targetS32.SegInfo.nLinBeginY;

            return new Layer3Info
            {
                S32Data = targetS32,
                LocalX = targetLocalX,
                LocalY = targetLocalY,
                GameX = gameX,
                GameY = gameY
            };
        }
    }

    /// <summary>
    /// Layer3 資訊（遊戲座標系統，包含正確的 S32 和 Layer3 本地座標 0-63）
    /// </summary>
    public class Layer3Info
    {
        /// <summary>包含此座標的 S32 檔案</summary>
        public S32Data S32Data { get; set; } = null!;
        /// <summary>在 S32 Layer3 中的 X 座標 (0-63)</summary>
        public int LocalX { get; set; }
        /// <summary>在 S32 Layer3 中的 Y 座標 (0-63)</summary>
        public int LocalY { get; set; }
        /// <summary>遊戲座標 X（絕對座標）</summary>
        public int GameX { get; set; }
        /// <summary>遊戲座標 Y（絕對座標）</summary>
        public int GameY { get; set; }
    }

    /// <summary>
    /// 複製的格子資料（支援多層）
    /// </summary>
    public class CopiedCellData
    {
        public int RelativeX { get; set; }
        public int RelativeY { get; set; }
        public TileCell? Layer1Cell1 { get; set; }
        public TileCell? Layer1Cell2 { get; set; }
        public MapAttribute? Layer3Attr { get; set; }
        public List<CopiedObjectTile> Layer4Objects { get; set; } = new List<CopiedObjectTile>();
    }

    /// <summary>
    /// 複製的物件資料（含相對位置）
    /// </summary>
    public class CopiedObjectTile
    {
        public int RelativeX { get; set; }
        public int RelativeY { get; set; }
        public int GroupId { get; set; }
        public int Layer { get; set; }
        public int IndexId { get; set; }
        public int TileId { get; set; }
        public int OriginalIndex { get; set; }
        // Layer1 座標系統 (0-127) 的原始局部座標
        public int OriginalLocalLayer1X { get; set; }
        public int OriginalLocalY { get; set; }
    }

    /// <summary>
    /// Undo 動作記錄
    /// </summary>
    public class UndoAction
    {
        public string Description { get; set; } = string.Empty;
        public List<UndoObjectInfo> AddedObjects { get; set; } = new List<UndoObjectInfo>();
        public List<UndoObjectInfo> RemovedObjects { get; set; } = new List<UndoObjectInfo>();
        public List<UndoLayer2Info> AddedLayer2Items { get; set; } = new List<UndoLayer2Info>();
        public List<UndoLayer7Info> RemovedLayer7Items { get; set; } = new List<UndoLayer7Info>();
        public List<UndoLayer1Info> ModifiedLayer1 { get; set; } = new List<UndoLayer1Info>();
        public List<UndoLayer3Info> ModifiedLayer3 { get; set; } = new List<UndoLayer3Info>();
    }

    /// <summary>
    /// Undo 第一層資訊（地板）
    /// </summary>
    public class UndoLayer1Info
    {
        public string S32FilePath { get; set; } = string.Empty;
        public int LocalX { get; set; }
        public int LocalY { get; set; }
        public int OldTileId { get; set; }
        public int OldIndexId { get; set; }
        public int NewTileId { get; set; }
        public int NewIndexId { get; set; }
    }

    /// <summary>
    /// Undo 第二層資訊（Tile索引表）
    /// </summary>
    public class UndoLayer2Info
    {
        public string S32FilePath { get; set; } = string.Empty;
        public byte X { get; set; }
        public byte Y { get; set; }
        public int IndexId { get; set; }
        public int TileId { get; set; }
        public int UK { get; set; }
    }

    /// <summary>
    /// Undo 第三層資訊（屬性）
    /// </summary>
    public class UndoLayer3Info
    {
        public string S32FilePath { get; set; } = string.Empty;
        public int LocalX { get; set; }
        public int LocalY { get; set; }
        public short OldAttribute1 { get; set; }
        public short OldAttribute2 { get; set; }
        public short NewAttribute1 { get; set; }
        public short NewAttribute2 { get; set; }
    }

    /// <summary>
    /// Undo 物件資訊
    /// </summary>
    public class UndoObjectInfo
    {
        public string S32FilePath { get; set; } = string.Empty;
        public int GameX { get; set; }
        public int GameY { get; set; }
        public int LocalX { get; set; }
        public int LocalY { get; set; }
        public int GroupId { get; set; }
        public int Layer { get; set; }
        public int IndexId { get; set; }
        public int TileId { get; set; }
    }

    /// <summary>
    /// Undo 第七層資訊（傳送點）
    /// </summary>
    public class UndoLayer7Info
    {
        public string S32FilePath { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public byte X { get; set; }
        public byte Y { get; set; }
        public ushort TargetMapId { get; set; }
        public int PortalId { get; set; }
    }
}
