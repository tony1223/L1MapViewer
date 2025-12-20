using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using L1MapViewer.Models;

namespace L1MapViewer.CLI
{
    /// <summary>
    /// fs3p 格式解析器 (ZIP 格式)
    /// </summary>
    public static class Fs3pParser
    {
        /// <summary>
        /// 從檔案讀取 fs3p (ZIP 格式)
        /// </summary>
        public static Fs3pData ParseFile(string filePath)
        {
            var fs3p = new Fs3pData();

            using (var archive = ZipFile.OpenRead(filePath))
            {
                // 讀取 metadata.json
                var metadataEntry = archive.GetEntry("metadata.json");
                if (metadataEntry != null)
                {
                    using (var stream = metadataEntry.Open())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        var metadata = JsonSerializer.Deserialize<Fs3pMetadata>(json);
                        if (metadata != null)
                        {
                            fs3p.Version = metadata.Version;
                            fs3p.LayerFlags = metadata.LayerFlags;
                            fs3p.Name = metadata.Name;
                            fs3p.OriginOffsetX = metadata.OriginOffsetX;
                            fs3p.OriginOffsetY = metadata.OriginOffsetY;
                            fs3p.Width = metadata.Width;
                            fs3p.Height = metadata.Height;
                            fs3p.CreatedTime = metadata.CreatedTime;
                            fs3p.ModifiedTime = metadata.ModifiedTime;
                            fs3p.Tags = metadata.Tags ?? new List<string>();
                        }
                    }
                }

                // 讀取縮圖
                var thumbnailEntry = archive.GetEntry("thumbnail.png");
                if (thumbnailEntry != null)
                {
                    using (var stream = thumbnailEntry.Open())
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        fs3p.ThumbnailPng = ms.ToArray();
                    }
                }

                // 讀取 Layer1
                if (fs3p.HasLayer1)
                {
                    var entry = archive.GetEntry("layers/layer1.bin");
                    if (entry != null)
                    {
                        using (var stream = entry.Open())
                        using (var br = new BinaryReader(stream))
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
                    }
                }

                // 讀取 Layer2
                if (fs3p.HasLayer2)
                {
                    var entry = archive.GetEntry("layers/layer2.bin");
                    if (entry != null)
                    {
                        using (var stream = entry.Open())
                        using (var br = new BinaryReader(stream))
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
                    }
                }

                // 讀取 Layer3
                if (fs3p.HasLayer3)
                {
                    var entry = archive.GetEntry("layers/layer3.bin");
                    if (entry != null)
                    {
                        using (var stream = entry.Open())
                        using (var br = new BinaryReader(stream))
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
                    }
                }

                // 讀取 Layer4
                if (fs3p.HasLayer4)
                {
                    var entry = archive.GetEntry("layers/layer4.bin");
                    if (entry != null)
                    {
                        using (var stream = entry.Open())
                        using (var br = new BinaryReader(stream))
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
                    }
                }

                // 讀取 Tiles
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith("tiles/") && entry.FullName.EndsWith(".til"))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(entry.Name);
                        if (int.TryParse(fileName, out int tileId))
                        {
                            using (var stream = entry.Open())
                            using (var ms = new MemoryStream())
                            {
                                stream.CopyTo(ms);
                                byte[] tilData = ms.ToArray();

                                fs3p.Tiles[tileId] = new TilePackageData
                                {
                                    OriginalTileId = tileId,
                                    Md5Hash = Helper.TileHashManager.CalculateMd5(tilData),
                                    TilData = tilData
                                };
                            }
                        }
                    }
                }
            }

            return fs3p;
        }

        /// <summary>
        /// 驗證檔案是否為有效的 fs3p 格式 (ZIP)
        /// </summary>
        public static bool IsValidFs3pFile(string filePath)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(filePath))
                {
                    return archive.GetEntry("metadata.json") != null;
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
            var info = new Fs3pInfo { FilePath = filePath };

            using (var archive = ZipFile.OpenRead(filePath))
            {
                // 讀取 metadata.json
                var metadataEntry = archive.GetEntry("metadata.json");
                if (metadataEntry == null)
                    return null;

                using (var stream = metadataEntry.Open())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    var metadata = JsonSerializer.Deserialize<Fs3pMetadata>(json);
                    if (metadata != null)
                    {
                        info.Version = metadata.Version;
                        info.LayerFlags = metadata.LayerFlags;
                        info.Name = metadata.Name;
                        info.OriginOffsetX = metadata.OriginOffsetX;
                        info.OriginOffsetY = metadata.OriginOffsetY;
                        info.Width = metadata.Width;
                        info.Height = metadata.Height;
                    }
                }

                // 讀取縮圖
                var thumbnailEntry = archive.GetEntry("thumbnail.png");
                info.HasThumbnail = thumbnailEntry != null;
                if (info.HasThumbnail)
                {
                    using (var stream = thumbnailEntry.Open())
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        info.ThumbnailPng = ms.ToArray();
                    }
                }
            }

            info.FileSize = new FileInfo(filePath).Length;
            return info;
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

    /// <summary>
    /// fs3p metadata.json 結構
    /// </summary>
    public class Fs3pMetadata
    {
        public ushort Version { get; set; }
        public ushort LayerFlags { get; set; }
        public string Name { get; set; }
        public int OriginOffsetX { get; set; }
        public int OriginOffsetY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long CreatedTime { get; set; }
        public long ModifiedTime { get; set; }
        public List<string> Tags { get; set; }
    }
}
