using System;
using System.Collections.Generic;

namespace L1MapViewer.Models
{
    /// <summary>
    /// fs3p 格式 - 素材庫格式
    /// 可選 Layer 1-4 + Tiles，用於跨地圖共享
    /// </summary>
    public class Fs3pData
    {
        public const uint MAGIC = 0x50335346; // "FS3P" little-endian
        public const ushort CURRENT_VERSION = 1;

        public ushort Version { get; set; } = CURRENT_VERSION;

        /// <summary>
        /// Layer 標記 (bit0=L1, bit1=L2, bit2=L3, bit3=L4)
        /// </summary>
        public ushort LayerFlags { get; set; } = 0x0F; // 預設 Layer1-4

        /// <summary>素材名稱</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>縮圖 PNG 資料 (可為空)</summary>
        public byte[] ThumbnailPng { get; set; } = Array.Empty<byte>();

        // 範圍資訊 (相對座標系統)
        public int OriginOffsetX { get; set; }
        public int OriginOffsetY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        // 各層資料
        public List<Fs3pLayer1Item> Layer1Items { get; set; } = new List<Fs3pLayer1Item>();
        public List<Fs3pLayer2Item> Layer2Items { get; set; } = new List<Fs3pLayer2Item>();
        public List<Fs3pLayer3Item> Layer3Items { get; set; } = new List<Fs3pLayer3Item>();
        public List<Fs3pLayer4Item> Layer4Items { get; set; } = new List<Fs3pLayer4Item>();

        /// <summary>
        /// Tile 資料 (TileId -> TilePackageData)
        /// </summary>
        public Dictionary<int, TilePackageData> Tiles { get; set; } = new Dictionary<int, TilePackageData>();

        // Metadata
        public long CreatedTime { get; set; }
        public long ModifiedTime { get; set; }
        public List<string> Tags { get; set; } = new List<string>();

        // Layer Flag 常數
        public const ushort FLAG_LAYER1 = 0x01;
        public const ushort FLAG_LAYER2 = 0x02;
        public const ushort FLAG_LAYER3 = 0x04;
        public const ushort FLAG_LAYER4 = 0x08;

        public bool HasLayer1 => (LayerFlags & FLAG_LAYER1) != 0;
        public bool HasLayer2 => (LayerFlags & FLAG_LAYER2) != 0;
        public bool HasLayer3 => (LayerFlags & FLAG_LAYER3) != 0;
        public bool HasLayer4 => (LayerFlags & FLAG_LAYER4) != 0;

        /// <summary>
        /// 設定建立時間為現在
        /// </summary>
        public void SetCreatedNow()
        {
            CreatedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ModifiedTime = CreatedTime;
        }

        /// <summary>
        /// 設定修改時間為現在
        /// </summary>
        public void SetModifiedNow()
        {
            ModifiedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    /// <summary>
    /// fs3p Layer1 項目 (地板)
    /// </summary>
    public class Fs3pLayer1Item
    {
        public int RelativeX { get; set; }
        public int RelativeY { get; set; }
        public byte IndexId { get; set; }
        public ushort TileId { get; set; }
    }

    /// <summary>
    /// fs3p Layer2 項目 (裝飾)
    /// </summary>
    public class Fs3pLayer2Item
    {
        public int RelativeX { get; set; }
        public int RelativeY { get; set; }
        public byte IndexId { get; set; }
        public ushort TileId { get; set; }
        public byte UK { get; set; }
    }

    /// <summary>
    /// fs3p Layer3 項目 (屬性/通行性)
    /// </summary>
    public class Fs3pLayer3Item
    {
        public int RelativeX { get; set; }
        public int RelativeY { get; set; }
        public short Attribute1 { get; set; }
        public short Attribute2 { get; set; }
    }

    /// <summary>
    /// fs3p Layer4 項目 (物件)
    /// </summary>
    public class Fs3pLayer4Item
    {
        public int RelativeX { get; set; }
        public int RelativeY { get; set; }
        public int GroupId { get; set; } // 相對 GroupId，從 0 開始
        public byte Layer { get; set; }  // 渲染順序
        public byte IndexId { get; set; }
        public ushort TileId { get; set; }
    }
}
