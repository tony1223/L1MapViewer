using System;
using System.Collections.Generic;

namespace L1MapViewer.Models
{
    /// <summary>
    /// fs32 格式 - 地圖打包格式 (ZIP 結構)
    /// 包含 S32 資料 + Tiles
    /// </summary>
    public class Fs32Data
    {
        public const ushort CURRENT_VERSION = 2; // v2 = ZIP 格式

        public ushort Version { get; set; } = CURRENT_VERSION;

        /// <summary>
        /// Layer 標記 (bit0-7 = Layer1-8)
        /// </summary>
        public ushort LayerFlags { get; set; } = 0xFF; // 預設全部

        /// <summary>
        /// 匯出模式
        /// </summary>
        public Fs32Mode Mode { get; set; } = Fs32Mode.WholeMap;

        /// <summary>
        /// 來源地圖 ID
        /// </summary>
        public string SourceMapId { get; set; } = string.Empty;

        // 選取區域資訊 (Mode=SelectedRegion 時使用)
        public int SelectionOriginX { get; set; }
        public int SelectionOriginY { get; set; }
        public int SelectionWidth { get; set; }
        public int SelectionHeight { get; set; }

        /// <summary>
        /// S32 區塊列表
        /// </summary>
        public List<Fs32Block> Blocks { get; set; } = new List<Fs32Block>();

        /// <summary>
        /// Tile 資料 (TileId -> TilePackageData)
        /// </summary>
        public Dictionary<int, TilePackageData> Tiles { get; set; } = new Dictionary<int, TilePackageData>();

        /// <summary>
        /// SPR 資料 (SprId -> SprPackageData)
        /// </summary>
        public Dictionary<int, SprPackageData> Sprs { get; set; } = new Dictionary<int, SprPackageData>();

        // Layer Flag 常數
        public const ushort FLAG_LAYER1 = 0x01;
        public const ushort FLAG_LAYER2 = 0x02;
        public const ushort FLAG_LAYER3 = 0x04;
        public const ushort FLAG_LAYER4 = 0x08;
        public const ushort FLAG_LAYER5 = 0x10;
        public const ushort FLAG_LAYER6 = 0x20;
        public const ushort FLAG_LAYER7 = 0x40;
        public const ushort FLAG_LAYER8 = 0x80;

        public bool HasLayer1 => (LayerFlags & FLAG_LAYER1) != 0;
        public bool HasLayer2 => (LayerFlags & FLAG_LAYER2) != 0;
        public bool HasLayer3 => (LayerFlags & FLAG_LAYER3) != 0;
        public bool HasLayer4 => (LayerFlags & FLAG_LAYER4) != 0;
        public bool HasLayer5 => (LayerFlags & FLAG_LAYER5) != 0;
        public bool HasLayer6 => (LayerFlags & FLAG_LAYER6) != 0;
        public bool HasLayer7 => (LayerFlags & FLAG_LAYER7) != 0;
        public bool HasLayer8 => (LayerFlags & FLAG_LAYER8) != 0;
    }

    /// <summary>
    /// 匯出模式
    /// </summary>
    public enum Fs32Mode : byte
    {
        /// <summary>整張地圖</summary>
        WholeMap = 0,
        /// <summary>選取的區塊</summary>
        SelectedBlocks = 1,
        /// <summary>選取的區域</summary>
        SelectedRegion = 2
    }

    /// <summary>
    /// fs32 中的 S32 區塊
    /// </summary>
    public class Fs32Block
    {
        /// <summary>區塊 X 座標</summary>
        public int BlockX { get; set; }

        /// <summary>區塊 Y 座標</summary>
        public int BlockY { get; set; }

        /// <summary>S32 原始二進位資料</summary>
        public byte[] S32Data { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// 打包的 Tile 資料
    /// </summary>
    public class TilePackageData
    {
        /// <summary>原始 Tile ID</summary>
        public int OriginalTileId { get; set; }

        /// <summary>MD5 雜湊值 (16 bytes)</summary>
        public byte[] Md5Hash { get; set; } = new byte[16];

        /// <summary>.til 檔案原始資料</summary>
        public byte[] TilData { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// fs32 ZIP 格式的 manifest.json 結構
    /// </summary>
    public class Fs32Manifest
    {
        public int Version { get; set; } = Fs32Data.CURRENT_VERSION;
        public int LayerFlags { get; set; } = 0xFF;
        public int Mode { get; set; } = 0;
        public string SourceMapId { get; set; } = string.Empty;

        // 選取區域資訊
        public int SelectionOriginX { get; set; }
        public int SelectionOriginY { get; set; }
        public int SelectionWidth { get; set; }
        public int SelectionHeight { get; set; }

        // 區塊列表 (BlockX_BlockY 格式)
        public List<string> Blocks { get; set; } = new List<string>();
    }

    /// <summary>
    /// tiles/index.json 結構
    /// </summary>
    public class TileIndex
    {
        /// <summary>TileId -> MD5 Hex</summary>
        public Dictionary<string, string> Tiles { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// 打包的 SPR 資料
    /// </summary>
    public class SprPackageData
    {
        /// <summary>原始 SPR ID</summary>
        public int SprId { get; set; }

        /// <summary>原始檔名 (如 2197-0.spr)</summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>.spr 檔案原始資料</summary>
        public byte[] SprData { get; set; } = Array.Empty<byte>();

        /// <summary>list.spr 對應的編碼文字 (Entry 格式)</summary>
        public string CodeText { get; set; } = string.Empty;
    }
}
