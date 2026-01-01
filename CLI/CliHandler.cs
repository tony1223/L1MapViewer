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
using L1MapViewer.CLI.Commands;
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
                    case "clear-l8":
                        return CmdClearL8(cmdArgs);
                    case "tile-stats":
                        return CmdTileStats(cmdArgs);
                    case "scan-tiles":
                        return CmdScanTiles(cmdArgs);
                    case "til-info":
                        return CmdTilInfo(cmdArgs);
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
                    case "test-layer8":
                        return CmdTestLayer8(cmdArgs);
                    case "test-layer8-click":
                        return CmdTestLayer8Click(cmdArgs);
                    case "render-material":
                        return Commands.MaterialCommands.RenderMaterial(cmdArgs);
                    case "verify-material-tiles":
                        return Commands.MaterialCommands.VerifyMaterialTiles(cmdArgs);
                    case "list-til":
                        return CmdListTil(cmdArgs);
                    case "list-idx":
                        return CmdListIdx(cmdArgs);
                    case "validate-tiles":
                        return CmdValidateTiles(cmdArgs);
                    case "export-fs32":
                        return CmdExportFs32(cmdArgs);
                    case "import-fs32":
                        return CmdImportFs32(cmdArgs);
                    case "check-fs32":
                        return CmdCheckFs32(cmdArgs);
                    case "extract-fs32-tile":
                        return CmdExtractFs32Tile(cmdArgs);
                    case "downscale-tile":
                        return CmdDownscaleTile(cmdArgs);
                    case "export-passability":
                        return CmdExportPassability(cmdArgs);
                    case "export-fullmap":
                        return Commands.ExportCommands.ExportFullMap(cmdArgs);
                    case "batch-export":
                        return Commands.ExportCommands.BatchExport(cmdArgs);
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
  check-fs32 <fs32檔案>       檢查 fs32 完整性（Tile 索引、Remaster/Classic 版本）
  export-passability <地圖資料夾> <輸出.txt> [--dir]
                              匯出地圖通行資料為 L1J/DIR 格式（預設 L1J）
  export-fullmap <地圖資料夾> <輸出.png> [選項]
                              匯出單張地圖全圖為 PNG/JPG
  batch-export <map目錄> <輸出目錄> [選項]
                              批次匯出所有地圖全圖
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
        /// list-idx 命令 - 列出 idx 檔案中的內容（支援 _EXTB$ 格式）
        /// </summary>
        private static int CmdListIdx(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli list-idx <idx檔案路徑> [--limit <數量>]");
                Console.WriteLine();
                Console.WriteLine("列出 idx 檔案中的所有條目");
                Console.WriteLine("支援格式: OLD, _EXT, _RMS, _EXTB$");
                return 1;
            }

            string idxPath = args[0];
            int limit = 50;

            // 解析 --limit 參數
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--limit" && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out limit);
                }
            }

            if (!File.Exists(idxPath))
            {
                Console.WriteLine($"錯誤: 檔案不存在: {idxPath}");
                return 1;
            }

            byte[] data = File.ReadAllBytes(idxPath);
            Console.WriteLine($"=== IDX 檔案資訊 ===");
            Console.WriteLine($"檔案: {Path.GetFileName(idxPath)}");
            Console.WriteLine($"大小: {data.Length:N0} bytes");

            // 檢測格式
            string format = "OLD";
            if (data.Length >= 6 && data[0] == '_' && data[1] == 'E' && data[2] == 'X' &&
                data[3] == 'T' && data[4] == 'B' && data[5] == '$')
            {
                format = "_EXTB$";
            }
            else if (data.Length >= 4)
            {
                string header = Encoding.Default.GetString(data, 0, 4);
                if (header.ToLower() == "_ext") format = "_EXT";
                else if (header.ToLower() == "_rms") format = "_RMS";
            }

            Console.WriteLine($"格式: {format}");
            Console.WriteLine($"Header: {string.Join(" ", data.Take(16).Select(b => $"{b:X2}"))}");
            Console.WriteLine();

            if (format == "_EXTB$")
            {
                // 解析 _EXTB$ 格式
                const int headerSize = 0x10;
                const int entrySize = 0x80;
                int entryCount = (data.Length - headerSize) / entrySize;

                Console.WriteLine($"Entry 數量: {entryCount}");
                Console.WriteLine();
                Console.WriteLine($"{"#",-6} {"Filename",-25} {"Offset",12} {"Size",10} {"Compression",-10}");
                Console.WriteLine(new string('-', 70));

                int shown = 0;
                int zlibCount = 0, brotliCount = 0, noneCount = 0;

                for (int i = 0; i < entryCount; i++)
                {
                    int entryOffset = headerSize + i * entrySize;
                    int pakOffset = BitConverter.ToInt32(data, entryOffset + 120);
                    int compression = BitConverter.ToInt32(data, entryOffset + 4);
                    int uncompressedSize = BitConverter.ToInt32(data, entryOffset + 124);

                    // 統計壓縮類型
                    if (compression == 1) zlibCount++;
                    else if (compression == 2) brotliCount++;
                    else noneCount++;

                    // 讀取檔案名稱
                    int nameStart = entryOffset + 8;
                    int nameEnd = nameStart;
                    while (nameEnd < entryOffset + 120 && data[nameEnd] != 0)
                        nameEnd++;

                    string fileName = nameEnd > nameStart
                        ? Encoding.Default.GetString(data, nameStart, nameEnd - nameStart)
                        : "(empty)";

                    string compStr = compression switch
                    {
                        0 => "none",
                        1 => "zlib",
                        2 => "brotli",
                        _ => $"unknown({compression})"
                    };

                    if (shown < limit)
                    {
                        Console.WriteLine($"{i,-6} {fileName,-25} {pakOffset,12:N0} {uncompressedSize,10:N0} {compStr,-10}");
                        shown++;
                    }
                }

                if (entryCount > limit)
                {
                    Console.WriteLine($"... (共 {entryCount} 個條目，僅顯示前 {limit} 個)");
                }

                Console.WriteLine();
                Console.WriteLine("=== 壓縮統計 ===");
                Console.WriteLine($"  None:   {noneCount,6} ({100.0 * noneCount / entryCount:F1}%)");
                Console.WriteLine($"  Zlib:   {zlibCount,6} ({100.0 * zlibCount / entryCount:F1}%)");
                Console.WriteLine($"  Brotli: {brotliCount,6} ({100.0 * brotliCount / entryCount:F1}%)");
            }
            else
            {
                // 使用 L1IdxReader 解析其他格式
                string pakPath = idxPath.Replace(".idx", ".pak");
                Console.WriteLine($"PAK: {pakPath}");
                Console.WriteLine();

                // 設定路徑並讀取
                string idxType = Path.GetFileNameWithoutExtension(idxPath);
                string parentDir = Path.GetDirectoryName(idxPath);
                Share.LineagePath = parentDir;

                // 清除快取
                if (Share.IdxDataList.ContainsKey(idxType))
                    Share.IdxDataList.Remove(idxType);

                var idxData = L1IdxReader.GetAll(idxType);
                Console.WriteLine($"Entry 數量: {idxData.Count}");
                Console.WriteLine();

                int shown = 0;
                foreach (var kvp in idxData.OrderBy(x => x.Key))
                {
                    if (shown >= limit) break;
                    var idx = kvp.Value;
                    string compStr = idx.nCompressType switch
                    {
                        0 => "none",
                        1 => "zlib",
                        2 => "brotli",
                        _ => $"unknown({idx.nCompressType})"
                    };
                    Console.WriteLine($"{kvp.Key,-30} pos={idx.nPosition,10} size={idx.nSize,10} comp={compStr}");
                    shown++;
                }

                if (idxData.Count > limit)
                {
                    Console.WriteLine($"... (共 {idxData.Count} 個條目，僅顯示前 {limit} 個)");
                }
            }

            return 0;
        }

        /// <summary>
        /// validate-tiles 命令 - 驗證 S32 中使用的 TileId 是否存在於 Tile.idx 中
        /// </summary>
        private static int CmdValidateTiles(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli validate-tiles <s32檔案或地圖資料夾> [--client <路徑>] [--detail]");
                Console.WriteLine();
                Console.WriteLine("驗證 S32 中使用的 TileId 是否存在於 Tile.idx 中");
                Console.WriteLine();
                Console.WriteLine("參數:");
                Console.WriteLine("  <s32檔案或地圖資料夾>  要驗證的 S32 檔案或包含 S32 的地圖資料夾");
                Console.WriteLine("  --client <路徑>        指定客戶端路徑（包含 Tile.idx/pak）");
                Console.WriteLine("                         若不指定，會自動從輸入路徑向上搜尋");
                Console.WriteLine("  --detail               顯示每個 Tile 的詳細資訊（存在、大小、block數）");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  validate-tiles C:\\client\\map\\4\\7fff8000.s32");
                Console.WriteLine("  validate-tiles C:\\client\\map\\4");
                Console.WriteLine("  validate-tiles C:\\map\\4 --client C:\\client --detail");
                return 1;
            }

            string inputPath = args[0];
            string clientPath = null;
            bool showDetail = args.Contains("--detail");

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

            // 顯示詳細資訊
            if (showDetail)
            {
                Console.WriteLine("=== Layer6 Tile 詳細列表 ===");
                Console.WriteLine($"{"TileId",-10} {"狀態",-8} {"Blocks",-8} {"大小",-12} {"備註"}");
                Console.WriteLine(new string('-', 60));

                foreach (var kvp in tileBlockCounts.OrderBy(k => k.Key))
                {
                    int tileId = kvp.Key;
                    int blockCount = kvp.Value;
                    string status = blockCount >= 0 ? "✓ 存在" : "✗ 缺失";
                    string blocks = blockCount >= 0 ? blockCount.ToString() : "-";

                    // 取得檔案大小
                    string sizeStr = "-";
                    if (blockCount >= 0)
                    {
                        byte[] tilData = L1PakReader.UnPack("Tile", $"{tileId}.til");
                        if (tilData != null)
                            sizeStr = $"{tilData.Length:N0} bytes";
                    }

                    string note = "";
                    if (blockCount < 0)
                        note = "Tile.idx 中不存在或無法讀取";
                    else if (blockCount == 0)
                        note = "Block 數為 0（空 Tile）";

                    Console.WriteLine($"{tileId,-10} {status,-8} {blocks,-8} {sizeStr,-12} {note}");
                }
                Console.WriteLine();
            }

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
                Console.WriteLine("用法: -cli export-fs32 <地圖資料夾> <輸出.fs32> [選項]");
                Console.WriteLine();
                Console.WriteLine("將地圖匯出為 fs32 格式");
                Console.WriteLine();
                Console.WriteLine("參數:");
                Console.WriteLine("  <地圖資料夾>    包含 S32 檔案的地圖資料夾");
                Console.WriteLine("  <輸出.fs32>     輸出的 fs32 檔案路徑");
                Console.WriteLine();
                Console.WriteLine("選項:");
                Console.WriteLine("  --downscale     將 Remaster 版 Tile (48x48) 降級為 Classic 版 (24x24)");
                Console.WriteLine("  --no-l8ext      移除 Layer8 擴展資料");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  export-fs32 C:\\client\\map\\100002 C:\\output\\100002.fs32");
                Console.WriteLine("  export-fs32 C:\\client\\map\\100002 C:\\output\\100002.fs32 --downscale");
                Console.WriteLine("  export-fs32 C:\\client\\map\\100002 C:\\output\\100002.fs32 --no-l8ext");
                return 1;
            }

            string mapPath = args[0];
            string outputPath = args[1];
            bool downscale = args.Contains("--downscale");
            bool noL8Ext = args.Contains("--no-l8ext");

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
            Console.WriteLine($"移除 L8 擴展: {(noL8Ext ? "是" : "否")}");
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

                    // 移除 Layer8 擴展資料
                    if (noL8Ext && s32Data.Layer8HasExtendedData)
                    {
                        s32Data.Layer8HasExtendedData = false;
                        foreach (var item in s32Data.Layer8)
                        {
                            item.ExtendedData = 0;
                        }
                        s32Data.OriginalFileData = null; // 強制重新序列化
                    }

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
        /// check-fs32 命令 - 檢查 fs32 完整性
        /// </summary>
        private static int CmdCheckFs32(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli check-fs32 <fs32檔案>");
                Console.WriteLine();
                Console.WriteLine("檢查 fs32 檔案的完整性：");
                Console.WriteLine("  - S32 區塊使用的 TileId 是否都包含在 fs32 中");
                Console.WriteLine("  - IndexId 是否在對應 Tile 的 block 數量範圍內");
                Console.WriteLine("  - 各 Tile 是 Remaster (48x48) 還是 Classic (24x24) 版本");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  check-fs32 C:\\100002.fs32");
                return 1;
            }

            string fs32Path = args[0];

            if (!File.Exists(fs32Path))
            {
                Console.WriteLine($"錯誤: fs32 檔案不存在: {fs32Path}");
                return 1;
            }

            Console.WriteLine("=== 檢查 fs32 ===");
            Console.WriteLine($"檔案: {fs32Path}");
            Console.WriteLine();

            var sw = Stopwatch.StartNew();

            // 載入 fs32
            Console.WriteLine("載入 fs32...");
            var fs32 = Fs32Parser.ParseFile(fs32Path);
            if (fs32 == null)
            {
                Console.WriteLine("錯誤: 無法解析 fs32 檔案");
                return 1;
            }

            Console.WriteLine($"來源地圖: {fs32.SourceMapId}");
            Console.WriteLine($"區塊數量: {fs32.Blocks.Count}");
            Console.WriteLine($"包含 Tile 數量: {fs32.Tiles.Count}");
            Console.WriteLine();

            // 建立 fs32 內 Tile 的 block 數量索引
            Console.WriteLine("解析 Tile 資訊...");
            var tileBlockCounts = new Dictionary<int, int>();
            var tileVersions = new Dictionary<int, L1Til.TileVersion>();

            int remasterCount = 0;
            int classicCount = 0;
            int hybridCount = 0;
            int unknownCount = 0;

            foreach (var kvp in fs32.Tiles)
            {
                int tileId = kvp.Key;
                byte[] tilData = kvp.Value.TilData;

                // 取得 block 數量 (第一個 int32 是 block count)
                int blockCount = tilData.Length >= 4 ? BitConverter.ToInt32(tilData, 0) : 0;
                tileBlockCounts[tileId] = blockCount;

                // 取得版本
                var version = L1Til.GetVersion(tilData);
                tileVersions[tileId] = version;

                switch (version)
                {
                    case L1Til.TileVersion.Remaster:
                        remasterCount++;
                        break;
                    case L1Til.TileVersion.Classic:
                        classicCount++;
                        break;
                    case L1Til.TileVersion.Hybrid:
                        hybridCount++;
                        break;
                    default:
                        unknownCount++;
                        break;
                }
            }

            Console.WriteLine();
            Console.WriteLine("=== Tile 版本統計 ===");
            Console.WriteLine($"  Remaster (48x48): {remasterCount}");
            Console.WriteLine($"  Classic (24x24):  {classicCount}");
            if (hybridCount > 0)
                Console.WriteLine($"  Hybrid (混合):    {hybridCount}");
            if (unknownCount > 0)
                Console.WriteLine($"  Unknown (未知):   {unknownCount}");

            // 列出各版本的 TileId
            if (remasterCount > 0)
            {
                var remasterTiles = tileVersions.Where(kv => kv.Value == L1Til.TileVersion.Remaster)
                                                 .Select(kv => kv.Key).OrderBy(t => t).ToList();
                Console.WriteLine();
                Console.WriteLine($"Remaster TileIds ({remasterTiles.Count}):");
                Console.WriteLine($"  {string.Join(", ", remasterTiles)}");
            }
            if (hybridCount > 0)
            {
                var hybridTiles = tileVersions.Where(kv => kv.Value == L1Til.TileVersion.Hybrid)
                                               .Select(kv => kv.Key).OrderBy(t => t).ToList();
                Console.WriteLine();
                Console.WriteLine($"Hybrid TileIds ({hybridTiles.Count}):");
                Console.WriteLine($"  {string.Join(", ", hybridTiles)}");
            }
            if (unknownCount > 0)
            {
                var unknownTiles = tileVersions.Where(kv => kv.Value == L1Til.TileVersion.Unknown)
                                                .Select(kv => kv.Key).OrderBy(t => t).ToList();
                Console.WriteLine();
                Console.WriteLine($"Unknown TileIds ({unknownTiles.Count}):");
                Console.WriteLine($"  {string.Join(", ", unknownTiles)}");
            }
            Console.WriteLine();

            // 解析 S32 區塊，收集使用的 TileId 和 IndexId
            Console.WriteLine("檢查 S32 區塊的 Tile 參照...");
            var usedTileIndexes = new Dictionary<int, HashSet<int>>(); // TileId -> 使用的 IndexIds
            int totalCells = 0;

            foreach (var block in fs32.Blocks)
            {
                var s32Data = S32Parser.Parse(block.S32Data);
                if (s32Data == null)
                {
                    Console.WriteLine($"警告: 無法解析區塊 {block.BlockX:X4}{block.BlockY:X4}");
                    continue;
                }

                // Layer1
                if (s32Data.Layer1 != null)
                {
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 128; x++)
                        {
                            var cell = s32Data.Layer1[y, x];
                            if (cell?.TileId > 0)
                            {
                                if (!usedTileIndexes.TryGetValue(cell.TileId, out var indexes))
                                {
                                    indexes = new HashSet<int>();
                                    usedTileIndexes[cell.TileId] = indexes;
                                }
                                indexes.Add(cell.IndexId);
                                totalCells++;
                            }
                        }
                    }
                }

                // Layer2
                foreach (var item in s32Data.Layer2)
                {
                    if (item.TileId > 0)
                    {
                        if (!usedTileIndexes.TryGetValue(item.TileId, out var indexes))
                        {
                            indexes = new HashSet<int>();
                            usedTileIndexes[item.TileId] = indexes;
                        }
                        indexes.Add(item.IndexId);
                        totalCells++;
                    }
                }

                // Layer4
                foreach (var obj in s32Data.Layer4)
                {
                    if (obj.TileId > 0)
                    {
                        if (!usedTileIndexes.TryGetValue(obj.TileId, out var indexes))
                        {
                            indexes = new HashSet<int>();
                            usedTileIndexes[obj.TileId] = indexes;
                        }
                        indexes.Add(obj.IndexId);
                        totalCells++;
                    }
                }
            }

            Console.WriteLine($"S32 使用的不同 Tile 數量: {usedTileIndexes.Count}");
            Console.WriteLine($"總共 {totalCells} 個 Tile 參照");
            Console.WriteLine();

            // 檢查 TileId 是否存在於 fs32 中
            Console.WriteLine("=== 驗證結果 ===");
            var missingTiles = new List<int>();
            var invalidIndexes = new List<(int TileId, int IndexId, int MaxIndex)>();

            foreach (var kvp in usedTileIndexes)
            {
                int tileId = kvp.Key;
                var usedIndexes = kvp.Value;

                if (!fs32.Tiles.ContainsKey(tileId))
                {
                    missingTiles.Add(tileId);
                }
                else
                {
                    int blockCount = tileBlockCounts[tileId];
                    foreach (int indexId in usedIndexes)
                    {
                        if (indexId < 0 || indexId >= blockCount)
                        {
                            invalidIndexes.Add((tileId, indexId, blockCount));
                        }
                    }
                }
            }

            if (missingTiles.Count == 0 && invalidIndexes.Count == 0)
            {
                Console.WriteLine("✓ 所有 TileId 和 IndexId 都有效，fs32 完整！");
            }
            else
            {
                if (missingTiles.Count > 0)
                {
                    Console.WriteLine($"✗ 缺少 {missingTiles.Count} 個 Tile:");
                    foreach (int tileId in missingTiles.OrderBy(t => t).Take(20))
                    {
                        Console.WriteLine($"    TileId {tileId}");
                    }
                    if (missingTiles.Count > 20)
                    {
                        Console.WriteLine($"    ... 還有 {missingTiles.Count - 20} 個");
                    }
                }

                if (invalidIndexes.Count > 0)
                {
                    Console.WriteLine($"✗ {invalidIndexes.Count} 個 IndexId 超出範圍:");
                    foreach (var (tileId, indexId, maxIndex) in invalidIndexes.Take(20))
                    {
                        Console.WriteLine($"    TileId {tileId}: IndexId {indexId} >= {maxIndex}");
                    }
                    if (invalidIndexes.Count > 20)
                    {
                        Console.WriteLine($"    ... 還有 {invalidIndexes.Count - 20} 個");
                    }
                }
            }

            sw.Stop();
            Console.WriteLine();
            Console.WriteLine($"耗時: {sw.ElapsedMilliseconds}ms");

            return (missingTiles.Count > 0 || invalidIndexes.Count > 0) ? 1 : 0;
        }

        /// <summary>
        /// extract-fs32-tile 命令 - 從 fs32 提取特定 Tile
        /// </summary>
        private static int CmdExtractFs32Tile(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法: -cli extract-fs32-tile <fs32檔案> <tileId> [輸出資料夾]");
                Console.WriteLine();
                Console.WriteLine("從 fs32 檔案中提取特定的 Tile");
                Console.WriteLine();
                Console.WriteLine("參數:");
                Console.WriteLine("  <fs32檔案>     要讀取的 fs32 檔案");
                Console.WriteLine("  <tileId>       要提取的 TileId");
                Console.WriteLine("  [輸出資料夾]   可選，預設為目前目錄");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  extract-fs32-tile C:\\100002.fs32 10035");
                Console.WriteLine("  extract-fs32-tile C:\\100002.fs32 10035 C:\\output");
                return 1;
            }

            string fs32Path = args[0];
            if (!int.TryParse(args[1], out int tileId))
            {
                Console.WriteLine($"無效的 TileId: {args[1]}");
                return 1;
            }
            string outputFolder = args.Length >= 3 ? args[2] : Directory.GetCurrentDirectory();

            if (!File.Exists(fs32Path))
            {
                Console.WriteLine($"錯誤: fs32 檔案不存在: {fs32Path}");
                return 1;
            }

            Console.WriteLine("=== 提取 fs32 Tile ===");
            Console.WriteLine($"fs32: {fs32Path}");
            Console.WriteLine($"TileId: {tileId}");
            Console.WriteLine($"輸出: {outputFolder}");
            Console.WriteLine();

            // 載入 fs32
            var fs32 = Fs32Parser.ParseFile(fs32Path);
            if (fs32 == null)
            {
                Console.WriteLine("錯誤: 無法解析 fs32 檔案");
                return 1;
            }

            // 檢查 Tile 是否存在
            if (!fs32.Tiles.TryGetValue(tileId, out var tileData))
            {
                Console.WriteLine($"錯誤: fs32 中不包含 TileId {tileId}");
                Console.WriteLine($"可用的 TileId: {string.Join(", ", fs32.Tiles.Keys.OrderBy(k => k).Take(20))}...");
                return 1;
            }

            byte[] tilData = tileData.TilData;
            var version = L1Til.GetVersion(tilData);
            int blockCount = tilData.Length >= 4 ? BitConverter.ToInt32(tilData, 0) : 0;

            Console.WriteLine($"Tile 大小: {tilData.Length} bytes");
            Console.WriteLine($"版本: {version}");
            Console.WriteLine($"Block 數量: {blockCount}");
            Console.WriteLine();

            // 建立輸出資料夾
            string tileOutputFolder = Path.Combine(outputFolder, $"fs32_tile_{tileId}");
            Directory.CreateDirectory(tileOutputFolder);

            // 輸出 .til 檔案
            string tilOutputPath = Path.Combine(tileOutputFolder, $"{tileId}.til");
            File.WriteAllBytes(tilOutputPath, tilData);
            Console.WriteLine($"已輸出: {tilOutputPath}");

            // 解析各個 block
            var blocks = L1Til.Parse(tilData);
            Console.WriteLine($"解析到 {blocks.Count} 個 block");

            // 分析 block 尺寸統計
            var (classic, remaster, hybrid, unknown) = L1Til.AnalyzeTilBlocks(tilData);
            Console.WriteLine();
            Console.WriteLine("=== Block 尺寸分析 ===");
            Console.WriteLine($"  24x24 (Classic):  {classic}");
            Console.WriteLine($"  48x48 (Remaster): {remaster}");
            Console.WriteLine($"  48x48 (Hybrid):   {hybrid}");
            Console.WriteLine($"  Unknown:          {unknown}");

            // 顯示每個 block 的詳細資訊
            Console.WriteLine();
            Console.WriteLine("Block 詳細資訊:");
            for (int i = 0; i < Math.Min(blocks.Count, 10); i++)
            {
                var analysis = L1Til.AnalyzeBlock(blocks[i]);
                if (analysis.IsSimpleDiamond)
                {
                    Console.WriteLine($"  [{i:D3}] type={analysis.Type}, size={analysis.Size}b, format={analysis.Format}");
                }
                else
                {
                    Console.WriteLine($"  [{i:D3}] type={analysis.Type}, size={analysis.Size}b, " +
                        $"offset=({analysis.XOffset},{analysis.YOffset}), len=({analysis.XxLen},{analysis.YLen}), " +
                        $"max=({analysis.MaxX},{analysis.MaxY}), format={analysis.Format}");
                }
            }
            if (blocks.Count > 10)
            {
                Console.WriteLine($"  ... 還有 {blocks.Count - 10} 個 block");
            }
            return 0;
        }

        /// <summary>
        /// downscale-tile 命令 - 診斷並降級單一 Tile
        /// </summary>
        private static int CmdDownscaleTile(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli downscale-tile <tileId|til檔案路徑> [輸出資料夾]");
                Console.WriteLine();
                Console.WriteLine("診斷並降級單一 Tile（從 Remaster 48x48 降為 Classic 24x24）");
                Console.WriteLine();
                Console.WriteLine("參數:");
                Console.WriteLine("  <tileId>       從 Tile.pak 讀取的 TileId（需設定 LineagePath）");
                Console.WriteLine("  <til檔案>      直接讀取 .til 檔案");
                Console.WriteLine("  [輸出資料夾]   可選，預設為目前目錄");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  downscale-tile 4703");
                Console.WriteLine("  downscale-tile C:\\tiles\\4703.til C:\\output");
                return 1;
            }

            string input = args[0];
            string outputFolder = args.Length >= 2 ? args[1] : Directory.GetCurrentDirectory();
            byte[] tilData = null;
            int tileId = 0;

            // 判斷是 tileId 還是檔案路徑
            if (int.TryParse(input, out tileId))
            {
                // 從 pak 讀取
                Console.WriteLine($"=== 降級 Tile {tileId} ===");
                tilData = L1PakReader.UnPack("Tile", $"{tileId}.til");
                if (tilData == null)
                {
                    Console.WriteLine($"錯誤: 無法從 Tile.pak 讀取 {tileId}.til");
                    return 1;
                }
                Console.WriteLine($"來源: Tile.pak");
            }
            else if (File.Exists(input))
            {
                // 從檔案讀取
                tilData = File.ReadAllBytes(input);
                tileId = 0;
                string fileName = Path.GetFileNameWithoutExtension(input);
                int.TryParse(fileName, out tileId);
                Console.WriteLine($"=== 降級 Tile ===");
                Console.WriteLine($"來源: {input}");
            }
            else
            {
                Console.WriteLine($"錯誤: 找不到檔案或無效的 TileId: {input}");
                return 1;
            }

            Console.WriteLine($"原始大小: {tilData.Length} bytes");
            Console.WriteLine();

            // === 診斷 GetVersion ===
            Console.WriteLine("=== GetVersion 診斷 ===");

            int blockCount = tilData.Length >= 4 ? BitConverter.ToInt32(tilData, 0) : 0;
            Console.WriteLine($"Block 數量: {blockCount}");

            if (tilData.Length >= 12)
            {
                int offset0 = BitConverter.ToInt32(tilData, 4);
                int offset1 = BitConverter.ToInt32(tilData, 8);
                int firstBlockSize = offset1 - offset0;
                Console.WriteLine($"第一個 Block 大小: {firstBlockSize} bytes");

                // 判斷邏輯
                if (firstBlockSize >= 1800 && firstBlockSize <= 3500)
                {
                    Console.WriteLine($"  → 1800 <= {firstBlockSize} <= 3500 → Remaster");
                }
                else if (firstBlockSize >= 10 && firstBlockSize <= 1800)
                {
                    Console.WriteLine($"  → 10 <= {firstBlockSize} <= 1800 → 需檢查所有 blocks");
                }
                else
                {
                    Console.WriteLine($"  → {firstBlockSize} 超出範圍 (10-3500) → Unknown ← 問題在這!");
                }
            }

            var version = L1Til.GetVersion(tilData);
            bool isRemaster = L1Til.IsRemaster(tilData);
            Console.WriteLine($"GetVersion 結果: {version}");
            Console.WriteLine($"IsRemaster: {isRemaster}");
            Console.WriteLine();

            // === Block 分析 ===
            Console.WriteLine("=== Block 分析 ===");
            var blocks = L1Til.Parse(tilData);
            var (classic, remaster, hybrid, unknown) = L1Til.AnalyzeTilBlocks(tilData);
            Console.WriteLine($"  24x24 (Classic):  {classic}");
            Console.WriteLine($"  48x48 (Remaster): {remaster}");
            Console.WriteLine($"  48x48 (Hybrid):   {hybrid}");
            Console.WriteLine($"  Unknown:          {unknown}");
            Console.WriteLine();

            // 顯示前幾個 block
            Console.WriteLine("前 5 個 Block:");
            for (int i = 0; i < Math.Min(blocks.Count, 5); i++)
            {
                var analysis = L1Til.AnalyzeBlock(blocks[i]);
                if (analysis.IsSimpleDiamond)
                {
                    Console.WriteLine($"  [{i:D3}] type={analysis.Type}, size={analysis.Size}b, format={analysis.Format}");
                }
                else
                {
                    Console.WriteLine($"  [{i:D3}] type={analysis.Type}, size={analysis.Size}b, " +
                        $"offset=({analysis.XOffset},{analysis.YOffset}), len=({analysis.XxLen},{analysis.YLen}), " +
                        $"max=({analysis.MaxX},{analysis.MaxY}), format={analysis.Format}");
                }
            }
            Console.WriteLine();

            // === 嘗試降級 ===
            Console.WriteLine("=== 嘗試降級 ===");
            if (!isRemaster)
            {
                Console.WriteLine("IsRemaster=false，不會進行降級");
                Console.WriteLine("這就是為什麼這個 tile 沒有被降級!");
                return 0;
            }

            byte[] downscaled = L1Til.DownscaleTil(tilData);
            Console.WriteLine($"降級後大小: {downscaled.Length} bytes");

            var newVersion = L1Til.GetVersion(downscaled);
            var (newClassic, newRemaster, newHybrid, newUnknown) = L1Til.AnalyzeTilBlocks(downscaled);
            Console.WriteLine($"降級後版本: {newVersion}");
            Console.WriteLine($"降級後 Block 分析:");
            Console.WriteLine($"  24x24 (Classic):  {newClassic}");
            Console.WriteLine($"  48x48 (Remaster): {newRemaster}");
            Console.WriteLine($"  48x48 (Hybrid):   {newHybrid}");
            Console.WriteLine($"  Unknown:          {newUnknown}");

            // 輸出檔案
            Directory.CreateDirectory(outputFolder);
            string outputPath = Path.Combine(outputFolder, $"{tileId}_downscaled.til");
            File.WriteAllBytes(outputPath, downscaled);
            Console.WriteLine();
            Console.WriteLine($"已輸出: {outputPath}");

            return 0;
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
        /// clear-l8 命令 - 清空 S32 的 Layer8 並寫回
        /// </summary>
        private static int CmdClearL8(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli clear-l8 <s32檔案>");
                return 1;
            }

            string filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"檔案不存在: {filePath}");
                return 1;
            }

            var s32 = S32Parser.ParseFile(filePath);
            int originalCount = s32.Layer8.Count;

            if (originalCount == 0)
            {
                Console.WriteLine("Layer8 已經是空的，無需處理");
                return 0;
            }

            Console.WriteLine($"原始 Layer8 數量: {originalCount}");
            s32.Layer8.Clear();

            // 寫回檔案
            S32Writer.Write(s32, filePath);
            Console.WriteLine($"已清空 Layer8 並寫回: {filePath}");

            return 0;
        }

        /// <summary>
        /// tile-stats 命令 - 統計地圖使用的 Tile Block Type
        /// </summary>
        private static int CmdTileStats(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli tile-stats <地圖資料夾> [--client <路徑>]");
                Console.WriteLine("統計地圖使用的所有 Tile 的 Block Type 分布");
                return 1;
            }

            string mapPath = args[0];
            string clientPath = null;

            // 解析參數
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--client" && i + 1 < args.Length)
                    clientPath = args[++i];
            }

            bool isSingleFile = File.Exists(mapPath) && mapPath.EndsWith(".s32", StringComparison.OrdinalIgnoreCase);
            if (!isSingleFile && !Directory.Exists(mapPath))
            {
                Console.WriteLine($"地圖資料夾或 S32 檔案不存在: {mapPath}");
                return 1;
            }

            // 自動搜尋 client 路徑
            if (string.IsNullOrEmpty(clientPath))
            {
                string searchPath = isSingleFile ? Path.GetDirectoryName(mapPath) : mapPath;
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

            Console.WriteLine($"=== Tile Block Type 統計 ===");
            Console.WriteLine($"地圖: {mapPath}");
            Console.WriteLine($"客戶端: {clientPath}");
            Console.WriteLine();

            // 設定客戶端路徑
            Share.LineagePath = clientPath;
            if (Share.IdxDataList.ContainsKey("Tile"))
                Share.IdxDataList.Remove("Tile");
            L1PakReader.UnPack("Tile", "1.til");

            // 收集所有 S32 使用的 TileId
            var allTileIds = new HashSet<int>();
            var s32Files = isSingleFile
                ? new[] { mapPath }
                : Directory.GetFiles(mapPath, "*.s32");

            Console.WriteLine($"掃描 {s32Files.Length} 個 S32 檔案...");

            // 正常的 Block Type
            var normalTypes = new HashSet<byte> { 0, 1, 2, 3, 6, 7, 8, 9, 16, 17, 18, 19, 22, 23, 34, 35 };

            foreach (var s32File in s32Files)
            {
                try
                {
                    var s32 = S32Parser.ParseFile(s32File);

                    // Layer1
                    for (int y = 0; y < 64; y++)
                        for (int x = 0; x < 128; x++)
                            if (s32.Layer1[y, x]?.TileId > 0)
                                allTileIds.Add(s32.Layer1[y, x].TileId);

                    // Layer2
                    foreach (var item in s32.Layer2)
                        if (item.TileId > 0)
                            allTileIds.Add(item.TileId);

                    // Layer4
                    foreach (var item in s32.Layer4)
                        if (item.TileId > 0)
                            allTileIds.Add(item.TileId);
                }
                catch { }
            }

            Console.WriteLine($"找到 {allTileIds.Count} 個不同的 TileId");
            Console.WriteLine();

            // 統計 Block Type
            var blockTypeStats = new Dictionary<byte, int>();
            var tileVersionStats = new Dictionary<string, int>
            {
                { "Classic", 0 },
                { "Remaster", 0 },
                { "Hybrid", 0 },
                { "Unknown", 0 }
            };
            int totalBlocks = 0;
            int tilesAnalyzed = 0;

            // 追蹤異常 Tile（包含非正常 Block Type）
            var abnormalTiles = new Dictionary<int, List<byte>>(); // TileId -> 異常的 Block Types

            Console.WriteLine("分析 Tile Block Type...");

            foreach (var tileId in allTileIds.OrderBy(x => x))
            {
                byte[] tilData = L1PakReader.UnPack("Tile", $"{tileId}.til");
                if (tilData == null || tilData.Length < 8)
                    continue;

                tilesAnalyzed++;

                // 判斷 Tile 版本
                var version = Converter.L1Til.GetVersion(tilData);
                tileVersionStats[version.ToString()]++;

                // 解析 blocks
                var blocks = Converter.L1Til.Parse(tilData);
                var tileAbnormalTypes = new HashSet<byte>();

                foreach (var block in blocks)
                {
                    if (block == null || block.Length < 1)
                        continue;

                    byte blockType = block[0];
                    if (!blockTypeStats.ContainsKey(blockType))
                        blockTypeStats[blockType] = 0;
                    blockTypeStats[blockType]++;
                    totalBlocks++;

                    // 檢查是否為異常類型
                    if (!normalTypes.Contains(blockType))
                    {
                        tileAbnormalTypes.Add(blockType);
                    }
                }

                if (tileAbnormalTypes.Count > 0)
                {
                    abnormalTiles[tileId] = tileAbnormalTypes.OrderBy(x => x).ToList();
                }
            }

            // 輸出統計結果
            Console.WriteLine($"\n=== Tile 版本統計 ===");
            Console.WriteLine($"{"版本",-12} {"數量",-8} {"百分比"}");
            Console.WriteLine(new string('-', 35));
            foreach (var kvp in tileVersionStats.OrderByDescending(x => x.Value))
            {
                double pct = tilesAnalyzed > 0 ? (double)kvp.Value / tilesAnalyzed * 100 : 0;
                Console.WriteLine($"{kvp.Key,-12} {kvp.Value,-8} {pct:F1}%");
            }
            Console.WriteLine($"{"總計",-12} {tilesAnalyzed,-8}");

            Console.WriteLine($"\n=== Block Type 統計 ===");
            Console.WriteLine($"{"Type",-6} {"名稱",-20} {"數量",-10} {"百分比"}");
            Console.WriteLine(new string('-', 50));

            // Block Type 說明
            var typeNames = new Dictionary<byte, string>
            {
                { 0, "SimpleDiamond" },
                { 1, "SimpleDiamond+Trans" },
                { 3, "Compressed" },
                { 6, "Compressed+6" },
                { 7, "Compressed+7" },
                { 8, "SimpleDiamond+8" },
                { 9, "SimpleDiamond+9" },
                { 16, "SimpleDiamond+16" },
                { 17, "SimpleDiamond+17" },
                { 34, "Compressed+34" },
                { 35, "Compressed+35" }
            };

            foreach (var kvp in blockTypeStats.OrderBy(x => x.Key))
            {
                string name = typeNames.ContainsKey(kvp.Key) ? typeNames[kvp.Key] : $"Type_{kvp.Key}";
                double pct = totalBlocks > 0 ? (double)kvp.Value / totalBlocks * 100 : 0;
                Console.WriteLine($"{kvp.Key,-6} {name,-20} {kvp.Value,-10} {pct:F2}%");
            }
            Console.WriteLine(new string('-', 50));
            Console.WriteLine($"{"總計",-6} {"",-20} {totalBlocks,-10}");

            // 輸出異常 Tile 列表
            if (abnormalTiles.Count > 0)
            {
                Console.WriteLine($"\n=== 包含異常 Block Type 的 Tile ({abnormalTiles.Count} 個) ===");
                Console.WriteLine($"{"TileId",-10} {"異常 Block Types"}");
                Console.WriteLine(new string('-', 50));
                foreach (var kvp in abnormalTiles.OrderBy(x => x.Key))
                {
                    string types = string.Join(", ", kvp.Value);
                    Console.WriteLine($"{kvp.Key,-10} {types}");
                }
            }
            else
            {
                Console.WriteLine("\n所有 Tile 的 Block Type 都是正常的 (0,1,2,3,6,7,8,9,16,17,18,19,22,23,34,35)");
            }

            return 0;
        }

        /// <summary>
        /// scan-tiles 命令 - 掃描 Tile.idx 或目錄找異常 Block Type
        /// </summary>
        private static int CmdScanTiles(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli scan-tiles <客戶端路徑或til目錄> [--type <types>]");
                Console.WriteLine("掃描 Tile.idx 或目錄中所有 Tile 的 Block Type");
                Console.WriteLine("選項:");
                Console.WriteLine("  --type <types>  查找包含指定 Block Type 的 Tile (例如: --type 6,7)");
                return 1;
            }

            string inputPath = args[0];
            bool isDirectory = Directory.Exists(inputPath);
            bool hasIdx = File.Exists(Path.Combine(inputPath, "Tile.idx"));

            // 解析 --type 參數
            HashSet<byte> searchTypes = null;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--type" && i + 1 < args.Length)
                {
                    searchTypes = new HashSet<byte>();
                    foreach (var t in args[i + 1].Split(','))
                    {
                        if (byte.TryParse(t.Trim(), out byte bt))
                            searchTypes.Add(bt);
                    }
                    break;
                }
            }

            // 正常的 Block Type
            var normalTypes = new HashSet<byte> { 0, 1, 2, 3, 6, 7, 8, 9, 16, 17, 18, 19, 22, 23, 34, 35 };
            var matchedTiles = new Dictionary<int, List<byte>>();
            int scanned = 0;

            if (isDirectory && !hasIdx)
            {
                // 掃描目錄中的 .til 檔案
                var tilFiles = Directory.GetFiles(inputPath, "*.til");
                Console.WriteLine($"=== 掃描目錄中的 Tile 檔案 ===");
                Console.WriteLine($"目錄: {inputPath}");
                Console.WriteLine($"找到 {tilFiles.Length} 個 .til 檔案");
                if (searchTypes != null)
                    Console.WriteLine($"查找 Block Type: {string.Join(", ", searchTypes.OrderBy(x => x))}");
                Console.WriteLine("掃描中...\n");

                foreach (var tilFile in tilFiles.OrderBy(f => f))
                {
                    string fileName = Path.GetFileNameWithoutExtension(tilFile);
                    if (!int.TryParse(fileName, out int tileId))
                        continue;

                    byte[] tilData = File.ReadAllBytes(tilFile);
                    if (tilData == null || tilData.Length < 8)
                        continue;

                    scanned++;

                    var blocks = Converter.L1Til.Parse(tilData);
                    var tileMatchedTypes = new HashSet<byte>();

                    foreach (var block in blocks)
                    {
                        if (block == null || block.Length < 1)
                            continue;

                        byte blockType = block[0];
                        if (searchTypes != null)
                        {
                            // 查找特定類型
                            if (searchTypes.Contains(blockType))
                                tileMatchedTypes.Add(blockType);
                        }
                        else
                        {
                            // 查找異常類型
                            if (!normalTypes.Contains(blockType))
                                tileMatchedTypes.Add(blockType);
                        }
                    }

                    if (tileMatchedTypes.Count > 0)
                        matchedTiles[tileId] = tileMatchedTypes.OrderBy(x => x).ToList();
                }
            }
            else
            {
                // 掃描 Tile.idx
                string clientPath = inputPath;
                string tileIdx = Path.Combine(clientPath, "Tile.idx");

                if (!File.Exists(tileIdx))
                {
                    Console.WriteLine($"Tile.idx 不存在: {tileIdx}");
                    return 1;
                }

                if (searchTypes != null)
                    Console.WriteLine($"=== 掃描 Tile.idx - 查找 Block Type: {string.Join(", ", searchTypes.OrderBy(x => x))} ===");
                else
                    Console.WriteLine($"=== 掃描 Tile.idx 異常 Block Type ===");
                Console.WriteLine($"客戶端: {clientPath}");
                Console.WriteLine();

                Share.LineagePath = clientPath;
                if (Share.IdxDataList.ContainsKey("Tile"))
                    Share.IdxDataList.Remove("Tile");
                L1PakReader.UnPack("Tile", "1.til");

                var allTileIds = new List<int>();
                if (Share.IdxDataList.TryGetValue("Tile", out var tileIdxData))
                {
                    foreach (var key in tileIdxData.Keys)
                    {
                        if (key.EndsWith(".til"))
                        {
                            string numStr = key.Substring(0, key.Length - 4);
                            if (int.TryParse(numStr, out int tileId))
                                allTileIds.Add(tileId);
                        }
                    }
                }

                Console.WriteLine($"Tile.idx 中共有 {allTileIds.Count} 個 Tile");
                Console.WriteLine("掃描中...\n");

                foreach (var tileId in allTileIds.OrderBy(x => x))
                {
                    byte[] tilData = L1PakReader.UnPack("Tile", $"{tileId}.til");
                    if (tilData == null || tilData.Length < 8)
                        continue;

                    scanned++;
                    if (scanned % 500 == 0)
                        Console.Write($"\r已掃描 {scanned} / {allTileIds.Count}...");

                    var blocks = Converter.L1Til.Parse(tilData);
                    var tileMatchedTypes = new HashSet<byte>();

                    foreach (var block in blocks)
                    {
                        if (block == null || block.Length < 1)
                            continue;

                        byte blockType = block[0];
                        if (searchTypes != null)
                        {
                            // 查找特定類型
                            if (searchTypes.Contains(blockType))
                                tileMatchedTypes.Add(blockType);
                        }
                        else
                        {
                            // 查找異常類型
                            if (!normalTypes.Contains(blockType))
                                tileMatchedTypes.Add(blockType);
                        }
                    }

                    if (tileMatchedTypes.Count > 0)
                        matchedTiles[tileId] = tileMatchedTypes.OrderBy(x => x).ToList();
                }
            }

            Console.WriteLine($"\r已掃描 {scanned} 個 Tile                    ");
            Console.WriteLine();

            if (matchedTiles.Count > 0)
            {
                if (searchTypes != null)
                {
                    Console.WriteLine($"=== 包含 Block Type {string.Join(", ", searchTypes.OrderBy(x => x))} 的 Tile ({matchedTiles.Count} 個) ===");
                    Console.WriteLine($"{"TileId",-10} {"匹配的 Block Types"}");
                }
                else
                {
                    Console.WriteLine($"=== 包含異常 Block Type 的 Tile ({matchedTiles.Count} 個) ===");
                    Console.WriteLine($"{"TileId",-10} {"異常 Block Types"}");
                }
                Console.WriteLine(new string('-', 60));
                foreach (var kvp in matchedTiles.OrderBy(x => x.Key))
                {
                    string types = string.Join(", ", kvp.Value);
                    Console.WriteLine($"{kvp.Key,-10} {types}");
                }
            }
            else
            {
                if (searchTypes != null)
                    Console.WriteLine($"沒有找到包含 Block Type {string.Join(", ", searchTypes.OrderBy(x => x))} 的 Tile");
                else
                    Console.WriteLine("所有 Tile 的 Block Type 都是正常的 (0,1,2,3,6,7,8,9,16,17,18,19,22,23,34,35)");
            }

            return 0;
        }

        /// <summary>
        /// til-info 命令 - 顯示 TIL 檔案的 Block Type 資訊
        /// </summary>
        private static int CmdTilInfo(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: -cli til-info <til檔案>");
                Console.WriteLine("顯示 TIL 檔案的 Block Type 統計資訊");
                return 1;
            }

            string filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"檔案不存在: {filePath}");
                return 1;
            }

            byte[] tilData = File.ReadAllBytes(filePath);
            if (tilData == null || tilData.Length < 4)
            {
                Console.WriteLine($"檔案太小或無法讀取: {filePath}");
                return 1;
            }

            var fileInfo = new FileInfo(filePath);
            Console.WriteLine($"=== TIL 檔案資訊 ===");
            Console.WriteLine($"檔案名稱: {fileInfo.Name}");
            Console.WriteLine($"檔案大小: {fileInfo.Length:N0} bytes");
            Console.WriteLine();

            // 解析 TIL
            var blocks = Converter.L1Til.Parse(tilData);
            Console.WriteLine($"Block 數量: {blocks.Count}");

            // 統計 Block Type
            var typeCount = new Dictionary<byte, int>();
            var emptyBlocks = 0;

            foreach (var block in blocks)
            {
                if (block == null || block.Length < 1)
                {
                    emptyBlocks++;
                    continue;
                }

                byte blockType = block[0];
                if (!typeCount.ContainsKey(blockType))
                    typeCount[blockType] = 0;
                typeCount[blockType]++;
            }

            Console.WriteLine($"空 Block: {emptyBlocks}");
            Console.WriteLine();

            // 正常的 Block Type
            var normalTypes = new HashSet<byte> { 0, 1, 2, 3, 6, 7, 8, 9, 16, 17, 18, 19, 22, 23, 34, 35 };

            Console.WriteLine($"=== Block Type 統計 ===");
            Console.WriteLine($"{"Type",-6} {"Count",-8} {"Status"}");
            Console.WriteLine(new string('-', 30));

            foreach (var kvp in typeCount.OrderBy(x => x.Key))
            {
                string status = normalTypes.Contains(kvp.Key) ? "正常" : "異常";
                Console.WriteLine($"{kvp.Key,-6} {kvp.Value,-8} {status}");
            }

            // 檢查是否有異常
            var abnormalTypes = typeCount.Keys.Where(t => !normalTypes.Contains(t)).ToList();
            if (abnormalTypes.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"*** 發現 {abnormalTypes.Count} 種異常 Block Type: {string.Join(", ", abnormalTypes.OrderBy(x => x))} ***");
            }

            // 判斷版本
            bool hasRemaster = typeCount.Keys.Any(t => t >= 16 && t <= 19);
            bool hasClassic = typeCount.Keys.Any(t => t >= 0 && t <= 7);
            string version = hasRemaster && hasClassic ? "Hybrid" : hasRemaster ? "Remaster (48x48)" : "Classic (24x24)";
            Console.WriteLine();
            Console.WriteLine($"推測版本: {version}");

            // 如果解析失敗 (0 blocks)，顯示原始資料分析
            if (blocks.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("=== 原始資料分析 (Block 數量為 0，可能是新格式) ===");

                // Hex dump first 128 bytes
                Console.WriteLine("\n前 128 bytes (hex):");
                for (int i = 0; i < Math.Min(128, tilData.Length); i++)
                {
                    Console.Write($"{tilData[i]:X2} ");
                    if ((i + 1) % 16 == 0) Console.WriteLine();
                }
                if (tilData.Length < 128 || tilData.Length % 16 != 0) Console.WriteLine();

                // Try to interpret as integers
                Console.WriteLine("\n前 32 bytes 解析為 Int32:");
                using (var br = new BinaryReader(new MemoryStream(tilData)))
                {
                    for (int i = 0; i < 8 && br.BaseStream.Position + 4 <= tilData.Length; i++)
                    {
                        long pos = br.BaseStream.Position;
                        int val = br.ReadInt32();
                        Console.WriteLine($"  offset {pos,4}: {val,12} (0x{val:X8})");
                    }
                }

                // Check for patterns
                Console.WriteLine("\n資料特徵:");
                Console.WriteLine($"  第一個 byte: 0x{tilData[0]:X2} ({tilData[0]})");
                if (tilData.Length >= 4)
                {
                    uint sig = BitConverter.ToUInt32(tilData, 0);
                    Console.WriteLine($"  前 4 bytes (uint32): {sig} (0x{sig:X8})");
                }

                // Look for text signature
                bool hasText = true;
                for (int i = 0; i < Math.Min(16, tilData.Length); i++)
                {
                    if (tilData[i] < 0x20 || tilData[i] > 0x7E)
                    {
                        hasText = false;
                        break;
                    }
                }
                if (hasText)
                {
                    string textStart = System.Text.Encoding.ASCII.GetString(tilData, 0, Math.Min(32, tilData.Length));
                    Console.WriteLine($"  開頭似乎是文字: \"{textStart}\"");
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

        /// <summary>
        /// test-layer8 命令 - 測試 Layer8 資料和渲染
        /// </summary>
        private static int CmdTestLayer8(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("用法: -cli test-layer8 <地圖資料夾> <gameX> <gameY> [--render]");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  -cli test-layer8 C:\\client\\map\\4 33712 32380");
                Console.WriteLine("  -cli test-layer8 C:\\client\\map\\4 33712 32380 --render");
                return 1;
            }

            string mapFolder = args[0];
            if (!int.TryParse(args[1], out int gameX) || !int.TryParse(args[2], out int gameY))
            {
                Console.WriteLine("錯誤: gameX 和 gameY 必須是數字");
                return 1;
            }
            bool doRender = args.Any(a => a.ToLower() == "--render");

            if (!Directory.Exists(mapFolder))
            {
                Console.WriteLine($"錯誤: 地圖資料夾不存在: {mapFolder}");
                return 1;
            }

            // 載入地圖
            var loadResult = MapLoader.Load(mapFolder);
            if (!loadResult.Success) return 1;

            Console.WriteLine($"載入 {loadResult.S32Files.Count} 個 S32 檔案");
            Console.WriteLine();

            // 統計 Layer8 資料
            int totalLayer8 = 0;
            var s32WithLayer8 = new List<S32Data>();

            foreach (var s32 in loadResult.S32Files.Values)
            {
                if (s32.Layer8.Count > 0)
                {
                    totalLayer8 += s32.Layer8.Count;
                    s32WithLayer8.Add(s32);
                }
            }

            Console.WriteLine($"=== Layer8 統計 ===");
            Console.WriteLine($"總 Layer8 項目數: {totalLayer8}");
            Console.WriteLine($"有 Layer8 的 S32 數量: {s32WithLayer8.Count}");
            Console.WriteLine();

            // 找出目標座標附近的 S32
            S32Data targetS32 = null;
            foreach (var s32 in loadResult.S32Files.Values)
            {
                var seg = s32.SegInfo;
                if (gameX >= seg.nLinBeginX && gameX <= seg.nLinEndX &&
                    gameY >= seg.nLinBeginY && gameY <= seg.nLinEndY)
                {
                    targetS32 = s32;
                    break;
                }
            }

            if (targetS32 != null)
            {
                Console.WriteLine($"=== 目標 S32: {Path.GetFileName(targetS32.FilePath)} ===");
                Console.WriteLine($"座標範圍: ({targetS32.SegInfo.nLinBeginX},{targetS32.SegInfo.nLinBeginY}) - ({targetS32.SegInfo.nLinEndX},{targetS32.SegInfo.nLinEndY})");
                Console.WriteLine($"Layer8 項目數: {targetS32.Layer8.Count}");

                if (targetS32.Layer8.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Layer8 項目:");
                    int[] loc = targetS32.SegInfo.GetLoc(1.0);
                    int mx = loc[0];
                    int my = loc[1];

                    for (int i = 0; i < targetS32.Layer8.Count && i < 20; i++)
                    {
                        var item = targetS32.Layer8[i];
                        // Layer8 X,Y 是絕對遊戲座標，轉為本地座標
                        int localL3X = item.X - targetS32.SegInfo.nLinBeginX;
                        int localL3Y = item.Y - targetS32.SegInfo.nLinBeginY;

                        // 計算世界像素座標
                        int layer1X = localL3X * 2;
                        int layer1Y = localL3Y;
                        int baseX = -24 * (layer1X / 2);
                        int baseY = 63 * 12 - 12 * (layer1X / 2);
                        int worldX = mx + baseX + layer1X * 24 + layer1Y * 24 + 12;
                        int worldY = my + baseY + layer1Y * 12 + 12;

                        Console.WriteLine($"  [{i}] SprId={item.SprId}, GameXY=({item.X},{item.Y}), LocalXY=({localL3X},{localL3Y}), WorldPos=({worldX},{worldY})");
                    }
                    if (targetS32.Layer8.Count > 20)
                        Console.WriteLine($"  ... 還有 {targetS32.Layer8.Count - 20} 個項目");
                }
            }
            else
            {
                Console.WriteLine($"找不到包含座標 ({gameX},{gameY}) 的 S32");
            }

            // 列出所有有 Layer8 的 S32
            if (s32WithLayer8.Count > 0 && s32WithLayer8.Count <= 20)
            {
                Console.WriteLine();
                Console.WriteLine("=== 所有有 Layer8 的 S32 ===");
                foreach (var s32 in s32WithLayer8)
                {
                    Console.WriteLine($"  {Path.GetFileName(s32.FilePath)}: {s32.Layer8.Count} 個項目");
                    foreach (var item in s32.Layer8.Take(5))
                    {
                        Console.WriteLine($"    SprId={item.SprId}, X={item.X}, Y={item.Y}");
                    }
                    if (s32.Layer8.Count > 5)
                        Console.WriteLine($"    ...");
                }
            }

            // 渲染測試
            if (doRender && targetS32 != null && targetS32.Layer8.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("=== 渲染測試 ===");

                int[] loc = targetS32.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 計算第一個 Layer8 項目的世界座標（使用正確的座標轉換）
                var firstItem = targetS32.Layer8[0];
                int firstLocalX = firstItem.X - targetS32.SegInfo.nLinBeginX;
                int firstLocalY = firstItem.Y - targetS32.SegInfo.nLinBeginY;
                int layer1X = firstLocalX * 2;
                int layer1Y = firstLocalY;
                int baseX = -24 * (layer1X / 2);
                int baseY = 63 * 12 - 12 * (layer1X / 2);
                int centerX = mx + baseX + layer1X * 24 + layer1Y * 24 + 12;
                int centerY = my + baseY + layer1Y * 12 + 12;

                Console.WriteLine($"第一個 Layer8 項目世界座標: ({centerX}, {centerY})");

                // 建立一個 800x600 的測試區域
                int testWidth = 800;
                int testHeight = 600;
                var worldRect = new Rectangle(centerX - testWidth / 2, centerY - testHeight / 2, testWidth, testHeight);
                Console.WriteLine($"測試渲染區域: {worldRect}");

                using (var bitmap = new Bitmap(testWidth, testHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.DarkGray);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    int drawnCount = 0;
                    foreach (var item in targetS32.Layer8)
                    {
                        // 正確的座標轉換
                        int localL3X = item.X - targetS32.SegInfo.nLinBeginX;
                        int localL3Y = item.Y - targetS32.SegInfo.nLinBeginY;

                        if (localL3X < 0 || localL3X > 63 || localL3Y < 0 || localL3Y > 63)
                            continue;

                        int l1X = localL3X * 2;
                        int l1Y = localL3Y;
                        int bX = -24 * (l1X / 2);
                        int bY = 63 * 12 - 12 * (l1X / 2);
                        int wX = mx + bX + l1X * 24 + l1Y * 24 + 12;
                        int wY = my + bY + l1Y * 12 + 12;

                        int x = wX - worldRect.X;
                        int y = wY - worldRect.Y;

                        if (x >= -20 && x < testWidth + 20 && y >= -20 && y < testHeight + 20)
                        {
                            g.FillEllipse(Brushes.Orange, x - 8, y - 8, 16, 16);
                            g.DrawEllipse(Pens.White, x - 8, y - 8, 16, 16);
                            using (var font = new Font("Arial", 8))
                            {
                                g.DrawString(item.SprId.ToString(), font, Brushes.White, x + 10, y - 6);
                            }
                            drawnCount++;
                        }
                    }

                    Console.WriteLine($"繪製了 {drawnCount} 個 marker");

                    string outputPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "tests", "layer8_test.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    bitmap.Save(outputPath);
                    Console.WriteLine($"輸出: {outputPath}");
                }
            }

            return 0;
        }

        /// <summary>
        /// test-layer8-click 命令 - 測試 Layer8 點擊偵測
        /// </summary>
        private static int CmdTestLayer8Click(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("用法: -cli test-layer8-click <地圖資料夾> <gameX> <gameY> [--simulate-click <worldX> <worldY>]");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  -cli test-layer8-click C:\\client\\map\\4 33706 32382");
                Console.WriteLine("  -cli test-layer8-click C:\\client\\map\\4 33706 32382 --simulate-click 55000 12000");
                return 1;
            }

            string mapFolder = args[0];
            if (!int.TryParse(args[1], out int gameX) || !int.TryParse(args[2], out int gameY))
            {
                Console.WriteLine("錯誤: gameX 和 gameY 必須是數字");
                return 1;
            }

            // 解析模擬點擊座標
            int? simClickX = null, simClickY = null;
            for (int i = 3; i < args.Length - 2; i++)
            {
                if (args[i].ToLower() == "--simulate-click" &&
                    int.TryParse(args[i + 1], out int sx) &&
                    int.TryParse(args[i + 2], out int sy))
                {
                    simClickX = sx;
                    simClickY = sy;
                    break;
                }
            }

            if (!Directory.Exists(mapFolder))
            {
                Console.WriteLine($"錯誤: 地圖資料夾不存在: {mapFolder}");
                return 1;
            }

            // 載入地圖
            var loadResult = MapLoader.Load(mapFolder);
            if (!loadResult.Success) return 1;

            Console.WriteLine($"載入 {loadResult.S32Files.Count} 個 S32 檔案");
            Console.WriteLine();

            // 找出目標座標的 S32
            S32Data targetS32 = null;
            foreach (var s32 in loadResult.S32Files.Values)
            {
                var seg = s32.SegInfo;
                if (gameX >= seg.nLinBeginX && gameX <= seg.nLinEndX &&
                    gameY >= seg.nLinBeginY && gameY <= seg.nLinEndY)
                {
                    targetS32 = s32;
                    break;
                }
            }

            if (targetS32 == null)
            {
                Console.WriteLine($"找不到包含座標 ({gameX},{gameY}) 的 S32");
                return 1;
            }

            Console.WriteLine($"=== 目標 S32: {Path.GetFileName(targetS32.FilePath)} ===");
            Console.WriteLine($"座標範圍: ({targetS32.SegInfo.nLinBeginX},{targetS32.SegInfo.nLinBeginY}) - ({targetS32.SegInfo.nLinEndX},{targetS32.SegInfo.nLinEndY})");
            Console.WriteLine($"Layer8 項目數: {targetS32.Layer8.Count}");
            Console.WriteLine();

            if (targetS32.Layer8.Count == 0)
            {
                Console.WriteLine("此 S32 沒有 Layer8 資料");
                return 0;
            }

            int[] loc = targetS32.SegInfo.GetLoc(1.0);
            int mx = loc[0];
            int my = loc[1];
            Console.WriteLine($"S32 世界起點: ({mx}, {my})");
            Console.WriteLine();

            // 列出所有 Layer8 marker 位置
            Console.WriteLine("=== Layer8 Marker 位置 ===");
            var markerPositions = new List<(int worldX, int worldY, int index, int sprId)>();

            for (int i = 0; i < targetS32.Layer8.Count; i++)
            {
                var item = targetS32.Layer8[i];
                int localL3X = item.X - targetS32.SegInfo.nLinBeginX;
                int localL3Y = item.Y - targetS32.SegInfo.nLinBeginY;

                if (localL3X < 0 || localL3X > 63 || localL3Y < 0 || localL3Y > 63)
                {
                    Console.WriteLine($"  [{i}] 超出範圍: GameXY=({item.X},{item.Y}), LocalXY=({localL3X},{localL3Y})");
                    continue;
                }

                int layer1X = localL3X * 2;
                int layer1Y = localL3Y;
                int baseX = -24 * (layer1X / 2);
                int baseY = 63 * 12 - 12 * (layer1X / 2);
                // 標記中心位置（+12 偏移）
                int markerWorldX = mx + baseX + layer1X * 24 + layer1Y * 24 + 12;
                int markerWorldY = my + baseY + layer1Y * 12 + 12;

                markerPositions.Add((markerWorldX, markerWorldY, i, item.SprId));
                Console.WriteLine($"  [{i}] SprId={item.SprId}, GameXY=({item.X},{item.Y}), MarkerWorldXY=({markerWorldX},{markerWorldY})");
            }

            Console.WriteLine();

            // 模擬點擊偵測
            if (simClickX.HasValue && simClickY.HasValue)
            {
                Console.WriteLine($"=== 模擬點擊偵測: ({simClickX}, {simClickY}) ===");
                const int hitRadius = 20;
                bool found = false;

                foreach (var (markerX, markerY, index, sprId) in markerPositions)
                {
                    int dx = simClickX.Value - markerX;
                    int dy = simClickY.Value - markerY;
                    int distSq = dx * dx + dy * dy;
                    double dist = Math.Sqrt(distSq);

                    if (distSq <= hitRadius * hitRadius)
                    {
                        Console.WriteLine($"  HIT! Marker[{index}] SprId={sprId} at ({markerX},{markerY}), dist={dist:F1}");
                        found = true;
                    }
                    else if (dist < 50) // 顯示靠近的 marker
                    {
                        Console.WriteLine($"  NEAR: Marker[{index}] at ({markerX},{markerY}), dist={dist:F1} (need <= {hitRadius})");
                    }
                }

                if (!found)
                {
                    Console.WriteLine("  沒有命中任何 marker");
                    Console.WriteLine();
                    Console.WriteLine("最近的 marker:");
                    var nearest = markerPositions
                        .Select(m => new { m.index, m.sprId, m.worldX, m.worldY,
                            dist = Math.Sqrt(Math.Pow(simClickX.Value - m.worldX, 2) + Math.Pow(simClickY.Value - m.worldY, 2)) })
                        .OrderBy(x => x.dist)
                        .Take(3)
                        .ToList();
                    foreach (var n in nearest)
                    {
                        Console.WriteLine($"    Marker[{n.index}] SprId={n.sprId} at ({n.worldX},{n.worldY}), dist={n.dist:F1}");
                    }
                }
            }
            else
            {
                // 自動計算應該點擊的位置
                Console.WriteLine("=== 點擊測試座標建議 ===");
                if (markerPositions.Count > 0)
                {
                    var first = markerPositions[0];
                    Console.WriteLine($"要測試第一個 marker，使用:");
                    Console.WriteLine($"  --simulate-click {first.worldX} {first.worldY}");
                }
            }

            return 0;
        }

        /// <summary>
        /// export-passability 命令 - 匯出地圖通行資料為 L1J/DIR 格式
        /// 完全按照 MapTool 的邏輯實作
        /// </summary>
        private static int CmdExportPassability(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法: -cli export-passability <地圖資料夾> <輸出.txt> [--dir]");
                Console.WriteLine();
                Console.WriteLine("參數:");
                Console.WriteLine("  <地圖資料夾>  包含 S32 檔案的地圖資料夾");
                Console.WriteLine("  <輸出.txt>    輸出檔案路徑");
                Console.WriteLine("  --dir         使用 DIR 格式（預設 L1J 格式）");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  -cli export-passability C:\\client\\map\\4 4.txt");
                Console.WriteLine("  -cli export-passability C:\\client\\map\\4 4_dir.txt --dir");
                return 1;
            }

            string mapFolder = args[0];
            string outputPath = args[1];
            bool isDirFormat = args.Any(a => a.ToLower() == "--dir");

            if (!Directory.Exists(mapFolder))
            {
                Console.WriteLine($"錯誤: 地圖資料夾不存在: {mapFolder}");
                return 1;
            }

            // 讀取所有 S32/SEG 檔案（S32 優先，與 MapTool 相同：每個區塊只處理一個檔案）
            var allFiles = Directory.GetFiles(mapFolder, "*.s32")
                .Concat(Directory.GetFiles(mapFolder, "*.seg"))
                .ToList();

            if (allFiles.Count == 0)
            {
                Console.WriteLine($"錯誤: 在 {mapFolder} 中找不到 S32/SEG 檔案");
                return 1;
            }

            // 每個區塊只保留一個檔案，S32 優先
            var blockFiles = new Dictionary<string, string>(); // baseName -> fullPath
            foreach (var file in allFiles)
            {
                string baseName = Path.GetFileNameWithoutExtension(file).ToLower();
                string ext = Path.GetExtension(file).ToLower();

                if (baseName.Length == 8)
                {
                    if (!blockFiles.ContainsKey(baseName))
                    {
                        // 第一個檔案，直接加入
                        blockFiles[baseName] = file;
                    }
                    else if (ext == ".s32")
                    {
                        // S32 優先，覆蓋已有的 SEG
                        blockFiles[baseName] = file;
                    }
                    // 如果已有 S32，忽略 SEG
                }
            }

            var s32Files = blockFiles.Values.ToList();

            Console.WriteLine($"地圖資料夾: {mapFolder}");
            Console.WriteLine($"找到 {s32Files.Count} 個區塊檔案（S32 優先）");
            Console.WriteLine($"輸出格式: {(isDirFormat ? "DIR" : "L1J")}");

            // 計算座標範圍（與 MapTool 相同方式）
            int minX = ushort.MaxValue;
            int maxX = 0;
            int minY = ushort.MaxValue;
            int maxY = 0;

            var fileCoords = new Dictionary<string, (int x, int y)>();

            foreach (var file in s32Files)
            {
                string name = Path.GetFileNameWithoutExtension(file).ToLower();
                if (name.Length == 8)
                {
                    int x = Convert.ToInt32(name.Substring(0, 4), 16);
                    int y = Convert.ToInt32(name.Substring(4, 4), 16);
                    fileCoords[file] = (x, y);
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }

            // 計算維度（每個 S32 是 64x64）
            int numBlocksX = maxX - minX + 1;
            int numBlocksY = maxY - minY + 1;
            int xLength = numBlocksX * 64;
            int yLength = numBlocksY * 64;

            // 計算遊戲座標範圍
            int xEnd = (maxX - 32767) * 64 + 32767;
            int yEnd = (maxY - 32767) * 64 + 32767;
            int xBegin = xEnd - xLength + 1;
            int yBegin = yEnd - yLength + 1;

            Console.WriteLine($"座標範圍: X({xBegin}~{xEnd}), Y({yBegin}~{yEnd})");
            Console.WriteLine($"陣列大小: {xLength} x {yLength}");

            // 建立 tileList_t1 和 tileList_t3 陣列
            int[,] tileList_t1 = new int[xLength, yLength];
            int[,] tileList_t3 = new int[xLength, yLength];

            // 初始化為不可通行（1 = 預設值，與 MapTool 相同）
            for (int x = 0; x < xLength; x++)
            {
                for (int y = 0; y < yLength; y++)
                {
                    tileList_t1[x, y] = 1;
                    tileList_t3[x, y] = 1;
                }
            }

            // 讀取每個 S32/SEG 檔案
            Console.WriteLine("讀取 S32/SEG 檔案...");
            int fileCount = 0;
            foreach (var file in s32Files)
            {
                if (!fileCoords.ContainsKey(file)) continue;

                var (fx, fy) = fileCoords[file];
                int pp = fx - minX;  // X 偏移（以 64 為單位）
                int ppp = fy - minY; // Y 偏移（以 64 為單位）

                byte[] data = File.ReadAllBytes(file);
                string ext = Path.GetExtension(file).ToLower();

                if (ext == ".s32")
                {
                    ReadS32ForPassability(data, pp, ppp, tileList_t1, tileList_t3);
                }
                else if (ext == ".seg")
                {
                    ReadSegForPassability(data, pp, ppp, tileList_t1, tileList_t3);
                }

                fileCount++;
                if (fileCount % 100 == 0)
                {
                    Console.Write($"\r  已處理: {fileCount}/{s32Files.Count}");
                }
            }
            Console.WriteLine($"\r  已處理: {fileCount}/{s32Files.Count}");

            // 計算 8 方向通行性（與 MapTool 的 decryptData 相同）
            Console.WriteLine("計算通行性...");
            int[,] tileList = new int[xLength, yLength];

            for (int x = 0; x < xLength; x++)
            {
                for (int y = 0; y < yLength; y++)
                {
                    if (x + 1 < xLength && y + 1 < yLength && x - 1 >= 0 && y - 1 >= 0)
                    {
                        // D0: 下方
                        if ((tileList_t1[x, y + 1] & 1) == 0)
                            tileList[x, y] += 1;
                        // D4: 上方
                        if ((tileList_t1[x, y] & 1) == 0)
                            tileList[x, y] += 2;
                        // D2: 左方
                        if ((tileList_t3[x - 1, y] & 1) == 0)
                            tileList[x, y] += 4;
                        // D6: 右方
                        if ((tileList_t3[x, y] & 1) == 0)
                            tileList[x, y] += 8;

                        // D1: 左下對角
                        if (IsPassable_D1_Cli(tileList_t1, tileList_t3, x - 1, y + 1, xLength, yLength))
                            tileList[x, y] += 16;
                        // D3: 左上對角
                        if (IsPassable_D3_Cli(tileList_t1, tileList_t3, x - 1, y - 1, xLength, yLength))
                            tileList[x, y] += 32;
                        // D5: 右上對角
                        if (IsPassable_D5_Cli(tileList_t1, tileList_t3, x + 1, y - 1, xLength, yLength))
                            tileList[x, y] += 64;
                        // D7: 右下對角
                        if (IsPassable_D7_Cli(tileList_t1, tileList_t3, x + 1, y + 1, xLength, yLength))
                            tileList[x, y] += 128;

                        // 區域類型
                        tileList[x, y] += GetZone_Cli(tileList_t1[x, y]);
                    }
                }
            }

            // 根據格式選擇輸出資料
            int[,] outputData;
            if (isDirFormat)
            {
                outputData = tileList;
            }
            else
            {
                // L1J 格式轉換
                outputData = FormatL1J_Cli(tileList, xLength, yLength);
            }

            // 寫入檔案（不帶 BOM，與 MapTool 相同）
            Console.WriteLine($"寫入檔案: {outputPath}");
            using (StreamWriter writer = new StreamWriter(outputPath, false, new UTF8Encoding(false)))
            {
                for (int y = 0; y < yLength; y++)
                {
                    StringBuilder line = new StringBuilder();
                    for (int x = 0; x < xLength; x++)
                    {
                        if (x > 0) line.Append(",");
                        line.Append(outputData[x, y]);
                    }
                    writer.WriteLine(line.ToString());
                }
            }

            Console.WriteLine($"完成！已匯出 {xLength}x{yLength} 格子");
            return 0;
        }

        /// <summary>
        /// 讀取 S32 檔案的 Layer3 屬性（與 MapTool 的 readS32 相同）
        /// </summary>
        private static void ReadS32ForPassability(byte[] data, int pp, int ppp, int[,] t1, int[,] t3)
        {
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                int offset = 32768; // 0x8000
                br.BaseStream.Seek(offset, SeekOrigin.Begin);
                int tileCount = br.ReadUInt16();

                // 跳過 tile 資料，到達 Layer3
                int layer3Offset = offset + tileCount * 6 + 2;
                br.BaseStream.Seek(layer3Offset, SeekOrigin.Begin);

                // 讀取 64x64 的 Layer3 資料
                int num4 = 0;  // x counter
                int num5 = -1; // y counter

                while (true)
                {
                    if (num4 % 64 == 0)
                    {
                        num5++;
                        num4 = 0;
                    }

                    if (num5 >= 64) break;

                    int index1 = num4 + pp * 64;  // x coordinate
                    int index2 = num5 + ppp * 64; // y coordinate

                    // 讀取 4 bytes: [Attribute1, ?, Attribute2, ?]
                    int attr1 = br.ReadByte();
                    br.ReadByte(); // skip
                    int attr2 = br.ReadByte();
                    br.ReadByte(); // skip

                    // 應用 replaceException（與 MapTool 相同）
                    t1[index1, index2] = ReplaceException_Cli(attr1);
                    t3[index1, index2] = ReplaceException_Cli(attr2);

                    num4++;
                }
            }
        }

        /// <summary>
        /// 讀取 SEG 檔案的 Layer3 屬性（與 MapTool 的 readSeg 相同）
        /// </summary>
        private static void ReadSegForPassability(byte[] data, int pp, int ppp, int[,] t1, int[,] t3)
        {
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                int offset = 16384; // 0x4000
                br.BaseStream.Seek(offset, SeekOrigin.Begin);
                int tileCount = br.ReadUInt16();

                // 跳過 tile 資料，到達 Layer3
                int layer3Offset = offset + tileCount * 4 + 2;
                br.BaseStream.Seek(layer3Offset, SeekOrigin.Begin);

                // 讀取 64x64 的 Layer3 資料
                int num3 = 0;  // x counter
                int num4 = -1; // y counter

                while (true)
                {
                    if (num3 % 64 == 0)
                    {
                        num4++;
                        num3 = 0;
                    }

                    if (num4 >= 64) break;

                    int index1 = num3 + pp * 64;  // x coordinate
                    int index2 = num4 + ppp * 64; // y coordinate

                    // SEG 只讀取 2 bytes: [Attribute1, Attribute2]
                    int attr1 = br.ReadByte();
                    int attr2 = br.ReadByte();

                    // 應用 replaceException（與 MapTool 相同）
                    t1[index1, index2] = ReplaceException_Cli(attr1);
                    t3[index1, index2] = ReplaceException_Cli(attr2);

                    num3++;
                }
            }
        }

        /// <summary>
        /// 替換例外值（與 MapTool 的 replaceException 相同）
        /// </summary>
        private static int ReplaceException_Cli(int i)
        {
            if (i == 65 || i == 69 || i == 73 || i == 33 || i == 77)
                return 5;
            return i;
        }

        /// <summary>
        /// 取得區域類型（與 MapTool 的 getZone 相同）
        /// </summary>
        private static int GetZone_Cli(int tileValue)
        {
            int zone = 256;
            string hex = (tileValue & 0x0F).ToString("X1");

            if (hex == "0" || hex == "1" || hex == "2" || hex == "3")
                zone = 256;
            else if (hex == "4" || hex == "5" || hex == "6" || hex == "7" ||
                     hex == "C" || hex == "D" || hex == "E" || hex == "F")
                zone = 512;
            else if (hex == "8" || hex == "9" || hex == "A" || hex == "B")
                zone = 1024;

            return zone;
        }

        /// <summary>
        /// L1J 格式轉換（與 MapTool 的 formate_L1J 相同）
        /// </summary>
        private static int[,] FormatL1J_Cli(int[,] tileList, int xLength, int yLength)
        {
            int[,] result = new int[xLength, yLength];

            for (int y = 0; y < yLength; y++)
            {
                for (int x = 0; x < xLength; x++)
                {
                    int tile = tileList[x, y];

                    if ((tile & 1) == 1 || (tile & 2) == 2)
                        result[x, y] += 2;

                    if ((tile & 4) == 4 || (tile & 8) == 8)
                        result[x, y] += 1;

                    if ((tile & 1) == 1 && (tile & 2) == 2)
                        result[x, y] += 8;

                    if ((tile & 4) == 4 && (tile & 8) == 8)
                        result[x, y] += 4;

                    if ((tile & 512) == 512)
                        result[x, y] += 16;

                    if ((tile & 1024) == 1024)
                        result[x, y] += 32;
                }
            }

            return result;
        }

        // 對角方向通行性判斷（與 MapTool 相同）
        private static bool IsPassable_D1_Cli(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 0 || x + 1 >= xLen || y < 0 || y >= yLen || y - 1 < 0) return false;
            return (t1[x, y] & 1) == 0 && (t1[x + 1, y] & 1) == 0 &&
                   (t3[x + 1, y] & 1) == 0 && (t3[x + 1, y - 1] & 1) == 0;
        }

        private static bool IsPassable_D3_Cli(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 0 || x + 1 >= xLen || y < 0 || y + 1 >= yLen) return false;
            return (t1[x, y + 1] & 1) == 0 && (t1[x + 1, y + 1] & 1) == 0 &&
                   (t3[x, y] & 1) == 0 && (t3[x, y + 1] & 1) == 0;
        }

        private static bool IsPassable_D5_Cli(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 1 || x >= xLen || y < 0 || y + 1 >= yLen) return false;
            return (t1[x, y + 1] & 1) == 0 && (t1[x - 1, y + 1] & 1) == 0 &&
                   (t3[x - 1, y] & 1) == 0 && (t3[x - 1, y + 1] & 1) == 0;
        }

        private static bool IsPassable_D7_Cli(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 1 || x >= xLen || y < 1 || y >= yLen) return false;
            return (t1[x, y] & 1) == 0 && (t1[x - 1, y] & 1) == 0 &&
                   (t3[x - 1, y] & 1) == 0 && (t3[x - 1, y - 1] & 1) == 0;
        }
    }
}
