using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using L1MapViewer.Helper;
using L1MapViewer.Models;
using L1MapViewer.Reader;

namespace L1MapViewer.CLI
{
    /// <summary>
    /// fs32 格式寫入器
    /// </summary>
    public static class Fs32Writer
    {
        /// <summary>
        /// 寫入 fs32 到檔案
        /// </summary>
        public static void Write(Fs32Data fs32, string filePath)
        {
            byte[] data = ToBytes(fs32);
            File.WriteAllBytes(filePath, data);
        }

        /// <summary>
        /// 轉換為二進位資料
        /// </summary>
        public static byte[] ToBytes(Fs32Data fs32)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.UTF8))
            {
                // 寫入 Header
                bw.Write(Fs32Data.MAGIC);
                bw.Write(fs32.Version);
                bw.Write(fs32.LayerFlags);
                bw.Write((byte)fs32.Mode);

                // 寫入 MapId
                byte[] mapIdBytes = Encoding.UTF8.GetBytes(fs32.SourceMapId ?? string.Empty);
                bw.Write(mapIdBytes.Length);
                if (mapIdBytes.Length > 0)
                {
                    bw.Write(mapIdBytes);
                }

                // 選取區域資訊 (Mode=2 時)
                if (fs32.Mode == Fs32Mode.SelectedRegion)
                {
                    bw.Write(fs32.SelectionOriginX);
                    bw.Write(fs32.SelectionOriginY);
                    bw.Write(fs32.SelectionWidth);
                    bw.Write(fs32.SelectionHeight);
                }

                // 寫入區塊列表
                bw.Write(fs32.Blocks.Count);
                foreach (var block in fs32.Blocks)
                {
                    bw.Write(block.BlockX);
                    bw.Write(block.BlockY);
                    bw.Write(block.S32Data.Length);
                    bw.Write(block.S32Data);
                }

                // 寫入 Tile 資料
                bw.Write(fs32.Tiles.Count);
                foreach (var tile in fs32.Tiles.Values)
                {
                    bw.Write(tile.OriginalTileId);
                    bw.Write(tile.Md5Hash);
                    bw.Write(tile.TilData.Length);
                    bw.Write(tile.TilData);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// 從 S32Data 建立 fs32 (單一區塊)
        /// </summary>
        public static Fs32Data CreateFromS32(S32Data s32Data, string mapId, ushort layerFlags = 0xFF, bool includeTiles = true)
        {
            var fs32 = new Fs32Data
            {
                Mode = Fs32Mode.SelectedBlocks,
                SourceMapId = mapId,
                LayerFlags = layerFlags
            };

            // 加入區塊
            var block = new Fs32Block
            {
                BlockX = s32Data.SegInfo.nBlockX,
                BlockY = s32Data.SegInfo.nBlockY,
                S32Data = s32Data.OriginalFileData ?? S32Writer.ToBytes(s32Data)
            };
            fs32.Blocks.Add(block);

            // 加入 Tiles
            if (includeTiles)
            {
                CollectTiles(fs32, s32Data);
            }

            return fs32;
        }

        /// <summary>
        /// 從多個 S32Data 建立 fs32
        /// </summary>
        public static Fs32Data CreateFromS32List(IEnumerable<S32Data> s32List, string mapId, ushort layerFlags = 0xFF, bool includeTiles = true)
        {
            var fs32 = new Fs32Data
            {
                Mode = Fs32Mode.SelectedBlocks,
                SourceMapId = mapId,
                LayerFlags = layerFlags
            };

            foreach (var s32Data in s32List)
            {
                var block = new Fs32Block
                {
                    BlockX = s32Data.SegInfo.nBlockX,
                    BlockY = s32Data.SegInfo.nBlockY,
                    S32Data = s32Data.OriginalFileData ?? S32Writer.ToBytes(s32Data)
                };
                fs32.Blocks.Add(block);

                if (includeTiles)
                {
                    CollectTiles(fs32, s32Data);
                }
            }

            return fs32;
        }

        /// <summary>
        /// 從整張地圖建立 fs32
        /// </summary>
        public static Fs32Data CreateFromMap(MapDocument mapDoc, ushort layerFlags = 0xFF, bool includeTiles = true)
        {
            var fs32 = new Fs32Data
            {
                Mode = Fs32Mode.WholeMap,
                SourceMapId = mapDoc.MapId,
                LayerFlags = layerFlags
            };

            foreach (var s32Data in mapDoc.S32Files.Values)
            {
                var block = new Fs32Block
                {
                    BlockX = s32Data.SegInfo.nBlockX,
                    BlockY = s32Data.SegInfo.nBlockY,
                    S32Data = s32Data.OriginalFileData ?? S32Writer.ToBytes(s32Data)
                };
                fs32.Blocks.Add(block);

                if (includeTiles)
                {
                    CollectTiles(fs32, s32Data);
                }
            }

            return fs32;
        }

        /// <summary>
        /// 收集 S32 使用的 Tiles
        /// </summary>
        private static void CollectTiles(Fs32Data fs32, S32Data s32Data)
        {
            // 從 Layer6 取得使用的 TileIds
            HashSet<int> tileIds = new HashSet<int>();

            // Layer1
            if (s32Data.Layer1 != null)
            {
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        var cell = s32Data.Layer1[y, x];
                        if (cell?.TileId > 0)
                        {
                            tileIds.Add(cell.TileId);
                        }
                    }
                }
            }

            // Layer2
            foreach (var item in s32Data.Layer2)
            {
                if (item.TileId > 0)
                {
                    tileIds.Add(item.TileId);
                }
            }

            // Layer4
            foreach (var obj in s32Data.Layer4)
            {
                if (obj.TileId > 0)
                {
                    tileIds.Add(obj.TileId);
                }
            }

            // 打包每個 Tile
            foreach (int tileId in tileIds)
            {
                if (fs32.Tiles.ContainsKey(tileId))
                    continue;

                byte[] tilData = L1PakReader.UnPack("Tile", $"{tileId}.til");
                if (tilData != null)
                {
                    fs32.Tiles[tileId] = new TilePackageData
                    {
                        OriginalTileId = tileId,
                        Md5Hash = TileHashManager.CalculateMd5(tilData),
                        TilData = tilData
                    };
                }
            }
        }
    }
}
