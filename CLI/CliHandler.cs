using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using L1FlyMapViewer;
using L1MapViewer.Converter;
using L1MapViewer.Helper;
using L1MapViewer.Models;
using L1MapViewer.Reader;
using static L1MapViewer.Other.Struct;

namespace L1MapViewer.CLI
{
    /// <summary>
    /// CLI 命令處理器
    /// </summary>
    public static class CliHandler
    {
        /// <summary>
        /// 執行 CLI 命令
        /// </summary>
        /// <returns>程式結束代碼 (0=成功)</returns>
        public static int Execute(string[] args)
        {
            if (args.Length < 2)
            {
                ShowHelp();
                return 1;
            }

            // 跳過 "-cli" 參數
            string command = args[1].ToLower();
            string[] cmdArgs = args.Skip(2).ToArray();

            try
            {
                switch (command)
                {
                    case "info":
                        return CmdInfo(cmdArgs);
                    case "layers":
                        return CmdLayers(cmdArgs);
                    case "l1":
                        return CmdLayer1(cmdArgs);
                    case "l3":
                        return CmdLayer3(cmdArgs);
                    case "l4":
                        return CmdLayer4(cmdArgs);
                    case "l5":
                        return CmdLayer5(cmdArgs);
                    case "l5check":
                        return CmdLayer5Check(cmdArgs);
                    case "l6":
                        return CmdLayer6(cmdArgs);
                    case "l7":
                        return CmdLayer7(cmdArgs);
                    case "l8":
                        return CmdLayer8(cmdArgs);
                    case "export":
                        return CmdExport(cmdArgs);
                    case "fix":
                        return CmdFix(cmdArgs);
                    case "coords":
                        return CmdCoords(cmdArgs);
                    case "export-tiles":
                        return CmdExportTiles(cmdArgs);
                    case "list-maps":
                        return CmdListMaps(cmdArgs);
                    case "extract-tile":
                        return CmdExtractTile(cmdArgs);
                    case "trim-s32":
                        return CmdTrimS32(cmdArgs);
                    case "generate-icon":
                        return CmdGenerateIcon(cmdArgs);
                    case "benchmark-viewport":
                        return CmdBenchmarkViewport(cmdArgs);
                    case "benchmark-minimap":
                        return CmdBenchmarkMiniMap(cmdArgs);
                    case "benchmark-s32parse":
                        return CmdBenchmarkS32Parse(cmdArgs);
                    case "benchmark-s32parse-detail":
                        return CmdBenchmarkS32ParseDetail(cmdArgs);
                    case "benchmark-tilevalidate":
                        return CmdBenchmarkTileValidate(cmdArgs);
                    case "benchmark-cellfind":
                        return Commands.BenchmarkCommands.CellFind(cmdArgs);
                    case "benchmark-mouseclick":
                        return Commands.BenchmarkCommands.MouseClick(cmdArgs);
                    case "benchmark-nearbygroups":
                        return Commands.BenchmarkCommands.NearbyGroups(cmdArgs);
                    case "benchmark-spatialindex":
                        return Commands.BenchmarkCommands.SpatialIndex(cmdArgs);
                    case "benchmark-thumbnails":
                        return Commands.BenchmarkCommands.Thumbnails(cmdArgs);
                    case "render-adjacent":
                        return Commands.BenchmarkCommands.RenderAdjacent(cmdArgs);
                    case "render-material":
                        return Commands.MaterialCommands.RenderMaterial(cmdArgs);
                    case "verify-material-tiles":
                        return Commands.MaterialCommands.VerifyMaterialTiles(cmdArgs);
                    case "list-til":
                        return CmdListTil(cmdArgs);
                    case "validate-tiles":
                        return CmdValidateTiles(cmdArgs);
                    case "export-fs32":
                        return CmdExportFs32(cmdArgs);
                    case "import-fs32":
                        return CmdImportFs32(cmdArgs);
                    case "help":
                    case "-h":
                    case "--help":
                        ShowHelp();
                        return 0;
                    default:
                        Console.WriteLine($"未知命令: {command}");
                        ShowHelp();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"錯誤: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// 顯示幫助資訊
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine(@"
L1MapViewer CLI - S32 檔案解析工具

用法: L1MapViewer.exe -cli <命令> [參數]

命令:
  info <s32檔案>              顯示 S32 檔案的基本資訊
  layers <s32檔案>            顯示各層的統計資訊
  l1 <s32檔案> [--tiles]      顯示第一層（地板）資訊
  l3 <s32檔案> [x] [y]        顯示第三層（屬性）資訊
  l4 <s32檔案> [--groups]     顯示第四層（物件）資訊
  l5 <s32檔案>                顯示第五層（透明圖塊）資訊
  l5check <s32檔案或資料夾> [-r N]  檢查 Layer5 異常（-r 指定檢查半徑，預設1）
  l6 <s32檔案>                顯示第六層（使用的 TileId）
  l7 <s32檔案>                顯示第七層（傳送點）資訊
  l8 <s32檔案>                顯示第八層（特效）資訊
  export <s32檔案> <輸出檔>   匯出 S32 資訊為 JSON
  fix <s32檔案> [--apply]     修復異常資料（如 X>=128 的 Layer4 物件）
  coords <地圖資料夾>         計算地圖的遊戲座標範圍（startX, endX, startY, endY）
  export-tiles <s32檔案或資料夾> <輸出.zip> [--til] [--png]
                              匯出 S32 使用的 Tile 到 ZIP（預設只匯出 .til）
  extract-tile <idx路徑> <tile-id> [輸出資料夾]
                              從指定的 Tile.idx/pak 提取特定 TileId 的所有 block
  compare-tile <classic-idx> <remaster-idx> <tile-id> [輸出資料夾]
                              比對 Classic 版與 R 版降級後的 Tile，輸出比較圖片
  render-material <fs3p> <地圖資料夾> <gameX> <gameY> [options]
                              渲染素材到指定地圖位置並存成圖片
  validate-tiles <s32檔案或地圖資料夾> [--client <路徑>]
                              驗證 S32 中使用的 TileId 是否存在於 Tile.idx 中
  export-fs32 <地圖資料夾> <輸出.fs32> [--downscale]
                              匯出地圖為 fs32 格式（--downscale 將 R 版 Tile 降級為 24x24）
  import-fs32 <fs32檔案> <目標地圖資料夾> [--replace]
                              匯入 fs32 到指定地圖（--replace 全部取代模式）
  help                        顯示此幫助資訊

範例:
  L1MapViewer.exe -cli info map.s32
  L1MapViewer.exe -cli l7 map.s32
  L1MapViewer.exe -cli l4 map.s32 --groups
  L1MapViewer.exe -cli export map.s32 output.json
");
        }

        /// <summary>
        /// list-til 命令 - 讀取 list.til 並顯示數字
        /// </summary>
        private static int CmdListTil(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli list-til <client_path>");
                Console.WriteLine();
                Console.WriteLine("讀取 Tile.pak 中的 list.til 並顯示內容");
                return 1;
            }

            string clientPath = args[0];

            if (!Directory.Exists(clientPath))
            {
                Console.WriteLine($"錯誤: 路徑不存在: {clientPath}");
                return 1;
            }

            string tileIdxPath = Path.Combine(clientPath, "Tile.idx");
            string tilePakPath = Path.Combine(clientPath, "Tile.pak");

            if (!File.Exists(tileIdxPath))
            {
                Console.WriteLine($"錯誤: Tile.idx 不存在: {tileIdxPath}");
                return 1;
            }

            if (!File.Exists(tilePakPath))
            {
                Console.WriteLine($"錯誤: Tile.pak 不存在: {tilePakPath}");
                return 1;
            }

            Console.WriteLine($"客戶端路徑: {clientPath}");
            Console.WriteLine($"Tile.idx: {tileIdxPath}");

            // 設定路徑
            Share.LineagePath = clientPath;

            // 清除快取
            if (Share.IdxDataList.ContainsKey("Tile"))
            {
                Share.IdxDataList.Remove("Tile");
            }

            // 讀取 list.til
            byte[] listTilData = L1PakReader.UnPack("Tile", "list.til");

            if (listTilData == null)
            {
                Console.WriteLine("錯誤: 找不到 list.til");
                return 1;
            }

            Console.WriteLine($"list.til 大小: {listTilData.Length} bytes");
            Console.WriteLine();

            // 解析 list.til - 通常是一系列的數字
            // 嘗試不同的解析方式
            Console.WriteLine("=== 原始資料 (前 100 bytes hex) ===");
            for (int i = 0; i < Math.Min(100, listTilData.Length); i++)
            {
                Console.Write($"{listTilData[i]:X2} ");
                if ((i + 1) % 16 == 0) Console.WriteLine();
            }
            Console.WriteLine();
            Console.WriteLine();

            // 嘗試解析為文字
            Console.WriteLine("=== 嘗試解析為文字 (前 500 字元) ===");
            try
            {
                string text = Encoding.Default.GetString(listTilData);
                Console.WriteLine(text.Substring(0, Math.Min(500, text.Length)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"無法解析為文字: {ex.Message}");
            }
            Console.WriteLine();

            // 嘗試解析為 int32 陣列
            Console.WriteLine("=== 嘗試解析為 Int32 陣列 (前 50 個) ===");
            if (listTilData.Length >= 4)
            {
                using (var br = new BinaryReader(new MemoryStream(listTilData)))
                {
                    int count = 0;
                    while (br.BaseStream.Position + 4 <= listTilData.Length && count < 50)
                    {
                        int value = br.ReadInt32();
                        Console.Write($"{value} ");
                        count++;
                        if (count % 10 == 0) Console.WriteLine();
                    }
                }
            }
            Console.WriteLine();

            // 嘗試解析為 int16 陣列
            Console.WriteLine();
            Console.WriteLine("=== 嘗試解析為 Int16 陣列 (前 50 個) ===");
            if (listTilData.Length >= 2)
            {
                using (var br = new BinaryReader(new MemoryStream(listTilData)))
                {
                    int count = 0;
                    while (br.BaseStream.Position + 2 <= listTilData.Length && count < 50)
                    {
                        short value = br.ReadInt16();
                        Console.Write($"{value} ");
                        count++;
                        if (count % 10 == 0) Console.WriteLine();
                    }
                }
            }
            Console.WriteLine();

            return 0;
        }

        /// <summary>
        /// validate-tiles 命令 - 驗證 S32 中使用的 TileId 是否存在於 Tile.idx 中
        /// </summary>
        private static int CmdValidateTiles(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli validate-tiles <s32檔案或地圖資料夾> [--client <路徑>]");
                Console.WriteLine();
                Console.WriteLine("驗證 S32 中使用的 TileId 是否存在於 Tile.idx 中");
                Console.WriteLine();
                Console.WriteLine("參數:");
                Console.WriteLine("  <s32檔案或地圖資料夾>  要驗證的 S32 檔案或包含 S32 的地圖資料夾");
                Console.WriteLine("  --client <路徑>        指定客戶端路徑（包含 Tile.idx/pak）");
                Console.WriteLine("                         若不指定，會自動從輸入路徑向上搜尋");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  validate-tiles C:\\client\\map\\4\\7fff8000.s32");
                Console.WriteLine("  validate-tiles C:\\client\\map\\4");
                Console.WriteLine("  validate-tiles C:\\map\\4 --client C:\\client");
                return 1;
            }

            string inputPath = args[0];
            string clientPath = null;

            // 解析參數
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--client" && i + 1 < args.Length)
                {
                    clientPath = args[++i];
                }
            }

            // 確定要處理的 S32 檔案列表
            var s32Files = new List<string>();

            if (File.Exists(inputPath) && inputPath.EndsWith(".s32", StringComparison.OrdinalIgnoreCase))
            {
                s32Files.Add(inputPath);
            }
            else if (Directory.Exists(inputPath))
            {
                s32Files.AddRange(Directory.GetFiles(inputPath, "*.s32"));
            }
            else
            {
                Console.WriteLine($"錯誤: 路徑不存在或不是有效的 S32 檔案/資料夾: {inputPath}");
                return 1;
            }

            if (s32Files.Count == 0)
            {
                Console.WriteLine($"錯誤: 找不到任何 S32 檔案: {inputPath}");
                return 1;
            }

            // 如果未指定 client 路徑，嘗試自動尋找
            if (string.IsNullOrEmpty(clientPath))
            {
                string searchPath = Directory.Exists(inputPath) ? inputPath : Path.GetDirectoryName(inputPath);
                while (!string.IsNullOrEmpty(searchPath))
                {
                    string tileIdxPath = Path.Combine(searchPath, "Tile.idx");
                    if (File.Exists(tileIdxPath))
                    {
                        clientPath = searchPath;
                        break;
                    }
                    searchPath = Path.GetDirectoryName(searchPath);
                }
            }

            if (string.IsNullOrEmpty(clientPath))
            {
                Console.WriteLine("錯誤: 找不到 Tile.idx，請使用 --client 參數指定客戶端路徑");
                return 1;
            }

            string tileIdx = Path.Combine(clientPath, "Tile.idx");
            string tilePak = Path.Combine(clientPath, "Tile.pak");

            if (!File.Exists(tileIdx) || !File.Exists(tilePak))
            {
                Console.WriteLine($"錯誤: Tile.idx 或 Tile.pak 不存在於: {clientPath}");
                return 1;
            }

            Console.WriteLine("=== Tile 驗證 ===");
            Console.WriteLine($"輸入路徑: {inputPath}");
            Console.WriteLine($"S32 檔案數: {s32Files.Count}");
            Console.WriteLine($"客戶端路徑: {clientPath}");
            Console.WriteLine();

            // 載入 Tile 索引
            Console.WriteLine("載入 Tile 索引...");
            var sw = Stopwatch.StartNew();

            Share.LineagePath = clientPath;

            // 清除快取以確保重新載入
            if (Share.IdxDataList.ContainsKey("Tile"))
            {
                Share.IdxDataList.Remove("Tile");
            }

            // 觸發載入 Tile 索引
            L1PakReader.UnPack("Tile", "1.til");

            // 從 Share.IdxDataList 取得所有可用的 TileId
            var availableTileIds = new HashSet<int>();
            if (Share.IdxDataList.TryGetValue("Tile", out var tileIdxData))
            {
                foreach (var key in tileIdxData.Keys)
                {
                    if (key.EndsWith(".til"))
                    {
                        string numStr = key.Substring(0, key.Length - 4);
                        if (int.TryParse(numStr, out int tileId))
                        {
                            availableTileIds.Add(tileId);
                        }
                    }
                }
            }

            sw.Stop();
            Console.WriteLine($"Tile 索引載入完成: {availableTileIds.Count} 個 Tile ({sw.ElapsedMilliseconds}ms)");
            Console.WriteLine();

            // 解析 S32 並驗證
            Console.WriteLine("解析 S32 並驗證 TileId 和 IndexId...");
            sw.Restart();

            // 快取每個 TileId 的 block 數量
            var tileBlockCounts = new Dictionary<int, int>();

            // 取得 Tile 的 block 數量
            int GetTileBlockCount(int tileId)
            {
                if (tileBlockCounts.TryGetValue(tileId, out int cached))
                    return cached;

                byte[] tilData = L1PakReader.UnPack("Tile", $"{tileId}.til");
                if (tilData == null || tilData.Length < 4)
                {
                    tileBlockCounts[tileId] = -1; // 標記為無效
                    return -1;
                }

                try
                {
                    using (var br = new BinaryReader(new MemoryStream(tilData)))
                    {
                        int blockCount = br.ReadInt32();
                        tileBlockCounts[tileId] = blockCount;
                        return blockCount;
                    }
                }
                catch
                {
                    tileBlockCounts[tileId] = -1;
                    return -1;
                }
            }

            var invalidTiles = new List<(string fileName, string layer, int x, int y, int tileId, int indexId, string reason)>();
            var uniqueInvalidTileIds = new HashSet<int>();
            var uniqueInvalidIndexIds = new HashSet<(int tileId, int indexId)>();
            int totalLayer1Cells = 0;
            int totalLayer2Items = 0;
            int totalLayer4Items = 0;

            // 驗證 TileId 和 IndexId
            void ValidateTile(string fileName, string layer, int x, int y, int tileId, int indexId)
            {
                if (tileId <= 0) return;

                // 檢查 TileId 是否存在
                if (!availableTileIds.Contains(tileId))
                {
                    invalidTiles.Add((fileName, layer, x, y, tileId, indexId, "Tile不存在"));
                    uniqueInvalidTileIds.Add(tileId);
                    return;
                }

                // 檢查 IndexId 是否有效
                int blockCount = GetTileBlockCount(tileId);
                if (blockCount < 0)
                {
                    invalidTiles.Add((fileName, layer, x, y, tileId, indexId, "Tile無法讀取"));
                    uniqueInvalidTileIds.Add(tileId);
                    return;
                }

                if (indexId < 0 || indexId >= blockCount)
                {
                    invalidTiles.Add((fileName, layer, x, y, tileId, indexId, $"IndexId超出範圍(0-{blockCount - 1})"));
                    uniqueInvalidIndexIds.Add((tileId, indexId));
                }
            }

            foreach (var filePath in s32Files)
            {
                string fileName = Path.GetFileName(filePath);
                S32Data s32Data;

                try
                {
                    s32Data = S32Parser.ParseFile(filePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"警告: 無法解析 {fileName}: {ex.Message}");
                    continue;
                }

                // 檢查 Layer1（地板）
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        var cell = s32Data.Layer1[y, x];
                        if (cell != null && cell.TileId > 0)
                        {
                            totalLayer1Cells++;
                            ValidateTile(fileName, "L1", x, y, cell.TileId, cell.IndexId);
                        }
                    }
                }

                // 檢查 Layer2
                foreach (var item in s32Data.Layer2)
                {
                    if (item.TileId > 0)
                    {
                        totalLayer2Items++;
                        ValidateTile(fileName, "L2", item.X, item.Y, item.TileId, item.IndexId);
                    }
                }

                // 檢查 Layer4（物件）
                foreach (var item in s32Data.Layer4)
                {
                    if (item.TileId > 0)
                    {
                        totalLayer4Items++;
                        ValidateTile(fileName, "L4", item.X, item.Y, item.TileId, item.IndexId);
                    }
                }
            }

            sw.Stop();

            Console.WriteLine($"驗證完成 ({sw.ElapsedMilliseconds}ms)");
            Console.WriteLine($"已檢查 {tileBlockCounts.Count} 個不同的 Tile");
            Console.WriteLine();
            Console.WriteLine("=== 統計 ===");
            Console.WriteLine($"Layer1 格子數: {totalLayer1Cells:N0}");
            Console.WriteLine($"Layer2 項目數: {totalLayer2Items:N0}");
            Console.WriteLine($"Layer4 物件數: {totalLayer4Items:N0}");
            Console.WriteLine();

            if (invalidTiles.Count == 0)
            {
                Console.WriteLine("結果: 所有 TileId 和 IndexId 都有效，沒有發現問題。");
                return 0;
            }

            // 輸出無效的項目
            int tileNotExistCount = invalidTiles.Count(t => t.reason == "Tile不存在" || t.reason == "Tile無法讀取");
            int indexOutOfRangeCount = invalidTiles.Count(t => t.reason.StartsWith("IndexId超出範圍"));

            Console.WriteLine($"=== 發現 {invalidTiles.Count} 個問題 ===");
            if (uniqueInvalidTileIds.Count > 0)
                Console.WriteLine($"  - Tile不存在: {tileNotExistCount} 個引用 (涉及 {uniqueInvalidTileIds.Count} 個 TileId)");
            if (uniqueInvalidIndexIds.Count > 0)
                Console.WriteLine($"  - IndexId超出範圍: {indexOutOfRangeCount} 個引用 (涉及 {uniqueInvalidIndexIds.Count} 個 TileId+IndexId 組合)");
            Console.WriteLine();

            // 按問題類型和 TileId 分組顯示
            if (uniqueInvalidTileIds.Count > 0)
            {
                Console.WriteLine("不存在的 TileId:");
                var notExistTiles = invalidTiles.Where(t => t.reason == "Tile不存在" || t.reason == "Tile無法讀取")
                    .GroupBy(t => t.tileId).OrderBy(g => g.Key);
                foreach (var group in notExistTiles)
                {
                    int tileId = group.Key;
                    int count = group.Count();
                    var layers = group.Select(g => g.layer).Distinct().OrderBy(l => l);
                    var files = group.Select(g => g.fileName).Distinct().Take(3);
                    Console.WriteLine($"  TileId {tileId}: {count} 個引用 (Layer: {string.Join(",", layers)}) - 檔案: {string.Join(", ", files)}{(group.Select(g => g.fileName).Distinct().Count() > 3 ? "..." : "")}");
                }
                Console.WriteLine();
            }

            if (uniqueInvalidIndexIds.Count > 0)
            {
                Console.WriteLine("IndexId 超出範圍:");
                var outOfRangeItems = invalidTiles.Where(t => t.reason.StartsWith("IndexId超出範圍"))
                    .GroupBy(t => (t.tileId, t.indexId)).OrderBy(g => g.Key.tileId).ThenBy(g => g.Key.indexId);
                foreach (var group in outOfRangeItems.Take(30))
                {
                    int tileId = group.Key.tileId;
                    int indexId = group.Key.indexId;
                    int blockCount = tileBlockCounts.TryGetValue(tileId, out var bc) ? bc : -1;
                    int count = group.Count();
                    var layers = group.Select(g => g.layer).Distinct().OrderBy(l => l);
                    Console.WriteLine($"  TileId {tileId}, IndexId {indexId} (最大={blockCount - 1}): {count} 個引用 (Layer: {string.Join(",", layers)})");
                }
                if (uniqueInvalidIndexIds.Count > 30)
                {
                    Console.WriteLine($"  ... 還有 {uniqueInvalidIndexIds.Count - 30} 個組合");
                }
                Console.WriteLine();
            }

            Console.WriteLine("詳細清單 (前 50 筆):");
            Console.WriteLine("檔案名稱                  Layer  X      Y      TileId   IndexId  原因");
            Console.WriteLine("──────────────────────────────────────────────────────────────────────────────");

            foreach (var item in invalidTiles.Take(50))
            {
                Console.WriteLine($"{item.fileName,-25} {item.layer,-6} {item.x,-6} {item.y,-6} {item.tileId,-8} {item.indexId,-8} {item.reason}");
            }

            if (invalidTiles.Count > 50)
            {
                Console.WriteLine($"... 還有 {invalidTiles.Count - 50} 筆");
            }

            return invalidTiles.Count > 0 ? 1 : 0;
        }

        /// <summary>
        /// export-fs32 命令 - 匯出地圖為 fs32 格式
        /// </summary>
        private static int CmdExportFs32(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法: -cli export-fs32 <地圖資料夾> <輸出.fs32> [--downscale]");
                Console.WriteLine();
                Console.WriteLine("將地圖匯出為 fs32 格式");
                Console.WriteLine();
                Console.WriteLine("參數:");
                Console.WriteLine("  <地圖資料夾>    包含 S32 檔案的地圖資料夾");
                Console.WriteLine("  <輸出.fs32>     輸出的 fs32 檔案路徑");
                Console.WriteLine("  --downscale     將 Remaster 版 Tile (48x48) 降級為 Classic 版 (24x24)");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  export-fs32 C:\\client\\map\\100002 C:\\output\\100002.fs32");
                Console.WriteLine("  export-fs32 C:\\client\\map\\100002 C:\\output\\100002.fs32 --downscale");
                return 1;
            }

            string mapPath = args[0];
            string outputPath = args[1];
            bool downscale = args.Contains("--downscale");

            if (!Directory.Exists(mapPath))
            {
                Console.WriteLine($"錯誤: 地圖資料夾不存在: {mapPath}");
                return 1;
            }

            var s32Files = Directory.GetFiles(mapPath, "*.s32");
            if (s32Files.Length == 0)
            {
                Console.WriteLine($"錯誤: 找不到任何 S32 檔案: {mapPath}");
                return 1;
            }

            // 確保輸出路徑有 .fs32 副檔名
            if (!outputPath.EndsWith(".fs32", StringComparison.OrdinalIgnoreCase))
            {
                outputPath += ".fs32";
            }

            // 嘗試找到 client 路徑
            string clientPath = mapPath;
            while (!string.IsNullOrEmpty(clientPath))
            {
                string tileIdxPath = Path.Combine(clientPath, "Tile.idx");
                if (File.Exists(tileIdxPath))
                {
                    Share.LineagePath = clientPath;
                    break;
                }
                clientPath = Path.GetDirectoryName(clientPath);
            }

            if (string.IsNullOrEmpty(Share.LineagePath))
            {
                Console.WriteLine("警告: 找不到 Tile.idx，將不會打包 Tile 檔案");
            }

            string mapId = Path.GetFileName(mapPath);

            Console.WriteLine("=== 匯出 fs32 ===");
            Console.WriteLine($"地圖資料夾: {mapPath}");
            Console.WriteLine($"地圖 ID: {mapId}");
            Console.WriteLine($"S32 檔案數: {s32Files.Length}");
            Console.WriteLine($"輸出路徑: {outputPath}");
            Console.WriteLine($"Tile 降級: {(downscale ? "是" : "否")}");
            Console.WriteLine();

            var sw = Stopwatch.StartNew();

            // 解析所有 S32 檔案
            Console.WriteLine("解析 S32 檔案...");
            var s32DataList = new List<S32Data>();
            foreach (var filePath in s32Files)
            {
                try
                {
                    var s32Data = S32Parser.ParseFile(filePath);
                    s32DataList.Add(s32Data);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"警告: 無法解析 {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }
            Console.WriteLine($"成功解析 {s32DataList.Count} 個 S32 檔案");

            // 建立 fs32
            Console.WriteLine("建立 fs32 資料...");
            var fs32 = new Fs32Data
            {
                Mode = Fs32Mode.WholeMap,
                SourceMapId = mapId,
                LayerFlags = 0xFF
            };

            // 收集所有使用的 TileIds
            var allTileIds = new HashSet<int>();

            foreach (var s32Data in s32DataList)
            {
                if (s32Data == null) continue;

                // 從檔名推算 BlockX/BlockY（如果 SegInfo 不存在）
                int blockX, blockY;
                if (s32Data.SegInfo != null)
                {
                    blockX = s32Data.SegInfo.nBlockX;
                    blockY = s32Data.SegInfo.nBlockY;
                }
                else if (!string.IsNullOrEmpty(s32Data.FilePath))
                {
                    // 從檔名解析，格式如 "80128008.s32" -> BlockX=0x8012, BlockY=0x8008
                    string fileName = Path.GetFileNameWithoutExtension(s32Data.FilePath);
                    if (fileName.Length == 8 &&
                        int.TryParse(fileName.Substring(0, 4), System.Globalization.NumberStyles.HexNumber, null, out blockX) &&
                        int.TryParse(fileName.Substring(4, 4), System.Globalization.NumberStyles.HexNumber, null, out blockY))
                    {
                        // 成功解析
                    }
                    else
                    {
                        Console.WriteLine($"警告: 無法從檔名解析區塊座標: {fileName}");
                        continue;
                    }
                }
                else
                {
                    Console.WriteLine("警告: S32 缺少 SegInfo 且無檔案路徑，跳過");
                    continue;
                }

                // 加入區塊
                var block = new Fs32Block
                {
                    BlockX = blockX,
                    BlockY = blockY,
                    S32Data = s32Data.OriginalFileData ?? S32Writer.ToBytes(s32Data)
                };
                fs32.Blocks.Add(block);

                // 收集 TileIds
                if (s32Data.Layer1 != null)
                {
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 128; x++)
                        {
                            var cell = s32Data.Layer1[y, x];
                            if (cell?.TileId > 0)
                                allTileIds.Add(cell.TileId);
                        }
                    }
                }

                if (s32Data.Layer2 != null)
                {
                    foreach (var item in s32Data.Layer2)
                    {
                        if (item.TileId > 0)
                            allTileIds.Add(item.TileId);
                    }
                }

                if (s32Data.Layer4 != null)
                {
                    foreach (var obj in s32Data.Layer4)
                    {
                        if (obj.TileId > 0)
                            allTileIds.Add(obj.TileId);
                    }
                }
            }

            Console.WriteLine($"區塊數量: {fs32.Blocks.Count}");
            Console.WriteLine($"使用的 TileId 數量: {allTileIds.Count}");

            // 打包 Tiles
            if (!string.IsNullOrEmpty(Share.LineagePath))
            {
                Console.WriteLine("打包 Tile 檔案...");
                int tileCount = 0;
                int downscaledCount = 0;

                foreach (int tileId in allTileIds)
                {
                    byte[] tilData = L1PakReader.UnPack("Tile", $"{tileId}.til");
                    if (tilData != null)
                    {
                        // 如果需要降級
                        if (downscale && L1Til.IsRemaster(tilData))
                        {
                            tilData = L1Til.DownscaleTil(tilData);
                            downscaledCount++;
                        }

                        fs32.Tiles[tileId] = new TilePackageData
                        {
                            OriginalTileId = tileId,
                            Md5Hash = TileHashManager.CalculateMd5(tilData),
                            TilData = tilData
                        };
                        tileCount++;
                    }
                }

                Console.WriteLine($"已打包 {tileCount} 個 Tile");
                if (downscale)
                {
                    Console.WriteLine($"已降級 {downscaledCount} 個 Remaster Tile");
                }
            }

            // 寫入 fs32
            Console.WriteLine("寫入 fs32 檔案...");
            Fs32Writer.Write(fs32, outputPath);

            sw.Stop();

            var fileInfo = new FileInfo(outputPath);
            Console.WriteLine();
            Console.WriteLine("=== 完成 ===");
            Console.WriteLine($"輸出檔案: {outputPath}");
            Console.WriteLine($"檔案大小: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");
            Console.WriteLine($"耗時: {sw.ElapsedMilliseconds}ms");

            return 0;
        }

        /// <summary>
        /// import-fs32 命令 - 匯入 fs32 到指定地圖
        /// </summary>
        private static int CmdImportFs32(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法: -cli import-fs32 <fs32檔案> <目標地圖資料夾> [--replace]");
                Console.WriteLine();
                Console.WriteLine("將 fs32 匯入到指定地圖");
                Console.WriteLine();
                Console.WriteLine("參數:");
                Console.WriteLine("  <fs32檔案>        要匯入的 fs32 檔案");
                Console.WriteLine("  <目標地圖資料夾>  目標地圖資料夾（會自動建立）");
                Console.WriteLine("  --replace         全部取代模式（刪除既有的 S32 檔案）");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  import-fs32 C:\\100002.fs32 C:\\client\\map\\100003");
                Console.WriteLine("  import-fs32 C:\\100002.fs32 C:\\client\\map\\100003 --replace");
                return 1;
            }

            string fs32Path = args[0];
            string targetMapPath = args[1];
            bool replaceMode = args.Contains("--replace");

            if (!File.Exists(fs32Path))
            {
                Console.WriteLine($"錯誤: fs32 檔案不存在: {fs32Path}");
                return 1;
            }

            // 嘗試找到 client 路徑
            string clientPath = targetMapPath;
            while (!string.IsNullOrEmpty(clientPath))
            {
                string tileIdxPath = Path.Combine(clientPath, "Tile.idx");
                if (File.Exists(tileIdxPath))
                {
                    Share.LineagePath = clientPath;
                    break;
                }
                clientPath = Path.GetDirectoryName(clientPath);
            }

            Console.WriteLine("=== 匯入 fs32 ===");
            Console.WriteLine($"fs32 檔案: {fs32Path}");
            Console.WriteLine($"目標資料夾: {targetMapPath}");
            Console.WriteLine($"匯入模式: {(replaceMode ? "全部取代" : "覆蓋/新增")}");
            if (!string.IsNullOrEmpty(Share.LineagePath))
            {
                Console.WriteLine($"Client 路徑: {Share.LineagePath}");
            }
            Console.WriteLine();

            var sw = Stopwatch.StartNew();

            // 載入 fs32
            Console.WriteLine("載入 fs32...");
            var fs32 = Fs32Parser.ParseFile(fs32Path);
            if (fs32 == null || fs32.Blocks.Count == 0)
            {
                Console.WriteLine("錯誤: 無效的 fs32 檔案或不包含任何區塊");
                return 1;
            }

            Console.WriteLine($"來源地圖: {fs32.SourceMapId}");
            Console.WriteLine($"區塊數量: {fs32.Blocks.Count}");
            Console.WriteLine($"Tile 數量: {fs32.Tiles.Count}");
            Console.WriteLine();

            // 確保目標資料夾存在
            if (!Directory.Exists(targetMapPath))
            {
                Console.WriteLine($"建立目標資料夾: {targetMapPath}");
                Directory.CreateDirectory(targetMapPath);
            }

            // 全部取代模式：刪除既有 S32 檔案
            int deletedCount = 0;
            if (replaceMode)
            {
                Console.WriteLine("刪除既有 S32 檔案...");
                var existingS32Files = Directory.GetFiles(targetMapPath, "*.s32");
                foreach (var file in existingS32Files)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"警告: 無法刪除 {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
                Console.WriteLine($"已刪除 {deletedCount} 個既有 S32 檔案");
            }

            // 處理 Tiles（如果有）
            if (fs32.Tiles.Count > 0 && !string.IsNullOrEmpty(Share.LineagePath))
            {
                Console.WriteLine("處理 Tile 檔案...");
                int importedTiles = 0;
                int skippedTiles = 0;

                foreach (var tile in fs32.Tiles.Values)
                {
                    // 檢查是否已存在相同的 Tile
                    byte[] existingTilData = L1PakReader.UnPack("Tile", $"{tile.OriginalTileId}.til");
                    if (existingTilData != null)
                    {
                        byte[] existingMd5 = TileHashManager.CalculateMd5(existingTilData);
                        if (TileHashManager.CompareMd5(existingMd5, tile.Md5Hash))
                        {
                            // MD5 相同，跳過
                            skippedTiles++;
                            continue;
                        }
                    }

                    // 寫入新 Tile
                    try
                    {
                        L1PakWriter.UpdateFile("Tile", $"{tile.OriginalTileId}.til", tile.TilData);
                        importedTiles++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"警告: 無法寫入 Tile {tile.OriginalTileId}: {ex.Message}");
                    }
                }

                Console.WriteLine($"已匯入 {importedTiles} 個新 Tile，跳過 {skippedTiles} 個重複 Tile");
            }

            // 寫入 S32 區塊
            Console.WriteLine("寫入 S32 區塊...");
            int importedBlocks = 0;
            int errorBlocks = 0;

            // 取得目標 MapId
            string targetMapId = Path.GetFileName(targetMapPath);

            // 嘗試讀取現有 S32 來取得 MapInfo
            int mapMinBlockX = 0x7FFF;
            int mapMinBlockY = 0x7FFF;
            int mapBlockCountX = 1;

            var existingS32 = Directory.GetFiles(targetMapPath, "*.s32").FirstOrDefault();
            if (existingS32 != null)
            {
                try
                {
                    var firstS32 = S32Parser.ParseFile(existingS32);
                    if (firstS32?.SegInfo != null)
                    {
                        mapMinBlockX = firstS32.SegInfo.nMapMinBlockX;
                        mapMinBlockY = firstS32.SegInfo.nMapMinBlockY;
                        mapBlockCountX = firstS32.SegInfo.nMapBlockCountX;
                    }
                }
                catch { }
            }

            // 如果是全新的地圖，從 fs32 區塊推算 MapInfo
            if (mapMinBlockX == 0x7FFF && fs32.Blocks.Count > 0)
            {
                mapMinBlockX = fs32.Blocks.Min(b => b.BlockX);
                mapMinBlockY = fs32.Blocks.Min(b => b.BlockY);
                int maxBlockX = fs32.Blocks.Max(b => b.BlockX);
                mapBlockCountX = maxBlockX - mapMinBlockX + 1;
            }

            foreach (var block in fs32.Blocks)
            {
                try
                {
                    // 解析 S32 資料
                    var s32Data = S32Parser.Parse(block.S32Data);
                    if (s32Data == null)
                    {
                        Console.WriteLine($"警告: 無法解析區塊 {block.BlockX:X4}{block.BlockY:X4}");
                        errorBlocks++;
                        continue;
                    }

                    // 確保 SegInfo 存在
                    if (s32Data.SegInfo == null)
                    {
                        s32Data.SegInfo = new L1MapSeg(block.BlockX, block.BlockY, true);
                    }
                    else
                    {
                        s32Data.SegInfo.nBlockX = block.BlockX;
                        s32Data.SegInfo.nBlockY = block.BlockY;
                    }

                    // 更新地圖資訊
                    s32Data.SegInfo.nMapMinBlockX = mapMinBlockX;
                    s32Data.SegInfo.nMapMinBlockY = mapMinBlockY;
                    s32Data.SegInfo.nMapBlockCountX = mapBlockCountX;

                    // 計算區塊在地圖中的索引
                    int relBlockX = block.BlockX - mapMinBlockX;
                    int relBlockY = block.BlockY - mapMinBlockY;

                    // 計算遊戲座標範圍
                    s32Data.SegInfo.nLinBeginX = (relBlockX + relBlockY) * 64;
                    s32Data.SegInfo.nLinBeginY = relBlockY * 64 - relBlockX * 64 + (mapBlockCountX - 1) * 64;
                    s32Data.SegInfo.nLinEndX = s32Data.SegInfo.nLinBeginX + 64;
                    s32Data.SegInfo.nLinEndY = s32Data.SegInfo.nLinBeginY + 64;

                    // 寫入 S32 檔案
                    string s32FileName = $"{block.BlockX:x4}{block.BlockY:x4}.s32";
                    string s32FilePath = Path.Combine(targetMapPath, s32FileName);

                    byte[] s32Bytes = S32Writer.ToBytes(s32Data);
                    File.WriteAllBytes(s32FilePath, s32Bytes);

                    importedBlocks++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"錯誤: 區塊 {block.BlockX:X4}{block.BlockY:X4}: {ex.Message}");
                    errorBlocks++;
                }
            }

            sw.Stop();

            Console.WriteLine();
            Console.WriteLine("=== 完成 ===");
            Console.WriteLine($"成功匯入 {importedBlocks} 個區塊");
            if (errorBlocks > 0)
            {
                Console.WriteLine($"失敗 {errorBlocks} 個區塊");
            }
            Console.WriteLine($"耗時: {sw.ElapsedMilliseconds}ms");

            return errorBlocks > 0 ? 1 : 0;
        }

        /// <summary>
        /// info 命令 - 顯示基本資訊
        /// </summary>
        private static int CmdInfo(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli info <s32檔案>");
                return 1;
            }

            string filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"檔案不存在: {filePath}");
                return 1;
            }

            var s32 = S32Parser.ParseFile(filePath);
            var fileInfo = new FileInfo(filePath);

            Console.WriteLine($"=== S32 檔案資訊 ===");
            Console.WriteLine($"檔案名稱: {fileInfo.Name}");
            Console.WriteLine($"檔案大小: {fileInfo.Length:N0} bytes");
            Console.WriteLine($"修改時間: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
            Console.WriteLine($"=== 各層資料統計 ===");
            Console.WriteLine($"Layer1 (地板): 64x128 = 8192 格");
            Console.WriteLine($"Layer2 (資料): {s32.Layer2.Count} 項");
            Console.WriteLine($"Layer3 (屬性): 64x64 = 4096 格");
            Console.WriteLine($"Layer4 (物件): {s32.Layer4.Count} 個");
            Console.WriteLine($"Layer5 (透明): {s32.Layer5.Count} 項");
            Console.WriteLine($"Layer6 (Tiles): {s32.Layer6.Count} 種");
            Console.WriteLine($"Layer7 (傳送): {s32.Layer7.Count} 個");
            Console.WriteLine($"Layer8 (特效): {s32.Layer8.Count} 個");
            Console.WriteLine();
            Console.WriteLine($"使用的不重複 TileId: {s32.UsedTiles.Count} 種");

            return 0;
        }

        /// <summary>
        /// layers 命令 - 顯示各層統計
        /// </summary>
        private static int CmdLayers(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli layers <s32檔案>");
                return 1;
            }

            string filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"檔案不存在: {filePath}");
                return 1;
            }

            var s32 = S32Parser.ParseFile(filePath);

            Console.WriteLine($"=== 各層偏移量 ===");
            Console.WriteLine($"Layer1 Offset: 0x{s32.Layer1Offset:X8} ({s32.Layer1Offset})");
            Console.WriteLine($"Layer2 Offset: 0x{s32.Layer2Offset:X8} ({s32.Layer2Offset})");
            Console.WriteLine($"Layer3 Offset: 0x{s32.Layer3Offset:X8} ({s32.Layer3Offset})");
            Console.WriteLine($"Layer4 Offset: 0x{s32.Layer4Offset:X8} ({s32.Layer4Offset})");
            Console.WriteLine($"Layer4 End:    0x{s32.Layer4EndOffset:X8} ({s32.Layer4EndOffset})");
            Console.WriteLine($"Layer5-8 Size: {s32.Layer5to8Data?.Length ?? 0} bytes");

            // 統計 Layer4 群組
            var groupStats = s32.Layer4.GroupBy(o => o.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .OrderBy(g => g.GroupId)
                .ToList();

            Console.WriteLine();
            Console.WriteLine($"=== Layer4 群組統計 ===");
            Console.WriteLine($"總群組數: {groupStats.Count}");
            if (groupStats.Count > 0)
            {
                Console.WriteLine($"群組 ID 範圍: {groupStats.Min(g => g.GroupId)} ~ {groupStats.Max(g => g.GroupId)}");
            }

            return 0;
        }

        /// <summary>
        /// l1 命令 - 顯示第一層資訊
        /// </summary>
        private static int CmdLayer1(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli l1 <s32檔案> [--tiles]");
                return 1;
            }

            string filePath = args[0];
            bool showTiles = args.Contains("--tiles");

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"檔案不存在: {filePath}");
                return 1;
            }

            var s32 = S32Parser.ParseFile(filePath);

            Console.WriteLine($"=== Layer1 (地板) ===");
            Console.WriteLine($"大小: 64 x 128 = 8192 格");

            // 統計使用的 TileId
            var tileStats = new Dictionary<int, int>();
            int emptyCount = 0;
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    var cell = s32.Layer1[y, x];
                    if (cell == null || cell.TileId == 0)
                    {
                        emptyCount++;
                    }
                    else
                    {
                        if (!tileStats.ContainsKey(cell.TileId))
                            tileStats[cell.TileId] = 0;
                        tileStats[cell.TileId]++;
                    }
                }
            }

            Console.WriteLine($"空白格子: {emptyCount}");
            Console.WriteLine($"有資料格子: {8192 - emptyCount}");
            Console.WriteLine($"使用的 TileId 種類: {tileStats.Count}");

            if (showTiles && tileStats.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("TileId 使用次數:");
                foreach (var kv in tileStats.OrderByDescending(k => k.Value).Take(20))
                {
                    Console.WriteLine($"  TileId {kv.Key}: {kv.Value} 次");
                }
                if (tileStats.Count > 20)
                    Console.WriteLine($"  ... (還有 {tileStats.Count - 20} 種)");
            }

            return 0;
        }

        /// <summary>
        /// l3 命令 - 顯示第三層資訊
        /// </summary>
        private static int CmdLayer3(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli l3 <s32檔案> [x] [y]");
                return 1;
            }

            string filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"檔案不存在: {filePath}");
                return 1;
            }

            var s32 = S32Parser.ParseFile(filePath);

            // 如果指定了座標，顯示特定格子
            if (args.Length >= 3 && int.TryParse(args[1], out int x) && int.TryParse(args[2], out int y))
            {
                if (x < 0 || x >= 64 || y < 0 || y >= 64)
                {
                    Console.WriteLine("座標超出範圍 (0-63)");
                    return 1;
                }

                var attr = s32.Layer3[y, x];
                Console.WriteLine($"Layer3[{x},{y}]:");
                Console.WriteLine($"  Attribute1: {attr.Attribute1} (0x{attr.Attribute1:X4})");
                Console.WriteLine($"  Attribute2: {attr.Attribute2} (0x{attr.Attribute2:X4})");
                Console.WriteLine($"  可通行(左上): {((attr.Attribute1 & 0x01) == 0 ? "是" : "否")}");
                Console.WriteLine($"  可通行(右上): {((attr.Attribute2 & 0x01) == 0 ? "是" : "否")}");
                return 0;
            }

            // 統計
            Console.WriteLine($"=== Layer3 (屬性) ===");
            Console.WriteLine($"大小: 64 x 64 = 4096 格");

            int passable = 0, blocked = 0;
            for (int ly = 0; ly < 64; ly++)
            {
                for (int lx = 0; lx < 64; lx++)
                {
                    var attr = s32.Layer3[ly, lx];
                    if ((attr.Attribute1 & 0x01) == 0)
                        passable++;
                    else
                        blocked++;
                }
            }

            Console.WriteLine($"可通行(左上): {passable} 格");
            Console.WriteLine($"不可通行(左上): {blocked} 格");

            return 0;
        }

        /// <summary>
        /// l4 命令 - 顯示第四層資訊
        /// </summary>
        private static int CmdLayer4(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli l4 <s32檔案> [--groups] [--all]");
                return 1;
            }

            string filePath = args[0];
            bool showGroups = args.Contains("--groups");
            bool showAll = args.Contains("--all");

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"檔案不存在: {filePath}");
                return 1;
            }

            var s32 = S32Parser.ParseFile(filePath);

            Console.WriteLine($"=== Layer4 (物件) ===");
            Console.WriteLine($"總物件數: {s32.Layer4.Count}");

            var groupStats = s32.Layer4.GroupBy(o => o.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .OrderBy(g => g.GroupId)
                .ToList();

            Console.WriteLine($"群組數: {groupStats.Count}");

            if (showGroups && groupStats.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("群組詳情:");
                foreach (var g in groupStats)
                {
                    Console.WriteLine($"  GroupId {g.GroupId}: {g.Count} 個物件");
                }
            }

            // 顯示所有物件詳細資訊
            if (showAll && s32.Layer4.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("所有物件:");
                for (int i = 0; i < s32.Layer4.Count; i++)
                {
                    var obj = s32.Layer4[i];
                    Console.WriteLine($"  [{i}] GroupId={obj.GroupId}, X={obj.X}, Y={obj.Y}, Layer={obj.Layer}, IndexId={obj.IndexId}, TileId={obj.TileId}");
                }
            }

            return 0;
        }

        /// <summary>
        /// l5 命令 - 顯示第五層資訊
        /// </summary>
        private static int CmdLayer5(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli l5 <s32檔案>");
                return 1;
            }

            string filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"檔案不存在: {filePath}");
                return 1;
            }

            var s32 = S32Parser.ParseFile(filePath);

            Console.WriteLine($"=== Layer5 (透明圖塊) ===");
            Console.WriteLine($"數量: {s32.Layer5.Count}");

            if (s32.Layer5.Count > 0)
            {
                Console.WriteLine();
                foreach (var item in s32.Layer5)
                {
                    Console.WriteLine($"  X={item.X}, Y={item.Y}, ObjectIndex={item.ObjectIndex}, Type={item.Type}");
                }
            }

            return 0;
        }

        /// <summary>
        /// l5check 命令 - 檢查 Layer5 異常
        /// </summary>
        private static int CmdLayer5Check(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli l5check <s32檔案或資料夾> [-r N]");
                Console.WriteLine("  檢查 Layer5 的 ObjectIndex 是否有對應的 Layer4 GroupId");
                Console.WriteLine("  以及該格周圍 N 格內是否有對應 GroupId 的物件");
                Console.WriteLine("  -r N: 檢查半徑，預設為 1（周圍一格）");
                return 1;
            }

            string path = args[0];
            int radius = 1;

            // 解析 -r 參數
            for (int i = 1; i < args.Length - 1; i++)
            {
                if (args[i] == "-r" && int.TryParse(args[i + 1], out int r))
                {
                    radius = r;
                    break;
                }
            }

            List<string> s32FilePaths = new List<string>();

            if (File.Exists(path))
            {
                s32FilePaths.Add(path);
            }
            else if (Directory.Exists(path))
            {
                s32FilePaths.AddRange(Directory.GetFiles(path, "*.s32"));
            }
            else
            {
                Console.WriteLine($"檔案或資料夾不存在: {path}");
                return 1;
            }

            if (s32FilePaths.Count == 0)
            {
                Console.WriteLine($"找不到 S32 檔案");
                return 1;
            }

            Console.WriteLine($"=== Layer5 異常檢查 ===");
            Console.WriteLine($"檔案數: {s32FilePaths.Count}");
            Console.WriteLine($"檢查半徑: {radius} 格");
            Console.WriteLine();

            // 載入所有 S32 檔案
            var allS32 = new Dictionary<string, S32Data>();
            foreach (var file in s32FilePaths)
            {
                allS32[file] = S32Parser.ParseFile(file);
            }

            // 使用共用檢查邏輯
            var invalidItems = Layer5Checker.Check(allS32, radius);

            // 按檔案分組輸出
            var groupedByFile = invalidItems.GroupBy(x => x.FilePath);
            foreach (var group in groupedByFile)
            {
                Console.WriteLine($"[{Path.GetFileName(group.Key)}] 發現 {group.Count()} 個異常:");
                foreach (var result in group)
                {
                    Console.WriteLine($"  [{result.ItemIndex}] X={result.Item.X}, Y={result.Item.Y}, ObjIdx={result.Item.ObjectIndex}, Type={result.Item.Type} - {result.Reason}");
                }
                Console.WriteLine();
            }

            if (invalidItems.Count == 0)
            {
                Console.WriteLine("沒有發現 Layer5 異常！");
            }
            else
            {
                Console.WriteLine($"總計: {invalidItems.Count} 個異常");
            }

            return invalidItems.Count > 0 ? 1 : 0;
        }

        /// <summary>
        /// l6 命令 - 顯示第六層資訊
        /// </summary>
        private static int CmdLayer6(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli l6 <s32檔案>");
                return 1;
            }

            string filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"檔案不存在: {filePath}");
                return 1;
            }

            var s32 = S32Parser.ParseFile(filePath);

            Console.WriteLine($"=== Layer6 (使用的 TileId) ===");
            Console.WriteLine($"數量: {s32.Layer6.Count}");

            if (s32.Layer6.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("TileId 列表:");
                for (int i = 0; i < s32.Layer6.Count; i++)
                {
                    Console.Write($"{s32.Layer6[i],6}");
                    if ((i + 1) % 10 == 0) Console.WriteLine();
                }
                if (s32.Layer6.Count % 10 != 0) Console.WriteLine();
            }

            return 0;
        }

        /// <summary>
        /// l7 命令 - 顯示第七層資訊
        /// </summary>
        private static int CmdLayer7(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli l7 <s32檔案>");
                return 1;
            }

            string filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"檔案不存在: {filePath}");
                return 1;
            }

            var s32 = S32Parser.ParseFile(filePath);

            Console.WriteLine($"=== Layer7 (傳送點) ===");
            Console.WriteLine($"數量: {s32.Layer7.Count}");

            if (s32.Layer7.Count > 0)
            {
                Console.WriteLine();
                foreach (var item in s32.Layer7)
                {
                    Console.WriteLine($"  [{item.Name}]");
                    Console.WriteLine($"    位置: ({item.X}, {item.Y})");
                    Console.WriteLine($"    目標地圖: {item.TargetMapId}");
                    Console.WriteLine($"    PortalId: {item.PortalId}");
                }
            }

            return 0;
        }

        /// <summary>
        /// l8 命令 - 顯示第八層資訊
        /// </summary>
        private static int CmdLayer8(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli l8 <s32檔案>");
                return 1;
            }

            string filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"檔案不存在: {filePath}");
                return 1;
            }

            var s32 = S32Parser.ParseFile(filePath);

            Console.WriteLine($"=== Layer8 (特效) ===");
            Console.WriteLine($"數量: {s32.Layer8.Count}");

            if (s32.Layer8.Count > 0)
            {
                Console.WriteLine();
                foreach (var item in s32.Layer8)
                {
                    Console.WriteLine($"  SprId={item.SprId}, X={item.X}, Y={item.Y}, ExtendedData={item.ExtendedData}");
                }
            }

            return 0;
        }

        /// <summary>
        /// export 命令 - 匯出為 JSON
        /// </summary>
        private static int CmdExport(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法: -cli export <s32檔案> <輸出檔>");
                return 1;
            }

            string filePath = args[0];
            string outputPath = args[1];

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"檔案不存在: {filePath}");
                return 1;
            }

            var s32 = S32Parser.ParseFile(filePath);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"file\": \"{Path.GetFileName(filePath).Replace("\\", "\\\\")}\",");
            sb.AppendLine($"  \"layer1_count\": 8192,");
            sb.AppendLine($"  \"layer2_count\": {s32.Layer2.Count},");
            sb.AppendLine($"  \"layer3_count\": 4096,");
            sb.AppendLine($"  \"layer4_count\": {s32.Layer4.Count},");
            sb.AppendLine($"  \"layer5_count\": {s32.Layer5.Count},");
            sb.AppendLine($"  \"layer6_count\": {s32.Layer6.Count},");
            sb.AppendLine($"  \"layer7_count\": {s32.Layer7.Count},");
            sb.AppendLine($"  \"layer8_count\": {s32.Layer8.Count},");

            // Layer6
            sb.AppendLine($"  \"layer6\": [{string.Join(", ", s32.Layer6)}],");

            // Layer7
            sb.AppendLine($"  \"layer7\": [");
            for (int i = 0; i < s32.Layer7.Count; i++)
            {
                var item = s32.Layer7[i];
                sb.Append($"    {{\"name\": \"{item.Name.Replace("\"", "\\\"")}\", \"x\": {item.X}, \"y\": {item.Y}, \"targetMapId\": {item.TargetMapId}, \"portalId\": {item.PortalId}}}");
                sb.AppendLine(i < s32.Layer7.Count - 1 ? "," : "");
            }
            sb.AppendLine("  ],");

            // Layer8
            sb.AppendLine($"  \"layer8\": [");
            for (int i = 0; i < s32.Layer8.Count; i++)
            {
                var item = s32.Layer8[i];
                sb.Append($"    {{\"sprId\": {item.SprId}, \"x\": {item.X}, \"y\": {item.Y}, \"extendedData\": {item.ExtendedData}}}");
                sb.AppendLine(i < s32.Layer8.Count - 1 ? "," : "");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            Console.WriteLine($"已匯出到: {outputPath}");

            return 0;
        }

        /// <summary>
        /// fix 命令 - 修復異常資料
        /// </summary>
        private static int CmdFix(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli fix <s32檔案> [--apply]");
                Console.WriteLine("  --apply: 實際執行修復並覆蓋原檔案（不加則只顯示問題）");
                return 1;
            }

            string filePath = args[0];
            bool applyFix = args.Contains("--apply");

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"檔案不存在: {filePath}");
                return 1;
            }

            var s32 = S32Parser.ParseFile(filePath);

            Console.WriteLine($"=== 檢查 S32 資料 ===");
            Console.WriteLine($"檔案: {Path.GetFileName(filePath)}");
            Console.WriteLine();

            // 找出所有 X>=128 的物件（溢出物件）
            var overflowLayer4 = s32.Layer4.Where(o => o.X >= 128).ToList();

            Console.WriteLine($"Layer4 溢出物件 (X>=128): {overflowLayer4.Count} 個");
            Console.WriteLine("（這些物件的 X 座標超出正常範圍 0-127，無法在編輯器中被選取刪除）");
            Console.WriteLine();
            foreach (var obj in overflowLayer4)
            {
                Console.WriteLine($"  GroupId={obj.GroupId}, X={obj.X}, Y={obj.Y}, Layer={obj.Layer}, IndexId={obj.IndexId}, TileId={obj.TileId}");
            }

            if (overflowLayer4.Count == 0)
            {
                Console.WriteLine("沒有發現溢出物件！");
                return 0;
            }

            if (!applyFix)
            {
                Console.WriteLine();
                Console.WriteLine("若要清除這些溢出物件，請加上 --apply 參數");
                return 0;
            }

            // 執行修復
            Console.WriteLine();
            Console.WriteLine("正在清除溢出物件...");

            // 移除溢出物件
            int removedCount = s32.Layer4.RemoveAll(o => o.X >= 128);

            // 重新寫入檔案
            SaveS32File(s32, filePath);

            Console.WriteLine($"已移除 {removedCount} 個溢出 Layer4 物件");
            Console.WriteLine($"已儲存到: {filePath}");

            return 0;
        }

        /// <summary>
        /// coords 命令 - 計算地圖座標範圍
        /// </summary>
        private static int CmdCoords(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli coords <地圖資料夾>");
                Console.WriteLine("  例如: -cli coords \"C:\\Lin\\map\\8100\"");
                return 1;
            }

            string mapFolder = args[0];
            if (!Directory.Exists(mapFolder))
            {
                Console.WriteLine($"資料夾不存在: {mapFolder}");
                return 1;
            }

            // 找出所有 S32 檔案
            var s32Files = Directory.GetFiles(mapFolder, "*.s32");
            if (s32Files.Length == 0)
            {
                Console.WriteLine($"資料夾中沒有 S32 檔案: {mapFolder}");
                return 1;
            }

            Console.WriteLine($"=== 地圖座標計算 ===");
            Console.WriteLine($"資料夾: {mapFolder}");
            Console.WriteLine($"S32 檔案數: {s32Files.Length}");
            Console.WriteLine();

            // 從檔名解析 Block 座標，計算地圖範圍
            int minBlockX = int.MaxValue;
            int minBlockY = int.MaxValue;
            int maxBlockX = int.MinValue;
            int maxBlockY = int.MinValue;

            foreach (var file in s32Files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                // 檔名格式: XXXXYYYYYYY.s32 (例如 7fff8000.s32)
                if (fileName.Length >= 8)
                {
                    try
                    {
                        int blockX = Convert.ToInt32(fileName.Substring(0, 4), 16);
                        int blockY = Convert.ToInt32(fileName.Substring(4, 4), 16);

                        minBlockX = Math.Min(minBlockX, blockX);
                        minBlockY = Math.Min(minBlockY, blockY);
                        maxBlockX = Math.Max(maxBlockX, blockX);
                        maxBlockY = Math.Max(maxBlockY, blockY);
                    }
                    catch
                    {
                        // 忽略無法解析的檔名
                    }
                }
            }

            if (minBlockX == int.MaxValue)
            {
                Console.WriteLine("無法從 S32 檔名解析 Block 座標");
                return 1;
            }

            // 計算遊戲座標
            // 每個 Block 是 64x64 格
            // 遊戲座標公式: (blockX - 0x7FFF) * 64 + 0x7FFF
            int startX = (minBlockX - 0x7FFF) * 64 + 0x7FFF - 64 + 1;
            int endX = (maxBlockX - 0x7FFF) * 64 + 0x7FFF;
            int startY = (minBlockY - 0x7FFF) * 64 + 0x7FFF - 64 + 1;
            int endY = (maxBlockY - 0x7FFF) * 64 + 0x7FFF;

            Console.WriteLine($"Block 座標範圍:");
            Console.WriteLine($"  X: 0x{minBlockX:X4} ~ 0x{maxBlockX:X4} ({minBlockX} ~ {maxBlockX})");
            Console.WriteLine($"  Y: 0x{minBlockY:X4} ~ 0x{maxBlockY:X4} ({minBlockY} ~ {maxBlockY})");
            Console.WriteLine();
            Console.WriteLine($"遊戲座標範圍:");
            Console.WriteLine($"  startX={startX}, endX={endX}, startY={startY}, endY={endY}");
            Console.WriteLine();

            // 輸出可直接複製的格式
            string coordText = $"startX={startX}, endX={endX}, startY={startY}, endY={endY}";
            Console.WriteLine($"可複製格式: {coordText}");

            return 0;
        }

        /// <summary>
        /// export-tiles 命令 - 匯出 Tiles 到 ZIP
        /// </summary>
        private static int CmdExportTiles(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法: -cli export-tiles <s32檔案或資料夾> <輸出.zip> [--til] [--png]");
                Console.WriteLine("  --til: 只匯出 .til 原始檔案");
                Console.WriteLine("  --png: 只匯出 .png 預覽圖片");
                Console.WriteLine("  不指定則同時匯出 .til 和 .png");
                return 1;
            }

            string inputPath = args[0];
            string outputPath = args[1];

            // 解析選項
            bool hasTilFlag = args.Contains("--til");
            bool hasPngFlag = args.Contains("--png");

            // 預設只匯出 .til，需要明確指定 --png 才匯出預覽圖片
            bool exportTil = !hasTilFlag && !hasPngFlag || hasTilFlag;
            bool exportPng = hasPngFlag;

            // 收集 S32 檔案
            List<string> s32Files = new List<string>();
            if (File.Exists(inputPath))
            {
                s32Files.Add(inputPath);
            }
            else if (Directory.Exists(inputPath))
            {
                s32Files.AddRange(Directory.GetFiles(inputPath, "*.s32"));
            }
            else
            {
                Console.WriteLine($"檔案或資料夾不存在: {inputPath}");
                return 1;
            }

            if (s32Files.Count == 0)
            {
                Console.WriteLine("找不到 S32 檔案");
                return 1;
            }

            // 從 S32 路徑推斷 client 路徑並設定 Share.LineagePath
            string firstS32 = s32Files[0];
            string clientPath = FindClientPath(firstS32);
            if (string.IsNullOrEmpty(clientPath))
            {
                Console.WriteLine($"無法找到 client 資料夾（需要 Tile.idx 和 Tile.pak）");
                Console.WriteLine($"請確保 S32 檔案位於 client/map/xxx/ 結構中");
                return 1;
            }
            Share.LineagePath = clientPath;

            Console.WriteLine($"=== 匯出 Tiles ===");
            Console.WriteLine($"輸入: {inputPath}");
            Console.WriteLine($"Client: {clientPath}");
            Console.WriteLine($"S32 檔案數: {s32Files.Count}");
            Console.WriteLine($"匯出 .til: {(exportTil ? "是" : "否")}");
            Console.WriteLine($"匯出 .png: {(exportPng ? "是" : "否")}");
            Console.WriteLine();

            // 收集所有使用的 TileId
            HashSet<int> allTileIds = new HashSet<int>();
            Dictionary<int, HashSet<int>> tileIndexIds = new Dictionary<int, HashSet<int>>(); // TileId -> IndexIds

            foreach (var s32File in s32Files)
            {
                var s32 = S32Parser.ParseFile(s32File);

                // 從 Layer1 收集
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        var cell = s32.Layer1[y, x];
                        if (cell != null && cell.TileId > 0)
                        {
                            allTileIds.Add(cell.TileId);
                            if (!tileIndexIds.ContainsKey(cell.TileId))
                                tileIndexIds[cell.TileId] = new HashSet<int>();
                            tileIndexIds[cell.TileId].Add(cell.IndexId);
                        }
                    }
                }

                // 從 Layer4 收集
                foreach (var obj in s32.Layer4)
                {
                    if (obj.TileId > 0)
                    {
                        allTileIds.Add(obj.TileId);
                        if (!tileIndexIds.ContainsKey(obj.TileId))
                            tileIndexIds[obj.TileId] = new HashSet<int>();
                        tileIndexIds[obj.TileId].Add(obj.IndexId);
                    }
                }
            }

            Console.WriteLine($"找到 {allTileIds.Count} 個不重複的 TileId");
            Console.WriteLine();

            // 匯出到 ZIP
            int tilExportedCount = 0;
            int pngExportedCount = 0;
            int errorCount = 0;

            try
            {
                using (var zipStream = new FileStream(outputPath, FileMode.Create))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    foreach (var tileId in allTileIds.OrderBy(id => id))
                    {
                        Console.Write($"\r處理 TileId {tileId}...                    ");

                        try
                        {
                            // 載入 til 檔案
                            string key = $"{tileId}.til";
                            byte[] data = L1PakReader.UnPack("Tile", key);
                            if (data == null)
                            {
                                errorCount++;
                                continue;
                            }

                            // 匯出 .til 原始檔案
                            if (exportTil)
                            {
                                string tilEntryName = $"{tileId}.til";
                                var tilEntry = archive.CreateEntry(tilEntryName, CompressionLevel.Optimal);
                                using (var entryStream = tilEntry.Open())
                                {
                                    entryStream.Write(data, 0, data.Length);
                                }
                                tilExportedCount++;
                            }

                            // 匯出 .png 預覽圖片
                            if (exportPng)
                            {
                                var tilArray = L1Til.Parse(data);
                                string folderName = $"preview/til_{tileId:D6}";

                                // 匯出所有 IndexIds（不只是使用到的）
                                for (int indexId = 0; indexId < tilArray.Count; indexId++)
                                {
                                    byte[] tilData = tilArray[indexId];
                                    // 自動偵測尺寸並以原始大小匯出
                                    using (var bitmap = RenderTileToBitmap(tilData))
                                    {
                                        if (bitmap != null)
                                        {
                                            string entryName = $"{folderName}/index_{indexId:D3}.png";
                                            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

                                            using (var entryStream = entry.Open())
                                            {
                                                bitmap.Save(entryStream, ImageFormat.Png);
                                            }
                                            pngExportedCount++;
                                        }
                                        else
                                        {
                                            errorCount++;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"\n  錯誤: {ex.Message}");
                            errorCount++;
                        }
                    }
                }

                Console.WriteLine("\r                                              ");
                Console.WriteLine();
                Console.WriteLine($"=== 匯出結果 ===");
                if (exportTil) Console.WriteLine($".til 檔案: {tilExportedCount} 個");
                if (exportPng) Console.WriteLine($".png 圖片: {pngExportedCount} 個");
                if (errorCount > 0) Console.WriteLine($"失敗: {errorCount} 個");
                Console.WriteLine($"輸出: {outputPath}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"匯出失敗: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// list-maps 命令 - 測試讀取地圖列表
        /// </summary>
        private static int CmdListMaps(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli list-maps <client路徑>");
                return 1;
            }

            string clientPath = args[0];
            if (!Directory.Exists(clientPath))
            {
                Console.WriteLine($"路徑不存在: {clientPath}");
                return 1;
            }

            string mapPath = Path.Combine(clientPath, "map");
            if (!Directory.Exists(mapPath))
            {
                Console.WriteLine($"map 資料夾不存在: {mapPath}");
                return 1;
            }

            Console.WriteLine($"=== 測試讀取地圖列表 ===");
            Console.WriteLine($"Client 路徑: {clientPath}");
            Console.WriteLine();

            // 設定 Share.LineagePath
            Share.LineagePath = clientPath;
            Console.WriteLine($"[1] Share.LineagePath = {Share.LineagePath}");

            // 測試讀取 Zone3desc
            Console.WriteLine();
            Console.WriteLine("[2] 測試 LoadZone3descTbl...");
            var sw = Stopwatch.StartNew();
            try
            {
                L1MapHelper.LoadZone3descTbl();
                sw.Stop();
                Console.WriteLine($"    完成! 耗時: {sw.ElapsedMilliseconds}ms, 項目數: {Share.Zone3descList.Count}");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Console.WriteLine($"    錯誤: {ex.Message}");
                Console.WriteLine($"    堆疊: {ex.StackTrace}");
            }

            // 測試讀取 ZoneXml
            Console.WriteLine();
            Console.WriteLine("[3] 測試 LoadZoneXml...");
            sw.Restart();
            try
            {
                L1MapHelper.LoadZoneXml();
                sw.Stop();
                Console.WriteLine($"    完成! 耗時: {sw.ElapsedMilliseconds}ms, 項目數: {Share.ZoneList.Count}");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Console.WriteLine($"    錯誤: {ex.Message}");
                Console.WriteLine($"    堆疊: {ex.StackTrace}");
            }

            // 測試完整讀取地圖列表
            Console.WriteLine();
            Console.WriteLine("[4] 測試 L1MapHelper.Read...");
            sw.Restart();
            try
            {
                var maps = L1MapHelper.Read(clientPath);
                sw.Stop();
                Console.WriteLine($"    完成! 耗時: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"    地圖數量: {maps.Count}");
                Console.WriteLine($"    檔案數量: {L1MapHelper.LastTotalFileCount}");

                // 列出前 10 個地圖
                Console.WriteLine();
                Console.WriteLine("前 10 個地圖:");
                int count = 0;
                foreach (var kvp in maps.OrderBy(k => k.Key))
                {
                    if (count++ >= 10) break;
                    Console.WriteLine($"    {kvp.Key}: {kvp.Value.szName} ({kvp.Value.FullFileNameList.Count} 個檔案)");
                }
                if (maps.Count > 10)
                    Console.WriteLine($"    ... 還有 {maps.Count - 10} 個地圖");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Console.WriteLine($"    錯誤: {ex.Message}");
                Console.WriteLine($"    堆疊: {ex.StackTrace}");
            }

            return 0;
        }

        /// <summary>
        /// extract-tile 命令 - 從指定 idx/pak 提取特定 TileId 的所有 block
        /// </summary>
        private static int CmdExtractTile(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法: -cli extract-tile <idx路徑> <tile-id> [輸出資料夾] [--downscale]");
                Console.WriteLine("  idx路徑: Tile.idx 檔案的完整路徑");
                Console.WriteLine("  tile-id: 要提取的 TileId");
                Console.WriteLine("  輸出資料夾: 可選，預設為目前目錄");
                Console.WriteLine("  --downscale: 將 R 版 (48x48) 縮小成 Classic 版 (24x24)");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  -cli extract-tile \"C:\\Lin\\Tile.idx\" 1234");
                Console.WriteLine("  -cli extract-tile \"C:\\Lin\\Tile.idx\" 1234 \"C:\\output\"");
                Console.WriteLine("  -cli extract-tile \"C:\\Lin4R\\Tile.idx\" 1234 \"C:\\output\" --downscale");
                return 1;
            }

            string idxPath = args[0];
            if (!int.TryParse(args[1], out int tileId))
            {
                Console.WriteLine($"無效的 TileId: {args[1]}");
                return 1;
            }

            bool downscale = args.Contains("--downscale");
            string outputFolder = args.Length >= 3 && !args[2].StartsWith("--") ? args[2] : Directory.GetCurrentDirectory();

            if (!File.Exists(idxPath))
            {
                Console.WriteLine($"idx 檔案不存在: {idxPath}");
                return 1;
            }

            string pakPath = Path.ChangeExtension(idxPath, ".pak");
            if (!File.Exists(pakPath))
            {
                Console.WriteLine($"pak 檔案不存在: {pakPath}");
                return 1;
            }

            Console.WriteLine($"=== 提取 Tile ===");
            Console.WriteLine($"idx: {idxPath}");
            Console.WriteLine($"pak: {pakPath}");
            Console.WriteLine($"TileId: {tileId}");
            Console.WriteLine($"輸出: {outputFolder}");
            Console.WriteLine($"縮小至 Classic: {(downscale ? "是" : "否")}");
            Console.WriteLine();

            try
            {
                // 讀取 idx 檔案
                byte[] idxData = File.ReadAllBytes(idxPath);

                // 判斷 idx 結構類型
                string head = Encoding.Default.GetString(idxData, 0, 4).ToLower();
                IdxType structType = IdxType.OLD;
                int baseOffset = 4;

                if (head == "_ext")
                {
                    structType = IdxType.EXT;
                    baseOffset = 8;
                }
                else if (head == "_rms")
                {
                    structType = IdxType.RMS;
                    baseOffset = 8;
                }

                Console.WriteLine($"idx 格式: {structType}");

                // 搜尋指定的 TileId
                string targetFileName = $"{tileId}.til".ToLower();
                int position = -1;
                int size = 0;
                int compressSize = 0;
                int compressType = 0;
                string foundFileName = null;

                using (var br = new BinaryReader(new MemoryStream(idxData)))
                {
                    br.BaseStream.Seek(baseOffset, SeekOrigin.Begin);

                    while (br.BaseStream.Position < idxData.Length)
                    {
                        int pos = br.ReadInt32();
                        string fileName;
                        int fSize, cSize = 0, cType = 0;

                        if (structType == IdxType.EXT)
                        {
                            fSize = br.ReadInt32();
                            cSize = br.ReadInt32();
                            cType = br.ReadInt32();
                            fileName = Encoding.Default.GetString(br.ReadBytes(112)).TrimEnd('\0').Trim();
                        }
                        else if (structType == IdxType.RMS)
                        {
                            fSize = br.ReadInt32();
                            cSize = br.ReadInt32();
                            cType = br.ReadInt32();
                            fileName = Encoding.Default.GetString(br.ReadBytes(260)).TrimEnd('\0').Trim();
                        }
                        else
                        {
                            fileName = Encoding.Default.GetString(br.ReadBytes(20)).TrimEnd('\0').Trim();
                            fSize = br.ReadInt32();
                        }

                        if (fileName.ToLower() == targetFileName)
                        {
                            position = pos;
                            size = fSize;
                            compressSize = cSize;
                            compressType = cType;
                            foundFileName = fileName;
                            break;
                        }
                    }
                }

                if (position < 0)
                {
                    Console.WriteLine($"找不到 TileId {tileId} ({targetFileName})");
                    return 1;
                }

                Console.WriteLine($"找到: {foundFileName}");
                Console.WriteLine($"  Position: {position} (0x{position:X8})");
                Console.WriteLine($"  Size: {size}");
                if (structType != IdxType.OLD)
                {
                    Console.WriteLine($"  CompressSize: {compressSize}");
                    Console.WriteLine($"  CompressType: {compressType}");
                }
                Console.WriteLine();

                // 從 pak 讀取資料
                int readSize = (compressSize > 0) ? compressSize : size;
                byte[] pakData;

                using (var fs = new FileStream(pakPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    fs.Seek(position, SeekOrigin.Begin);
                    pakData = new byte[readSize];
                    fs.Read(pakData, 0, readSize);
                }

                // 解壓縮（如果需要）
                byte[] tilData = pakData;
                if (compressType == 1)
                {
                    // zlib
                    tilData = DecompressZlib(pakData, size);
                    Console.WriteLine($"已解壓縮 (zlib): {pakData.Length} -> {tilData.Length} bytes");
                }
                else if (compressType == 2)
                {
                    // brotli
                    tilData = DecompressBrotli(pakData, size);
                    Console.WriteLine($"已解壓縮 (brotli): {pakData.Length} -> {tilData.Length} bytes");
                }

                // 判斷版本
                var version = L1Til.GetVersion(tilData);
                int tileSize = L1Til.GetTileSize(version);
                Console.WriteLine($"版本: {version} ({tileSize}x{tileSize})");

                // 如果需要縮小且是 R 版或混合格式
                byte[] outputTilData = tilData;
                if (downscale && (version == L1Til.TileVersion.Remaster || version == L1Til.TileVersion.Hybrid))
                {
                    outputTilData = L1Til.DownscaleTil(tilData);
                    Console.WriteLine($"已縮小: {tilData.Length} -> {outputTilData.Length} bytes");
                    version = L1Til.TileVersion.Classic;
                    tileSize = 24;
                }

                // 使用 L1Til.Parse 解析 til 檔案的 block
                var blocks = L1Til.Parse(outputTilData);
                Console.WriteLine($"Block 數量 (IndexId): {blocks.Count}");
                Console.WriteLine();

                // 建立輸出資料夾
                string folderSuffix = downscale && L1Til.IsRemaster(tilData) ? "_downscaled" : "";
                string tileOutputFolder = Path.Combine(outputFolder, $"tile_{tileId}{folderSuffix}");
                Directory.CreateDirectory(tileOutputFolder);

                // 輸出 til 檔案
                string tilOutputPath = Path.Combine(tileOutputFolder, $"{tileId}.til");
                File.WriteAllBytes(tilOutputPath, outputTilData);
                Console.WriteLine($"已輸出: {tilOutputPath}");

                // 輸出各個 block
                for (int i = 0; i < blocks.Count; i++)
                {
                    byte[] blockData = blocks[i];
                    string blockPath = Path.Combine(tileOutputFolder, $"block_{i:D3}.bin");
                    File.WriteAllBytes(blockPath, blockData);

                    // 嘗試渲染為 PNG
                    try
                    {
                        using (var bitmap = RenderTileToBitmap(blockData))
                        {
                            if (bitmap != null)
                            {
                                string pngPath = Path.Combine(tileOutputFolder, $"block_{i:D3}.png");
                                bitmap.Save(pngPath, ImageFormat.Png);
                            }
                        }
                    }
                    catch
                    {
                        // 忽略渲染錯誤
                    }
                }

                Console.WriteLine($"已輸出 {blocks.Count} 個 block 到: {tileOutputFolder}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"錯誤: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// zlib 解壓縮
        /// </summary>
        private static byte[] DecompressZlib(byte[] data, int expectedSize)
        {
            // 跳過 zlib header (2 bytes)
            using (var ms = new MemoryStream(data, 2, data.Length - 2))
            using (var deflate = new System.IO.Compression.DeflateStream(ms, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                deflate.CopyTo(output);
                return output.ToArray();
            }
        }

        /// <summary>
        /// brotli 解壓縮
        /// </summary>
        private static byte[] DecompressBrotli(byte[] data, int expectedSize)
        {
            using (var ms = new MemoryStream(data))
            using (var brotli = new System.IO.Compression.BrotliStream(ms, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                brotli.CopyTo(output);
                return output.ToArray();
            }
        }

        /// <summary>
        /// 從 S32 檔案路徑尋找 client 路徑（往上找直到找到 Tile.idx）
        /// </summary>
        private static string FindClientPath(string s32FilePath)
        {
            string dir = Path.GetDirectoryName(s32FilePath);
            while (!string.IsNullOrEmpty(dir))
            {
                string tileIdx = Path.Combine(dir, "Tile.idx");
                string tilePak = Path.Combine(dir, "Tile.pak");
                if (File.Exists(tileIdx) && File.Exists(tilePak))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        /// <summary>
        /// 從 til 資料偵測原始尺寸
        /// 2.5D 菱形格式: 每行像素數 = 2+4+6+...+2n+...+6+4+2 = 2*(1+2+...+n)*2 = 2*n*(n+1)
        /// 每像素 2 bytes, 加上 1 byte type
        /// 24x24: 2*12*13*2 + 1 = 625 bytes (標準尺寸)
        /// 48x48: 2*24*25*2 + 1 = 2401 bytes
        /// </summary>
        private static int DetectTilSize(byte[] tilData)
        {
            if (tilData == null || tilData.Length < 2)
                return 24; // 預設

            int dataLen = tilData.Length - 1; // 扣掉 type byte
            int pixelCount = dataLen / 2;

            // 反推 n: pixelCount = 2 * n * (n+1)
            // n^2 + n - pixelCount/2 = 0
            // n = (-1 + sqrt(1 + 2*pixelCount)) / 2
            double n = (-1 + Math.Sqrt(1 + 2 * pixelCount)) / 2;
            int tileSize = (int)Math.Round(n) * 2; // 菱形高度 = 2n

            // 常見尺寸: 24, 48, 96
            if (tileSize <= 24) return 24;
            if (tileSize <= 48) return 48;
            if (tileSize <= 96) return 96;
            return tileSize;
        }

        /// <summary>
        /// 渲染 til 資料為 Bitmap (2.5D 菱形格式)，自動偵測尺寸
        /// </summary>
        private static unsafe Bitmap RenderTileToBitmap(byte[] tilData)
        {
            int tileSize = DetectTilSize(tilData);
            return RenderTileToBitmap(tilData, tileSize);
        }

        /// <summary>
        /// 渲染 til 資料為 Bitmap (2.5D 菱形格式)
        /// </summary>
        private static unsafe Bitmap RenderTileToBitmap(byte[] tilData, int size)
        {
            try
            {
                int tileSize = DetectTilSize(tilData);
                Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format16bppRgb555);

                Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                BitmapData bmpData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);
                int rowpix = bmpData.Stride;
                byte* ptr = (byte*)bmpData.Scan0;

                fixed (byte* til_ptr_fixed = tilData)
                {
                    byte* til_ptr = til_ptr_fixed;
                    byte type = *(til_ptr++);

                    int halfSize = tileSize / 2; // 24 for 48x48, 12 for 24x24
                    double scale = (double)size / tileSize;
                    int offsetX = (int)((size - halfSize * scale) / 2);
                    int offsetY = (int)((size - halfSize * scale) / 2);

                    if (type == 1 || type == 9 || type == 17)
                    {
                        // 下半部 2.5D 方塊
                        for (int ty = 0; ty < halfSize; ty++)
                        {
                            int n = (ty <= halfSize / 2 - 1) ? (ty + 1) * 2 : (halfSize - 1 - ty) * 2;
                            int tx = 0;
                            for (int p = 0; p < n; p++)
                            {
                                ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));

                                int baseX = (int)(offsetX + tx * scale);
                                int baseY = (int)(offsetY + ty * scale);

                                for (int sy = 0; sy < (int)scale + 1; sy++)
                                {
                                    for (int sx = 0; sx < (int)scale + 1; sx++)
                                    {
                                        int px = baseX + sx;
                                        int py = baseY + sy;
                                        if (px >= 0 && px < size && py >= 0 && py < size)
                                        {
                                            int v = py * rowpix + (px * 2);
                                            *(ptr + v) = (byte)(color & 0x00FF);
                                            *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                                        }
                                    }
                                }
                                tx++;
                            }
                        }
                    }
                    else if (type == 0 || type == 8 || type == 16)
                    {
                        // 上半部 2.5D 方塊
                        for (int ty = 0; ty < halfSize; ty++)
                        {
                            int n = (ty <= halfSize / 2 - 1) ? (ty + 1) * 2 : (halfSize - 1 - ty) * 2;
                            int tx = halfSize - n;
                            for (int p = 0; p < n; p++)
                            {
                                ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));

                                int baseX = (int)(offsetX + tx * scale);
                                int baseY = (int)(offsetY + ty * scale);

                                for (int sy = 0; sy < (int)scale + 1; sy++)
                                {
                                    for (int sx = 0; sx < (int)scale + 1; sx++)
                                    {
                                        int px = baseX + sx;
                                        int py = baseY + sy;
                                        if (px >= 0 && px < size && py >= 0 && py < size)
                                        {
                                            int v = py * rowpix + (px * 2);
                                            *(ptr + v) = (byte)(color & 0x00FF);
                                            *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                                        }
                                    }
                                }
                                tx++;
                            }
                        }
                    }
                }

                bitmap.UnlockBits(bmpData);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 儲存 S32 檔案
        /// </summary>
        private static void SaveS32File(S32Data s32, string filePath)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // 第一層（地板）- 64x128
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        var cell = s32.Layer1[y, x];
                        if (cell != null)
                        {
                            bw.Write((byte)cell.IndexId);
                            bw.Write((ushort)cell.TileId);
                            bw.Write((byte)0); // nk
                        }
                        else
                        {
                            bw.Write((byte)0);
                            bw.Write((ushort)0);
                            bw.Write((byte)0);
                        }
                    }
                }

                // 第二層
                bw.Write((ushort)s32.Layer2.Count);
                foreach (var item in s32.Layer2)
                {
                    bw.Write(item.X);
                    bw.Write(item.Y);
                    bw.Write(item.IndexId);
                    bw.Write(item.TileId);
                    bw.Write(item.UK);
                }

                // 第三層（地圖屬性）- 64x64
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        var attr = s32.Layer3[y, x];
                        if (attr != null)
                        {
                            bw.Write(attr.Attribute1);
                            bw.Write(attr.Attribute2);
                        }
                        else
                        {
                            bw.Write((short)0);
                            bw.Write((short)0);
                        }
                    }
                }

                // 第四層（物件）- 按 GroupId 分組
                var groupedObjects = s32.Layer4.GroupBy(o => o.GroupId).OrderBy(g => g.Key).ToList();
                bw.Write(groupedObjects.Count); // 群組數

                foreach (var group in groupedObjects)
                {
                    bw.Write((short)group.Key); // GroupId
                    bw.Write((ushort)group.Count()); // blockCount

                    foreach (var obj in group)
                    {
                        bw.Write((byte)obj.X);
                        bw.Write((byte)obj.Y);
                        bw.Write((byte)obj.Layer);
                        bw.Write((byte)obj.IndexId);
                        bw.Write((short)obj.TileId);
                        bw.Write((byte)0); // uk
                    }
                }

                // 第五層 - 可透明化的圖塊
                bw.Write(s32.Layer5.Count);
                foreach (var item in s32.Layer5)
                {
                    bw.Write(item.X);
                    bw.Write(item.Y);
                    bw.Write(item.ObjectIndex);
                    bw.Write(item.Type);
                }

                // 第六層 - 使用的 til（重新計算並排序）
                // 收集 Layer1 和 Layer4 使用的所有 TileId
                HashSet<int> usedTileIds = new HashSet<int>();

                // 從 Layer1 收集
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        var cell = s32.Layer1[y, x];
                        if (cell != null && cell.TileId > 0)
                        {
                            usedTileIds.Add(cell.TileId);
                        }
                    }
                }

                // 從 Layer4 收集
                foreach (var obj in s32.Layer4)
                {
                    if (obj.TileId > 0)
                    {
                        usedTileIds.Add(obj.TileId);
                    }
                }

                // 排序後寫入
                List<int> sortedTileIds = usedTileIds.OrderBy(id => id).ToList();
                bw.Write(sortedTileIds.Count);
                foreach (var tilId in sortedTileIds)
                {
                    bw.Write(tilId);
                }

                // 更新記憶體中的 Layer6 資料
                s32.Layer6.Clear();
                s32.Layer6.AddRange(sortedTileIds);

                // 第七層 - 傳送點、入口點
                bw.Write((ushort)s32.Layer7.Count);
                foreach (var item in s32.Layer7)
                {
                    byte[] nameBytes = Encoding.Default.GetBytes(item.Name ?? "");
                    bw.Write((byte)nameBytes.Length);
                    bw.Write(nameBytes);
                    bw.Write(item.X);
                    bw.Write(item.Y);
                    bw.Write(item.TargetMapId);
                    bw.Write(item.PortalId);
                }

                // 第八層 - 特效、裝飾品
                ushort lv8Count = (ushort)s32.Layer8.Count;
                if (s32.Layer8HasExtendedData)
                {
                    lv8Count |= 0x8000;  // 設置高位表示有擴展資料
                }
                bw.Write(lv8Count);
                foreach (var item in s32.Layer8)
                {
                    bw.Write(item.SprId);
                    bw.Write(item.X);
                    bw.Write(item.Y);
                    if (s32.Layer8HasExtendedData)
                    {
                        bw.Write(item.ExtendedData);
                    }
                }

                File.WriteAllBytes(filePath, ms.ToArray());
            }
        }

        /// <summary>
        /// 裁減 S32 檔案，只保留前 N 個 TileId
        /// 用法: trim-s32 <來源s32> <輸出s32> <保留數量> [--skip TileId1,TileId2,...]
        /// </summary>
        private static int CmdTrimS32(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("用法: trim-s32 <來源s32> <輸出s32> <保留數量> [--skip TileId1,TileId2,...]");
                Console.WriteLine("範例: trim-s32 source.s32 output.s32 10");
                Console.WriteLine("範例: trim-s32 source.s32 output.s32 25 --skip 7137");
                return 1;
            }

            string srcPath = args[0];
            string dstPath = args[1];
            if (!int.TryParse(args[2], out int keepCount) || keepCount <= 0)
            {
                Console.WriteLine("保留數量必須是正整數");
                return 1;
            }

            // 解析 --skip 參數
            HashSet<int> skipTileIds = new HashSet<int>();
            for (int i = 3; i < args.Length; i++)
            {
                if (args[i] == "--skip" && i + 1 < args.Length)
                {
                    string[] skipIds = args[i + 1].Split(',');
                    foreach (var idStr in skipIds)
                    {
                        if (int.TryParse(idStr.Trim(), out int skipId))
                        {
                            skipTileIds.Add(skipId);
                        }
                    }
                    i++; // 跳過下一個參數
                }
            }

            if (!File.Exists(srcPath))
            {
                Console.WriteLine($"找不到來源檔案: {srcPath}");
                return 1;
            }

            // 讀取來源 S32
            Console.WriteLine($"讀取來源: {srcPath}");
            S32Data s32 = S32Parser.ParseFile(srcPath);

            // 取得所有使用的 TileId（從 Layer6 或 UsedTiles）
            List<int> allTileIds = s32.Layer6.Count > 0
                ? s32.Layer6.OrderBy(t => t).ToList()
                : s32.UsedTiles.Keys.OrderBy(t => t).ToList();

            Console.WriteLine($"原始 TileId 數量: {allTileIds.Count}");

            // 先過濾掉要跳過的 TileId
            if (skipTileIds.Count > 0)
            {
                Console.WriteLine($"跳過的 TileId: {string.Join(", ", skipTileIds.OrderBy(t => t))}");
                allTileIds = allTileIds.Where(t => !skipTileIds.Contains(t)).ToList();
                Console.WriteLine($"過濾後 TileId 數量: {allTileIds.Count}");
            }

            if (allTileIds.Count <= keepCount)
            {
                Console.WriteLine($"TileId 數量 ({allTileIds.Count}) 已小於等於保留數量 ({keepCount})，保留所有 TileId");
                // 仍需清空 Layer5, Layer7, Layer8
                int l5Count = s32.Layer5.Count;
                int l7Count = s32.Layer7.Count;
                int l8Count = s32.Layer8.Count;
                s32.Layer5.Clear();
                s32.Layer7.Clear();
                s32.Layer8.Clear();
                Console.WriteLine($"Layer5 清空了 {l5Count} 個事件");
                Console.WriteLine($"Layer7 清空了 {l7Count} 個傳送點");
                Console.WriteLine($"Layer8 清空了 {l8Count} 個特效");

                // 寫入目標檔案
                S32Writer.Write(s32, dstPath);
                Console.WriteLine($"已寫入: {dstPath}");
                return 0;
            }

            // 只保留前 N 個 TileId
            HashSet<int> keepTileIds = new HashSet<int>(allTileIds.Take(keepCount));
            int defaultTileId = allTileIds.First(); // 用第一個 TileId 作為替代

            Console.WriteLine($"保留的 TileId: {string.Join(", ", keepTileIds.OrderBy(t => t))}");
            Console.WriteLine($"替代 TileId: {defaultTileId}");

            int layer1Replaced = 0;
            int layer4Replaced = 0;

            // 處理 Layer1 - 將非保留的 TileId 替換為預設值
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    var cell = s32.Layer1[y, x];
                    if (cell != null && cell.TileId > 0 && !keepTileIds.Contains(cell.TileId))
                    {
                        cell.TileId = defaultTileId;
                        cell.IndexId = 0; // 重設 IndexId
                        layer1Replaced++;
                    }
                }
            }

            // 處理 Layer4 - 移除使用非保留 TileId 的物件
            int originalLayer4Count = s32.Layer4.Count;
            s32.Layer4.RemoveAll(obj => obj.TileId > 0 && !keepTileIds.Contains(obj.TileId));
            layer4Replaced = originalLayer4Count - s32.Layer4.Count;

            // 清空 Layer5, Layer7, Layer8
            int layer5Count = s32.Layer5.Count;
            int layer7Count = s32.Layer7.Count;
            int layer8Count = s32.Layer8.Count;
            s32.Layer5.Clear();
            s32.Layer7.Clear();
            s32.Layer8.Clear();

            Console.WriteLine($"Layer1 替換了 {layer1Replaced} 個格子");
            Console.WriteLine($"Layer4 移除了 {layer4Replaced} 個物件");
            Console.WriteLine($"Layer5 清空了 {layer5Count} 個事件");
            Console.WriteLine($"Layer7 清空了 {layer7Count} 個傳送點");
            Console.WriteLine($"Layer8 清空了 {layer8Count} 個特效");

            // 確保輸出目錄存在
            string dstDir = Path.GetDirectoryName(dstPath);
            if (!string.IsNullOrEmpty(dstDir) && !Directory.Exists(dstDir))
            {
                Directory.CreateDirectory(dstDir);
            }

            // 寫出（S32Writer 會自動重新計算 Layer6）
            S32Writer.Write(s32, dstPath);

            Console.WriteLine($"已寫入: {dstPath}");
            Console.WriteLine($"新的 TileId 數量: {keepCount}");

            return 0;
        }

        /// <summary>
        /// 產生應用程式圖示
        /// </summary>
        private static int CmdGenerateIcon(string[] args)
        {
            string outputPath = args.Length > 0 ? args[0] : "icon.png";

            Console.WriteLine($"產生圖示: {outputPath}");

            using (var bmp = new Bitmap(256, 256))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                int cx = 128, cy = 128;
                int r = 50;  // 菱形半徑（高度）

                // 繪製 5 個菱形格子（十字排列）
                DrawDiamond(g, cx, cy - r * 2, r, Color.FromArgb(74, 124, 89));     // 上 - 草地綠
                DrawDiamond(g, cx - r * 2, cy, r, Color.FromArgb(196, 163, 90));    // 左 - 沙地黃
                DrawDiamond(g, cx, cy, r, Color.FromArgb(60, 100, 150));            // 中 - 水藍
                DrawDiamond(g, cx + r * 2, cy, r, Color.FromArgb(140, 100, 70));    // 右 - 土棕
                DrawDiamond(g, cx, cy + r * 2, r, Color.FromArgb(100, 110, 100));   // 下 - 石灰

                // 中間菱形加白框表示選中
                DrawDiamondBorder(g, cx, cy, r, Color.White, 4);

                // 儲存 PNG
                bmp.Save(outputPath, ImageFormat.Png);
                Console.WriteLine($"已產生: {outputPath}");

                // 產生不同尺寸
                string dir = Path.GetDirectoryName(outputPath);
                string baseName = Path.GetFileNameWithoutExtension(outputPath);
                if (string.IsNullOrEmpty(dir)) dir = ".";

                int[] sizes = { 16, 32, 48, 64, 128 };
                foreach (var s in sizes)
                {
                    using (var resized = new Bitmap(bmp, s, s))
                    {
                        string sizePath = Path.Combine(dir, $"{baseName}_{s}.png");
                        resized.Save(sizePath, ImageFormat.Png);
                        Console.WriteLine($"已產生: {sizePath}");
                    }
                }

                // 產生 ICO 檔案（包含多種尺寸）
                string icoPath = Path.Combine(dir, $"{baseName}.ico");
                SaveAsIco(bmp, icoPath);
                Console.WriteLine($"已產生: {icoPath}");
            }

            return 0;
        }

        /// <summary>
        /// 將 Bitmap 儲存為 ICO 檔案（包含 16, 32, 48, 256 尺寸）
        /// </summary>
        private static void SaveAsIco(Bitmap source, string icoPath)
        {
            int[] sizes = { 16, 32, 48, 256 };

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // ICO Header
                bw.Write((short)0);      // Reserved
                bw.Write((short)1);      // Type: 1 = ICO
                bw.Write((short)sizes.Length);  // Number of images

                // 計算資料偏移量
                int headerSize = 6 + (16 * sizes.Length);  // 6 bytes header + 16 bytes per entry
                int currentOffset = headerSize;

                // 準備所有圖片的 PNG 資料
                var pngDataList = new List<byte[]>();
                foreach (var size in sizes)
                {
                    using (var resized = new Bitmap(source, size, size))
                    using (var pngMs = new MemoryStream())
                    {
                        resized.Save(pngMs, ImageFormat.Png);
                        pngDataList.Add(pngMs.ToArray());
                    }
                }

                // 寫入每個圖片的目錄條目
                for (int i = 0; i < sizes.Length; i++)
                {
                    int size = sizes[i];
                    byte[] pngData = pngDataList[i];

                    bw.Write((byte)(size == 256 ? 0 : size));  // Width (0 = 256)
                    bw.Write((byte)(size == 256 ? 0 : size));  // Height (0 = 256)
                    bw.Write((byte)0);       // Color palette
                    bw.Write((byte)0);       // Reserved
                    bw.Write((short)1);      // Color planes
                    bw.Write((short)32);     // Bits per pixel
                    bw.Write(pngData.Length); // Size of image data
                    bw.Write(currentOffset);  // Offset to image data

                    currentOffset += pngData.Length;
                }

                // 寫入所有圖片資料
                foreach (var pngData in pngDataList)
                {
                    bw.Write(pngData);
                }

                File.WriteAllBytes(icoPath, ms.ToArray());
            }
        }

        private static void DrawDiamond(Graphics g, int cx, int cy, int r, Color fillColor)
        {
            // r 是高度半徑，寬度是 2 倍（等角視角）
            var points = new Point[]
            {
                new Point(cx, cy - r),      // 上
                new Point(cx + r * 2, cy),  // 右
                new Point(cx, cy + r),      // 下
                new Point(cx - r * 2, cy),  // 左
            };

            using (var brush = new SolidBrush(fillColor))
            using (var pen = new Pen(Color.FromArgb(40, 40, 40), 2))
            {
                g.FillPolygon(brush, points);
                g.DrawPolygon(pen, points);
            }
        }

        private static void DrawDiamondBorder(Graphics g, int cx, int cy, int r, Color borderColor, int width)
        {
            var points = new Point[]
            {
                new Point(cx, cy - r),
                new Point(cx + r * 2, cy),
                new Point(cx, cy + r),
                new Point(cx - r * 2, cy),
            };

            using (var pen = new Pen(borderColor, width))
            {
                g.DrawPolygon(pen, points);
            }
        }

        /// <summary>
        /// benchmark-viewport 命令 - 測試 viewport 渲染效能
        /// </summary>
        private static int CmdBenchmarkViewport(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli benchmark-viewport <map_path> [--regions N]");
                Console.WriteLine("  map_path: 地圖資料夾路徑（包含 S32 檔案）");
                Console.WriteLine("  --regions N: 測試區域數量（預設 6）");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  -cli benchmark-viewport \"C:\\Lin\\map\\4\"");
                Console.WriteLine("  -cli benchmark-viewport \"C:\\Lin\\map\\4\" --regions 10");
                return 1;
            }

            string mapPath = args[0];
            int regionCount = 6;

            // 解析 --regions 參數
            for (int i = 1; i < args.Length - 1; i++)
            {
                if (args[i] == "--regions" && int.TryParse(args[i + 1], out int r))
                {
                    regionCount = r;
                    break;
                }
            }

            if (!Directory.Exists(mapPath))
            {
                Console.WriteLine($"資料夾不存在: {mapPath}");
                return 1;
            }

            // 找出所有 S32 檔案
            var s32FilePaths = Directory.GetFiles(mapPath, "*.s32");
            if (s32FilePaths.Length == 0)
            {
                Console.WriteLine($"找不到 S32 檔案: {mapPath}");
                return 1;
            }

            // 從 S32 路徑推斷 client 路徑
            string clientPath = FindClientPath(s32FilePaths[0]);
            if (string.IsNullOrEmpty(clientPath))
            {
                Console.WriteLine($"無法找到 client 資料夾（需要 Tile.idx 和 Tile.pak）");
                return 1;
            }
            Share.LineagePath = clientPath;

            // 取得 mapId (地圖資料夾名稱)
            string mapId = Path.GetFileName(mapPath);

            Console.WriteLine("=== Viewport Benchmark ===");
            Console.WriteLine($"Map: {mapId}");
            Console.WriteLine($"Client: {clientPath}");
            Console.WriteLine($"S32 Files: {s32FilePaths.Length}");
            Console.WriteLine($"Regions: {regionCount}");
            Console.WriteLine();

            // 使用 L1MapHelper.Read 載入地圖資料（會填入 SegInfo）
            Console.Write("Loading map data...");
            var sw = Stopwatch.StartNew();
            L1MapViewer.Helper.L1MapHelper.Read(clientPath);
            sw.Stop();
            Console.WriteLine($" {sw.ElapsedMilliseconds} ms");

            // 確認地圖存在於 MapDataList
            if (!Share.MapDataList.ContainsKey(mapId))
            {
                Console.WriteLine($"找不到地圖 {mapId}，可用地圖: {string.Join(", ", Share.MapDataList.Keys.Take(10))}...");
                return 1;
            }
            var currentMap = Share.MapDataList[mapId];

            // 載入所有 S32 檔案
            Console.Write("Loading S32 files...");
            sw.Restart();
            var s32Files = new Dictionary<string, S32Data>();
            int nullS32 = 0;
            foreach (var kvp in currentMap.FullFileNameList)
            {
                string filePath = kvp.Key;
                var segInfo = kvp.Value;

                if (!segInfo.isS32) continue;
                if (!File.Exists(filePath)) continue;

                var s32 = S32Parser.ParseFile(filePath);
                if (s32 == null)
                {
                    nullS32++;
                    continue;
                }
                s32.FilePath = filePath;
                s32.SegInfo = segInfo;
                s32Files[filePath] = s32;
            }
            sw.Stop();
            Console.WriteLine($" {sw.ElapsedMilliseconds} ms (valid: {s32Files.Count}, nullS32: {nullS32})");

            // 計算地圖範圍
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var s32Data in s32Files.Values)
            {
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                if (loc == null) continue;
                int mx = loc[0];
                int my = loc[1];

                minX = Math.Min(minX, mx);
                minY = Math.Min(minY, my);
                maxX = Math.Max(maxX, mx + L1MapViewer.Helper.ViewportRenderer.BlockWidth);
                maxY = Math.Max(maxY, my + L1MapViewer.Helper.ViewportRenderer.BlockHeight);
            }

            int mapWidth = maxX - minX;
            int mapHeight = maxY - minY;
            Console.WriteLine($"Map Size: {mapWidth} x {mapHeight} px");
            Console.WriteLine();

            // 建立渲染器
            var renderer = new L1MapViewer.Helper.ViewportRenderer();
            var checkedFiles = new HashSet<string>(s32Files.Keys);

            // Viewport 大小（模擬典型螢幕）
            int viewportWidth = 4121;
            int viewportHeight = 3844;

            // 隨機生成測試區域
            var random = new Random(42); // 固定種子以便重現
            var regions = new List<Rectangle>();
            for (int i = 0; i < regionCount; i++)
            {
                int x = random.Next(minX, Math.Max(minX + 1, maxX - viewportWidth));
                int y = random.Next(minY, Math.Max(minY + 1, maxY - viewportHeight));
                regions.Add(new Rectangle(x, y, viewportWidth, viewportHeight));
            }

            // 執行測試
            var allStats = new List<L1MapViewer.Helper.ViewportRenderer.RenderStats>();

            for (int i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                Console.WriteLine($"--- Region {i + 1}/{regionCount}: ({region.X}, {region.Y}) {region.Width}x{region.Height} ---");

                L1MapViewer.Helper.ViewportRenderer.RenderStats stats;
                using (var bitmap = renderer.RenderViewport(region, s32Files, checkedFiles, true, true, true, out stats))
                {
                    // bitmap 會在 using 結束時 dispose
                }

                Console.WriteLine($"  [Create Bitmap]    {stats.CreateBitmapMs,5} ms");
                Console.WriteLine($"  [Spatial Query]    {stats.SpatialQueryMs,5} ms  (candidates: {stats.CandidateCount})");
                Console.WriteLine($"  [Sort]             {stats.SortMs,5} ms");
                Console.WriteLine($"  [GetOrRender S32]  {stats.GetBlockMs,5} ms  (blocks: {stats.BlockCount}, hits: {stats.CacheHits}, misses: {stats.CacheMisses})");
                Console.WriteLine($"  [CopyBitmapDirect] {stats.CopyBitmapMs,5} ms");
                Console.WriteLine($"  [Total]            {stats.TotalMs,5} ms");
                Console.WriteLine();

                allStats.Add(stats);
            }

            // 統計
            Console.WriteLine("=== Summary ===");
            var avgTotal = allStats.Average(s => s.TotalMs);
            var minTotal = allStats.Min(s => s.TotalMs);
            var maxTotal = allStats.Max(s => s.TotalMs);
            Console.WriteLine($"Average: {avgTotal:F0} ms");
            Console.WriteLine($"Min: {minTotal} ms, Max: {maxTotal} ms");

            // 找出瓶頸
            var avgCreate = allStats.Average(s => s.CreateBitmapMs);
            var avgSpatial = allStats.Average(s => s.SpatialQueryMs);
            var avgSort = allStats.Average(s => s.SortMs);
            var avgGetBlock = allStats.Average(s => s.GetBlockMs);
            var avgCopy = allStats.Average(s => s.CopyBitmapMs);

            var bottleneck = new[] {
                ("Create Bitmap", avgCreate),
                ("Spatial Query", avgSpatial),
                ("Sort", avgSort),
                ("GetOrRender S32", avgGetBlock),
                ("CopyBitmapDirect", avgCopy)
            }.OrderByDescending(x => x.Item2).First();

            double percentage = (bottleneck.Item2 / avgTotal) * 100;
            Console.WriteLine($"Bottleneck: {bottleneck.Item1} ({percentage:F0}%)");

            renderer.ClearCache();
            return 0;
        }

        /// <summary>
        /// benchmark-minimap 命令 - 測試 MiniMap 渲染效能
        /// </summary>
        private static int CmdBenchmarkMiniMap(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli benchmark-minimap <map_path> [--size N] [--runs N]");
                Console.WriteLine("  map_path: 地圖資料夾路徑（包含 S32 檔案）");
                Console.WriteLine("  --size N: MiniMap 目標大小（預設 256）");
                Console.WriteLine("  --runs N: 執行次數（預設 3）");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  -cli benchmark-minimap \"C:\\Lin\\map\\4\"");
                Console.WriteLine("  -cli benchmark-minimap \"C:\\Lin\\map\\4\" --size 512 --runs 5");
                return 1;
            }

            string mapPath = args[0];
            int targetSize = 256;
            int runCount = 3;

            // 解析參數
            for (int i = 1; i < args.Length - 1; i++)
            {
                if (args[i] == "--size" && int.TryParse(args[i + 1], out int s))
                {
                    targetSize = s;
                }
                else if (args[i] == "--runs" && int.TryParse(args[i + 1], out int r))
                {
                    runCount = r;
                }
            }

            if (!Directory.Exists(mapPath))
            {
                Console.WriteLine($"資料夾不存在: {mapPath}");
                return 1;
            }

            // 找出所有 S32 檔案
            var s32FilePaths = Directory.GetFiles(mapPath, "*.s32");
            if (s32FilePaths.Length == 0)
            {
                Console.WriteLine($"找不到 S32 檔案: {mapPath}");
                return 1;
            }

            // 從 S32 路徑推斷 client 路徑
            string clientPath = FindClientPath(s32FilePaths[0]);
            if (string.IsNullOrEmpty(clientPath))
            {
                Console.WriteLine($"無法找到 client 資料夾（需要 Tile.idx 和 Tile.pak）");
                return 1;
            }
            Share.LineagePath = clientPath;

            // 取得 mapId
            string mapId = Path.GetFileName(mapPath);

            Console.WriteLine("=== MiniMap Benchmark ===");
            Console.WriteLine($"Map: {mapId}");
            Console.WriteLine($"Client: {clientPath}");
            Console.WriteLine($"Target Size: {targetSize}");
            Console.WriteLine($"Runs: {runCount}");
            Console.WriteLine();

            // 使用 L1MapHelper.Read 載入地圖資料
            Console.Write("Loading map data...");
            var sw = Stopwatch.StartNew();
            L1MapViewer.Helper.L1MapHelper.Read(clientPath);
            sw.Stop();
            Console.WriteLine($" {sw.ElapsedMilliseconds} ms");

            // 確認地圖存在於 MapDataList
            if (!Share.MapDataList.ContainsKey(mapId))
            {
                Console.WriteLine($"找不到地圖 {mapId}，可用地圖: {string.Join(", ", Share.MapDataList.Keys.Take(10))}...");
                return 1;
            }
            var currentMap = Share.MapDataList[mapId];

            // 載入所有 S32 檔案
            Console.Write("Loading S32 files...");
            sw.Restart();
            var s32Files = new Dictionary<string, S32Data>();
            foreach (var kvp in currentMap.FullFileNameList)
            {
                string filePath = kvp.Key;
                var segInfo = kvp.Value;

                if (!segInfo.isS32) continue;
                if (!File.Exists(filePath)) continue;

                var s32 = S32Parser.ParseFile(filePath);
                if (s32 == null) continue;

                s32.FilePath = filePath;
                s32.SegInfo = segInfo;
                s32Files[filePath] = s32;
            }
            sw.Stop();
            Console.WriteLine($" {sw.ElapsedMilliseconds} ms (valid: {s32Files.Count})");

            // 計算地圖範圍
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var s32Data in s32Files.Values)
            {
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                if (loc == null) continue;
                int mx = loc[0];
                int my = loc[1];

                minX = Math.Min(minX, mx);
                minY = Math.Min(minY, my);
                maxX = Math.Max(maxX, mx + L1MapViewer.Helper.MiniMapRenderer.BlockWidth);
                maxY = Math.Max(maxY, my + L1MapViewer.Helper.MiniMapRenderer.BlockHeight);
            }

            int mapWidth = maxX - minX;
            int mapHeight = maxY - minY;
            Console.WriteLine($"Map Size: {mapWidth} x {mapHeight} px");
            Console.WriteLine($"S32 Blocks: {s32Files.Count}");
            Console.WriteLine();

            // 建立渲染器
            var renderer = new L1MapViewer.Helper.MiniMapRenderer();
            var checkedFiles = new HashSet<string>(s32Files.Keys);

            // 執行測試
            var allStats = new List<L1MapViewer.Helper.MiniMapRenderer.RenderStats>();

            for (int i = 0; i < runCount; i++)
            {
                Console.WriteLine($"--- Run {i + 1}/{runCount} ---");

                // 每次清除快取以測試完整渲染時間
                renderer.ClearCache();

                L1MapViewer.Helper.MiniMapRenderer.RenderStats stats;
                using (var bitmap = renderer.RenderMiniMap(mapWidth, mapHeight, targetSize, s32Files, checkedFiles, out stats))
                {
                    // 儲存第一次的結果（轉換成 24bpp 以便正常顯示）
                    if (i == 0)
                    {
                        string outputPath = Path.Combine(mapPath, $"minimap_test.png");
                        using (var bmp24 = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb))
                        {
                            using (var g = Graphics.FromImage(bmp24))
                            {
                                g.DrawImage(bitmap, 0, 0);
                            }
                            bmp24.Save(outputPath, ImageFormat.Png);
                        }
                        Console.WriteLine($"  [Saved]      {outputPath}");
                    }
                }

                string mode = stats.IsSimplified ? "simplified" : "full";
                Console.WriteLine($"  [Mode]       {mode}");
                Console.WriteLine($"  [Scale]      {stats.Scale:F4}");
                Console.WriteLine($"  [Output]     {stats.ScaledWidth}x{stats.ScaledHeight}");
                Console.WriteLine($"  [GetBlock]   {stats.GetBlockMs,5} ms  (blocks: {stats.BlockCount})");
                Console.WriteLine($"  [DrawImage]  {stats.DrawImageMs,5} ms");
                Console.WriteLine($"  [Total]      {stats.TotalMs,5} ms");
                Console.WriteLine();

                allStats.Add(stats);
            }

            // 統計
            Console.WriteLine("=== Summary ===");
            var avgTotal = allStats.Average(s => s.TotalMs);
            var minTotal = allStats.Min(s => s.TotalMs);
            var maxTotal = allStats.Max(s => s.TotalMs);
            Console.WriteLine($"Average: {avgTotal:F0} ms");
            Console.WriteLine($"Min: {minTotal} ms, Max: {maxTotal} ms");

            var avgGetBlock = allStats.Average(s => s.GetBlockMs);
            var avgDrawImage = allStats.Average(s => s.DrawImageMs);
            Console.WriteLine($"Avg GetBlock: {avgGetBlock:F0} ms ({avgGetBlock / avgTotal * 100:F0}%)");
            Console.WriteLine($"Avg DrawImage: {avgDrawImage:F0} ms ({avgDrawImage / avgTotal * 100:F0}%)");

            renderer.ClearCache();
            return 0;
        }

        /// <summary>
        /// 測試 S32 解析效能
        /// </summary>
        private static int CmdBenchmarkS32Parse(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: benchmark-s32parse <map_path> [--runs N] [--parallel]");
                Console.WriteLine("範例: benchmark-s32parse C:\\client\\map\\4");
                Console.WriteLine("      benchmark-s32parse C:\\client\\map\\4 --parallel");
                return 1;
            }

            string mapPath = args[0];
            int runs = 3;
            bool useParallel = false;

            // 解析參數
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--runs" && i + 1 < args.Length)
                {
                    int.TryParse(args[++i], out runs);
                }
                else if (args[i] == "--parallel")
                {
                    useParallel = true;
                }
            }

            if (!Directory.Exists(mapPath))
            {
                Console.WriteLine($"目錄不存在: {mapPath}");
                return 1;
            }

            // 找出所有 S32 檔案
            var s32Files = Directory.GetFiles(mapPath, "*.s32");
            if (s32Files.Length == 0)
            {
                Console.WriteLine($"找不到 S32 檔案: {mapPath}");
                return 1;
            }

            Console.WriteLine($"=== S32 Parse Benchmark ===");
            Console.WriteLine($"Map: {Path.GetFileName(mapPath)}");
            Console.WriteLine($"S32 Files: {s32Files.Length}");
            Console.WriteLine($"Mode: {(useParallel ? "Parallel (seq read + parallel parse)" : "Sequential")}");
            Console.WriteLine($"Runs: {runs}");
            Console.WriteLine();

            var allTimes = new List<(long total, long read, long parse)>();

            for (int run = 1; run <= runs; run++)
            {
                Console.WriteLine($"--- Run {run}/{runs} ---");

                var totalSw = Stopwatch.StartNew();
                long readMs = 0;
                long parseMs = 0;

                if (useParallel)
                {
                    // 模擬 Form 的流程：先順序讀取，再平行解析

                    // 1. 順序讀取所有檔案
                    var readSw = Stopwatch.StartNew();
                    var fileDataList = new List<(string path, byte[] data)>();
                    foreach (var filePath in s32Files)
                    {
                        byte[] data = File.ReadAllBytes(filePath);
                        fileDataList.Add((filePath, data));
                    }
                    readSw.Stop();
                    readMs = readSw.ElapsedMilliseconds;

                    // 2. 平行解析
                    var parseSw = Stopwatch.StartNew();
                    var results = new System.Collections.Concurrent.ConcurrentBag<S32Data>();
                    var parallelOptions = new System.Threading.Tasks.ParallelOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount
                    };
                    System.Threading.Tasks.Parallel.ForEach(fileDataList, parallelOptions, fileData =>
                    {
                        var s32Data = S32Parser.Parse(fileData.data);
                        s32Data.FilePath = fileData.path;
                        results.Add(s32Data);
                    });
                    parseSw.Stop();
                    parseMs = parseSw.ElapsedMilliseconds;
                }
                else
                {
                    // 順序解析
                    foreach (var filePath in s32Files)
                    {
                        var s32Data = S32Parser.ParseFile(filePath);
                    }
                }

                totalSw.Stop();

                if (useParallel)
                {
                    Console.WriteLine($"  [File Read]   {readMs,6} ms");
                    Console.WriteLine($"  [Parse]       {parseMs,6} ms");
                }
                Console.WriteLine($"  [Total]       {totalSw.ElapsedMilliseconds,6} ms");
                Console.WriteLine($"  [Per File]    {(double)totalSw.ElapsedMilliseconds / s32Files.Length:F2} ms/file");
                Console.WriteLine();

                allTimes.Add((totalSw.ElapsedMilliseconds, readMs, parseMs));
            }

            // 統計
            Console.WriteLine($"=== Summary ===");
            var avgTotal = allTimes.Average(t => t.total);
            var minTotal = allTimes.Min(t => t.total);
            var maxTotal = allTimes.Max(t => t.total);

            Console.WriteLine($"Average: {avgTotal:F0} ms");
            Console.WriteLine($"Min: {minTotal} ms, Max: {maxTotal} ms");

            if (useParallel)
            {
                var avgRead = allTimes.Average(t => t.read);
                var avgParse = allTimes.Average(t => t.parse);
                Console.WriteLine($"Avg File Read: {avgRead:F0} ms ({avgRead / avgTotal * 100:F0}%)");
                Console.WriteLine($"Avg Parse: {avgParse:F0} ms ({avgParse / avgTotal * 100:F0}%)");
            }

            Console.WriteLine($"Per File: {avgTotal / s32Files.Length:F2} ms/file");

            return 0;
        }

        /// <summary>
        /// 測試 S32 解析效能（詳細分層計時）
        /// </summary>
        private static int CmdBenchmarkS32ParseDetail(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: benchmark-s32parse-detail <map_path>");
                return 1;
            }

            string mapPath = args[0];

            if (!Directory.Exists(mapPath))
            {
                Console.WriteLine($"目錄不存在: {mapPath}");
                return 1;
            }

            var s32Files = Directory.GetFiles(mapPath, "*.s32");
            if (s32Files.Length == 0)
            {
                Console.WriteLine($"找不到 S32 檔案: {mapPath}");
                return 1;
            }

            Console.WriteLine($"=== S32 Parse Detail Benchmark ===");
            Console.WriteLine($"S32 Files: {s32Files.Length}");
            Console.WriteLine();

            // 先順序讀取所有檔案
            var fileDataList = new List<(string path, byte[] data)>();
            foreach (var filePath in s32Files)
            {
                byte[] data = File.ReadAllBytes(filePath);
                fileDataList.Add((filePath, data));
            }
            Console.WriteLine($"Files loaded into memory");

            // 計時各階段
            long totalLayer1 = 0, totalLayer2 = 0, totalLayer3 = 0, totalLayer4 = 0, totalOther = 0;
            var totalSw = Stopwatch.StartNew();

            foreach (var fileData in fileDataList)
            {
                var data = fileData.data;
                var s32Data = new S32Data();
                s32Data.OriginalFileData = data;

                using (BinaryReader br = new BinaryReader(new MemoryStream(data)))
                {
                    // Layer1
                    var sw1 = Stopwatch.StartNew();
                    s32Data.Layer1Offset = (int)br.BaseStream.Position;
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 128; x++)
                        {
                            int id = br.ReadByte();
                            int til = br.ReadUInt16();
                            int nk = br.ReadByte();
                            s32Data.Layer1[y, x] = new TileCell { X = x, Y = y, TileId = til, IndexId = id };
                            if (!s32Data.UsedTiles.ContainsKey(til))
                                s32Data.UsedTiles[til] = new TileInfo { TileId = til, IndexId = id, UsageCount = 1 };
                            else
                                s32Data.UsedTiles[til].UsageCount++;
                        }
                    }
                    sw1.Stop();
                    totalLayer1 += sw1.ElapsedTicks;

                    // Layer2
                    var sw2 = Stopwatch.StartNew();
                    s32Data.Layer2Offset = (int)br.BaseStream.Position;
                    int layer2Count = br.ReadUInt16();
                    for (int i = 0; i < layer2Count; i++)
                    {
                        s32Data.Layer2.Add(new Layer2Item
                        {
                            X = br.ReadByte(),
                            Y = br.ReadByte(),
                            IndexId = br.ReadByte(),
                            TileId = br.ReadUInt16(),
                            UK = br.ReadByte()
                        });
                    }
                    sw2.Stop();
                    totalLayer2 += sw2.ElapsedTicks;

                    // Layer3
                    var sw3 = Stopwatch.StartNew();
                    s32Data.Layer3Offset = (int)br.BaseStream.Position;
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 64; x++)
                        {
                            s32Data.Layer3[y, x] = new MapAttribute
                            {
                                Attribute1 = br.ReadInt16(),
                                Attribute2 = br.ReadInt16()
                            };
                        }
                    }
                    sw3.Stop();
                    totalLayer3 += sw3.ElapsedTicks;

                    // Layer4
                    var sw4 = Stopwatch.StartNew();
                    s32Data.Layer4Offset = (int)br.BaseStream.Position;
                    int layer4GroupCount = br.ReadInt32();
                    for (int i = 0; i < layer4GroupCount; i++)
                    {
                        int groupId = br.ReadInt16();
                        int blockCount = br.ReadUInt16();
                        for (int j = 0; j < blockCount; j++)
                        {
                            int x = br.ReadByte();
                            int y = br.ReadByte();
                            if (x == 0xCD && y == 0xCD)
                            {
                                br.ReadBytes(5);
                                continue;
                            }
                            int layer = br.ReadByte();
                            int indexId = br.ReadByte();
                            int tileId = br.ReadInt16();
                            int uk = br.ReadByte();
                            s32Data.Layer4.Add(new ObjectTile
                            {
                                GroupId = 0,
                                X = x,
                                Y = y,
                                Layer = layer,
                                IndexId = indexId,
                                TileId = tileId
                            });
                        }
                    }
                    sw4.Stop();
                    totalLayer4 += sw4.ElapsedTicks;
                }
            }
            totalSw.Stop();

            double ticksPerMs = Stopwatch.Frequency / 1000.0;
            long layer1Ms = (long)(totalLayer1 / ticksPerMs);
            long layer2Ms = (long)(totalLayer2 / ticksPerMs);
            long layer3Ms = (long)(totalLayer3 / ticksPerMs);
            long layer4Ms = (long)(totalLayer4 / ticksPerMs);
            long totalMs = totalSw.ElapsedMilliseconds;

            Console.WriteLine($"[Layer1]  {layer1Ms,6} ms  ({layer1Ms * 100.0 / totalMs:F1}%)  - 64x128 TileCell + UsedTiles Dict");
            Console.WriteLine($"[Layer2]  {layer2Ms,6} ms  ({layer2Ms * 100.0 / totalMs:F1}%)");
            Console.WriteLine($"[Layer3]  {layer3Ms,6} ms  ({layer3Ms * 100.0 / totalMs:F1}%)  - 64x64 MapAttribute");
            Console.WriteLine($"[Layer4]  {layer4Ms,6} ms  ({layer4Ms * 100.0 / totalMs:F1}%)");
            Console.WriteLine($"[Total]   {totalMs,6} ms  (sequential)");

            return 0;
        }

        /// <summary>
        /// 測試 Tile 驗證效能（模擬 GetInvalidTileIds）
        /// </summary>
        private static int CmdBenchmarkTileValidate(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: benchmark-tilevalidate <map_path> [--runs N] [--fast]");
                Console.WriteLine("範例: benchmark-tilevalidate C:\\client\\map\\4");
                Console.WriteLine("      benchmark-tilevalidate C:\\client\\map\\4 --fast  (只檢查存在性)");
                return 1;
            }

            string mapPath = args[0];
            int runs = 1;
            bool fastMode = false;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--runs" && i + 1 < args.Length)
                {
                    int.TryParse(args[++i], out runs);
                }
                else if (args[i] == "--fast")
                {
                    fastMode = true;
                }
            }

            if (!Directory.Exists(mapPath))
            {
                Console.WriteLine($"目錄不存在: {mapPath}");
                return 1;
            }

            var s32Files = Directory.GetFiles(mapPath, "*.s32");
            if (s32Files.Length == 0)
            {
                Console.WriteLine($"找不到 S32 檔案: {mapPath}");
                return 1;
            }

            Console.WriteLine($"=== Tile Validate Benchmark ===");
            Console.WriteLine($"Map: {Path.GetFileName(mapPath)}");
            Console.WriteLine($"S32 Files: {s32Files.Length}");
            Console.WriteLine($"Mode: {(fastMode ? "Fast (index lookup only)" : "Full (UnPack + Parse)")}");
            Console.WriteLine($"Runs: {runs}");
            Console.WriteLine();

            // 先解析所有 S32 檔案
            Console.WriteLine("Loading S32 files...");
            var loadSw = Stopwatch.StartNew();
            var s32DataList = new List<S32Data>();
            foreach (var filePath in s32Files)
            {
                s32DataList.Add(S32Parser.ParseFile(filePath));
            }
            loadSw.Stop();
            Console.WriteLine($"Loaded {s32DataList.Count} files in {loadSw.ElapsedMilliseconds}ms");
            Console.WriteLine();

            // 統計 TileId 數量
            int totalLayer1Cells = s32DataList.Count * 64 * 128;
            int totalLayer2Items = s32DataList.Sum(s => s.Layer2.Count);
            int totalLayer4Items = s32DataList.Sum(s => s.Layer4.Count);
            var uniqueTileIds = new HashSet<int>();
            foreach (var s32 in s32DataList)
            {
                foreach (var tile in s32.UsedTiles.Keys)
                    uniqueTileIds.Add(tile);
            }
            Console.WriteLine($"Total Layer1 cells: {totalLayer1Cells:N0}");
            Console.WriteLine($"Total Layer2 items: {totalLayer2Items:N0}");
            Console.WriteLine($"Total Layer4 items: {totalLayer4Items:N0}");
            Console.WriteLine($"Unique TileIds: {uniqueTileIds.Count}");
            Console.WriteLine();

            // 如果是 fast 模式，先載入 Tile 索引
            HashSet<int> availableTileIds = null;
            if (fastMode)
            {
                Console.WriteLine("Loading Tile index...");
                var idxSw = Stopwatch.StartNew();

                // 嘗試從 map 路徑推斷 client 路徑
                string clientPath = mapPath;
                while (!string.IsNullOrEmpty(clientPath))
                {
                    string tileIdxPath = Path.Combine(clientPath, "Tile.idx");
                    if (File.Exists(tileIdxPath))
                    {
                        Share.LineagePath = clientPath;
                        Console.WriteLine($"Found Tile.idx at: {clientPath}");
                        break;
                    }
                    clientPath = Path.GetDirectoryName(clientPath);
                }

                // 觸發載入 Tile 索引（呼叫任何一個 tile 就會載入整個索引）
                L1PakReader.UnPack("Tile", "1.til");

                // 從 Share.IdxDataList 取得所有可用的 TileId
                availableTileIds = new HashSet<int>();
                if (Share.IdxDataList.TryGetValue("Tile", out var tileIdx))
                {
                    foreach (var key in tileIdx.Keys)
                    {
                        // key 格式是 "123.til"
                        if (key.EndsWith(".til"))
                        {
                            string numStr = key.Substring(0, key.Length - 4);
                            if (int.TryParse(numStr, out int tileId))
                            {
                                availableTileIds.Add(tileId);
                            }
                        }
                    }
                }
                idxSw.Stop();
                Console.WriteLine($"Tile index loaded: {availableTileIds.Count} tiles in {idxSw.ElapsedMilliseconds}ms");
                Console.WriteLine();
            }

            var allTimes = new List<long>();

            for (int run = 1; run <= runs; run++)
            {
                Console.WriteLine($"--- Run {run}/{runs} ---");

                int invalidCount = 0;
                int pakReadCount = 0;

                var sw = Stopwatch.StartNew();

                if (fastMode)
                {
                    // Fast mode: 只檢查 TileId 是否存在於索引中
                    foreach (var s32Data in s32DataList)
                    {
                        // 檢查 Layer1
                        for (int y = 0; y < 64; y++)
                        {
                            for (int x = 0; x < 128; x++)
                            {
                                var cell = s32Data.Layer1[y, x];
                                if (cell != null && cell.TileId > 0)
                                {
                                    if (!availableTileIds.Contains(cell.TileId))
                                    {
                                        invalidCount++;
                                    }
                                }
                            }
                        }

                        // 檢查 Layer2
                        foreach (var item in s32Data.Layer2)
                        {
                            if (item.TileId > 0 && !availableTileIds.Contains(item.TileId))
                            {
                                invalidCount++;
                            }
                        }

                        // 檢查 Layer4
                        foreach (var obj in s32Data.Layer4)
                        {
                            if (obj.TileId > 0 && !availableTileIds.Contains(obj.TileId))
                            {
                                invalidCount++;
                            }
                        }
                    }
                }
                else
                {
                    // Full mode: UnPack + Parse 每個 tile
                    var tilCache = new Dictionary<int, (bool exists, int count)>();

                    foreach (var s32Data in s32DataList)
                    {
                        // 檢查 Layer1
                        for (int y = 0; y < 64; y++)
                        {
                            for (int x = 0; x < 128; x++)
                            {
                                var cell = s32Data.Layer1[y, x];
                                if (cell != null && cell.TileId > 0)
                                {
                                    if (!tilCache.TryGetValue(cell.TileId, out var cacheInfo))
                                    {
                                        pakReadCount++;
                                        string key = $"{cell.TileId}.til";
                                        byte[] data = L1PakReader.UnPack("Tile", key);
                                        if (data == null)
                                        {
                                            tilCache[cell.TileId] = (false, 0);
                                            invalidCount++;
                                        }
                                        else
                                        {
                                            var tilArray = L1Til.Parse(data);
                                            tilCache[cell.TileId] = (true, tilArray.Count);
                                            if (cell.IndexId >= tilArray.Count)
                                                invalidCount++;
                                        }
                                    }
                                    else if (!cacheInfo.exists || cell.IndexId >= cacheInfo.count)
                                    {
                                        invalidCount++;
                                    }
                                }
                            }
                        }

                        // 檢查 Layer2
                        foreach (var item in s32Data.Layer2)
                        {
                            if (item.TileId > 0)
                            {
                                if (!tilCache.TryGetValue(item.TileId, out var cacheInfo))
                                {
                                    pakReadCount++;
                                    string key = $"{item.TileId}.til";
                                    byte[] data = L1PakReader.UnPack("Tile", key);
                                    if (data == null)
                                    {
                                        tilCache[item.TileId] = (false, 0);
                                        invalidCount++;
                                    }
                                    else
                                    {
                                        var tilArray = L1Til.Parse(data);
                                        tilCache[item.TileId] = (true, tilArray.Count);
                                        if (item.IndexId >= tilArray.Count)
                                            invalidCount++;
                                    }
                                }
                                else if (!cacheInfo.exists || item.IndexId >= cacheInfo.count)
                                {
                                    invalidCount++;
                                }
                            }
                        }

                        // 檢查 Layer4
                        foreach (var obj in s32Data.Layer4)
                        {
                            if (obj.TileId > 0)
                            {
                                if (!tilCache.TryGetValue(obj.TileId, out var cacheInfo))
                                {
                                    pakReadCount++;
                                    string key = $"{obj.TileId}.til";
                                    byte[] data = L1PakReader.UnPack("Tile", key);
                                    if (data == null)
                                    {
                                        tilCache[obj.TileId] = (false, 0);
                                        invalidCount++;
                                    }
                                    else
                                    {
                                        var tilArray = L1Til.Parse(data);
                                        tilCache[obj.TileId] = (true, tilArray.Count);
                                        if (obj.IndexId >= tilArray.Count)
                                            invalidCount++;
                                    }
                                }
                                else if (!cacheInfo.exists || obj.IndexId >= cacheInfo.count)
                                {
                                    invalidCount++;
                                }
                            }
                        }
                    }
                }

                sw.Stop();
                allTimes.Add(sw.ElapsedMilliseconds);

                Console.WriteLine($"  Time: {sw.ElapsedMilliseconds} ms");
                if (!fastMode) Console.WriteLine($"  Pak Reads: {pakReadCount}");
                Console.WriteLine($"  Invalid: {invalidCount}");
            }

            if (runs > 1)
            {
                Console.WriteLine();
                Console.WriteLine($"=== Summary ===");
                Console.WriteLine($"Average: {allTimes.Average():F0} ms");
                Console.WriteLine($"Min: {allTimes.Min()} ms");
                Console.WriteLine($"Max: {allTimes.Max()} ms");
            }

            return 0;
        }

        // CmdBenchmarkCellFind, CmdBenchmarkMouseClick 已移至 Commands/BenchmarkCommands.cs
    }
}
