using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using L1MapViewer.Helper;
using L1MapViewer.Models;
using L1MapViewer.Other;
using L1MapViewer.Localization;
using Eto.Drawing;
using NLog;

namespace L1FlyMapViewer
{
    public partial class MapForm
    {
        // Logger for overlay (MapForm 主檔案已有 _logger)

        #region SkiaSharp Overlay 繪製函數

        // 輔助函數：取得屬性對應的 SKColor
        private SKColor GetAttributeColorSK(int attribute)
        {
            // 參考 GetAttributeColor 函數
            return attribute switch
            {
                1 => new SKColor(255, 0, 0, 150),     // 紅色
                2 => new SKColor(0, 255, 0, 150),     // 綠色
                3 => new SKColor(0, 0, 255, 150),     // 藍色
                4 => new SKColor(255, 255, 0, 150),   // 黃色
                5 => new SKColor(255, 0, 255, 150),   // 紫色
                6 => new SKColor(0, 255, 255, 150),   // 青色
                7 => new SKColor(255, 128, 0, 150),   // 橙色
                8 => new SKColor(128, 0, 255, 150),   // 紫羅蘭
                _ => new SKColor(128, 128, 128, 150)  // 灰色
            };
        }

        // 繪製 Layer3 屬性覆蓋層 (SkiaSharp 版本)
        // 參考 DrawLayer3AttributesViewport
        private void DrawLayer3AttributesViewportSK(SKCanvas canvas, Struct.L1Map currentMap, Rectangle worldRect, IEnumerable<S32Data> s32FilesSnapshot)
        {
            foreach (var s32Data in s32FilesSnapshot)
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

                        // 菱形的頂點
                        var pTop = new SKPoint(X + 24, Y + 0);
                        var pRight = new SKPoint(X + 48, Y + 12);
                        var pBottom = new SKPoint(X + 24, Y + 24);
                        var pLeft = new SKPoint(X + 0, Y + 12);
                        var pCenter = new SKPoint(X + 24, Y + 12);

                        // 左半邊 - 使用 Attribute1
                        if (attr.Attribute1 != 0)
                        {
                            var color1 = GetAttributeColorSK(attr.Attribute1);
                            using var pen = new SKPaint { Color = color1, Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };
                            canvas.DrawLine(pLeft, pTop, pen);
                            canvas.DrawLine(pTop, pCenter, pen);
                            canvas.DrawLine(pCenter, pBottom, pen);
                            canvas.DrawLine(pBottom, pLeft, pen);
                        }

                        // 右半邊 - 使用 Attribute2
                        if (attr.Attribute2 != 0)
                        {
                            var color2 = GetAttributeColorSK(attr.Attribute2);
                            using var pen = new SKPaint { Color = color2, Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };
                            canvas.DrawLine(pTop, pRight, pen);
                            canvas.DrawLine(pRight, pBottom, pen);
                            canvas.DrawLine(pBottom, pCenter, pen);
                            canvas.DrawLine(pCenter, pTop, pen);
                        }
                    }
                }
            }
        }

        // 繪製通行性覆蓋層 (SkiaSharp 版本)
        // 參考 DrawPassableOverlayViewport - 繪製邊線而非填充
        // Attribute1 = 左上邊線, Attribute2 = 右上邊線
        private void DrawPassableOverlayViewportSK(SKCanvas canvas, Struct.L1Map currentMap, Rectangle worldRect, IEnumerable<S32Data> s32FilesSnapshot)
        {
            // 不可通行：紫色粗線，可通行：青色細線
            using var penImpassable = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3,
                Color = new SKColor(128, 0, 128, 255)  // 紫色
            };
            using var penPassable = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                Color = new SKColor(50, 200, 255, 255)  // 青色
            };

            foreach (var s32Data in s32FilesSnapshot)
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

                        if (X + 48 < 0 || X > worldRect.Width || Y + 24 < 0 || Y > worldRect.Height)
                            continue;

                        // 菱形頂點
                        var pTop = new SKPoint(X + 24, Y + 0);
                        var pRight = new SKPoint(X + 48, Y + 12);
                        var pLeft = new SKPoint(X + 0, Y + 12);

                        // 左上邊線 - 使用 Attribute1 bit0 判斷
                        var pen1 = (attr.Attribute1 & 0x01) != 0 ? penImpassable : penPassable;
                        canvas.DrawLine(pLeft, pTop, pen1);

                        // 右上邊線 - 使用 Attribute2 bit0 判斷
                        var pen2 = (attr.Attribute2 & 0x01) != 0 ? penImpassable : penPassable;
                        canvas.DrawLine(pTop, pRight, pen2);
                    }
                }
            }
        }

        // 繪製區域覆蓋層 (SkiaSharp 版本)
        // 參考 DrawRegionsOverlayViewport - 使用 Attribute1 低4位判斷整個格子
        // 低4位: 0-3=一般, 4-7/C-F=安全(bit2), 8-B=戰鬥(bit3且非bit2)
        private void DrawRegionsOverlayViewportSK(SKCanvas canvas, Struct.L1Map currentMap, Rectangle worldRect, bool showSafe, bool showCombat, IEnumerable<S32Data> s32FilesSnapshot)
        {
            // 安全區：藍色，戰鬥區：紅色（與舊版一致）
            using var safeBrush = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = new SKColor(0, 150, 255, 80)  // 藍色半透明
            };
            using var combatBrush = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = new SKColor(255, 50, 50, 80)  // 紅色半透明
            };

            foreach (var s32Data in s32FilesSnapshot)
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

                        if (X + 48 < 0 || X > worldRect.Width || Y + 24 < 0 || Y > worldRect.Height)
                            continue;

                        // 使用 Layer3AttributeDecoder 統一處理（含例外值替換）
                        bool isSafe = Layer3AttributeDecoder.IsSafeZone(attr.Attribute1);
                        bool isCombat = Layer3AttributeDecoder.IsCombatZone(attr.Attribute1);

                        // 決定整個格子的顏色
                        SKPaint regionBrush = null;
                        if (isCombat && showCombat)
                            regionBrush = combatBrush;
                        else if (isSafe && showSafe)
                            regionBrush = safeBrush;

                        if (regionBrush != null)
                        {
                            // 繪製整個菱形
                            using var path = new SKPath();
                            path.MoveTo(X + 24, Y + 0);      // 上
                            path.LineTo(X + 48, Y + 12);     // 右
                            path.LineTo(X + 24, Y + 24);     // 下
                            path.LineTo(X + 0, Y + 12);      // 左
                            path.Close();
                            canvas.DrawPath(path, regionBrush);
                        }
                    }
                }
            }
        }

        // 繪製格線 (SkiaSharp 版本)
        // 參考 DrawS32GridViewport
        private void DrawS32GridViewportSK(SKCanvas canvas, Struct.L1Map currentMap, Rectangle worldRect, IEnumerable<S32Data> s32FilesSnapshot)
        {
            using var gridPaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                Color = new SKColor(255, 0, 0, 100)
            };

            foreach (var s32Data in s32FilesSnapshot)
            {
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 繪製 "/" 方向的線
                for (int i = 0; i <= 64; i++)
                {
                    int x3 = i;
                    int x = x3 * 2;

                    int startLocalBaseX = -24 * (x / 2);
                    int startLocalBaseY = 63 * 12 - 12 * (x / 2);
                    int startX = mx + startLocalBaseX + x * 24 + 0 * 24 + 24 - worldRect.X;
                    int startY = my + startLocalBaseY + 0 * 12 - worldRect.Y;

                    // 終點: y=63 格子的右頂點 = (X+48, Y+12)
                    int endX = mx + startLocalBaseX + x * 24 + 63 * 24 + 48 - worldRect.X;
                    int endY = my + startLocalBaseY + 63 * 12 + 12 - worldRect.Y;

                    if (endX >= 0 && startX <= worldRect.Width &&
                        Math.Max(startY, endY) >= 0 && Math.Min(startY, endY) <= worldRect.Height)
                    {
                        canvas.DrawLine(startX, startY, endX, endY, gridPaint);
                    }
                }

                // 繪製 "\" 方向的線（沿著 x 方向的邊）
                // 總共需要 65 條線
                for (int j = 0; j <= 64; j++)
                {
                    int y = j;

                    // 起點 (x3=0 時的左頂點)
                    int x = 0;
                    int startLocalBaseX = -24 * (x / 2);
                    int startLocalBaseY = 63 * 12 - 12 * (x / 2);
                    int startX = mx + startLocalBaseX + x * 24 + y * 24 - worldRect.X;
                    int startY = my + startLocalBaseY + y * 12 + 12 - worldRect.Y; // +12 是到左頂點

                    // 終點 (x3=63 時的上頂點) = (X+24, Y)
                    x = 63 * 2;
                    int endLocalBaseX = -24 * (x / 2);
                    int endLocalBaseY = 63 * 12 - 12 * (x / 2);
                    int endX = mx + endLocalBaseX + x * 24 + y * 24 + 24 - worldRect.X;
                    int endY = my + endLocalBaseY + y * 12 - worldRect.Y;

                    if (endX >= 0 && startX <= worldRect.Width &&
                        Math.Max(startY, endY) >= 0 && Math.Min(startY, endY) <= worldRect.Height)
                    {
                        canvas.DrawLine(startX, startY, endX, endY, gridPaint);
                    }
                }
            }
        }

        // 繪製座標標籤 (SkiaSharp 版本)
        // 參考 DrawCoordinateLabelsViewport
        private void DrawCoordinateLabelsViewportSK(SKCanvas canvas, Struct.L1Map currentMap, Rectangle worldRect, IEnumerable<S32Data> s32FilesSnapshot)
        {
            using var textPaint = new SKPaint
            {
                IsAntialias = true,
                TextSize = 10,
                Color = SKColors.Blue,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            };
            using var bgPaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = new SKColor(255, 255, 255, 180)
            };

            int interval = 10;

            foreach (var s32Data in s32FilesSnapshot)
            {
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                for (int y = 0; y < 64; y += interval)
                {
                    for (int x = 0; x < 128; x += interval)
                    {
                        int localBaseX = -24 * (x / 2);
                        int localBaseY = 63 * 12 - 12 * (x / 2);
                        int pixelX = mx + localBaseX + x * 24 + y * 24 - worldRect.X;
                        int pixelY = my + localBaseY + y * 12 - worldRect.Y;

                        // 跳過不在 Viewport 內的格子
                        if (pixelX + 24 < 0 || pixelX > worldRect.Width ||
                            pixelY + 24 < 0 || pixelY > worldRect.Height)
                            continue;

                        // 計算遊戲座標
                        int gameX = s32Data.SegInfo.nLinBeginX + x / 2;  // Layer1 座標轉遊戲座標
                        int gameY = s32Data.SegInfo.nLinBeginY + y;

                        string text = $"{gameX},{gameY}";
                        var bounds = new SKRect();
                        textPaint.MeasureText(text, ref bounds);

                        int textX = pixelX + 12 - (int)bounds.Width / 2;
                        int textY = pixelY + 12 - (int)bounds.Height / 2;

                        canvas.DrawRect(textX - 2, textY - 1, bounds.Width + 4, bounds.Height + 2, bgPaint);
                        canvas.DrawText(text, textX, textY + bounds.Height, textPaint);
                    }
                }
            }
        }

        // 繪製 S32 邊界 (SkiaSharp 版本)
        // 參考 DrawS32BoundaryOnlyViewport
        private void DrawS32BoundaryOnlyViewportSK(SKCanvas canvas, Struct.L1Map currentMap, Rectangle worldRect, IEnumerable<S32Data> s32FilesSnapshot)
        {
            using var boundaryPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                Color = SKColors.Cyan
            };
            using var textPaint = new SKPaint
            {
                IsAntialias = true,
                TextSize = 10,
                Color = SKColors.Lime,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            };
            using var bgPaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = new SKColor(0, 0, 0, 200)
            };

            foreach (var s32Data in s32FilesSnapshot)
            {
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 計算四個角點
                var corners = new SKPoint[4];
                int[] cornerX3 = { 0, 64, 64, 0 };
                int[] cornerY = { 0, 0, 64, 64 };

                for (int i = 0; i < 4; i++)
                {
                    int x3 = cornerX3[i];
                    int y = cornerY[i];
                    int x = x3 * 2;

                    int localBaseX = -24 * (x / 2);
                    int localBaseY = 63 * 12 - 12 * (x / 2);
                    int X = mx + localBaseX + x * 24 + y * 24 - worldRect.X;
                    int Y = my + localBaseY + y * 12 - worldRect.Y;

                    corners[i] = new SKPoint(X, Y + 12);
                }

                canvas.DrawLine(corners[0], corners[1], boundaryPaint);
                canvas.DrawLine(corners[1], corners[2], boundaryPaint);
                canvas.DrawLine(corners[2], corners[3], boundaryPaint);
                canvas.DrawLine(corners[3], corners[0], boundaryPaint);

                // 繪製中心標籤
                float centerX = (corners[0].X + corners[2].X) / 2;
                float centerY = (corners[0].Y + corners[2].Y) / 2;
                string centerText = $"({mx},{my})";

                var bounds = new SKRect();
                textPaint.MeasureText(centerText, ref bounds);
                canvas.DrawRect(centerX - bounds.Width / 2 - 2, centerY - bounds.Height / 2 - 1, bounds.Width + 4, bounds.Height + 2, bgPaint);
                canvas.DrawText(centerText, centerX - bounds.Width / 2, centerY + bounds.Height / 2, textPaint);
            }
        }

        // 繪製 Layer5 覆蓋層 (SkiaSharp 版本)
        // 完全複製自 DrawLayer5OverlayViewport
        private void DrawLayer5OverlayViewportSK(SKCanvas canvas, Struct.L1Map currentMap, Rectangle worldRect, bool isLayer5Edit, IEnumerable<S32Data> s32FilesSnapshot, HashSet<string> checkedFilePaths)
        {
            // 收集所有 Layer5 位置（去重）
            var drawnPositions = new HashSet<(int mx, int my, int x, int y)>();

            // 半透明藍色填充和邊框 (原版顏色: ARGB 80,60,140,255)
            using var fillPaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = new SKColor(60, 140, 255, 80)
            };
            using var borderPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                Color = new SKColor(80, 160, 255, 180)
            };
            using var highlightPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                Color = new SKColor(150, 200, 255, 200)
            };

            foreach (var s32Data in s32FilesSnapshot)
            {
                // 只繪製已啟用的 S32
                if (!checkedFilePaths.Contains(s32Data.FilePath)) continue;
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

                    // Layer5 的 X 是 0-127（Layer1 座標系），Y 是 0-63
                    // 繪製半格大小的三角形（X 切半）
                    int x1 = item.X;  // 0-127
                    int y = item.Y;   // 0-63

                    int localBaseX = 0 - 24 * (x1 / 2);
                    int localBaseY = 63 * 12 - 12 * (x1 / 2);

                    int X = mx + localBaseX + x1 * 24 + y * 24 - worldRect.X;
                    int Y = my + localBaseY + y * 12 - worldRect.Y;

                    // 跳過不在 Viewport 內的格子
                    if (X + 24 < 0 || X > worldRect.Width || Y + 12 < 0 || Y > worldRect.Height)
                        continue;

                    // 繪製半格三角形（根據 X 奇偶決定左半或右半）
                    using var path = new SKPath();
                    if (x1 % 2 == 0)
                    {
                        // 偶數 X：左半三角形
                        float pLeftX = X + 0, pLeftY = Y + 12;
                        float pTopX = X + 24, pTopY = Y + 0;
                        float pBottomX = X + 24, pBottomY = Y + 24;

                        path.MoveTo(pLeftX, pLeftY);
                        path.LineTo(pTopX, pTopY);
                        path.LineTo(pBottomX, pBottomY);
                        path.Close();

                        canvas.DrawPath(path, fillPaint);
                        // 邊框（上亮下暗）
                        canvas.DrawLine(pLeftX, pLeftY, pTopX, pTopY, highlightPaint);
                        canvas.DrawLine(pTopX, pTopY, pBottomX, pBottomY, borderPaint);
                        canvas.DrawLine(pBottomX, pBottomY, pLeftX, pLeftY, borderPaint);
                    }
                    else
                    {
                        // 奇數 X：右半三角形
                        float pTopX = X + 0, pTopY = Y + 0;
                        float pRightX = X + 24, pRightY = Y + 12;
                        float pBottomX = X + 0, pBottomY = Y + 24;

                        path.MoveTo(pTopX, pTopY);
                        path.LineTo(pRightX, pRightY);
                        path.LineTo(pBottomX, pBottomY);
                        path.Close();

                        canvas.DrawPath(path, fillPaint);
                        // 邊框（上亮下暗）
                        canvas.DrawLine(pTopX, pTopY, pRightX, pRightY, highlightPaint);
                        canvas.DrawLine(pRightX, pRightY, pBottomX, pBottomY, borderPaint);
                        canvas.DrawLine(pBottomX, pBottomY, pTopX, pTopY, borderPaint);
                    }
                }
            }

            // 在透明編輯模式下，繪製已設定 Layer5 的群組物件覆蓋層
            if (isLayer5Edit)
            {
                DrawLayer5GroupOverlaySK(canvas, worldRect, s32FilesSnapshot, checkedFilePaths);
            }
        }

        // 繪製 Layer5 群組覆蓋層 (SkiaSharp 版本)
        // 完全複製自 DrawLayer5GroupOverlay
        private void DrawLayer5GroupOverlaySK(SKCanvas canvas, Rectangle worldRect, IEnumerable<S32Data> s32FilesSnapshot, HashSet<string> checkedFilePaths)
        {
            // 只有在有選取格子時才顯示
            if (_editState.SelectedCells.Count == 0) return;

            // 從選取的格子收集 Layer5 的 GroupId 及其 Type
            var groupLayer5Info = new Dictionary<int, byte>(); // GroupId -> Type
            foreach (var selectedCell in _editState.SelectedCells)
            {
                var s32Data = selectedCell.S32Data;
                int localX = selectedCell.LocalX;  // Layer1 座標 (0-127)
                int localY = selectedCell.LocalY;  // Layer3 座標 (0-63)

                // 查找該格子位置對應的 Layer5 設定
                // Layer5 的 X 是 0-127，Y 是 0-63
                // selectedCell.LocalX 是 Layer1 座標 (0-127)，LocalY 是 (0-63)
                // 一個遊戲格子對應兩個 Layer1 X 座標（localX 和 localX+1）
                foreach (var item in s32Data.Layer5)
                {
                    if ((item.X == localX || item.X == localX + 1) && item.Y == localY)
                    {
                        // 如果同一個 GroupId 有多個設定，保留第一個
                        if (!groupLayer5Info.ContainsKey(item.ObjectIndex))
                        {
                            groupLayer5Info[item.ObjectIndex] = item.Type;
                        }
                    }
                }
            }

            if (groupLayer5Info.Count == 0) return;

            // 半透明覆蓋色：Type=0 紫色（高對比），Type=1 紅色
            using var type0Paint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = new SKColor(180, 0, 255, 100)
            };
            using var type1Paint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = new SKColor(255, 80, 80, 100)
            };

            foreach (var s32Data in s32FilesSnapshot)
            {
                // 只繪製已啟用的 S32
                if (!checkedFilePaths.Contains(s32Data.FilePath)) continue;

                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                foreach (var obj in s32Data.Layer4)
                {
                    // 檢查該群組是否有 Layer5 設定
                    if (!groupLayer5Info.TryGetValue(obj.GroupId, out byte type))
                        continue;

                    // 使用與高亮格子相同的座標計算方式
                    int x1 = obj.X;  // 0-127 (Layer1 座標系)
                    int y = obj.Y;   // 0-63

                    int localBaseX = 0 - 24 * (x1 / 2);
                    int localBaseY = 63 * 12 - 12 * (x1 / 2);

                    int X = mx + localBaseX + x1 * 24 + y * 24 - worldRect.X;
                    int Y = my + localBaseY + y * 12 - worldRect.Y;

                    // 跳過不在 Viewport 內的物件
                    if (X + 24 < 0 || X > worldRect.Width || Y + 24 < 0 || Y > worldRect.Height)
                        continue;

                    // 繪製半格菱形覆蓋（與格子高亮相同大小）
                    var paint = type == 0 ? type0Paint : type1Paint;
                    using var path = new SKPath();
                    path.MoveTo(X + 0, Y + 12);   // 左
                    path.LineTo(X + 12, Y + 0);   // 上
                    path.LineTo(X + 24, Y + 12);  // 右
                    path.LineTo(X + 12, Y + 24);  // 下
                    path.Close();
                    canvas.DrawPath(path, paint);
                }
            }
        }

        // 繪製群組高亮覆蓋層 (SkiaSharp 版本)
        // 參考 DrawGroupHighlightOverlay
        private void DrawGroupHighlightOverlaySK(SKCanvas canvas, Rectangle worldRect, List<(int, int)> groupHighlightCells, IEnumerable<S32Data> s32FilesSnapshot)
        {
            using var fillPaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = new SKColor(0, 255, 0, 80) // 綠色半透明
            };
            using var strokePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                Color = new SKColor(0, 255, 0, 200)
            };

            foreach (var (cellX, cellY) in groupHighlightCells)
            {
                // 找到包含這個遊戲座標的 S32
                foreach (var s32Data in s32FilesSnapshot)
                {
                    if (cellX < s32Data.SegInfo.nLinBeginX || cellX >= s32Data.SegInfo.nLinEndX ||
                        cellY < s32Data.SegInfo.nLinBeginY || cellY >= s32Data.SegInfo.nLinEndY)
                        continue;

                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    int mx = loc[0];
                    int my = loc[1];

                    int localX3 = cellX - s32Data.SegInfo.nLinBeginX;
                    int localY = cellY - s32Data.SegInfo.nLinBeginY;
                    int x = localX3 * 2;

                    int localBaseX = -24 * (x / 2);
                    int localBaseY = 63 * 12 - 12 * (x / 2);
                    int pixelX = mx + localBaseX + x * 24 + localY * 24 - worldRect.X;
                    int pixelY = my + localBaseY + localY * 12 - worldRect.Y;

                    if (pixelX + 48 < 0 || pixelX > worldRect.Width ||
                        pixelY + 24 < 0 || pixelY > worldRect.Height)
                        continue;

                    var path = new SKPath();
                    path.MoveTo(pixelX + 24, pixelY);
                    path.LineTo(pixelX + 48, pixelY + 12);
                    path.LineTo(pixelX + 24, pixelY + 24);
                    path.LineTo(pixelX, pixelY + 12);
                    path.Close();

                    canvas.DrawPath(path, fillPaint);
                    canvas.DrawPath(path, strokePaint);
                    break;
                }
            }
        }

        // 繪製選中格子高亮 (SkiaSharp 版本)
        // 參考 DrawHighlightedCellViewport
        private void DrawHighlightedCellViewportSK(SKCanvas canvas, Rectangle worldRect, S32Data highlightedS32Data, int highlightedCellX, int highlightedCellY)
        {
            _logger.Debug($"[HIGHLIGHT-SK] Enter: s32Data={highlightedS32Data?.FilePath}, cellX={highlightedCellX}, cellY={highlightedCellY}");

            if (highlightedS32Data == null)
            {
                _logger.Debug("[HIGHLIGHT-SK] Exit: s32Data is null");
                return;
            }

            // RGB565 不支援 alpha，使用 Fill + Stroke 繪製明顯的高亮
            using var fillPaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = new SKColor(255, 255, 0) // 亮黃色填充
            };
            using var strokePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3,
                Color = new SKColor(255, 128, 0) // 橙色邊框
            };

            int[] loc = highlightedS32Data.SegInfo.GetLoc(1.0);
            int mx = loc[0];
            int my = loc[1];

            // highlightedCellX 是 Layer1 座標 (0-127)
            // 公式與原版 DrawHighlightedCellViewport 完全一致
            int localBaseX = 0 - 24 * (highlightedCellX / 2);
            int localBaseY = 63 * 12 - 12 * (highlightedCellX / 2);

            int X = mx + localBaseX + highlightedCellX * 24 + highlightedCellY * 24 - worldRect.X;
            int Y = my + localBaseY + highlightedCellY * 12 - worldRect.Y;

            _logger.Debug($"[HIGHLIGHT-SK] worldRect=({worldRect.X},{worldRect.Y},{worldRect.Width},{worldRect.Height}), mx={mx}, my={my}, X={X}, Y={Y}");

            // 菱形四個角點 (與原版完全一致: p1=左, p2=上, p3=右, p4=下)
            using var path = new SKPath();
            path.MoveTo(X + 0, Y + 12);   // p1 左
            path.LineTo(X + 12, Y + 0);   // p2 上
            path.LineTo(X + 24, Y + 12);  // p3 右
            path.LineTo(X + 12, Y + 24);  // p4 下
            path.Close();

            canvas.DrawPath(path, fillPaint);
            canvas.DrawPath(path, strokePaint);

            _logger.Debug($"[HIGHLIGHT-SK] Done drawing at ({X},{Y})");
        }

        // PaintOverlaySK callback - 繪製所有覆蓋層（Layer8、選取格子等）
        private void DrawSelectedCellsSK(SKCanvas canvas, float zoomLevel, int scrollX, int scrollY)
        {
            // 繪製 Layer8 標記和 SPR 動畫
            if (_viewState.ShowLayer8)
            {
                DrawLayer8OverlaySK(canvas, zoomLevel, scrollX, scrollY);
            }

            // 繪製選取的格子
            DrawSelectedCellsOnlySK(canvas, zoomLevel, scrollX, scrollY);

            // 繪製編輯模式的 Help Label
            DrawEditModeHelpLabelSK(canvas);
        }

        // 繪製編輯模式的 Help Label（固定位置，不隨地圖捲動）
        private void DrawEditModeHelpLabelSK(SKCanvas canvas)
        {
            string helpText = null;
            SKColor bgColor;
            SKColor textColor;

            // 根據當前編輯模式決定顯示內容
            if (_editState.IsLayer5EditMode)
            {
                helpText = "【透明編輯模式】\n" +
                           "• 左鍵：選取地圖格子\n" +
                           "• 查看右側【附近群組】\n" +
                           "• 右鍵：設定半透明/消失\n" +
                           "  紫色 = 半透明區塊\n" +
                           "  紅色 = 消失區塊\n" +
                           "• 再按按鈕：取消模式";
                bgColor = new SKColor(30, 30, 50, 220);
                textColor = new SKColor(100, 180, 255);
            }
            else if (currentPassableEditMode != PassableEditMode.None)
            {
                helpText = "【通行編輯模式】\n" +
                           "• 左鍵拖曳選取區域\n" +
                           "• 右鍵：設定通行性\n" +
                           "  - 上/下/左/右 阻擋\n" +
                           "  - 整格 可/不可通行\n" +
                           "• 再按按鈕：取消模式";
                bgColor = new SKColor(30, 30, 30, 200);
                textColor = new SKColor(173, 216, 230); // LightBlue
            }
            else if (currentRegionEditMode != RegionEditMode.None)
            {
                helpText = "【區域設置模式】\n" +
                           "• 左鍵拖曳選取區域\n" +
                           "• 右鍵：選擇區域類型\n" +
                           "  - 一般區域（灰色）\n" +
                           "  - 安全區域（藍色）\n" +
                           "  - 戰鬥區域（紅色）\n" +
                           "• 再按按鈕：取消模式";
                bgColor = new SKColor(40, 80, 40, 200);
                textColor = new SKColor(144, 238, 144); // LightGreen
            }
            else
            {
                // 沒有編輯模式，繪製預設提示
                helpText = LocalizationManager.L("Hint_MouseControls");
                if (string.IsNullOrEmpty(helpText))
                {
                    helpText = "【操作說明】\n" +
                               "• 滾輪：縮放\n" +
                               "• 左鍵拖曳：移動地圖\n" +
                               "• 右鍵：選單";
                }
                bgColor = new SKColor(50, 50, 50, 180);
                textColor = new SKColor(255, 255, 255);
            }

            // 繪製 Help Label
            float x = 10;
            float y = 10;
            float padding = 8;
            float fontSize = 13;
            float lineHeight = fontSize * 1.4f;

            using var textPaint = new SKPaint
            {
                IsAntialias = true,
                TextSize = fontSize,
                Color = textColor,
                Typeface = SKTypeface.FromFamilyName("Microsoft JhengHei", SKFontStyle.Normal)
            };

            // 計算文字區域大小
            string[] lines = helpText.Split('\n');
            float maxWidth = 0;
            foreach (var line in lines)
            {
                float lineWidth = textPaint.MeasureText(line);
                if (lineWidth > maxWidth) maxWidth = lineWidth;
            }
            float boxWidth = maxWidth + padding * 2;
            float boxHeight = lines.Length * lineHeight + padding * 2;

            // 繪製背景
            using var bgPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = bgColor
            };
            canvas.DrawRoundRect(x, y, boxWidth, boxHeight, 4, 4, bgPaint);

            // 繪製邊框
            using var borderPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                Color = new SKColor(100, 100, 100, 200)
            };
            canvas.DrawRoundRect(x, y, boxWidth, boxHeight, 4, 4, borderPaint);

            // 繪製文字
            float textY = y + padding + fontSize;
            foreach (var line in lines)
            {
                canvas.DrawText(line, x + padding, textY, textPaint);
                textY += lineHeight;
            }
        }

        // 繪製 Layer8 覆蓋層（SK 版本）
        // 參考 DrawLayer8OverlayOnControl
        private void DrawLayer8OverlaySK(SKCanvas canvas, float zoomLevel, int scrollX, int scrollY)
        {
            if (_document?.S32Files == null || _document.S32Files.Count == 0) return;

            int viewportWidth = (int)canvas.LocalClipBounds.Width;
            int viewportHeight = (int)canvas.LocalClipBounds.Height;

            foreach (var s32Data in _document.S32Files.Values)
            {
                if (s32Data.Layer8.Count == 0) continue;

                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                for (int i = 0; i < s32Data.Layer8.Count; i++)
                {
                    var item = s32Data.Layer8[i];

                    // Layer8 X,Y 是絕對遊戲座標，先轉為本地座標
                    int localLayer3X = item.X - s32Data.SegInfo.nLinBeginX;
                    int localLayer3Y = item.Y - s32Data.SegInfo.nLinBeginY;

                    if (localLayer3X < 0 || localLayer3X > 63 || localLayer3Y < 0 || localLayer3Y > 63)
                        continue;

                    int layer1X = localLayer3X * 2;
                    int layer1Y = localLayer3Y;

                    int baseX = -24 * (layer1X / 2);
                    int baseY = 63 * 12 - 12 * (layer1X / 2);

                    // Marker 位置：格子中心 (+12, +12)
                    int markerWorldX = mx + baseX + layer1X * 24 + layer1Y * 24 + 12;
                    int markerWorldY = my + baseY + layer1Y * 12 + 12;

                    // SPR 位置：格子左上角（offset 由 SPR 檔案提供）
                    int sprWorldX = mx + baseX + layer1X * 24 + layer1Y * 24;
                    int sprWorldY = my + baseY + layer1Y * 12;

                    // 轉為螢幕座標
                    float markerX = (markerWorldX - scrollX) * zoomLevel;
                    float markerY = (markerWorldY - scrollY) * zoomLevel;
                    float sprX = (sprWorldX - scrollX) * zoomLevel;
                    float sprY = (sprWorldY - scrollY) * zoomLevel;

                    // 檢查是否在可見範圍內
                    if (markerX < -50 || markerX > viewportWidth + 50 || markerY < -50 || markerY > viewportHeight + 50)
                        continue;

                    bool isEnabled = _editState.EnabledLayer8Items.Contains((s32Data.FilePath, i));

                    // 繪製標記（圓點）- 受 ShowLayer8Marker 控制
                    float markerRadius = Math.Max(5, 10 * zoomLevel);

                    if (_viewState.ShowLayer8Marker)
                    {
                        if (isEnabled)
                        {
                            // 啟用狀態：灰色半透明 marker
                            using var fillPaint = new SKPaint
                            {
                                IsAntialias = true,
                                Style = SKPaintStyle.Fill,
                                Color = new SKColor(128, 128, 128, 25)
                            };
                            using var strokePaint = new SKPaint
                            {
                                IsAntialias = true,
                                Style = SKPaintStyle.Stroke,
                                StrokeWidth = 1,
                                Color = new SKColor(255, 255, 255, 50)
                            };
                            canvas.DrawCircle(markerX, markerY, markerRadius, fillPaint);
                            canvas.DrawCircle(markerX, markerY, markerRadius, strokePaint);
                        }
                        else
                        {
                            // 停用狀態：橙色實心 marker
                            using var fillPaint = new SKPaint
                            {
                                IsAntialias = true,
                                Style = SKPaintStyle.Fill,
                                Color = new SKColor(255, 165, 0)  // Orange
                            };
                            using var strokePaint = new SKPaint
                            {
                                IsAntialias = true,
                                Style = SKPaintStyle.Stroke,
                                StrokeWidth = 1,
                                Color = SKColors.White
                            };
                            canvas.DrawCircle(markerX, markerY, markerRadius, fillPaint);
                            canvas.DrawCircle(markerX, markerY, markerRadius, strokePaint);
                        }

                        // 顯示 SprId
                        float textSize = Math.Max(6, 8 * zoomLevel);
                        using var textPaint = new SKPaint
                        {
                            IsAntialias = true,
                            TextSize = textSize,
                            Color = SKColors.White,
                            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                        };
                        canvas.DrawText(item.SprId.ToString(), markerX + markerRadius + 2, markerY + textSize / 3, textPaint);
                    }

                    // 繪製 SPR 動畫 - 受 ShowLayer8Spr 控制
                    if (_viewState.ShowLayer8Spr && isEnabled)
                    {
                        DrawLayer8SpriteSK(canvas, item.SprId, sprX, sprY, zoomLevel, s32Data.FilePath, i);
                    }
                }
            }
        }

        // 繪製 Layer8 SPR 動畫帧（SK 版本）
        private void DrawLayer8SpriteSK(SKCanvas canvas, int sprId, float x, float y, float zoomLevel, string s32Path, int itemIndex)
        {
            // 使用 SKBitmap 快取
            if (!_renderCache.Layer8SprCacheSK.TryGetValue(sprId, out var skFrames))
            {
                // 從原始 Eto.Drawing.Image 轉換為 SKBitmap
                if (!_renderCache.Layer8SprCache.TryGetValue(sprId, out var etoFrames))
                {
                    etoFrames = LoadLayer8SprFrames(sprId);
                    _renderCache.Layer8SprCache[sprId] = etoFrames;
                }

                skFrames = new List<Layer8FrameSK>();
                if (etoFrames != null)
                {
                    foreach (var f in etoFrames)
                    {
                        if (f.Image is Bitmap bmp)
                        {
                            var skBitmap = EtoBitmapToSKBitmap(bmp);
                            if (skBitmap != null)
                            {
                                skFrames.Add(new Layer8FrameSK
                                {
                                    Image = skBitmap,
                                    XOffset = f.XOffset,
                                    YOffset = f.YOffset
                                });
                            }
                        }
                    }
                }
                _renderCache.Layer8SprCacheSK[sprId] = skFrames;
            }

            if (skFrames == null || skFrames.Count == 0) return;

            var key = (s32Path, itemIndex);
            if (!_renderCache.Layer8AnimFrame.TryGetValue(key, out int frameIdx))
            {
                frameIdx = 0;
                _renderCache.Layer8AnimFrame[key] = 0;
            }

            var frame = skFrames[frameIdx % skFrames.Count];
            float drawW = frame.Image.Width * zoomLevel;
            float drawH = frame.Image.Height * zoomLevel;

            // 使用 SPR 檔案中的 offset（縮放後）
            float drawX = x + frame.XOffset * zoomLevel;
            float drawY = y + frame.YOffset * zoomLevel;

            var destRect = new SKRect(drawX, drawY, drawX + drawW, drawY + drawH);
            canvas.DrawBitmap(frame.Image, destRect);
        }

        // Eto.Drawing.Bitmap 轉換為 SKBitmap
        private SKBitmap EtoBitmapToSKBitmap(Bitmap etoBitmap)
        {
            try
            {
                using var ms = new System.IO.MemoryStream();
                etoBitmap.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                return SKBitmap.Decode(ms);
            }
            catch
            {
                return null;
            }
        }

        // 繪製選取的格子（SK 版本）
        private void DrawSelectedCellsOnlySK(SKCanvas canvas, float zoomLevel, int scrollX, int scrollY)
        {
            if (_editState.SelectedCells.Count == 0) return;

            // 判斷顏色（區域選取模式用綠色，其他用橙色）
            bool isSelectingRegion = _viewState.ShowSafeZones || _viewState.ShowCombatZones ||
                                      currentRegionEditMode != RegionEditMode.None;
            var color = isSelectingRegion ? new SKColor(0, 255, 0) : new SKColor(255, 165, 0); // 綠/橙

            using var fillPaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = new SKColor(color.Red, color.Green, color.Blue, 80) // 半透明
            };
            using var strokePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                Color = color
            };

            foreach (var cell in _editState.SelectedCells)
            {
                // 與 DrawSelectedCells 完全相同的座標計算
                int[] loc = cell.S32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                int x = cell.LocalX;  // Layer1 座標 (0-127)
                int y = cell.LocalY;  // (0-63)

                int localBaseX = 0 - 24 * (x / 2);
                int localBaseY = 63 * 12 - 12 * (x / 2);

                // 計算世界座標
                int worldX = mx + localBaseX + x * 24 + y * 24;
                int worldY = my + localBaseY + y * 12;

                // 轉換為螢幕座標（考慮捲動位置和縮放）
                float screenX = (worldX - scrollX) * zoomLevel;
                float screenY = (worldY - scrollY) * zoomLevel;
                float scaledWidth = 48 * zoomLevel;   // Layer3 格子寬度
                float scaledHeight = 24 * zoomLevel;  // Layer3 格子高度

                // Layer3 菱形四個頂點
                using var path = new SKPath();
                path.MoveTo(screenX, screenY + scaledHeight / 2);                    // 左
                path.LineTo(screenX + scaledWidth / 2, screenY);                     // 上
                path.LineTo(screenX + scaledWidth, screenY + scaledHeight / 2);      // 右
                path.LineTo(screenX + scaledWidth / 2, screenY + scaledHeight);      // 下
                path.Close();

                canvas.DrawPath(path, fillPaint);
                canvas.DrawPath(path, strokePaint);
            }

            // 顯示選取數量（在選取區域模式下）
            if (isSelectingRegion && _editState.SelectedCells.Count > 0)
            {
                string info = $"選取 {_editState.SelectedCells.Count} 格";
                using var textPaint = new SKPaint
                {
                    IsAntialias = true,
                    TextSize = 12,
                    Color = SKColors.White,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                };
                using var bgPaint = new SKPaint
                {
                    IsAntialias = false,
                    Style = SKPaintStyle.Fill,
                    Color = new SKColor(0, 0, 0, 180)
                };

                var bounds = new SKRect();
                textPaint.MeasureText(info, ref bounds);

                // 在畫面右上角顯示
                float textX = canvas.LocalClipBounds.Right - bounds.Width - 10;
                float textY = 30;

                canvas.DrawRect(textX - 4, textY - bounds.Height - 2, bounds.Width + 8, bounds.Height + 6, bgPaint);
                canvas.DrawText(info, textX, textY, textPaint);
            }
        }

        #endregion
    }
}
