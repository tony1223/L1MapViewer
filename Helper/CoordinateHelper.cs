using System;
using System.Collections.Generic;
using System.Drawing;
using L1MapViewer.Models;
using L1MapViewer.Other;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// 座標轉換輔助類別
    /// </summary>
    public class CoordinateHelper
    {
        /// <summary>
        /// 根據遊戲座標找到對應的 S32Data
        /// </summary>
        public static S32Data? GetS32DataByGameCoords(int gameX, int gameY, Dictionary<string, S32Data> allS32DataDict)
        {
            foreach (var s32Data in allS32DataDict.Values)
            {
                // 使用 ContainsGameCoord 檢查實際邊界
                if (s32Data.ContainsGameCoord(gameX, gameY))
                {
                    return s32Data;
                }
            }
            return null;
        }

        /// <summary>
        /// 螢幕座標轉換為遊戲座標
        /// </summary>
        public static (int gameX, int gameY, S32Data? s32Data, int localX, int localY) ScreenToGameCoords(
            int screenX, int screenY,
            Dictionary<string, S32Data> allS32DataDict)
        {
            foreach (var s32Data in allS32DataDict.Values)
            {
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 使用 Layer3 格子
                for (int y = 0; y < 64; y++)
                {
                    for (int x3 = 0; x3 < 64; x3++)
                    {
                        int x = x3 * 2;  // Layer1 座標

                        int localBaseX = 0;
                        int localBaseY = 63 * 12;
                        localBaseX -= 24 * (x / 2);
                        localBaseY -= 12 * (x / 2);

                        int X = mx + localBaseX + x * 24 + y * 24;
                        int Y = my + localBaseY + y * 12;

                        // Layer3 菱形的四個頂點（48x24）
                        Point p1 = new Point(X, Y + 12);       // 左
                        Point p2 = new Point(X + 24, Y);       // 上
                        Point p3 = new Point(X + 48, Y + 12);  // 右
                        Point p4 = new Point(X + 24, Y + 24);  // 下

                        if (IsPointInDiamond(new Point(screenX, screenY), p1, p2, p3, p4))
                        {
                            int gameX = s32Data.SegInfo.nLinBeginX + x3;
                            int gameY = s32Data.SegInfo.nLinBeginY + y;
                            return (gameX, gameY, s32Data, x, y);
                        }
                    }
                }
            }
            return (-1, -1, null, -1, -1);
        }

        /// <summary>
        /// 遊戲座標轉換為螢幕座標中心點
        /// </summary>
        public static (int screenX, int screenY) GameToScreenCoords(
            int gameX, int gameY,
            Dictionary<string, S32Data> allS32DataDict)
        {
            foreach (var s32Data in allS32DataDict.Values)
            {
                int localX = gameX - s32Data.SegInfo.nLinBeginX;
                int localY = gameY - s32Data.SegInfo.nLinBeginY;

                if (localX >= 0 && localX < 128 && localY >= 0 && localY < 64)
                {
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    int mx = loc[0];
                    int my = loc[1];

                    int localBaseX = 0;
                    int localBaseY = 63 * 12;
                    localBaseX -= 24 * (localX / 2);
                    localBaseY -= 12 * (localX / 2);

                    int X = mx + localBaseX + localX * 24 + localY * 24;
                    int Y = my + localBaseY + localY * 12;

                    return (X + 12, Y + 12);
                }
            }
            return (-1, -1);
        }

        /// <summary>
        /// 檢查點是否在菱形內
        /// </summary>
        public static bool IsPointInDiamond(Point p, Point p1, Point p2, Point p3, Point p4)
        {
            // 使用向量叉積判斷點是否在四邊形內
            return CrossProduct(p1, p2, p) >= 0 &&
                   CrossProduct(p2, p3, p) >= 0 &&
                   CrossProduct(p3, p4, p) >= 0 &&
                   CrossProduct(p4, p1, p) >= 0;
        }

        /// <summary>
        /// 計算向量叉積
        /// </summary>
        private static int CrossProduct(Point o, Point a, Point b)
        {
            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        }

        /// <summary>
        /// 檢查點是否在等距菱形內
        /// </summary>
        public static bool IsPointInIsometricRegion(float px, float py, float centerX, float centerY, float halfWidth, float halfHeight)
        {
            float dx = Math.Abs(px - centerX) / halfWidth;
            float dy = Math.Abs(py - centerY) / halfHeight;
            return (dx + dy) <= 1.0f;
        }

        /// <summary>
        /// 取得等距菱形區域內的所有格子
        /// </summary>
        public static List<SelectedCell> GetCellsInIsometricRegion(
            Rectangle region,
            Dictionary<string, S32Data> allS32DataDict)
        {
            List<SelectedCell> result = new List<SelectedCell>();

            float centerX = region.Left + region.Width / 2f;
            float centerY = region.Top + region.Height / 2f;
            float halfWidth = region.Width / 2f;
            float halfHeight = region.Height / 2f;

            foreach (var s32Data in allS32DataDict.Values)
            {
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        int localBaseX = 0;
                        int localBaseY = 63 * 12;
                        localBaseX -= 24 * (x / 2);
                        localBaseY -= 12 * (x / 2);

                        int X = mx + localBaseX + x * 24 + y * 24;
                        int Y = my + localBaseY + y * 12;

                        float cellCenterX = X + 12;
                        float cellCenterY = Y + 12;

                        if (IsPointInIsometricRegion(cellCenterX, cellCenterY, centerX, centerY, halfWidth, halfHeight))
                        {
                            result.Add(new SelectedCell
                            {
                                S32Data = s32Data,
                                LocalX = x,
                                LocalY = y
                            });
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 取得螢幕矩形區域內的所有格子
        /// </summary>
        public static List<SelectedCell> GetCellsInScreenRect(
            Rectangle screenRect,
            Dictionary<string, S32Data> allS32DataDict)
        {
            List<SelectedCell> result = new List<SelectedCell>();

            foreach (var s32Data in allS32DataDict.Values)
            {
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        int localBaseX = 0;
                        int localBaseY = 63 * 12;
                        localBaseX -= 24 * (x / 2);
                        localBaseY -= 12 * (x / 2);

                        int X = mx + localBaseX + x * 24 + y * 24;
                        int Y = my + localBaseY + y * 12;

                        int cellCenterX = X + 12;
                        int cellCenterY = Y + 12;

                        if (screenRect.Contains(cellCenterX, cellCenterY))
                        {
                            result.Add(new SelectedCell
                            {
                                S32Data = s32Data,
                                LocalX = x,
                                LocalY = y
                            });
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 從螢幕起點到終點，計算等距投影矩形範圍內的所有格子
        /// </summary>
        public static List<SelectedCell> GetCellsInIsometricRange(
            Point startPoint, Point endPoint,
            Dictionary<string, S32Data> allS32DataDict)
        {
            List<SelectedCell> result = new List<SelectedCell>();

            var startCoords = ScreenToGameCoords(startPoint.X, startPoint.Y, allS32DataDict);
            var endCoords = ScreenToGameCoords(endPoint.X, endPoint.Y, allS32DataDict);

            if (startCoords.gameX < 0)
                return result;

            if (endCoords.gameX < 0)
            {
                endCoords = startCoords;
            }

            int minGameX = Math.Min(startCoords.gameX, endCoords.gameX);
            int maxGameX = Math.Max(startCoords.gameX, endCoords.gameX);
            int minGameY = Math.Min(startCoords.gameY, endCoords.gameY);
            int maxGameY = Math.Max(startCoords.gameY, endCoords.gameY);

            foreach (var s32Data in allS32DataDict.Values)
            {
                for (int y = 0; y < 64; y++)
                {
                    for (int x3 = 0; x3 < 64; x3++)
                    {
                        int gameX = s32Data.SegInfo.nLinBeginX + x3;
                        int gameY = s32Data.SegInfo.nLinBeginY + y;

                        if (gameX >= minGameX && gameX <= maxGameX &&
                            gameY >= minGameY && gameY <= maxGameY)
                        {
                            result.Add(new SelectedCell
                            {
                                S32Data = s32Data,
                                LocalX = x3 * 2,
                                LocalY = y
                            });
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 根據選中的格子計算對齊格子的菱形邊界
        /// </summary>
        public static Rectangle GetAlignedBoundsFromCells(List<SelectedCell> cells)
        {
            if (cells.Count == 0)
                return new Rectangle();

            int minScreenX = int.MaxValue, maxScreenX = int.MinValue;
            int minScreenY = int.MaxValue, maxScreenY = int.MinValue;

            foreach (var cell in cells)
            {
                int[] loc = cell.S32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                int localBaseX = 0;
                int localBaseY = 63 * 12;
                localBaseX -= 24 * (cell.LocalX / 2);
                localBaseY -= 12 * (cell.LocalX / 2);

                int X = mx + localBaseX + cell.LocalX * 24 + cell.LocalY * 24;
                int Y = my + localBaseY + cell.LocalY * 12;

                int left = X + 0;
                int right = X + 24;
                int top = Y + 0;
                int bottom = Y + 24;

                minScreenX = Math.Min(minScreenX, left);
                maxScreenX = Math.Max(maxScreenX, right);
                minScreenY = Math.Min(minScreenY, top);
                maxScreenY = Math.Max(maxScreenY, bottom);
            }

            int centerX = (minScreenX + maxScreenX) / 2;
            int centerY = (minScreenY + maxScreenY) / 2;
            int width = maxScreenX - minScreenX;
            int height = maxScreenY - minScreenY;

            return new Rectangle(centerX - width / 2, centerY - height / 2, width, height);
        }

        /// <summary>
        /// Layer1 本地座標轉換為遊戲座標
        /// </summary>
        /// <param name="s32Data">S32 資料</param>
        /// <param name="localX">Layer1 本地 X (0-127)</param>
        /// <param name="localY">Layer1 本地 Y (0-63)</param>
        /// <returns>遊戲座標 (gameX, gameY)</returns>
        public static (int gameX, int gameY) LocalToGameCoords(S32Data s32Data, int localX, int localY)
        {
            int gameX = s32Data.SegInfo.nLinBeginX + (localX / 2);
            int gameY = s32Data.SegInfo.nLinBeginY + localY;
            return (gameX, gameY);
        }

        /// <summary>
        /// 遊戲座標轉換為 Layer1 本地座標
        /// </summary>
        /// <param name="s32Data">S32 資料</param>
        /// <param name="gameX">遊戲座標 X</param>
        /// <param name="gameY">遊戲座標 Y</param>
        /// <returns>Layer1 本地座標 (localX, localY)，localX 為偶數</returns>
        public static (int localX, int localY) GameToLocalCoords(S32Data s32Data, int gameX, int gameY)
        {
            int localX = (gameX - s32Data.SegInfo.nLinBeginX) * 2;
            int localY = gameY - s32Data.SegInfo.nLinBeginY;
            return (localX, localY);
        }

        /// <summary>
        /// 計算格子的螢幕位置
        /// </summary>
        public static (int X, int Y) GetCellScreenPosition(S32Data s32Data, int localX, int localY)
        {
            int[] loc = s32Data.SegInfo.GetLoc(1.0);
            int mx = loc[0];
            int my = loc[1];

            int localBaseX = 0;
            int localBaseY = 63 * 12;
            localBaseX -= 24 * (localX / 2);
            localBaseY -= 12 * (localX / 2);

            int X = mx + localBaseX + localX * 24 + localY * 24;
            int Y = my + localBaseY + localY * 12;

            return (X, Y);
        }
    }
}
