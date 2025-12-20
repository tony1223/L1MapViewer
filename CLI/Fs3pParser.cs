using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using L1MapViewer.Models;

namespace L1MapViewer.CLI
{
    /// <summary>
    /// fs3p 格式解析器
    /// </summary>
    public static class Fs3pParser
    {
        /// <summary>
        /// 從檔案讀取 fs3p
        /// </summary>
        public static Fs3pData ParseFile(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            return Parse(data);
        }

        /// <summary>
        /// 解析 fs3p 二進位資料
        /// </summary>
        public static Fs3pData Parse(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms, Encoding.UTF8))
            {
                var fs3p = new Fs3pData();

                // 讀取 Header
                uint magic = br.ReadUInt32();
                if (magic != Fs3pData.MAGIC)
                {
                    throw new InvalidDataException($"Invalid fs3p magic: 0x{magic:X8}, expected 0x{Fs3pData.MAGIC:X8}");
                }

                fs3p.Version = br.ReadUInt16();
                fs3p.LayerFlags = br.ReadUInt16();

                // 讀取名稱
                int nameLen = br.ReadInt32();
                if (nameLen > 0)
                {
                    byte[] nameBytes = br.ReadBytes(nameLen);
                    fs3p.Name = Encoding.UTF8.GetString(nameBytes);
                }

                // 讀取縮圖
                int thumbnailLen = br.ReadInt32();
                if (thumbnailLen > 0)
                {
                    fs3p.ThumbnailPng = br.ReadBytes(thumbnailLen);
                }

                // 讀取範圍資訊
                fs3p.OriginOffsetX = br.ReadInt32();
                fs3p.OriginOffsetY = br.ReadInt32();
                fs3p.Width = br.ReadInt32();
                fs3p.Height = br.ReadInt32();

                // 讀取 Layer1
                if (fs3p.HasLayer1)
                {
                    int count = br.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        fs3p.Layer1Items.Add(new Fs3pLayer1Item
                        {
                            RelativeX = br.ReadInt32(),
                            RelativeY = br.ReadInt32(),
                            IndexId = br.ReadByte(),
                            TileId = br.ReadUInt16()
                        });
                        br.ReadByte(); // Reserved
                    }
                }

                // 讀取 Layer2
                if (fs3p.HasLayer2)
                {
                    int count = br.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        fs3p.Layer2Items.Add(new Fs3pLayer2Item
                        {
                            RelativeX = br.ReadInt32(),
                            RelativeY = br.ReadInt32(),
                            IndexId = br.ReadByte(),
                            TileId = br.ReadUInt16(),
                            UK = br.ReadByte()
                        });
                    }
                }

                // 讀取 Layer3
                if (fs3p.HasLayer3)
                {
                    int count = br.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        fs3p.Layer3Items.Add(new Fs3pLayer3Item
                        {
                            RelativeX = br.ReadInt32(),
                            RelativeY = br.ReadInt32(),
                            Attribute1 = br.ReadInt16(),
                            Attribute2 = br.ReadInt16()
                        });
                    }
                }

                // 讀取 Layer4
                if (fs3p.HasLayer4)
                {
                    int count = br.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        fs3p.Layer4Items.Add(new Fs3pLayer4Item
                        {
                            RelativeX = br.ReadInt32(),
                            RelativeY = br.ReadInt32(),
                            GroupId = br.ReadInt32(),
                            Layer = br.ReadByte(),
                            IndexId = br.ReadByte(),
                            TileId = br.ReadUInt16()
                        });
                    }
                }

                // 讀取 Tile 資料
                int tileCount = br.ReadInt32();
                for (int i = 0; i < tileCount; i++)
                {
                    var tile = new TilePackageData
                    {
                        OriginalTileId = br.ReadInt32()
                    };
                    tile.Md5Hash = br.ReadBytes(16);
                    int tilDataLen = br.ReadInt32();
                    tile.TilData = br.ReadBytes(tilDataLen);

                    fs3p.Tiles[tile.OriginalTileId] = tile;
                }

                // 讀取 Metadata
                fs3p.CreatedTime = br.ReadInt64();
                fs3p.ModifiedTime = br.ReadInt64();

                int tagCount = br.ReadInt32();
                for (int i = 0; i < tagCount; i++)
                {
                    int tagLen = br.ReadInt32();
                    byte[] tagBytes = br.ReadBytes(tagLen);
                    fs3p.Tags.Add(Encoding.UTF8.GetString(tagBytes));
                }

                return fs3p;
            }
        }

        /// <summary>
        /// 驗證檔案是否為有效的 fs3p 格式
        /// </summary>
        public static bool IsValidFs3pFile(string filePath)
        {
            try
            {
                using (var fs = File.OpenRead(filePath))
                using (var br = new BinaryReader(fs))
                {
                    if (fs.Length < 4)
                        return false;

                    uint magic = br.ReadUInt32();
                    return magic == Fs3pData.MAGIC;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 取得 fs3p 檔案資訊 (不載入完整資料)
        /// </summary>
        public static Fs3pInfo GetInfo(string filePath)
        {
            using (var fs = File.OpenRead(filePath))
            using (var br = new BinaryReader(fs, Encoding.UTF8))
            {
                var info = new Fs3pInfo();

                uint magic = br.ReadUInt32();
                if (magic != Fs3pData.MAGIC)
                    return null;

                info.Version = br.ReadUInt16();
                info.LayerFlags = br.ReadUInt16();

                int nameLen = br.ReadInt32();
                if (nameLen > 0)
                {
                    byte[] nameBytes = br.ReadBytes(nameLen);
                    info.Name = Encoding.UTF8.GetString(nameBytes);
                }

                int thumbnailLen = br.ReadInt32();
                info.HasThumbnail = thumbnailLen > 0;
                if (thumbnailLen > 0)
                {
                    info.ThumbnailPng = br.ReadBytes(thumbnailLen);
                }

                info.OriginOffsetX = br.ReadInt32();
                info.OriginOffsetY = br.ReadInt32();
                info.Width = br.ReadInt32();
                info.Height = br.ReadInt32();

                info.FileSize = fs.Length;
                info.FilePath = filePath;

                return info;
            }
        }
    }

    /// <summary>
    /// fs3p 檔案資訊 (輕量級)
    /// </summary>
    public class Fs3pInfo
    {
        public string FilePath { get; set; }
        public ushort Version { get; set; }
        public ushort LayerFlags { get; set; }
        public string Name { get; set; }
        public bool HasThumbnail { get; set; }
        public byte[] ThumbnailPng { get; set; }
        public int OriginOffsetX { get; set; }
        public int OriginOffsetY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long FileSize { get; set; }

        public bool HasLayer1 => (LayerFlags & 0x01) != 0;
        public bool HasLayer2 => (LayerFlags & 0x02) != 0;
        public bool HasLayer3 => (LayerFlags & 0x04) != 0;
        public bool HasLayer4 => (LayerFlags & 0x08) != 0;
    }
}
