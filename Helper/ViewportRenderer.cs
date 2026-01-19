using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
// using System.Drawing; // Replaced with Eto.Drawing
// using System.Drawing.Imaging; // Replaced with SkiaSharp
using System.Linq;
using L1FlyMapViewer;
using L1MapViewer.Converter;
using L1MapViewer.Models;
using L1MapViewer.Other;
using L1MapViewer.Reader;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// Viewport 渲染器 - CLI 和 Form 共用的渲染邏輯
    /// </summary>
    public class ViewportRenderer
    {
        /// <summary>
        /// 渲染統計資訊
        /// </summary>
        public class RenderStats
        {
            public long CreateBitmapMs;
            public long SpatialQueryMs;
            public long SortMs;
            public long GetBlockMs;
            public long CopyBitmapMs;
            public long TotalMs;
            public int BlockCount;
            public int CandidateCount;
            public int CacheHits;
            public int CacheMisses;
        }

        // S32 區塊快取
        private ConcurrentDictionary<string, Bitmap> _s32BlockCache = new ConcurrentDictionary<string, Bitmap>();

        // 常數
        public const int BlockWidth = 64 * 24 * 2;  // 3072
        public const int BlockHeight = 64 * 12 * 2; // 1536

        /// <summary>
        /// 渲染 Viewport 區域（全域 Layer 排序）
        /// </summary>
        public Bitmap RenderViewport(
            Rectangle worldRect,
            Dictionary<string, S32Data> s32Files,
            HashSet<string> checkedFiles,
            bool showLayer1,
            bool showLayer2,
            bool showLayer4,
            out RenderStats stats)
        {
            stats = new RenderStats();
            var totalSw = Stopwatch.StartNew();

            // 1. 建立 Viewport Bitmap
            var createBmpSw = Stopwatch.StartNew();
            Bitmap viewportBitmap = new Bitmap(worldRect.Width, worldRect.Height, PixelFormat.Format16bppRgb555);
            createBmpSw.Stop();
            stats.CreateBitmapMs = createBmpSw.ElapsedMilliseconds;

            // 2. 空間查詢 - 找出與 worldRect 相交的 S32
            var spatialSw = Stopwatch.StartNew();
            var candidateFiles = GetS32FilesInRect(worldRect, s32Files);
            spatialSw.Stop();
            stats.SpatialQueryMs = spatialSw.ElapsedMilliseconds;
            stats.CandidateCount = candidateFiles.Count;

            // 3. 篩選需要渲染的區塊
            var blocksToRender = new List<(S32Data s32Data, int offsetX, int offsetY)>();
            foreach (var filePath in candidateFiles)
            {
                if (!s32Files.ContainsKey(filePath)) continue;
                if (!checkedFiles.Contains(filePath)) continue;

                var s32Data = s32Files[filePath];
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                Rectangle blockRect = new Rectangle(mx, my, BlockWidth, BlockHeight);
                if (!blockRect.IntersectsWith(worldRect)) continue;

                int offsetX = mx - worldRect.X;
                int offsetY = my - worldRect.Y;
                blocksToRender.Add((s32Data, offsetX, offsetY));
            }
            stats.BlockCount = blocksToRender.Count;

            // 4. 收集所有 Layer 物件並計算絕對像素位置
            var sortSw = Stopwatch.StartNew();
            var allTiles = new List<(int pixelX, int pixelY, int layer, int tileId, int indexId, bool needsBlend)>();

            foreach (var (s32Data, offsetX, offsetY) in blocksToRender)
            {
                // Layer 1 (地板) - layer 值設為 -2 確保最先繪製
                if (showLayer1)
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
                                int pixelX = offsetX + baseX + x * 24 + y * 24;
                                int pixelY = offsetY + baseY + y * 12;

                                allTiles.Add((pixelX, pixelY, -2, cell.TileId, cell.IndexId, false));
                            }
                        }
                    }
                }

                // Layer 2 - layer 值設為 -1 確保在 Layer1 之後、Layer4 之前繪製
                if (showLayer2)
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
                            int pixelX = offsetX + baseX + x * 24 + y * 24;
                            int pixelY = offsetY + baseY + y * 12;

                            allTiles.Add((pixelX, pixelY, -1, item.TileId, item.IndexId, false));
                        }
                    }
                }

                // Layer 4 (物件) - 使用原始 Layer 值
                if (showLayer4)
                {
                    foreach (var obj in s32Data.Layer4)
                    {
                        int halfX = obj.X / 2;
                        int baseX = -24 * halfX;
                        int baseY = 63 * 12 - 12 * halfX;
                        int pixelX = offsetX + baseX + obj.X * 24 + obj.Y * 24;
                        int pixelY = offsetY + baseY + obj.Y * 12;

                        allTiles.Add((pixelX, pixelY, obj.Layer, obj.TileId, obj.IndexId, false));
                    }
                }
            }

            // 5. 按 Layer 全域排序
            var sortedTiles = allTiles.OrderBy(t => t.layer).ToList();
            sortSw.Stop();
            stats.SortMs = sortSw.ElapsedMilliseconds;

            // 6. 繪製所有 Tile
            var getBlockSw = Stopwatch.StartNew();
            Rectangle rect = new Rectangle(0, 0, viewportBitmap.Width, viewportBitmap.Height);
            BitmapData bmpData = viewportBitmap.LockBits(rect, ImageLockMode.ReadWrite, viewportBitmap.PixelFormat);
            int rowpix = bmpData.Stride;

            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;

                foreach (var tile in sortedTiles)
                {
                    DrawTilToBufferDirect(tile.pixelX, tile.pixelY, tile.tileId, tile.indexId,
                        rowpix, ptr, viewportBitmap.Width, viewportBitmap.Height);
                }
            }

            viewportBitmap.UnlockBits(bmpData);
            getBlockSw.Stop();
            stats.GetBlockMs = getBlockSw.ElapsedMilliseconds;

            totalSw.Stop();
            stats.TotalMs = totalSw.ElapsedMilliseconds;

            return viewportBitmap;
        }

        /// <summary>
        /// 空間查詢 - 找出與指定區域相交的 S32 檔案
        /// </summary>
        private List<string> GetS32FilesInRect(Rectangle worldRect, Dictionary<string, S32Data> s32Files)
        {
            var result = new List<string>();

            foreach (var kvp in s32Files)
            {
                var s32Data = kvp.Value;
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                Rectangle blockRect = new Rectangle(mx, my, BlockWidth, BlockHeight);
                if (blockRect.IntersectsWith(worldRect))
                {
                    result.Add(kvp.Key);
                }
            }

            return result;
        }

        /// <summary>
        /// 取得或渲染 S32 區塊
        /// </summary>
        private Bitmap GetOrRenderS32Block(S32Data s32Data, bool showLayer1, bool showLayer2, bool showLayer4, out bool wasHit)
        {
            // 只有全開時使用快取
            if (showLayer1 && showLayer2 && showLayer4)
            {
                string cacheKey = s32Data.FilePath;
                if (_s32BlockCache.TryGetValue(cacheKey, out Bitmap cached))
                {
                    wasHit = true;
                    return cached;
                }

                wasHit = false;
                Bitmap rendered = RenderS32Block(s32Data, showLayer1, showLayer2, showLayer4);
                _s32BlockCache.TryAdd(cacheKey, rendered);
                return rendered;
            }

            wasHit = false;
            return RenderS32Block(s32Data, showLayer1, showLayer2, showLayer4);
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
                    else if ((type & 0x02) == 0 && (type & 0x01) == 0)
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
        /// 直接複製 bitmap 像素（比 Graphics.DrawImage 快）
        /// </summary>
        private void CopyBitmapDirect(Bitmap src, Bitmap dst, int dstX, int dstY)
        {
            int srcW = src.Width;
            int srcH = src.Height;
            int dstW = dst.Width;
            int dstH = dst.Height;

            int startX = Math.Max(0, -dstX);
            int startY = Math.Max(0, -dstY);
            int endX = Math.Min(srcW, dstW - dstX);
            int endY = Math.Min(srcH, dstH - dstY);

            if (startX >= endX || startY >= endY) return;

            Rectangle srcRect = new Rectangle(0, 0, srcW, srcH);
            Rectangle dstRect = new Rectangle(0, 0, dstW, dstH);

            BitmapData srcData = src.LockBits(srcRect, ImageLockMode.ReadOnly, PixelFormat.Format16bppRgb555);
            BitmapData dstData = dst.LockBits(dstRect, ImageLockMode.ReadWrite, PixelFormat.Format16bppRgb555);

            unsafe
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;
                int srcStride = srcData.Stride;
                int dstStride = dstData.Stride;

                for (int y = startY; y < endY; y++)
                {
                    ushort* srcRow = (ushort*)(srcPtr + y * srcStride + startX * 2);
                    ushort* dstRow = (ushort*)(dstPtr + (y + dstY) * dstStride + (startX + dstX) * 2);

                    for (int x = startX; x < endX; x++)
                    {
                        ushort pixel = *srcRow++;
                        if (pixel != 0)
                        {
                            *dstRow = pixel;
                        }
                        dstRow++;
                    }
                }
            }

            src.UnlockBits(srcData);
            dst.UnlockBits(dstData);
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
            TileProvider.Instance.ClearCache();
        }

        /// <summary>
        /// 清除單個 S32 區塊快取
        /// </summary>
        public void InvalidateBlockCache(string filePath)
        {
            if (_s32BlockCache.TryRemove(filePath, out Bitmap bmp))
            {
                bmp?.Dispose();
            }
        }
    }
}
