using System;
using System.Collections.Concurrent;
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
using static L1MapViewer.Other.Struct;
using L1MapViewer.Compatibility;

namespace L1MapViewer.CLI.Commands
{
    /// <summary>
    /// 效能測試指令
    /// </summary>
    public static class BenchmarkCommands
    {
        /// <summary>
        /// 格子查找效能測試
        /// </summary>
        public static int CellFind(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: benchmark-cellfind <map_path> [--runs N] [--optimized]");
                Console.WriteLine("範例: benchmark-cellfind C:\\client\\map\\4");
                Console.WriteLine("      benchmark-cellfind C:\\client\\map\\4 --optimized");
                Console.WriteLine();
                Console.WriteLine("測試從世界座標查找對應格子的效能");
                return 1;
            }

            string mapPath = args[0];
            int runs = 3;
            bool useOptimized = false;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--runs" && i + 1 < args.Length)
                    int.TryParse(args[++i], out runs);
                else if (args[i] == "--optimized")
                    useOptimized = true;
            }

            var loadResult = MapLoader.Load(mapPath);
            if (!loadResult.Success) return 1;

            Console.WriteLine();
            Console.WriteLine($"Runs: {runs}");
            Console.WriteLine($"Method: {(useOptimized ? "Optimized" : "BruteForce")}");
            Console.WriteLine();

            // 產生隨機測試點
            var random = new Random(42);
            int testPointCount = 20;
            var testPoints = new List<(int x, int y)>();

            for (int i = 0; i < testPointCount; i++)
            {
                int x = loadResult.MinX + random.Next(loadResult.MapWidth);
                int y = loadResult.MinY + random.Next(loadResult.MapHeight);
                testPoints.Add((x, y));
            }

            Console.WriteLine($"Test Points: {testPointCount}");
            Console.WriteLine();

            var allTimes = new List<long>();
            var totalCellsChecked = new List<int>();
            var totalS32Checked = new List<int>();
            int foundCount = 0;

            var sw = new Stopwatch();

            for (int run = 1; run <= runs; run++)
            {
                Console.WriteLine($"--- Run {run}/{runs} ---");

                int runCellsChecked = 0;
                int runS32Checked = 0;
                int runFound = 0;

                sw.Restart();

                foreach (var (x, y) in testPoints)
                {
                    CellFinder.FindResult result;
                    if (useOptimized)
                        result = CellFinder.FindCellOptimized(x, y, loadResult.S32Files.Values);
                    else
                        result = CellFinder.FindCellBruteForce(x, y, loadResult.S32Files.Values);

                    runCellsChecked += result.CellsChecked;
                    runS32Checked += result.S32Checked;
                    if (result.Found) runFound++;
                }

                sw.Stop();
                allTimes.Add(sw.ElapsedMilliseconds);
                totalCellsChecked.Add(runCellsChecked);
                totalS32Checked.Add(runS32Checked);
                if (run == 1) foundCount = runFound;

                Console.WriteLine($"  Time: {sw.ElapsedMilliseconds} ms");
                Console.WriteLine($"  S32 Checked: {runS32Checked}");
                Console.WriteLine($"  Cells Checked: {runCellsChecked:N0}");
                Console.WriteLine($"  Found: {runFound}/{testPointCount}");
            }

            Console.WriteLine();
            Console.WriteLine("=== Summary ===");
            Console.WriteLine($"Average Time: {allTimes.Average():F1} ms");
            Console.WriteLine($"Min: {allTimes.Min()} ms, Max: {allTimes.Max()} ms");
            Console.WriteLine($"Avg S32 Checked: {totalS32Checked.Average():F0}");
            Console.WriteLine($"Avg Cells Checked: {totalCellsChecked.Average():F0}");
            Console.WriteLine($"Found Rate: {foundCount}/{testPointCount} ({100.0 * foundCount / testPointCount:F1}%)");

            return 0;
        }

        /// <summary>
        /// 模擬完整 MouseClick 流程（格子查找 + 渲染）
        /// </summary>
        public static int MouseClick(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: benchmark-mouseclick <map_path> [--runs N]");
                Console.WriteLine("範例: benchmark-mouseclick C:\\client\\map\\4");
                Console.WriteLine();
                Console.WriteLine("模擬完整的滑鼠點擊流程：格子查找 + Viewport 渲染");
                return 1;
            }

            string mapPath = args[0];
            int runs = 5;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--runs" && i + 1 < args.Length)
                    int.TryParse(args[++i], out runs);
            }

            var loadResult = MapLoader.Load(mapPath);
            if (!loadResult.Success) return 1;

            Console.WriteLine();
            Console.WriteLine($"Runs: {runs}");

            int viewportWidth = 2048;
            int viewportHeight = 2048;

            var renderer = new ViewportRenderer();
            var checkedFiles = new HashSet<string>(loadResult.S32Files.Keys);

            var random = new Random(42);
            int testPointCount = 10;
            var testPoints = new List<(int x, int y)>();

            for (int i = 0; i < testPointCount; i++)
            {
                int x = loadResult.MinX + random.Next(loadResult.MapWidth);
                int y = loadResult.MinY + random.Next(loadResult.MapHeight);
                testPoints.Add((x, y));
            }

            Console.WriteLine($"Test Points: {testPointCount}");
            Console.WriteLine($"Viewport: {viewportWidth} x {viewportHeight}");
            Console.WriteLine();

            var allCellFindTimes = new List<long>();
            var allRenderTimes = new List<long>();
            var allTotalTimes = new List<long>();

            var sw = new Stopwatch();

            for (int run = 1; run <= runs; run++)
            {
                Console.WriteLine($"--- Run {run}/{runs} ---");

                long totalCellFind = 0;
                long totalRender = 0;
                int foundCount = 0;

                renderer.ClearCache();

                foreach (var (clickX, clickY) in testPoints)
                {
                    sw.Restart();
                    var findResult = CellFinder.FindCellOptimized(clickX, clickY, loadResult.S32Files.Values);
                    sw.Stop();
                    totalCellFind += sw.ElapsedMilliseconds;

                    if (findResult.Found)
                    {
                        foundCount++;

                        int scrollX = clickX - viewportWidth / 2;
                        int scrollY = clickY - viewportHeight / 2;
                        var worldRect = new Rectangle(scrollX, scrollY, viewportWidth, viewportHeight);

                        sw.Restart();
                        var stats = new ViewportRenderer.RenderStats();
                        using (var bmp = renderer.RenderViewport(worldRect, loadResult.S32Files, checkedFiles,
                            true, true, true, out stats))
                        {
                        }
                        sw.Stop();
                        totalRender += sw.ElapsedMilliseconds;
                    }
                }

                long runTotal = totalCellFind + totalRender;
                allCellFindTimes.Add(totalCellFind);
                allRenderTimes.Add(totalRender);
                allTotalTimes.Add(runTotal);

                Console.WriteLine($"  Cell Find:  {totalCellFind,5} ms ({(double)totalCellFind / testPointCount:F1} ms/click)");
                Console.WriteLine($"  Render:     {totalRender,5} ms ({(foundCount > 0 ? (double)totalRender / foundCount : 0):F1} ms/click)");
                Console.WriteLine($"  Total:      {runTotal,5} ms ({(double)runTotal / testPointCount:F1} ms/click)");
                Console.WriteLine($"  Found: {foundCount}/{testPointCount}");
            }

            Console.WriteLine();
            Console.WriteLine("=== Summary (per click) ===");
            Console.WriteLine($"Cell Find Avg: {allCellFindTimes.Average() / testPointCount:F1} ms");
            Console.WriteLine($"Render Avg:    {allRenderTimes.Average() / testPointCount:F1} ms");
            Console.WriteLine($"Total Avg:     {allTotalTimes.Average() / testPointCount:F1} ms");
            Console.WriteLine();
            Console.WriteLine($"Min Total: {allTotalTimes.Min() / testPointCount:F1} ms/click");
            Console.WriteLine($"Max Total: {allTotalTimes.Max() / testPointCount:F1} ms/click");

            return 0;
        }

        /// <summary>
        /// 測試附近群組搜尋效能（UpdateNearbyGroupThumbnails 的核心邏輯）
        /// </summary>
        public static int NearbyGroups(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: benchmark-nearbygroups <map_path> [--runs N] [--radius N]");
                Console.WriteLine("範例: benchmark-nearbygroups C:\\client\\map\\4");
                Console.WriteLine("      benchmark-nearbygroups C:\\client\\map\\4 --radius 20");
                Console.WriteLine();
                Console.WriteLine("測試 UpdateNearbyGroupThumbnails 的群組收集效能");
                return 1;
            }

            string mapPath = args[0];
            int runs = 5;
            int radius = 10;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--runs" && i + 1 < args.Length)
                    int.TryParse(args[++i], out runs);
                else if (args[i] == "--radius" && i + 1 < args.Length)
                    int.TryParse(args[++i], out radius);
            }

            var loadResult = MapLoader.Load(mapPath);
            if (!loadResult.Success) return 1;

            Console.WriteLine();
            Console.WriteLine($"Runs: {runs}");
            Console.WriteLine($"Radius: {radius}");

            // 統計 Layer4 物件總數
            int totalLayer4 = 0;
            int totalLayer5 = 0;
            foreach (var s32 in loadResult.S32Files.Values)
            {
                totalLayer4 += s32.Layer4.Count;
                totalLayer5 += s32.Layer5.Count;
            }
            Console.WriteLine($"Total Layer4 Objects: {totalLayer4:N0}");
            Console.WriteLine($"Total Layer5 Items: {totalLayer5:N0}");
            Console.WriteLine();

            // 產生隨機測試點（選擇有 Layer4 物件的格子）
            var random = new Random(42);
            int testPointCount = 10;
            var testCells = new List<(S32Data s32, int cellX, int cellY, int gameX, int gameY)>();

            // 收集所有有 Layer4 物件的格子
            var cellsWithLayer4 = new List<(S32Data s32, int cellX, int cellY, int gameX, int gameY)>();
            foreach (var s32 in loadResult.S32Files.Values)
            {
                foreach (var obj in s32.Layer4)
                {
                    int gameX = s32.SegInfo.nLinBeginX + obj.X / 2;
                    int gameY = s32.SegInfo.nLinBeginY + obj.Y;
                    cellsWithLayer4.Add((s32, obj.X, obj.Y, gameX, gameY));
                }
            }

            // 隨機選擇測試點
            for (int i = 0; i < Math.Min(testPointCount, cellsWithLayer4.Count); i++)
            {
                int idx = random.Next(cellsWithLayer4.Count);
                testCells.Add(cellsWithLayer4[idx]);
            }

            Console.WriteLine($"Test Points: {testCells.Count}");
            Console.WriteLine();

            var allCollectGroupsTimes = new List<long>();
            var allCollectLayer5Times = new List<long>();
            var allTotalTimes = new List<long>();

            var sw = new Stopwatch();

            for (int run = 1; run <= runs; run++)
            {
                Console.WriteLine($"--- Run {run}/{runs} ---");

                long totalCollectGroups = 0;
                long totalCollectLayer5 = 0;
                int totalGroups = 0;
                int totalObjects = 0;

                foreach (var (clickedS32, cellX, cellY, clickedGameX, clickedGameY) in testCells)
                {
                    // Step 1: 收集點擊格子的 Layer5 設定
                    sw.Restart();
                    var clickedCellLayer5 = new Dictionary<int, byte>();
                    foreach (var item in clickedS32.Layer5)
                    {
                        if (item.X == cellX && item.Y == cellY)
                        {
                            if (!clickedCellLayer5.ContainsKey(item.ObjectIndex))
                                clickedCellLayer5[item.ObjectIndex] = item.Type;
                        }
                    }
                    sw.Stop();
                    totalCollectLayer5 += sw.ElapsedMilliseconds;

                    // Step 2: 收集附近的群組
                    sw.Restart();
                    var nearbyGroups = new Dictionary<int, (int distance, List<ObjectTile> objects, bool hasLayer5, byte layer5Type)>();

                    foreach (var s32Data in loadResult.S32Files.Values)
                    {
                        int segStartX = s32Data.SegInfo.nLinBeginX;
                        int segStartY = s32Data.SegInfo.nLinBeginY;

                        foreach (var obj in s32Data.Layer4)
                        {
                            int objGameX = segStartX + obj.X / 2;
                            int objGameY = segStartY + obj.Y;
                            int distance = Math.Abs(objGameX - clickedGameX) + Math.Abs(objGameY - clickedGameY);

                            if (distance <= radius)
                            {
                                if (!nearbyGroups.ContainsKey(obj.GroupId))
                                {
                                    bool hasLayer5 = clickedCellLayer5.TryGetValue(obj.GroupId, out byte layer5Type);
                                    nearbyGroups[obj.GroupId] = (distance, new List<ObjectTile>(), hasLayer5, layer5Type);
                                }

                                var current = nearbyGroups[obj.GroupId];
                                if (distance < current.distance)
                                    nearbyGroups[obj.GroupId] = (distance, current.objects, current.hasLayer5, current.layer5Type);

                                nearbyGroups[obj.GroupId].objects.Add(obj);
                            }
                        }
                    }
                    sw.Stop();
                    totalCollectGroups += sw.ElapsedMilliseconds;

                    totalGroups += nearbyGroups.Count;
                    totalObjects += nearbyGroups.Values.Sum(g => g.objects.Count);
                }

                long runTotal = totalCollectGroups + totalCollectLayer5;
                allCollectGroupsTimes.Add(totalCollectGroups);
                allCollectLayer5Times.Add(totalCollectLayer5);
                allTotalTimes.Add(runTotal);

                Console.WriteLine($"  Collect Layer5:  {totalCollectLayer5,5} ms");
                Console.WriteLine($"  Collect Groups:  {totalCollectGroups,5} ms");
                Console.WriteLine($"  Total:           {runTotal,5} ms ({(double)runTotal / testCells.Count:F1} ms/click)");
                Console.WriteLine($"  Avg Groups/click: {(double)totalGroups / testCells.Count:F1}");
                Console.WriteLine($"  Avg Objects/click: {(double)totalObjects / testCells.Count:F1}");
            }

            Console.WriteLine();
            Console.WriteLine("=== Summary (per click) ===");
            Console.WriteLine($"Collect Layer5 Avg: {allCollectLayer5Times.Average() / testCells.Count:F1} ms");
            Console.WriteLine($"Collect Groups Avg: {allCollectGroupsTimes.Average() / testCells.Count:F1} ms");
            Console.WriteLine($"Total Avg:          {allTotalTimes.Average() / testCells.Count:F1} ms");
            Console.WriteLine();
            Console.WriteLine("Note: This only tests data collection, not thumbnail generation.");
            Console.WriteLine("Thumbnail generation (2600ms in your log) is the main bottleneck.");

            return 0;
        }

        /// <summary>
        /// 測試空間索引 vs 暴力搜尋的效能比較
        /// </summary>
        public static int SpatialIndex(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: benchmark-spatialindex <map_path> [--runs N] [--radius N]");
                Console.WriteLine("範例: benchmark-spatialindex C:\\client\\map\\4");
                Console.WriteLine();
                Console.WriteLine("比較空間索引 vs 暴力搜尋的效能");
                return 1;
            }

            string mapPath = args[0];
            int runs = 5;
            int radius = 10;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--runs" && i + 1 < args.Length)
                    int.TryParse(args[++i], out runs);
                else if (args[i] == "--radius" && i + 1 < args.Length)
                    int.TryParse(args[++i], out radius);
            }

            var loadResult = MapLoader.Load(mapPath);
            if (!loadResult.Success) return 1;

            Console.WriteLine();
            Console.WriteLine($"Runs: {runs}");
            Console.WriteLine($"Radius: {radius}");

            // 統計 Layer4 物件總數
            int totalLayer4 = 0;
            foreach (var s32 in loadResult.S32Files.Values)
                totalLayer4 += s32.Layer4.Count;
            Console.WriteLine($"Total Layer4 Objects: {totalLayer4:N0}");
            Console.WriteLine();

            // 建立空間索引
            Console.Write("Building spatial index...");
            var spatialIndex = new Layer4SpatialIndex();
            spatialIndex.Build(loadResult.S32Files.Values);
            Console.WriteLine($" {spatialIndex.BuildTimeMs} ms");
            Console.WriteLine($"  Grid cells: {spatialIndex.GridCellCount:N0}");
            Console.WriteLine($"  Objects indexed: {spatialIndex.TotalObjects:N0}");
            Console.WriteLine();

            // 產生隨機測試點
            var random = new Random(42);
            int testPointCount = 20;
            var testCells = new List<(int gameX, int gameY)>();

            var cellsWithLayer4 = new List<(int gameX, int gameY)>();
            foreach (var s32 in loadResult.S32Files.Values)
            {
                foreach (var obj in s32.Layer4)
                {
                    int gameX = s32.SegInfo.nLinBeginX + obj.X / 2;
                    int gameY = s32.SegInfo.nLinBeginY + obj.Y;
                    cellsWithLayer4.Add((gameX, gameY));
                }
            }

            for (int i = 0; i < Math.Min(testPointCount, cellsWithLayer4.Count); i++)
            {
                int idx = random.Next(cellsWithLayer4.Count);
                testCells.Add(cellsWithLayer4[idx]);
            }

            Console.WriteLine($"Test Points: {testCells.Count}");
            Console.WriteLine();

            var bruteForceTimes = new List<long>();
            var spatialIndexTimes = new List<long>();
            var sw = new Stopwatch();

            for (int run = 1; run <= runs; run++)
            {
                Console.WriteLine($"--- Run {run}/{runs} ---");

                // 暴力搜尋
                long bruteForceTotal = 0;
                int bruteForceGroups = 0;
                int bruteForceObjects = 0;

                foreach (var (centerX, centerY) in testCells)
                {
                    sw.Restart();
                    var nearbyGroups = new Dictionary<int, (int distance, List<ObjectTile> objects)>();

                    foreach (var s32Data in loadResult.S32Files.Values)
                    {
                        int segStartX = s32Data.SegInfo.nLinBeginX;
                        int segStartY = s32Data.SegInfo.nLinBeginY;

                        foreach (var obj in s32Data.Layer4)
                        {
                            int objGameX = segStartX + obj.X / 2;
                            int objGameY = segStartY + obj.Y;
                            int distance = Math.Abs(objGameX - centerX) + Math.Abs(objGameY - centerY);

                            if (distance <= radius)
                            {
                                if (!nearbyGroups.TryGetValue(obj.GroupId, out var groupInfo))
                                {
                                    groupInfo = (distance, new List<ObjectTile>());
                                    nearbyGroups[obj.GroupId] = groupInfo;
                                }
                                if (distance < groupInfo.distance)
                                    nearbyGroups[obj.GroupId] = (distance, groupInfo.objects);
                                groupInfo.objects.Add(obj);
                            }
                        }
                    }
                    sw.Stop();
                    bruteForceTotal += sw.ElapsedMilliseconds;
                    bruteForceGroups += nearbyGroups.Count;
                    bruteForceObjects += nearbyGroups.Values.Sum(g => g.objects.Count);
                }

                // 空間索引搜尋
                long spatialTotal = 0;
                int spatialGroups = 0;
                int spatialObjects = 0;

                foreach (var (centerX, centerY) in testCells)
                {
                    sw.Restart();
                    var nearbyGroups = spatialIndex.CollectNearbyGroups(centerX, centerY, radius);
                    sw.Stop();
                    spatialTotal += sw.ElapsedMilliseconds;
                    spatialGroups += nearbyGroups.Count;
                    spatialObjects += nearbyGroups.Values.Sum(g => g.objects.Count);
                }

                bruteForceTimes.Add(bruteForceTotal);
                spatialIndexTimes.Add(spatialTotal);

                double speedup = bruteForceTotal > 0 ? (double)bruteForceTotal / Math.Max(1, spatialTotal) : 0;

                Console.WriteLine($"  Brute Force:    {bruteForceTotal,5} ms ({(double)bruteForceTotal / testCells.Count:F1} ms/click)");
                Console.WriteLine($"  Spatial Index:  {spatialTotal,5} ms ({(double)spatialTotal / testCells.Count:F1} ms/click)");
                Console.WriteLine($"  Speedup:        {speedup:F1}x");
                Console.WriteLine($"  Groups found:   {bruteForceGroups / testCells.Count:F1} (BF) vs {spatialGroups / testCells.Count:F1} (SI)");
                Console.WriteLine($"  Objects found:  {bruteForceObjects / testCells.Count:F1} (BF) vs {spatialObjects / testCells.Count:F1} (SI)");
            }

            Console.WriteLine();
            Console.WriteLine("=== Summary ===");
            double avgBruteForce = bruteForceTimes.Average();
            double avgSpatial = spatialIndexTimes.Average();
            double overallSpeedup = avgBruteForce / Math.Max(1, avgSpatial);

            Console.WriteLine($"Brute Force Avg:   {avgBruteForce:F0} ms ({avgBruteForce / testCells.Count:F1} ms/click)");
            Console.WriteLine($"Spatial Index Avg: {avgSpatial:F0} ms ({avgSpatial / testCells.Count:F1} ms/click)");
            Console.WriteLine($"Overall Speedup:   {overallSpeedup:F1}x");
            Console.WriteLine();
            Console.WriteLine($"Index Build Time:  {spatialIndex.BuildTimeMs} ms (one-time cost)");

            return 0;
        }

        /// <summary>
        /// 測試縮圖產生效能（不含實際 tile 繪製）
        /// </summary>
        public static int Thumbnails(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: benchmark-thumbnails <map_path> [--runs N] [--size N]");
                Console.WriteLine("範例: benchmark-thumbnails C:\\client\\map\\4");
                Console.WriteLine("      benchmark-thumbnails C:\\client\\map\\4 --size 80");
                Console.WriteLine();
                Console.WriteLine("測試縮圖產生的效能（不含實際 tile 繪製）");
                Console.WriteLine("這可以分離 Bitmap 操作開銷 vs tile 讀取/繪製開銷");
                return 1;
            }

            string mapPath = args[0];
            int runs = 3;
            int thumbnailSize = 80;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--runs" && i + 1 < args.Length)
                    int.TryParse(args[++i], out runs);
                else if (args[i] == "--size" && i + 1 < args.Length)
                    int.TryParse(args[++i], out thumbnailSize);
            }

            var loadResult = MapLoader.Load(mapPath);
            if (!loadResult.Success) return 1;

            Console.WriteLine();
            Console.WriteLine($"Runs: {runs}");
            Console.WriteLine($"Thumbnail Size: {thumbnailSize}x{thumbnailSize}");

            // 建立空間索引取得所有群組
            Console.Write("Building spatial index...");
            var spatialIndex = new Layer4SpatialIndex();
            spatialIndex.Build(loadResult.S32Files.Values);
            Console.WriteLine($" {spatialIndex.BuildTimeMs} ms");
            Console.WriteLine($"  Total Groups: {spatialIndex.GroupCount:N0}");
            Console.WriteLine();

            var allGroups = spatialIndex.GetAllGroups();
            var groupList = allGroups.OrderBy(k => k.Key).ToList();

            Console.WriteLine($"=== Benchmark: Generate {groupList.Count} Thumbnails ===");
            Console.WriteLine();

            var allTimes = new List<long>();
            var sw = new Stopwatch();

            for (int run = 1; run <= runs; run++)
            {
                Console.WriteLine($"--- Run {run}/{runs} ---");

                long totalBitmapCreate = 0;
                long totalFillWhite = 0;
                long totalCalcBounds = 0;
                long totalScale = 0;
                long totalDispose = 0;
                int processedCount = 0;

                sw.Restart();

                // 模擬並行處理（與 MapForm 相同）
                var parallelOptions = new System.Threading.Tasks.ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                };

                System.Threading.Tasks.Parallel.ForEach(groupList, parallelOptions, kvp =>
                {
                    var localSw = new Stopwatch();
                    var objects = kvp.Value;

                    // 1. 計算像素邊界
                    localSw.Start();
                    int pixelMinX = int.MaxValue, pixelMaxX = int.MinValue;
                    int pixelMinY = int.MaxValue, pixelMaxY = int.MinValue;

                    foreach (var item in objects)
                    {
                        var obj = item.obj;
                        int halfX = obj.X / 2;
                        int baseX = -24 * halfX;
                        int baseY = 63 * 12 - 12 * halfX;
                        int px = baseX + obj.X * 24 + obj.Y * 24;
                        int py = baseY + obj.Y * 12;

                        pixelMinX = Math.Min(pixelMinX, px);
                        pixelMaxX = Math.Max(pixelMaxX, px + 48);
                        pixelMinY = Math.Min(pixelMinY, py);
                        pixelMaxY = Math.Max(pixelMaxY, py + 48);
                    }
                    localSw.Stop();
                    System.Threading.Interlocked.Add(ref totalCalcBounds, localSw.ElapsedTicks);

                    // 2. 計算大小
                    int margin = 8;
                    int actualWidth = pixelMaxX - pixelMinX + margin * 2;
                    int actualHeight = pixelMaxY - pixelMinY + margin * 2;
                    int maxTempSize = 512;
                    int tempWidth = Math.Min(Math.Max(actualWidth, 64), maxTempSize);
                    int tempHeight = Math.Min(Math.Max(actualHeight, 64), maxTempSize);

                    float preScale = 1.0f;
                    if (actualWidth > maxTempSize || actualHeight > maxTempSize)
                    {
                        preScale = Math.Min((float)maxTempSize / actualWidth, (float)maxTempSize / actualHeight);
                        tempWidth = (int)(actualWidth * preScale);
                        tempHeight = (int)(actualHeight * preScale);
                    }

                    // 3. 建立暫存 Bitmap
                    localSw.Restart();
                    using (var tempBitmap = new Bitmap(tempWidth, tempHeight, PixelFormat.Format16bppRgb555))
                    {
                        localSw.Stop();
                        System.Threading.Interlocked.Add(ref totalBitmapCreate, localSw.ElapsedTicks);

                        // 4. 填充白色背景
                        localSw.Restart();
                        var rect = new Rectangle(0, 0, tempBitmap.Width, tempBitmap.Height);
                        var bmpData = tempBitmap.LockBits(rect, ImageLockMode.ReadWrite, tempBitmap.PixelFormat);
                        int rowpix = bmpData.Stride;

                        unsafe
                        {
                            byte* ptr = (byte*)bmpData.Scan0;
                            byte[] whiteLine = new byte[rowpix];
                            for (int x = 0; x < tempWidth; x++)
                            {
                                whiteLine[x * 2] = 0xFF;
                                whiteLine[x * 2 + 1] = 0x7F;
                            }
                            for (int y = 0; y < tempHeight; y++)
                            {
                                System.Runtime.InteropServices.Marshal.Copy(whiteLine, 0, (IntPtr)(ptr + y * rowpix), rowpix);
                            }
                        }
                        tempBitmap.UnlockBits(bmpData);
                        localSw.Stop();
                        System.Threading.Interlocked.Add(ref totalFillWhite, localSw.ElapsedTicks);

                        // 5. 縮放到目標大小
                        localSw.Restart();
                        using (var result = new Bitmap(thumbnailSize, thumbnailSize, PixelFormat.Format32bppArgb))
                        {
                            using (var g = GraphicsHelper.FromImage(result))
                            {
                                g.Clear(Colors.White);
                                g.SetInterpolationMode(InterpolationMode.NearestNeighbor);
                                g.SetPixelOffsetMode(PixelOffsetMode.HighSpeed);

                                float scaleX = (float)(thumbnailSize - 4) / tempWidth;
                                float scaleY = (float)(thumbnailSize - 4) / tempHeight;
                                float scale = Math.Min(scaleX, scaleY);
                                int scaledWidth = (int)(tempWidth * scale);
                                int scaledHeight = (int)(tempHeight * scale);
                                int drawX = (thumbnailSize - scaledWidth) / 2;
                                int drawY = (thumbnailSize - scaledHeight) / 2;

                                g.DrawImage(tempBitmap, drawX, drawY, scaledWidth, scaledHeight);
                                g.DrawRectangle(Pens.LightGray, 0, 0, thumbnailSize - 1, thumbnailSize - 1);
                            }
                            localSw.Stop();
                            System.Threading.Interlocked.Add(ref totalScale, localSw.ElapsedTicks);
                        }
                    }

                    System.Threading.Interlocked.Increment(ref processedCount);
                });

                sw.Stop();
                long totalMs = sw.ElapsedMilliseconds;
                allTimes.Add(totalMs);

                // 轉換 ticks 到 ms
                double ticksPerMs = Stopwatch.Frequency / 1000.0;
                double calcBoundsMs = totalCalcBounds / ticksPerMs;
                double bitmapCreateMs = totalBitmapCreate / ticksPerMs;
                double fillWhiteMs = totalFillWhite / ticksPerMs;
                double scaleMs = totalScale / ticksPerMs;

                Console.WriteLine($"  Total:          {totalMs,6} ms");
                Console.WriteLine($"  Per thumbnail:  {(double)totalMs / groupList.Count:F2} ms");
                Console.WriteLine();
                Console.WriteLine($"  Breakdown (cumulative across all threads):");
                Console.WriteLine($"    Calc Bounds:  {calcBoundsMs,8:F1} ms");
                Console.WriteLine($"    Bitmap Create:{bitmapCreateMs,8:F1} ms");
                Console.WriteLine($"    Fill White:   {fillWhiteMs,8:F1} ms");
                Console.WriteLine($"    Scale+Draw:   {scaleMs,8:F1} ms");
                Console.WriteLine();
            }

            Console.WriteLine("=== Summary ===");
            Console.WriteLine($"Average:  {allTimes.Average():F0} ms ({allTimes.Average() / groupList.Count:F2} ms/thumbnail)");
            Console.WriteLine($"Min:      {allTimes.Min()} ms");
            Console.WriteLine($"Max:      {allTimes.Max()} ms");
            Console.WriteLine();
            Console.WriteLine("NOTE: This benchmark does NOT include actual tile drawing (DrawTilToBufferDirect).");
            Console.WriteLine("      The real thumbnail generation includes tile lookup and pixel copying.");
            Console.WriteLine("      If this is fast but GUI is slow, tile drawing is the bottleneck.");

            return 0;
        }

        /// <summary>
        /// 繪製相鄰 S32 區塊（用於 debug Layer4 重疊問題）
        /// </summary>
        public static int RenderAdjacent(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("用法: render-adjacent <map_path> <gameX> <gameY> [--output dir] [--size N]");
                Console.WriteLine("範例: render-adjacent C:\\client\\map\\4 33384 32774");
                Console.WriteLine("      render-adjacent C:\\client\\map\\4 33384 32774 --size 5");
                Console.WriteLine();
                Console.WriteLine("繪製以指定遊戲座標為中心的相鄰 S32 區塊");
                Console.WriteLine("用於診斷 Layer4 跨區塊渲染問題");
                return 1;
            }

            string mapPath = args[0];
            if (!int.TryParse(args[1], out int gameX) || !int.TryParse(args[2], out int gameY))
            {
                Console.WriteLine("錯誤: gameX 和 gameY 必須是數字");
                return 1;
            }

            string outputDir = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "tests");
            int gridSize = 3; // 3x3 grid by default

            for (int i = 3; i < args.Length; i++)
            {
                if (args[i] == "--output" && i + 1 < args.Length)
                    outputDir = args[++i];
                else if (args[i] == "--size" && i + 1 < args.Length)
                    int.TryParse(args[++i], out gridSize);
            }

            // 確保 gridSize >= 2
            if (gridSize < 2) gridSize = 2;

            var loadResult = MapLoader.Load(mapPath);
            if (!loadResult.Success) return 1;

            // 測試 Tile 載入
            Console.WriteLine();
            Console.WriteLine("Testing Tile loading...");
            var testData1 = L1PakReader.UnPack("Tile", "1.til");
            var testData22 = L1PakReader.UnPack("Tile", "22.til");
            Console.WriteLine($"  1.til: {(testData1 != null ? testData1.Length + " bytes" : "NULL")}");
            Console.WriteLine($"  22.til: {(testData22 != null ? testData22.Length + " bytes" : "NULL")}");

            // 檢查 Layer4 物件的 TileId
            var s32WithL4 = loadResult.S32Files.Values.FirstOrDefault(s => s.Layer4.Count > 0);
            if (s32WithL4 != null)
            {
                var layer4Sample = s32WithL4.Layer4.Take(5).ToList();
                Console.WriteLine($"  Layer4 TileId samples from {Path.GetFileName(s32WithL4.FilePath)} ({s32WithL4.Layer4.Count} objects):");
                foreach (var obj in layer4Sample)
                {
                    var tilData = L1PakReader.UnPack("Tile", $"{obj.TileId}.til");
                    int parseCount = 0;
                    if (tilData != null)
                    {
                        var parsed = L1Til.Parse(tilData);
                        parseCount = parsed?.Count ?? 0;
                    }
                    Console.WriteLine($"    TileId={obj.TileId}, IndexId={obj.IndexId} -> {(tilData != null ? $"{tilData.Length} bytes, parsed={parseCount}" : "NULL")}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Target Game Coordinate: ({gameX}, {gameY})");
            Console.WriteLine($"Grid Size: {gridSize}x{gridSize}");
            Console.WriteLine();

            // 找出包含該遊戲座標的 S32
            S32Data centerS32 = null;
            foreach (var s32 in loadResult.S32Files.Values)
            {
                var seg = s32.SegInfo;
                if (gameX >= seg.nLinBeginX && gameX <= seg.nLinEndX &&
                    gameY >= seg.nLinBeginY && gameY <= seg.nLinEndY)
                {
                    centerS32 = s32;
                    break;
                }
            }

            if (centerS32 == null)
            {
                Console.WriteLine($"錯誤: 找不到包含座標 ({gameX}, {gameY}) 的 S32");
                return 1;
            }

            int centerBlockX = centerS32.SegInfo.nBlockX;
            int centerBlockY = centerS32.SegInfo.nBlockY;
            Console.WriteLine($"Center S32: {Path.GetFileName(centerS32.FilePath)}");
            Console.WriteLine($"  BlockX: 0x{centerBlockX:X4}, BlockY: 0x{centerBlockY:X4}");
            Console.WriteLine($"  Game Range: ({centerS32.SegInfo.nLinBeginX},{centerS32.SegInfo.nLinBeginY}) - ({centerS32.SegInfo.nLinEndX},{centerS32.SegInfo.nLinEndY})");
            Console.WriteLine();

            // 建立 blockX/blockY 索引
            var blockIndex = new Dictionary<(int, int), S32Data>();
            foreach (var s32 in loadResult.S32Files.Values)
            {
                blockIndex[(s32.SegInfo.nBlockX, s32.SegInfo.nBlockY)] = s32;
            }

            // 收集 grid 範圍內的 S32
            // 對於 2x2：中心在右下角，查詢左上、上、左、中心
            // 對於 3x3：中心在正中央
            int halfGrid = gridSize / 2;
            var gridS32 = new S32Data[gridSize, gridSize];
            int foundCount = 0;

            // 計算起始偏移
            int startOffset = (gridSize % 2 == 0) ? -(gridSize - 1) : -halfGrid;
            int endOffset = (gridSize % 2 == 0) ? 0 : halfGrid;

            Console.WriteLine($"Looking for {gridSize}x{gridSize} grid centered at (0x{centerBlockX:X4}, 0x{centerBlockY:X4}):");
            for (int dy = startOffset; dy <= endOffset; dy++)
            {
                for (int dx = startOffset; dx <= endOffset; dx++)
                {
                    int bx = centerBlockX + dx;
                    int by = centerBlockY + dy;
                    int gx = dx - startOffset;
                    int gy = dy - startOffset;

                    if (blockIndex.TryGetValue((bx, by), out var s32))
                    {
                        gridS32[gx, gy] = s32;
                        foundCount++;
                        Console.WriteLine($"  [{gx},{gy}] 0x{bx:X4}{by:X4}.s32 - Found ({s32.Layer4.Count} L4 objects)");
                    }
                    else
                    {
                        Console.WriteLine($"  [{gx},{gy}] 0x{bx:X4}{by:X4}.s32 - Not found");
                    }
                }
            }
            Console.WriteLine($"Found {foundCount}/{gridSize * gridSize} S32 files");
            Console.WriteLine();

            // 計算所有 S32 的世界像素位置，找出邊界
            int blockWidth = 3072;
            int blockHeight = 1536;
            int worldMinX = int.MaxValue, worldMinY = int.MaxValue;
            int worldMaxX = int.MinValue, worldMaxY = int.MinValue;

            var s32WorldPositions = new Dictionary<S32Data, (int worldX, int worldY)>();

            for (int gy = 0; gy < gridSize; gy++)
            {
                for (int gx = 0; gx < gridSize; gx++)
                {
                    var s32 = gridS32[gx, gy];
                    if (s32 == null) continue;

                    int[] loc = s32.SegInfo.GetLoc(1.0);
                    int worldX = loc[0];
                    int worldY = loc[1];
                    s32WorldPositions[s32] = (worldX, worldY);

                    worldMinX = Math.Min(worldMinX, worldX);
                    worldMinY = Math.Min(worldMinY, worldY);
                    worldMaxX = Math.Max(worldMaxX, worldX + blockWidth);
                    worldMaxY = Math.Max(worldMaxY, worldY + blockHeight);
                }
            }

            int totalWidth = worldMaxX - worldMinX;
            int totalHeight = worldMaxY - worldMinY;

            Console.WriteLine($"World bounds: ({worldMinX}, {worldMinY}) - ({worldMaxX}, {worldMaxY})");

            // 清除 Tile 快取
            _tilCache.Clear();

            Console.WriteLine($"Rendering combined image: {totalWidth}x{totalHeight} px");

            using (var combinedBitmap = new Bitmap(totalWidth, totalHeight, PixelFormat.Format16bppRgb555))
            {
                // 填充背景為灰色
                var rect = new Rectangle(0, 0, totalWidth, totalHeight);
                var bmpData = combinedBitmap.LockBits(rect, ImageLockMode.WriteOnly, combinedBitmap.PixelFormat);
                unsafe
                {
                    byte* ptr = (byte*)bmpData.Scan0;
                    // RGB555 灰色 (128,128,128) ≈ 0x4210
                    for (int y = 0; y < totalHeight; y++)
                    {
                        for (int x = 0; x < totalWidth; x++)
                        {
                            int offset = y * bmpData.Stride + x * 2;
                            ptr[offset] = 0x10;
                            ptr[offset + 1] = 0x42;
                        }
                    }
                }
                combinedBitmap.UnlockBits(bmpData);

                // 渲染每個 S32 的 Layer1 和 Layer2（地板層）
                var swTotal = Stopwatch.StartNew();
                Console.WriteLine("  Rendering Layer1 & Layer2...");
                var bmpDataCombined = combinedBitmap.LockBits(
                    new Rectangle(0, 0, totalWidth, totalHeight),
                    ImageLockMode.ReadWrite, combinedBitmap.PixelFormat);
                int combinedRowPix = bmpDataCombined.Stride;

                unsafe
                {
                    byte* combinedPtr = (byte*)bmpDataCombined.Scan0;

                    for (int gy = 0; gy < gridSize; gy++)
                    {
                        for (int gx = 0; gx < gridSize; gx++)
                        {
                            var s32 = gridS32[gx, gy];
                            if (s32 == null) continue;

                            // 使用世界座標計算偏移
                            var (worldX, worldY) = s32WorldPositions[s32];
                            int offsetX = worldX - worldMinX;
                            int offsetY = worldY - worldMinY;

                            // Layer 1 (地板)
                            for (int y = 0; y < 64; y++)
                            {
                                for (int x = 0; x < 128; x++)
                                {
                                    var cell = s32.Layer1[y, x];
                                    if (cell != null && cell.TileId > 0)
                                    {
                                        int halfX = x / 2;
                                        int baseX = -24 * halfX;
                                        int baseY = 63 * 12 - 12 * halfX;
                                        int pixelX = offsetX + baseX + x * 24 + y * 24;
                                        int pixelY = offsetY + baseY + y * 12;

                                        DrawTilToBufferDirect(pixelX, pixelY, cell.TileId, cell.IndexId,
                                            combinedRowPix, combinedPtr, totalWidth, totalHeight);
                                    }
                                }
                            }

                            // Layer 2
                            foreach (var item in s32.Layer2)
                            {
                                if (item.TileId > 0)
                                {
                                    int x = item.X;
                                    int y = item.Y;
                                    int halfX = x / 2;
                                    int baseX = -24 * halfX;
                                    int baseY = 63 * 12 - 12 * halfX;
                                    int pixelX = offsetX + baseX + x * 24 + y * 24;
                                    int pixelY = offsetY + baseY + y * 12;

                                    DrawTilToBufferDirect(pixelX, pixelY, item.TileId, item.IndexId,
                                        combinedRowPix, combinedPtr, totalWidth, totalHeight);
                                }
                            }
                        }
                    }

                    // 收集所有 S32 的 Layer4 物件，計算絕對像素位置
                    Console.WriteLine("  Collecting Layer4 objects...");
                    var allLayer4Objects = new List<(int pixelX, int pixelY, int layer, int tileId, int indexId)>();

                    for (int gy = 0; gy < gridSize; gy++)
                    {
                        for (int gx = 0; gx < gridSize; gx++)
                        {
                            var s32 = gridS32[gx, gy];
                            if (s32 == null) continue;

                            // 使用世界座標計算偏移
                            var (worldX, worldY) = s32WorldPositions[s32];
                            int offsetX = worldX - worldMinX;
                            int offsetY = worldY - worldMinY;

                            foreach (var obj in s32.Layer4)
                            {
                                int halfX = obj.X / 2;
                                int baseX = -24 * halfX;
                                int baseY = 63 * 12 - 12 * halfX;
                                int pixelX = offsetX + baseX + obj.X * 24 + obj.Y * 24;
                                int pixelY = offsetY + baseY + obj.Y * 12;

                                allLayer4Objects.Add((pixelX, pixelY, obj.Layer, obj.TileId, obj.IndexId));
                            }
                        }
                    }

                    Console.WriteLine($"  Total Layer4 objects: {allLayer4Objects.Count}");

                    // 按 Layer 全域排序後繪製
                    Console.WriteLine("  Rendering Layer4 (sorted by Layer)...");
                    int layer4Ok = 0, layer4Fail = 0;

                    foreach (var obj in allLayer4Objects.OrderBy(o => o.layer))
                    {
                        bool drawn = DrawTilToBufferDirect(obj.pixelX, obj.pixelY, obj.tileId, obj.indexId,
                            combinedRowPix, combinedPtr, totalWidth, totalHeight);
                        if (drawn) layer4Ok++;
                        else layer4Fail++;
                    }

                    Console.WriteLine($"  Layer4: {layer4Ok} ok, {layer4Fail} fail");
                }

                combinedBitmap.UnlockBits(bmpDataCombined);

                swTotal.Stop();
                Console.WriteLine($"  Total render time: {swTotal.ElapsedMilliseconds} ms");

                // 標記目標位置
                using (var g = GraphicsHelper.FromImage(combinedBitmap))
                {
                    // 使用世界座標計算目標位置
                    var (centerWorldX, centerWorldY) = s32WorldPositions[centerS32];
                    int centerOffsetX = centerWorldX - worldMinX;
                    int centerOffsetY = centerWorldY - worldMinY;

                    // 遊戲座標轉格子座標
                    int cellX = (gameX - centerS32.SegInfo.nLinBeginX) * 2;  // Layer4 X 是 0-127
                    int cellY = gameY - centerS32.SegInfo.nLinBeginY;        // Layer4 Y 是 0-63

                    // 格子座標轉像素座標（使用 RenderS32Block 的公式）
                    int halfX = cellX / 2;
                    int baseX = -24 * halfX;
                    int baseY = 63 * 12 - 12 * halfX;
                    int pixelX = baseX + cellX * 24 + cellY * 24;
                    int pixelY = baseY + cellY * 12;

                    // 加上世界座標偏移
                    int finalX = centerOffsetX + pixelX;
                    int finalY = centerOffsetY + pixelY;

                    // 畫紅色十字標記
                    using (var pen = new Pen(Colors.Red, 3))
                    {
                        g.DrawLine(pen, finalX - 20, finalY, finalX + 20, finalY);
                        g.DrawLine(pen, finalX, finalY - 20, finalX, finalY + 20);
                    }

                    // 畫目標區域的圓圈
                    using (var pen = new Pen(Colors.Red, 2))
                    {
                        g.DrawEllipse(pen, finalX - 50, finalY - 50, 100, 100);
                    }

                    Console.WriteLine();
                    Console.WriteLine($"Target marked at pixel ({finalX}, {finalY})");
                }

                // 儲存
                Directory.CreateDirectory(outputDir);
                string outputPath = Path.Combine(outputDir, $"adjacent_{gameX}_{gameY}.png");
                combinedBitmap.Save(outputPath, ImageFormat.Png);
                Console.WriteLine();
                Console.WriteLine($"Saved: {outputPath}");

                // 也輸出個別 S32 的 Layer4 範圍資訊
                Console.WriteLine();
                Console.WriteLine("=== Layer4 Analysis ===");
                foreach (var s32 in loadResult.S32Files.Values.Where(s => gridS32.Cast<S32Data>().Contains(s)))
                {
                    if (s32.Layer4.Count == 0) continue;

                    int minX = s32.Layer4.Min(o => o.X);
                    int maxX = s32.Layer4.Max(o => o.X);
                    int minY = s32.Layer4.Min(o => o.Y);
                    int maxY = s32.Layer4.Max(o => o.Y);

                    bool hasOverflow = maxX > 127 || maxY > 63 || minX < 0 || minY < 0;

                    Console.WriteLine($"{Path.GetFileName(s32.FilePath)}:");
                    Console.WriteLine($"  Layer4 X range: {minX} ~ {maxX} {(maxX > 127 ? "⚠️ OVERFLOW" : "")}");
                    Console.WriteLine($"  Layer4 Y range: {minY} ~ {maxY} {(maxY > 63 ? "⚠️ OVERFLOW" : "")}");
                    if (hasOverflow)
                    {
                        var overflowObjects = s32.Layer4.Where(o => o.X > 127 || o.Y > 63 || o.X < 0 || o.Y < 0).ToList();
                        Console.WriteLine($"  Overflow objects: {overflowObjects.Count}");
                        foreach (var obj in overflowObjects.Take(5))
                        {
                            Console.WriteLine($"    GroupId={obj.GroupId} X={obj.X} Y={obj.Y} Layer={obj.Layer}");
                        }
                        if (overflowObjects.Count > 5)
                            Console.WriteLine($"    ... and {overflowObjects.Count - 5} more");
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// 為 CLI 渲染單一 S32 區塊（所有層）
        /// </summary>
        private static Bitmap RenderS32BlockForCli(S32Data s32Data, bool debug = false)
        {
            int layer4Rendered = 0;
            int layer4Failed = 0;
            int blockWidth = 3072;
            int blockHeight = 1536;

            var result = new Bitmap(blockWidth, blockHeight, PixelFormat.Format16bppRgb555);
            var rect = new Rectangle(0, 0, blockWidth, blockHeight);
            var bmpData = result.LockBits(rect, ImageLockMode.ReadWrite, result.PixelFormat);
            int rowpix = bmpData.Stride;

            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;

                // 填充白色背景
                for (int y = 0; y < blockHeight; y++)
                {
                    for (int x = 0; x < blockWidth; x++)
                    {
                        int offset = y * rowpix + x * 2;
                        ptr[offset] = 0xFF;
                        ptr[offset + 1] = 0x7F;
                    }
                }

                // Layer 1 (地板)
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        var cell = s32Data.Layer1[y, x];
                        if (cell != null && cell.TileId > 0)
                        {
                            int halfX = x / 2;
                            int baseX = -24 * halfX;
                            int baseY = 63 * 12 - 12 * halfX;
                            int pixelX = baseX + x * 24 + y * 24;
                            int pixelY = baseY + y * 12;

                            DrawTilToBufferDirect(pixelX, pixelY, cell.TileId, cell.IndexId, rowpix, ptr, blockWidth, blockHeight);
                        }
                    }
                }

                // Layer 2
                foreach (var item in s32Data.Layer2)
                {
                    if (item.TileId > 0)
                    {
                        int x = item.X;
                        int y = item.Y;
                        int halfX = x / 2;
                        int baseX = -24 * halfX;
                        int baseY = 63 * 12 - 12 * halfX;
                        int pixelX = baseX + x * 24 + y * 24;
                        int pixelY = baseY + y * 12;

                        DrawTilToBufferDirect(pixelX, pixelY, item.TileId, item.IndexId, rowpix, ptr, blockWidth, blockHeight);
                    }
                }

                // Layer 4 (物件) - 按 Layer 排序
                int debugCount = 0;
                foreach (var obj in s32Data.Layer4.OrderBy(o => o.Layer))
                {
                    int halfX = obj.X / 2;
                    int baseX = -24 * halfX;
                    int baseY = 63 * 12 - 12 * halfX;
                    int pixelX = baseX + obj.X * 24 + obj.Y * 24;
                    int pixelY = baseY + obj.Y * 12;

                    bool drawn = DrawTilToBufferDirect(pixelX, pixelY, obj.TileId, obj.IndexId, rowpix, ptr, blockWidth, blockHeight);
                    if (drawn) layer4Rendered++;
                    else
                    {
                        layer4Failed++;
                        if (debug && debugCount++ < 3)
                        {
                            // 手動測試這個 Tile
                            var testData = L1PakReader.UnPack("Tile", $"{obj.TileId}.til");
                            var testParsed = testData != null ? L1Til.Parse(testData) : null;
                            Console.Write($"\n    [L4 FAIL] TileId={obj.TileId}, IndexId={obj.IndexId}, data={testData?.Length ?? 0}, parsed={testParsed?.Count ?? 0}");
                            if (testParsed != null && obj.IndexId < testParsed.Count)
                            {
                                var td = testParsed[obj.IndexId];
                                Console.Write($", tilData[0]={td?[0] ?? -1}");
                            }
                        }
                    }
                }
            }

            result.UnlockBits(bmpData);

            if (debug && (layer4Rendered > 0 || layer4Failed > 0))
            {
                Console.Write($" [L4: {layer4Rendered} ok, {layer4Failed} fail]");
            }

            return result;
        }

        // Tile 資料快取
        private static ConcurrentDictionary<int, List<byte[]>> _tilCache = new ConcurrentDictionary<int, List<byte[]>>();

        /// <summary>
        /// 繪製 Tile 到 buffer
        /// </summary>
        private static unsafe bool DrawTilToBufferDirect(int pixelX, int pixelY, int tileId, int indexId,
            int rowpix, byte* ptr, int maxWidth, int maxHeight)
        {
            // 從快取取得或載入 tile 資料（不緩存 null 值）
            if (!_tilCache.TryGetValue(tileId, out var tilArray))
            {
                string key = $"{tileId}.til";
                byte[] data = L1PakReader.UnPack("Tile", key);
                if (data != null)
                {
                    tilArray = L1Til.Parse(data);
                    if (tilArray != null)
                    {
                        _tilCache.TryAdd(tileId, tilArray);
                    }
                }
            }

            if (tilArray == null || indexId >= tilArray.Count)
            {
                return false;
            }

            byte[] tilData = tilArray[indexId];
            if (tilData == null || tilData.Length < 1)
            {
                return false;
            }

            byte type = tilData[0];

            fixed (byte* til_ptr_base = tilData)
            {
                byte* til_ptr = til_ptr_base + 1;

                // 根據 type 解析並繪製
                switch (type)
                {
                    case 0: case 1: case 8: case 9: case 16: case 17:
                        // Diamond 類型
                        DrawDiamondTile(til_ptr, pixelX, pixelY, rowpix, ptr, maxWidth, maxHeight);
                        break;

                    case 2: case 3: case 4: case 5: case 6: case 7:
                    case 32: case 33: case 40: case 41: case 48: case 49:
                        // 壓縮格式（type 2-7 也是壓縮格式）
                        DrawCompressedTile(til_ptr, pixelX, pixelY, rowpix, ptr, maxWidth, maxHeight, false);
                        break;

                    case 34: case 35: case 42: case 43: case 50: case 51:
                        // 壓縮格式 + 混合
                        DrawCompressedTile(til_ptr, pixelX, pixelY, rowpix, ptr, maxWidth, maxHeight, true);
                        break;

                    default:
                        // 未知類型，跳過
                        return false;
                }
            }
            return true;
        }

        private static unsafe void DrawDiamondTile(byte* til_ptr, int pixelX, int pixelY,
            int rowpix, byte* ptr, int maxWidth, int maxHeight)
        {
            int tx = 0, ty = 0;
            for (int row = 0; row < 24; row++)
            {
                int n = (row + 1) * 2;
                tx = 24 - row - 1;
                ty = row;
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
            for (int row = 0; row < 24; row++)
            {
                int n = (24 - row) * 2;
                tx = row;
                ty = 24 + row;
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

        private static unsafe void DrawCompressedTile(byte* til_ptr, int pixelX, int pixelY,
            int rowpix, byte* ptr, int maxWidth, int maxHeight, bool needsBlend)
        {
            int x_offset = *(til_ptr++);
            int y_offset = *(til_ptr++);
            int width = *(til_ptr++);
            int height = *(til_ptr++);

            for (int row = 0; row < height; row++)
            {
                int colCount = *(til_ptr++);
                for (int col = 0; col < colCount; col++)
                {
                    int startCol = *(til_ptr++);
                    int pixelCount = *(til_ptr++);

                    for (int p = 0; p < pixelCount; p++)
                    {
                        ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));
                        int startX = pixelX + x_offset + startCol + p;
                        int startY = pixelY + y_offset + row;

                        if (startX >= 0 && startX < maxWidth && startY >= 0 && startY < maxHeight)
                        {
                            int v = startY * rowpix + (startX * 2);
                            if (needsBlend)
                            {
                                ushort oldColor = (ushort)(*(ptr + v) | (*(ptr + v + 1) << 8));
                                color = (ushort)(oldColor + 0xffff - color);
                            }
                            *(ptr + v) = (byte)(color & 0x00FF);
                            *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 分析指定格子附近的 Layer4 物件（包含溢出物件）
        /// </summary>
        public static int AnalyzeOverflow(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("用法: analyze-overflow <map_path> <gameX> <gameY> [--radius N]");
                Console.WriteLine("範例: analyze-overflow C:\\client\\map\\4 33384 32775");
                return 1;
            }

            string mapPath = args[0];
            if (!int.TryParse(args[1], out int gameX) || !int.TryParse(args[2], out int gameY))
            {
                Console.WriteLine("錯誤: gameX 和 gameY 必須是數字");
                return 1;
            }

            int radius = 3;
            for (int i = 3; i < args.Length; i++)
            {
                if (args[i] == "--radius" && i + 1 < args.Length)
                    int.TryParse(args[++i], out radius);
            }

            var loadResult = MapLoader.Load(mapPath);
            if (!loadResult.Success) return 1;

            Console.WriteLine();
            Console.WriteLine($"Target: ({gameX}, {gameY}), Radius: {radius}");
            Console.WriteLine();

            // 找出包含該座標的 S32
            S32Data centerS32 = null;
            foreach (var s32 in loadResult.S32Files.Values)
            {
                var seg = s32.SegInfo;
                if (gameX >= seg.nLinBeginX && gameX <= seg.nLinEndX &&
                    gameY >= seg.nLinBeginY && gameY <= seg.nLinEndY)
                {
                    centerS32 = s32;
                    break;
                }
            }

            if (centerS32 == null)
            {
                Console.WriteLine($"錯誤: 找不到包含座標 ({gameX}, {gameY}) 的 S32");
                return 1;
            }

            Console.WriteLine($"Center S32: {Path.GetFileName(centerS32.FilePath)}");
            int centerBlockX = centerS32.SegInfo.nBlockX;
            int centerBlockY = centerS32.SegInfo.nBlockY;

            // 計算目標在中心區塊的格子座標
            int targetCellX = (gameX - centerS32.SegInfo.nLinBeginX) * 2;
            int targetCellY = gameY - centerS32.SegInfo.nLinBeginY;
            Console.WriteLine($"Target cell in center S32: X={targetCellX}, Y={targetCellY}");
            Console.WriteLine();

            // 建立 block 索引
            var blockIndex = new Dictionary<(int, int), S32Data>();
            foreach (var s32 in loadResult.S32Files.Values)
            {
                blockIndex[(s32.SegInfo.nBlockX, s32.SegInfo.nBlockY)] = s32;
            }

            // 查找相鄰區塊
            var adjacentBlocks = new (int dx, int dy, string name)[]
            {
                (-1, -1, "左上"), (0, -1, "上方"), (1, -1, "右上"),
                (-1, 0, "左邊"), (0, 0, "中心"), (1, 0, "右邊"),
                (-1, 1, "左下"), (0, 1, "下方"), (1, 1, "右下")
            };

            Console.WriteLine("=== 溢出到中心區塊的物件 ===");
            Console.WriteLine();

            var overflowObjects = new List<(string source, ObjectTile obj, int relX, int relY)>();

            foreach (var (dx, dy, name) in adjacentBlocks)
            {
                if (dx == 0 && dy == 0) continue; // 跳過中心自己

                var key = (centerBlockX + dx, centerBlockY + dy);
                if (!blockIndex.TryGetValue(key, out var srcS32)) continue;

                // 檢查該區塊的物件是否溢出到中心區塊
                foreach (var obj in srcS32.Layer4)
                {
                    // 計算物件溢出後在目標區塊的相對座標
                    int overflowDeltaX = obj.X / 128;
                    int overflowDeltaY = obj.Y / 64;

                    // 如果這個物件溢出到中心區塊
                    if (dx + overflowDeltaX == 0 && dy + overflowDeltaY == 0)
                    {
                        int relX = obj.X - (-dx) * 128;
                        int relY = obj.Y - (-dy) * 64;

                        // 檢查是否在目標附近
                        int distX = Math.Abs(relX - targetCellX);
                        int distY = Math.Abs(relY - targetCellY);
                        if (distX <= radius * 2 && distY <= radius)
                        {
                            overflowObjects.Add((Path.GetFileName(srcS32.FilePath), obj, relX, relY));
                        }
                    }
                }
            }

            // 收集中心區塊本身的物件
            var localObjects = new List<(ObjectTile obj, int cellX, int cellY)>();
            foreach (var obj in centerS32.Layer4)
            {
                int distX = Math.Abs(obj.X - targetCellX);
                int distY = Math.Abs(obj.Y - targetCellY);
                if (distX <= radius * 2 && distY <= radius)
                {
                    localObjects.Add((obj, obj.X, obj.Y));
                }
            }

            Console.WriteLine($"中心區塊附近物件: {localObjects.Count}");
            Console.WriteLine($"溢出到中心區塊的物件: {overflowObjects.Count}");
            Console.WriteLine();

            // 合併並按 Layer 排序
            var allObjects = new List<(string source, int groupId, int layer, int tileId, int cellX, int cellY, bool isOverflow)>();

            foreach (var (obj, cellX, cellY) in localObjects)
            {
                allObjects.Add((Path.GetFileName(centerS32.FilePath), obj.GroupId, obj.Layer, obj.TileId, cellX, cellY, false));
            }

            foreach (var (source, obj, relX, relY) in overflowObjects)
            {
                allObjects.Add((source, obj.GroupId, obj.Layer, obj.TileId, relX, relY, true));
            }

            var sorted = allObjects.OrderBy(o => o.layer).ToList();

            Console.WriteLine("=== 所有物件 (按 Layer 排序) ===");
            foreach (var item in sorted)
            {
                string overflow = item.isOverflow ? " [OVERFLOW]" : "";
                Console.WriteLine($"  Layer={item.layer,-3} Group={item.groupId,-5} Tile={item.tileId,-5} Cell=({item.cellX},{item.cellY}) Src={item.source}{overflow}");
            }

            // 按 Group 統計
            Console.WriteLine();
            Console.WriteLine("=== 按 Group 統計 ===");
            var groupStats = sorted.GroupBy(o => o.groupId)
                .Select(g => new {
                    GroupId = g.Key,
                    Count = g.Count(),
                    MinLayer = g.Min(o => o.layer),
                    MaxLayer = g.Max(o => o.layer),
                    HasOverflow = g.Any(o => o.isOverflow),
                    Sources = g.Select(o => o.source).Distinct().ToList()
                })
                .OrderByDescending(g => g.MaxLayer)
                .ToList();

            foreach (var g in groupStats)
            {
                string overflow = g.HasOverflow ? " [含溢出]" : "";
                Console.WriteLine($"  Group={g.GroupId,-5} Count={g.Count,-3} Layer={g.MinLayer}~{g.MaxLayer} Src={string.Join("+", g.Sources)}{overflow}");
            }

            return 0;
        }
    }
}
