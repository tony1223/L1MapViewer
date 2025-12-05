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
  help                        顯示此幫助資訊

範例:
  L1MapViewer.exe -cli info map.s32
  L1MapViewer.exe -cli l7 map.s32
  L1MapViewer.exe -cli l4 map.s32 --groups
  L1MapViewer.exe -cli export map.s32 output.json
");
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
                Console.WriteLine($"TileId 數量 ({allTileIds.Count}) 已小於等於保留數量 ({keepCount})，直接複製");
                File.Copy(srcPath, dstPath, true);
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
    }
}
