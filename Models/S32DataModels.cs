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
        public string FilePath { get; set; }
        public string DisplayName { get; set; }
        public Struct.L1MapSeg SegInfo { get; set; }
        public bool IsChecked { get; set; } = true;

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// S32 資料結構
    /// </summary>
    public class S32Data
    {
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
        public byte[] OriginalFileData { get; set; }

        // 記錄各層在文件中的位置
        public int Layer1Offset { get; set; }
        public int Layer2Offset { get; set; }
        public int Layer3Offset { get; set; }
        public int Layer4Offset { get; set; }
        public int Layer4EndOffset { get; set; }

        // 第5-8層的原始資料（未解析）
        public byte[] Layer5to8Data { get; set; }

        // 第5層 - 可透明化的圖塊 (X, Y, R, G, B)
        public List<Layer5Item> Layer5 { get; set; } = new List<Layer5Item>();

        // 第6層 - 使用的 til
        public List<int> Layer6 { get; set; } = new List<int>();

        // 第7層 - 傳送點、入口點
        public List<Layer7Item> Layer7 { get; set; } = new List<Layer7Item>();

        // 第8層 - 特效、裝飾品
        public List<Layer8Item> Layer8 { get; set; } = new List<Layer8Item>();

        // 檔案路徑和 SegInfo
        public string FilePath { get; set; }
        public Struct.L1MapSeg SegInfo { get; set; }

        // 是否已修改
        public bool IsModified { get; set; }
    }

    /// <summary>
    /// 第二層項目
    /// </summary>
    public class Layer2Item
    {
        public byte Value1 { get; set; }
        public byte Value2 { get; set; }
        public int Value3 { get; set; }
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
    /// 第五層項目 - 可透明化的圖塊
    /// </summary>
    public class Layer5Item
    {
        public byte X { get; set; }
        public byte Y { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
    }

    /// <summary>
    /// 第七層項目 - 傳送點、入口點
    /// </summary>
    public class Layer7Item
    {
        public string Name { get; set; }
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
        public int Unknown { get; set; }
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
        public System.Drawing.Bitmap Thumbnail { get; set; }
        public int UsageCount { get; set; }
    }

    /// <summary>
    /// 選中的格子
    /// </summary>
    public class SelectedCell
    {
        public S32Data S32Data { get; set; }
        public int LocalX { get; set; }
        public int LocalY { get; set; }
    }

    /// <summary>
    /// 複製的格子資料（支援多層）
    /// </summary>
    public class CopiedCellData
    {
        public int RelativeX { get; set; }
        public int RelativeY { get; set; }
        public TileCell Layer1Cell1 { get; set; }
        public TileCell Layer1Cell2 { get; set; }
        public MapAttribute Layer3Attr { get; set; }
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
    }

    /// <summary>
    /// Undo 動作記錄
    /// </summary>
    public class UndoAction
    {
        public string Description { get; set; }
        public List<UndoObjectInfo> AddedObjects { get; set; } = new List<UndoObjectInfo>();
        public List<UndoObjectInfo> RemovedObjects { get; set; } = new List<UndoObjectInfo>();
        public List<UndoLayer7Info> RemovedLayer7Items { get; set; } = new List<UndoLayer7Info>();
    }

    /// <summary>
    /// Undo 物件資訊
    /// </summary>
    public class UndoObjectInfo
    {
        public string S32FilePath { get; set; }
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
        public string S32FilePath { get; set; }
        public string Name { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }
        public ushort TargetMapId { get; set; }
        public int PortalId { get; set; }
    }
}
