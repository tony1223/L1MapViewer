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
        public static Fs32Data CreateFromS32(S32Data s32Data, string mapId, ushort layerFlags = 0xFF, bool includeTiles = true, bool stripL8Ext = false)
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
                S32Data = GetS32Bytes(s32Data, stripL8Ext, layerFlags)
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
        public static Fs32Data CreateFromS32List(IEnumerable<S32Data> s32List, string mapId, ushort layerFlags = 0xFF, bool includeTiles = true, bool stripL8Ext = false)
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
                    S32Data = GetS32Bytes(s32Data, stripL8Ext, layerFlags)
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
        public static Fs32Data CreateFromMap(MapDocument mapDoc, ushort layerFlags = 0xFF, bool includeTiles = true, bool stripL8Ext = false)
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
                    S32Data = GetS32Bytes(s32Data, stripL8Ext, layerFlags)
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
        /// 取得 S32 資料位元組，根據 layerFlags 過濾圖層資料
        /// </summary>
        private static byte[] GetS32Bytes(S32Data s32Data, bool stripL8Ext, ushort layerFlags = 0xFF)
        {
            // 如果所有圖層都選取且不需要移除 L8 擴展，直接返回原始資料
            if (layerFlags == 0xFF && !stripL8Ext)
            {
                return s32Data.OriginalFileData ?? S32Writer.ToBytes(s32Data);
            }

            // 需要過濾圖層，建立副本避免修改原始資料
            var filtered = CloneS32Data(s32Data);

            // 根據 layerFlags 清除未選取的圖層
            if ((layerFlags & 0x01) == 0) // Layer1
            {
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 128; x++)
                        filtered.Layer1[y, x] = new TileCell { TileId = 0, IndexId = 0 };
            }

            if ((layerFlags & 0x02) == 0) // Layer2
            {
                filtered.Layer2.Clear();
            }

            if ((layerFlags & 0x04) == 0) // Layer3
            {
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        filtered.Layer3[y, x] = new MapAttribute { Attribute1 = 0, Attribute2 = 0 };
            }

            if ((layerFlags & 0x08) == 0) // Layer4
            {
                filtered.Layer4.Clear();
            }

            if ((layerFlags & 0x10) == 0) // Layer5
            {
                filtered.Layer5.Clear();
            }

            // Layer6 會由 S32Writer 自動重新計算

            if ((layerFlags & 0x40) == 0) // Layer7
            {
                filtered.Layer7.Clear();
            }

            if ((layerFlags & 0x80) == 0) // Layer8
            {
                filtered.Layer8.Clear();
                filtered.Layer8HasExtendedData = false;
            }
            else if (stripL8Ext && filtered.Layer8HasExtendedData)
            {
                // 移除 Layer8 擴展資料
                filtered.Layer8HasExtendedData = false;
                foreach (var item in filtered.Layer8)
                {
                    item.ExtendedData = 0;
                }
            }

            return S32Writer.ToBytes(filtered);
        }

        /// <summary>
        /// 複製 S32Data (深層複製)
        /// </summary>
        private static S32Data CloneS32Data(S32Data source)
        {
            var clone = new S32Data
            {
                SegInfo = source.SegInfo,
                Layer8HasExtendedData = source.Layer8HasExtendedData
            };

            // Layer1
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    var src = source.Layer1[y, x];
                    if (src != null)
                    {
                        clone.Layer1[y, x] = new TileCell
                        {
                            X = src.X,
                            Y = src.Y,
                            TileId = src.TileId,
                            IndexId = src.IndexId
                        };
                    }
                }
            }

            // Layer2
            foreach (var item in source.Layer2)
            {
                clone.Layer2.Add(new Layer2Item
                {
                    X = item.X,
                    Y = item.Y,
                    IndexId = item.IndexId,
                    TileId = item.TileId,
                    UK = item.UK
                });
            }

            // Layer3
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    var src = source.Layer3[y, x];
                    if (src != null)
                    {
                        clone.Layer3[y, x] = new MapAttribute
                        {
                            Attribute1 = src.Attribute1,
                            Attribute2 = src.Attribute2
                        };
                    }
                }
            }

            // Layer4
            foreach (var obj in source.Layer4)
            {
                clone.Layer4.Add(new ObjectTile
                {
                    X = obj.X,
                    Y = obj.Y,
                    GroupId = obj.GroupId,
                    Layer = obj.Layer,
                    IndexId = obj.IndexId,
                    TileId = obj.TileId
                });
            }

            // Layer5
            foreach (var item in source.Layer5)
            {
                clone.Layer5.Add(new Layer5Item
                {
                    X = item.X,
                    Y = item.Y,
                    ObjectIndex = item.ObjectIndex,
                    Type = item.Type
                });
            }

            // Layer6 (會由 S32Writer 重新計算)
            clone.Layer6.AddRange(source.Layer6);

            // Layer7
            foreach (var item in source.Layer7)
            {
                clone.Layer7.Add(new Layer7Item
                {
                    Name = item.Name,
                    X = item.X,
                    Y = item.Y,
                    TargetMapId = item.TargetMapId,
                    PortalId = item.PortalId
                });
            }

            // Layer8
            foreach (var item in source.Layer8)
            {
                clone.Layer8.Add(new Layer8Item
                {
                    SprId = item.SprId,
                    X = item.X,
                    Y = item.Y,
                    ExtendedData = item.ExtendedData
                });
            }

            return clone;
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
