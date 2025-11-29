using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using L1MapViewer.Models;

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
  l6 <s32檔案>                顯示第六層（使用的 TileId）
  l7 <s32檔案>                顯示第七層（傳送點）資訊
  l8 <s32檔案>                顯示第八層（特效）資訊
  export <s32檔案> <輸出檔>   匯出 S32 資訊為 JSON
  fix <s32檔案> [--apply]     修復異常資料（如 X>=128 的 Layer4 物件）
  coords <地圖資料夾>         計算地圖的遊戲座標範圍（startX, endX, startY, endY）
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
                    Console.WriteLine($"  X={item.X}, Y={item.Y}, RGB=({item.R},{item.G},{item.B})");
                }
            }

            return 0;
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
                    Console.WriteLine($"  SprId={item.SprId}, X={item.X}, Y={item.Y}, Unknown={item.Unknown}");
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
                sb.Append($"    {{\"sprId\": {item.SprId}, \"x\": {item.X}, \"y\": {item.Y}, \"unknown\": {item.Unknown}}}");
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
                    bw.Write(item.Value1);
                    bw.Write(item.Value2);
                    bw.Write(item.Value3);
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
                    bw.Write(item.R);
                    bw.Write(item.G);
                    bw.Write(item.B);
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
                bw.Write((byte)s32.Layer8.Count);
                bw.Write((byte)0); // 跳過一個 byte
                foreach (var item in s32.Layer8)
                {
                    bw.Write(item.SprId);
                    bw.Write(item.X);
                    bw.Write(item.Y);
                    bw.Write(item.Unknown);
                }

                File.WriteAllBytes(filePath, ms.ToArray());
            }
        }
    }
}
