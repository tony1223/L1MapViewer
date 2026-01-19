using System;
using System.Collections.Generic;
using System.Diagnostics;
// using System.Drawing; // Replaced with Eto.Drawing
using L1MapViewer.Models;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// 格子查找器 - 根據世界座標找到對應的 S32 和格子
    /// </summary>
    public static class CellFinder
    {
        /// <summary>
        /// 查找結果
        /// </summary>
        public class FindResult
        {
            public S32Data S32Data { get; set; }
            public int CellX { get; set; }
            public int CellY { get; set; }
            public bool Found { get; set; }
            public long ElapsedMs { get; set; }
            public int S32Checked { get; set; }
            public int CellsChecked { get; set; }
        }

        /// <summary>
        /// 根據世界座標找到對應的格子（暴力搜尋法）
        /// 擴展範圍: X 0-255, Y 0-127 (支援超出邊界的物件)
        /// </summary>
        public static FindResult FindCellBruteForce(int worldX, int worldY, IEnumerable<S32Data> s32Files)
        {
            var sw = Stopwatch.StartNew();
            var result = new FindResult { Found = false };

            foreach (var s32Data in s32Files)
            {
                result.S32Checked++;

                // 使用 GetLoc 計算區塊位置
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 遍歷該 S32 的所有格子（擴展範圍 Y: 0-127, X: 0-255）
                for (int y = 0; y < 128; y++)
                {
                    for (int x = 0; x < 256; x++)
                    {
                        result.CellsChecked++;

                        // 使用 GetLoc + drawTilBlock 公式計算像素位置
                        int localBaseX = 0;
                        int localBaseY = 63 * 12;
                        localBaseX -= 24 * (x / 2);
                        localBaseY -= 12 * (x / 2);

                        int X = mx + localBaseX + x * 24 + y * 24;
                        int Y = my + localBaseY + y * 12;

                        // 檢查點擊位置是否在這個菱形內（使用數學公式而非 GDI+）
                        if (IsPointInDiamond(worldX, worldY, X, Y, 24, 24))
                        {
                            result.Found = true;
                            result.S32Data = s32Data;
                            result.CellX = x;
                            result.CellY = y;
                            sw.Stop();
                            result.ElapsedMs = sw.ElapsedMilliseconds;
                            return result;
                        }
                    }
                }
            }

            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;
            return result;
        }

        /// <summary>
        /// 根據世界座標找到對應的格子（優化版：先過濾 S32 範圍）
        /// 擴展範圍: X 0-255, Y 0-127 (支援超出邊界的物件)
        /// </summary>
        public static FindResult FindCellOptimized(int worldX, int worldY, IEnumerable<S32Data> s32Files)
        {
            var sw = Stopwatch.StartNew();
            var result = new FindResult { Found = false };

            // 擴展區塊範圍以支援超出邊界的格子
            // 原始區塊: 3072 x 1536
            // 擴展後需要覆蓋更大的區域（向各方向擴展）
            const int ExtendedBlockWidth = 12288;  // 256 * 24 * 2 (覆蓋擴展的 X 範圍)
            const int ExtendedBlockHeight = 6144;  // 256 * 12 * 2 (覆蓋擴展的 Y 範圍)
            const int OffsetX = -3072;    // 擴展區域可能向左延伸
            const int OffsetY = -1536;    // 擴展區域可能向上延伸

            foreach (var s32Data in s32Files)
            {
                result.S32Checked++;

                // 使用 GetLoc 計算區塊位置
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 先檢查點擊位置是否在這個 S32 的擴展範圍內（使用更寬鬆的邊界）
                if (worldX < mx + OffsetX || worldX >= mx + ExtendedBlockWidth + OffsetX ||
                    worldY < my + OffsetY || worldY >= my + ExtendedBlockHeight + OffsetY)
                {
                    continue; // 跳過不在範圍內的 S32
                }

                // 遍歷該 S32 的所有格子（擴展範圍 Y: 0-127, X: 0-255）
                for (int y = 0; y < 128; y++)
                {
                    for (int x = 0; x < 256; x++)
                    {
                        result.CellsChecked++;

                        // 使用 GetLoc + drawTilBlock 公式計算像素位置
                        int localBaseX = 0;
                        int localBaseY = 63 * 12;
                        localBaseX -= 24 * (x / 2);
                        localBaseY -= 12 * (x / 2);

                        int X = mx + localBaseX + x * 24 + y * 24;
                        int Y = my + localBaseY + y * 12;

                        // 檢查點擊位置是否在這個菱形內
                        if (IsPointInDiamond(worldX, worldY, X, Y, 24, 24))
                        {
                            result.Found = true;
                            result.S32Data = s32Data;
                            result.CellX = x;
                            result.CellY = y;
                            sw.Stop();
                            result.ElapsedMs = sw.ElapsedMilliseconds;
                            return result;
                        }
                    }
                }
            }

            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;
            return result;
        }

        /// <summary>
        /// 檢查點是否在菱形內（純數學計算，不使用 GDI+）
        /// 菱形的四個頂點：(X, Y+12), (X+12, Y), (X+24, Y+12), (X+12, Y+24)
        /// </summary>
        private static bool IsPointInDiamond(int px, int py, int diamondX, int diamondY, int width, int height)
        {
            // 菱形中心點
            int centerX = diamondX + width / 2;  // X + 12
            int centerY = diamondY + height / 2; // Y + 12

            // 將點轉換為相對於中心的座標
            int dx = Math.Abs(px - centerX);
            int dy = Math.Abs(py - centerY);

            // 菱形的半寬和半高
            int halfWidth = width / 2;   // 12
            int halfHeight = height / 2; // 12

            // 菱形內的點滿足: |dx|/halfWidth + |dy|/halfHeight <= 1
            // 即: dx * halfHeight + dy * halfWidth <= halfWidth * halfHeight
            return dx * halfHeight + dy * halfWidth <= halfWidth * halfHeight;
        }

        /// <summary>
        /// 檢查點是否在菱形內（使用數學方法，跨平台相容）
        /// </summary>
        public static bool IsPointInDiamondGdi(Point p, Point p1, Point p2, Point p3, Point p4)
        {
            // Use cross-product based point-in-polygon test
            return IsPointInPolygon(p, new[] { p1, p2, p3, p4 });
        }

        /// <summary>
        /// 檢查點是否在多邊形內（使用向量叉積方法）
        /// </summary>
        private static bool IsPointInPolygon(Point test, Point[] polygon)
        {
            if (polygon.Length < 3) return false;

            int positive = 0;
            int negative = 0;

            for (int i = 0; i < polygon.Length; i++)
            {
                Point p1 = polygon[i];
                Point p2 = polygon[(i + 1) % polygon.Length];

                // Cross product to determine which side of the edge the point is on
                int cross = (p2.X - p1.X) * (test.Y - p1.Y) - (p2.Y - p1.Y) * (test.X - p1.X);

                if (cross > 0) positive++;
                else if (cross < 0) negative++;

                // If we have both positive and negative, point is outside
                if (positive > 0 && negative > 0) return false;
            }

            return true;
        }
    }
}
