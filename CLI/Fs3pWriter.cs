using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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
    /// fs3p 格式寫入器 (ZIP 格式)
    /// </summary>
    public static class Fs3pWriter
    {
        /// <summary>
        /// 寫入 fs3p 到檔案 (ZIP 格式)
        /// </summary>
        public static void Write(Fs3pData fs3p, string filePath)
        {
            WriteZipFormat(fs3p, filePath);
        }

        /// <summary>
        /// 寫入 ZIP 格式
        /// </summary>
        private static void WriteZipFormat(Fs3pData fs3p, string filePath)
        {
            // 刪除已存在的檔案
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            using (var archive = ZipFile.Open(filePath, ZipArchiveMode.Create))
            {
                // 寫入 metadata.json
                var metadata = new Fs3pMetadata
                {
                    Version = fs3p.Version,
                    LayerFlags = fs3p.LayerFlags,
                    Name = fs3p.Name,
                    OriginOffsetX = fs3p.OriginOffsetX,
                    OriginOffsetY = fs3p.OriginOffsetY,
                    Width = fs3p.Width,
                    Height = fs3p.Height,
                    CreatedTime = fs3p.CreatedTime,
                    ModifiedTime = fs3p.ModifiedTime,
                    Tags = fs3p.Tags
                };

                var metadataEntry = archive.CreateEntry("metadata.json");
                using (var stream = metadataEntry.Open())
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(metadata, options);
                    writer.Write(json);
                }

                // 寫入縮圖
                if (fs3p.ThumbnailPng != null && fs3p.ThumbnailPng.Length > 0)
                {
                    var thumbnailEntry = archive.CreateEntry("thumbnail.png");
                    using (var stream = thumbnailEntry.Open())
                    {
                        stream.Write(fs3p.ThumbnailPng, 0, fs3p.ThumbnailPng.Length);
                    }
                }

                // 寫入 Layer1
                if (fs3p.HasLayer1 && fs3p.Layer1Items.Count > 0)
                {
                    var entry = archive.CreateEntry("layers/layer1.bin");
                    using (var stream = entry.Open())
                    using (var bw = new BinaryWriter(stream))
                    {
                        bw.Write(fs3p.Layer1Items.Count);
                        foreach (var item in fs3p.Layer1Items)
                        {
                            bw.Write(item.RelativeX);
                            bw.Write(item.RelativeY);
                            bw.Write(item.IndexId);
                            bw.Write(item.TileId);
                            bw.Write((byte)0); // Reserved
                        }
                    }
                }

                // 寫入 Layer2
                if (fs3p.HasLayer2 && fs3p.Layer2Items.Count > 0)
                {
                    var entry = archive.CreateEntry("layers/layer2.bin");
                    using (var stream = entry.Open())
                    using (var bw = new BinaryWriter(stream))
                    {
                        bw.Write(fs3p.Layer2Items.Count);
                        foreach (var item in fs3p.Layer2Items)
                        {
                            bw.Write(item.RelativeX);
                            bw.Write(item.RelativeY);
                            bw.Write(item.IndexId);
                            bw.Write(item.TileId);
                            bw.Write(item.UK);
                        }
                    }
                }

                // 寫入 Layer3
                if (fs3p.HasLayer3 && fs3p.Layer3Items.Count > 0)
                {
                    var entry = archive.CreateEntry("layers/layer3.bin");
                    using (var stream = entry.Open())
                    using (var bw = new BinaryWriter(stream))
                    {
                        bw.Write(fs3p.Layer3Items.Count);
                        foreach (var item in fs3p.Layer3Items)
                        {
                            bw.Write(item.RelativeX);
                            bw.Write(item.RelativeY);
                            bw.Write(item.Attribute1);
                            bw.Write(item.Attribute2);
                        }
                    }
                }

                // 寫入 Layer4
                if (fs3p.HasLayer4 && fs3p.Layer4Items.Count > 0)
                {
                    var entry = archive.CreateEntry("layers/layer4.bin");
                    using (var stream = entry.Open())
                    using (var bw = new BinaryWriter(stream))
                    {
                        bw.Write(fs3p.Layer4Items.Count);
                        foreach (var item in fs3p.Layer4Items)
                        {
                            bw.Write(item.RelativeX);
                            bw.Write(item.RelativeY);
                            bw.Write(item.GroupId);
                            bw.Write(item.Layer);
                            bw.Write(item.IndexId);
                            bw.Write(item.TileId);
                        }
                    }
                }

                // 寫入 Tiles
                foreach (var tile in fs3p.Tiles.Values)
                {
                    var entry = archive.CreateEntry($"tiles/{tile.OriginalTileId}.til");
                    using (var stream = entry.Open())
                    {
                        stream.Write(tile.TilData, 0, tile.TilData.Length);
                    }
                }
            }
        }

        /// <summary>
        /// 從選取的格子建立 fs3p
        /// </summary>
        public static Fs3pData CreateFromSelection(
            List<SelectedCell> selectedCells,
            Dictionary<string, S32Data> s32Files,
            string name,
            ushort layerFlags = 0x0F,
            bool includeTiles = true,
            Bitmap thumbnail = null)
        {
            if (selectedCells == null || selectedCells.Count == 0)
                return null;

            var fs3p = new Fs3pData
            {
                Name = name,
                LayerFlags = layerFlags
            };
            fs3p.SetCreatedNow();

            // 計算世界座標（LocalX + SegInfo offset）
            int GetWorldX(SelectedCell c) => c.S32Data.SegInfo.nLinBeginX * 2 + c.LocalX;
            int GetWorldY(SelectedCell c) => c.S32Data.SegInfo.nLinBeginY + c.LocalY;

            // 計算邊界和原點
            int minX = selectedCells.Min(c => GetWorldX(c));
            int minY = selectedCells.Min(c => GetWorldY(c));
            int maxX = selectedCells.Max(c => GetWorldX(c));
            int maxY = selectedCells.Max(c => GetWorldY(c));

            fs3p.OriginOffsetX = minX;
            fs3p.OriginOffsetY = minY;
            fs3p.Width = maxX - minX + 1;
            fs3p.Height = maxY - minY + 1;

            // 收集使用的 TileIds
            HashSet<int> usedTileIds = new HashSet<int>();

            // GroupId 重新編號對應表
            Dictionary<int, int> groupIdMapping = new Dictionary<int, int>();
            int nextGroupId = 0;

            foreach (var cell in selectedCells)
            {
                // 使用 cell 自帶的 S32Data
                var s32Data = cell.S32Data;
                if (s32Data == null)
                    continue;

                int relX = GetWorldX(cell) - minX;
                int relY = GetWorldY(cell) - minY;

                // Layer1
                if (fs3p.HasLayer1 && cell.LocalX < 128 && cell.LocalY < 64)
                {
                    var tileCell = s32Data.Layer1[cell.LocalY, cell.LocalX];
                    if (tileCell != null && tileCell.TileId > 0)
                    {
                        fs3p.Layer1Items.Add(new Fs3pLayer1Item
                        {
                            RelativeX = relX,
                            RelativeY = relY,
                            IndexId = (byte)tileCell.IndexId,
                            TileId = (ushort)tileCell.TileId
                        });
                        usedTileIds.Add(tileCell.TileId);
                    }
                }

                // Layer2
                if (fs3p.HasLayer2)
                {
                    foreach (var item in s32Data.Layer2.Where(i => i.X == cell.LocalX && i.Y == cell.LocalY))
                    {
                        fs3p.Layer2Items.Add(new Fs3pLayer2Item
                        {
                            RelativeX = relX,
                            RelativeY = relY,
                            IndexId = item.IndexId,
                            TileId = item.TileId,
                            UK = item.UK
                        });
                        if (item.TileId > 0)
                            usedTileIds.Add(item.TileId);
                    }
                }

                // Layer3 (使用 Layer3 座標系統: LocalX/2, LocalY)
                if (fs3p.HasLayer3)
                {
                    int l3x = cell.LocalX / 2;
                    int l3y = cell.LocalY;
                    if (l3x < 64 && l3y < 64)
                    {
                        var attr = s32Data.Layer3[l3y, l3x];
                        if (attr != null)
                        {
                            fs3p.Layer3Items.Add(new Fs3pLayer3Item
                            {
                                RelativeX = relX,
                                RelativeY = relY,
                                Attribute1 = attr.Attribute1,
                                Attribute2 = attr.Attribute2
                            });
                        }
                    }
                }

                // Layer4
                if (fs3p.HasLayer4)
                {
                    foreach (var obj in s32Data.Layer4.Where(o => o.X == cell.LocalX && o.Y == cell.LocalY))
                    {
                        // 重新編號 GroupId
                        if (!groupIdMapping.TryGetValue(obj.GroupId, out int newGroupId))
                        {
                            newGroupId = nextGroupId++;
                            groupIdMapping[obj.GroupId] = newGroupId;
                        }

                        fs3p.Layer4Items.Add(new Fs3pLayer4Item
                        {
                            RelativeX = relX,
                            RelativeY = relY,
                            GroupId = newGroupId,
                            Layer = (byte)obj.Layer,
                            IndexId = (byte)obj.IndexId,
                            TileId = (ushort)obj.TileId
                        });
                        if (obj.TileId > 0)
                            usedTileIds.Add(obj.TileId);
                    }
                }
            }

            // 打包 Tiles
            if (includeTiles)
            {
                foreach (int tileId in usedTileIds)
                {
                    byte[] tilData = L1PakReader.UnPack("Tile", $"{tileId}.til");
                    if (tilData != null)
                    {
                        fs3p.Tiles[tileId] = new TilePackageData
                        {
                            OriginalTileId = tileId,
                            Md5Hash = TileHashManager.CalculateMd5(tilData),
                            TilData = tilData
                        };
                    }
                }
            }

            // 縮圖
            if (thumbnail != null)
            {
                using (var ms = new MemoryStream())
                {
                    thumbnail.Save(ms, ImageFormat.Png);
                    fs3p.ThumbnailPng = ms.ToArray();
                }
            }

            return fs3p;
        }

    }
}
