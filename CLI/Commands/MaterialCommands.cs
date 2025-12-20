using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using L1MapViewer.Helper;
using L1MapViewer.Models;
using L1MapViewer.Reader;
using static L1MapViewer.Other.Struct;

namespace L1MapViewer.CLI.Commands
{
    /// <summary>
    /// 素材相關 CLI 命令
    /// </summary>
    public static class MaterialCommands
    {
        /// <summary>
        /// 驗證素材中的 Tile MD5
        /// </summary>
        public static int VerifyMaterialTiles(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法: verify-material-tiles <client_path> <material.fs32p>");
                Console.WriteLine();
                Console.WriteLine("參數:");
                Console.WriteLine("  client_path     客戶端路徑 (包含 Tile.idx/Tile.pak)");
                Console.WriteLine("  material.fs32p  素材檔案路徑");
                Console.WriteLine();
                Console.WriteLine("此命令會檢查素材中每個 Tile 的 MD5 是否能在客戶端 Tile.pak 中找到匹配");
                return 1;
            }

            string clientPath = args[0];
            string materialPath = args[1];

            // 設定客戶端路徑
            if (!Directory.Exists(clientPath))
            {
                Console.WriteLine($"錯誤: 客戶端路徑不存在: {clientPath}");
                return 1;
            }

            string tileIdxPath = Path.Combine(clientPath, "Tile.idx");
            string tilePakPath = Path.Combine(clientPath, "Tile.pak");
            if (!File.Exists(tileIdxPath) || !File.Exists(tilePakPath))
            {
                Console.WriteLine($"錯誤: 找不到 Tile.idx 或 Tile.pak");
                return 1;
            }

            Share.LineagePath = clientPath;
            Console.WriteLine($"客戶端路徑: {clientPath}");

            // 清除快取，強制重新載入
            TileHashManager.ClearCache();
            if (Share.IdxDataList.ContainsKey("Tile"))
            {
                Share.IdxDataList.Remove("Tile");
            }

            // 載入素材
            if (!File.Exists(materialPath))
            {
                Console.WriteLine($"錯誤: 素材檔案不存在: {materialPath}");
                return 1;
            }

            Console.WriteLine($"載入素材: {materialPath}");
            Fs3pData material;
            try
            {
                material = Fs3pParser.ParseFile(materialPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"錯誤: 無法解析素材檔案: {ex.Message}");
                return 1;
            }

            Console.WriteLine($"  名稱: {material.Name}");
            Console.WriteLine($"  尺寸: {material.Width} x {material.Height}");
            Console.WriteLine($"  Tiles: {material.Tiles?.Count ?? 0} 個");
            Console.WriteLine();

            if (material.Tiles == null || material.Tiles.Count == 0)
            {
                Console.WriteLine("素材不包含任何 Tile 資料");
                return 0;
            }

            // 取得客戶端所有 Tile
            Console.WriteLine("載入客戶端 Tile.idx...");
            var idxData = L1IdxReader.GetAll("Tile");
            Console.WriteLine($"  共 {idxData.Count} 個 Tile 記錄");
            Console.WriteLine();

            // 逐一檢查素材中的 Tile
            Console.WriteLine("驗證 Tile MD5...");
            Console.WriteLine(new string('-', 80));

            int reuseCount = 0;
            int remapCount = 0;
            int newCount = 0;

            foreach (var kvp in material.Tiles)
            {
                int originalId = kvp.Key;
                byte[] packageMd5 = kvp.Value.Md5Hash;
                string packageMd5Hex = TileHashManager.Md5ToHex(packageMd5);

                Console.Write($"Tile {originalId,5}: MD5={packageMd5Hex.Substring(0, 16)}... ");

                // 檢查原始 ID 是否存在
                byte[] existingTilData = L1PakReader.UnPack("Tile", $"{originalId}.til");

                if (existingTilData != null)
                {
                    byte[] existingMd5 = TileHashManager.CalculateMd5(existingTilData);
                    string existingMd5Hex = TileHashManager.Md5ToHex(existingMd5);

                    if (TileHashManager.CompareMd5(existingMd5, packageMd5))
                    {
                        Console.WriteLine($"[重用] ID {originalId} 存在且 MD5 相符");
                        reuseCount++;
                    }
                    else
                    {
                        // ID 存在但 MD5 不同，需要尋找其他匹配
                        Console.Write($"[衝突] ID {originalId} 存在但 MD5 不同 (現有={existingMd5Hex.Substring(0, 16)}...)");

                        // 搜尋所有 Tile 找匹配的 MD5
                        int? foundId = FindTileByMd5Full(idxData, packageMd5);
                        if (foundId.HasValue)
                        {
                            Console.WriteLine($" -> 可重映射到 ID {foundId.Value}");
                            remapCount++;
                        }
                        else
                        {
                            Console.WriteLine(" -> 需新增匯入");
                            newCount++;
                        }
                    }
                }
                else
                {
                    // 原始 ID 不存在，搜尋 MD5 匹配
                    Console.Write($"[不存在] ID {originalId} 不存在");

                    int? foundId = FindTileByMd5Full(idxData, packageMd5);
                    if (foundId.HasValue)
                    {
                        Console.WriteLine($" -> 可重映射到 ID {foundId.Value}");
                        remapCount++;
                    }
                    else
                    {
                        Console.WriteLine(" -> 需新增匯入");
                        newCount++;
                    }
                }
            }

            Console.WriteLine(new string('-', 80));
            Console.WriteLine();
            Console.WriteLine("統計結果:");
            Console.WriteLine($"  可重用 (MD5 相符): {reuseCount} 個");
            Console.WriteLine($"  可重映射 (找到其他 ID): {remapCount} 個");
            Console.WriteLine($"  需新增匯入: {newCount} 個");
            Console.WriteLine($"  總計: {material.Tiles.Count} 個");

            return 0;
        }

        /// <summary>
        /// 完整掃描所有 Tile 尋找 MD5 匹配
        /// </summary>
        private static int? FindTileByMd5Full(Dictionary<string, L1Idx> idxData, byte[] targetMd5)
        {
            foreach (var entry in idxData)
            {
                string fileName = entry.Key;
                if (!fileName.EndsWith(".til", StringComparison.OrdinalIgnoreCase))
                    continue;

                string idStr = fileName.Substring(0, fileName.Length - 4);
                if (!int.TryParse(idStr, out int id))
                    continue;

                byte[] tilData = L1PakReader.UnPack("Tile", fileName);
                if (tilData == null)
                    continue;

                byte[] md5 = TileHashManager.CalculateMd5(tilData);
                if (TileHashManager.CompareMd5(md5, targetMd5))
                {
                    return id;
                }
            }
            return null;
        }

        /// <summary>
        /// 渲染素材到指定地圖位置並存成圖片
        /// </summary>
        public static int RenderMaterial(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("用法: render-material <material.fs32p> <map_path> <gameX> <gameY> [options]");
                Console.WriteLine();
                Console.WriteLine("參數:");
                Console.WriteLine("  material.fs32p  素材檔案路徑");
                Console.WriteLine("  map_path       地圖資料夾路徑");
                Console.WriteLine("  gameX          遊戲座標 X");
                Console.WriteLine("  gameY          遊戲座標 Y");
                Console.WriteLine();
                Console.WriteLine("選項:");
                Console.WriteLine("  --output <path>   輸出圖片路徑 (預設: material_render.png)");
                Console.WriteLine("  --padding <n>     素材周圍留白格數 (預設: 5)");
                Console.WriteLine("  --no-original     不渲染原本地圖，只渲染素材");
                Console.WriteLine("  --side-by-side    並排顯示 (左:原圖, 右:套用素材後)");
                Console.WriteLine();
                Console.WriteLine("範例:");
                Console.WriteLine("  render-material house.fs32p C:\\client\\map\\4 32800 32800");
                Console.WriteLine("  render-material house.fs32p C:\\client\\map\\4 32800 32800 --side-by-side");
                return 1;
            }

            string materialPath = args[0];
            string mapPath = args[1];

            if (!int.TryParse(args[2], out int gameX) || !int.TryParse(args[3], out int gameY))
            {
                Console.WriteLine("錯誤: gameX 和 gameY 必須是數字");
                return 1;
            }

            // 解析選項
            string outputPath = "material_render.png";
            int padding = 5;
            bool showOriginal = true;
            bool sideBySide = false;

            for (int i = 4; i < args.Length; i++)
            {
                if (args[i] == "--output" && i + 1 < args.Length)
                    outputPath = args[++i];
                else if (args[i] == "--padding" && i + 1 < args.Length)
                    int.TryParse(args[++i], out padding);
                else if (args[i] == "--no-original")
                    showOriginal = false;
                else if (args[i] == "--side-by-side")
                    sideBySide = true;
            }

            // 載入素材
            Console.WriteLine($"載入素材: {materialPath}");
            if (!File.Exists(materialPath))
            {
                Console.WriteLine($"錯誤: 素材檔案不存在: {materialPath}");
                return 1;
            }

            Fs3pData material;
            try
            {
                material = Fs3pParser.ParseFile(materialPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"錯誤: 無法解析素材檔案: {ex.Message}");
                return 1;
            }

            Console.WriteLine($"  名稱: {material.Name}");
            Console.WriteLine($"  尺寸: {material.Width} x {material.Height}");
            Console.WriteLine($"  Layer1: {material.Layer1Items.Count} 項");
            Console.WriteLine($"  Layer4: {material.Layer4Items.Count} 項");

            // 載入地圖
            Console.WriteLine();
            Console.WriteLine($"載入地圖: {mapPath}");
            var loadResult = MapLoader.Load(mapPath);
            if (!loadResult.Success)
            {
                Console.WriteLine("錯誤: 無法載入地圖");
                return 1;
            }

            // 計算渲染範圍（以素材尺寸 + padding 為基準）
            int renderWidth = material.Width + padding * 2;
            int renderHeight = material.Height + padding * 2;

            // 轉換遊戲座標到 Layer1 座標
            int pasteOriginX = gameX * 2;
            int pasteOriginY = gameY;

            // 計算素材的 RelativeX/Y 修正值
            int minRelX = int.MaxValue;
            int minRelY = int.MaxValue;
            int maxRelX = int.MinValue;
            int maxRelY = int.MinValue;
            foreach (var item in material.Layer1Items)
            {
                if (item.RelativeX < minRelX) minRelX = item.RelativeX;
                if (item.RelativeY < minRelY) minRelY = item.RelativeY;
                if (item.RelativeX > maxRelX) maxRelX = item.RelativeX;
                if (item.RelativeY > maxRelY) maxRelY = item.RelativeY;
            }
            foreach (var item in material.Layer4Items)
            {
                if (item.RelativeX < minRelX) minRelX = item.RelativeX;
                if (item.RelativeY < minRelY) minRelY = item.RelativeY;
                if (item.RelativeX > maxRelX) maxRelX = item.RelativeX;
                if (item.RelativeY > maxRelY) maxRelY = item.RelativeY;
            }
            if (minRelX == int.MaxValue) minRelX = 0;
            if (minRelY == int.MaxValue) minRelY = 0;
            if (maxRelX == int.MinValue) maxRelX = 0;
            if (maxRelY == int.MinValue) maxRelY = 0;

            // 使用 maxRelY 作為基準點（素材底部對齊點擊位置）
            int baselineY = maxRelY;

            Console.WriteLine($"  素材 RelativeX 範圍: {minRelX} ~ {maxRelX}");
            Console.WriteLine($"  素材 RelativeY 範圍: {minRelY} ~ {maxRelY}");
            Console.WriteLine($"  基準點: RelativeY={baselineY} (素材底部)");
            Console.WriteLine($"  素材 OriginOffset: ({material.OriginOffsetX}, {material.OriginOffsetY})");

            // 顯示 Layer4 項目分布
            Console.WriteLine();
            Console.WriteLine("  Layer4 項目分布:");
            var groupedByPos = material.Layer4Items
                .GroupBy(item => (item.RelativeX, item.RelativeY))
                .OrderBy(g => g.Key.RelativeY)
                .ThenBy(g => g.Key.RelativeX);
            foreach (var group in groupedByPos)
            {
                var tileIds = string.Join(",", group.Select(i => i.TileId).Distinct().Take(3));
                Console.WriteLine($"    ({group.Key.RelativeX,2}, {group.Key.RelativeY,2}): {group.Count()} 個物件, TileId: {tileIds}");
            }

            // 計算世界像素座標範圍
            // 使用與 ViewportRenderer 相同的座標轉換
            int startGameX = gameX - padding;
            int startGameY = gameY - padding;
            int endGameX = gameX + material.Width + padding;
            int endGameY = gameY + material.Height + padding;

            // 計算世界像素座標（參考 ViewportRenderer 的計算方式）
            // 遊戲座標轉世界座標需要找到對應的 S32
            S32Data referenceS32 = null;
            foreach (var s32 in loadResult.S32Files.Values)
            {
                if (gameX >= s32.SegInfo.nLinBeginX && gameX < s32.SegInfo.nLinBeginX + 64 &&
                    gameY >= s32.SegInfo.nLinBeginY && gameY < s32.SegInfo.nLinBeginY + 64)
                {
                    referenceS32 = s32;
                    break;
                }
            }

            if (referenceS32 == null)
            {
                Console.WriteLine($"錯誤: 找不到座標 ({gameX}, {gameY}) 對應的地圖區塊");
                return 1;
            }

            Console.WriteLine($"  參考 S32: {Path.GetFileName(referenceS32.FilePath)}");
            Console.WriteLine($"  S32 遊戲座標範圍: ({referenceS32.SegInfo.nLinBeginX}, {referenceS32.SegInfo.nLinBeginY}) ~ ({referenceS32.SegInfo.nLinBeginX + 63}, {referenceS32.SegInfo.nLinBeginY + 63})");

            // 計算世界像素座標 (使用 MapForm.JumpToGameCoordinate 相同公式)
            int[] refLoc = referenceS32.SegInfo.GetLoc(1.0);
            int layer3LocalX = gameX - referenceS32.SegInfo.nLinBeginX;  // 遊戲本地座標 (0~63)
            int localX = layer3LocalX * 2;  // 轉換為 Layer1 座標 (0~127)
            int localY = gameY - referenceS32.SegInfo.nLinBeginY;

            int localBaseX = 0 - 24 * (localX / 2);
            int localBaseY = 63 * 12 - 12 * (localX / 2);
            int worldPixelX = refLoc[0] + localBaseX + localX * 24 + localY * 24;
            int worldPixelY = refLoc[1] + localBaseY + localY * 12;

            Console.WriteLine($"  worldPixel (格子左上): ({worldPixelX}, {worldPixelY})");

            // 擴展渲染範圍 (每遊戲格約 48x24 像素)
            int pixelPadding = padding * 48;
            int materialPixelWidth = material.Width * 24 + material.Height * 24;  // 菱形寬度
            int materialPixelHeight = material.Width * 12 + material.Height * 12; // 菱形高度
            int viewWidth = materialPixelWidth + pixelPadding * 2;
            int viewHeight = materialPixelHeight + pixelPadding * 2;

            Rectangle worldRect = new Rectangle(
                worldPixelX - pixelPadding,
                worldPixelY - pixelPadding,
                viewWidth,
                viewHeight
            );

            Console.WriteLine();
            Console.WriteLine($"渲染範圍: {worldRect}");

            // 準備渲染
            var renderer = new ViewportRenderer();
            var checkedFiles = new HashSet<string>(loadResult.S32Files.Keys);

            Bitmap originalBitmap = null;
            Bitmap materialBitmap = null;

            // 渲染原始地圖
            if (showOriginal || sideBySide)
            {
                Console.WriteLine("渲染原始地圖...");
                originalBitmap = renderer.RenderViewport(
                    worldRect,
                    loadResult.S32Files,
                    checkedFiles,
                    true, true, true,
                    out _);
            }

            // 套用素材到 S32 資料（在記憶體中）
            Console.WriteLine("套用素材...");
            Console.WriteLine($"  目標遊戲座標: ({gameX}, {gameY})");
            Console.WriteLine($"  offsetFix: ({minRelX}, {baselineY})");
            int appliedCount = ApplyMaterialToS32(material, loadResult.S32Files, gameX, gameY, minRelX, baselineY, true);
            Console.WriteLine($"  套用了 {appliedCount} 個項目");

            // 渲染套用素材後的地圖
            Console.WriteLine("渲染素材後地圖...");
            materialBitmap = renderer.RenderViewport(
                worldRect,
                loadResult.S32Files,
                checkedFiles,
                true, true, true,
                out _);

            // 計算指定座標在 viewport 中的位置 (紅點)
            int markerX = worldPixelX - worldRect.X;
            int markerY = worldPixelY - worldRect.Y;
            Console.WriteLine($"  紅點位置 (viewport 內): ({markerX}, {markerY})");

            // 計算素材基準點 (RelativeX=0, RelativeY=baselineY) 的實際渲染位置 (黃點)
            // 基準點現在是素材底部 (maxRelY)
            // 使用與 ViewportRenderer 相同的計算方式
            int pasteOriginXL1 = gameX * 2;  // 全域 Layer1 X
            // 基準點的位置 = 點擊位置，所以 RelativeY=baselineY 的項目會在這裡
            // 實際上 itemLocalY 應該是點擊位置的 Y
            int itemLocalX = pasteOriginXL1 + 0 - minRelX - referenceS32.SegInfo.nLinBeginX * 2;  // S32 內 Layer1 X
            int itemLocalY = gameY + baselineY - baselineY - referenceS32.SegInfo.nLinBeginY;  // = gameY - nLinBeginY

            int itemHalfX = itemLocalX / 2;
            int itemBaseX = -24 * itemHalfX;
            int itemBaseY = 63 * 12 - 12 * itemHalfX;
            int itemWorldX = refLoc[0] + itemBaseX + itemLocalX * 24 + itemLocalY * 24;
            int itemWorldY = refLoc[1] + itemBaseY + itemLocalY * 12;

            int yellowMarkerX = itemWorldX - worldRect.X;
            int yellowMarkerY = itemWorldY - worldRect.Y;
            Console.WriteLine($"  黃點位置 (基準點 RelativeY={baselineY} 渲染位置): ({yellowMarkerX}, {yellowMarkerY})");
            Console.WriteLine($"  素材基準點 Local Layer1: ({itemLocalX}, {itemLocalY})");

            // 在素材圖上畫標記
            using (var g = Graphics.FromImage(materialBitmap))
            {
                // 畫黃點 - 素材 RelativeX=0, RelativeY=0 的渲染位置
                using (var brush = new SolidBrush(Color.Yellow))
                {
                    g.FillEllipse(brush, yellowMarkerX - 6, yellowMarkerY - 6, 12, 12);
                }
                using (var pen = new Pen(Color.Yellow, 2))
                {
                    g.DrawLine(pen, yellowMarkerX - 15, yellowMarkerY, yellowMarkerX + 15, yellowMarkerY);
                    g.DrawLine(pen, yellowMarkerX, yellowMarkerY - 15, yellowMarkerX, yellowMarkerY + 15);
                }
                using (var font = new Font("Arial", 9, FontStyle.Bold))
                using (var bgBrush = new SolidBrush(Color.Black))
                using (var fgBrush = new SolidBrush(Color.Yellow))
                {
                    g.DrawString($"Rel(0,0)", font, bgBrush, yellowMarkerX + 12, yellowMarkerY + 3);
                    g.DrawString($"Rel(0,0)", font, fgBrush, yellowMarkerX + 10, yellowMarkerY + 1);
                }

                // 畫紅點 - 指定座標位置
                using (var brush = new SolidBrush(Color.Red))
                {
                    g.FillEllipse(brush, markerX - 8, markerY - 8, 16, 16);
                }
                using (var pen = new Pen(Color.Red, 2))
                {
                    g.DrawLine(pen, markerX - 20, markerY, markerX + 20, markerY);
                    g.DrawLine(pen, markerX, markerY - 20, markerX, markerY + 20);
                }
                using (var font = new Font("Arial", 10, FontStyle.Bold))
                using (var bgBrush = new SolidBrush(Color.Black))
                using (var fgBrush = new SolidBrush(Color.White))
                {
                    g.DrawString($"指定({gameX},{gameY})", font, bgBrush, markerX + 17, markerY - 18);
                    g.DrawString($"指定({gameX},{gameY})", font, fgBrush, markerX + 15, markerY - 20);
                }
            }

            // 輸出圖片
            Bitmap outputBitmap;
            if (sideBySide && originalBitmap != null)
            {
                // 並排顯示
                outputBitmap = new Bitmap(originalBitmap.Width * 2 + 10, originalBitmap.Height);
                using (var g = Graphics.FromImage(outputBitmap))
                {
                    g.Clear(Color.Black);
                    g.DrawImage(originalBitmap, 0, 0);
                    g.DrawImage(materialBitmap, originalBitmap.Width + 10, 0);

                    // 加標籤
                    using (var font = new Font("Arial", 14, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.White))
                    {
                        g.DrawString("原始", font, brush, 10, 10);
                        g.DrawString("套用素材後", font, brush, originalBitmap.Width + 20, 10);
                    }
                }
            }
            else if (!showOriginal)
            {
                outputBitmap = materialBitmap;
            }
            else
            {
                outputBitmap = materialBitmap;
            }

            // 儲存圖片
            Console.WriteLine();
            Console.WriteLine($"儲存圖片: {outputPath}");
            outputBitmap.Save(outputPath, ImageFormat.Png);

            // 清理
            if (originalBitmap != null && originalBitmap != outputBitmap)
                originalBitmap.Dispose();
            if (materialBitmap != null && materialBitmap != outputBitmap)
                materialBitmap.Dispose();

            Console.WriteLine("完成!");
            return 0;
        }

        /// <summary>
        /// 將素材套用到 S32 資料（在記憶體中修改）
        /// </summary>
        private static int ApplyMaterialToS32(
            Fs3pData material,
            Dictionary<string, S32Data> s32Files,
            int gameX, int gameY,
            int offsetFixX, int offsetFixY,
            bool debug = false)
        {
            int count = 0;
            int pasteOriginX = gameX * 2;
            int pasteOriginY = gameY;

            if (debug)
            {
                Console.WriteLine($"  pasteOriginX (Layer1): {pasteOriginX}");
                Console.WriteLine($"  pasteOriginY: {pasteOriginY}");
            }

            // 取得新的 GroupId 起始值
            int maxGroupId = 0;
            foreach (var s32 in s32Files.Values)
            {
                foreach (var obj in s32.Layer4)
                {
                    if (obj.GroupId > maxGroupId)
                        maxGroupId = obj.GroupId;
                }
            }
            int baseGroupId = maxGroupId + 1;

            // 套用 Layer1
            if (material.HasLayer1)
            {
                foreach (var item in material.Layer1Items)
                {
                    int targetGlobalX = pasteOriginX + item.RelativeX - offsetFixX;
                    int targetGlobalY = pasteOriginY + item.RelativeY - offsetFixY;
                    int targetGameX = targetGlobalX / 2;
                    int targetGameY = targetGlobalY;

                    var targetS32 = FindS32ByGameCoords(s32Files, targetGameX, targetGameY);
                    if (targetS32 == null) continue;

                    int localX = targetGlobalX - targetS32.SegInfo.nLinBeginX * 2;
                    int localY = targetGlobalY - targetS32.SegInfo.nLinBeginY;

                    if (localX < 0 || localX >= 128 || localY < 0 || localY >= 64)
                        continue;

                    targetS32.Layer1[localY, localX] = new TileCell
                    {
                        X = localX,
                        Y = localY,
                        TileId = item.TileId,
                        IndexId = item.IndexId
                    };
                    count++;
                }
            }

            // 套用 Layer4
            if (material.HasLayer4)
            {
                int minGameX = int.MaxValue, maxGameX = int.MinValue;
                int minGameY = int.MaxValue, maxGameY = int.MinValue;

                foreach (var item in material.Layer4Items)
                {
                    int targetGlobalX = pasteOriginX + item.RelativeX - offsetFixX;
                    int targetGlobalY = pasteOriginY + item.RelativeY - offsetFixY;
                    int targetGameX = targetGlobalX / 2;
                    int targetGameY = targetGlobalY;

                    if (targetGameX < minGameX) minGameX = targetGameX;
                    if (targetGameX > maxGameX) maxGameX = targetGameX;
                    if (targetGameY < minGameY) minGameY = targetGameY;
                    if (targetGameY > maxGameY) maxGameY = targetGameY;

                    var targetS32 = FindS32ByGameCoords(s32Files, targetGameX, targetGameY);
                    if (targetS32 == null) continue;

                    int objLocalX = targetGlobalX - targetS32.SegInfo.nLinBeginX * 2;
                    int objLocalY = targetGlobalY - targetS32.SegInfo.nLinBeginY;

                    if (objLocalY < 0 || objLocalY >= 64)
                        continue;

                    var newObj = new ObjectTile
                    {
                        GroupId = baseGroupId + item.GroupId,
                        X = objLocalX,
                        Y = objLocalY,
                        Layer = item.Layer,
                        IndexId = item.IndexId,
                        TileId = item.TileId
                    };
                    targetS32.Layer4.Add(newObj);
                    count++;
                }

                if (debug && material.Layer4Items.Count > 0)
                {
                    Console.WriteLine($"  Layer4 實際放置遊戲座標範圍: ({minGameX}, {minGameY}) ~ ({maxGameX}, {maxGameY})");
                }
            }

            return count;
        }

        /// <summary>
        /// 根據遊戲座標找到對應的 S32
        /// </summary>
        private static S32Data FindS32ByGameCoords(Dictionary<string, S32Data> s32Files, int gameX, int gameY)
        {
            foreach (var s32 in s32Files.Values)
            {
                if (gameX >= s32.SegInfo.nLinBeginX && gameX < s32.SegInfo.nLinBeginX + 64 &&
                    gameY >= s32.SegInfo.nLinBeginY && gameY < s32.SegInfo.nLinBeginY + 64)
                {
                    return s32;
                }
            }
            return null;
        }
    }
}
