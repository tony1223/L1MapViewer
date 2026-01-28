using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using NLog;
using L1MapViewer.Models;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// 匯出進度資訊
    /// </summary>
    public class ExportProgress
    {
        public int CurrentTile { get; set; }
        public int TotalTiles { get; set; }
        public double Percentage => TotalTiles > 0 ? (double)CurrentTile / TotalTiles * 100 : 0;
        public string Status { get; set; }
    }

    /// <summary>
    /// 分塊地圖匯出器 - 使用低記憶體方式匯出大型地圖
    /// </summary>
    public class TiledMapExporter
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 區塊大小（固定 2048×2048）
        /// </summary>
        public const int TileSize = 2048;

        /// <summary>
        /// 進度變更事件
        /// </summary>
        public event Action<ExportProgress> ProgressChanged;

        private readonly MiniMapRenderer _renderer;

        public TiledMapExporter()
        {
            _renderer = new MiniMapRenderer();
            _renderer.Padding = 0; // 匯出時不需要 padding
        }

        /// <summary>
        /// 非同步匯出地圖圖片
        /// </summary>
        /// <param name="outputPath">輸出檔案路徑</param>
        /// <param name="format">格式 ("png" 或 "bmp")</param>
        /// <param name="outputWidth">輸出寬度</param>
        /// <param name="outputHeight">輸出高度</param>
        /// <param name="s32Files">S32 檔案字典</param>
        /// <param name="checkedFiles">要渲染的檔案集合</param>
        /// <param name="bounds">地圖邊界資訊</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task ExportAsync(
            string outputPath,
            string format,
            int outputWidth,
            int outputHeight,
            Dictionary<string, S32Data> s32Files,
            HashSet<string> checkedFiles,
            MiniMapRenderer.MiniMapBounds bounds,
            CancellationToken cancellationToken)
        {
            _logger.Info($"[TiledExporter] Starting export: {outputWidth}x{outputHeight}, format={format}");

            // 計算區塊數量
            int tilesX = (int)Math.Ceiling((double)outputWidth / TileSize);
            int tilesY = (int)Math.Ceiling((double)outputHeight / TileSize);
            int totalTiles = tilesX * tilesY;

            _logger.Debug($"[TiledExporter] Tiles: {tilesX}x{tilesY} = {totalTiles}");

            try
            {
                if (format.ToLower() == "bmp")
                {
                    await ExportAsBmpAsync(outputPath, outputWidth, outputHeight,
                        tilesX, tilesY, s32Files, checkedFiles, bounds, cancellationToken);
                }
                else
                {
                    await ExportAsPngAsync(outputPath, outputWidth, outputHeight,
                        tilesX, tilesY, s32Files, checkedFiles, bounds, cancellationToken);
                }

                _logger.Info($"[TiledExporter] Export completed: {outputPath}");
            }
            catch (OperationCanceledException)
            {
                _logger.Info("[TiledExporter] Export cancelled by user");
                CleanupFile(outputPath);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[TiledExporter] Export failed");
                CleanupFile(outputPath);
                throw;
            }
        }

        /// <summary>
        /// 匯出為 PNG 格式
        /// </summary>
        private async Task ExportAsPngAsync(
            string outputPath,
            int outputWidth,
            int outputHeight,
            int tilesX,
            int tilesY,
            Dictionary<string, S32Data> s32Files,
            HashSet<string> checkedFiles,
            MiniMapRenderer.MiniMapBounds bounds,
            CancellationToken cancellationToken)
        {
            int totalTiles = tilesX * tilesY;

            // 計算縮放比例
            float scale = (float)outputWidth / bounds.ContentWidth;

            // 使用 SKBitmap 逐區塊渲染，最後合併輸出
            // 為了節省記憶體，使用逐行處理方式
            using (var outputBitmap = new SKBitmap(outputWidth, outputHeight, SKColorType.Rgb565, SKAlphaType.Opaque))
            {
                using (var canvas = new SKCanvas(outputBitmap))
                {
                    canvas.Clear(SKColors.Black);

                    int currentTile = 0;
                    for (int ty = 0; ty < tilesY; ty++)
                    {
                        for (int tx = 0; tx < tilesX; tx++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // 計算此區塊在輸出圖片的位置
                            int outX = tx * TileSize;
                            int outY = ty * TileSize;
                            int outW = Math.Min(TileSize, outputWidth - outX);
                            int outH = Math.Min(TileSize, outputHeight - outY);

                            // 計算對應的世界座標範圍
                            int worldX = bounds.WorldMinX + (int)(outX / scale);
                            int worldY = bounds.WorldMinY + (int)(outY / scale);
                            int worldW = (int)(outW / scale);
                            int worldH = (int)(outH / scale);

                            // 渲染此區域
                            using (var tileBitmap = _renderer.RenderRegion(
                                worldX, worldY, worldW, worldH,
                                outW, outH, s32Files, checkedFiles))
                            {
                                // 繪製到輸出 bitmap
                                canvas.DrawBitmap(tileBitmap, outX, outY);
                            }

                            // 回報進度
                            currentTile++;
                            OnProgressChanged(new ExportProgress
                            {
                                CurrentTile = currentTile,
                                TotalTiles = totalTiles,
                                Status = $"處理區塊 {currentTile}/{totalTiles}"
                            });

                            // 讓出執行緒
                            await Task.Yield();
                        }
                    }
                }

                // 儲存為 PNG
                OnProgressChanged(new ExportProgress
                {
                    CurrentTile = totalTiles,
                    TotalTiles = totalTiles,
                    Status = "正在儲存 PNG..."
                });

                using (var image = SKImage.FromBitmap(outputBitmap))
                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (var stream = File.OpenWrite(outputPath))
                {
                    data.SaveTo(stream);
                }
            }
        }

        /// <summary>
        /// 匯出為 BMP 格式（串流寫入）
        /// </summary>
        private async Task ExportAsBmpAsync(
            string outputPath,
            int outputWidth,
            int outputHeight,
            int tilesX,
            int tilesY,
            Dictionary<string, S32Data> s32Files,
            HashSet<string> checkedFiles,
            MiniMapRenderer.MiniMapBounds bounds,
            CancellationToken cancellationToken)
        {
            int totalTiles = tilesX * tilesY;
            float scale = (float)outputWidth / bounds.ContentWidth;

            // BMP 每行需要 4 byte 對齊
            int bytesPerPixel = 2; // RGB565
            int rowBytes = outputWidth * bytesPerPixel;
            int rowPadding = (4 - (rowBytes % 4)) % 4;
            int paddedRowBytes = rowBytes + rowPadding;

            // BMP 檔案大小
            int headerSize = 14 + 40 + 12; // File header + DIB header + RGB565 masks
            int imageSize = paddedRowBytes * outputHeight;
            int fileSize = headerSize + imageSize;

            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // 寫入 BMP header
                WriteBmpHeader(fs, outputWidth, outputHeight, fileSize, headerSize, imageSize);

                // BMP 是 bottom-up 格式，從最後一行開始寫
                // 使用暫存緩衝區存儲每行資料
                byte[] rowBuffer = new byte[paddedRowBytes];

                int currentTile = 0;

                // 逐區塊處理
                for (int ty = tilesY - 1; ty >= 0; ty--)
                {
                    for (int tx = 0; tx < tilesX; tx++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // 計算此區塊在輸出圖片的位置
                        int outX = tx * TileSize;
                        int outY = ty * TileSize;
                        int outW = Math.Min(TileSize, outputWidth - outX);
                        int outH = Math.Min(TileSize, outputHeight - outY);

                        // 計算對應的世界座標範圍
                        int worldX = bounds.WorldMinX + (int)(outX / scale);
                        int worldY = bounds.WorldMinY + (int)(outY / scale);
                        int worldW = (int)(outW / scale);
                        int worldH = (int)(outH / scale);

                        // 渲染此區域
                        using (var tileBitmap = _renderer.RenderRegion(
                            worldX, worldY, worldW, worldH,
                            outW, outH, s32Files, checkedFiles))
                        {
                            // 將區塊寫入檔案對應位置
                            WriteTileToBmp(fs, tileBitmap, outX, outY, outW, outH,
                                outputWidth, outputHeight, headerSize, paddedRowBytes, bytesPerPixel);
                        }

                        currentTile++;
                        OnProgressChanged(new ExportProgress
                        {
                            CurrentTile = currentTile,
                            TotalTiles = totalTiles,
                            Status = $"處理區塊 {currentTile}/{totalTiles}"
                        });

                        await Task.Yield();
                    }
                }
            }
        }

        /// <summary>
        /// 寫入 BMP 檔頭
        /// </summary>
        private void WriteBmpHeader(FileStream fs, int width, int height, int fileSize, int headerSize, int imageSize)
        {
            using (var bw = new BinaryWriter(fs, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                // BMP File Header (14 bytes)
                bw.Write((byte)'B');
                bw.Write((byte)'M');
                bw.Write(fileSize);
                bw.Write((ushort)0); // Reserved
                bw.Write((ushort)0); // Reserved
                bw.Write(headerSize); // Offset to pixel data

                // DIB Header (BITMAPINFOHEADER - 40 bytes)
                bw.Write(40); // Header size
                bw.Write(width);
                bw.Write(height);
                bw.Write((ushort)1); // Color planes
                bw.Write((ushort)16); // Bits per pixel (RGB565)
                bw.Write(3); // Compression: BI_BITFIELDS
                bw.Write(imageSize);
                bw.Write(2835); // Horizontal resolution (72 DPI)
                bw.Write(2835); // Vertical resolution (72 DPI)
                bw.Write(0); // Colors in palette
                bw.Write(0); // Important colors

                // RGB565 bit masks (12 bytes)
                bw.Write(0x0000F800); // Red mask
                bw.Write(0x000007E0); // Green mask
                bw.Write(0x0000001F); // Blue mask
            }
        }

        /// <summary>
        /// 將區塊寫入 BMP 檔案對應位置
        /// </summary>
        private void WriteTileToBmp(FileStream fs, SKBitmap tile, int tileX, int tileY, int tileW, int tileH,
            int imgWidth, int imgHeight, int headerSize, int paddedRowBytes, int bytesPerPixel)
        {
            unsafe
            {
                byte* tilePtr = (byte*)tile.GetPixels().ToPointer();
                int tileRowBytes = tile.RowBytes;

                // BMP 是 bottom-up，所以 y=0 在檔案最後
                for (int y = 0; y < tileH; y++)
                {
                    // 計算在圖片中的實際 Y 座標
                    int imgY = tileY + y;
                    // BMP 中的 Y 座標（從底部算起）
                    int bmpY = imgHeight - 1 - imgY;

                    // 計算檔案位置
                    long filePos = headerSize + (long)bmpY * paddedRowBytes + (long)tileX * bytesPerPixel;

                    fs.Seek(filePos, SeekOrigin.Begin);

                    // 寫入這一行的像素資料
                    byte* srcRow = tilePtr + y * tileRowBytes;
                    byte[] rowData = new byte[tileW * bytesPerPixel];
                    System.Runtime.InteropServices.Marshal.Copy((IntPtr)srcRow, rowData, 0, rowData.Length);
                    fs.Write(rowData, 0, rowData.Length);
                }
            }
        }

        /// <summary>
        /// 計算地圖邊界
        /// </summary>
        public MiniMapRenderer.MiniMapBounds CalculateBounds(
            Dictionary<string, S32Data> s32Files,
            HashSet<string> checkedFiles)
        {
            var bounds = new MiniMapRenderer.MiniMapBounds();

            int worldMinX = int.MaxValue, worldMinY = int.MaxValue;
            int worldMaxX = int.MinValue, worldMaxY = int.MinValue;

            foreach (var kvp in s32Files)
            {
                if (!checkedFiles.Contains(kvp.Key)) continue;

                var s32Data = kvp.Value;
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int blockX = loc[0];
                int blockY = loc[1];

                worldMinX = Math.Min(worldMinX, blockX);
                worldMinY = Math.Min(worldMinY, blockY);
                worldMaxX = Math.Max(worldMaxX, blockX + MiniMapRenderer.BlockWidth);
                worldMaxY = Math.Max(worldMaxY, blockY + MiniMapRenderer.BlockHeight);
            }

            if (worldMinX == int.MaxValue)
            {
                worldMinX = worldMinY = 0;
                worldMaxX = worldMaxY = 1;
            }

            bounds.WorldMinX = worldMinX;
            bounds.WorldMinY = worldMinY;
            bounds.ContentWidth = worldMaxX - worldMinX;
            bounds.ContentHeight = worldMaxY - worldMinY;

            return bounds;
        }

        /// <summary>
        /// 清理未完成的檔案
        /// </summary>
        private void CleanupFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    _logger.Debug($"[TiledExporter] Cleaned up incomplete file: {path}");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, $"[TiledExporter] Failed to cleanup file: {path}");
            }
        }

        /// <summary>
        /// 觸發進度變更事件
        /// </summary>
        private void OnProgressChanged(ExportProgress progress)
        {
            ProgressChanged?.Invoke(progress);
        }
    }
}
