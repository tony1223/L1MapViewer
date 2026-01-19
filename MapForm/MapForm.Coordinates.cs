using System;
// using System.Drawing; // Replaced with Eto.Drawing
using System.IO;
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer;
using L1MapViewer.Helper;
using L1MapViewer.Localization;
using L1MapViewer.Models;
using static L1MapViewer.Other.Struct;

namespace L1FlyMapViewer
{
    /// <summary>
    /// MapForm - 座標轉換與跳轉
    /// </summary>
    public partial class MapForm
    {
        // 螢幕座標轉遊戲座標
        private (int gameX, int gameY, S32Data s32Data, int localX, int localY) ScreenToGameCoords(int screenX, int screenY)
        {
            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
                return (-1, -1, null, -1, -1);

            // 使用 ViewState 的捲動位置（世界座標）
            // 將螢幕座標轉換為世界座標（考慮縮放和捲動）
            int worldX = (int)(screenX / _viewState.ZoomLevel) + _viewState.ScrollX;
            int worldY = (int)(screenY / _viewState.ZoomLevel) + _viewState.ScrollY;

            // 使用空間索引快速查找可能包含這個點的 S32
            // 建立擴展範圍的查詢矩形（支援超出邊界的格子）
            Rectangle queryRect = new Rectangle(worldX - 3072, worldY - 1536, 6144, 3072);
            var candidateFiles = GetS32FilesInRect(queryRect);

            // 擴展範圍以支援超出邊界的格子 (128x128 Layer3 格子)
            int blockWidth = 128 * 24 * 2;   // 6144 (擴展後)
            int blockHeight = 128 * 12 * 2;  // 3072 (擴展後)
            int offsetX = -1536;  // 向左擴展
            int offsetY = -768;   // 向上擴展

            foreach (var filePath in candidateFiles)
            {
                if (!_document.S32Files.TryGetValue(filePath, out var s32Data))
                    continue;

                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 先檢查點是否在這個 S32 的擴展範圍內（粗略檢查）
                if (worldX < mx + offsetX || worldX > mx + offsetX + blockWidth ||
                    worldY < my + offsetY || worldY > my + offsetY + blockHeight)
                    continue;

                // 使用 Layer3 格子（與 DrawS32Grid 一致）- 擴展到 128x128
                for (int y = 0; y < 128; y++)
                {
                    for (int x3 = 0; x3 < 128; x3++)
                    {
                        int x = x3 * 2;  // Layer1 座標

                        int localBaseX = 0;
                        int localBaseY = 63 * 12;
                        localBaseX -= 24 * (x / 2);
                        localBaseY -= 12 * (x / 2);

                        int X = mx + localBaseX + x * 24 + y * 24;
                        int Y = my + localBaseY + y * 12;

                        // Layer3 菱形的四個頂點（48x24，與格線一致）
                        Point p1 = new Point(X, Y + 12);       // 左
                        Point p2 = new Point(X + 24, Y);       // 上
                        Point p3 = new Point(X + 48, Y + 12);  // 右
                        Point p4 = new Point(X + 24, Y + 24);  // 下

                        if (IsPointInDiamond(new Point(worldX, worldY), p1, p2, p3, p4))
                        {
                            // 返回 Layer3 座標作為遊戲座標
                            int gameX = s32Data.SegInfo.nLinBeginX + x3;
                            int gameY = s32Data.SegInfo.nLinBeginY + y;
                            return (gameX, gameY, s32Data, x, y);
                        }
                    }
                }
            }
            return (-1, -1, null, -1, -1);
        }

        // 遊戲座標轉換為世界座標中心點
        private (int worldX, int worldY) GameToWorldCoords(int gameX, int gameY)
        {
            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
                return (-1, -1);

            L1Map currentMap = Share.MapDataList[_document.MapId];

            // 找到包含這個遊戲座標的 S32
            foreach (var s32Data in _document.S32Files.Values)
            {
                int localX = gameX - s32Data.SegInfo.nLinBeginX;
                int localY = gameY - s32Data.SegInfo.nLinBeginY;

                // 檢查是否在這個 S32 的範圍內
                if (localX >= 0 && localX < 128 && localY >= 0 && localY < 64)
                {
                    // 使用與 RenderS32Map 相同的座標計算方式
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    int mx = loc[0];
                    int my = loc[1];

                    int localBaseX = 0;
                    int localBaseY = 63 * 12;
                    localBaseX -= 24 * (localX / 2);
                    localBaseY -= 12 * (localX / 2);

                    int X = mx + localBaseX + localX * 24 + localY * 24;
                    int Y = my + localBaseY + localY * 12;

                    // 返回菱形中心點（世界座標）
                    return (X + 12, Y + 12);
                }
            }

            return (-1, -1);
        }

        // 遊戲座標轉換為螢幕座標中心點（考慮捲動位置）
        private (int screenX, int screenY) GameToScreenCoords(int gameX, int gameY)
        {
            var (worldX, worldY) = GameToWorldCoords(gameX, gameY);
            if (worldX < 0) return (-1, -1);

            // 使用 ViewState 的捲動位置（世界座標）
            // 世界座標轉螢幕座標（考慮縮放和捲動）
            int screenX = (int)((worldX - _viewState.ScrollX) * _viewState.ZoomLevel);
            int screenY = (int)((worldY - _viewState.ScrollY) * _viewState.ZoomLevel);

            return (screenX, screenY);
        }

        // S32 螢幕座標轉世界座標
        private Point S32ScreenToWorld(int screenX, int screenY)
        {
            // bitmap 繪製位置 = (RenderOrigin - Scroll) * zoom
            int drawOffsetX = (int)((_viewState.RenderOriginX - _viewState.ScrollX) * _viewState.ZoomLevel);
            int drawOffsetY = (int)((_viewState.RenderOriginY - _viewState.ScrollY) * _viewState.ZoomLevel);

            // 螢幕座標 -> bitmap 座標 -> 世界座標
            int bitmapX = (int)((screenX - drawOffsetX) / _viewState.ZoomLevel);
            int bitmapY = (int)((screenY - drawOffsetY) / _viewState.ZoomLevel);
            int worldX = bitmapX + _viewState.RenderOriginX;
            int worldY = bitmapY + _viewState.RenderOriginY;

            return new Point(worldX, worldY);
        }

        // 跳轉到 S32 區塊中心
        private void JumpToS32Block(S32FileItem item)
        {
            // 使用 S32 區塊中心位置的 Layer3 座標
            var segInfo = item.SegInfo;
            int globalX = segInfo.nLinBeginX + 32;  // S32 中心 (64x64 的中心)
            int globalY = segInfo.nLinBeginY + 32;

            // 使用與 JumpToGameCoordinate 相同的座標計算邏輯
            // Layer3 的本地座標
            int layer3LocalX = 32;  // 中心位置
            int localY = 32;

            // 轉換為 Layer1 座標
            int localX = layer3LocalX * 2;

            // 計算螢幕座標
            int[] loc = segInfo.GetLoc(1.0);
            int mx = loc[0];
            int my = loc[1];

            int localBaseX = 0;
            int localBaseY = 63 * 12;
            localBaseX -= 24 * (localX / 2);
            localBaseY -= 12 * (localX / 2);

            // 計算世界座標
            int worldX = mx + localBaseX + localX * 24 + localY * 24;
            int worldY = my + localBaseY + localY * 12;

            // 捲動到該位置（世界座標）
            int viewportWidthWorld = (int)(s32MapPanel.Width / _viewState.ZoomLevel);
            int viewportHeightWorld = (int)(s32MapPanel.Height / _viewState.ZoomLevel);
            int scrollX = worldX - viewportWidthWorld / 2;
            int scrollY = worldY - viewportHeightWorld / 2;

            int maxScrollX = Math.Max(0, _viewState.MapWidth - viewportWidthWorld);
            int maxScrollY = Math.Max(0, _viewState.MapHeight - viewportHeightWorld);
            scrollX = Math.Max(0, Math.Min(scrollX, maxScrollX));
            scrollY = Math.Max(0, Math.Min(scrollY, maxScrollY));

            _viewState.SetScrollSilent(scrollX, scrollY);

            // 更新捲軸
            hScrollBar1.Value = Math.Min(scrollX, hScrollBar1.Maximum);
            vScrollBar1.Value = Math.Min(scrollY, vScrollBar1.Maximum);

            // 重新渲染
            CheckAndRerenderIfNeeded();
            UpdateMiniMap();

            this.toolStripStatusLabel1.Text = $"跳轉至 {item.DisplayName}";
        }

        // 跳轉到 Layer1 座標
        private void JumpToLayer1Coordinate(int layer1GlobalX, int layer1GlobalY)
        {
            // 找到包含此座標的 S32
            foreach (var s32Data in _document.S32Files.Values)
            {
                int segStartX = s32Data.SegInfo.nLinBeginX * 2;
                int segEndX = segStartX + 128;
                int segStartY = s32Data.SegInfo.nLinBeginY;
                int segEndY = segStartY + 64;

                if (layer1GlobalX >= segStartX && layer1GlobalX < segEndX &&
                    layer1GlobalY >= segStartY && layer1GlobalY < segEndY)
                {
                    // 計算本地座標
                    int localX = layer1GlobalX - segStartX;
                    int localY = layer1GlobalY - segStartY;

                    // 使用現有的跳轉方法
                    int gameX = s32Data.SegInfo.nLinBeginX + localX / 2;
                    int gameY = s32Data.SegInfo.nLinBeginY + localY;
                    JumpToGameCoordinate(gameX, gameY);
                    return;
                }
            }
        }

        // 跳轉到指定的遊戲座標 (Layer3 座標系：64x64)
        private void JumpToGameCoordinate(int globalX, int globalY)
        {
            if (!Share.MapDataList.ContainsKey(_document.MapId))
                return;

            L1Map currentMap = Share.MapDataList[_document.MapId];

            // 找到包含此座標的 S32 (使用 Layer3 座標系：64x64)
            foreach (var s32Data in _document.S32Files.Values)
            {
                int segStartX = s32Data.SegInfo.nLinBeginX;
                int segStartY = s32Data.SegInfo.nLinBeginY;
                int segEndX = segStartX + 64;
                int segEndY = segStartY + 64;

                if (globalX >= segStartX && globalX < segEndX &&
                    globalY >= segStartY && globalY < segEndY)
                {
                    // Layer3 的本地座標
                    int layer3LocalX = globalX - segStartX;
                    int localY = globalY - segStartY;

                    // 轉換為 Layer1 座標 (用於高亮和螢幕座標計算)
                    int localX = layer3LocalX * 2;

                    // 計算螢幕座標
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    int mx = loc[0];
                    int my = loc[1];

                    int localBaseX = 0;
                    int localBaseY = 63 * 12;
                    localBaseX -= 24 * (localX / 2);
                    localBaseY -= 12 * (localX / 2);

                    // 計算世界座標（這是格子的螢幕座標，但在這裡是世界座標）
                    int worldX = mx + localBaseX + localX * 24 + localY * 24;
                    int worldY = my + localBaseY + localY * 12;

                    // 捲動到該位置（世界座標）
                    int viewportWidthWorld = (int)(s32MapPanel.Width / _viewState.ZoomLevel);
                    int viewportHeightWorld = (int)(s32MapPanel.Height / _viewState.ZoomLevel);
                    int scrollX = worldX - viewportWidthWorld / 2;
                    int scrollY = worldY - viewportHeightWorld / 2;

                    int maxScrollX = Math.Max(0, _viewState.MapWidth - viewportWidthWorld);
                    int maxScrollY = Math.Max(0, _viewState.MapHeight - viewportHeightWorld);
                    scrollX = Math.Max(0, Math.Min(scrollX, maxScrollX));
                    scrollY = Math.Max(0, Math.Min(scrollY, maxScrollY));

                    _viewState.SetScrollSilent(scrollX, scrollY);

                    // 設定高亮 (使用 Layer1 座標)
                    _editState.HighlightedS32Data = s32Data;
                    _editState.HighlightedCellX = localX;
                    _editState.HighlightedCellY = localY;

                    CheckAndRerenderIfNeeded();
                    UpdateMiniMap();
                    return;
                }
            }
        }

        // 解析座標輸入
        private bool TryParseCoordinate(string input, out int x, out int y, out string mapId)
        {
            x = 0;
            y = 0;
            mapId = null;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // 支援逗號或空格分隔
            string[] parts = input.Split(new char[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            // 支援 "移動 X Y MapID" 格式
            if (parts.Length >= 4 && parts[0] == "移動")
            {
                if (int.TryParse(parts[1].Trim(), out x) &&
                    int.TryParse(parts[2].Trim(), out y))
                {
                    mapId = parts[3].Trim();
                    return true;
                }
            }

            // 支援 "X,Y" 或 "X Y" 格式
            if (parts.Length >= 2 &&
                int.TryParse(parts[0].Trim(), out x) &&
                int.TryParse(parts[1].Trim(), out y))
            {
                return true;
            }
            return false;
        }

        // 重載：不需要 mapId 的版本
        private bool TryParseCoordinate(string input, out int x, out int y)
        {
            return TryParseCoordinate(input, out x, out y, out _);
        }

        // 執行座標跳轉
        private void PerformCoordinateJump()
        {
            // 優先使用 Eto 狀態列文字框的內容
            string input = _statusTxtJump?.Text?.Trim() ?? toolStripJumpTextBox.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                this.toolStripStatusLabel1.Text = "請輸入座標，格式: X,Y 或 移動 X Y 地圖ID";
                return;
            }

            if (TryParseCoordinate(input, out int x, out int y, out string mapId))
            {
                // 如果有指定地圖 ID 且與當前不同，先切換地圖
                if (!string.IsNullOrEmpty(mapId) && mapId != _document.MapId)
                {
                    // 嘗試找到並載入指定的地圖
                    if (Share.MapDataList.ContainsKey(mapId))
                    {
                        LoadMap(mapId);
                        this.toolStripStatusLabel1.Text = $"已切換到地圖 {mapId}，跳轉到座標 ({x}, {y})";
                    }
                    else
                    {
                        this.toolStripStatusLabel1.Text = $"找不到地圖 {mapId}，嘗試在當前地圖跳轉";
                    }
                }

                JumpToGameCoordinate(x, y);
                if (string.IsNullOrEmpty(mapId) || mapId == _document.MapId)
                {
                    this.toolStripStatusLabel1.Text = $"已跳轉到座標 ({x}, {y})";
                }
            }
            else
            {
                this.toolStripStatusLabel1.Text = "座標格式錯誤，請使用格式: X,Y 或 移動 X Y 地圖ID";
            }
        }

        // 狀態列座標跳轉按鈕點擊事件
        private void toolStripJumpButton_Click(object sender, EventArgs e)
        {
            PerformCoordinateJump();
        }

        // 狀態列座標輸入框按鍵事件
        private void toolStripJumpTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.GetKeyCode() == Keys.Enter)
            {
                PerformCoordinateJump();
                e.Handled = true;
                e.SetSuppressKeyPress(true);
            }
        }

        // 複製移動指令按鈕點擊事件
        private void toolStripCopyMoveCmd_Click(object sender, EventArgs e)
        {
            if (_editState.SelectedGameX >= 0 && _editState.SelectedGameY >= 0 && !string.IsNullOrEmpty(_document.MapId))
            {
                string moveCmd = $"移動 {_editState.SelectedGameX} {_editState.SelectedGameY} {_document.MapId}";
                ClipboardHelper.SetText(moveCmd);
                this.toolStripStatusLabel1.Text = $"已複製: {moveCmd}";
            }
        }

        // 更新狀態列遊戲座標
        private void UpdateStatusBarWithGameCoords(int screenX, int screenY)
        {
            var coords = ScreenToGameCoords(screenX, screenY);
            if (coords.gameX >= 0 && coords.gameY >= 0)
            {
                this.toolStripStatusLabel2.Text = $"座標: ({coords.gameX}, {coords.gameY})";
            }
            else
            {
                this.toolStripStatusLabel2.Text = "";
            }
        }

        // 更新狀態列 Layer3 資訊
        private void UpdateStatusBarWithLayer3Info(S32Data s32Data, int cellX, int cellY)
        {
            // 計算第三層座標（第三層是 64x64，第一層是 64x128）
            int layer3X = cellX / 2;
            if (layer3X >= 64) layer3X = 63;

            // 計算遊戲座標（Layer3 尺度，與已選取區域邏輯一致）
            // 已選取區域用: globalLayer1X = nLinBeginX * 2 + LocalX
            // 遊戲座標 = globalLayer1X / 2 = nLinBeginX + LocalX / 2 = nLinBeginX + layer3X
            int gameX = s32Data.SegInfo.nLinBeginX + layer3X;
            int gameY = s32Data.SegInfo.nLinBeginY + cellY;

            // 更新選中的遊戲座標（用於複製移動指令）
            _editState.SelectedGameX = gameX;
            _editState.SelectedGameY = gameY;
            toolStripCopyMoveCmd.Enabled = true;
            toolStripCopyMoveCmd.Text = $"移動 {gameX} {gameY} {_document.MapId}";

            // 取得 S32 檔名
            string s32FileName = Path.GetFileName(s32Data.FilePath);

            // 取得相對於 client 的路徑
            string s32RelativePath = s32Data.FilePath;
            int clientIndex = s32RelativePath.IndexOf("\\client\\", StringComparison.OrdinalIgnoreCase);
            if (clientIndex >= 0)
            {
                s32RelativePath = s32RelativePath.Substring(clientIndex + 1);  // 從 "client\" 開始
            }

            // S32 邊界的遊戲座標（四個角落）
            int linBeginX = s32Data.SegInfo.nLinBeginX;
            int linBeginY = s32Data.SegInfo.nLinBeginY;
            int linEndX = s32Data.SegInfo.nLinEndX;
            int linEndY = s32Data.SegInfo.nLinEndY;

            // 取得 GetLoc 返回值用於除錯
            int[] loc = s32Data.SegInfo.GetLoc(1.0);
            int mx = loc[0];
            int my = loc[1];

            string boundaryInfo = $"S32邊界: [{linBeginX},{linBeginY}~{linEndX},{linEndY}] GetLoc=({mx},{my}) Block=({s32Data.SegInfo.nBlockX:X4},{s32Data.SegInfo.nBlockY:X4})";

            // 取得各層資訊
            string layersInfo = $"L5:{s32Data.Layer5.Count} L6:{s32Data.Layer6.Count} L7:{s32Data.Layer7.Count} L8:{s32Data.Layer8.Count}";

            var attr = s32Data.Layer3[cellY, layer3X];
            if (attr != null)
            {
                this.toolStripStatusLabel1.Text = $"格子({cellX},{cellY}) 遊戲座標({gameX},{gameY}) | 第3層[{layer3X},{cellY}]: Attr1={attr.Attribute1} (0x{attr.Attribute1:X4}) Attr2={attr.Attribute2} (0x{attr.Attribute2:X4}) | {layersInfo} | {s32RelativePath}";
            }
            else
            {
                this.toolStripStatusLabel1.Text = $"格子({cellX},{cellY}) 遊戲座標({gameX},{gameY}) | 第3層: 無資料 | {layersInfo} | {s32RelativePath}";
            }
        }
    }
}
