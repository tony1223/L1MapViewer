using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using L1MapViewer.Models;
using L1MapViewer.Other;
using L1MapViewer.Reader;
using Lin.Helper.Core.Tile;
using NLog;
using SkiaSharp;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// 地圖輸出器 - 將地圖渲染為透明 PNG
    /// </summary>
    public class MapExporter
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 區塊寬度（像素）
        /// </summary>
        public const int BlockWidth = 3072;  // 64 * 24 * 2

        /// <summary>
        /// 區塊高度（像素）
        /// </summary>
        public const int BlockHeight = 1536; // 64 * 12 * 2

        /// <summary>
        /// 輸出選項
        /// </summary>
        public class ExportOptions
        {
            public bool ShowLayer1 { get; set; } = true;
            public bool ShowLayer2 { get; set; } = true;
            public bool ShowLayer4 { get; set; } = true;
            public bool ShowLayer8 { get; set; } = true;
            public int Layer8FramePreference { get; set; } = 2; // 0-based, 第3帧
            public List<CustomMarker> CustomMarkers { get; set; } = new List<CustomMarker>();
            /// <summary>
            /// 標記大小等級 (1-10)，決定圓點和文字大小
            /// </summary>
            public int MarkerSizeLevel { get; set; } = 5;
            /// <summary>
            /// 縮放百分比 (相對於原始尺寸)
            /// </summary>
            public int ScalePercent { get; set; } = 100;
            /// <summary>
            /// 相對縮放百分比（相對於最大可輸出尺寸的百分比）
            /// 批次輸出時使用，會自動計算每張地圖的實際縮放比例
            /// </summary>
            public int RelativeScalePercent { get; set; } = 100;
        }

        /// <summary>
        /// 自訂標記
        /// </summary>
        public class CustomMarker
        {
            public int GameX { get; set; }
            public int GameY { get; set; }
            public string Name { get; set; } = string.Empty;

            /// <summary>
            /// 解析座標文字
            /// </summary>
            public static List<CustomMarker> Parse(string text)
            {
                var result = new List<CustomMarker>();
                if (string.IsNullOrWhiteSpace(text)) return result;

                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        if (int.TryParse(parts[0].Trim(), out int x) && int.TryParse(parts[1].Trim(), out int y))
                        {
                            result.Add(new CustomMarker
                            {
                                GameX = x,
                                GameY = y,
                                Name = parts.Length >= 3 ? parts[2].Trim() : string.Empty
                            });
                        }
                    }
                }
                return result;
            }
        }

        // SPR 帧快取
        private readonly Dictionary<int, List<SprFrame>> _sprCache = new Dictionary<int, List<SprFrame>>();

        private class SprFrame
        {
            public SKBitmap Image { get; set; }
            public int XOffset { get; set; }
            public int YOffset { get; set; }
        }

        /// <summary>
        /// 記憶體上限（500MB）
        /// </summary>
        public const long MAX_MEMORY_BYTES = 500 * 1024 * 1024;

        /// <summary>
        /// 估算輸出所需的記憶體
        /// </summary>
        /// <param name="document">地圖文件</param>
        /// <param name="scalePercent">縮放百分比</param>
        /// <returns>預估記憶體需求（bytes）及輸出尺寸</returns>
        public static (long requiredBytes, int width, int height) EstimateMemory(MapDocument document, int scalePercent)
        {
            if (document == null)
                return (0, 0, 0);

            float scale = scalePercent / 100f;
            if (scale <= 0 || scale > 1) scale = 1f;

            int width = (int)(document.MapPixelWidth * scale);
            int height = (int)(document.MapPixelHeight * scale);
            long requiredBytes = (long)width * height * 4; // RGBA 每像素 4 bytes

            return (requiredBytes, width, height);
        }

        /// <summary>
        /// 檢查是否會超過記憶體上限
        /// </summary>
        public static bool WillExceedMemoryLimit(MapDocument document, int scalePercent)
        {
            var (requiredBytes, _, _) = EstimateMemory(document, scalePercent);
            return requiredBytes > MAX_MEMORY_BYTES;
        }

        /// <summary>
        /// 計算在記憶體上限內的最大縮放比例
        /// </summary>
        public static int GetMaxScaleWithinLimit(MapDocument document)
        {
            if (document == null)
                return 100;

            long originalPixels = (long)document.MapPixelWidth * document.MapPixelHeight;
            long maxPixels = MAX_MEMORY_BYTES / 4; // 每像素 4 bytes

            if (originalPixels <= maxPixels)
                return 100;

            // 計算最大縮放比例: scale = sqrt(maxPixels / originalPixels)
            double maxScale = Math.Sqrt((double)maxPixels / originalPixels);
            int maxPercent = (int)(maxScale * 100);

            // 確保至少 1%
            return Math.Max(1, maxPercent);
        }

        /// <summary>
        /// 輸出單張地圖（平行繪製）
        /// </summary>
        public SKBitmap ExportMap(MapDocument document, ExportOptions options)
        {
            if (document == null || document.S32Files.Count == 0)
                return null;

            int originalWidth = document.MapPixelWidth;
            int originalHeight = document.MapPixelHeight;

            if (originalWidth <= 0 || originalHeight <= 0)
            {
                _logger.Warn("Invalid map dimensions: {0}x{1}", originalWidth, originalHeight);
                return null;
            }

            // 計算縮放比例（先縮放再繪製，避免記憶體不足）
            float scale = options.ScalePercent / 100f;
            if (scale <= 0 || scale > 1) scale = 1f;

            int width = (int)(originalWidth * scale);
            int height = (int)(originalHeight * scale);

            // 檢查記憶體需求（每像素 4 bytes）
            long requiredBytes = (long)width * height * 4;
            const long MAX_SAFE_BYTES = 500 * 1024 * 1024; // 500MB 上限

            if (requiredBytes > MAX_SAFE_BYTES)
            {
                _logger.Error("Map too large: {0}x{1} requires {2}MB, max is {3}MB",
                    width, height, requiredBytes / 1024 / 1024, MAX_SAFE_BYTES / 1024 / 1024);
                throw new OutOfMemoryException($"地圖太大 ({width}x{height})，請選擇較小的縮放比例");
            }

            _logger.Info("Exporting map: {0}x{1} -> {2}x{3} pixels (scale {4}%)",
                originalWidth, originalHeight, width, height, options.ScalePercent);

            // 建立縮放後尺寸的 bitmap
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            var bitmap = new SKBitmap(info);

            // 取得要處理的 S32 列表
            var s32List = document.S32Files.Values
                .Where(s => document.CheckedS32Files.Contains(s.FilePath))
                .ToList();

            if (s32List.Count == 0)
                return bitmap;

            // 建立最終 canvas
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            // 套用縮放
            if (scale < 1f)
            {
                canvas.Scale(scale, scale);
            }

            // 限制平行度以避免記憶體不足 (每個 S32 bitmap 約 18MB)
            int maxParallelism = Math.Max(1, Math.Min(Environment.ProcessorCount, 4));
            var parallelOptions = new System.Threading.Tasks.ParallelOptions
            {
                MaxDegreeOfParallelism = maxParallelism
            };

            // 用於同步 canvas 繪製
            var canvasLock = new object();

            _logger.Info("Parallel rendering {0} S32 blocks with {1} threads", s32List.Count, maxParallelism);

            System.Threading.Tasks.Parallel.ForEach(s32List, parallelOptions, s32Data =>
            {
                try
                {
                    using var s32Bitmap = RenderS32ToBitmap(s32Data, options);
                    if (s32Bitmap != null)
                    {
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        lock (canvasLock)
                        {
                            canvas.DrawBitmap(s32Bitmap, loc[0], loc[1]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to render S32: {0}", s32Data.FilePath);
                }
            });

            // 繪製 Layer8 SPR（仍在縮放的 canvas 上）
            if (options.ShowLayer8)
            {
                DrawLayer8ToCanvas(canvas, document, options.Layer8FramePreference);
            }

            // 重置縮放以繪製清晰的標記文字
            canvas.ResetMatrix();

            // 繪製自訂標記（在縮放後的座標系統上，文字保持清晰）
            if (options.CustomMarkers.Count > 0)
            {
                DrawCustomMarkers(canvas, document, options.CustomMarkers, options.MarkerSizeLevel, scale);
            }

            return bitmap;
        }

        /// <summary>
        /// 將單個 S32 區塊渲染為獨立的 bitmap（使用 RGB565 渲染後轉換為 RGBA）
        /// </summary>
        private unsafe SKBitmap RenderS32ToBitmap(S32Data s32Data, ExportOptions options)
        {
            // S32 區塊大小: 3072 x 1536 像素
            const int S32_WIDTH = 3072;
            const int S32_HEIGHT = 1536;

            // 先渲染到 RGB565 buffer（與 ViewportRenderer 相同的方式，確保 alpha 混合正確）
            byte[] rgb565Buffer = new byte[S32_WIDTH * S32_HEIGHT * 2];
            int rowPitch = S32_WIDTH * 2;

            // 收集此 S32 的所有 tile
            var tiles = new List<(int pixelX, int pixelY, int layer, int tileId, int indexId)>();

            // Layer 1 (地板)
            if (options.ShowLayer1)
            {
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        var cell = s32Data.Layer1[y, x];
                        if (cell != null && cell.TileId >= 0)
                        {
                            int halfX = x / 2;
                            int baseX = -24 * halfX;
                            int baseY = 63 * 12 - 12 * halfX;
                            int pixelX = baseX + x * 24 + y * 24;
                            int pixelY = baseY + y * 12;

                            tiles.Add((pixelX, pixelY, -2, cell.TileId, cell.IndexId));
                        }
                    }
                }
            }

            // Layer 2
            if (options.ShowLayer2)
            {
                foreach (var item in s32Data.Layer2)
                {
                    if (item.TileId >= 0)
                    {
                        int x = item.X;
                        int y = item.Y;
                        int halfX = x / 2;
                        int baseX = -24 * halfX;
                        int baseY = 63 * 12 - 12 * halfX;
                        int pixelX = baseX + x * 24 + y * 24;
                        int pixelY = baseY + y * 12;

                        tiles.Add((pixelX, pixelY, -1, item.TileId, item.IndexId));
                    }
                }
            }

            // Layer 4 (物件)
            if (options.ShowLayer4)
            {
                foreach (var obj in s32Data.Layer4)
                {
                    int halfX = obj.X / 2;
                    int baseX = -24 * halfX;
                    int baseY = 63 * 12 - 12 * halfX;
                    int pixelX = baseX + obj.X * 24 + obj.Y * 24;
                    int pixelY = baseY + obj.Y * 12;

                    tiles.Add((pixelX, pixelY, obj.Layer, obj.TileId, obj.IndexId));
                }
            }

            // 按 layer 排序，使用 RGB565 直接渲染（支援正確的 alpha 混合）
            fixed (byte* bufferPtr = rgb565Buffer)
            {
                foreach (var tile in tiles.OrderBy(t => t.layer))
                {
                    DrawTileToRgb565Buffer(bufferPtr, rowPitch, S32_WIDTH, S32_HEIGHT,
                        tile.pixelX, tile.pixelY, tile.tileId, tile.indexId);
                }
            }

            // 轉換 RGB565 為 RGBA（黑色變透明）
            return ConvertRgb565ToRgba(rgb565Buffer, S32_WIDTH, S32_HEIGHT);
        }

        /// <summary>
        /// 繪製 Tile 到 RGB565 buffer（使用 L1Til.RenderBlockDirectRgb565，確保 alpha 混合正確）
        /// </summary>
        private unsafe void DrawTileToRgb565Buffer(byte* buffer, int rowPitch, int maxWidth, int maxHeight,
            int pixelX, int pixelY, int tileId, int indexId)
        {
            try
            {
                var tilArray = TileProvider.Instance.GetTilArrayWithFallback(tileId, indexId, pixelX, out indexId);
                if (tilArray == null || indexId < 0 || indexId >= tilArray.Count) return;
                byte[] tilData = tilArray[indexId];
                if (tilData == null || tilData.Length < 2) return;

                // 使用 L1Til.RenderBlockDirectRgb565 渲染，支援完整的 alpha 混合
                L1Til.RenderBlockDirectRgb565(tilData, pixelX, pixelY, buffer, rowPitch, maxWidth, maxHeight, applyTypeAlpha: true);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to draw tile {0}:{1}", tileId, indexId);
            }
        }

        /// <summary>
        /// 將 RGB565 buffer 轉換為 RGBA SKBitmap（黑色變透明）
        /// </summary>
        private SKBitmap ConvertRgb565ToRgba(byte[] rgb565Buffer, int width, int height)
        {
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            var bitmap = new SKBitmap(info);
            var pixels = bitmap.GetPixels();

            unsafe
            {
                byte* dst = (byte*)pixels.ToPointer();
                int srcIdx = 0;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        ushort rgb565 = (ushort)(rgb565Buffer[srcIdx] | (rgb565Buffer[srcIdx + 1] << 8));
                        srcIdx += 2;

                        int dstIdx = (y * width + x) * 4;

                        if (rgb565 == 0)
                        {
                            // 黑色變透明
                            dst[dstIdx + 0] = 0;     // R
                            dst[dstIdx + 1] = 0;     // G
                            dst[dstIdx + 2] = 0;     // B
                            dst[dstIdx + 3] = 0;     // A (透明)
                        }
                        else
                        {
                            // RGB565 轉 RGB888
                            // RGB565: RRRRRGGGGGGBBBBB
                            int r5 = (rgb565 >> 11) & 0x1F;
                            int g6 = (rgb565 >> 5) & 0x3F;
                            int b5 = rgb565 & 0x1F;

                            dst[dstIdx + 0] = (byte)((r5 << 3) | (r5 >> 2)); // R
                            dst[dstIdx + 1] = (byte)((g6 << 2) | (g6 >> 4)); // G
                            dst[dstIdx + 2] = (byte)((b5 << 3) | (b5 >> 2)); // B
                            dst[dstIdx + 3] = 255;   // A (不透明)
                        }
                    }
                }
            }

            return bitmap;
        }

        /// <summary>
        /// 縮放 bitmap
        /// </summary>
        private SKBitmap ScaleBitmap(SKBitmap source, int scalePercent)
        {
            float scale = scalePercent / 100f;
            int newWidth = (int)(source.Width * scale);
            int newHeight = (int)(source.Height * scale);

            _logger.Info("Scaling bitmap from {0}x{1} to {2}x{3} ({4}%)",
                source.Width, source.Height, newWidth, newHeight, scalePercent);

            var info = new SKImageInfo(newWidth, newHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
            var scaledBitmap = new SKBitmap(info);

            using var canvas = new SKCanvas(scaledBitmap);
            canvas.Clear(SKColors.Transparent);

            // 使用高品質縮放
            using var paint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };

            var destRect = new SKRect(0, 0, newWidth, newHeight);
            canvas.DrawBitmap(source, destRect, paint);

            // 釋放原始 bitmap
            source.Dispose();

            return scaledBitmap;
        }

        /// <summary>
        /// 繪製 Layer8 SPR 到 canvas
        /// </summary>
        private void DrawLayer8ToCanvas(SKCanvas canvas, MapDocument document, int preferredFrame)
        {
            foreach (var s32Data in document.S32Files.Values)
            {
                if (!document.CheckedS32Files.Contains(s32Data.FilePath))
                    continue;

                if (s32Data.Layer8.Count == 0) continue;

                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                foreach (var item in s32Data.Layer8)
                {
                    // 計算像素位置
                    int localLayer3X = item.X - s32Data.SegInfo.nLinBeginX;
                    int localLayer3Y = item.Y - s32Data.SegInfo.nLinBeginY;

                    if (localLayer3X < 0 || localLayer3X > 63 || localLayer3Y < 0 || localLayer3Y > 63)
                        continue;

                    int layer1X = localLayer3X * 2;
                    int layer1Y = localLayer3Y;

                    int baseX = -24 * (layer1X / 2);
                    int baseY = 63 * 12 - 12 * (layer1X / 2);

                    int worldX = mx + baseX + layer1X * 24 + layer1Y * 24;
                    int worldY = my + baseY + layer1Y * 12;

                    // 繪製 SPR
                    DrawSprToCanvas(canvas, item.SprId, worldX, worldY, preferredFrame);
                }
            }
        }

        /// <summary>
        /// 繪製 SPR 到 canvas（選擇指定帧）
        /// </summary>
        private void DrawSprToCanvas(SKCanvas canvas, int sprId, int x, int y, int preferredFrame)
        {
            var frames = GetSprFrames(sprId);
            if (frames == null || frames.Count == 0) return;

            // 選擇帧：優先 preferredFrame，若無則往前找
            int frameIndex = GetPreferredFrameIndex(frames, preferredFrame);
            if (frameIndex < 0) return;

            var frame = frames[frameIndex];
            canvas.DrawBitmap(frame.Image, x + frame.XOffset, y + frame.YOffset);
        }

        /// <summary>
        /// 取得 SPR 帧（含快取）
        /// </summary>
        private List<SprFrame> GetSprFrames(int sprId)
        {
            if (_sprCache.TryGetValue(sprId, out var cached))
                return cached;

            var frames = LoadSprFrames(sprId);
            _sprCache[sprId] = frames;
            return frames;
        }

        /// <summary>
        /// 載入 SPR 帧
        /// </summary>
        private List<SprFrame> LoadSprFrames(int sprId)
        {
            try
            {
                byte[] sprData = L1PakReader.UnPackSpriteById(sprId);
                if (sprData != null && sprData.Length > 0)
                {
                    var rawFrames = Lin.Helper.Core.Sprite.SprReader.LoadRaw(sprData);
                    if (rawFrames != null && rawFrames.Length > 0)
                    {
                        var result = new List<SprFrame>();
                        foreach (var f in rawFrames)
                        {
                            if (f.Width > 0 && f.Height > 0 && f.Pixels != null)
                            {
                                // SprReader.LoadRaw 返回 RGBA 格式，直接使用
                                var info = new SKImageInfo(f.Width, f.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
                                var skBitmap = new SKBitmap(info);

                                var handle = System.Runtime.InteropServices.GCHandle.Alloc(f.Pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                                try
                                {
                                    skBitmap.InstallPixels(info, handle.AddrOfPinnedObject(), info.RowBytes);
                                    // 複製一份，因為 GCHandle 會被釋放
                                    var copy = skBitmap.Copy();
                                    result.Add(new SprFrame
                                    {
                                        Image = copy,
                                        XOffset = f.XOffset,
                                        YOffset = f.YOffset
                                    });
                                }
                                finally
                                {
                                    handle.Free();
                                }
                            }
                        }
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to load SPR {0}", sprId);
            }

            return new List<SprFrame>();
        }

        /// <summary>
        /// 取得優先帧索引
        /// </summary>
        private int GetPreferredFrameIndex(List<SprFrame> frames, int preferredIndex)
        {
            if (frames == null || frames.Count == 0) return -1;

            // 優先 preferredIndex，若無則往前找
            for (int i = preferredIndex; i >= 0; i--)
            {
                if (i < frames.Count) return i;
            }
            return 0; // fallback 到第一帧
        }

        /// <summary>
        /// 繪製自訂標記
        /// </summary>
        /// <param name="sizeLevel">大小等級 1-10，決定圓點和文字大小</param>
        /// <param name="scale">縮放比例（用於計算縮放後的座標位置）</param>
        private void DrawCustomMarkers(SKCanvas canvas, MapDocument document, List<CustomMarker> markers, int sizeLevel, float scale = 1f)
        {
            // 根據等級計算大小 (等級 1-10 對應不同尺寸)
            // 等級 1: 最小 (適合大地圖縮圖), 等級 10: 最大 (適合小區域放大圖)
            // 標記大小不受縮放影響，保持清晰
            float circleRadius = 3 + sizeLevel * 2;      // 5 ~ 23
            float textSize = 8 + sizeLevel * 2;          // 10 ~ 28
            float strokeWidth = 1 + sizeLevel * 0.3f;    // 1.3 ~ 4

            using var fillPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = SKColors.Yellow
            };
            using var strokePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeWidth,
                Color = SKColors.Black
            };
            // 使用支援中日韓文字的字體
            var typeface = SKTypeface.FromFamilyName("Microsoft JhengHei", SKFontStyle.Bold)
                ?? SKTypeface.FromFamilyName("Microsoft YaHei", SKFontStyle.Bold)
                ?? SKTypeface.FromFamilyName("Meiryo", SKFontStyle.Bold)
                ?? SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);

            using var textPaint = new SKPaint
            {
                IsAntialias = true,
                TextSize = textSize,
                Color = SKColors.White,
                Typeface = typeface
            };
            using var textShadowPaint = new SKPaint
            {
                IsAntialias = true,
                TextSize = textSize,
                Color = SKColors.Black,
                Typeface = typeface
            };

            foreach (var marker in markers)
            {
                // 找到包含該座標的 S32
                var s32Data = FindS32ByGameCoord(document, marker.GameX, marker.GameY);
                if (s32Data == null) continue;

                // 計算世界像素座標
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                int localLayer3X = marker.GameX - s32Data.SegInfo.nLinBeginX;
                int localLayer3Y = marker.GameY - s32Data.SegInfo.nLinBeginY;

                int layer1X = localLayer3X * 2;
                int layer1Y = localLayer3Y;

                int baseX = -24 * (layer1X / 2);
                int baseY = 63 * 12 - 12 * (layer1X / 2);

                // 格子中心（原始世界座標）
                int worldX = mx + baseX + layer1X * 24 + layer1Y * 24 + 12;
                int worldY = my + baseY + layer1Y * 12 + 12;

                // 套用縮放比例（因為 canvas 已重置縮放，需要手動計算縮放後的座標）
                float scaledX = worldX * scale;
                float scaledY = worldY * scale;

                // 繪製黃色圓點（在縮放後的位置，但保持原始大小）
                canvas.DrawCircle(scaledX, scaledY, circleRadius, fillPaint);
                canvas.DrawCircle(scaledX, scaledY, circleRadius, strokePaint);

                // 繪製名稱（帶陰影，文字大小不受縮放影響，保持清晰）
                if (!string.IsNullOrEmpty(marker.Name))
                {
                    float textOffset = circleRadius + 4;
                    float textX = scaledX + textOffset;
                    float textY = scaledY + textSize / 3;
                    // 陰影偏移也根據大小調整
                    float shadowOffset = Math.Max(1, sizeLevel / 3f);
                    canvas.DrawText(marker.Name, textX + shadowOffset, textY + shadowOffset, textShadowPaint);
                    canvas.DrawText(marker.Name, textX, textY, textPaint);
                }
            }
        }

        /// <summary>
        /// 根據遊戲座標找到對應的 S32
        /// </summary>
        private S32Data FindS32ByGameCoord(MapDocument document, int gameX, int gameY)
        {
            foreach (var s32Data in document.S32Files.Values)
            {
                if (gameX >= s32Data.SegInfo.nLinBeginX && gameX < s32Data.SegInfo.nLinBeginX + 64 &&
                    gameY >= s32Data.SegInfo.nLinBeginY && gameY < s32Data.SegInfo.nLinBeginY + 64)
                {
                    return s32Data;
                }
            }
            return null;
        }

        /// <summary>
        /// 儲存為 PNG（使用最高壓縮等級，無損）
        /// </summary>
        public void SaveToPng(SKBitmap bitmap, string filePath)
        {
            // 使用 SKPixmap 和 SKPngEncoderOptions 以獲得更好的壓縮
            using var pixmap = bitmap.PeekPixels();

            // AllFilters (248) 讓編碼器嘗試所有過濾器並選擇最佳
            // ZLibLevel 9 是最高壓縮等級（範圍 0-9）
            var options = new SKPngEncoderOptions(
                SKPngEncoderFilterFlags.AllFilters,
                9  // zlib compression level: 9 = maximum compression
            );

            using var data = pixmap.Encode(options);
            if (data != null)
            {
                using var stream = File.OpenWrite(filePath);
                data.SaveTo(stream);
            }
            else
            {
                // fallback 到原本的方法
                _logger.Warn("Failed to encode with high compression, falling back to default");
                using var image = SKImage.FromBitmap(bitmap);
                using var fallbackData = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(filePath);
                fallbackData.SaveTo(stream);
            }
        }

        /// <summary>
        /// 批次輸出所有地圖
        /// </summary>
        public async Task BatchExportAsync(
            string outputFolder,
            ExportOptions options,
            IProgress<(int current, int total, string mapId)> progress,
            CancellationToken cancellationToken = default)
        {
            if (Share.MapDataList == null || Share.MapDataList.Count == 0)
                return;

            Directory.CreateDirectory(outputFolder);

            var mapIds = Share.MapDataList.Keys.ToList();
            int total = mapIds.Count;
            int current = 0;

            foreach (var mapId in mapIds)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                current++;
                progress?.Report((current, total, mapId));

                try
                {
                    // 載入地圖
                    var document = new MapDocument();
                    await Task.Run(() => document.Load(mapId), cancellationToken);

                    if (document.S32Files.Count == 0)
                        continue;

                    // 計算此地圖的實際縮放比例
                    // 縮放比例 = 最大可輸出比例 × 使用者選擇的相對比例
                    int maxScale = GetMaxScaleWithinLimit(document);
                    int actualScale = maxScale * options.RelativeScalePercent / 100;
                    if (actualScale < 1) actualScale = 1;

                    // 批次輸出不含標記
                    var batchOptions = new ExportOptions
                    {
                        ShowLayer1 = options.ShowLayer1,
                        ShowLayer2 = options.ShowLayer2,
                        ShowLayer4 = options.ShowLayer4,
                        ShowLayer8 = options.ShowLayer8,
                        Layer8FramePreference = options.Layer8FramePreference,
                        ScalePercent = actualScale,
                        CustomMarkers = new List<CustomMarker>() // 批次不含標記
                    };

                    _logger.Info("Map {0}: maxScale={1}%, relativeScale={2}%, actualScale={3}%",
                        mapId, maxScale, options.RelativeScalePercent, actualScale);

                    using var bitmap = ExportMap(document, batchOptions);
                    if (bitmap != null)
                    {
                        string fileName = $"{mapId}.png";
                        string filePath = Path.Combine(outputFolder, fileName);
                        SaveToPng(bitmap, filePath);
                        _logger.Info("Exported: {0}", filePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to export map {0}", mapId);
                }
            }
        }

        /// <summary>
        /// 清除快取
        /// </summary>
        public void ClearCache()
        {
            foreach (var frames in _sprCache.Values)
            {
                foreach (var frame in frames)
                {
                    frame.Image?.Dispose();
                }
            }
            _sprCache.Clear();
        }
    }
}
