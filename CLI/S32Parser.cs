using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using L1MapViewer.Models;

namespace L1MapViewer.CLI
{
    /// <summary>
    /// S32 檔案解析器（獨立於 UI）
    /// </summary>
    public static class S32Parser
    {
        /// <summary>
        /// 解析 S32 檔案
        /// </summary>
        public static S32Data Parse(byte[] data)
        {
            S32Data s32Data = new S32Data();

            // 保存原始文件數據（直接使用，不複製）
            s32Data.OriginalFileData = data;

            using (BinaryReader br = new BinaryReader(new MemoryStream(data)))
            {
                // 記錄第一層偏移
                s32Data.Layer1Offset = (int)br.BaseStream.Position;

                // 第一層（地板）- 64x128
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        int id = br.ReadByte();
                        int til = br.ReadUInt16();
                        int nk = br.ReadByte();

                        s32Data.Layer1[y, x] = new TileCell
                        {
                            X = x,
                            Y = y,
                            TileId = til,
                            IndexId = id
                        };

                        // 收集使用的 tile（第一層）
                        if (!s32Data.UsedTiles.ContainsKey(til))
                        {
                            s32Data.UsedTiles[til] = new TileInfo
                            {
                                TileId = til,
                                IndexId = id,
                                UsageCount = 1,
                                Thumbnail = null
                            };
                        }
                        else
                        {
                            s32Data.UsedTiles[til].UsageCount++;
                        }
                    }
                }

                // 記錄第二層偏移
                s32Data.Layer2Offset = (int)br.BaseStream.Position;

                // 第二層 - X(BYTE), Y(BYTE), IndexId(BYTE), TileId(USHORT), UK(BYTE)
                int layer2Count = br.ReadUInt16();
                for (int i = 0; i < layer2Count; i++)
                {
                    s32Data.Layer2.Add(new Layer2Item
                    {
                        X = br.ReadByte(),
                        Y = br.ReadByte(),
                        IndexId = br.ReadByte(),
                        TileId = br.ReadUInt16(),
                        UK = br.ReadByte()
                    });
                }

                // 記錄第三層偏移
                s32Data.Layer3Offset = (int)br.BaseStream.Position;

                // 第三層（地圖屬性）- 64x64
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        s32Data.Layer3[y, x] = new MapAttribute
                        {
                            Attribute1 = br.ReadInt16(),
                            Attribute2 = br.ReadInt16()
                        };
                    }
                }

                // 記錄第四層偏移
                s32Data.Layer4Offset = (int)br.BaseStream.Position;

                // 第四層（物件）
                int layer4GroupCount = br.ReadInt32();
                for (int i = 0; i < layer4GroupCount; i++)
                {
                    int groupId = br.ReadInt16();
                    int blockCount = br.ReadUInt16();

                    for (int j = 0; j < blockCount; j++)
                    {
                        int x = br.ReadByte();
                        int y = br.ReadByte();
                        int layer = br.ReadByte();
                        int indexId = br.ReadByte();
                        int tileId = br.ReadInt16();
                        int uk = br.ReadByte();

                        var objTile = new ObjectTile
                        {
                            GroupId = groupId,
                            X = x,
                            Y = y,
                            Layer = layer,
                            IndexId = indexId,
                            TileId = tileId
                        };

                        s32Data.Layer4.Add(objTile);

                        // 收集使用的 tile（第四層）
                        if (!s32Data.UsedTiles.ContainsKey(tileId))
                        {
                            s32Data.UsedTiles[tileId] = new TileInfo
                            {
                                TileId = tileId,
                                IndexId = indexId,
                                UsageCount = 1,
                                Thumbnail = null
                            };
                        }
                        else
                        {
                            s32Data.UsedTiles[tileId].UsageCount++;
                        }
                    }
                }

                // 記錄第四層結束位置
                s32Data.Layer4EndOffset = (int)br.BaseStream.Position;

                // 讀取第5-8層的原始資料
                int remainingLength = (int)(br.BaseStream.Length - br.BaseStream.Position);
                if (remainingLength > 0)
                {
                    s32Data.Layer5to8Data = br.ReadBytes(remainingLength);
                    ParseLayers5to8(s32Data);
                }
                else
                {
                    s32Data.Layer5to8Data = new byte[0];
                }
            }

            // Layer2 覆蓋 Layer1 - 根據逆向代碼實現
            // Layer2 項目會覆蓋 Layer1 對應位置的 TileId 和 IndexId
            // ApplyLayer2ToLayer1(s32Data);

            // 計算實際邊界 (根據各層資料的最大 X, Y)
            s32Data.CalculateRealBounds();

            return s32Data;
        }

        /// <summary>
        /// 將 Layer2 覆蓋到 Layer1
        /// 根據逆向代碼 sub_4E7D70，Layer2 項目會覆蓋 Layer1 對應位置
        /// </summary>
        private static void ApplyLayer2ToLayer1(S32Data s32Data)
        {
            foreach (var layer2Item in s32Data.Layer2)
            {
                int x = layer2Item.X;
                int y = layer2Item.Y;

                // 確保座標在有效範圍內 (Layer1 是 64x128)
                if (x >= 0 && x < 128 && y >= 0 && y < 64)
                {
                    // 覆蓋 Layer1 對應位置的 TileId 和 IndexId
                    s32Data.Layer1[y, x] = new TileCell
                    {
                        X = x,
                        Y = y,
                        TileId = layer2Item.TileId,
                        IndexId = layer2Item.IndexId
                    };

                    // 更新 UsedTiles
                    if (!s32Data.UsedTiles.ContainsKey(layer2Item.TileId))
                    {
                        s32Data.UsedTiles[layer2Item.TileId] = new TileInfo
                        {
                            TileId = layer2Item.TileId,
                            IndexId = layer2Item.IndexId,
                            UsageCount = 1,
                            Thumbnail = null
                        };
                    }
                    else
                    {
                        s32Data.UsedTiles[layer2Item.TileId].UsageCount++;
                    }
                }
            }
        }

        /// <summary>
        /// 解析第 5-8 層
        /// </summary>
        private static void ParseLayers5to8(S32Data s32Data)
        {
            using (var layerStream = new MemoryStream(s32Data.Layer5to8Data))
            using (var layerReader = new BinaryReader(layerStream))
            {
                try
                {
                    // 第五層 - 事件
                    if (layerStream.Position + 4 <= layerStream.Length)
                    {
                        int lv5Count = layerReader.ReadInt32();
                        for (int i = 0; i < lv5Count && layerStream.Position + 5 <= layerStream.Length; i++)
                        {
                            s32Data.Layer5.Add(new Layer5Item
                            {
                                X = layerReader.ReadByte(),
                                Y = layerReader.ReadByte(),
                                ObjectIndex = layerReader.ReadUInt16(),
                                Type = layerReader.ReadByte()
                            });
                        }
                    }

                    // 第六層 - 使用的 til
                    if (layerStream.Position + 4 <= layerStream.Length)
                    {
                        int lv6Count = layerReader.ReadInt32();
                        for (int i = 0; i < lv6Count && layerStream.Position + 4 <= layerStream.Length; i++)
                        {
                            int til = layerReader.ReadInt32();
                            s32Data.Layer6.Add(til);
                        }
                    }

                    // 第七層 - 傳送點、入口點
                    if (layerStream.Position + 2 <= layerStream.Length)
                    {
                        int lv7Count = layerReader.ReadUInt16();
                        for (int i = 0; i < lv7Count && layerStream.Position + 1 <= layerStream.Length; i++)
                        {
                            byte len = layerReader.ReadByte();
                            if (layerStream.Position + len + 8 > layerStream.Length) break;

                            string name = Encoding.Default.GetString(layerReader.ReadBytes(len));
                            s32Data.Layer7.Add(new Layer7Item
                            {
                                Name = name,
                                X = layerReader.ReadByte(),
                                Y = layerReader.ReadByte(),
                                TargetMapId = layerReader.ReadUInt16(),
                                PortalId = layerReader.ReadInt32()
                            });
                        }
                    }

                    // 第八層 - 特效、裝飾品
                    if (layerStream.Position + 2 <= layerStream.Length)
                    {
                        ushort lv8Num = layerReader.ReadUInt16();
                        bool hasExtendedData = (lv8Num >= 0x8000);
                        if (hasExtendedData)
                        {
                            lv8Num = (ushort)(lv8Num & 0x7FFF);  // 取消高位
                        }
                        s32Data.Layer8HasExtendedData = hasExtendedData;

                        int itemSize = hasExtendedData ? 10 : 6;  // 6 bytes 基本, +4 bytes 擴展
                        for (int i = 0; i < lv8Num && layerStream.Position + itemSize <= layerStream.Length; i++)
                        {
                            var item = new Layer8Item
                            {
                                SprId = layerReader.ReadUInt16(),
                                X = layerReader.ReadUInt16(),
                                Y = layerReader.ReadUInt16(),
                                ExtendedData = hasExtendedData ? layerReader.ReadInt32() : 0
                            };
                            s32Data.Layer8.Add(item);
                        }
                    }
                }
                catch (EndOfStreamException)
                {
                    // 忽略讀取超出範圍的錯誤
                }
            }
        }

        /// <summary>
        /// 從檔案載入並解析 S32
        /// </summary>
        public static S32Data ParseFile(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            S32Data s32Data = Parse(data);
            s32Data.FilePath = filePath;
            return s32Data;
        }
    }
}
