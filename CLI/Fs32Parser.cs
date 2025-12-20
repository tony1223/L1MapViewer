using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using L1MapViewer.Models;

namespace L1MapViewer.CLI
{
    /// <summary>
    /// fs32 格式解析器
    /// </summary>
    public static class Fs32Parser
    {
        /// <summary>
        /// 從檔案讀取 fs32
        /// </summary>
        public static Fs32Data ParseFile(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            return Parse(data);
        }

        /// <summary>
        /// 解析 fs32 二進位資料
        /// </summary>
        public static Fs32Data Parse(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms, Encoding.UTF8))
            {
                var fs32 = new Fs32Data();

                // 讀取 Header
                uint magic = br.ReadUInt32();
                if (magic != Fs32Data.MAGIC)
                {
                    throw new InvalidDataException($"Invalid fs32 magic: 0x{magic:X8}, expected 0x{Fs32Data.MAGIC:X8}");
                }

                fs32.Version = br.ReadUInt16();
                fs32.LayerFlags = br.ReadUInt16();
                fs32.Mode = (Fs32Mode)br.ReadByte();

                // 讀取 MapId
                int mapIdLen = br.ReadInt32();
                if (mapIdLen > 0)
                {
                    byte[] mapIdBytes = br.ReadBytes(mapIdLen);
                    fs32.SourceMapId = Encoding.UTF8.GetString(mapIdBytes);
                }

                // 選取區域資訊 (Mode=2 時)
                if (fs32.Mode == Fs32Mode.SelectedRegion)
                {
                    fs32.SelectionOriginX = br.ReadInt32();
                    fs32.SelectionOriginY = br.ReadInt32();
                    fs32.SelectionWidth = br.ReadInt32();
                    fs32.SelectionHeight = br.ReadInt32();
                }

                // 讀取區塊列表
                int blockCount = br.ReadInt32();
                for (int i = 0; i < blockCount; i++)
                {
                    var block = new Fs32Block
                    {
                        BlockX = br.ReadInt32(),
                        BlockY = br.ReadInt32()
                    };

                    int s32DataLen = br.ReadInt32();
                    block.S32Data = br.ReadBytes(s32DataLen);

                    fs32.Blocks.Add(block);
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

                    fs32.Tiles[tile.OriginalTileId] = tile;
                }

                return fs32;
            }
        }

        /// <summary>
        /// 驗證檔案是否為有效的 fs32 格式
        /// </summary>
        public static bool IsValidFs32File(string filePath)
        {
            try
            {
                using (var fs = File.OpenRead(filePath))
                using (var br = new BinaryReader(fs))
                {
                    if (fs.Length < 4)
                        return false;

                    uint magic = br.ReadUInt32();
                    return magic == Fs32Data.MAGIC;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 取得 fs32 檔案資訊 (不載入完整資料)
        /// </summary>
        public static Fs32Info GetInfo(string filePath)
        {
            using (var fs = File.OpenRead(filePath))
            using (var br = new BinaryReader(fs, Encoding.UTF8))
            {
                var info = new Fs32Info();

                uint magic = br.ReadUInt32();
                if (magic != Fs32Data.MAGIC)
                    return null;

                info.Version = br.ReadUInt16();
                info.LayerFlags = br.ReadUInt16();
                info.Mode = (Fs32Mode)br.ReadByte();

                int mapIdLen = br.ReadInt32();
                if (mapIdLen > 0)
                {
                    byte[] mapIdBytes = br.ReadBytes(mapIdLen);
                    info.SourceMapId = Encoding.UTF8.GetString(mapIdBytes);
                }

                if (info.Mode == Fs32Mode.SelectedRegion)
                {
                    info.SelectionOriginX = br.ReadInt32();
                    info.SelectionOriginY = br.ReadInt32();
                    info.SelectionWidth = br.ReadInt32();
                    info.SelectionHeight = br.ReadInt32();
                }

                info.BlockCount = br.ReadInt32();

                // 跳過區塊資料來計算 Tile 數量
                for (int i = 0; i < info.BlockCount; i++)
                {
                    br.ReadInt32(); // BlockX
                    br.ReadInt32(); // BlockY
                    int s32DataLen = br.ReadInt32();
                    br.BaseStream.Seek(s32DataLen, SeekOrigin.Current);
                }

                info.TileCount = br.ReadInt32();
                info.FileSize = fs.Length;

                return info;
            }
        }
    }

    /// <summary>
    /// fs32 檔案資訊 (輕量級)
    /// </summary>
    public class Fs32Info
    {
        public ushort Version { get; set; }
        public ushort LayerFlags { get; set; }
        public Fs32Mode Mode { get; set; }
        public string SourceMapId { get; set; }
        public int SelectionOriginX { get; set; }
        public int SelectionOriginY { get; set; }
        public int SelectionWidth { get; set; }
        public int SelectionHeight { get; set; }
        public int BlockCount { get; set; }
        public int TileCount { get; set; }
        public long FileSize { get; set; }
    }
}
