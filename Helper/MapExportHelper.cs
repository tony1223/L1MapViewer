using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using L1MapViewer.Models;
using L1MapViewer.Other;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// 地圖匯出輔助類別
    /// </summary>
    public class MapExportHelper
    {
        /// <summary>
        /// 匯出地圖資料（L1J 格式）
        /// </summary>
        public static void ExportMapData(
            string filePath,
            string mapId,
            Dictionary<string, S32Data> allS32DataDict,
            Struct.L1Map currentMap)
        {
            // MapTool 的維度計算：每個 S32 是 64x64
            int xLength = currentMap.nLinLengthX / 2;
            int yLength = currentMap.nLinLengthY;
            int xBegin = currentMap.nLinBeginX;
            int yBegin = currentMap.nLinBeginY;

            // 建立 tileList_t1 和 tileList_t3 陣列
            int[,] tileList_t1 = new int[xLength, yLength];
            int[,] tileList_t3 = new int[xLength, yLength];

            // 初始化為不可通行
            for (int x = 0; x < xLength; x++)
            {
                for (int y = 0; y < yLength; y++)
                {
                    tileList_t1[x, y] = 1;
                    tileList_t3[x, y] = 1;
                }
            }

            // 從 S32 資料填充 tileList
            foreach (var s32Data in allS32DataDict.Values)
            {
                int offsetX = (s32Data.SegInfo.nLinBeginX - xBegin) / 2;
                int offsetY = s32Data.SegInfo.nLinBeginY - yBegin;

                for (int ly = 0; ly < 64; ly++)
                {
                    for (int lx = 0; lx < 64; lx++)
                    {
                        var attr = s32Data.Layer3[ly, lx];
                        if (attr == null) continue;

                        int gx = offsetX + lx;
                        int gy = offsetY + ly;

                        if (gx >= 0 && gx < xLength && gy >= 0 && gy < yLength)
                        {
                            tileList_t1[gx, gy] = attr.Attribute1;
                            tileList_t3[gx, gy] = attr.Attribute2;
                        }
                    }
                }
            }

            // 計算 8 方向通行性
            int[,] tileList = new int[xLength, yLength];

            for (int x = 0; x < xLength; x++)
            {
                for (int y = 0; y < yLength; y++)
                {
                    if (x + 1 < xLength && y + 1 < yLength && x - 1 >= 0 && y - 1 >= 0)
                    {
                        // D0: 下方
                        if ((tileList_t1[x, y + 1] & 1) == 0)
                            tileList[x, y] += 1;
                        // D4: 上方
                        if ((tileList_t1[x, y] & 1) == 0)
                            tileList[x, y] += 2;
                        // D2: 左方
                        if ((tileList_t3[x - 1, y] & 1) == 0)
                            tileList[x, y] += 4;
                        // D6: 右方
                        if ((tileList_t3[x, y] & 1) == 0)
                            tileList[x, y] += 8;

                        // D1: 左下對角
                        if (IsPassable_D1(tileList_t1, tileList_t3, x - 1, y + 1, xLength, yLength))
                            tileList[x, y] += 16;
                        // D3: 左上對角
                        if (IsPassable_D3(tileList_t1, tileList_t3, x - 1, y - 1, xLength, yLength))
                            tileList[x, y] += 32;
                        // D5: 右上對角
                        if (IsPassable_D5(tileList_t1, tileList_t3, x + 1, y - 1, xLength, yLength))
                            tileList[x, y] += 64;
                        // D7: 右下對角
                        if (IsPassable_D7(tileList_t1, tileList_t3, x + 1, y + 1, xLength, yLength))
                            tileList[x, y] += 128;

                        // 區域類型
                        tileList[x, y] += GetZone(tileList_t1[x, y]);
                    }
                }
            }

            // 轉換為 L1J 格式
            int[,] l1jData = FormatL1J(tileList, xLength, yLength);

            // 寫入檔案
            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                for (int y = 0; y < yLength; y++)
                {
                    StringBuilder line = new StringBuilder();
                    for (int x = 0; x < xLength; x++)
                    {
                        if (x > 0) line.Append(",");
                        line.Append(l1jData[x, y]);
                    }
                    writer.WriteLine(line.ToString());
                }
            }
        }

        /// <summary>
        /// 對角方向通行性判斷 D1 (左下)
        /// 根據客戶端逆向 sub_4F5910：只有當特定兩個相鄰格子都是障礙時才阻擋
        /// 邏輯：!((A && B) || (A && C) || (D && B) || (D && C))
        /// </summary>
        private static bool IsPassable_D1(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 0 || x + 1 >= xLen || y < 0 || y >= yLen || y - 1 < 0) return false;

            bool a = (t1[x, y] & 1) != 0;
            bool b = (t1[x + 1, y] & 1) != 0;
            bool c = (t3[x + 1, y] & 1) != 0;
            bool d = (t3[x + 1, y - 1] & 1) != 0;

            // 客戶端邏輯：任意兩個特定相鄰格子都是障礙才阻擋
            return !((a && b) || (a && c) || (d && b) || (d && c));
        }

        /// <summary>
        /// 對角方向通行性判斷 D3 (左上)
        /// 根據客戶端逆向 sub_4F5910
        /// </summary>
        private static bool IsPassable_D3(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 0 || x + 1 >= xLen || y < 0 || y + 1 >= yLen) return false;

            bool a = (t1[x, y + 1] & 1) != 0;
            bool b = (t1[x + 1, y + 1] & 1) != 0;
            bool c = (t3[x, y] & 1) != 0;
            bool d = (t3[x, y + 1] & 1) != 0;

            return !((a && b) || (a && c) || (d && b) || (d && c));
        }

        /// <summary>
        /// 對角方向通行性判斷 D5 (右上)
        /// 根據客戶端逆向 sub_4F5910
        /// </summary>
        private static bool IsPassable_D5(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 1 || x >= xLen || y < 0 || y + 1 >= yLen) return false;

            bool a = (t1[x, y + 1] & 1) != 0;
            bool b = (t1[x - 1, y + 1] & 1) != 0;
            bool c = (t3[x - 1, y] & 1) != 0;
            bool d = (t3[x - 1, y + 1] & 1) != 0;

            return !((a && b) || (a && c) || (d && b) || (d && c));
        }

        /// <summary>
        /// 對角方向通行性判斷 D7 (右下)
        /// 根據客戶端逆向 sub_4F5910
        /// </summary>
        private static bool IsPassable_D7(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 1 || x >= xLen || y < 1 || y >= yLen) return false;

            bool a = (t1[x, y] & 1) != 0;
            bool b = (t1[x - 1, y] & 1) != 0;
            bool c = (t3[x - 1, y] & 1) != 0;
            bool d = (t3[x - 1, y - 1] & 1) != 0;

            return !((a && b) || (a && c) || (d && b) || (d && c));
        }

        /// <summary>
        /// 取得區域類型
        /// </summary>
        private static int GetZone(int tileValue)
        {
            string hex = (tileValue & 0x0F).ToString("X1");
            if (hex == "0" || hex == "1" || hex == "2" || hex == "3")
                return 256;
            else if (hex == "4" || hex == "5" || hex == "6" || hex == "7" ||
                     hex == "C" || hex == "D" || hex == "E" || hex == "F")
                return 512;
            else if (hex == "8" || hex == "9" || hex == "A" || hex == "B")
                return 1024;
            return 256;
        }

        /// <summary>
        /// 轉換為 L1J 格式
        /// </summary>
        private static int[,] FormatL1J(int[,] tileList, int xLength, int yLength)
        {
            int[,] result = new int[xLength, yLength];

            for (int y = 0; y < yLength; y++)
            {
                for (int x = 0; x < xLength; x++)
                {
                    int tile = tileList[x, y];

                    if ((tile & 1) == 1 || (tile & 2) == 2)
                        result[x, y] += 2;

                    if ((tile & 4) == 4 || (tile & 8) == 8)
                        result[x, y] += 1;

                    if ((tile & 1) == 1 && (tile & 2) == 2)
                        result[x, y] += 8;

                    if ((tile & 4) == 4 && (tile & 8) == 8)
                        result[x, y] += 4;

                    if ((tile & 512) == 512)
                        result[x, y] += 16;

                    if ((tile & 1024) == 1024)
                        result[x, y] += 32;
                }
            }

            return result;
        }

        /// <summary>
        /// 取得屬性旗標描述（使用 Layer3AttributeDecoder 統一處理）
        /// </summary>
        public static string GetAttributeFlags(short value)
        {
            return Layer3AttributeDecoder.GetAttributeFlags(value);
        }
    }
}
