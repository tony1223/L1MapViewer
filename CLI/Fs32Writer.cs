using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using L1MapViewer.Helper;
using L1MapViewer.Models;
using L1MapViewer.Reader;

namespace L1MapViewer.CLI
{
    /// <summary>
    /// fs32 格式寫入器 (ZIP 結構)
    /// </summary>
    public static class Fs32Writer
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        /// <summary>
        /// 寫入 fs32 到檔案
        /// </summary>
        public static void Write(Fs32Data fs32, string filePath)
        {
            // 確保目錄存在
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 如果檔案已存在，先刪除
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            using (var zipArchive = ZipFile.Open(filePath, ZipArchiveMode.Create))
            {
                // 1. 寫入 manifest.json
                var manifest = new Fs32Manifest
                {
                    Version = fs32.Version,
                    LayerFlags = fs32.LayerFlags,
                    Mode = (int)fs32.Mode,
                    SourceMapId = fs32.SourceMapId ?? string.Empty,
                    SelectionOriginX = fs32.SelectionOriginX,
                    SelectionOriginY = fs32.SelectionOriginY,
                    SelectionWidth = fs32.SelectionWidth,
                    SelectionHeight = fs32.SelectionHeight
                };

                // 2. 寫入區塊 (使用標準 s32 檔名格式: 7fff8000.s32)
                foreach (var block in fs32.Blocks)
                {
                    string blockName = $"{block.BlockX:x4}{block.BlockY:x4}";
                    manifest.Blocks.Add(blockName);

                    var entry = zipArchive.CreateEntry($"blocks/{blockName}.s32", CompressionLevel.Optimal);
                    using (var stream = entry.Open())
                    {
                        stream.Write(block.S32Data, 0, block.S32Data.Length);
                    }
                }

                // 寫入 manifest
                var manifestEntry = zipArchive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using (var stream = manifestEntry.Open())
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    string json = JsonSerializer.Serialize(manifest, JsonOptions);
                    writer.Write(json);
                }

                // 3. 寫入 Tiles
                if (fs32.Tiles.Count > 0)
                {
                    var tileIndex = new TileIndex();

                    foreach (var tile in fs32.Tiles.Values)
                    {
                        // 寫入 .til 檔案
                        var tileEntry = zipArchive.CreateEntry($"tiles/{tile.OriginalTileId}.til", CompressionLevel.Optimal);
                        using (var stream = tileEntry.Open())
                        {
                            stream.Write(tile.TilData, 0, tile.TilData.Length);
                        }

                        // 記錄 MD5
                        tileIndex.Tiles[tile.OriginalTileId.ToString()] = TileHashManager.Md5ToHex(tile.Md5Hash);
                    }

                    // 寫入 tiles/index.json
                    var indexEntry = zipArchive.CreateEntry("tiles/index.json", CompressionLevel.Optimal);
                    using (var stream = indexEntry.Open())
                    using (var writer = new StreamWriter(stream, Encoding.UTF8))
                    {
                        string json = JsonSerializer.Serialize(tileIndex, JsonOptions);
                        writer.Write(json);
                    }
                }
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
            // 從各 Layer 取得使用的 TileIds
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
