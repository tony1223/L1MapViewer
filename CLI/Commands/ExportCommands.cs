using System;
using System.Collections.Generic;
using System.Diagnostics;
// using System.Drawing; // Replaced with Eto.Drawing
// using System.Drawing.Imaging; // Replaced with SkiaSharp
using System.IO;
using System.Linq;
using L1FlyMapViewer;
using L1MapViewer.Converter;
using L1MapViewer.Helper;
using L1MapViewer.Models;
using L1MapViewer.Reader;
using L1MapViewer.Compatibility;

namespace L1MapViewer.CLI.Commands
{
    /// <summary>
    /// 地圖匯出相關命令
    /// </summary>
    public static class ExportCommands
    {
        /// <summary>
        /// export-fullmap 命令 - 匯出單張地圖全圖
        /// </summary>
        public static int ExportFullMap(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法: -cli export-fullmap <地圖資料夾> <輸出.png> [選項]");
                Console.WriteLine();
                Console.WriteLine("選項:");
                Console.WriteLine("  --scale <比例>    縮放比例 (預設 1.0，即原始大小)");
                Console.WriteLine("  --max-size <px>   最大邊長像素 (與 scale 互斥)");
                Console.WriteLine("  --quality <0-3>   渲染品質 (0=最快, 3=最高品質，預設 2)");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  export-fullmap C:\\client\\map\\4 map4.png");
                Console.WriteLine("  export-fullmap C:\\client\\map\\4 map4.png --scale 0.5");
                Console.WriteLine("  export-fullmap C:\\client\\map\\4 map4.png --max-size 4096");
                return 1;
            }

            string mapPath = args[0];
            string outputPath = args[1];

            // 解析選項
            float scale = 1.0f;
            int maxSize = 0;
            int quality = 2;

            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "--scale" && i + 1 < args.Length)
                {
                    if (!float.TryParse(args[++i], out scale) || scale <= 0 || scale > 1)
                    {
                        Console.WriteLine("錯誤: scale 必須在 0 到 1 之間");
                        return 1;
                    }
                }
                else if (args[i] == "--max-size" && i + 1 < args.Length)
                {
                    if (!int.TryParse(args[++i], out maxSize) || maxSize <= 0)
                    {
                        Console.WriteLine("錯誤: max-size 必須為正整數");
                        return 1;
                    }
                }
                else if (args[i] == "--quality" && i + 1 < args.Length)
                {
                    if (!int.TryParse(args[++i], out quality) || quality < 0 || quality > 3)
                    {
                        Console.WriteLine("錯誤: quality 必須在 0 到 3 之間");
                        return 1;
                    }
                }
            }

            // 載入地圖
            var loadResult = MapLoader.Load(mapPath);
            if (!loadResult.Success)
            {
                return 1;
            }

            // 計算實際縮放比例
            if (maxSize > 0)
            {
                scale = Math.Min(
                    (float)maxSize / loadResult.MapWidth,
                    (float)maxSize / loadResult.MapHeight
                );
                if (scale > 1.0f) scale = 1.0f;
            }

            Console.WriteLine($"地圖: {loadResult.MapId}");
            Console.WriteLine($"原始大小: {loadResult.MapWidth} x {loadResult.MapHeight} px");
            Console.WriteLine($"縮放比例: {scale:F3}");

            int outputWidth = (int)(loadResult.MapWidth * scale);
            int outputHeight = (int)(loadResult.MapHeight * scale);
            Console.WriteLine($"輸出大小: {outputWidth} x {outputHeight} px");

            // 渲染地圖
            var sw = Stopwatch.StartNew();

            using (var bitmap = RenderFullMap(loadResult, scale, quality))
            {
                sw.Stop();
                Console.WriteLine($"渲染耗時: {sw.ElapsedMilliseconds} ms");

                // 儲存圖片
                string ext = Path.GetExtension(outputPath).ToLower();
                ImageFormat format = ext == ".jpg" || ext == ".jpeg" ? ImageFormat.Jpeg : ImageFormat.Png;

                string dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                bitmap.Save(outputPath, format);
                Console.WriteLine($"已儲存: {outputPath}");
            }

            return 0;
        }

        /// <summary>
        /// batch-export 命令 - 批次匯出所有地圖
        /// </summary>
        public static int BatchExport(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法: -cli batch-export <map目錄> <輸出目錄> [選項]");
                Console.WriteLine();
                Console.WriteLine("選項:");
                Console.WriteLine("  --scale <比例>    縮放比例 (預設 1.0)");
                Console.WriteLine("  --max-size <px>   最大邊長像素 (與 scale 互斥)");
                Console.WriteLine("  --quality <0-3>   渲染品質 (預設 2)");
                Console.WriteLine("  --format <格式>   輸出格式 png/jpg (預設 png)");
                Console.WriteLine("  --skip-existing   跳過已存在的檔案");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  batch-export C:\\client\\map C:\\output");
                Console.WriteLine("  batch-export C:\\client\\map C:\\output --max-size 2048 --format jpg");
                return 1;
            }

            string mapRoot = args[0];
            string outputDir = args[1];

            // 解析選項
            float scale = 1.0f;
            int maxSize = 0;
            int quality = 2;
            string format = "png";
            bool skipExisting = false;

            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "--scale" && i + 1 < args.Length)
                {
                    float.TryParse(args[++i], out scale);
                }
                else if (args[i] == "--max-size" && i + 1 < args.Length)
                {
                    int.TryParse(args[++i], out maxSize);
                }
                else if (args[i] == "--quality" && i + 1 < args.Length)
                {
                    int.TryParse(args[++i], out quality);
                }
                else if (args[i] == "--format" && i + 1 < args.Length)
                {
                    format = args[++i].ToLower();
                }
                else if (args[i] == "--skip-existing")
                {
                    skipExisting = true;
                }
            }

            if (!Directory.Exists(mapRoot))
            {
                Console.WriteLine($"目錄不存在: {mapRoot}");
                return 1;
            }

            // 確保輸出目錄存在
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 尋找所有地圖資料夾
            var mapDirs = Directory.GetDirectories(mapRoot)
                .Where(d => Directory.GetFiles(d, "*.s32").Length > 0)
                .OrderBy(d =>
                {
                    string name = Path.GetFileName(d);
                    return int.TryParse(name, out int n) ? n : int.MaxValue;
                })
                .ToList();

            Console.WriteLine($"找到 {mapDirs.Count} 個地圖");
            Console.WriteLine();

            int successCount = 0;
            int skipCount = 0;
            int failCount = 0;
            var totalSw = Stopwatch.StartNew();

            for (int i = 0; i < mapDirs.Count; i++)
            {
                string mapPath = mapDirs[i];
                string mapId = Path.GetFileName(mapPath);
                string outputPath = Path.Combine(outputDir, $"{mapId}.{format}");

                Console.WriteLine($"[{i + 1}/{mapDirs.Count}] 處理地圖 {mapId}...");

                // 檢查是否跳過
                if (skipExisting && File.Exists(outputPath))
                {
                    Console.WriteLine($"  已存在，跳過");
                    skipCount++;
                    continue;
                }

                try
                {
                    // 載入地圖（靜默模式）
                    var loadResult = MapLoader.Load(mapPath, verbose: false);
                    if (!loadResult.Success)
                    {
                        Console.WriteLine($"  載入失敗");
                        failCount++;
                        continue;
                    }

                    // 計算縮放比例
                    float actualScale = scale;
                    if (maxSize > 0)
                    {
                        actualScale = Math.Min(
                            (float)maxSize / loadResult.MapWidth,
                            (float)maxSize / loadResult.MapHeight
                        );
                        if (actualScale > 1.0f) actualScale = 1.0f;
                    }

                    int outputWidth = (int)(loadResult.MapWidth * actualScale);
                    int outputHeight = (int)(loadResult.MapHeight * actualScale);

                    var sw = Stopwatch.StartNew();
                    using (var bitmap = RenderFullMap(loadResult, actualScale, quality))
                    {
                        sw.Stop();

                        ImageFormat imgFormat = format == "jpg" ? ImageFormat.Jpeg : ImageFormat.Png;
                        bitmap.Save(outputPath, imgFormat);

                        Console.WriteLine($"  {loadResult.MapWidth}x{loadResult.MapHeight} -> {outputWidth}x{outputHeight}, {sw.ElapsedMilliseconds}ms");
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  錯誤: {ex.Message}");
                    failCount++;
                }

                // 強制 GC 釋放記憶體
                if ((i + 1) % 10 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            totalSw.Stop();

            Console.WriteLine();
            Console.WriteLine("=== 完成 ===");
            Console.WriteLine($"成功: {successCount}");
            Console.WriteLine($"跳過: {skipCount}");
            Console.WriteLine($"失敗: {failCount}");
            Console.WriteLine($"總耗時: {totalSw.Elapsed.TotalSeconds:F1} 秒");

            return failCount > 0 ? 1 : 0;
        }

        /// <summary>
        /// 渲染完整地圖
        /// </summary>
        private static Bitmap RenderFullMap(MapLoader.LoadResult loadResult, float scale, int quality)
        {
            int fullWidth = loadResult.MapWidth;
            int fullHeight = loadResult.MapHeight;
            int scaledWidth = (int)(fullWidth * scale);
            int scaledHeight = (int)(fullHeight * scale);

            // 決定渲染策略
            bool useDirectScaling = scale < 0.5f || loadResult.S32Files.Count > 20;

            var renderer = new MiniMapRenderer();
            var checkedFiles = new HashSet<string>(loadResult.S32Files.Keys);

            if (useDirectScaling)
            {
                // 使用 MiniMapRenderer 的直接縮放模式
                int targetSize = Math.Max(scaledWidth, scaledHeight);
                MiniMapRenderer.RenderStats stats;
                MiniMapRenderer.MiniMapBounds bounds;
                return renderer.RenderMiniMap(fullWidth, fullHeight, targetSize, loadResult.S32Files, checkedFiles, out stats, out bounds);
            }
            else
            {
                // 渲染原始大小，然後縮放
                // 先創建完整大小的 bitmap
                using (var fullBitmap = RenderFullSizeBitmap(loadResult, renderer))
                {
                    if (Math.Abs(scale - 1.0f) < 0.001f)
                    {
                        // 不需要縮放，直接複製
                        return (Bitmap)fullBitmap.Clone();
                    }
                    else
                    {
                        // 縮放
                        var result = new Bitmap(scaledWidth, scaledHeight, PixelFormat.Format16bppRgb555);
                        using (var g = GraphicsHelper.FromImage(result))
                        {
                            g.SetInterpolationMode(quality >= 2
                                ? InterpolationMode.HighQualityBicubic
                                : InterpolationMode.Bilinear);
                            g.DrawImage(fullBitmap, 0, 0, scaledWidth, scaledHeight);
                        }
                        return result;
                    }
                }
            }
        }

        /// <summary>
        /// 渲染完整大小的地圖 bitmap
        /// </summary>
        private static unsafe Bitmap RenderFullSizeBitmap(MapLoader.LoadResult loadResult, MiniMapRenderer renderer)
        {
            int fullWidth = loadResult.MapWidth;
            int fullHeight = loadResult.MapHeight;
            int offsetX = loadResult.MinX;
            int offsetY = loadResult.MinY;

            var fullBitmap = new Bitmap(fullWidth, fullHeight, PixelFormat.Format16bppRgb555);

            // 收集所有需要渲染的 tiles
            var allTiles = new List<(int pixelX, int pixelY, int layer, int tileId, int indexId)>();

            foreach (var s32Data in loadResult.S32Files.Values)
            {
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int blockOffsetX = loc[0] - offsetX;
                int blockOffsetY = loc[1] - offsetY;

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
                            int pixelX = blockOffsetX + baseX + x * 24 + y * 24;
                            int pixelY = blockOffsetY + baseY + y * 12;

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
                        int pixelX = blockOffsetX + baseX + x * 24 + y * 24;
                        int pixelY = blockOffsetY + baseY + y * 12;

                        allTiles.Add((pixelX, pixelY, -1, item.TileId, item.IndexId));
                    }
                }

                // Layer 4 (物件)
                foreach (var obj in s32Data.Layer4)
                {
                    int halfX = obj.X / 2;
                    int baseX = -24 * halfX;
                    int baseY = 63 * 12 - 12 * halfX;
                    int pixelX = blockOffsetX + baseX + obj.X * 24 + obj.Y * 24;
                    int pixelY = blockOffsetY + baseY + obj.Y * 12;

                    allTiles.Add((pixelX, pixelY, obj.Layer, obj.TileId, obj.IndexId));
                }
            }

            // 按 Layer 排序
            var sortedTiles = allTiles.OrderBy(t => t.layer).ToList();

            // 渲染到 bitmap
            Rectangle rect = new Rectangle(0, 0, fullWidth, fullHeight);
            BitmapData bmpData = fullBitmap.LockBits(rect, ImageLockMode.ReadWrite, fullBitmap.PixelFormat);
            int rowpix = bmpData.Stride;

            byte* ptr = (byte*)bmpData.Scan0;

            // Tile 快取
            var tilFileCache = new Dictionary<int, List<byte[]>>();

            foreach (var tile in sortedTiles)
            {
                DrawTilToBuffer(tile.pixelX, tile.pixelY, tile.tileId, tile.indexId,
                    rowpix, ptr, fullWidth, fullHeight, tilFileCache);
            }

            fullBitmap.UnlockBits(bmpData);
            return fullBitmap;
        }

        /// <summary>
        /// 繪製 Tile 到緩衝區
        /// </summary>
        private static unsafe void DrawTilToBuffer(int pixelX, int pixelY, int tileId, int indexId,
            int rowpix, byte* ptr, int maxWidth, int maxHeight, Dictionary<int, List<byte[]>> tilFileCache)
        {
            try
            {
                if (!tilFileCache.TryGetValue(tileId, out List<byte[]> tilArray))
                {
                    string key = $"{tileId}.til";
                    byte[] data = L1PakReader.UnPack("Tile", key);
                    if (data == null)
                    {
                        tilFileCache[tileId] = null;
                        return;
                    }
                    tilArray = L1Til.Parse(data);
                    tilFileCache[tileId] = tilArray;
                }

                // 備援機制
                if (tilArray == null || indexId >= tilArray.Count)
                {
                    if (tileId != 0)
                    {
                        if (!tilFileCache.TryGetValue(0, out tilArray))
                        {
                            string key = "0.til";
                            byte[] data = L1PakReader.UnPack("Tile", key);
                            if (data == null) return;
                            tilArray = L1Til.Parse(data);
                            tilFileCache[0] = tilArray;
                        }
                        if (tilArray == null || tilArray.Count == 0) return;
                        indexId = 187 + ((pixelX / 24) & 1);
                        if (indexId >= tilArray.Count)
                            indexId = indexId % tilArray.Count;
                    }
                    else
                    {
                        if (tilArray != null && tilArray.Count > 0)
                            indexId = indexId % tilArray.Count;
                        else
                            return;
                    }
                }

                if (tilArray == null || indexId >= tilArray.Count) return;
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
    }
}
