using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
// using System.Drawing; // Replaced with Eto.Drawing
// using System.Drawing.Imaging; // Replaced with SkiaSharp
using System.Linq;
using SkiaSharp;
using NLog;
using L1FlyMapViewer;
using L1MapViewer.Converter;
using L1MapViewer.Models;
using L1MapViewer.Other;
using L1MapViewer.Reader;
using L1MapViewer.Compatibility;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// MiniMap 渲染器 - CLI 和 Form 共用的渲染邏輯
    /// </summary>
    public class MiniMapRenderer
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// RGB555 轉換為 RGB565
        /// RGB555: 0RRRRRGGGGGBBBBB (bit15 unused, 5R, 5G, 5B)
        /// RGB565: RRRRRGGGGGGBBBBB (5R, 6G, 5B)
        /// </summary>
        /// <param name="rgb555">RGB555 格式的顏色值</param>
        /// <returns>RGB565 格式的顏色值</returns>
        public static ushort Rgb555ToRgb565(ushort rgb555)
        {
            // 從 RGB555 解析各通道 (5-5-5)
            int r = (rgb555 >> 10) & 0x1F;  // 5 bits red
            int g = (rgb555 >> 5) & 0x1F;   // 5 bits green
            int b = rgb555 & 0x1F;          // 5 bits blue

            // 轉換為 RGB565 (5-6-5)
            // Green 從 5 bits 擴展到 6 bits: 將 5-bit 值左移 1 bit，並複製最高位到最低位
            int g6 = (g << 1) | (g >> 4);

            // 組合成 RGB565: RRRRRGGGGGGBBBBB
            return (ushort)((r << 11) | (g6 << 5) | b);
        }

        /// <summary>
        /// 渲染統計資訊
        /// </summary>
        public class RenderStats
        {
            public long TotalMs;
            public long GetBlockMs;
            public long DrawImageMs;
            public int BlockCount;
            public int ScaledWidth;
            public int ScaledHeight;
            public float Scale;
            public bool IsSimplified;
        }

        /// <summary>
        /// 小地圖座標邊界資訊 - 用於 world ↔ minimap 座標轉換
        /// </summary>
        public class MiniMapBounds
        {
            /// <summary>世界座標最小 X</summary>
            public int WorldMinX;
            /// <summary>世界座標最小 Y</summary>
            public int WorldMinY;
            /// <summary>世界座標內容寬度</summary>
            public int ContentWidth;
            /// <summary>世界座標內容高度</summary>
            public int ContentHeight;
            /// <summary>小地圖 bitmap 寬度</summary>
            public int BitmapWidth;
            /// <summary>小地圖 bitmap 高度</summary>
            public int BitmapHeight;

            /// <summary>
            /// 世界座標 → 小地圖 bitmap 座標
            /// </summary>
            public (float x, float y) WorldToMiniMap(int worldX, int worldY)
            {
                if (ContentWidth <= 0 || ContentHeight <= 0) return (0, 0);
                float x = (float)(worldX - WorldMinX) / ContentWidth * BitmapWidth;
                float y = (float)(worldY - WorldMinY) / ContentHeight * BitmapHeight;
                return (x, y);
            }

            /// <summary>
            /// 小地圖 bitmap 座標 → 世界座標
            /// </summary>
            public (int x, int y) MiniMapToWorld(float miniMapX, float miniMapY)
            {
                if (BitmapWidth <= 0 || BitmapHeight <= 0) return (0, 0);
                int x = (int)(miniMapX / BitmapWidth * ContentWidth) + WorldMinX;
                int y = (int)(miniMapY / BitmapHeight * ContentHeight) + WorldMinY;
                return (x, y);
            }
        }

        // S32 區塊快取
        private ConcurrentDictionary<string, Bitmap> _s32BlockCache = new ConcurrentDictionary<string, Bitmap>();

        // 取樣版 S32 區塊快取
        private ConcurrentDictionary<string, Bitmap> _s32BlockCacheSampled = new ConcurrentDictionary<string, Bitmap>();

        // 常數
        public const int BlockWidth = 64 * 24 * 2;  // 3072
        public const int BlockHeight = 64 * 12 * 2; // 1536

        /// <summary>
        /// 小地圖邊界 padding（世界座標像素）
        /// </summary>
        public int Padding { get; set; } = 200;

        /// <summary>
        /// 渲染 MiniMap
        /// </summary>
        public Bitmap RenderMiniMap(
            int mapWidth,
            int mapHeight,
            int targetSize,
            Dictionary<string, S32Data> s32Files,
            HashSet<string> checkedFiles,
            out RenderStats stats,
            out MiniMapBounds bounds)
        {
            stats = new RenderStats();
            bounds = new MiniMapBounds();
            var totalSw = Stopwatch.StartNew();

            // 先計算所有區塊的世界座標邊界（使用所有 S32，不只是勾選的）
            int worldMinX = int.MaxValue, worldMinY = int.MaxValue;
            int worldMaxX = int.MinValue, worldMaxY = int.MinValue;
            var sortedFilePaths = Utils.SortDesc(s32Files.Keys);

            foreach (object filePathObj in sortedFilePaths)
            {
                string filePath = filePathObj as string;
                if (filePath == null || !s32Files.ContainsKey(filePath)) continue;
                // 邊界計算包含所有 S32（不檢查 checkedFiles）

                var s32Data = s32Files[filePath];
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int blockX = loc[0];
                int blockY = loc[1];

                worldMinX = Math.Min(worldMinX, blockX);
                worldMinY = Math.Min(worldMinY, blockY);
                worldMaxX = Math.Max(worldMaxX, blockX + BlockWidth);
                worldMaxY = Math.Max(worldMaxY, blockY + BlockHeight);
            }

            // 如果沒有有效區塊，使用預設值
            if (worldMinX == int.MaxValue)
            {
                worldMinX = worldMinY = 0;
                worldMaxX = mapWidth;
                worldMaxY = mapHeight;
            }

            // 加入 padding（四周邊界）
            worldMinX -= Padding;
            worldMinY -= Padding;
            worldMaxX += Padding;
            worldMaxY += Padding;

            int contentWidth = worldMaxX - worldMinX;
            int contentHeight = worldMaxY - worldMinY;

            // 計算縮放比例（基於實際內容大小）
            float scale = Math.Min((float)targetSize / contentWidth, (float)targetSize / contentHeight);
            int scaledWidth = (int)(contentWidth * scale);
            int scaledHeight = (int)(contentHeight * scale);

            // 確保至少有 1 像素
            if (scaledWidth < 1) scaledWidth = 1;
            if (scaledHeight < 1) scaledHeight = 1;

            stats.Scale = scale;
            stats.ScaledWidth = scaledWidth;
            stats.ScaledHeight = scaledHeight;

            // 設置邊界資訊
            bounds.WorldMinX = worldMinX;
            bounds.WorldMinY = worldMinY;
            bounds.ContentWidth = contentWidth;
            bounds.ContentHeight = contentHeight;
            bounds.BitmapWidth = scaledWidth;
            bounds.BitmapHeight = scaledHeight;

            // 決定渲染模式：超過 100 個 S32 時使用簡化渲染（取樣）
            // 100 個以下使用完整渲染（全域 Layer 排序）
            int s32Count = checkedFiles.Count;
            bool useSimplifiedRendering = s32Count > 100;
            stats.IsSimplified = useSimplifiedRendering;

            // 建立小地圖 Bitmap
            Bitmap miniBitmap = new Bitmap(scaledWidth, scaledHeight, PixelFormat.Format16bppRgb555);

            // 透明色設定
            ImageAttributes vAttr = new ImageAttributes();
            vAttr.SetColorKey(Color.FromArgb(0), Color.FromArgb(0));

            long totalGetBlockMs = 0;
            long totalDrawImageMs = 0;
            int blockCount = 0;

            using (Graphics g = GraphicsHelper.FromImage(miniBitmap))
            {
                // 使用最快的縮放模式
                g.SetInterpolationMode(InterpolationMode.NearestNeighbor);
                g.SetPixelOffsetMode(PixelOffsetMode.HighSpeed);
                g.SetCompositingQuality(CompositingQuality.HighSpeed);
                g.Clear(Colors.Black);

                if (useSimplifiedRendering)
                {
                    // 直接渲染到 mini map（不經過 full-size bitmap）
                    var getBlockSw = Stopwatch.StartNew();

                    // 收集所有需要渲染的區塊資訊（使用相對於 worldMin 的座標）
                    var blocksToRender = new List<(S32Data s32Data, int blockX, int blockY)>();
                    foreach (object filePathObj in sortedFilePaths)
                    {
                        string filePath = filePathObj as string;
                        if (filePath == null || !s32Files.ContainsKey(filePath)) continue;
                        if (!checkedFiles.Contains(filePath)) continue;

                        var s32Data = s32Files[filePath];
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        // 使用相對於 worldMin 的座標
                        blocksToRender.Add((s32Data, loc[0] - worldMinX, loc[1] - worldMinY));
                    }
                    blockCount = blocksToRender.Count;

                    // 平行處理：每個區塊計算自己貢獻的像素
                    var pixelData = new ConcurrentDictionary<(int x, int y), ushort>();

                    System.Threading.Tasks.Parallel.ForEach(blocksToRender, block =>
                    {
                        RenderBlockToMiniMapDirect(block.s32Data, block.blockX, block.blockY, scale, scaledWidth, scaledHeight, pixelData);
                    });
                    getBlockSw.Stop();
                    totalGetBlockMs = getBlockSw.ElapsedMilliseconds;

                    // 寫入 bitmap
                    var drawSw = Stopwatch.StartNew();
                    Rectangle rect = new Rectangle(0, 0, scaledWidth, scaledHeight);
                    BitmapData bmpData = miniBitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb555);
                    unsafe
                    {
                        byte* ptr = (byte*)bmpData.Scan0;
                        int stride = bmpData.Stride;
                        foreach (var kvp in pixelData)
                        {
                            int x = kvp.Key.x;
                            int y = kvp.Key.y;
                            if (x >= 0 && x < scaledWidth && y >= 0 && y < scaledHeight)
                            {
                                ushort color = kvp.Value;
                                int offset = y * stride + x * 2;
                                *(ptr + offset) = (byte)(color & 0xFF);
                                *(ptr + offset + 1) = (byte)((color >> 8) & 0xFF);
                            }
                        }
                    }
                    miniBitmap.UnlockBits(bmpData);
                    drawSw.Stop();
                    totalDrawImageMs = drawSw.ElapsedMilliseconds;
                }
                else
                {
                    // 完整渲染（全域 Layer 排序）- 先渲染到完整大小，再縮小
                    var getBlockSw = Stopwatch.StartNew();

                    // 收集所有需要渲染的 S32 區塊（使用已計算的邊界）
                    var blocksToRender = new List<(S32Data s32Data, int offsetX, int offsetY)>();

                    foreach (object filePathObj in sortedFilePaths)
                    {
                        string filePath = filePathObj as string;
                        if (filePath == null || !s32Files.ContainsKey(filePath)) continue;
                        if (!checkedFiles.Contains(filePath)) continue;

                        var s32Data = s32Files[filePath];
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int blockX = loc[0];
                        int blockY = loc[1];

                        // 使用相對於 worldMin 的座標
                        blocksToRender.Add((s32Data, blockX - worldMinX, blockY - worldMinY));
                    }
                    blockCount = blocksToRender.Count;

                    // 收集所有 Layer 物件
                    var allTiles = new List<(int pixelX, int pixelY, int layer, int tileId, int indexId)>();

                    foreach (var (s32Data, offsetX, offsetY) in blocksToRender)
                    {

                        // Layer 1 (地板)
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
                                    int pixelX = offsetX + baseX + x * 24 + y * 24;
                                    int pixelY = offsetY + baseY + y * 12;

                                    allTiles.Add((pixelX, pixelY, -2, cell.TileId, cell.IndexId));
                                }
                            }
                        }

                        // Layer 2
                        foreach (var item in s32Data.Layer2)
                        {
                            if (item.TileId >= 0)
                            {
                                int x = item.X;
                                int y = item.Y;
                                int halfX = x / 2;
                                int baseX = -24 * halfX;
                                int baseY = 63 * 12 - 12 * halfX;
                                int pixelX = offsetX + baseX + x * 24 + y * 24;
                                int pixelY = offsetY + baseY + y * 12;

                                allTiles.Add((pixelX, pixelY, -1, item.TileId, item.IndexId));
                            }
                        }

                        // Layer 4 (物件)
                        foreach (var obj in s32Data.Layer4)
                        {
                            int halfX = obj.X / 2;
                            int baseX = -24 * halfX;
                            int baseY = 63 * 12 - 12 * halfX;
                            int pixelX = offsetX + baseX + obj.X * 24 + obj.Y * 24;
                            int pixelY = offsetY + baseY + obj.Y * 12;

                            allTiles.Add((pixelX, pixelY, obj.Layer, obj.TileId, obj.IndexId));
                        }
                    }

                    // 按 Layer 全域排序
                    var sortedTiles = allTiles.OrderBy(t => t.layer).ToList();
                    getBlockSw.Stop();
                    totalGetBlockMs = getBlockSw.ElapsedMilliseconds;

                    // 渲染到完整大小的 bitmap，再用高品質插值縮小
                    var drawSw = Stopwatch.StartNew();
                    using (var fullBitmap = new Bitmap(contentWidth, contentHeight, PixelFormat.Format16bppRgb555))
                    {
                        Rectangle rect = new Rectangle(0, 0, contentWidth, contentHeight);
                        BitmapData bmpData = fullBitmap.LockBits(rect, ImageLockMode.ReadWrite, fullBitmap.PixelFormat);
                        int rowpix = bmpData.Stride;

                        unsafe
                        {
                            byte* ptr = (byte*)bmpData.Scan0;

                            foreach (var tile in sortedTiles)
                            {
                                DrawTilToBufferDirect(tile.pixelX, tile.pixelY, tile.tileId, tile.indexId,
                                    rowpix, ptr, contentWidth, contentHeight);
                            }
                        }

                        fullBitmap.UnlockBits(bmpData);

                        // 用高品質插值縮小到 minimap 大小（保留更多細節）
                        g.SetInterpolationMode(InterpolationMode.HighQualityBicubic);
                        g.DrawImage(fullBitmap, 0, 0, scaledWidth, scaledHeight);
                    }
                    drawSw.Stop();
                    totalDrawImageMs = drawSw.ElapsedMilliseconds;
                }

                // 繪製未勾選區塊的虛線邊框
                DrawUncheckedBlockBorders(g, s32Files, checkedFiles, sortedFilePaths,
                    worldMinX, worldMinY, scale, scaledWidth, scaledHeight);
            }

            totalSw.Stop();
            stats.TotalMs = totalSw.ElapsedMilliseconds;
            stats.GetBlockMs = totalGetBlockMs;
            stats.DrawImageMs = totalDrawImageMs;
            stats.BlockCount = blockCount;

            return miniBitmap;
        }

        /// <summary>
        /// 渲染 MiniMap（SkiaSharp 版本）- 直接輸出 SKBitmap，無需格式轉換
        /// </summary>
        public SKBitmap RenderMiniMapSK(
            int mapWidth,
            int mapHeight,
            int targetSize,
            Dictionary<string, S32Data> s32Files,
            HashSet<string> checkedFiles,
            out RenderStats stats,
            out MiniMapBounds bounds)
        {
            stats = new RenderStats();
            bounds = new MiniMapBounds();
            var totalSw = Stopwatch.StartNew();

            // 先計算所有區塊的世界座標邊界（使用所有 S32，不只是勾選的）
            int worldMinX = int.MaxValue, worldMinY = int.MaxValue;
            int worldMaxX = int.MinValue, worldMaxY = int.MinValue;
            var sortedFilePaths = Utils.SortDesc(s32Files.Keys);

            foreach (object filePathObj in sortedFilePaths)
            {
                string filePath = filePathObj as string;
                if (filePath == null || !s32Files.ContainsKey(filePath)) continue;

                var s32Data = s32Files[filePath];
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int blockX = loc[0];
                int blockY = loc[1];

                worldMinX = Math.Min(worldMinX, blockX);
                worldMinY = Math.Min(worldMinY, blockY);
                worldMaxX = Math.Max(worldMaxX, blockX + BlockWidth);
                worldMaxY = Math.Max(worldMaxY, blockY + BlockHeight);
            }

            // 如果沒有有效區塊，使用預設值
            if (worldMinX == int.MaxValue)
            {
                worldMinX = worldMinY = 0;
                worldMaxX = mapWidth;
                worldMaxY = mapHeight;
            }

            // 加入 padding
            worldMinX -= Padding;
            worldMinY -= Padding;
            worldMaxX += Padding;
            worldMaxY += Padding;

            int contentWidth = worldMaxX - worldMinX;
            int contentHeight = worldMaxY - worldMinY;

            // 計算縮放比例
            float scale = Math.Min((float)targetSize / contentWidth, (float)targetSize / contentHeight);
            int scaledWidth = (int)(contentWidth * scale);
            int scaledHeight = (int)(contentHeight * scale);

            if (scaledWidth < 1) scaledWidth = 1;
            if (scaledHeight < 1) scaledHeight = 1;

            stats.Scale = scale;
            stats.ScaledWidth = scaledWidth;
            stats.ScaledHeight = scaledHeight;

            bounds.WorldMinX = worldMinX;
            bounds.WorldMinY = worldMinY;
            bounds.ContentWidth = contentWidth;
            bounds.ContentHeight = contentHeight;
            bounds.BitmapWidth = scaledWidth;
            bounds.BitmapHeight = scaledHeight;

            // 決定渲染模式
            int s32Count = checkedFiles.Count;
            bool useSimplifiedRendering = s32Count > 100;
            stats.IsSimplified = useSimplifiedRendering;

            // 建立 SKBitmap（使用 RGB565 格式與 Tile 資料相同）
            var skBitmap = new SKBitmap(scaledWidth, scaledHeight, SKColorType.Rgb565, SKAlphaType.Opaque);
            _logger.Debug($"[MiniMapSK] Creating SKBitmap: {scaledWidth}x{scaledHeight}, scale={scale:F4}");

            long totalGetBlockMs = 0;
            long totalDrawImageMs = 0;
            int blockCount = 0;

            if (useSimplifiedRendering)
            {
                // 簡化渲染：直接取樣到 mini map
                var getBlockSw = Stopwatch.StartNew();
                var blocksToRender = new List<(S32Data s32Data, int blockX, int blockY)>();
                foreach (object filePathObj in sortedFilePaths)
                {
                    string filePath = filePathObj as string;
                    if (filePath == null || !s32Files.ContainsKey(filePath)) continue;
                    if (!checkedFiles.Contains(filePath)) continue;

                    var s32Data = s32Files[filePath];
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    blocksToRender.Add((s32Data, loc[0] - worldMinX, loc[1] - worldMinY));
                }
                blockCount = blocksToRender.Count;

                var pixelData = new ConcurrentDictionary<(int x, int y), ushort>();
                System.Threading.Tasks.Parallel.ForEach(blocksToRender, block =>
                {
                    RenderBlockToMiniMapDirect(block.s32Data, block.blockX, block.blockY, scale, scaledWidth, scaledHeight, pixelData);
                });
                getBlockSw.Stop();
                totalGetBlockMs = getBlockSw.ElapsedMilliseconds;

                // 寫入 SKBitmap（RGB555 → RGB565 轉換）
                var drawSw = Stopwatch.StartNew();
                unsafe
                {
                    byte* ptr = (byte*)skBitmap.GetPixels().ToPointer();
                    int rowBytes = skBitmap.RowBytes;
                    foreach (var kvp in pixelData)
                    {
                        int x = kvp.Key.x;
                        int y = kvp.Key.y;
                        if (x >= 0 && x < scaledWidth && y >= 0 && y < scaledHeight)
                        {
                            // pixelData 存的是 RGB555，需轉換為 RGB565
                            ushort rgb565 = Rgb555ToRgb565(kvp.Value);
                            int offset = y * rowBytes + x * 2;
                            *(ptr + offset) = (byte)(rgb565 & 0xFF);
                            *(ptr + offset + 1) = (byte)((rgb565 >> 8) & 0xFF);
                        }
                    }
                }
                drawSw.Stop();
                totalDrawImageMs = drawSw.ElapsedMilliseconds;
            }
            else
            {
                // 完整渲染：全域 Layer 排序
                var getBlockSw = Stopwatch.StartNew();
                var blocksToRender = new List<(S32Data s32Data, int offsetX, int offsetY)>();

                foreach (object filePathObj in sortedFilePaths)
                {
                    string filePath = filePathObj as string;
                    if (filePath == null || !s32Files.ContainsKey(filePath)) continue;
                    if (!checkedFiles.Contains(filePath)) continue;

                    var s32Data = s32Files[filePath];
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    blocksToRender.Add((s32Data, loc[0] - worldMinX, loc[1] - worldMinY));
                }
                blockCount = blocksToRender.Count;

                // 收集所有 Layer 物件
                var allTiles = new List<(int pixelX, int pixelY, int layer, int tileId, int indexId)>();
                foreach (var (s32Data, offsetX, offsetY) in blocksToRender)
                {
                    // Layer 1
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
                                int pixelX = offsetX + baseX + x * 24 + y * 24;
                                int pixelY = offsetY + baseY + y * 12;
                                allTiles.Add((pixelX, pixelY, -2, cell.TileId, cell.IndexId));
                            }
                        }
                    }

                    // Layer 2
                    foreach (var item in s32Data.Layer2)
                    {
                        if (item.TileId >= 0)
                        {
                            int x = item.X;
                            int y = item.Y;
                            int halfX = x / 2;
                            int baseX = -24 * halfX;
                            int baseY = 63 * 12 - 12 * halfX;
                            int pixelX = offsetX + baseX + x * 24 + y * 24;
                            int pixelY = offsetY + baseY + y * 12;
                            allTiles.Add((pixelX, pixelY, -1, item.TileId, item.IndexId));
                        }
                    }

                    // Layer 4
                    foreach (var obj in s32Data.Layer4)
                    {
                        int halfX = obj.X / 2;
                        int baseX = -24 * halfX;
                        int baseY = 63 * 12 - 12 * halfX;
                        int pixelX = offsetX + baseX + obj.X * 24 + obj.Y * 24;
                        int pixelY = offsetY + baseY + obj.Y * 12;
                        allTiles.Add((pixelX, pixelY, obj.Layer, obj.TileId, obj.IndexId));
                    }
                }

                var sortedTiles = allTiles.OrderBy(t => t.layer).ToList();
                getBlockSw.Stop();
                totalGetBlockMs = getBlockSw.ElapsedMilliseconds;

                // 渲染到完整大小的 SKBitmap，再縮小
                var drawSw = Stopwatch.StartNew();
                using (var fullBitmap = new SKBitmap(contentWidth, contentHeight, SKColorType.Rgb565, SKAlphaType.Opaque))
                {
                    int rowBytes = fullBitmap.RowBytes;
                    unsafe
                    {
                        byte* ptr = (byte*)fullBitmap.GetPixels().ToPointer();
                        foreach (var tile in sortedTiles)
                        {
                            DrawTilToBufferDirectSK(tile.pixelX, tile.pixelY, tile.tileId, tile.indexId,
                                rowBytes, ptr, contentWidth, contentHeight);
                        }
                    }

                    // 縮小到 minimap 大小
                    using (var canvas = new SKCanvas(skBitmap))
                    {
                        canvas.Clear(SKColors.Black);
                        var destRect = new SKRect(0, 0, scaledWidth, scaledHeight);
                        using (var paint = new SKPaint { FilterQuality = SKFilterQuality.High })
                        {
                            canvas.DrawBitmap(fullBitmap, destRect, paint);
                        }
                    }
                }
                drawSw.Stop();
                totalDrawImageMs = drawSw.ElapsedMilliseconds;
            }

            // 繪製未勾選區塊的虛線邊框
            using (var canvas = new SKCanvas(skBitmap))
            {
                DrawUncheckedBlockBordersSK(canvas, s32Files, checkedFiles, sortedFilePaths,
                    worldMinX, worldMinY, scale, scaledWidth, scaledHeight);
            }

            totalSw.Stop();
            stats.TotalMs = totalSw.ElapsedMilliseconds;
            stats.GetBlockMs = totalGetBlockMs;
            stats.DrawImageMs = totalDrawImageMs;
            stats.BlockCount = blockCount;

            _logger.Debug($"[MiniMapSK] Render completed: {stats.TotalMs}ms total, {stats.BlockCount} blocks, simplified={stats.IsSimplified}");
            return skBitmap;
        }

        /// <summary>
        /// 繪製 Tile 到緩衝區（SKBitmap RGB565 版本）
        /// 使用 Lin.Helper.Core.L1Til.RenderBlockDirectRgb565 處理：
        /// 1. RGB555 → RGB565 轉換
        /// 2. type bit 1,2,4,5 特效（煙霧、血跡等）
        /// </summary>
        private unsafe void DrawTilToBufferDirectSK(int pixelX, int pixelY, int tileId, int indexId, int rowBytes, byte* ptr, int maxWidth, int maxHeight)
        {
            try
            {
                // 使用 TileProvider 取得 til 資料
                var tilArray = TileProvider.Instance.GetTilArrayWithFallback(tileId, indexId, pixelX, out indexId);
                if (tilArray == null || indexId < 0 || indexId >= tilArray.Count) return;
                byte[] tilData = tilArray[indexId];
                if (tilData == null) return;

                // 使用 Lin.Helper.Core.L1Til 渲染 RGB565，支援 type 6/7 半透明效果
                Lin.Helper.Core.Tile.L1Til.RenderBlockDirectRgb565(tilData, pixelX, pixelY, ptr, rowBytes, maxWidth, maxHeight, applyTypeAlpha: true);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"[MiniMapSK] DrawTilToBufferDirectSK failed: tileId={tileId}, indexId={indexId}");
            }
        }

        /// <summary>
        /// 繪製未勾選區塊的虛線邊框（SkiaSharp 版本）
        /// </summary>
        private void DrawUncheckedBlockBordersSK(
            SKCanvas canvas,
            Dictionary<string, S32Data> s32Files,
            HashSet<string> checkedFiles,
            System.Collections.ICollection sortedFilePaths,
            int worldMinX,
            int worldMinY,
            float scale,
            int scaledWidth,
            int scaledHeight)
        {
            using (var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                Color = new SKColor(128, 128, 128, 180),
                PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0)
            })
            {
                foreach (object filePathObj in sortedFilePaths)
                {
                    string filePath = filePathObj as string;
                    if (filePath == null || !s32Files.ContainsKey(filePath)) continue;
                    if (checkedFiles.Contains(filePath)) continue;

                    var s32Data = s32Files[filePath];
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    int blockX = loc[0] - worldMinX;
                    int blockY = loc[1] - worldMinY;

                    float destX = blockX * scale;
                    float destY = blockY * scale;
                    float destW = BlockWidth * scale;
                    float destH = BlockHeight * scale;

                    if (destX + destW < 0 || destY + destH < 0 || destX > scaledWidth || destY > scaledHeight)
                        continue;

                    // 繪製菱形邊框
                    float centerX = destX + destW / 2;
                    float centerY = destY + destH / 2;

                    using (var path = new SKPath())
                    {
                        path.MoveTo(centerX, destY);           // 上
                        path.LineTo(destX + destW, centerY);   // 右
                        path.LineTo(centerX, destY + destH);   // 下
                        path.LineTo(destX, centerY);           // 左
                        path.Close();
                        canvas.DrawPath(path, paint);
                    }
                }
            }
        }

        /// <summary>
        /// 繪製未勾選區塊的虛線邊框（菱形，比照 S32 區塊形狀）
        /// </summary>
        private void DrawUncheckedBlockBorders(
            Graphics g,
            Dictionary<string, S32Data> s32Files,
            HashSet<string> checkedFiles,
            System.Collections.ICollection sortedFilePaths,
            int worldMinX,
            int worldMinY,
            float scale,
            int scaledWidth,
            int scaledHeight)
        {
            using (var pen = new Pen(Color.FromArgb(180, 128, 128, 128), 1f))
            {
                pen.SetDashStyle(DashStyle.Dash);
                pen.SetDashPattern(new float[] { 4, 4 });

                foreach (object filePathObj in sortedFilePaths)
                {
                    string filePath = filePathObj as string;
                    if (filePath == null || !s32Files.ContainsKey(filePath)) continue;

                    // 只繪製未勾選的區塊
                    if (checkedFiles.Contains(filePath)) continue;

                    var s32Data = s32Files[filePath];
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    int blockX = loc[0] - worldMinX;
                    int blockY = loc[1] - worldMinY;

                    // 計算縮放後的位置和大小
                    float destX = blockX * scale;
                    float destY = blockY * scale;
                    float destW = BlockWidth * scale;
                    float destH = BlockHeight * scale;

                    // 確保在可見範圍內
                    if (destX + destW < 0 || destY + destH < 0 || destX > scaledWidth || destY > scaledHeight)
                        continue;

                    // 繪製菱形邊框（S32 區塊是菱形的 isometric 投影）
                    // 菱形四個角點：上、右、下、左
                    float centerX = destX + destW / 2;
                    float centerY = destY + destH / 2;
                    float halfW = destW / 2;
                    float halfH = destH / 2;

                    PointF top = new PointF(centerX, destY);           // 上角
                    PointF right = new PointF(destX + destW, centerY); // 右角
                    PointF bottom = new PointF(centerX, destY + destH); // 下角
                    PointF left = new PointF(destX, centerY);          // 左角

                    // 繪製菱形四條邊
                    g.DrawLine(pen, top, right);
                    g.DrawLine(pen, right, bottom);
                    g.DrawLine(pen, bottom, left);
                    g.DrawLine(pen, left, top);
                }
            }
        }

        /// <summary>
        /// 直接渲染區塊到 mini map 像素（不經過 full-size bitmap）
        /// </summary>
        private void RenderBlockToMiniMapDirect(S32Data s32Data, int blockX, int blockY, float scale,
            int scaledWidth, int scaledHeight, ConcurrentDictionary<(int x, int y), ushort> pixelData)
        {
            // 計算這個區塊在 mini map 上的範圍
            int destX = (int)(blockX * scale);
            int destY = (int)(blockY * scale);
            int destW = Math.Max(1, (int)(BlockWidth * scale));
            int destH = Math.Max(1, (int)(BlockHeight * scale));

            // 計算每個 mini map 像素對應多少個 Layer1 格子
            float cellsPerPixelX = 128.0f / destW;
            float cellsPerPixelY = 64.0f / destH;

            // 對於每個目標像素，取樣多個格子並混合顏色
            for (int dy = 0; dy < destH; dy++)
            {
                int miniY = destY + dy;
                if (miniY < 0 || miniY >= scaledHeight) continue;

                for (int dx = 0; dx < destW; dx++)
                {
                    int miniX = destX + dx;
                    if (miniX < 0 || miniX >= scaledWidth) continue;

                    // 計算這個像素對應的 Layer1 格子範圍
                    int cellStartX = (int)(dx * cellsPerPixelX);
                    int cellEndX = (int)((dx + 1) * cellsPerPixelX);
                    int cellStartY = (int)(dy * cellsPerPixelY);
                    int cellEndY = (int)((dy + 1) * cellsPerPixelY);

                    // 確保至少取樣一個格子
                    if (cellEndX <= cellStartX) cellEndX = cellStartX + 1;
                    if (cellEndY <= cellStartY) cellEndY = cellStartY + 1;

                    // 限制範圍
                    cellEndX = Math.Min(cellEndX, 128);
                    cellEndY = Math.Min(cellEndY, 64);

                    // 收集這個範圍內的顏色並混合
                    int totalR = 0, totalG = 0, totalB = 0;
                    int colorCount = 0;

                    for (int cy = cellStartY; cy < cellEndY; cy++)
                    {
                        for (int cx = cellStartX; cx < cellEndX; cx++)
                        {
                            var cell = s32Data.Layer1[cy, cx];
                            if (cell != null && cell.TileId >= 0)
                            {
                                ushort color = GetTileRepresentativeColor(cell.TileId, cell.IndexId);
                                if (color != 0)
                                {
                                    // RGB555 解碼
                                    int r = (color >> 10) & 0x1F;
                                    int g = (color >> 5) & 0x1F;
                                    int b = color & 0x1F;
                                    totalR += r;
                                    totalG += g;
                                    totalB += b;
                                    colorCount++;
                                }
                            }
                        }
                    }

                    if (colorCount > 0)
                    {
                        // 計算平均顏色
                        int avgR = totalR / colorCount;
                        int avgG = totalG / colorCount;
                        int avgB = totalB / colorCount;
                        ushort avgColor = (ushort)((avgR << 10) | (avgG << 5) | avgB);
                        pixelData[(miniX, miniY)] = avgColor;
                    }
                }
            }
        }

        /// <summary>
        /// 取得 tile 的代表色（快取）
        /// </summary>
        private ConcurrentDictionary<(int tileId, int indexId), ushort> _tileColorCache = new ConcurrentDictionary<(int, int), ushort>();

        private ushort GetTileRepresentativeColor(int tileId, int indexId)
        {
            var key = (tileId, indexId);
            if (_tileColorCache.TryGetValue(key, out ushort cached))
            {
                return cached;
            }

            ushort color = CalculateTileRepresentativeColor(tileId, indexId);
            _tileColorCache.TryAdd(key, color);
            return color;
        }

        private ushort CalculateTileRepresentativeColor(int tileId, int indexId)
        {
            try
            {
                // 使用 TileProvider 取得 til 資料（自動處理 override 和備援）
                var tilArray = TileProvider.Instance.GetTilArrayWithFallback(tileId, indexId, 0, out indexId);
                if (tilArray == null || indexId < 0 || indexId >= tilArray.Count) return 0;
                byte[] tilData = tilArray[indexId];
                if (tilData == null || tilData.Length < 10) return 0;

                // 取中間的像素作為代表色
                // 跳過 type byte，讀取中間區域的顏色
                int offset = 1;
                byte type = tilData[0];

                if ( (type & 0x02) == 0)
                {
                    // 菱形 tile，取中間行（約第 12 行）的像素
                    // 計算到第 12 行的偏移
                    for (int row = 0; row < 11; row++)
                    {
                        int n = (row + 1) * 2;
                        offset += n * 2; // 每像素 2 bytes
                    }
                    // 現在在第 12 行，有 24 個像素，取中間
                    if (offset + 24 < tilData.Length)
                    {
                        return (ushort)(tilData[offset + 12] | (tilData[offset + 13] << 8));
                    }
                }
                else
                {
                    // 壓縮格式，取前面的像素
                    if (tilData.Length > 10)
                    {
                        offset = 5; // 跳過 header
                        // 找第一個像素
                        if (offset + 5 < tilData.Length)
                        {
                            int skipBytes = tilData[offset];
                            offset += 1 + skipBytes / 2;
                            if (offset + 2 < tilData.Length)
                            {
                                int len = tilData[offset];
                                offset++;
                                if (len > 0 && offset + 2 <= tilData.Length)
                                {
                                    return (ushort)(tilData[offset] | (tilData[offset + 1] << 8));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略錯誤
            }
            return 0;
        }

        /// <summary>
        /// 取得或渲染 S32 區塊
        /// </summary>
        private Bitmap GetOrRenderS32Block(S32Data s32Data, bool showLayer1, bool showLayer2, bool showLayer4)
        {
            // 快取 key 包含 layer 設定，避免返回錯誤的渲染結果
            string cacheKey = $"{s32Data.FilePath}_{showLayer1}_{showLayer2}_{showLayer4}";
            if (_s32BlockCache.TryGetValue(cacheKey, out Bitmap cached))
            {
                return cached;
            }

            Bitmap rendered = RenderS32Block(s32Data, showLayer1, showLayer2, showLayer4);
            _s32BlockCache.TryAdd(cacheKey, rendered);
            return rendered;
        }

        /// <summary>
        /// 取得或渲染取樣版 S32 區塊
        /// </summary>
        private Bitmap GetOrRenderS32BlockSampled(S32Data s32Data, int sampleStep)
        {
            string cacheKey = $"{s32Data.FilePath}_s{sampleStep}";
            if (_s32BlockCacheSampled.TryGetValue(cacheKey, out Bitmap cached))
            {
                return cached;
            }

            Bitmap rendered = RenderS32BlockSampled(s32Data, sampleStep);
            _s32BlockCacheSampled.TryAdd(cacheKey, rendered);
            return rendered;
        }

        /// <summary>
        /// 渲染單個 S32 區塊
        /// </summary>
        private Bitmap RenderS32Block(S32Data s32Data, bool showLayer1, bool showLayer2, bool showLayer4)
        {
            Bitmap result = new Bitmap(BlockWidth, BlockHeight, PixelFormat.Format16bppRgb555);

            Rectangle rect = new Rectangle(0, 0, result.Width, result.Height);
            BitmapData bmpData = result.LockBits(rect, ImageLockMode.ReadWrite, result.PixelFormat);
            int rowpix = bmpData.Stride;

            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;

                // 第一層（地板）
                if (showLayer1)
                {
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 128; x++)
                        {
                            var cell = s32Data.Layer1[y, x];
                            if (cell != null && cell.TileId >= 0)
                            {
                                int baseX = 0;
                                int baseY = 63 * 12;
                                baseX -= 24 * (x / 2);
                                baseY -= 12 * (x / 2);

                                int pixelX = baseX + x * 24 + y * 24;
                                int pixelY = baseY + y * 12;

                                DrawTilToBufferDirect(pixelX, pixelY, cell.TileId, cell.IndexId, rowpix, ptr, BlockWidth, BlockHeight);
                            }
                        }
                    }
                }

                // 第二層
                if (showLayer2)
                {
                    foreach (var item in s32Data.Layer2)
                    {
                        if (item.TileId >= 0)
                        {
                            int x = item.X;
                            int y = item.Y;

                            int baseX = 0;
                            int baseY = 63 * 12;
                            baseX -= 24 * (x / 2);
                            baseY -= 12 * (x / 2);

                            int pixelX = baseX + x * 24 + y * 24;
                            int pixelY = baseY + y * 12;

                            DrawTilToBufferDirect(pixelX, pixelY, item.TileId, item.IndexId, rowpix, ptr, BlockWidth, BlockHeight);
                        }
                    }
                }

                // 第四層（物件）
                if (showLayer4)
                {
                    var sortedObjects = s32Data.Layer4.OrderBy(o => o.Layer).ToList();

                    foreach (var obj in sortedObjects)
                    {
                        int baseX = 0;
                        int baseY = 63 * 12;
                        baseX -= 24 * (obj.X / 2);
                        baseY -= 12 * (obj.X / 2);

                        int pixelX = baseX + obj.X * 24 + obj.Y * 24;
                        int pixelY = baseY + obj.Y * 12;

                        DrawTilToBufferDirect(pixelX, pixelY, obj.TileId, obj.IndexId, rowpix, ptr, BlockWidth, BlockHeight);
                    }
                }
            }

            result.UnlockBits(bmpData);
            return result;
        }

        /// <summary>
        /// 渲染取樣版 S32 區塊
        /// </summary>
        private Bitmap RenderS32BlockSampled(S32Data s32Data, int sampleStep)
        {
            Bitmap result = new Bitmap(BlockWidth, BlockHeight, PixelFormat.Format16bppRgb555);

            Rectangle rect = new Rectangle(0, 0, result.Width, result.Height);
            BitmapData bmpData = result.LockBits(rect, ImageLockMode.ReadWrite, result.PixelFormat);
            int rowpix = bmpData.Stride;

            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;

                // Layer1（地板）- 取樣並填滿
                for (int sy = 0; sy < 64; sy += sampleStep)
                {
                    for (int sx = 0; sx < 128; sx += sampleStep)
                    {
                        var cell = s32Data.Layer1[sy, sx];
                        if (cell == null || cell.TileId == 0) continue;

                        // 用取樣的 tile 填滿整個區域
                        for (int dy = 0; dy < sampleStep && sy + dy < 64; dy++)
                        {
                            for (int dx = 0; dx < sampleStep && sx + dx < 128; dx++)
                            {
                                int x = sx + dx;
                                int y = sy + dy;

                                int baseX = 0;
                                int baseY = 63 * 12;
                                baseX -= 24 * (x / 2);
                                baseY -= 12 * (x / 2);

                                int pixelX = baseX + x * 24 + y * 24;
                                int pixelY = baseY + y * 12;

                                DrawTilToBufferDirect(pixelX, pixelY, cell.TileId, cell.IndexId, rowpix, ptr, BlockWidth, BlockHeight);
                            }
                        }
                    }
                }

                // Layer4（物件）- 取樣
                var sortedObjects = s32Data.Layer4.OrderBy(o => o.Layer).ToList();
                foreach (var obj in sortedObjects)
                {
                    // 簡單取樣：只渲染符合取樣間隔的物件
                    if (obj.X % sampleStep != 0 || obj.Y % sampleStep != 0) continue;

                    int baseX = 0;
                    int baseY = 63 * 12;
                    baseX -= 24 * (obj.X / 2);
                    baseY -= 12 * (obj.X / 2);

                    int pixelX = baseX + obj.X * 24 + obj.Y * 24;
                    int pixelY = baseY + obj.Y * 12;

                    DrawTilToBufferDirect(pixelX, pixelY, obj.TileId, obj.IndexId, rowpix, ptr, BlockWidth, BlockHeight);
                }
            }

            result.UnlockBits(bmpData);
            return result;
        }

        /// <summary>
        /// 繪製 Tile 到緩衝區
        /// </summary>
        private unsafe void DrawTilToBufferDirect(int pixelX, int pixelY, int tileId, int indexId, int rowpix, byte* ptr, int maxWidth, int maxHeight)
        {
            try
            {
                // 使用 TileProvider 取得 til 資料（自動處理 override 和備援）
                var tilArray = TileProvider.Instance.GetTilArrayWithFallback(tileId, indexId, pixelX, out indexId);
                if (tilArray == null || indexId < 0 || indexId >= tilArray.Count) return;
                byte[] tilData = tilArray[indexId];
                if (tilData == null) return;

                fixed (byte* til_ptr_fixed = tilData)
                {
                    byte* til_ptr = til_ptr_fixed;
                    byte type = *(til_ptr++);

                    if ((type & 0x02) == 0 && (type & 0x01) != 0)
                    {
                        for (int ty = 0; ty < 24; ty++)
                        {
                            int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                            int tx = 0;
                            for (int p = 0; p < n; p++)
                            {
                                ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));
                                int startX = pixelX + tx;
                                int startY = pixelY + ty;
                                if (startX >= 0 && startX < maxWidth && startY >= 0 && startY < maxHeight)
                                {
                                    int v = startY * rowpix + (startX * 2);
                                    *(ptr + v) = (byte)(color & 0x00FF);
                                    *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                                }
                                tx++;
                            }
                        }
                    }
                    else if ( (type & 0x02) == 0 && (type & 0x01) == 0)
                    {
                        for (int ty = 0; ty < 24; ty++)
                        {
                            int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                            int tx = 24 - n;
                            for (int p = 0; p < n; p++)
                            {
                                ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));
                                int startX = pixelX + tx;
                                int startY = pixelY + ty;
                                if (startX >= 0 && startX < maxWidth && startY >= 0 && startY < maxHeight)
                                {
                                    int v = startY * rowpix + (startX * 2);
                                    *(ptr + v) = (byte)(color & 0x00FF);
                                    *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                                }
                                tx++;
                            }
                        }
                    }
                    else
                    {
                        // 其他壓縮格式
                        byte x_offset = *(til_ptr++);
                        byte y_offset = *(til_ptr++);
                        byte xxLen = *(til_ptr++);
                        byte yLen = *(til_ptr++);

                        for (int ty = 0; ty < yLen; ty++)
                        {
                            int tx = x_offset;
                            byte xSegmentCount = *(til_ptr++);
                            for (int nx = 0; nx < xSegmentCount; nx++)
                            {
                                tx += *(til_ptr++) / 2;
                                int xLen = *(til_ptr++);
                                for (int p = 0; p < xLen; p++)
                                {
                                    ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));
                                    int startX = pixelX + tx;
                                    int startY = pixelY + ty + y_offset;
                                    if (startX >= 0 && startX < maxWidth && startY >= 0 && startY < maxHeight)
                                    {
                                        int v = startY * rowpix + (startX * 2);
                                        *(ptr + v) = (byte)(color & 0x00FF);
                                        *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                                    }
                                    tx++;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略錯誤
            }
        }

        /// <summary>
        /// 清除指定 tileId 的顏色快取
        /// </summary>
        public void ClearTileColorCache(List<int> tileIds)
        {
            if (tileIds == null || tileIds.Count == 0)
            {
                // 清除全部
                _tileColorCache.Clear();
                return;
            }

            // 清除指定 tileIds 的快取
            var keysToRemove = _tileColorCache.Keys
                .Where(k => tileIds.Contains(k.tileId))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _tileColorCache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// 清除指定 tileId 的 S32 區塊快取
        /// </summary>
        public void InvalidateS32BlockCache()
        {
            // 清除所有 S32 區塊快取（因為任何區塊都可能包含該 tile）
            foreach (var bmp in _s32BlockCache.Values)
            {
                bmp?.Dispose();
            }
            _s32BlockCache.Clear();

            foreach (var bmp in _s32BlockCacheSampled.Values)
            {
                bmp?.Dispose();
            }
            _s32BlockCacheSampled.Clear();
        }

        /// <summary>
        /// 清除快取
        /// </summary>
        public void ClearCache()
        {
            foreach (var bmp in _s32BlockCache.Values)
            {
                bmp?.Dispose();
            }
            _s32BlockCache.Clear();

            foreach (var bmp in _s32BlockCacheSampled.Values)
            {
                bmp?.Dispose();
            }
            _s32BlockCacheSampled.Clear();

            TileProvider.Instance.ClearCache();
            _tileColorCache.Clear();
        }
    }
}
