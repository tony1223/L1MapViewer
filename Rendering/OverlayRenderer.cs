using System;
using System.Collections.Generic;
using Eto.Drawing;
using L1MapViewer.Compatibility;
using L1MapViewer.Helper;
using L1MapViewer.Models;

namespace L1MapViewer.Rendering
{
    /// <summary>
    /// 覆蓋層渲染器 - 負責繪製格線、通行性、區域等覆蓋層
    /// 從 MapForm.cs 抽離的渲染邏輯，不依賴 WinForms 控件
    /// </summary>
    public class OverlayRenderer
    {
        /// <summary>
        /// 渲染所有啟用的覆蓋層到 Bitmap
        /// </summary>
        public void RenderOverlays(
            Bitmap bitmap,
            Rectangle worldRect,
            IEnumerable<S32Data> s32Files,
            RenderOptions options)
        {
            if (options.ShowLayer3Attributes)
            {
                DrawLayer3Attributes(bitmap, worldRect, s32Files);
            }

            if (options.ShowPassability)
            {
                DrawPassability(bitmap, worldRect, s32Files);
            }

            if (options.ShowSafeZones || options.ShowCombatZones)
            {
                DrawRegions(bitmap, worldRect, s32Files, options.ShowSafeZones, options.ShowCombatZones);
            }

            if (options.ShowGrid)
            {
                DrawGrid(bitmap, worldRect, s32Files);
            }

            if (options.ShowS32Boundary)
            {
                DrawS32Boundary(bitmap, worldRect, s32Files);
            }

            if (options.ShowLayer5)
            {
                DrawLayer5(bitmap, worldRect, s32Files, options.IsLayer5EditMode);
            }

            if (options.ShowCoordinateLabels)
            {
                DrawCoordinateLabels(bitmap, worldRect, s32Files);
            }
        }

        /// <summary>
        /// 繪製 Layer3 屬性（菱形邊線）
        /// </summary>
        public void DrawLayer3Attributes(Bitmap bitmap, Rectangle worldRect, IEnumerable<S32Data> s32Files)
        {
            using (Graphics g = GraphicsHelper.FromImage(bitmap))
            {
                g.SetSmoothingMode(SmoothingMode.AntiAlias);

                foreach (var s32Data in s32Files)
                {
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    int mx = loc[0];
                    int my = loc[1];

                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 64; x++)
                        {
                            var attr = s32Data.Layer3[y, x];
                            if (attr == null) continue;
                            if (attr.Attribute1 == 0 && attr.Attribute2 == 0) continue;

                            int x1 = x * 2;
                            int localBaseX = 0 - 24 * (x1 / 2);
                            int localBaseY = 63 * 12 - 12 * (x1 / 2);

                            int X = mx + localBaseX + x1 * 24 + y * 24 - worldRect.X;
                            int Y = my + localBaseY + y * 12 - worldRect.Y;

                            // 跳過不在 Viewport 內的格子
                            if (X + 48 < 0 || X > worldRect.Width || Y + 24 < 0 || Y > worldRect.Height)
                                continue;

                            Point pLeft = new Point(X + 0, Y + 12);
                            Point pTop = new Point(X + 24, Y + 0);
                            Point pRight = new Point(X + 48, Y + 12);

                            if (attr.Attribute1 != 0)
                            {
                                Color color = GetAttributeColor(attr.Attribute1);
                                using (Pen pen = new Pen(color, 3))
                                {
                                    g.DrawLine(pen, pLeft, pTop);
                                }
                            }

                            if (attr.Attribute2 != 0)
                            {
                                Color color = GetAttributeColor(attr.Attribute2);
                                using (Pen pen = new Pen(color, 3))
                                {
                                    g.DrawLine(pen, pTop, pRight);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 繪製通行性覆蓋層
        /// </summary>
        public void DrawPassability(Bitmap bitmap, Rectangle worldRect, IEnumerable<S32Data> s32Files)
        {
            using (Graphics g = GraphicsHelper.FromImage(bitmap))
            {
                g.SetSmoothingMode(SmoothingMode.AntiAlias);

                using (Pen penImpassable = new Pen(Color.FromArgb(255, 128, 0, 128), 3))
                using (Pen penPassable = new Pen(Color.FromArgb(255, 50, 200, 255), 2))
                {
                    foreach (var s32Data in s32Files)
                    {
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int mx = loc[0];
                        int my = loc[1];

                        for (int y = 0; y < 64; y++)
                        {
                            for (int x = 0; x < 64; x++)
                            {
                                var attr = s32Data.Layer3[y, x];
                                if (attr == null) continue;

                                int x1 = x * 2;
                                int localBaseX = 0 - 24 * (x1 / 2);
                                int localBaseY = 63 * 12 - 12 * (x1 / 2);

                                int X = mx + localBaseX + x1 * 24 + y * 24 - worldRect.X;
                                int Y = my + localBaseY + y * 12 - worldRect.Y;

                                // 跳過不在 Viewport 內的格子
                                if (X + 48 < 0 || X > worldRect.Width || Y + 24 < 0 || Y > worldRect.Height)
                                    continue;

                                Point pLeft = new Point(X + 0, Y + 12);
                                Point pTop = new Point(X + 24, Y + 0);
                                Point pRight = new Point(X + 48, Y + 12);

                                Pen penLeft = (attr.Attribute1 & 0x01) != 0 ? penImpassable : penPassable;
                                g.DrawLine(penLeft, pLeft, pTop);

                                Pen penRight = (attr.Attribute2 & 0x01) != 0 ? penImpassable : penPassable;
                                g.DrawLine(penRight, pTop, pRight);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 繪製區域覆蓋層（安全區/戰鬥區）
        /// </summary>
        public void DrawRegions(Bitmap bitmap, Rectangle worldRect, IEnumerable<S32Data> s32Files, bool showSafeZones, bool showCombatZones)
        {
            using (Graphics g = GraphicsHelper.FromImage(bitmap))
            {
                // 定義區域顏色（半透明）
                using (Brush safeBrush = new SolidBrush(Color.FromArgb(80, 0, 150, 255)))       // 藍色
                using (Brush combatBrush = new SolidBrush(Color.FromArgb(80, 180, 0, 255)))     // 紫色
                {
                    foreach (var s32Data in s32Files)
                    {
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int mx = loc[0];
                        int my = loc[1];

                        for (int y = 0; y < 64; y++)
                        {
                            for (int x = 0; x < 64; x++)
                            {
                                var attr = s32Data.Layer3[y, x];

                                // 使用 Layer3AttributeDecoder 統一處理（含例外值替換）
                                bool isSafe = attr != null && (Layer3AttributeDecoder.IsSafeZone(attr.Attribute1) || Layer3AttributeDecoder.IsSafeZone(attr.Attribute2));
                                bool isCombat = attr != null && (Layer3AttributeDecoder.IsCombatZone(attr.Attribute1) || Layer3AttributeDecoder.IsCombatZone(attr.Attribute2));

                                // 根據顯示選項過濾
                                if (isSafe && !showSafeZones) isSafe = false;
                                if (isCombat && !showCombatZones) isCombat = false;

                                // 如果沒有需要顯示的區域，跳過
                                if (!isSafe && !isCombat)
                                    continue;

                                int x1 = x * 2;
                                int localBaseX = 0 - 24 * (x1 / 2);
                                int localBaseY = 63 * 12 - 12 * (x1 / 2);

                                int X = mx + localBaseX + x1 * 24 + y * 24 - worldRect.X;
                                int Y = my + localBaseY + y * 12 - worldRect.Y;

                                // 跳過不在 Viewport 內的格子
                                if (X + 48 < 0 || X > worldRect.Width || Y + 24 < 0 || Y > worldRect.Height)
                                    continue;

                                // 繪製菱形格子的區域標記
                                Point[] diamond = new Point[]
                                {
                                    new Point(X + 24, Y + 0),      // 上
                                    new Point(X + 48, Y + 12),     // 右
                                    new Point(X + 24, Y + 24),     // 下
                                    new Point(X + 0, Y + 12)       // 左
                                };

                                // 選擇對應的顏色（戰鬥區優先）
                                Brush regionBrush = isCombat ? combatBrush : safeBrush;
                                g.FillPolygon(regionBrush, diamond);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 繪製遊戲格線
        /// </summary>
        public void DrawGrid(Bitmap bitmap, Rectangle worldRect, IEnumerable<S32Data> s32Files)
        {
            using (Graphics g = GraphicsHelper.FromImage(bitmap))
            {
                using (Pen gridPen = new Pen(ColorExtensions.FromArgb(100, Eto.Drawing.Colors.Red), 1))
                {
                    foreach (var s32Data in s32Files)
                    {
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int mx = loc[0];
                        int my = loc[1];

                        for (int y = 0; y < 64; y++)
                        {
                            for (int x3 = 0; x3 < 64; x3++)
                            {
                                int x = x3 * 2;

                                int localBaseX = 0 - 24 * (x / 2);
                                int localBaseY = 63 * 12 - 12 * (x / 2);

                                int X = mx + localBaseX + x * 24 + y * 24 - worldRect.X;
                                int Y = my + localBaseY + y * 12 - worldRect.Y;

                                // 跳過不在 Viewport 內的格子
                                if (X + 48 < 0 || X > worldRect.Width || Y + 24 < 0 || Y > worldRect.Height)
                                    continue;

                                Point p1 = new Point(X, Y + 12);
                                Point p2 = new Point(X + 24, Y);
                                Point p3 = new Point(X + 48, Y + 12);
                                Point p4 = new Point(X + 24, Y + 24);

                                g.DrawLine(gridPen, p1, p2);
                                g.DrawLine(gridPen, p2, p3);
                                g.DrawLine(gridPen, p3, p4);
                                g.DrawLine(gridPen, p4, p1);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 繪製 S32 區塊邊界
        /// </summary>
        public void DrawS32Boundary(Bitmap bitmap, Rectangle worldRect, IEnumerable<S32Data> s32Files)
        {
            using (Graphics g = GraphicsHelper.FromImage(bitmap))
            {
                g.SetSmoothingMode(SmoothingMode.AntiAlias);
                // TextRenderingHint not supported in Eto.Drawing

                using (Font font = new Font("Arial", 9, FontStyle.Bold))
                using (Pen boundaryPen = new Pen(Eto.Drawing.Colors.Cyan, 2))
                {
                    foreach (var s32Data in s32Files)
                    {
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int mx = loc[0];
                        int my = loc[1];

                        Point[] corners = new Point[4];
                        int[][] cornerCoords = new int[][] {
                            new int[] { 0, 0 },
                            new int[] { 64, 0 },
                            new int[] { 64, 64 },
                            new int[] { 0, 64 }
                        };

                        for (int i = 0; i < 4; i++)
                        {
                            int x3 = cornerCoords[i][0];
                            int y = cornerCoords[i][1];
                            int x = x3 * 2;

                            int localBaseX = 0 - 24 * (x / 2);
                            int localBaseY = 63 * 12 - 12 * (x / 2);
                            int X = mx + localBaseX + x * 24 + y * 24 - worldRect.X;
                            int Y = my + localBaseY + y * 12 - worldRect.Y;

                            corners[i] = new Point(X, Y + 12);
                        }

                        g.DrawLine(boundaryPen, corners[0], corners[1]);
                        g.DrawLine(boundaryPen, corners[1], corners[2]);
                        g.DrawLine(boundaryPen, corners[2], corners[3]);
                        g.DrawLine(boundaryPen, corners[3], corners[0]);

                        int centerX = (corners[0].X + corners[2].X) / 2;
                        int centerY = (corners[0].Y + corners[2].Y) / 2;
                        string centerText = $"GetLoc({mx},{my})\n{s32Data.SegInfo.nLinBeginX},{s32Data.SegInfo.nLinBeginY}~{s32Data.SegInfo.nLinEndX},{s32Data.SegInfo.nLinEndY}";

                        using (SolidBrush cb = new SolidBrush(ColorExtensions.FromArgb(200, Eto.Drawing.Colors.Black)))
                        using (SolidBrush ct = new SolidBrush(Eto.Drawing.Colors.Lime))
                        {
                            SizeF cs = g.MeasureString(centerText, font);
                            g.FillRectangle(cb, centerX - cs.Width / 2 - 2, centerY - cs.Height / 2 - 1, cs.Width + 4, cs.Height + 2);
                            g.DrawString(centerText, font, ct, centerX - cs.Width / 2, centerY - cs.Height / 2);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 繪製 Layer5 覆蓋層（透明圖塊標記）
        /// </summary>
        public void DrawLayer5(Bitmap bitmap, Rectangle worldRect, IEnumerable<S32Data> s32Files, bool isEditMode)
        {
            using (Graphics g = GraphicsHelper.FromImage(bitmap))
            {
                g.SetSmoothingMode(SmoothingMode.AntiAlias);

                // 收集所有 Layer5 位置（去重）
                var drawnPositions = new HashSet<(int mx, int my, int x, int y)>();

                // 半透明藍色填充和邊框
                using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(80, 60, 140, 255)))
                using (Pen borderPen = new Pen(Color.FromArgb(180, 80, 160, 255), 1.5f))
                using (Pen highlightPen = new Pen(Color.FromArgb(200, 150, 200, 255), 1f))
                {
                    foreach (var s32Data in s32Files)
                    {
                        if (s32Data.Layer5.Count == 0) continue;

                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int mx = loc[0];
                        int my = loc[1];

                        foreach (var item in s32Data.Layer5)
                        {
                            // 同位置只畫一次
                            var posKey = (mx, my, (int)item.X, (int)item.Y);
                            if (drawnPositions.Contains(posKey)) continue;
                            drawnPositions.Add(posKey);

                            int x1 = item.X;
                            int y = item.Y;

                            int localBaseX = 0 - 24 * (x1 / 2);
                            int localBaseY = 63 * 12 - 12 * (x1 / 2);

                            int X = mx + localBaseX + x1 * 24 + y * 24 - worldRect.X;
                            int Y = my + localBaseY + y * 12 - worldRect.Y;

                            // 跳過不在 Viewport 內的格子
                            if (X + 24 < 0 || X > worldRect.Width || Y + 12 < 0 || Y > worldRect.Height)
                                continue;

                            // 繪製半格三角形（根據 X 奇偶決定左半或右半）
                            Point[] triangle;
                            if (x1 % 2 == 0)
                            {
                                // 偶數 X：左半三角形
                                Point pLeft = new Point(X + 0, Y + 12);
                                Point pTop = new Point(X + 24, Y + 0);
                                Point pBottom = new Point(X + 24, Y + 24);
                                triangle = new Point[] { pLeft, pTop, pBottom };

                                g.FillPolygon(fillBrush, triangle);
                                g.DrawLine(highlightPen, pLeft, pTop);
                                g.DrawLine(borderPen, pTop, pBottom);
                                g.DrawLine(borderPen, pBottom, pLeft);
                            }
                            else
                            {
                                // 奇數 X：右半三角形
                                Point pTop = new Point(X + 0, Y + 0);
                                Point pRight = new Point(X + 24, Y + 12);
                                Point pBottom = new Point(X + 0, Y + 24);
                                triangle = new Point[] { pTop, pRight, pBottom };

                                g.FillPolygon(fillBrush, triangle);
                                g.DrawLine(highlightPen, pTop, pRight);
                                g.DrawLine(borderPen, pRight, pBottom);
                                g.DrawLine(borderPen, pBottom, pTop);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 繪製群組高亮覆蓋層（綠色標記）
        /// </summary>
        public void DrawGroupHighlight(Bitmap bitmap, Rectangle worldRect, IEnumerable<S32Data> s32Files, List<(int globalX, int globalY)> highlightCells)
        {
            if (highlightCells == null || highlightCells.Count == 0)
                return;

            // 建立快速查找的 HashSet
            var highlightSet = new HashSet<(int, int)>(highlightCells);

            using (Graphics g = GraphicsHelper.FromImage(bitmap))
            {
                g.SetSmoothingMode(SmoothingMode.AntiAlias);

                // 綠色半透明填充
                using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(100, 50, 200, 50)))
                using (Pen borderPen = new Pen(Color.FromArgb(200, 30, 180, 30), 2f))
                {
                    foreach (var s32Data in s32Files)
                    {
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int mx = loc[0];
                        int my = loc[1];

                        int segStartX = s32Data.SegInfo.nLinBeginX * 2;
                        int segStartY = s32Data.SegInfo.nLinBeginY;

                        // 檢查此 S32 範圍內是否有高亮格子
                        for (int localY = 0; localY < 64; localY++)
                        {
                            for (int localX = 0; localX < 128; localX += 2)  // 每次跳 2（一格）
                            {
                                int globalX = segStartX + localX;
                                int globalY = segStartY + localY;

                                if (!highlightSet.Contains((globalX, globalY)))
                                    continue;

                                // 計算像素位置（整格，包含左右兩半）
                                int x1 = localX;  // 偶數 X（左半）
                                int localBaseX = 0 - 24 * (x1 / 2);
                                int localBaseY = 63 * 12 - 12 * (x1 / 2);

                                int X = mx + localBaseX + x1 * 24 + localY * 24 - worldRect.X;
                                int Y = my + localBaseY + localY * 12 - worldRect.Y;

                                // 跳過不在 Viewport 內的格子
                                if (X + 48 < 0 || X > worldRect.Width || Y + 24 < 0 || Y > worldRect.Height)
                                    continue;

                                // 繪製整格菱形
                                Point[] diamond = new Point[]
                                {
                                    new Point(X + 24, Y),       // 上
                                    new Point(X + 48, Y + 12),  // 右
                                    new Point(X + 24, Y + 24),  // 下
                                    new Point(X, Y + 12)        // 左
                                };

                                g.FillPolygon(fillBrush, diamond);
                                g.DrawPolygon(borderPen, diamond);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 繪製座標標籤
        /// </summary>
        public void DrawCoordinateLabels(Bitmap bitmap, Rectangle worldRect, IEnumerable<S32Data> s32Files)
        {
            using (Graphics g = GraphicsHelper.FromImage(bitmap))
            {
                g.SetSmoothingMode(SmoothingMode.AntiAlias);
                // TextRenderingHint not available in Eto.Drawing

                using (Font font = new Font("Arial", 8, FontStyle.Bold))
                {
                    int interval = 10;

                    foreach (var s32Data in s32Files)
                    {
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int mx = loc[0];
                        int my = loc[1];

                        for (int y = 0; y < 64; y += interval)
                        {
                            for (int x = 0; x < 128; x += interval)
                            {
                                int localBaseX = 0 - 24 * (x / 2);
                                int localBaseY = 63 * 12 - 12 * (x / 2);

                                int X = mx + localBaseX + x * 24 + y * 24 - worldRect.X;
                                int Y = my + localBaseY + y * 12 - worldRect.Y;

                                // 跳過不在 Viewport 內的格子
                                if (X + 24 < 0 || X > worldRect.Width || Y + 24 < 0 || Y > worldRect.Height)
                                    continue;

                                int gameX = s32Data.SegInfo.nLinBeginX + x;
                                int gameY = s32Data.SegInfo.nLinBeginY + y;

                                string coordText = $"{gameX},{gameY}";
                                SizeF textSize = g.MeasureString(coordText, font);

                                int textX = X + 12 - (int)textSize.Width / 2;
                                int textY = Y + 12 - (int)textSize.Height / 2;

                                using (SolidBrush bgBrush = new SolidBrush(ColorExtensions.FromArgb(180, Eto.Drawing.Colors.White)))
                                {
                                    g.FillRectangle(bgBrush, textX - 2, textY - 1, textSize.Width + 4, textSize.Height + 2);
                                }

                                using (SolidBrush textBrush = new SolidBrush(Eto.Drawing.Colors.Blue))
                                {
                                    g.DrawString(coordText, font, textBrush, textX, textY);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 繪製高亮選中的格子
        /// </summary>
        public void DrawHighlightedCell(Bitmap bitmap, Rectangle worldRect, S32Data s32Data, int cellX, int cellY)
        {
            if (s32Data == null) return;

            using (Graphics g = GraphicsHelper.FromImage(bitmap))
            {
                g.SetSmoothingMode(SmoothingMode.AntiAlias);

                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                int localBaseX = 0 - 24 * (cellX / 2);
                int localBaseY = 63 * 12 - 12 * (cellX / 2);

                int X = mx + localBaseX + cellX * 24 + cellY * 24 - worldRect.X;
                int Y = my + localBaseY + cellY * 12 - worldRect.Y;

                Point p1 = new Point(X + 0, Y + 12);
                Point p2 = new Point(X + 12, Y + 0);
                Point p3 = new Point(X + 24, Y + 12);
                Point p4 = new Point(X + 12, Y + 24);

                using (SolidBrush brush = new SolidBrush(Color.FromArgb(120, 255, 255, 0)))
                {
                    g.FillPolygon(brush, new Point[] { p1, p2, p3, p4 });
                }

                using (Pen pen = new Pen(Color.FromArgb(255, 255, 200, 0), 3))
                {
                    g.DrawPolygon(pen, new Point[] { p1, p2, p3, p4 });
                }
            }
        }

        /// <summary>
        /// 根據屬性值取得對應顏色
        /// </summary>
        public static Color GetAttributeColor(short attrValue)
        {
            return Color.FromArgb(230, 200, 200, 200);
            // 以下為備用顏色對照表
            /*
            switch (attrValue)
            {
                case 0x0001: return Color.FromArgb(230, 255, 0, 0);       // 紅色
                case 0x0002: return Color.FromArgb(230, 0, 100, 255);     // 藍色
                case 0x0003: return Color.FromArgb(230, 180, 0, 180);     // 紫色
                case 0x0004: return Color.FromArgb(230, 255, 200, 0);     // 黃色
                case 0x0005: return Color.FromArgb(230, 255, 100, 0);     // 橘色
                case 0x0006: return Color.FromArgb(230, 0, 200, 100);     // 綠色
                case 0x0007: return Color.FromArgb(230, 0, 200, 200);     // 青色
                case 0x0008: return Color.FromArgb(230, 200, 100, 50);    // 棕色
                case 0x0009: return Color.FromArgb(230, 255, 150, 150);   // 粉紅
                case 0x000A: return Color.FromArgb(230, 150, 255, 150);   // 淺綠
                case 0x000B: return Color.FromArgb(230, 150, 150, 255);   // 淺藍
                case 0x000C: return Color.FromArgb(230, 255, 255, 100);   // 淺黃
                case 0x000D: return Color.FromArgb(230, 255, 100, 255);   // 洋紅
                case 0x000E: return Color.FromArgb(230, 100, 255, 255);   // 淺青
                case 0x000F: return Color.FromArgb(230, 200, 200, 200);   // 淺灰
                default:
                    int r = (attrValue * 37) % 200 + 55;
                    int g = (attrValue * 73) % 200 + 55;
                    int b = (attrValue * 113) % 200 + 55;
                    return Color.FromArgb(230, r, g, b);
            }
            */
        }
    }
}
