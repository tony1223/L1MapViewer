using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using L1MapViewer;
using L1MapViewer.Converter;
using L1MapViewer.Helper;
using L1MapViewer.Models;
using L1MapViewer.Other;
using L1MapViewer.Reader;

namespace L1FlyMapViewer
{
    public partial class MapForm : Form, IMapViewer
    {
        // IMapViewer 介面實作 - 明確公開控制項屬性
        ComboBox IMapViewer.comboBox1 => this.comboBox1;
        PictureBox IMapViewer.pictureBox1 => this.pictureBox1;
        PictureBox IMapViewer.pictureBox2 => this.pictureBox2;
        PictureBox IMapViewer.pictureBox3 => this.pictureBox3;
        PictureBox IMapViewer.pictureBox4 => this.pictureBox4;
        VScrollBar IMapViewer.vScrollBar1 => this.vScrollBar1;
        HScrollBar IMapViewer.hScrollBar1 => this.hScrollBar1;
        ToolStripProgressBar IMapViewer.toolStripProgressBar1 => this.toolStripProgressBar1;
        ToolStripStatusLabel IMapViewer.toolStripStatusLabel1 => this.toolStripStatusLabel1;
        ToolStripStatusLabel IMapViewer.toolStripStatusLabel2 => this.toolStripStatusLabel2;
        ToolStripStatusLabel IMapViewer.toolStripStatusLabel3 => this.toolStripStatusLabel3;
        Panel IMapViewer.panel1 => this.panel1;

        private Point mouseDownPoint;
        private bool isMouseDrag;
        private const int DRAG_THRESHOLD = 5;

        // 縮放相關（地圖預覽）
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public double zoomLevel { get; set; } = 1.0;
        private const double ZOOM_MIN = 0.1;
        private const double ZOOM_MAX = 5.0;
        private const double ZOOM_STEP = 0.2;
        private Image originalMapImage;

        // S32 編輯器縮放相關
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public double s32ZoomLevel { get; set; } = 1.0;
        private Image originalS32Image;
        private double pendingS32ZoomLevel = 1.0;

        // 圖層切換防抖Timer
        private System.Windows.Forms.Timer renderDebounceTimer;

        // 縮放防抖Timer
        private System.Windows.Forms.Timer zoomDebounceTimer;

        // 通行性編輯模式
        private enum PassableEditMode
        {
            None,           // 無編輯模式
            SetPassable,    // 設定為可通行
            SetImpassable   // 設定為不可通行
        }
        private PassableEditMode currentPassableEditMode = PassableEditMode.None;

        // 當前選中的格子（用於高亮顯示）
        private S32Data highlightedS32Data = null;
        private int highlightedCellX = -1;
        private int highlightedCellY = -1;

        // 當前選中格子的遊戲座標（用於複製移動指令）
        private int selectedGameX = -1;
        private int selectedGameY = -1;

        // Layer4 群組篩選（勾選的 GroupId 才會渲染）
        private HashSet<int> selectedLayer4Groups = new HashSet<int>();
        private bool isFilteringLayer4Groups = false;

        // 小地圖拖拽
        private bool isMiniMapDragging = false;

        // 主地圖拖拽（中鍵拖拽移動視圖）
        private bool isMainMapDragging = false;
        private Point mainMapDragStartPoint;
        private Point mainMapDragStartScroll;

        // Tile 資料快取 - key: "tileId_indexId"
        private Dictionary<string, byte[]> tileDataCache = new Dictionary<string, byte[]>();

        // 地圖過濾相關
        private List<string> allMapItems = new List<string>();  // 所有地圖項目
        private bool isFiltering = false;  // 防止過濾時觸發 SelectedIndexChanged

        public MapForm()
        {
            InitializeComponent();

            // 初始化渲染防抖Timer（300ms延遲）
            renderDebounceTimer = new System.Windows.Forms.Timer();
            renderDebounceTimer.Interval = 300;
            renderDebounceTimer.Tick += (s, e) =>
            {
                renderDebounceTimer.Stop();
                if (allS32DataDict.Count > 0)
                {
                    RenderS32Map();
                }
            };

            // 初始化縮放防抖Timer（150ms延遲）
            zoomDebounceTimer = new System.Windows.Forms.Timer();
            zoomDebounceTimer.Interval = 150;
            zoomDebounceTimer.Tick += (s, e) =>
            {
                zoomDebounceTimer.Stop();
                ApplyS32Zoom(pendingS32ZoomLevel);
            };

            // 註冊滑鼠滾輪事件用於縮放
            this.panel1.MouseWheel += Panel1_MouseWheel;

            // 確保 panel1 可以接收焦點
            this.panel1.TabStop = true;

            // 當滑鼠進入 panel1 時自動取得焦點
            this.panel1.MouseEnter += (s, e) => this.panel1.Focus();

            // 設置 PictureBox 的 SizeMode 為 StretchImage
            this.pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            this.pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            this.pictureBox3.SizeMode = PictureBoxSizeMode.StretchImage;
            this.pictureBox4.SizeMode = PictureBoxSizeMode.StretchImage;

            // 註冊 S32 編輯器的滑鼠滾輪事件
            this.s32MapPanel.MouseWheel += S32MapPanel_MouseWheel;
            this.s32MapPanel.TabStop = true;
            this.s32MapPanel.MouseEnter += (s, e) => this.s32MapPanel.Focus();

            // 啟用雙緩衝以減少閃爍
            typeof(Panel).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, this.s32MapPanel, new object[] { true });
            typeof(PictureBox).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, this.s32PictureBox, new object[] { true });

            // 當 s32MapPanel 捲動時更新小地圖（使用防抖避免過度更新）
            System.Windows.Forms.Timer scrollUpdateTimer = new System.Windows.Forms.Timer();
            scrollUpdateTimer.Interval = 50;
            scrollUpdateTimer.Tick += (s, e) => {
                scrollUpdateTimer.Stop();
                if (this.s32PictureBox.Image != null)
                    UpdateMiniMap();
            };
            this.s32MapPanel.Scroll += (s, e) => {
                scrollUpdateTimer.Stop();
                scrollUpdateTimer.Start();
            };

            // 註冊 F5 快捷鍵重新載入
            this.KeyPreview = true;
            this.KeyDown += MapForm_KeyDown;
        }

        // 處理快捷鍵
        private void MapForm_KeyDown(object sender, KeyEventArgs e)
        {
            // F5: 重新載入當前地圖
            if (e.KeyCode == Keys.F5)
            {
                e.Handled = true;
                ReloadCurrentMap();
            }
            // Ctrl+C: 複製選取區域
            else if (e.Control && e.KeyCode == Keys.C)
            {
                e.Handled = true;
                CopySelectedCells();
            }
            // Ctrl+V: 貼上選取區域
            else if (e.Control && e.KeyCode == Keys.V)
            {
                e.Handled = true;
                PasteSelectedCells();
            }
            // Escape: 取消複製/貼上模式
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                CancelLayer4CopyPaste();
            }
            // Ctrl+Z: 還原
            else if (e.Control && e.KeyCode == Keys.Z)
            {
                e.Handled = true;
                UndoLastAction();
            }
            // Ctrl+Y: 重做
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                e.Handled = true;
                RedoLastAction();
            }
            // Del: 刪除選取區域內的 Layer4 物件
            else if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                DeleteSelectedLayer4Objects();
            }
        }

        // 刪除選取區域內的 Layer4 物件
        private void DeleteSelectedLayer4Objects()
        {
            if (!isLayer4CopyMode || currentSelectedCells.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "請先使用 Shift+左鍵 選取要刪除的區域";
                return;
            }

            // 呼叫現有的批次刪除功能
            DeleteAllLayer4ObjectsInRegion(currentSelectedCells);

            // 清除選取狀態
            isLayer4CopyMode = false;
            selectedRegion = new Rectangle();
            copyRegionBounds = new Rectangle();
            currentSelectedCells.Clear();
            s32PictureBox.Invalidate();
        }

        // 複製 Layer4 物件
        private void CopySelectedCells()
        {
            if (!isLayer4CopyMode || copyRegionBounds.Width == 0 || copyRegionBounds.Height == 0)
            {
                this.toolStripStatusLabel1.Text = "請先使用 Shift+左鍵 選取要複製的區域";
                return;
            }

            // 檢查是否有選擇任何層
            bool copyLayer1 = copySettingLayer1;
            bool copyLayer3 = copySettingLayer3;
            bool copyLayer4 = copySettingLayer4;

            if (!copyLayer1 && !copyLayer3 && !copyLayer4)
            {
                this.toolStripStatusLabel1.Text = "請點擊「複製設定...」按鈕選擇要複製的圖層";
                return;
            }

            cellClipboard.Clear();

            // 使用已經在 MouseMove/MouseUp 中計算好的 currentSelectedCells
            List<SelectedCell> selectedCells = currentSelectedCells;

            if (selectedCells.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "選取區域內沒有任何格子";
                return;
            }

            // 計算所有格子的全域 Layer1 座標範圍
            int minGlobalX = int.MaxValue, minGlobalY = int.MaxValue;
            foreach (var cell in selectedCells)
            {
                int globalX = cell.S32Data.SegInfo.nLinBeginX * 2 + cell.LocalX;
                int globalY = cell.S32Data.SegInfo.nLinBeginY + cell.LocalY;
                if (globalX < minGlobalX) minGlobalX = globalX;
                if (globalY < minGlobalY) minGlobalY = globalY;
            }

            int layer1Count = 0, layer3Count = 0, layer4Count = 0;

            // 收集每個格子的資料
            foreach (var cell in selectedCells)
            {
                int globalX = cell.S32Data.SegInfo.nLinBeginX * 2 + cell.LocalX;
                int globalY = cell.S32Data.SegInfo.nLinBeginY + cell.LocalY;

                var cellData = new CopiedCellData
                {
                    RelativeX = globalX - minGlobalX,
                    RelativeY = globalY - minGlobalY
                };

                // Layer1 資料（地板）- 一個 Layer3 格子對應兩個 Layer1 格子
                if (copyLayer1)
                {
                    int layer1X = cell.LocalX;  // 偶數
                    if (layer1X >= 0 && layer1X < 128 && cell.LocalY >= 0 && cell.LocalY < 64)
                    {
                        var cell1 = cell.S32Data.Layer1[cell.LocalY, layer1X];
                        if (cell1 != null)
                        {
                            cellData.Layer1Cell1 = new TileCell { X = cell1.X, Y = cell1.Y, TileId = cell1.TileId, IndexId = cell1.IndexId };
                            if (cell1.TileId > 0) layer1Count++;
                        }
                    }
                    if (layer1X + 1 >= 0 && layer1X + 1 < 128 && cell.LocalY >= 0 && cell.LocalY < 64)
                    {
                        var cell2 = cell.S32Data.Layer1[cell.LocalY, layer1X + 1];
                        if (cell2 != null)
                        {
                            cellData.Layer1Cell2 = new TileCell { X = cell2.X, Y = cell2.Y, TileId = cell2.TileId, IndexId = cell2.IndexId };
                            if (cell2.TileId > 0) layer1Count++;
                        }
                    }
                }

                // Layer3 資料（屬性）
                if (copyLayer3)
                {
                    int layer3X = cell.LocalX / 2;  // Layer3 座標
                    if (layer3X >= 0 && layer3X < 64 && cell.LocalY >= 0 && cell.LocalY < 64)
                    {
                        var attr = cell.S32Data.Layer3[cell.LocalY, layer3X];
                        if (attr != null)
                        {
                            cellData.Layer3Attr = new MapAttribute { Attribute1 = attr.Attribute1, Attribute2 = attr.Attribute2 };
                            layer3Count++;
                        }
                    }
                }

                // Layer4 資料已移至跨區塊搜索統一處理

                cellClipboard.Add(cellData);
            }

            // Layer4 物件搜索：遍歷所有 S32，找出座標精確落在選取範圍內的物件
            if (copyLayer4 && selectedCells.Count > 0)
            {
                // 建立選取格子的全域座標集合 (Layer3 座標系)
                var selectedGlobalCells = new HashSet<(int x, int y)>();
                foreach (var cell in selectedCells)
                {
                    int globalL3X = cell.S32Data.SegInfo.nLinBeginX + cell.LocalX / 2;
                    int globalL3Y = cell.S32Data.SegInfo.nLinBeginY + cell.LocalY;
                    selectedGlobalCells.Add((globalL3X, globalL3Y));
                }

                // 遍歷所有 S32 檔案搜索物件（精確匹配選取格子）
                foreach (var s32Data in allS32DataDict.Values)
                {
                    int segStartX = s32Data.SegInfo.nLinBeginX;
                    int segStartY = s32Data.SegInfo.nLinBeginY;

                    for (int i = 0; i < s32Data.Layer4.Count; i++)
                    {
                        var obj = s32Data.Layer4[i];

                        // 計算物件的全域座標 (考慮物件 X 可能超出 0-127 範圍)
                        int objGlobalL3X = segStartX + obj.X / 2;
                        int objGlobalL3Y = segStartY + obj.Y;

                        // 精確檢查是否在選取的格子內
                        if (selectedGlobalCells.Contains((objGlobalL3X, objGlobalL3Y)))
                        {
                            // 檢查群組篩選
                            if (isFilteringLayer4Groups && selectedLayer4Groups.Count > 0 &&
                                !selectedLayer4Groups.Contains(obj.GroupId))
                                continue;

                            // 檢查是否已複製（用全域座標和 GroupId 識別）
                            int objGlobalX = segStartX * 2 + obj.X;
                            int objGlobalY = segStartY + obj.Y;
                            int relX = objGlobalX - minGlobalX;
                            int relY = objGlobalY - minGlobalY;

                            bool alreadyCopied = cellClipboard.Any(cd =>
                                cd.Layer4Objects.Any(o =>
                                    o.RelativeX == relX && o.RelativeY == relY && o.GroupId == obj.GroupId && o.TileId == obj.TileId));

                            if (!alreadyCopied)
                            {
                                // 建立新的 cellData 來存放物件
                                var objCellData = new CopiedCellData
                                {
                                    RelativeX = relX,
                                    RelativeY = relY
                                };
                                objCellData.Layer4Objects.Add(new CopiedObjectTile
                                {
                                    RelativeX = relX,
                                    RelativeY = relY,
                                    GroupId = obj.GroupId,
                                    Layer = obj.Layer,
                                    IndexId = obj.IndexId,
                                    TileId = obj.TileId,
                                    OriginalIndex = i
                                });
                                cellClipboard.Add(objCellData);
                                layer4Count++;
                            }
                        }
                    }
                }
            }

            hasLayer4Clipboard = cellClipboard.Count > 0;
            clipboardSourceMapId = currentMapId;

            // 複製 Layer2 和 Layer5-8 資料（從所有涉及的 S32 收集，根據設定）
            layer2Clipboard.Clear();
            layer5Clipboard.Clear();
            layer6Clipboard.Clear();
            layer7Clipboard.Clear();
            layer8Clipboard.Clear();
            bool copyLayer2 = copySettingLayer2;
            bool copyLayer5to8 = copySettingLayer5to8;

            var processedS32 = new HashSet<S32Data>();
            foreach (var cell in selectedCells)
            {
                if (!processedS32.Contains(cell.S32Data))
                {
                    processedS32.Add(cell.S32Data);
                    // Layer2
                    if (copyLayer2)
                    {
                        foreach (var item in cell.S32Data.Layer2)
                        {
                            // 避免重複加入
                            if (!layer2Clipboard.Any(l => l.Value1 == item.Value1 && l.Value2 == item.Value2 && l.Value3 == item.Value3))
                            {
                                layer2Clipboard.Add(new Layer2Item
                                {
                                    Value1 = item.Value1,
                                    Value2 = item.Value2,
                                    Value3 = item.Value3
                                });
                            }
                        }
                    }
                }
            }

            // Layer5-8：只複製座標落在選取格子範圍內的項目
            if (copyLayer5to8)
            {
                // 建立選取格子的本地座標集合（按 S32 檔案分組）
                var selectedLocalCellsByS32 = new Dictionary<S32Data, HashSet<(int x, int y)>>();
                foreach (var cell in selectedCells)
                {
                    if (!selectedLocalCellsByS32.ContainsKey(cell.S32Data))
                        selectedLocalCellsByS32[cell.S32Data] = new HashSet<(int, int)>();
                    // Layer3/Layer5/Layer7 使用的座標系是 64x64，LocalX 需要除以 2
                    int localL3X = cell.LocalX / 2;
                    selectedLocalCellsByS32[cell.S32Data].Add((localL3X, cell.LocalY));
                }

                foreach (var kvp in selectedLocalCellsByS32)
                {
                    var s32Data = kvp.Key;
                    var localCells = kvp.Value;

                    // Layer5 - 透明圖塊（檢查 X, Y 是否在選取範圍內）
                    foreach (var item in s32Data.Layer5)
                    {
                        // Layer5 的 X, Y 是 64x64 座標系
                        if (localCells.Contains((item.X, item.Y)))
                        {
                            if (!layer5Clipboard.Any(l => l.X == item.X && l.Y == item.Y))
                            {
                                layer5Clipboard.Add(new Layer5Item { X = item.X, Y = item.Y, R = item.R, G = item.G, B = item.B });
                            }
                        }
                    }

                    // Layer6 - 使用的 TilId（合併不重複的，這個是整個 S32 的）
                    foreach (var tilId in s32Data.Layer6)
                    {
                        if (!layer6Clipboard.Contains(tilId))
                        {
                            layer6Clipboard.Add(tilId);
                        }
                    }

                    // Layer7 - 傳送點（檢查 X, Y 是否在選取範圍內）
                    foreach (var item in s32Data.Layer7)
                    {
                        // Layer7 的 X, Y 是 64x64 座標系
                        if (localCells.Contains((item.X, item.Y)))
                        {
                            if (!layer7Clipboard.Any(l => l.Name == item.Name && l.X == item.X && l.Y == item.Y))
                            {
                                layer7Clipboard.Add(new Layer7Item { Name = item.Name, X = item.X, Y = item.Y, TargetMapId = item.TargetMapId, PortalId = item.PortalId });
                            }
                        }
                    }

                    // Layer8 - 特效（檢查 X, Y 是否在選取範圍內）
                    // Layer8 的 X, Y 可能是像素座標，需要轉換為格子座標
                    foreach (var item in s32Data.Layer8)
                    {
                        // 假設 Layer8 的 X, Y 也是 64x64 座標系（如果是像素座標則需要除以 tile 大小）
                        // 根據實際檔案格式，Layer8 座標可能需要調整
                        int cellX = item.X;
                        int cellY = item.Y;
                        // 如果座標超過 64，可能是像素座標，需要除以某個係數（假設是 24）
                        if (cellX >= 64 || cellY >= 64)
                        {
                            cellX = item.X / 24;  // 假設 tile 寬度
                            cellY = item.Y / 24;  // 假設 tile 高度
                        }
                        if (localCells.Contains((cellX, cellY)))
                        {
                            if (!layer8Clipboard.Any(l => l.SprId == item.SprId && l.X == item.X && l.Y == item.Y))
                            {
                                layer8Clipboard.Add(new Layer8Item { SprId = item.SprId, X = item.X, Y = item.Y, Unknown = item.Unknown });
                            }
                        }
                    }
                }
            }

            // 組合提示訊息
            var parts = new List<string>();
            if (copyLayer1 && layer1Count > 0) parts.Add($"L1:{layer1Count}");
            if (copyLayer2 && layer2Clipboard.Count > 0) parts.Add($"L2:{layer2Clipboard.Count}");
            if (copyLayer3 && layer3Count > 0) parts.Add($"L3:{layer3Count}");
            if (copyLayer4 && layer4Count > 0) parts.Add($"L4:{layer4Count}");
            if (copyLayer5to8 && (layer5Clipboard.Count > 0 || layer6Clipboard.Count > 0 || layer7Clipboard.Count > 0 || layer8Clipboard.Count > 0))
                parts.Add($"L5:{layer5Clipboard.Count} L6:{layer6Clipboard.Count} L7:{layer7Clipboard.Count} L8:{layer8Clipboard.Count}");

            string layerInfo = parts.Count > 0 ? string.Join(", ", parts) : "無資料";
            this.toolStripStatusLabel1.Text = $"已複製 {selectedCells.Count} 格 ({layerInfo}) 來源: {currentMapId}，Shift+左鍵選取貼上位置後按 Ctrl+V";

            // 清除選取框但保留複製資料
            isLayer4CopyMode = false;
            selectedRegion = new Rectangle();
            copyRegionBounds = new Rectangle();
            currentSelectedCells.Clear();
            s32PictureBox.Invalidate();
        }

        // 貼上選取區域
        private void PasteSelectedCells()
        {
            if (!hasLayer4Clipboard || cellClipboard.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "剪貼簿沒有資料，請先使用 Shift+左鍵 選取區域後按 Ctrl+C 複製";
                return;
            }

            // 需要先選取貼上位置
            if (!isLayer4CopyMode || copyRegionBounds.Width == 0)
            {
                this.toolStripStatusLabel1.Text = "請使用 Shift+左鍵 選取貼上位置";
                return;
            }

            // 取得貼上位置的全域 Layer1 座標
            int pasteOriginX = copyRegionOrigin.X;
            int pasteOriginY = copyRegionOrigin.Y;

            int layer1Count = 0, layer3Count = 0, layer4Count = 0;
            int skippedCount = 0;

            // 貼上每個格子的資料
            foreach (var cellData in cellClipboard)
            {
                // 計算目標全域 Layer1 座標
                int targetGlobalX = pasteOriginX + cellData.RelativeX;
                int targetGlobalY = pasteOriginY + cellData.RelativeY;

                // 轉換為遊戲座標（Layer3）來找 S32
                int targetGameX = targetGlobalX / 2;
                int targetGameY = targetGlobalY;

                // 找到目標格子所屬的 S32
                S32Data targetS32 = GetS32DataByGameCoords(targetGameX, targetGameY);
                if (targetS32 == null)
                {
                    skippedCount++;
                    continue;
                }

                // 計算目標 S32 內的局部座標
                int localX = targetGlobalX - targetS32.SegInfo.nLinBeginX * 2;
                int localY = targetGlobalY - targetS32.SegInfo.nLinBeginY;

                // 檢查座標是否有效
                if (localX < 0 || localX >= 128 || localY < 0 || localY >= 64)
                {
                    skippedCount++;
                    continue;
                }

                // Layer1 資料（地板）
                if (cellData.Layer1Cell1 != null && localX >= 0 && localX < 128)
                {
                    targetS32.Layer1[localY, localX] = new TileCell
                    {
                        X = localX,
                        Y = localY,
                        TileId = cellData.Layer1Cell1.TileId,
                        IndexId = cellData.Layer1Cell1.IndexId
                    };
                    if (cellData.Layer1Cell1.TileId > 0) layer1Count++;
                    targetS32.IsModified = true;
                }
                if (cellData.Layer1Cell2 != null && localX + 1 >= 0 && localX + 1 < 128)
                {
                    targetS32.Layer1[localY, localX + 1] = new TileCell
                    {
                        X = localX + 1,
                        Y = localY,
                        TileId = cellData.Layer1Cell2.TileId,
                        IndexId = cellData.Layer1Cell2.IndexId
                    };
                    if (cellData.Layer1Cell2.TileId > 0) layer1Count++;
                    targetS32.IsModified = true;
                }

                // Layer3 資料（屬性）
                if (cellData.Layer3Attr != null)
                {
                    int layer3X = localX / 2;
                    if (layer3X >= 0 && layer3X < 64)
                    {
                        targetS32.Layer3[localY, layer3X] = new MapAttribute
                        {
                            Attribute1 = cellData.Layer3Attr.Attribute1,
                            Attribute2 = cellData.Layer3Attr.Attribute2
                        };
                        layer3Count++;
                        targetS32.IsModified = true;
                    }
                }

                // Layer4 資料（物件）- 先刪除目標格子的舊物件，再加入新物件
                if (cellData.Layer4Objects.Count > 0)
                {
                    int layer3X = localX / 2;
                    // 刪除目標格子內的舊物件
                    targetS32.Layer4.RemoveAll(o => (o.X / 2) == layer3X && o.Y == localY);

                    // 加入新物件
                    foreach (var objData in cellData.Layer4Objects.OrderBy(o => o.OriginalIndex))
                    {
                        // 計算物件在目標 S32 內的局部座標
                        int objTargetGlobalX = pasteOriginX + objData.RelativeX;
                        int objTargetGlobalY = pasteOriginY + objData.RelativeY;
                        int objLocalX = objTargetGlobalX - targetS32.SegInfo.nLinBeginX * 2;
                        int objLocalY = objTargetGlobalY - targetS32.SegInfo.nLinBeginY;

                        if (objLocalX >= 0 && objLocalX < 128 && objLocalY >= 0 && objLocalY < 64)
                        {
                            targetS32.Layer4.Add(new ObjectTile
                            {
                                GroupId = objData.GroupId,
                                X = objLocalX,
                                Y = objLocalY,
                                Layer = objData.Layer,
                                IndexId = objData.IndexId,
                                TileId = objData.TileId
                            });
                            layer4Count++;
                        }
                    }
                    targetS32.IsModified = true;
                }
            }

            // 合併 Layer2 和 Layer5-8 到所有受影響的目標 S32（根據設定）
            int layer2AddedCount = 0;
            int layer5to8CopiedCount = 0;
            var affectedS32Set = new HashSet<S32Data>();
            foreach (var cellData in cellClipboard)
            {
                int targetGlobalX = pasteOriginX + cellData.RelativeX;
                int targetGlobalY = pasteOriginY + cellData.RelativeY;
                int targetGameX = targetGlobalX / 2;
                int targetGameY = targetGlobalY;
                S32Data targetS32 = GetS32DataByGameCoords(targetGameX, targetGameY);
                if (targetS32 != null)
                    affectedS32Set.Add(targetS32);
            }

            foreach (var targetS32 in affectedS32Set)
            {
                // 合併 Layer2（加入不存在的項目）- 根據設定
                if (copySettingLayer2 && layer2Clipboard.Count > 0)
                {
                    foreach (var item in layer2Clipboard)
                    {
                        if (!targetS32.Layer2.Any(l => l.Value1 == item.Value1 && l.Value2 == item.Value2 && l.Value3 == item.Value3))
                        {
                            targetS32.Layer2.Add(new Layer2Item
                            {
                                Value1 = item.Value1,
                                Value2 = item.Value2,
                                Value3 = item.Value3
                            });
                            layer2AddedCount++;
                            targetS32.IsModified = true;
                        }
                    }
                }

                // 複製 Layer5-8（合併到目標 S32）- 根據設定
                if (copySettingLayer5to8)
                {
                    // Layer5 - 透明圖塊（合併不重複的）
                    foreach (var item in layer5Clipboard)
                    {
                        if (!targetS32.Layer5.Any(l => l.X == item.X && l.Y == item.Y))
                        {
                            targetS32.Layer5.Add(new Layer5Item { X = item.X, Y = item.Y, R = item.R, G = item.G, B = item.B });
                            targetS32.IsModified = true;
                        }
                    }
                    // Layer6 - 使用的 TilId（合併不重複的）
                    int layer6Added = 0;
                    foreach (var tilId in layer6Clipboard)
                    {
                        if (!targetS32.Layer6.Contains(tilId))
                        {
                            targetS32.Layer6.Add(tilId);
                            targetS32.IsModified = true;
                            layer6Added++;
                        }
                    }
                    if (layer6Added > 0) layer5to8CopiedCount++;
                    // Layer7 - 傳送點（合併不重複的）
                    foreach (var item in layer7Clipboard)
                    {
                        if (!targetS32.Layer7.Any(l => l.Name == item.Name && l.X == item.X && l.Y == item.Y))
                        {
                            targetS32.Layer7.Add(new Layer7Item { Name = item.Name, X = item.X, Y = item.Y, TargetMapId = item.TargetMapId, PortalId = item.PortalId });
                            targetS32.IsModified = true;
                        }
                    }
                    // Layer8 - 特效（合併不重複的）
                    foreach (var item in layer8Clipboard)
                    {
                        if (!targetS32.Layer8.Any(l => l.SprId == item.SprId && l.X == item.X && l.Y == item.Y))
                        {
                            targetS32.Layer8.Add(new Layer8Item { SprId = item.SprId, X = item.X, Y = item.Y, Unknown = item.Unknown });
                            targetS32.IsModified = true;
                        }
                    }
                }
            }

            // 清除選取模式
            isLayer4CopyMode = false;
            selectedRegion = new Rectangle();
            copyRegionBounds = new Rectangle();
            currentSelectedCells.Clear();

            // 重新渲染地圖
            RenderS32Map();

            // 檢查是否跨地圖貼上
            bool isCrossMap = !string.IsNullOrEmpty(clipboardSourceMapId) && clipboardSourceMapId != currentMapId;

            // 組合提示訊息
            var parts = new List<string>();
            if (layer1Count > 0) parts.Add($"L1:{layer1Count}");
            if (layer2AddedCount > 0) parts.Add($"L2:+{layer2AddedCount}");
            if (layer3Count > 0) parts.Add($"L3:{layer3Count}");
            if (layer4Count > 0) parts.Add($"L4:{layer4Count}");
            if (layer5to8CopiedCount > 0) parts.Add($"L6:+{layer5to8CopiedCount}");

            string layerInfo = parts.Count > 0 ? string.Join(", ", parts) : "無資料";
            string message = $"已貼上 {cellClipboard.Count} 格 ({layerInfo})";
            if (isCrossMap)
                message += $" (從 {clipboardSourceMapId} 跨地圖貼上)";
            if (skippedCount > 0)
                message += $"，{skippedCount} 格超出範圍被跳過";
            this.toolStripStatusLabel1.Text = message;
        }

        // 取消複製/貼上模式
        private void CancelLayer4CopyPaste()
        {
            isLayer4CopyMode = false;
            // 恢復顯示全部群組
            UpdateGroupThumbnailsList();
            selectedRegion = new Rectangle();
            copyRegionBounds = new Rectangle();
            currentSelectedCells.Clear();
            s32PictureBox.Invalidate();
            this.toolStripStatusLabel1.Text = "已取消複製/貼上模式";
        }

        // 新增 Undo 記錄
        private void PushUndoAction(UndoAction action)
        {
            undoHistory.Push(action);
            // 新操作會清空 redo 歷史
            redoHistory.Clear();
            // 限制歷史記錄數量
            if (undoHistory.Count > MAX_UNDO_HISTORY)
            {
                var tempStack = new Stack<UndoAction>();
                for (int i = 0; i < MAX_UNDO_HISTORY; i++)
                {
                    tempStack.Push(undoHistory.Pop());
                }
                undoHistory.Clear();
                while (tempStack.Count > 0)
                {
                    undoHistory.Push(tempStack.Pop());
                }
            }
        }

        // 執行還原 (Ctrl+Z)
        private void UndoLastAction()
        {
            if (undoHistory.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "沒有可還原的操作";
                return;
            }

            var action = undoHistory.Pop();

            // 還原刪除的物件（重新新增）
            foreach (var objInfo in action.RemovedObjects)
            {
                S32Data targetS32 = null;
                if (allS32DataDict.TryGetValue(objInfo.S32FilePath, out targetS32))
                {
                    var newObj = new ObjectTile
                    {
                        GroupId = objInfo.GroupId,
                        X = objInfo.LocalX,
                        Y = objInfo.LocalY,
                        Layer = objInfo.Layer,
                        IndexId = objInfo.IndexId,
                        TileId = objInfo.TileId
                    };
                    targetS32.Layer4.Add(newObj);
                    targetS32.IsModified = true;
                }
            }

            // 還原新增的物件（刪除）
            foreach (var objInfo in action.AddedObjects)
            {
                S32Data targetS32 = null;
                if (allS32DataDict.TryGetValue(objInfo.S32FilePath, out targetS32))
                {
                    var objToRemove = targetS32.Layer4.FirstOrDefault(o =>
                        o.X == objInfo.LocalX &&
                        o.Y == objInfo.LocalY &&
                        o.GroupId == objInfo.GroupId &&
                        o.Layer == objInfo.Layer &&
                        o.IndexId == objInfo.IndexId &&
                        o.TileId == objInfo.TileId);

                    if (objToRemove != null)
                    {
                        targetS32.Layer4.Remove(objToRemove);
                        targetS32.IsModified = true;
                    }
                }
            }

            // 還原刪除的第七層資料（重新新增）
            foreach (var layer7Info in action.RemovedLayer7Items)
            {
                S32Data targetS32 = null;
                if (allS32DataDict.TryGetValue(layer7Info.S32FilePath, out targetS32))
                {
                    targetS32.Layer7.Add(new Layer7Item
                    {
                        Name = layer7Info.Name,
                        X = layer7Info.X,
                        Y = layer7Info.Y,
                        TargetMapId = layer7Info.TargetMapId,
                        PortalId = layer7Info.PortalId
                    });
                    targetS32.IsModified = true;
                }
            }

            // 將此動作放入 redo 歷史
            redoHistory.Push(action);

            // 重新渲染地圖
            RenderS32Map();

            this.toolStripStatusLabel1.Text = $"已還原: {action.Description} (Ctrl+Z: {undoHistory.Count} / Ctrl+Y: {redoHistory.Count})";
        }

        // 執行重做 (Ctrl+Y)
        private void RedoLastAction()
        {
            if (redoHistory.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "沒有可重做的操作";
                return;
            }

            var action = redoHistory.Pop();

            // 重做新增的物件（重新新增）
            foreach (var objInfo in action.AddedObjects)
            {
                S32Data targetS32 = null;
                if (allS32DataDict.TryGetValue(objInfo.S32FilePath, out targetS32))
                {
                    var newObj = new ObjectTile
                    {
                        GroupId = objInfo.GroupId,
                        X = objInfo.LocalX,
                        Y = objInfo.LocalY,
                        Layer = objInfo.Layer,
                        IndexId = objInfo.IndexId,
                        TileId = objInfo.TileId
                    };
                    targetS32.Layer4.Add(newObj);
                    targetS32.IsModified = true;
                }
            }

            // 重做刪除的物件（重新刪除）
            foreach (var objInfo in action.RemovedObjects)
            {
                S32Data targetS32 = null;
                if (allS32DataDict.TryGetValue(objInfo.S32FilePath, out targetS32))
                {
                    var objToRemove = targetS32.Layer4.FirstOrDefault(o =>
                        o.X == objInfo.LocalX &&
                        o.Y == objInfo.LocalY &&
                        o.GroupId == objInfo.GroupId &&
                        o.Layer == objInfo.Layer &&
                        o.IndexId == objInfo.IndexId &&
                        o.TileId == objInfo.TileId);

                    if (objToRemove != null)
                    {
                        targetS32.Layer4.Remove(objToRemove);
                        targetS32.IsModified = true;
                    }
                }
            }

            // 重做刪除的第七層資料（重新刪除）
            foreach (var layer7Info in action.RemovedLayer7Items)
            {
                S32Data targetS32 = null;
                if (allS32DataDict.TryGetValue(layer7Info.S32FilePath, out targetS32))
                {
                    var itemToRemove = targetS32.Layer7.FirstOrDefault(l =>
                        l.Name == layer7Info.Name &&
                        l.X == layer7Info.X &&
                        l.Y == layer7Info.Y &&
                        l.TargetMapId == layer7Info.TargetMapId &&
                        l.PortalId == layer7Info.PortalId);

                    if (itemToRemove != null)
                    {
                        targetS32.Layer7.Remove(itemToRemove);
                        targetS32.IsModified = true;
                    }
                }
            }

            // 將此動作放回 undo 歷史
            undoHistory.Push(action);

            // 重新渲染地圖
            RenderS32Map();

            this.toolStripStatusLabel1.Text = $"已重做: {action.Description} (Ctrl+Z: {undoHistory.Count} / Ctrl+Y: {redoHistory.Count})";
        }

        // 取得等距菱形區域內的所有格子（支援長方形）
        private List<SelectedCell> GetCellsInIsometricRegion(Rectangle region)
        {
            List<SelectedCell> result = new List<SelectedCell>();

            if (string.IsNullOrEmpty(currentMapId) || !Share.MapDataList.ContainsKey(currentMapId))
                return result;

            Struct.L1Map currentMap = Share.MapDataList[currentMapId];

            // 計算菱形的參數（使用實際寬高）
            float centerX = region.Left + region.Width / 2f;
            float centerY = region.Top + region.Height / 2f;
            float halfWidth = region.Width / 2f;
            float halfHeight = region.Height / 2f;

            foreach (var s32Data in allS32DataDict.Values)
            {
                // 使用與 RenderS32Map 相同的座標計算方式
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        // 與 drawTilBlock 相同的像素計算
                        int localBaseX = 0;
                        int localBaseY = 63 * 12;
                        localBaseX -= 24 * (x / 2);
                        localBaseY -= 12 * (x / 2);

                        int X = mx + localBaseX + x * 24 + y * 24;
                        int Y = my + localBaseY + y * 12;

                        // 格子中心點
                        float cellCenterX = X + 12;
                        float cellCenterY = Y + 12;

                        // 檢查是否在等距菱形內
                        if (IsPointInIsometricRegion(cellCenterX, cellCenterY, centerX, centerY, halfWidth, halfHeight))
                        {
                            result.Add(new SelectedCell
                            {
                                S32Data = s32Data,
                                LocalX = x,
                                LocalY = y
                            });
                        }
                    }
                }
            }

            return result;
        }

        // 檢查點是否在等距菱形內
        private bool IsPointInIsometricRegion(float px, float py, float centerX, float centerY, float halfWidth, float halfHeight)
        {
            // 使用標準化的菱形檢測公式
            float dx = Math.Abs(px - centerX) / halfWidth;
            float dy = Math.Abs(py - centerY) / halfHeight;
            return (dx + dy) <= 1.0f;
        }

        // 根據選中的格子計算對齊格子的菱形邊界
        private Rectangle GetAlignedBoundsFromCells(List<SelectedCell> cells)
        {
            if (cells.Count == 0 || string.IsNullOrEmpty(currentMapId) || !Share.MapDataList.ContainsKey(currentMapId))
                return new Rectangle();

            Struct.L1Map currentMap = Share.MapDataList[currentMapId];

            // 計算所有格子的螢幕座標範圍
            int minScreenX = int.MaxValue, maxScreenX = int.MinValue;
            int minScreenY = int.MaxValue, maxScreenY = int.MinValue;

            foreach (var cell in cells)
            {
                // 使用與 RenderS32Map 相同的座標計算方式（GetLoc + drawTilBlock 公式）
                int[] loc = cell.S32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 與 drawTilBlock 相同的像素計算
                int localBaseX = 0;
                int localBaseY = 63 * 12;
                localBaseX -= 24 * (cell.LocalX / 2);
                localBaseY -= 12 * (cell.LocalX / 2);

                int X = mx + localBaseX + cell.LocalX * 24 + cell.LocalY * 24;
                int Y = my + localBaseY + cell.LocalY * 12;

                // 菱形的四個頂點
                int left = X + 0;
                int right = X + 24;
                int top = Y + 0;
                int bottom = Y + 24;

                minScreenX = Math.Min(minScreenX, left);
                maxScreenX = Math.Max(maxScreenX, right);
                minScreenY = Math.Min(minScreenY, top);
                maxScreenY = Math.Max(maxScreenY, bottom);
            }

            // 計算菱形的中心和大小
            int centerX = (minScreenX + maxScreenX) / 2;
            int centerY = (minScreenY + maxScreenY) / 2;
            int width = maxScreenX - minScreenX;
            int height = maxScreenY - minScreenY;

            // 返回一個矩形，表示菱形的邊界框
            return new Rectangle(centerX - width / 2, centerY - height / 2, width, height);
        }

        // 取得螢幕矩形區域內的所有格子（用於拖曳選取時即時對齊）
        private List<SelectedCell> GetCellsInScreenRect(Rectangle screenRect)
        {
            List<SelectedCell> result = new List<SelectedCell>();

            if (string.IsNullOrEmpty(currentMapId) || !Share.MapDataList.ContainsKey(currentMapId))
                return result;

            Struct.L1Map currentMap = Share.MapDataList[currentMapId];

            foreach (var s32Data in allS32DataDict.Values)
            {
                // 使用與 RenderS32Map 相同的座標計算方式
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        // 與 drawTilBlock 相同的像素計算
                        int localBaseX = 0;
                        int localBaseY = 63 * 12;
                        localBaseX -= 24 * (x / 2);
                        localBaseY -= 12 * (x / 2);

                        int X = mx + localBaseX + x * 24 + y * 24;
                        int Y = my + localBaseY + y * 12;

                        // 格子中心點
                        int cellCenterX = X + 12;
                        int cellCenterY = Y + 12;

                        // 檢查格子中心是否在螢幕矩形內
                        if (screenRect.Contains(cellCenterX, cellCenterY))
                        {
                            result.Add(new SelectedCell
                            {
                                S32Data = s32Data,
                                LocalX = x,
                                LocalY = y
                            });
                        }
                    }
                }
            }

            return result;
        }

        // 從螢幕起點到終點，計算等距投影矩形範圍內的所有格子
        private List<SelectedCell> GetCellsInIsometricRange(Point startPoint, Point endPoint)
        {
            List<SelectedCell> result = new List<SelectedCell>();

            if (string.IsNullOrEmpty(currentMapId) || !Share.MapDataList.ContainsKey(currentMapId))
                return result;

            // 找出起點和終點對應的遊戲座標
            var (startGameX, startGameY, _, _, _) = ScreenToGameCoords(startPoint.X, startPoint.Y);
            var (endGameX, endGameY, _, _, _) = ScreenToGameCoords(endPoint.X, endPoint.Y);

            // 如果起點找不到，返回空
            if (startGameX < 0)
                return result;

            // 如果終點找不到，使用起點
            if (endGameX < 0)
            {
                endGameX = startGameX;
                endGameY = startGameY;
            }

            // 計算遊戲座標範圍
            int minGameX = Math.Min(startGameX, endGameX);
            int maxGameX = Math.Max(startGameX, endGameX);
            int minGameY = Math.Min(startGameY, endGameY);
            int maxGameY = Math.Max(startGameY, endGameY);

            // 收集範圍內所有格子（使用 Layer3 座標）
            foreach (var s32Data in allS32DataDict.Values)
            {
                for (int y = 0; y < 64; y++)
                {
                    for (int x3 = 0; x3 < 64; x3++)
                    {
                        int gameX = s32Data.SegInfo.nLinBeginX + x3;
                        int gameY = s32Data.SegInfo.nLinBeginY + y;

                        if (gameX >= minGameX && gameX <= maxGameX &&
                            gameY >= minGameY && gameY <= maxGameY)
                        {
                            result.Add(new SelectedCell
                            {
                                S32Data = s32Data,
                                LocalX = x3 * 2,  // 轉換為 Layer1 座標
                                LocalY = y
                            });
                        }
                    }
                }
            }

            return result;
        }

        // 重新載入當前地圖
        private void ReloadCurrentMap()
        {
            if (string.IsNullOrEmpty(currentMapId))
            {
                this.toolStripStatusLabel1.Text = "沒有選擇地圖";
                return;
            }

            // 清除快取
            tileDataCache.Clear();
            highlightedS32Data = null;
            highlightedCellX = -1;
            highlightedCellY = -1;

            // 重新載入 S32 檔案
            this.toolStripStatusLabel1.Text = "正在重新載入...";
            LoadS32FileList(currentMapId);
        }

        private void MapForm_Load(object sender, EventArgs e)
        {
            string iniPath = Path.GetTempPath() + "mapviewer.ini";

            // 檢查是否有保存的天堂路徑，如果有就自動載入
            if (File.Exists(iniPath))
            {
                string savedPath = Utils.GetINI("Path", "LineagePath", "", iniPath);
                if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
                {
                    // 如果還沒載入地圖資料，自動載入
                    if (Share.MapDataList == null || Share.MapDataList.Count == 0)
                    {
                        this.toolStripStatusLabel3.Text = savedPath;
                        Share.LineagePath = savedPath;
                        this.LoadMap(savedPath);
                        return; // LoadMap 會觸發 comboBox1_SelectedIndexChanged，會自動載入上次選擇的地圖
                    }
                }
            }

            // 如果已經有地圖資料，填充 comboBox
            if (Share.MapDataList != null && Share.MapDataList.Count > 0)
            {
                // 儲存所有地圖項目供過濾使用
                allMapItems.Clear();
                foreach (string key in Utils.SortAsc(Share.MapDataList.Keys))
                {
                    Struct.L1Map l1Map = Share.MapDataList[key];
                    allMapItems.Add(string.Format("{0}-{1}", key, l1Map.szName));
                }

                isFiltering = true;
                this.comboBox1.Items.Clear();
                this.comboBox1.BeginUpdate();
                foreach (var item in allMapItems)
                {
                    this.comboBox1.Items.Add(item);
                }
                this.comboBox1.EndUpdate();
                isFiltering = false;

                // 讀取上次選擇的地圖名稱
                if (this.comboBox1.Items.Count > 0)
                {
                    string lastMapName = "";

                    if (File.Exists(iniPath))
                    {
                        lastMapName = Utils.GetINI("MapForm", "LastSelectedMapName", "", iniPath);
                    }

                    // 如果有上次選擇的地圖，找到它
                    int selectedIndex = 0;
                    if (!string.IsNullOrEmpty(lastMapName))
                    {
                        for (int i = 0; i < this.comboBox1.Items.Count; i++)
                        {
                            if (this.comboBox1.Items[i].ToString() == lastMapName)
                            {
                                selectedIndex = i;
                                break;
                            }
                        }
                    }

                    this.comboBox1.SelectedIndex = selectedIndex;
                }
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "請選擇天堂資料夾";
                folderDialog.ShowNewFolderButton = false;

                string iniPath = Path.GetTempPath() + "mapviewer.ini";
                if (File.Exists(iniPath))
                {
                    string savedPath = Utils.GetINI("Path", "LineagePath", "", iniPath);
                    if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
                        folderDialog.SelectedPath = savedPath;
                }
                else
                {
                    folderDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                }

                if (folderDialog.ShowDialog() != DialogResult.OK || string.IsNullOrEmpty(folderDialog.SelectedPath))
                    return;

                this.toolStripStatusLabel3.Text = folderDialog.SelectedPath;
                Share.LineagePath = folderDialog.SelectedPath;
                Utils.WriteINI("Path", "LineagePath", folderDialog.SelectedPath, iniPath);
                this.LoadMap(folderDialog.SelectedPath);
            }
        }

        // 匯出地圖通行資料給伺服器使用（L1J 格式）
        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentMapId) || !Share.MapDataList.ContainsKey(currentMapId))
            {
                MessageBox.Show("請先載入地圖！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (allS32DataDict.Count == 0)
            {
                MessageBox.Show("請先在 S32 編輯器中載入地圖資料！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "文字檔 (*.txt)|*.txt";
                saveDialog.FileName = $"{currentMapId}.txt";
                saveDialog.Title = "匯出地圖通行資料";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        ExportMapData(saveDialog.FileName);
                        MessageBox.Show($"已成功匯出至：\n{saveDialog.FileName}", "匯出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"匯出失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // 匯出地圖資料（參考 MapTool 的格式）
        // MapTool 的 readS32 讀取方式：
        // - 從 offset 32768 + tileCount*6 + 2 開始
        // - 每個格子讀 4 bytes: [Attribute1, ?, Attribute2, ?]
        // - 迴圈是 64x64，tileList_t1 存 Attribute1，tileList_t3 存 Attribute2
        // - xLength = S32數量 * 64（不是 128！）
        private void ExportMapData(string filePath)
        {
            Struct.L1Map currentMap = Share.MapDataList[currentMapId];

            // MapTool 的維度計算：每個 S32 是 64x64
            // 但我們的 Layer3 也是 64x64，所以維度是一樣的
            int xLength = currentMap.nLinLengthX / 2;  // Layer3 的 X 維度 = Layer1 / 2
            int yLength = currentMap.nLinLengthY;
            int xBegin = currentMap.nLinBeginX;
            int yBegin = currentMap.nLinBeginY;

            // 建立 tileList_t1 和 tileList_t3 陣列（對應 MapTool 的格式）
            // tileList_t1[x,y] = Layer3 的 Attribute1
            // tileList_t3[x,y] = Layer3 的 Attribute2
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

            // 從 S32 資料填充 tileList
            foreach (var s32Data in allS32DataDict.Values)
            {
                // 計算這個 S32 在地圖陣列中的偏移（以 64 為單位）
                int offsetX = (s32Data.SegInfo.nLinBeginX - xBegin) / 2;  // Layer3 座標
                int offsetY = s32Data.SegInfo.nLinBeginY - yBegin;

                // Layer3 是 64x64
                for (int ly = 0; ly < 64; ly++)
                {
                    for (int lx = 0; lx < 64; lx++)
                    {
                        var attr = s32Data.Layer3[ly, lx];
                        if (attr == null) continue;

                        int gx = offsetX + lx;
                        int gy = offsetY + ly;

                        if (gx >= 0 && gx < xLength && gy >= 0 && gy < yLength)
                        {
                            // Attribute1 對應 tileList_t1
                            // Attribute2 對應 tileList_t3
                            tileList_t1[gx, gy] = attr.Attribute1;
                            tileList_t3[gx, gy] = attr.Attribute2;
                        }
                    }
                }
            }

            // 計算 8 方向通行性（參考 MapTool 的 decryptData）
            int[,] tileList = new int[xLength, yLength];

            for (int x = 0; x < xLength; x++)
            {
                for (int y = 0; y < yLength; y++)
                {
                    if (x + 1 < xLength && y + 1 < yLength && x - 1 >= 0 && y - 1 >= 0)
                    {
                        // D0: 下方 - isPassable_D0(x, y) => (tileList_t1[x, y + 1] & 1) == 0
                        if ((tileList_t1[x, y + 1] & 1) == 0)
                            tileList[x, y] += 1;
                        // D4: 上方 - isPassable_D4(x, y) => (tileList_t1[x, y] & 1) == 0
                        if ((tileList_t1[x, y] & 1) == 0)
                            tileList[x, y] += 2;
                        // D2: 左方 - isPassable_D2(x, y) => (tileList_t3[x - 1, y] & 1) == 0
                        if ((tileList_t3[x - 1, y] & 1) == 0)
                            tileList[x, y] += 4;
                        // D6: 右方 - isPassable_D6(x, y) => (tileList_t3[x, y] & 1) == 0
                        if ((tileList_t3[x, y] & 1) == 0)
                            tileList[x, y] += 8;

                        // D1: 左下對角 - isPassable_D1(x - 1, y + 1)
                        if (IsPassable_D1(tileList_t1, tileList_t3, x - 1, y + 1, xLength, yLength))
                            tileList[x, y] += 16;
                        // D3: 左上對角 - isPassable_D3(x - 1, y - 1)
                        if (IsPassable_D3(tileList_t1, tileList_t3, x - 1, y - 1, xLength, yLength))
                            tileList[x, y] += 32;
                        // D5: 右上對角 - isPassable_D5(x + 1, y - 1)
                        if (IsPassable_D5(tileList_t1, tileList_t3, x + 1, y - 1, xLength, yLength))
                            tileList[x, y] += 64;
                        // D7: 右下對角 - isPassable_D7(x + 1, y + 1)
                        if (IsPassable_D7(tileList_t1, tileList_t3, x + 1, y + 1, xLength, yLength))
                            tileList[x, y] += 128;

                        // 區域類型 - getZone(x, y) 使用 tileList_t1[x, y]
                        tileList[x, y] += GetZone(tileList_t1[x, y]);
                    }
                }
            }

            // 轉換為 L1J 格式
            int[,] l1jData = FormatL1J(tileList, xLength, yLength);

            // 寫入檔案（與 MapTool 相同格式，不含座標範圍）
            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // 寫入資料（y 為行，x 為列）
                for (int y = 0; y < yLength; y++)
                {
                    StringBuilder line = new StringBuilder();
                    for (int x = 0; x < xLength; x++)
                    {
                        if (x > 0) line.Append(",");
                        line.Append(l1jData[x, y]);
                    }
                    writer.WriteLine(line.ToString());
                }
            }

            this.toolStripStatusLabel1.Text = $"已匯出 {currentMapId}.txt ({xLength}x{yLength})";
        }

        // 對角方向通行性判斷（完全按照 MapTool 的邏輯）
        // isPassable_D1(x, y) => t1[x,y] & t1[x+1,y] & t3[x+1,y] & t3[x+1,y-1] 都為 0
        private bool IsPassable_D1(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 0 || x + 1 >= xLen || y < 0 || y >= yLen || y - 1 < 0) return false;
            return (t1[x, y] & 1) == 0 && (t1[x + 1, y] & 1) == 0 &&
                   (t3[x + 1, y] & 1) == 0 && (t3[x + 1, y - 1] & 1) == 0;
        }

        // isPassable_D3(x, y) => t1[x,y+1] & t1[x+1,y+1] & t3[x,y] & t3[x,y+1] 都為 0
        private bool IsPassable_D3(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 0 || x + 1 >= xLen || y < 0 || y + 1 >= yLen) return false;
            return (t1[x, y + 1] & 1) == 0 && (t1[x + 1, y + 1] & 1) == 0 &&
                   (t3[x, y] & 1) == 0 && (t3[x, y + 1] & 1) == 0;
        }

        // isPassable_D5(x, y) => t1[x,y+1] & t1[x-1,y+1] & t3[x-1,y] & t3[x-1,y+1] 都為 0
        private bool IsPassable_D5(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 1 || x >= xLen || y < 0 || y + 1 >= yLen) return false;
            return (t1[x, y + 1] & 1) == 0 && (t1[x - 1, y + 1] & 1) == 0 &&
                   (t3[x - 1, y] & 1) == 0 && (t3[x - 1, y + 1] & 1) == 0;
        }

        // isPassable_D7(x, y) => t1[x,y] & t1[x-1,y] & t3[x-1,y] & t3[x-1,y-1] 都為 0
        private bool IsPassable_D7(int[,] t1, int[,] t3, int x, int y, int xLen, int yLen)
        {
            if (x < 1 || x >= xLen || y < 1 || y >= yLen) return false;
            return (t1[x, y] & 1) == 0 && (t1[x - 1, y] & 1) == 0 &&
                   (t3[x - 1, y] & 1) == 0 && (t3[x - 1, y - 1] & 1) == 0;
        }

        // 取得區域類型（完全按照 MapTool 的 getZone 邏輯）
        // 看 tileList_t1[x,y] 的低 4 位元（十六進位的最後一位）
        private int GetZone(int tileValue)
        {
            string hex = (tileValue & 0x0F).ToString("X1");
            // 0-3: 一般區域 (256)
            if (hex == "0" || hex == "1" || hex == "2" || hex == "3")
                return 256;
            // 4-7, C-F: 安全區域 (512)
            else if (hex == "4" || hex == "5" || hex == "6" || hex == "7" ||
                     hex == "C" || hex == "D" || hex == "E" || hex == "F")
                return 512;
            // 8-B: 戰鬥區域 (1024)
            else if (hex == "8" || hex == "9" || hex == "A" || hex == "B")
                return 1024;
            return 256;
        }

        // 轉換為 L1J 格式（完全按照 MapTool 的 formate_L1J 邏輯）
        private int[,] FormatL1J(int[,] tileList, int xLength, int yLength)
        {
            int[,] result = new int[xLength, yLength];

            for (int y = 0; y < yLength; y++)
            {
                for (int x = 0; x < xLength; x++)
                {
                    int tile = tileList[x, y];

                    // (tile & 1) == 1 || (tile & 2) == 2 => +2
                    if ((tile & 1) == 1 || (tile & 2) == 2)
                        result[x, y] += 2;

                    // (tile & 4) == 4 || (tile & 8) == 8 => +1
                    if ((tile & 4) == 4 || (tile & 8) == 8)
                        result[x, y] += 1;

                    // (tile & 1) == 1 && (tile & 2) == 2 => +8
                    if ((tile & 1) == 1 && (tile & 2) == 2)
                        result[x, y] += 8;

                    // (tile & 4) == 4 && (tile & 8) == 8 => +4
                    if ((tile & 4) == 4 && (tile & 8) == 8)
                        result[x, y] += 4;

                    // (tile & 256) == 256 => 什麼都不做（一般區域）
                    // (tile & 512) == 512 => +16（安全區域）
                    if ((tile & 512) == 512)
                        result[x, y] += 16;

                    // (tile & 1024) == 1024 => +32（戰鬥區域）
                    if ((tile & 1024) == 1024)
                        result[x, y] += 32;
                }
            }

            return result;
        }

        public void LoadMap(string selectedPath)
        {
            Utils.ShowProgressBar(true, this);
            var dictionary = L1MapHelper.Read(selectedPath);

            // 儲存所有地圖項目供過濾使用
            allMapItems.Clear();
            foreach (string key in Utils.SortAsc(dictionary.Keys))
            {
                Struct.L1Map l1Map = dictionary[key];
                allMapItems.Add(string.Format("{0}-{1}", key, l1Map.szName));
            }

            // 填充 ComboBox
            isFiltering = true;
            this.comboBox1.Items.Clear();
            this.comboBox1.BeginUpdate();
            foreach (var item in allMapItems)
            {
                this.comboBox1.Items.Add(item);
            }
            this.comboBox1.EndUpdate();
            isFiltering = false;

            // 讀取上次選擇的地圖名稱（改用名稱而非索引，避免過濾後索引錯亂）
            if (this.comboBox1.Items.Count > 0)
            {
                string iniPath = Path.GetTempPath() + "mapviewer.ini";
                string lastMapName = "";

                if (File.Exists(iniPath))
                {
                    lastMapName = Utils.GetINI("MapForm", "LastSelectedMapName", "", iniPath);
                }

                // 如果有上次選擇的地圖，找到它
                int selectedIndex = 0;
                if (!string.IsNullOrEmpty(lastMapName))
                {
                    for (int i = 0; i < this.comboBox1.Items.Count; i++)
                    {
                        if (this.comboBox1.Items[i].ToString() == lastMapName)
                        {
                            selectedIndex = i;
                            break;
                        }
                    }
                }

                this.comboBox1.SelectedIndex = selectedIndex;
            }

            this.toolStripStatusLabel2.Text = "MapCount=" + dictionary.Count;
            Utils.ShowProgressBar(false, this);
        }

        // 地圖搜尋過濾
        private void comboBox1_TextChanged(object sender, EventArgs e)
        {
            if (isFiltering) return;

            string searchText = this.comboBox1.Text.ToLower();

            // 如果文字為空或是選中了一個項目，不過濾
            if (string.IsNullOrEmpty(searchText) || this.comboBox1.SelectedItem != null &&
                this.comboBox1.SelectedItem.ToString().ToLower() == searchText)
            {
                return;
            }

            isFiltering = true;

            // 記住游標位置
            int cursorPos = this.comboBox1.SelectionStart;

            // 過濾項目
            var filteredItems = allMapItems
                .Where(item => item.ToLower().Contains(searchText))
                .ToList();

            this.comboBox1.BeginUpdate();
            this.comboBox1.Items.Clear();
            foreach (var item in filteredItems)
            {
                this.comboBox1.Items.Add(item);
            }
            this.comboBox1.EndUpdate();

            // 還原文字和游標位置
            this.comboBox1.Text = searchText;
            this.comboBox1.SelectionStart = cursorPos;
            this.comboBox1.SelectionLength = 0;

            // 顯示下拉選單
            if (filteredItems.Count > 0 && !this.comboBox1.DroppedDown)
            {
                this.comboBox1.DroppedDown = true;
            }

            isFiltering = false;

            this.toolStripStatusLabel1.Text = $"找到 {filteredItems.Count} 個地圖";
        }

        // 清空搜尋，顯示所有地圖
        private void ResetMapFilter()
        {
            isFiltering = true;
            this.comboBox1.BeginUpdate();
            this.comboBox1.Items.Clear();
            foreach (var item in allMapItems)
            {
                this.comboBox1.Items.Add(item);
            }
            this.comboBox1.EndUpdate();
            isFiltering = false;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isFiltering) return;  // 過濾中不觸發
            if (this.comboBox1.SelectedItem == null)
                return;

            // 保存當前選擇的地圖名稱（用名稱而非索引，避免過濾後錯亂）
            string iniPath = Path.GetTempPath() + "mapviewer.ini";
            Utils.WriteINI("MapForm", "LastSelectedMapName", this.comboBox1.SelectedItem.ToString(), iniPath);

            // 重置縮放級別
            zoomLevel = 1.0;
            if (originalMapImage != null)
            {
                originalMapImage.Dispose();
                originalMapImage = null;
            }

            // 重置 S32 編輯器縮放級別
            s32ZoomLevel = 1.0;
            if (originalS32Image != null)
            {
                originalS32Image.Dispose();
                originalS32Image = null;
            }

            // 清除 Tile 快取（切換地圖時釋放記憶體）
            tileDataCache.Clear();

            string szSelectName = this.comboBox1.SelectedItem.ToString();
            if (szSelectName.Contains("-"))
                szSelectName = szSelectName.Split('-')[0].Trim();
            L1MapHelper.doPaintEvent(szSelectName, this);

            // 等待地圖繪製完成後更新小地圖
            Application.DoEvents();
            UpdateMiniMap();

            // 載入該地圖的 s32 檔案清單
            LoadS32FileList(szSelectName);

            // 選擇後重置過濾，顯示所有地圖
            ResetMapFilter();
        }

        // 更新小地圖
        private void UpdateMiniMap()
        {
            try
            {
                if (this.s32PictureBox.Image == null)
                    return;

                int miniWidth = 260;
                int miniHeight = 260;
                int pictureWidth = this.s32PictureBox.Width;
                int pictureHeight = this.s32PictureBox.Height;

                // 計算縮放比例和偏移
                float scaleX = (float)miniWidth / pictureWidth;
                float scaleY = (float)miniHeight / pictureHeight;
                float scale = Math.Min(scaleX, scaleY);

                int scaledWidth = (int)(pictureWidth * scale);
                int scaledHeight = (int)(pictureHeight * scale);
                int offsetX = (miniWidth - scaledWidth) / 2;
                int offsetY = (miniHeight - scaledHeight) / 2;

                // 建立基底圖（不含紅框）
                if (miniMapBaseImage != null)
                    miniMapBaseImage.Dispose();
                miniMapBaseImage = new Bitmap(miniWidth, miniHeight);
                using (Graphics g = Graphics.FromImage(miniMapBaseImage))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                    g.FillRectangle(Brushes.Black, 0, 0, miniWidth, miniHeight);
                    g.DrawImage(this.s32PictureBox.Image, offsetX, offsetY, scaledWidth, scaledHeight);
                }

                // 建立顯示圖（含紅框）
                Bitmap miniMap = (Bitmap)miniMapBaseImage.Clone();
                using (Graphics g = Graphics.FromImage(miniMap))
                {
                    // 繪製視窗位置紅框
                    if (this.s32MapPanel.Width > 0 && this.s32MapPanel.Height > 0 && pictureWidth > 0 && pictureHeight > 0)
                    {
                        float viewPortScaleX = (float)scaledWidth / pictureWidth;
                        float viewPortScaleY = (float)scaledHeight / pictureHeight;

                        // 取得目前捲動位置（AutoScrollPosition 返回負值）
                        int scrollX = -this.s32MapPanel.AutoScrollPosition.X;
                        int scrollY = -this.s32MapPanel.AutoScrollPosition.Y;

                        int viewX = (int)(scrollX * viewPortScaleX) + offsetX;
                        int viewY = (int)(scrollY * viewPortScaleY) + offsetY;
                        int viewWidth = (int)(this.s32MapPanel.Width * viewPortScaleX);
                        int viewHeight = (int)(this.s32MapPanel.Height * viewPortScaleY);

                        using (Pen viewPortPen = new Pen(Color.Red, 2))
                        {
                            g.DrawRectangle(viewPortPen, viewX, viewY, viewWidth, viewHeight);
                        }
                    }
                }

                if (this.miniMapPictureBox.Image != null && this.miniMapPictureBox.Image != miniMapBaseImage)
                    this.miniMapPictureBox.Image.Dispose();
                this.miniMapPictureBox.Image = miniMap;
            }
            catch
            {
                // 忽略錯誤
            }
        }

        public void vScrollBar1_Scroll(object sender, ScrollEventArgs e)
        {
            this.pictureBox1.Top = -this.vScrollBar1.Value;
            if (!this.isMouseDrag)
                UpdateMiniMap();
        }

        public void hScrollBar1_Scroll(object sender, ScrollEventArgs e)
        {
            this.pictureBox1.Left = -this.hScrollBar1.Value;
            if (!this.isMouseDrag)
                UpdateMiniMap();
        }

        private void pictureBox2_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.mouseDownPoint = Cursor.Position;
                this.isMouseDrag = true;
                this.Cursor = Cursors.Hand;
            }
            else if (e.Button == MouseButtons.Right)
            {
                L1MapHelper.doLocTagEvent(e, this);
            }
        }

        private void pictureBox2_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            this.Cursor = Cursors.Default;

            if (this.isMouseDrag)
            {
                int dragDistance = Math.Abs(Cursor.Position.X - this.mouseDownPoint.X) +
                                  Math.Abs(Cursor.Position.Y - this.mouseDownPoint.Y);

                if (dragDistance < DRAG_THRESHOLD)
                {
                    int adjustedX = (int)(e.X / this.zoomLevel);
                    int adjustedY = (int)(e.Y / this.zoomLevel);
                    var linLoc = L1MapHelper.GetLinLocation(adjustedX, adjustedY);
                    if (linLoc != null)
                    {
                        ShowSinglePoint(linLoc.x, linLoc.y);
                    }
                }

                UpdateMiniMap();
                this.isMouseDrag = false;
            }
        }

        private void pictureBox2_MouseMove(object sender, MouseEventArgs e)
        {
            if (this.isMouseDrag)
            {
                try
                {
                    int deltaX = Cursor.Position.X - this.mouseDownPoint.X;
                    int deltaY = Cursor.Position.Y - this.mouseDownPoint.Y;

                    int newScrollX = this.hScrollBar1.Value - deltaX;
                    int newScrollY = this.vScrollBar1.Value - deltaY;

                    if (this.hScrollBar1.Maximum > 0)
                    {
                        newScrollX = Math.Max(this.hScrollBar1.Minimum, Math.Min(newScrollX, this.hScrollBar1.Maximum));
                        this.hScrollBar1.Value = newScrollX;
                    }

                    if (this.vScrollBar1.Maximum > 0)
                    {
                        newScrollY = Math.Max(this.vScrollBar1.Minimum, Math.Min(newScrollY, this.vScrollBar1.Maximum));
                        this.vScrollBar1.Value = newScrollY;
                    }

                    this.vScrollBar1_Scroll(null, null);
                    this.hScrollBar1_Scroll(null, null);

                    this.mouseDownPoint = Cursor.Position;
                }
                catch
                {
                    // 忽略錯誤
                }
            }
            else
            {
                L1MapHelper.doMouseMoveEvent(e, this);
            }
        }

        private void pictureBox2_Paint(object sender, PaintEventArgs e)
        {
            // 可以在這裡繪製額外的標記
        }

        // 滑鼠滾輪縮放地圖
        private void Panel1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys != Keys.Control)
                return;

            if (this.pictureBox1.Image == null)
                return;

            double oldZoom = zoomLevel;
            if (e.Delta > 0)
            {
                zoomLevel = Math.Min(ZOOM_MAX, zoomLevel + ZOOM_STEP);
            }
            else
            {
                zoomLevel = Math.Max(ZOOM_MIN, zoomLevel - ZOOM_STEP);
            }

            if (Math.Abs(oldZoom - zoomLevel) < 0.001)
                return;

            if (originalMapImage == null)
            {
                originalMapImage = (Image)this.pictureBox1.Image.Clone();
            }

            this.panel1.SuspendLayout();

            try
            {
                Point mousePos = this.panel1.PointToClient(Cursor.Position);

                double xRatio = (double)(mousePos.X + this.hScrollBar1.Value) / this.pictureBox1.Width;
                double yRatio = (double)(mousePos.Y + this.vScrollBar1.Value) / this.pictureBox1.Height;

                int newWidth = (int)(originalMapImage.Width * zoomLevel);
                int newHeight = (int)(originalMapImage.Height * zoomLevel);

                this.pictureBox1.Size = new Size(newWidth, newHeight);
                this.pictureBox2.Size = new Size(newWidth, newHeight);
                this.pictureBox3.Size = new Size(newWidth, newHeight);
                this.pictureBox4.Size = new Size(newWidth, newHeight);

                this.hScrollBar1.Maximum = Math.Max(0, newWidth);
                this.vScrollBar1.Maximum = Math.Max(0, newHeight);

                int newScrollX = (int)(newWidth * xRatio - mousePos.X);
                int newScrollY = (int)(newHeight * yRatio - mousePos.Y);

                this.hScrollBar1.Value = Math.Max(0, Math.Min(this.hScrollBar1.Maximum - this.panel1.Width, newScrollX));
                this.vScrollBar1.Value = Math.Max(0, Math.Min(this.vScrollBar1.Maximum - this.panel1.Height, newScrollY));

                this.vScrollBar1_Scroll(null, null);
                this.hScrollBar1_Scroll(null, null);
            }
            finally
            {
                this.panel1.ResumeLayout();
            }

            this.panel1.Invalidate();
        }

        // S32 編輯器滑鼠滾輪事件
        private void S32MapPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            // Ctrl+滾輪 = 縮放
            if (Control.ModifierKeys == Keys.Control)
            {
                if (this.s32PictureBox.Image == null)
                    return;

                double oldZoom = pendingS32ZoomLevel;
                if (e.Delta > 0)
                {
                    pendingS32ZoomLevel = Math.Min(ZOOM_MAX, pendingS32ZoomLevel + ZOOM_STEP);
                }
                else
                {
                    pendingS32ZoomLevel = Math.Max(ZOOM_MIN, pendingS32ZoomLevel - ZOOM_STEP);
                }

                if (Math.Abs(oldZoom - pendingS32ZoomLevel) < 0.001)
                    return;

                // 立即更新狀態欄顯示縮放級別（給用戶即時反饋）
                this.lblS32Info.Text = $"縮放: {pendingS32ZoomLevel:P0}";

                // 使用防抖計時器延遲執行實際的縮放操作
                zoomDebounceTimer.Stop();
                zoomDebounceTimer.Start();

                // 阻止事件繼續傳遞
                ((HandledMouseEventArgs)e).Handled = true;
                return;
            }

            // Shift+滾輪 = 左右捲動，普通滾輪 = 上下捲動
            int scrollAmount = 120;  // 捲動量（像素）
            int currentX = -this.s32MapPanel.AutoScrollPosition.X;
            int currentY = -this.s32MapPanel.AutoScrollPosition.Y;

            if (Control.ModifierKeys == Keys.Shift)
            {
                // 左右捲動
                int newX = currentX - (e.Delta > 0 ? scrollAmount : -scrollAmount);
                newX = Math.Max(0, Math.Min(newX, this.s32PictureBox.Width - this.s32MapPanel.Width));
                this.s32MapPanel.AutoScrollPosition = new Point(newX, currentY);
            }
            else
            {
                // 上下捲動
                int newY = currentY - (e.Delta > 0 ? scrollAmount : -scrollAmount);
                newY = Math.Max(0, Math.Min(newY, this.s32PictureBox.Height - this.s32MapPanel.Height));
                this.s32MapPanel.AutoScrollPosition = new Point(currentX, newY);
            }

            // 更新小地圖
            UpdateMiniMap();

            // 阻止事件繼續傳遞
            ((HandledMouseEventArgs)e).Handled = true;
        }

        // 執行實際的縮放操作
        private void ApplyS32Zoom(double targetZoomLevel)
        {
            try
            {
                if (this.s32PictureBox.Image == null)
                    return;

                if (originalS32Image == null)
                {
                    originalS32Image = (Image)this.s32PictureBox.Image.Clone();
                }

                // 計算新的圖片大小
                int newWidth = (int)(originalS32Image.Width * targetZoomLevel);
                int newHeight = (int)(originalS32Image.Height * targetZoomLevel);

                // 創建縮放後的圖片
                Bitmap scaledImage = new Bitmap(newWidth, newHeight);
                using (Graphics g = Graphics.FromImage(scaledImage))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(originalS32Image, 0, 0, newWidth, newHeight);
                }

                // 更新 PictureBox
                if (this.s32PictureBox.Image != null && this.s32PictureBox.Image != originalS32Image)
                    this.s32PictureBox.Image.Dispose();

                this.s32PictureBox.Image = scaledImage;
                this.s32PictureBox.Size = new Size(newWidth, newHeight);
                s32ZoomLevel = targetZoomLevel;

                // 更新狀態欄顯示縮放級別
                this.lblS32Info.Text = $"縮放: {s32ZoomLevel:P0}";
            }
            catch (Exception ex)
            {
                this.lblS32Info.Text = $"縮放失敗: {ex.Message}";
            }
        }

        // 顯示單點座標
        private void ShowSinglePoint(int x, int y)
        {
            string coords = string.Format("{0},{1}", x, y);
            this.toolStripStatusLabel2.Text = coords;

            try
            {
                Clipboard.SetText(coords);
                this.toolStripStatusLabel1.Text = "已複製: " + coords;
            }
            catch
            {
                this.toolStripStatusLabel1.Text = coords;
            }
        }

        // 小地圖滑鼠按下 - 開始拖拽或點擊跳轉
        private void miniMapPictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isMiniMapDragging = true;
                MoveMainMapFromMiniMap(e.X, e.Y, true);
            }
        }

        // 小地圖滑鼠移動 - 拖拽時只更新小地圖紅框
        private void miniMapPictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMiniMapDragging && e.Button == MouseButtons.Left)
            {
                MoveMainMapFromMiniMap(e.X, e.Y, false);  // 拖拽中不重繪主地圖
            }
        }

        // 小地圖滑鼠放開 - 結束拖拽，更新主地圖
        private void miniMapPictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (isMiniMapDragging)
            {
                isMiniMapDragging = false;
                // 拖拽結束時更新小地圖（主地圖已經捲動到正確位置）
                UpdateMiniMap();
            }
        }

        // 根據小地圖座標移動主地圖（點擊位置為紅框中心）
        // updateMiniMapFlag: true=更新小地圖, false=只更新紅框
        private void MoveMainMapFromMiniMap(int mouseX, int mouseY, bool updateMiniMapFlag)
        {
            try
            {
                if (this.s32PictureBox.Image == null)
                    return;

                int miniWidth = 260;
                int miniHeight = 260;

                // 使用 s32PictureBox 的實際控件大小
                int pictureWidth = this.s32PictureBox.Width;
                int pictureHeight = this.s32PictureBox.Height;

                // 計算小地圖中圖片的縮放和偏移（與 UpdateMiniMap 一致）
                float scaleX = (float)miniWidth / pictureWidth;
                float scaleY = (float)miniHeight / pictureHeight;
                float scale = Math.Min(scaleX, scaleY);

                int scaledWidth = (int)(pictureWidth * scale);
                int scaledHeight = (int)(pictureHeight * scale);
                int offsetX = (miniWidth - scaledWidth) / 2;
                int offsetY = (miniHeight - scaledHeight) / 2;

                // 計算點擊在縮放圖片中的相對位置
                int clickX = mouseX - offsetX;
                int clickY = mouseY - offsetY;

                // 限制在有效範圍內
                clickX = Math.Max(0, Math.Min(clickX, scaledWidth));
                clickY = Math.Max(0, Math.Min(clickY, scaledHeight));

                // 計算點擊位置對應的主地圖座標
                int mapPosX = (int)((float)clickX / scaledWidth * pictureWidth);
                int mapPosY = (int)((float)clickY / scaledHeight * pictureHeight);

                // 計算捲動位置，讓點擊位置成為視窗中央
                int newScrollX = mapPosX - this.s32MapPanel.Width / 2;
                int newScrollY = mapPosY - this.s32MapPanel.Height / 2;

                // 限制在有效範圍內
                int maxScrollX = Math.Max(0, pictureWidth - this.s32MapPanel.Width);
                int maxScrollY = Math.Max(0, pictureHeight - this.s32MapPanel.Height);
                newScrollX = Math.Max(0, Math.Min(newScrollX, maxScrollX));
                newScrollY = Math.Max(0, Math.Min(newScrollY, maxScrollY));

                // 設定 AutoScrollPosition
                this.s32MapPanel.AutoScrollPosition = new Point(newScrollX, newScrollY);

                // 根據參數決定是否更新小地圖
                if (updateMiniMapFlag)
                {
                    UpdateMiniMap();
                }
                else
                {
                    // 拖拽時只更新小地圖紅框位置（快速繪製）
                    UpdateMiniMapRedBox(newScrollX, newScrollY);
                }
            }
            catch
            {
                // 忽略錯誤
            }
        }

        // 快速更新小地圖紅框位置（不重繪整張小地圖）
        private Bitmap miniMapBaseImage = null;
        private void UpdateMiniMapRedBox(int scrollX, int scrollY)
        {
            try
            {
                if (this.s32PictureBox.Image == null)
                    return;

                // 如果沒有基底圖，先建立一張
                if (miniMapBaseImage == null)
                {
                    UpdateMiniMap();
                    return;
                }

                int miniWidth = 260;
                int miniHeight = 260;
                int pictureWidth = this.s32PictureBox.Width;
                int pictureHeight = this.s32PictureBox.Height;

                float scaleX = (float)miniWidth / pictureWidth;
                float scaleY = (float)miniHeight / pictureHeight;
                float scale = Math.Min(scaleX, scaleY);

                int scaledWidth = (int)(pictureWidth * scale);
                int scaledHeight = (int)(pictureHeight * scale);
                int offsetX = (miniWidth - scaledWidth) / 2;
                int offsetY = (miniHeight - scaledHeight) / 2;

                // 複製基底圖
                Bitmap miniMap = (Bitmap)miniMapBaseImage.Clone();
                using (Graphics g = Graphics.FromImage(miniMap))
                {
                    float viewPortScaleX = (float)scaledWidth / pictureWidth;
                    float viewPortScaleY = (float)scaledHeight / pictureHeight;

                    int viewX = (int)(scrollX * viewPortScaleX) + offsetX;
                    int viewY = (int)(scrollY * viewPortScaleY) + offsetY;
                    int viewWidth = (int)(this.s32MapPanel.Width * viewPortScaleX);
                    int viewHeight = (int)(this.s32MapPanel.Height * viewPortScaleY);

                    using (Pen viewPortPen = new Pen(Color.Red, 2))
                    {
                        g.DrawRectangle(viewPortPen, viewX, viewY, viewWidth, viewHeight);
                    }
                }

                if (this.miniMapPictureBox.Image != null && this.miniMapPictureBox.Image != miniMapBaseImage)
                    this.miniMapPictureBox.Image.Dispose();
                this.miniMapPictureBox.Image = miniMap;
            }
            catch { }
        }

        // 小地圖點擊跳轉（保留給滑鼠右鍵查詢 S32 檔案用）
        private void miniMapPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            // 右鍵點擊顯示 S32 檔案資訊
            if (e.Button == MouseButtons.Right)
            {
                try
                {
                    if (this.s32PictureBox.Image == null)
                        return;

                    int miniWidth = 260;
                    int miniHeight = 260;

                    float scaleX = (float)miniWidth / this.s32PictureBox.Image.Width;
                    float scaleY = (float)miniHeight / this.s32PictureBox.Image.Height;
                    float scale = Math.Min(scaleX, scaleY);

                    int scaledWidth = (int)(this.s32PictureBox.Image.Width * scale);
                    int scaledHeight = (int)(this.s32PictureBox.Image.Height * scale);
                    int offsetX = (miniWidth - scaledWidth) / 2;
                    int offsetY = (miniHeight - scaledHeight) / 2;

                    int clickX = e.X - offsetX;
                    int clickY = e.Y - offsetY;

                    if (clickX < 0 || clickY < 0 || clickX > scaledWidth || clickY > scaledHeight)
                        return;

                    float clickRatioX = (float)clickX / scaledWidth;
                    float clickRatioY = (float)clickY / scaledHeight;

                    int mapX = (int)(clickRatioX * this.s32PictureBox.Image.Width);
                    int mapY = (int)(clickRatioY * this.s32PictureBox.Image.Height);

                    var linLoc = L1MapHelper.GetLinLocation(mapX, mapY);
                    if (linLoc != null)
                    {
                        int blockX = ((linLoc.x - 0x7FFF) / 64) + 0x7FFF;
                        int blockY = ((linLoc.y - 0x7FFF) / 64) + 0x7FFF;
                        string targetFileName = $"{blockX:X4}{blockY:X4}.s32";
                        this.toolStripStatusLabel1.Text = $"S32 檔案: {targetFileName} (座標: {linLoc.x},{linLoc.y})";
                    }
                }
                catch { }
            }
        }

        // ===== S32 編輯器功能 =====

        // S32 檔案資訊類別
        private class S32FileItem
        {
            public string FilePath { get; set; }
            public string DisplayName { get; set; }
            public Struct.L1MapSeg SegInfo { get; set; }
            public bool IsChecked { get; set; } = true;  // 預設勾選

            public override string ToString() => DisplayName;
        }

        // S32 資料結構
        private class S32Data
        {
            // 第一層（地板）- 64x128
            public TileCell[,] Layer1 { get; set; } = new TileCell[64, 128];

            // 第二層資料
            public List<Layer2Item> Layer2 { get; set; } = new List<Layer2Item>();

            // 第三層（地圖屬性）- 64x64
            public MapAttribute[,] Layer3 { get; set; } = new MapAttribute[64, 64];

            // 第四層（物件）
            public List<ObjectTile> Layer4 { get; set; } = new List<ObjectTile>();

            // 使用的所有 tile（不重複）
            public Dictionary<int, TileInfo> UsedTiles { get; set; } = new Dictionary<int, TileInfo>();

            // 保存原始文件內容，用於安全保存
            public byte[] OriginalFileData { get; set; }

            // 記錄各層在文件中的位置
            public int Layer1Offset { get; set; }
            public int Layer2Offset { get; set; }
            public int Layer3Offset { get; set; }
            public int Layer4Offset { get; set; }
            public int Layer4EndOffset { get; set; }  // 第四層結束位置

            // 第5-8層的原始資料（未解析）
            public byte[] Layer5to8Data { get; set; }

            // 第5層 - 可透明化的圖塊 (X, Y, R, G, B)
            public List<Layer5Item> Layer5 { get; set; } = new List<Layer5Item>();

            // 第6層 - 使用的 til
            public List<int> Layer6 { get; set; } = new List<int>();

            // 第7層 - 傳送點、入口點
            public List<Layer7Item> Layer7 { get; set; } = new List<Layer7Item>();

            // 第8層 - 特效、裝飾品
            public List<Layer8Item> Layer8 { get; set; } = new List<Layer8Item>();

            // 檔案路徑和 SegInfo
            public string FilePath { get; set; }
            public Struct.L1MapSeg SegInfo { get; set; }

            // 是否已修改
            public bool IsModified { get; set; }
        }

        // 第二層項目
        private class Layer2Item
        {
            public byte Value1 { get; set; }
            public byte Value2 { get; set; }
            public int Value3 { get; set; }
        }

        // 地圖屬性（第三層）
        private class MapAttribute
        {
            public short Attribute1 { get; set; }
            public short Attribute2 { get; set; }
        }

        // 物件 Tile（第四層）
        private class ObjectTile
        {
            public int GroupId { get; set; }      // 物件組 ID
            public int X { get; set; }            // X 座標
            public int Y { get; set; }            // Y 座標
            public int Layer { get; set; }        // 層次（用於排序）
            public int IndexId { get; set; }      // 索引 ID
            public int TileId { get; set; }       // Tile ID
        }

        // 第五層項目 - 可透明化的圖塊
        private class Layer5Item
        {
            public byte X { get; set; }
            public byte Y { get; set; }
            public byte R { get; set; }
            public byte G { get; set; }
            public byte B { get; set; }
        }

        // 第七層項目 - 傳送點、入口點
        private class Layer7Item
        {
            public string Name { get; set; }      // 傳送點、入口點名稱
            public byte X { get; set; }           // X軸
            public byte Y { get; set; }           // Y軸
            public ushort TargetMapId { get; set; }  // 進入傳送點、入口點要傳送的地圖編號
            public int PortalId { get; set; }     // 傳送點、入口點的編號
        }

        // 第八層項目 - 特效、裝飾品
        private class Layer8Item
        {
            public ushort SprId { get; set; }     // 特效編號
            public ushort X { get; set; }         // X軸
            public ushort Y { get; set; }         // Y軸
            public int Unknown { get; set; }      // 未知
        }

        // 格子資料
        private class TileCell
        {
            public int X { get; set; }          // 格子座標 X (0-127)
            public int Y { get; set; }          // 格子座標 Y (0-63)
            public int TileId { get; set; }     // til ID
            public int IndexId { get; set; }    // id (索引)
            public bool IsModified { get; set; } // 是否已修改
        }

        // Tile 資訊
        private class TileInfo
        {
            public int TileId { get; set; }
            public int IndexId { get; set; }
            public Bitmap Thumbnail { get; set; }  // 縮圖
            public int UsageCount { get; set; }    // 使用次數
        }

        // 所有載入的 s32 資料（key: 檔案路徑）
        private Dictionary<string, S32Data> allS32DataDict = new Dictionary<string, S32Data>();

        // 當前選擇的地圖 ID
        private string currentMapId;

        // 當前選擇的 S32 檔案資訊（用於顯示）
        private S32FileItem currentS32FileItem;

        // 便捷屬性：從字典中獲取當前選中的 S32 資料
        private S32Data currentS32Data
        {
            get
            {
                if (currentS32FileItem != null && allS32DataDict.ContainsKey(currentS32FileItem.FilePath))
                {
                    return allS32DataDict[currentS32FileItem.FilePath];
                }
                return null;
            }
        }

        // 便捷屬性：檢查當前選中的 S32 是否已修改
        private bool isS32Modified
        {
            get
            {
                return currentS32Data != null && currentS32Data.IsModified;
            }
            set
            {
                if (currentS32Data != null)
                {
                    currentS32Data.IsModified = value;
                }
            }
        }

        // 區域選擇相關變量
        private bool isSelectingRegion = false;
        private Point regionStartPoint;
        private Point regionEndPoint;
        private Rectangle selectedRegion;

        // Layer4 複製貼上相關變數
        private bool isLayer4CopyMode = false;           // 是否在複製選取模式
        private bool hasLayer4Clipboard = false;         // 剪貼簿是否有資料
        private List<CopiedCellData> cellClipboard = new List<CopiedCellData>();  // 複製的格子資料（多層）
        private List<Layer2Item> layer2Clipboard = new List<Layer2Item>();  // 複製的 Layer2 資料
        private List<Layer5Item> layer5Clipboard = new List<Layer5Item>();  // 複製的 Layer5 資料
        private List<int> layer6Clipboard = new List<int>();  // 複製的 Layer6 資料 (使用的 TilId)
        private List<Layer7Item> layer7Clipboard = new List<Layer7Item>();  // 複製的 Layer7 資料
        private List<Layer8Item> layer8Clipboard = new List<Layer8Item>();  // 複製的 Layer8 資料
        private Point copyRegionOrigin;                   // 複製區域的原點（全域 Layer1 座標）
        private Rectangle copyRegionBounds;               // 複製區域的範圍（螢幕座標）
        private Point pastePreviewLocation;               // 貼上預覽位置
        private bool isPastePreviewMode = false;          // 是否在貼上預覽模式
        private List<SelectedCell> currentSelectedCells = new List<SelectedCell>();  // 目前選中的格子（用於繪製）
        private string clipboardSourceMapId;              // 剪貼簿來源地圖 ID

        // 複製/刪除設定（由 Dialog 設定）
        private bool copySettingLayer1 = true;
        private bool copySettingLayer2 = true;
        private bool copySettingLayer3 = true;
        private bool copySettingLayer4 = true;
        private bool copySettingLayer5to8 = true;

        // 複製的格子資料（支援多層）
        private class CopiedCellData
        {
            public int RelativeX { get; set; }    // 相對於原點的 X 偏移（Layer1 座標）
            public int RelativeY { get; set; }    // 相對於原點的 Y 偏移

            // Layer1 資料（地板）- 兩個 Layer1 格子對應一個 Layer3 格子
            public TileCell Layer1Cell1 { get; set; }  // X 為偶數的格子
            public TileCell Layer1Cell2 { get; set; }  // X 為奇數的格子

            // Layer3 資料（屬性）
            public MapAttribute Layer3Attr { get; set; }

            // Layer4 資料（物件）- 一個格子可能有多個物件
            public List<CopiedObjectTile> Layer4Objects { get; set; } = new List<CopiedObjectTile>();
        }

        // 複製的物件資料（含相對位置）
        private class CopiedObjectTile
        {
            public int RelativeX { get; set; }    // 相對於原點的 X 偏移（Layer1 座標）
            public int RelativeY { get; set; }    // 相對於原點的 Y 偏移
            public int GroupId { get; set; }
            public int Layer { get; set; }
            public int IndexId { get; set; }
            public int TileId { get; set; }
            public int OriginalIndex { get; set; } // 原始 Layer4 列表中的索引（用於保持順序）
        }

        // Undo 相關變數
        private const int MAX_UNDO_HISTORY = 5;
        private Stack<UndoAction> undoHistory = new Stack<UndoAction>();
        private Stack<UndoAction> redoHistory = new Stack<UndoAction>();

        // Undo 動作類別
        private class UndoAction
        {
            public string Description { get; set; }
            public List<UndoObjectInfo> AddedObjects { get; set; } = new List<UndoObjectInfo>();    // 新增的物件（還原時要刪除）
            public List<UndoObjectInfo> RemovedObjects { get; set; } = new List<UndoObjectInfo>();  // 刪除的物件（還原時要新增回去）
            public List<UndoLayer7Info> RemovedLayer7Items { get; set; } = new List<UndoLayer7Info>();  // 刪除的第七層資料
        }

        // 記錄物件資訊用於 Undo
        private class UndoObjectInfo
        {
            public string S32FilePath { get; set; }  // S32 檔案路徑
            public int GameX { get; set; }           // 遊戲座標 X
            public int GameY { get; set; }           // 遊戲座標 Y
            public int LocalX { get; set; }          // S32 內局部座標 X
            public int LocalY { get; set; }          // S32 內局部座標 Y
            public int GroupId { get; set; }
            public int Layer { get; set; }
            public int IndexId { get; set; }
            public int TileId { get; set; }
        }

        // 記錄第七層資訊用於 Undo
        private class UndoLayer7Info
        {
            public string S32FilePath { get; set; }
            public string Name { get; set; }
            public byte X { get; set; }
            public byte Y { get; set; }
            public ushort TargetMapId { get; set; }
            public int PortalId { get; set; }
        }

        // 根據遊戲座標找到對應的 S32Data
        private S32Data GetS32DataByGameCoords(int gameX, int gameY)
        {
            foreach (var s32Data in allS32DataDict.Values)
            {
                if (gameX >= s32Data.SegInfo.nLinBeginX && gameX <= s32Data.SegInfo.nLinEndX &&
                    gameY >= s32Data.SegInfo.nLinBeginY && gameY <= s32Data.SegInfo.nLinEndY)
                {
                    return s32Data;
                }
            }
            return null;
        }

        // 螢幕座標轉換為遊戲座標（使用 Layer3 格子，與格線一致）
        private (int gameX, int gameY, S32Data s32Data, int localX, int localY) ScreenToGameCoords(int screenX, int screenY)
        {
            if (string.IsNullOrEmpty(currentMapId) || !Share.MapDataList.ContainsKey(currentMapId))
                return (-1, -1, null, -1, -1);

            // 將螢幕座標轉換為原始座標（考慮縮放）
            int origX = (int)(screenX / s32ZoomLevel);
            int origY = (int)(screenY / s32ZoomLevel);

            Struct.L1Map currentMap = Share.MapDataList[currentMapId];

            foreach (var s32Data in allS32DataDict.Values)
            {
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 使用 Layer3 格子（與 DrawS32Grid 一致）
                for (int y = 0; y < 64; y++)
                {
                    for (int x3 = 0; x3 < 64; x3++)
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

                        if (IsPointInDiamond(new Point(origX, origY), p1, p2, p3, p4))
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

        // 遊戲座標轉換為螢幕座標中心點
        private (int screenX, int screenY) GameToScreenCoords(int gameX, int gameY)
        {
            if (string.IsNullOrEmpty(currentMapId) || !Share.MapDataList.ContainsKey(currentMapId))
                return (-1, -1);

            Struct.L1Map currentMap = Share.MapDataList[currentMapId];

            // 找到包含這個遊戲座標的 S32
            foreach (var s32Data in allS32DataDict.Values)
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

                    // 返回菱形中心點
                    return (X + 12, Y + 12);
                }
            }

            return (-1, -1);
        }

        // 載入當前地圖的 s32 檔案清單並載入所有 S32 資料
        private void LoadS32FileList(string mapId)
        {
            lstS32Files.Items.Clear();
            allS32DataDict.Clear();
            currentMapId = mapId;

            // 從 Share.MapDataList 取得地圖資料
            if (!Share.MapDataList.ContainsKey(mapId))
                return;

            Struct.L1Map currentMap = Share.MapDataList[mapId];

            // 先快速添加清單項目（不載入資料）
            List<S32FileItem> s32FileItems = new List<S32FileItem>();
            foreach (var kvp in currentMap.FullFileNameList)
            {
                string filePath = kvp.Key;
                Struct.L1MapSeg segInfo = kvp.Value;

                // 只處理 s32 檔案
                if (segInfo.isS32)
                {
                    string fileName = Path.GetFileName(filePath);
                    string displayName = $"{fileName} ({segInfo.nBlockX:X4},{segInfo.nBlockY:X4}) [{segInfo.nLinBeginX},{segInfo.nLinBeginY}~{segInfo.nLinEndX},{segInfo.nLinEndY}]";

                    S32FileItem item = new S32FileItem
                    {
                        FilePath = filePath,
                        DisplayName = displayName,
                        SegInfo = segInfo,
                        IsChecked = true
                    };

                    int index = lstS32Files.Items.Add(item);
                    lstS32Files.SetItemChecked(index, true);  // 預設勾選
                    s32FileItems.Add(item);
                }
            }

            // 自動選擇第一個S32檔案
            if (lstS32Files.Items.Count > 0)
            {
                lstS32Files.SelectedIndex = 0;
            }

            this.toolStripStatusLabel1.Text = $"找到 {s32FileItems.Count} 個 S32 檔案，正在載入...";

            // 使用背景執行緒順序載入（避免並行造成磁碟競爭）
            Task.Run(() =>
            {
                int loadedCount = 0;

                foreach (var item in s32FileItems)
                {
                    try
                    {
                        byte[] data = File.ReadAllBytes(item.FilePath);
                        S32Data s32Data = ParseS32File(data);
                        s32Data.FilePath = item.FilePath;
                        s32Data.SegInfo = item.SegInfo;
                        s32Data.IsModified = false;
                        allS32DataDict[item.FilePath] = s32Data;

                        loadedCount++;

                        // 更新進度（每5個更新一次UI）
                        if (loadedCount % 5 == 0 || loadedCount == s32FileItems.Count)
                        {
                            int count = loadedCount;
                            this.Invoke((MethodInvoker)delegate
                            {
                                this.toolStripStatusLabel1.Text = $"已載入 {count}/{s32FileItems.Count} 個 S32 檔案...";
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"載入 S32 檔案失敗: {item.FilePath}, 錯誤: {ex.Message}");
                    }
                }

                // 載入完成後更新 UI
                this.Invoke((MethodInvoker)delegate
                {
                    this.toolStripStatusLabel1.Text = $"已載入 {allS32DataDict.Count} 個 S32 檔案";

                    // 自動渲染整張地圖和更新 Tile 清單
                    if (allS32DataDict.Count > 0)
                    {
                        RenderS32Map();
                        UpdateTileList();

                        // 更新群組縮圖列表（載入時執行一次）
                        UpdateGroupThumbnailsList();

                        // 捲動到地圖中間
                        ScrollToMapCenter();
                    }
                });
            });
        }

        // 分析第三層屬性類型
        private void AnalyzeLayer3Attributes()
        {
            // 統計 Attribute1 和 Attribute2 的所有不同值
            Dictionary<short, int> attr1Values = new Dictionary<short, int>();
            Dictionary<short, int> attr2Values = new Dictionary<short, int>();

            foreach (var s32Data in allS32DataDict.Values)
            {
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        var attr = s32Data.Layer3[y, x];
                        if (attr == null) continue;

                        // 統計 Attribute1
                        if (!attr1Values.ContainsKey(attr.Attribute1))
                            attr1Values[attr.Attribute1] = 0;
                        attr1Values[attr.Attribute1]++;

                        // 統計 Attribute2
                        if (!attr2Values.ContainsKey(attr.Attribute2))
                            attr2Values[attr.Attribute2] = 0;
                        attr2Values[attr.Attribute2]++;
                    }
                }
            }

            // 建立分析結果
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"地圖: {currentMapId}");
            sb.AppendLine();

            sb.AppendLine("【左上邊 (Attribute1) 統計】");
            foreach (var kvp in attr1Values.OrderByDescending(x => x.Value).Take(15))
            {
                string flags = GetAttributeFlags(kvp.Key);
                sb.AppendLine($"  0x{kvp.Key:X4}: {kvp.Value} 個 | {flags}");
            }
            if (attr1Values.Count > 15)
                sb.AppendLine($"  ... 還有 {attr1Values.Count - 15} 種其他值");

            sb.AppendLine();
            sb.AppendLine("【右上邊 (Attribute2) 統計】");
            foreach (var kvp in attr2Values.OrderByDescending(x => x.Value).Take(15))
            {
                string flags = GetAttributeFlags(kvp.Key);
                sb.AppendLine($"  0x{kvp.Key:X4}: {kvp.Value} 個 | {flags}");
            }
            if (attr2Values.Count > 15)
                sb.AppendLine($"  ... 還有 {attr2Values.Count - 15} 種其他值");

            MessageBox.Show(sb.ToString(), "第三層屬性分析", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // 取得屬性標記說明
        private string GetAttributeFlags(short value)
        {
            List<string> flags = new List<string>();

            if ((value & 0x0001) != 0) flags.Add("不可通行");
            if ((value & 0x0002) != 0) flags.Add("安全區");
            if ((value & 0x0004) != 0) flags.Add("bit2");
            if ((value & 0x0008) != 0) flags.Add("bit3");
            if ((value & 0x0010) != 0) flags.Add("bit4");
            if ((value & 0x0020) != 0) flags.Add("bit5");
            if ((value & 0x0040) != 0) flags.Add("bit6");
            if ((value & 0x0080) != 0) flags.Add("bit7");
            if ((value & 0x0100) != 0) flags.Add("bit8");
            if ((value & 0x0200) != 0) flags.Add("bit9");
            if ((value & 0x0400) != 0) flags.Add("bit10");
            if ((value & 0x0800) != 0) flags.Add("bit11");
            if ((value & 0x1000) != 0) flags.Add("bit12");
            if ((value & 0x2000) != 0) flags.Add("bit13");
            if ((value & 0x4000) != 0) flags.Add("bit14");
            if ((value & 0x8000) != 0) flags.Add("bit15");

            if (flags.Count == 0) flags.Add("無標記(可通行)");

            return string.Join(", ", flags);
        }

        // s32 檔案選擇變更事件
        private void lstS32Files_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstS32Files.SelectedItem == null)
                return;

            var item = (S32FileItem)lstS32Files.SelectedItem;

            // 保存當前選擇的檔案資訊
            currentS32FileItem = item;

            // 載入並解析 s32 檔案
            LoadAndParseS32File(item.FilePath);
        }

        // S32 清單勾選狀態變更事件
        private void lstS32Files_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // 更新項目的勾選狀態
            if (lstS32Files.Items[e.Index] is S32FileItem item)
            {
                item.IsChecked = (e.NewValue == CheckState.Checked);

                // 延遲觸發重新渲染（因為 ItemCheck 在狀態變更前觸發）
                this.BeginInvoke((MethodInvoker)delegate
                {
                    RenderS32Map();
                });
            }
        }

        // 純粹的 S32 檔案解析方法（不涉及 UI）
        private S32Data ParseS32File(byte[] data)
        {
            S32Data s32Data = new S32Data();

            // 保存原始文件數據
            s32Data.OriginalFileData = new byte[data.Length];
            Array.Copy(data, s32Data.OriginalFileData, data.Length);

            using (BinaryReader br = new BinaryReader(new MemoryStream(data)))
            {
                // 記錄第一層偏移
                s32Data.Layer1Offset = (int)br.BaseStream.Position;

                // 第一層（地板）- 64x128
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        int id = br.ReadByte();
                        int til = br.ReadUInt16();
                        int nk = br.ReadByte();

                        s32Data.Layer1[y, x] = new TileCell
                        {
                            X = x,
                            Y = y,
                            TileId = til,
                            IndexId = id
                        };

                        // 收集使用的 tile（第一層）
                        if (!s32Data.UsedTiles.ContainsKey(til))
                        {
                            s32Data.UsedTiles[til] = new TileInfo
                            {
                                TileId = til,
                                IndexId = id,
                                UsageCount = 1,
                                Thumbnail = null
                            };
                        }
                        else
                        {
                            s32Data.UsedTiles[til].UsageCount++;
                        }
                    }
                }

                // 記錄第二層偏移
                s32Data.Layer2Offset = (int)br.BaseStream.Position;

                // 第二層
                int layer2Count = br.ReadUInt16();
                for (int i = 0; i < layer2Count; i++)
                {
                    s32Data.Layer2.Add(new Layer2Item
                    {
                        Value1 = br.ReadByte(),
                        Value2 = br.ReadByte(),
                        Value3 = br.ReadInt32()
                    });
                }

                // 記錄第三層偏移
                s32Data.Layer3Offset = (int)br.BaseStream.Position;

                // 第三層（地圖屬性）- 64x64
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

                // 記錄第四層偏移
                s32Data.Layer4Offset = (int)br.BaseStream.Position;

                // 第四層（物件）
                int layer4GroupCount = br.ReadInt32();
                for (int i = 0; i < layer4GroupCount; i++)
                {
                    int groupId = br.ReadInt16();
                    int blockCount = br.ReadUInt16();

                    for (int j = 0; j < blockCount; j++)
                    {
                        int x = br.ReadByte();
                        int y = br.ReadByte();
                        int layer = br.ReadByte();
                        int indexId = br.ReadByte();
                        int tileId = br.ReadInt16();
                        int uk = br.ReadByte();

                        var objTile = new ObjectTile
                        {
                            GroupId = groupId,
                            X = x,
                            Y = y,
                            Layer = layer,
                            IndexId = indexId,
                            TileId = tileId
                        };

                        s32Data.Layer4.Add(objTile);

                        // 收集使用的 tile（第四層）
                        if (!s32Data.UsedTiles.ContainsKey(tileId))
                        {
                            s32Data.UsedTiles[tileId] = new TileInfo
                            {
                                TileId = tileId,
                                IndexId = indexId,
                                UsageCount = 1,
                                Thumbnail = null
                            };
                        }
                        else
                        {
                            s32Data.UsedTiles[tileId].UsageCount++;
                        }
                    }
                }

                // 記錄第四層結束位置（後面可能還有第5-8層等未知數據）
                s32Data.Layer4EndOffset = (int)br.BaseStream.Position;

                // 讀取第5-8層的原始資料
                int remainingLength = (int)(br.BaseStream.Length - br.BaseStream.Position);
                if (remainingLength > 0)
                {
                    s32Data.Layer5to8Data = br.ReadBytes(remainingLength);

                    // 解析第5-8層
                    using (var layerStream = new MemoryStream(s32Data.Layer5to8Data))
                    using (var layerReader = new BinaryReader(layerStream))
                    {
                        try
                        {
                            // 第五層 - 可透明化的圖塊
                            if (layerStream.Position + 4 <= layerStream.Length)
                            {
                                int lv5Count = layerReader.ReadInt32();
                                for (int i = 0; i < lv5Count && layerStream.Position + 5 <= layerStream.Length; i++)
                                {
                                    s32Data.Layer5.Add(new Layer5Item
                                    {
                                        X = layerReader.ReadByte(),
                                        Y = layerReader.ReadByte(),
                                        R = layerReader.ReadByte(),
                                        G = layerReader.ReadByte(),
                                        B = layerReader.ReadByte()
                                    });
                                }
                            }

                            // 第六層 - 使用的 til
                            if (layerStream.Position + 4 <= layerStream.Length)
                            {
                                int lv6Count = layerReader.ReadInt32();
                                for (int i = 0; i < lv6Count && layerStream.Position + 4 <= layerStream.Length; i++)
                                {
                                    int til = layerReader.ReadInt32();
                                    s32Data.Layer6.Add(til);
                                }
                            }

                            // 第七層 - 傳送點、入口點
                            if (layerStream.Position + 2 <= layerStream.Length)
                            {
                                int lv7Count = layerReader.ReadUInt16();
                                for (int i = 0; i < lv7Count && layerStream.Position + 1 <= layerStream.Length; i++)
                                {
                                    byte len = layerReader.ReadByte();
                                    if (layerStream.Position + len + 8 > layerStream.Length) break;

                                    string name = Encoding.Default.GetString(layerReader.ReadBytes(len));
                                    s32Data.Layer7.Add(new Layer7Item
                                    {
                                        Name = name,
                                        X = layerReader.ReadByte(),
                                        Y = layerReader.ReadByte(),
                                        TargetMapId = layerReader.ReadUInt16(),
                                        PortalId = layerReader.ReadInt32()
                                    });
                                }
                            }

                            // 第八層 - 特效、裝飾品
                            if (layerStream.Position + 2 <= layerStream.Length)
                            {
                                int lv8Count = layerReader.ReadByte();
                                layerReader.ReadByte(); // 跳過一個 byte
                                for (int i = 0; i < lv8Count && layerStream.Position + 10 <= layerStream.Length; i++)
                                {
                                    s32Data.Layer8.Add(new Layer8Item
                                    {
                                        SprId = layerReader.ReadUInt16(),
                                        X = layerReader.ReadUInt16(),
                                        Y = layerReader.ReadUInt16(),
                                        Unknown = layerReader.ReadInt32()
                                    });
                                }
                            }
                        }
                        catch (EndOfStreamException)
                        {
                            // 忽略讀取超出範圍的錯誤
                        }
                    }
                }
                else
                {
                    s32Data.Layer5to8Data = new byte[0];
                }
            }

            return s32Data;
        }

        // 載入並解析 s32 檔案（帶 UI 更新）
        private void LoadAndParseS32File(string filePath)
        {
            try
            {
                // S32 檔案已經在 LoadS32FileList() 中載入，這裡只更新UI顯示
                if (!allS32DataDict.ContainsKey(filePath))
                {
                    this.toolStripStatusLabel1.Text = "選中的 S32 檔案不在記憶體中";
                    return;
                }

                S32Data s32Data = allS32DataDict[filePath];

                this.toolStripStatusLabel1.Text = $"已選擇 {Path.GetFileName(filePath)} - 第1層:{64*128}格, 第2層:{s32Data.Layer2.Count}項, 第3層:{64*64}格, 第4層:{s32Data.Layer4.Count}物件, 此檔案使用{s32Data.UsedTiles.Count}種Tile";

                // 注意：不需要重新渲染地圖，因為整張地圖已經在顯示了
                // 如果需要高亮顯示當前選中的 S32 區域，可以在這裡添加
            }
            catch (Exception ex)
            {
                MessageBox.Show($"顯示 s32 檔案資訊失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 層選擇變更事件
        private void S32Layer_CheckedChanged(object sender, EventArgs e)
        {
            // 使用防抖Timer，避免快速切換時多次渲染
            renderDebounceTimer.Stop();
            renderDebounceTimer.Start();
        }

        // 複製設定按鈕點擊事件
        private void btnCopySettings_Click(object sender, EventArgs e)
        {
            using (var dialog = new CopySettingsDialog(copySettingLayer1, copySettingLayer2, copySettingLayer3, copySettingLayer4, copySettingLayer5to8))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    copySettingLayer1 = dialog.CopyLayer1;
                    copySettingLayer2 = dialog.CopyLayer2;
                    copySettingLayer3 = dialog.CopyLayer3;
                    copySettingLayer4 = dialog.CopyLayer4;
                    copySettingLayer5to8 = dialog.CopyLayer5to8;

                    // 更新按鈕文字顯示目前設定
                    var layers = new List<string>();
                    if (copySettingLayer1) layers.Add("L1");
                    if (copySettingLayer2) layers.Add("L2");
                    if (copySettingLayer3) layers.Add("L3");
                    if (copySettingLayer4) layers.Add("L4");
                    if (copySettingLayer5to8) layers.Add("L5-8");
                    string layerInfo = layers.Count > 0 ? string.Join(",", layers) : "無";

                    this.toolStripStatusLabel1.Text = $"複製/刪除設定已更新: {layerInfo}";
                }
            }
        }

        // 複製地圖座標按鈕點擊事件
        private void btnCopyMapCoords_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentMapId) || !Share.MapDataList.ContainsKey(currentMapId))
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Struct.L1Map currentMap = Share.MapDataList[currentMapId];

            // 取得地圖的遊戲座標範圍
            int startX = currentMap.nLinBeginX;
            int endX = currentMap.nLinEndX;
            int startY = currentMap.nLinBeginY;
            int endY = currentMap.nLinEndY;

            // 格式化為資料庫設定格式
            string coordText = $"startX={startX}, endX={endX}, startY={startY}, endY={endY}";

            // 複製到剪貼簿
            Clipboard.SetText(coordText);

            this.toolStripStatusLabel1.Text = $"已複製地圖座標: {coordText}";
        }

        // 允許通行按鈕點擊事件
        private void btnSetPassable_Click(object sender, EventArgs e)
        {
            if (currentPassableEditMode == PassableEditMode.SetPassable)
            {
                // 取消模式
                currentPassableEditMode = PassableEditMode.None;
                btnSetPassable.BackColor = SystemColors.Control;
                this.toolStripStatusLabel1.Text = "已取消允許通行模式";
            }
            else
            {
                // 啟用允許通行模式
                currentPassableEditMode = PassableEditMode.SetPassable;
                btnSetPassable.BackColor = Color.LightGreen;
                btnSetImpassable.BackColor = SystemColors.Control;
                this.toolStripStatusLabel1.Text = "允許通行模式：點擊格子設定為可通行 | Ctrl+拖拽批次設定";
            }
        }

        // 禁止通行按鈕點擊事件
        private void btnSetImpassable_Click(object sender, EventArgs e)
        {
            if (currentPassableEditMode == PassableEditMode.SetImpassable)
            {
                // 取消模式
                currentPassableEditMode = PassableEditMode.None;
                btnSetImpassable.BackColor = SystemColors.Control;
                this.toolStripStatusLabel1.Text = "已取消禁止通行模式";
            }
            else
            {
                // 啟用禁止通行模式
                currentPassableEditMode = PassableEditMode.SetImpassable;
                btnSetImpassable.BackColor = Color.LightCoral;
                btnSetPassable.BackColor = SystemColors.Control;
                this.toolStripStatusLabel1.Text = "禁止通行模式：點擊格子設定為不可通行 | Ctrl+拖拽批次設定";
            }
        }

        // 重新載入按鈕點擊事件
        private void btnReloadMap_Click(object sender, EventArgs e)
        {
            ReloadCurrentMap();
        }

        // 渲染整張 S32 地圖（所有 S32 檔案拼接）
        private void RenderS32Map()
        {
            try
            {
                if (allS32DataDict.Count == 0 || string.IsNullOrEmpty(currentMapId))
                {
                    lblS32Info.Text = "請選擇一個地圖";
                    return;
                }

                if (!Share.MapDataList.ContainsKey(currentMapId))
                {
                    lblS32Info.Text = "地圖資料不存在";
                    return;
                }

                Struct.L1Map currentMap = Share.MapDataList[currentMapId];

                // 計算整張地圖的大小（使用與 L1MapHelper 相同的公式）
                // 每個 block 的像素大小: BMP_W = 64 * 24 * 2 = 3072, BMP_H = 64 * 12 * 2 = 1536
                int blockWidth = 64 * 24 * 2;  // 3072
                int blockHeight = 64 * 12 * 2; // 1536

                // 地圖像素大小（與 L1MapHelper.LoadMap 相同）
                int mapWidth = currentMap.nBlockCountX * blockWidth;
                int mapHeight = currentMap.nBlockCountX * blockHeight / 2 + currentMap.nBlockCountY * blockHeight / 2;

                // 格子數量（Layer1 是 128 寬，64 高）
                int mapWidthInCells = currentMap.nBlockCountX * 128;
                int mapHeightInCells = currentMap.nBlockCountY * 64;

                Bitmap s32Bitmap = new Bitmap(mapWidth, mapHeight, PixelFormat.Format16bppRgb555);

                // 使用與原始 L1MapHelper.LoadMap 完全相同的方式：
                // 1. 為每個 S32 生成獨立的 bitmap
                // 2. 用 DrawImage 合併到大地圖
                ImageAttributes vAttr = new ImageAttributes();
                vAttr.SetColorKey(Color.FromArgb(0), Color.FromArgb(0)); // 透明色

                // 建立勾選的 S32 檔案清單
                HashSet<string> checkedFilePaths = new HashSet<string>();
                for (int i = 0; i < lstS32Files.Items.Count; i++)
                {
                    if (lstS32Files.GetItemChecked(i) && lstS32Files.Items[i] is S32FileItem item)
                    {
                        checkedFilePaths.Add(item.FilePath);
                    }
                }

                using (Graphics g = Graphics.FromImage(s32Bitmap))
                {
                    // 使用與原始 L1MapHelper.LoadMap 完全相同的排序方式（Utils.SortDesc）
                    var sortedFilePaths = Utils.SortDesc(allS32DataDict.Keys);

                    // 遍歷所有 S32 檔案
                    foreach (object filePathObj in sortedFilePaths)
                    {
                        string filePath = filePathObj as string;
                        if (filePath == null || !allS32DataDict.ContainsKey(filePath)) continue;

                        // 只渲染有勾選的 S32
                        if (!checkedFilePaths.Contains(filePath)) continue;

                        var s32Data = allS32DataDict[filePath];
                        // 使用 GetLoc 計算區塊位置
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int mx = loc[0];
                        int my = loc[1];

                        // 為這個 S32 生成獨立的 bitmap
                        Bitmap blockBmp = RenderS32Block(s32Data, chkLayer1.Checked, chkLayer4.Checked);

                        // 合併到大地圖（與原始方法一致）
                        g.DrawImage(blockBmp, new Rectangle(mx, my, blockBmp.Width, blockBmp.Height),
                            0, 0, blockBmp.Width, blockBmp.Height, GraphicsUnit.Pixel, vAttr);

                        blockBmp.Dispose();
                    }
                }

                // 第三層（地圖屬性）- 用半透明顏色疊加顯示
                if (chkLayer3.Checked)
                {
                    DrawLayer3Attributes(s32Bitmap, currentMap);
                }

                // 顯示通行性覆蓋層
                if (chkShowPassable.Checked)
                {
                    DrawPassableOverlay(s32Bitmap, currentMap);
                }

                // 顯示格線和邊界框
                if (chkShowGrid.Checked)
                {
                    // 繪製格子網格線
                    DrawS32Grid(s32Bitmap, currentMap);

                    // 繪製座標標籤（每 10 格顯示一次）
                    DrawCoordinateLabels(s32Bitmap, currentMap);
                }

                // 只顯示 S32 邊界框（用於除錯對齊）
                if (chkShowS32Boundary.Checked)
                {
                    DrawS32BoundaryOnly(s32Bitmap, currentMap);
                }

                // 繪製選中格子的高亮
                if (highlightedS32Data != null && highlightedCellX >= 0 && highlightedCellY >= 0)
                {
                    DrawHighlightedCell(s32Bitmap, currentMap);
                }

                // 顯示在 PictureBox
                if (s32PictureBox.Image != null)
                    s32PictureBox.Image.Dispose();
                s32PictureBox.Image = s32Bitmap;

                // 統計勾選的 S32 數量和物件數量
                int checkedCount = checkedFilePaths.Count;
                int totalObjects = allS32DataDict.Values
                    .Where(s => checkedFilePaths.Contains(s.FilePath))
                    .Sum(s => s.Layer4.Count);

                lblS32Info.Text = $"已渲染 {checkedCount}/{allS32DataDict.Count} 個S32檔案 | 大小: {mapWidth}x{mapHeight} | 第1層:{(chkLayer1.Checked ? "顯示" : "隱藏")} 第3層:{(chkLayer3.Checked ? "顯示" : "隱藏")} 第4層:{(chkLayer4.Checked ? "顯示" : "隱藏")} ({totalObjects}個物件)";
            }
            catch (Exception ex)
            {
                lblS32Info.Text = $"渲染失敗: {ex.Message}";
            }
        }

        // 渲染單個 S32 區塊為 bitmap（與 L1MapHelper.s32FileToBmp 相同的方式）
        private Bitmap RenderS32Block(S32Data s32Data, bool showLayer1, bool showLayer4)
        {
            int blockWidth = 64 * 24 * 2;  // 3072
            int blockHeight = 64 * 12 * 2; // 1536

            Bitmap result = new Bitmap(blockWidth, blockHeight, PixelFormat.Format16bppRgb555);

            Rectangle rect = new Rectangle(0, 0, result.Width, result.Height);
            BitmapData bmpData = result.LockBits(rect, ImageLockMode.ReadWrite, result.PixelFormat);
            int rowpix = bmpData.Stride;

            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;

                // 第一層（地板）- 與 drawTilBlock 完全相同的座標計算
                if (showLayer1)
                {
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 128; x++)
                        {
                            var cell = s32Data.Layer1[y, x];
                            if (cell != null && cell.TileId > 0)
                            {
                                // 與 L1MapHelper.drawTilBlock 完全相同的座標計算
                                int baseX = 0;
                                int baseY = 63 * 12;
                                baseX -= 24 * (x / 2);
                                baseY -= 12 * (x / 2);

                                int pixelX = baseX + x * 24 + y * 24;
                                int pixelY = baseY + y * 12;

                                DrawTilToBufferDirect(pixelX, pixelY, cell.TileId, cell.IndexId, rowpix, ptr, blockWidth, blockHeight);
                            }
                        }
                    }
                }

                // 第四層（物件）
                if (showLayer4)
                {
                    var sortedObjects = s32Data.Layer4.OrderBy(o => o.Layer).ToList();

                    // 如果有篩選條件，只渲染選中的群組
                    if (isFilteringLayer4Groups && selectedLayer4Groups.Count > 0)
                    {
                        sortedObjects = sortedObjects.Where(o => selectedLayer4Groups.Contains(o.GroupId)).ToList();
                    }

                    foreach (var obj in sortedObjects)
                    {
                        int baseX = 0;
                        int baseY = 63 * 12;
                        baseX -= 24 * (obj.X / 2);
                        baseY -= 12 * (obj.X / 2);

                        int pixelX = baseX + obj.X * 24 + obj.Y * 24;
                        int pixelY = baseY + obj.Y * 12;

                        DrawTilToBufferDirect(pixelX, pixelY, obj.TileId, obj.IndexId, rowpix, ptr, blockWidth, blockHeight);
                    }
                }
            }

            result.UnlockBits(bmpData);
            return result;
        }

        // 捲動到地圖中央
        private void ScrollToMapCenter()
        {
            if (this.s32PictureBox.Image == null)
                return;

            // 計算中央位置
            int centerX = this.s32PictureBox.Image.Width / 2 - this.s32MapPanel.Width / 2;
            int centerY = this.s32PictureBox.Image.Height / 2 - this.s32MapPanel.Height / 2;

            // 限制在有效範圍內
            int maxScrollX = Math.Max(0, this.s32PictureBox.Width - this.s32MapPanel.Width);
            int maxScrollY = Math.Max(0, this.s32PictureBox.Height - this.s32MapPanel.Height);
            centerX = Math.Max(0, Math.Min(centerX, maxScrollX));
            centerY = Math.Max(0, Math.Min(centerY, maxScrollY));

            // 設定捲動位置
            this.s32MapPanel.AutoScrollPosition = new Point(centerX, centerY);

            // 更新小地圖
            UpdateMiniMap();
        }

        // 繪製第三層（地圖屬性）- 用邊線顯示屬性
        // Attribute1 = 左上邊線, Attribute2 = 右上邊線（各自獨立的屬性值）
        private void DrawLayer3Attributes(Bitmap bitmap, Struct.L1Map currentMap)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // 遍歷所有 S32 檔案
                foreach (var s32Data in allS32DataDict.Values)
                {
                    // 使用與 RenderS32Map 相同的座標計算方式
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    int mx = loc[0];
                    int my = loc[1];

                    // Layer3 是 64x64，每個 Layer3 格子對應 Layer1 的 x*2 和 x*2+1
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 64; x++)  // Layer3 座標 (0-63)
                        {
                            var attr = s32Data.Layer3[y, x];
                            if (attr == null) continue;

                            // 只有當屬性非0時才繪製
                            if (attr.Attribute1 == 0 && attr.Attribute2 == 0) continue;

                            // 第3層的一個格子對應第1層的兩個格子（X方向）
                            int x1 = x * 2;

                            // 與 drawTilBlock 相同的像素計算
                            int localBaseX = 0;
                            int localBaseY = 63 * 12;
                            localBaseX -= 24 * (x1 / 2);
                            localBaseY -= 12 * (x1 / 2);

                            int X = mx + localBaseX + x1 * 24 + y * 24;
                            int Y = my + localBaseY + y * 12;

                            // 菱形的四個頂點（雙寬格子 48x24）
                            Point pLeft = new Point(X + 0, Y + 12);    // 左
                            Point pTop = new Point(X + 24, Y + 0);     // 上
                            Point pRight = new Point(X + 48, Y + 12);  // 右

                            // 繪製左上邊線 (Attribute1) - 根據該邊的屬性值決定顏色
                            if (attr.Attribute1 != 0)
                            {
                                Color color = GetAttributeColor(attr.Attribute1);
                                using (Pen pen = new Pen(color, 3))
                                {
                                    g.DrawLine(pen, pLeft, pTop);
                                }
                            }

                            // 繪製右上邊線 (Attribute2) - 根據該邊的屬性值決定顏色
                            if (attr.Attribute2 != 0)
                            {
                                Color color = GetAttributeColor(attr.Attribute2);
                                using (Pen pen = new Pen(color, 3))
                                {
                                    g.DrawLine(pen, pTop, pRight);
                                }
                            }
                        }
                    }
                }
            }
        }

        // 繪製通行性覆蓋層 - 用邊線顯示屬性
        // Attribute1 = 左上邊線, Attribute2 = 右上邊線
        // 顏色分類（考慮組合情況）：
        //   紅色 = 不可通行 (0x01)
        //   深紅 = 不可通行+安全區 (0x01+0x02)
        //   橘色 = 不可通行+戰鬥區 (0x01+0x04)
        //   藍色 = 安全區 (0x02)
        //   黃色 = 戰鬥區 (0x04)
        //   綠色 = 安全區+戰鬥區 (0x02+0x04)
        //   灰色 = 其他屬性
        // 繪製通行性覆蓋層 - 用邊線顯示可通行/不可通行
        // Attribute1 = 左上邊線, Attribute2 = 右上邊線
       // 繪製通行性覆蓋層 - 用邊線顯示可通行/不可通行
        // Attribute1 = 左上邊線, Attribute2 = 右上邊線
       // 繪製通行性覆蓋層 - 用邊線顯示可通行/不可通行
        // Attribute1 = 左上邊線, Attribute2 = 右上邊線
        // 繪製通行性覆蓋層 - 用邊線顯示可通行/不可通行
        // Attribute1 = 左上邊線, Attribute2 = 右上邊線
        private void DrawPassableOverlay(Bitmap bitmap, Struct.L1Map currentMap)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // 定義畫筆 - 使用明顯區分的顏色 (不透明避免重疊混色)
                using (Pen penImpassable = new Pen(Color.FromArgb(255, 128, 0, 128), 3))  // 不可通行 - 深紫色粗線
                using (Pen penPassable = new Pen(Color.FromArgb(255, 50, 200, 255), 2))   // 可通行 - 天藍色
                {
                    // 遍歷所有 S32 檔案
                    foreach (var s32Data in allS32DataDict.Values)
                    {
                        // 使用 GetLoc 計算區塊位置
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int mx = loc[0];
                        int my = loc[1];

                        // Layer3 是 64x64，每個 Layer3 格子對應 Layer1 的 x*2 和 x*2+1
                        for (int y = 0; y < 64; y++)
                        {
                            for (int x = 0; x < 64; x++)  // Layer3 座標 (0-63)
                            {
                                var attr = s32Data.Layer3[y, x];
                                if (attr == null) continue;

                                // 第3層的一個格子對應第1層的兩個格子（X方向）
                                int x1 = x * 2;

                                // 使用 GetLoc + drawTilBlock 公式計算像素位置
                                int localBaseX = 0;
                                int localBaseY = 63 * 12;
                                localBaseX -= 24 * (x1 / 2);
                                localBaseY -= 12 * (x1 / 2);

                                int X = mx + localBaseX + x1 * 24 + y * 24;
                                int Y = my + localBaseY + y * 12;

                                // 菱形的四個頂點（雙寬格子 48x24）
                                Point pLeft = new Point(X + 0, Y + 12);    // 左
                                Point pTop = new Point(X + 24, Y + 0);     // 上
                                Point pRight = new Point(X + 48, Y + 12);  // 右

                                // 繪製左上邊線 (Attribute1)
                                Pen penLeft = (attr.Attribute1 & 0x01) != 0 ? penImpassable : penPassable;
                                g.DrawLine(penLeft, pLeft, pTop);

                                // 繪製右上邊線 (Attribute2)
                                Pen penRight = (attr.Attribute2 & 0x01) != 0 ? penImpassable : penPassable;
                                g.DrawLine(penRight, pTop, pRight);
                            }
                        }
                    }
                }
            }
        }
        
        // 根據屬性值取得對應的顏色（不同值 = 不同顏色）
        private Color GetAttributeColor(short attrValue)
        {
            return Color.FromArgb(230, 200, 200, 200);
            // 根據不同的值返回不同的顏色
            switch (attrValue)
            {
                case 0x0001: return Color.FromArgb(230, 255, 0, 0);       // 紅色
                case 0x0002: return Color.FromArgb(230, 0, 100, 255);     // 藍色
                case 0x0003: return Color.FromArgb(230, 180, 0, 180);     // 紫色
                case 0x0004: return Color.FromArgb(230, 255, 200, 0);     // 黃色
                case 0x0005: return Color.FromArgb(230, 255, 100, 0);     // 橘色
                case 0x0006: return Color.FromArgb(230, 0, 200, 100);     // 綠色
                case 0x0007: return Color.FromArgb(230, 0, 200, 200);     // 青色
                case 0x0008: return Color.FromArgb(230, 200, 100, 50);    // 棕色
                case 0x0009: return Color.FromArgb(230, 255, 150, 150);   // 粉紅
                case 0x000A: return Color.FromArgb(230, 150, 255, 150);   // 淺綠
                case 0x000B: return Color.FromArgb(230, 150, 150, 255);   // 淺藍
                case 0x000C: return Color.FromArgb(230, 255, 255, 100);   // 淺黃
                case 0x000D: return Color.FromArgb(230, 255, 100, 255);   // 洋紅
                case 0x000E: return Color.FromArgb(230, 100, 255, 255);   // 淺青
                case 0x000F: return Color.FromArgb(230, 200, 200, 200);   // 淺灰
                default:
                    // 對於其他值，根據值生成顏色
                    int r = (attrValue * 37) % 200 + 55;
                    int g = (attrValue * 73) % 200 + 55;
                    int b = (attrValue * 113) % 200 + 55;
                    return Color.FromArgb(230, r, g, b);
            }
        }

        // 繪製選中格子的高亮
        private void DrawHighlightedCell(Bitmap bitmap, Struct.L1Map currentMap)
        {
            if (highlightedS32Data == null) return;

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // 使用 GetLoc 計算區塊位置
                int[] loc = highlightedS32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 使用 GetLoc + drawTilBlock 公式計算像素位置
                int localBaseX = 0;
                int localBaseY = 63 * 12;
                localBaseX -= 24 * (highlightedCellX / 2);
                localBaseY -= 12 * (highlightedCellX / 2);

                int X = mx + localBaseX + highlightedCellX * 24 + highlightedCellY * 24;
                int Y = my + localBaseY + highlightedCellY * 12;

                // 菱形的四個頂點（24x24 的菱形）
                Point p1 = new Point(X + 0, Y + 12);   // 左
                Point p2 = new Point(X + 12, Y + 0);   // 上
                Point p3 = new Point(X + 24, Y + 12);  // 右
                Point p4 = new Point(X + 12, Y + 24);  // 下

                // 填充半透明黃色
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(120, 255, 255, 0)))
                {
                    g.FillPolygon(brush, new Point[] { p1, p2, p3, p4 });
                }

                // 繪製亮黃色邊框
                using (Pen pen = new Pen(Color.FromArgb(255, 255, 200, 0), 3))
                {
                    g.DrawPolygon(pen, new Point[] { p1, p2, p3, p4 });
                }
            }
        }

        // 只繪製 S32 邊界框（用於除錯對齊），四個角落內側顯示座標
        private void DrawS32BoundaryOnly(Bitmap bitmap, Struct.L1Map currentMap)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                Font font = new Font("Arial", 9, FontStyle.Bold);
                Pen boundaryPen = new Pen(Color.Cyan, 2);

                foreach (var s32Data in allS32DataDict.Values)
                {
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    int mx = loc[0];
                    int my = loc[1];

                    // 用 Layer3 座標系
                    // 邊界框的四個角落直接用 (x3, y) = (0,0), (64,0), (64,64), (0,64)
                    // 這樣下一個 S32 的 (0,0) 就會和這個 S32 的 (0,64) 或 (64,0) 重疊
                    Point[] corners = new Point[4];

                    int[][] cornerCoords = new int[][] {
                        new int[] { 0, 0 },    // 左上
                        new int[] { 64, 0 },   // 右上
                        new int[] { 64, 64 },  // 右下
                        new int[] { 0, 64 }    // 左下
                    };

                    for (int i = 0; i < 4; i++)
                    {
                        int x3 = cornerCoords[i][0];
                        int y = cornerCoords[i][1];
                        int x = x3 * 2;

                        int localBaseX = 0 - 24 * (x / 2);
                        int localBaseY = 63 * 12 - 12 * (x / 2);
                        int X = mx + localBaseX + x * 24 + y * 24;
                        int Y = my + localBaseY + y * 12;

                        // 所有角落都取「上頂點」位置，並往左下移一格 (-24, +12)
                        corners[i] = new Point(X, Y + 12);
                    }

                    // 繪製邊界框
                    g.DrawLine(boundaryPen, corners[0], corners[1]);
                    g.DrawLine(boundaryPen, corners[1], corners[2]);
                    g.DrawLine(boundaryPen, corners[2], corners[3]);
                    g.DrawLine(boundaryPen, corners[3], corners[0]);

                    // 在中心顯示 GetLoc 值和座標範圍
                    int centerX = (corners[0].X + corners[2].X) / 2;
                    int centerY = (corners[0].Y + corners[2].Y) / 2;
                    string centerText = $"GetLoc({mx},{my})\n{s32Data.SegInfo.nLinBeginX},{s32Data.SegInfo.nLinBeginY}~{s32Data.SegInfo.nLinEndX},{s32Data.SegInfo.nLinEndY}";
                    using (SolidBrush cb = new SolidBrush(Color.FromArgb(200, Color.Black)))
                    using (SolidBrush ct = new SolidBrush(Color.Lime))
                    {
                        SizeF cs = g.MeasureString(centerText, font);
                        g.FillRectangle(cb, centerX - cs.Width/2 - 2, centerY - cs.Height/2 - 1, cs.Width + 4, cs.Height + 2);
                        g.DrawString(centerText, font, ct, centerX - cs.Width/2, centerY - cs.Height/2);
                    }

                    // 四個角落的遊戲座標
                    int bx = s32Data.SegInfo.nLinBeginX;
                    int by = s32Data.SegInfo.nLinBeginY;
                    int ex = s32Data.SegInfo.nLinEndX;
                    int ey = s32Data.SegInfo.nLinEndY;

                    // 在四個角落內側繪製座標（包含螢幕座標以便除錯）
                    using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
                    using (SolidBrush textBrush = new SolidBrush(Color.Yellow))
                    {
                        string[] texts = new string[] {
                            $"{bx},{by}\n({corners[0].X},{corners[0].Y})",   // 左上
                            $"{ex},{by}\n({corners[1].X},{corners[1].Y})",   // 右上
                            $"{ex},{ey}\n({corners[2].X},{corners[2].Y})",   // 右下
                            $"{bx},{ey}\n({corners[3].X},{corners[3].Y})"    // 左下
                        };

                        // 偏移量讓文字在邊界內側
                        int[][] offsets = new int[][] {
                            new int[] { 5, 5 },      // 左上：往右下
                            new int[] { -90, 5 },    // 右上：往左下
                            new int[] { -90, -35 },  // 右下：往左上
                            new int[] { 5, -35 }     // 左下：往右上
                        };

                        for (int i = 0; i < 4; i++)
                        {
                            SizeF size = g.MeasureString(texts[i], font);
                            int tx = corners[i].X + offsets[i][0];
                            int ty = corners[i].Y + offsets[i][1];
                            g.FillRectangle(bgBrush, tx - 2, ty - 1, size.Width + 4, size.Height + 2);
                            g.DrawString(texts[i], font, textBrush, tx, ty);
                        }
                    }
                }

                font.Dispose();
                boundaryPen.Dispose();
            }
        }

        // 繪製 S32 格子網格線 - 基於 Layer3 (64x64) 繪製格線
        // Layer3 的一個格子 = Layer1 的兩個格子 (x*2, x*2+1)，形成一個完整的等距菱形
        private void DrawS32Grid(Bitmap bitmap, Struct.L1Map currentMap)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                using (Pen gridPen = new Pen(Color.FromArgb(100, Color.Red), 1)) // 半透明紅色
                {
                    // 遍歷所有 S32 檔案
                    foreach (var s32Data in allS32DataDict.Values)
                    {
                        // 使用與 RenderS32Map 相同的座標計算方式
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int mx = loc[0];
                        int my = loc[1];

                        // 繪製格線 - 基於 Layer3（每個格子對應 Layer1 的 2 個格子）
                        // S32 覆蓋 128 個 Layer1 X 格子，對應 64 個 Layer3 X 格子
                        for (int y = 0; y < 64; y++)
                        {
                            for (int x3 = 0; x3 < 64; x3++)  // Layer3 座標 (0-63)
                            {
                                // Layer3 座標轉 Layer1 座標（取偶數 x）
                                int x = x3 * 2;

                                // 與 drawTilBlock 相同的像素計算
                                int localBaseX = 0;
                                int localBaseY = 63 * 12;
                                localBaseX -= 24 * (x / 2);
                                localBaseY -= 12 * (x / 2);

                                int X = mx + localBaseX + x * 24 + y * 24;
                                int Y = my + localBaseY + y * 12;

                                // Layer3 菱形的四個頂點（48x24，覆蓋兩個 Layer1 格子）
                                Point p1 = new Point(X, Y + 12);       // 左
                                Point p2 = new Point(X + 24, Y);       // 上
                                Point p3 = new Point(X + 48, Y + 12);  // 右
                                Point p4 = new Point(X + 24, Y + 24);  // 下

                                // 繪製菱形的四條邊
                                g.DrawLine(gridPen, p1, p2);  // 左上邊
                                g.DrawLine(gridPen, p2, p3);  // 右上邊
                                g.DrawLine(gridPen, p3, p4);  // 右下邊
                                g.DrawLine(gridPen, p4, p1);  // 左下邊
                            }
                        }
                    }
                }
            }
        }

        // 繪製座標標籤 - 渲染整張地圖的所有 S32
        private void DrawCoordinateLabels(Bitmap bitmap, Struct.L1Map currentMap)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                Font font = new Font("Arial", 8, FontStyle.Bold);

                // 每隔 10 格顯示一次座標（可調整間隔）
                int interval = 10;

                // 遍歷所有 S32 檔案
                foreach (var s32Data in allS32DataDict.Values)
                {
                    // 使用 GetLoc 計算區塊位置
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    int mx = loc[0];
                    int my = loc[1];

                    for (int y = 0; y < 64; y += interval)
                    {
                        for (int x = 0; x < 128; x += interval)
                        {
                            // 使用 GetLoc + drawTilBlock 公式計算像素位置
                            int localBaseX = 0;
                            int localBaseY = 63 * 12;
                            localBaseX -= 24 * (x / 2);
                            localBaseY -= 12 * (x / 2);

                            int X = mx + localBaseX + x * 24 + y * 24;
                            int Y = my + localBaseY + y * 12;

                            // 計算實際遊戲座標
                            int gameX = s32Data.SegInfo.nLinBeginX + x;
                            int gameY = s32Data.SegInfo.nLinBeginY + y;

                            // 繪製座標文字
                            string coordText = $"{gameX},{gameY}";
                            SizeF textSize = g.MeasureString(coordText, font);

                            // 繪製背景（半透明白色）
                            int textX = X + 12 - (int)textSize.Width / 2;
                            int textY = Y + 12 - (int)textSize.Height / 2;

                            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(180, Color.White)))
                            {
                                g.FillRectangle(bgBrush, textX - 2, textY - 1, textSize.Width + 4, textSize.Height + 2);
                            }

                            // 繪製座標文字（藍色）
                            using (SolidBrush textBrush = new SolidBrush(Color.Blue))
                            {
                                g.DrawString(coordText, font, textBrush, textX, textY);
                            }
                        }
                    }
                }

                font.Dispose();
            }
        }

        // 繪製 Tile 到緩衝區（簡化版）
        private unsafe void DrawTilToBuffer(int x, int y, int tileId, int indexId, int rowpix, byte* ptr, int maxWidth, int maxHeight, int mapHeightInCells)
        {
            try
            {
                // 使用快取減少重複讀取
                string cacheKey = $"{tileId}_{indexId}";
                byte[] tilData;

                if (!tileDataCache.TryGetValue(cacheKey, out tilData))
                {
                    // 快取中沒有，需要讀取
                    string key = $"{tileId}.til";
                    byte[] data = L1PakReader.UnPack("Tile", key);
                    if (data == null) return;

                    var tilArray = L1Til.Parse(data);
                    if (indexId >= tilArray.Count) return;

                    tilData = tilArray[indexId];

                    // 加入快取
                    tileDataCache[cacheKey] = tilData;
                }

                fixed (byte* til_ptr_fixed = tilData)
                {
                    byte* til_ptr = til_ptr_fixed;
                    byte type = *(til_ptr++);

                    int baseX = 0;
                    int baseY = (mapHeightInCells - 1) * 12;
                    baseX -= 24 * (x / 2);
                    baseY -= 12 * (x / 2);

                    if (type == 1 || type == 9 || type == 17)
                    {
                        for (int ty = 0; ty < 24; ty++)
                        {
                            int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                            int tx = 0;
                            for (int p = 0; p < n; p++)
                            {
                                ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));
                                int startX = baseX + x * 24 + y * 24 + tx;
                                int startY = baseY + y * 12 + ty;
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
                    else if (type == 0 || type == 8 || type == 16)
                    {
                        for (int ty = 0; ty < 24; ty++)
                        {
                            int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                            int tx = 24 - n;
                            for (int p = 0; p < n; p++)
                            {
                                ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));
                                int startX = baseX + x * 24 + y * 24 + tx;
                                int startY = baseY + y * 12 + ty;
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
                    else if (type == 34 || type == 35)
                    {
                        // 壓縮格式 - 需要與背景混合
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
                                    int startX = baseX + x * 24 + y * 24 + tx;
                                    int startY = baseY + y * 12 + ty + y_offset;
                                    if (startX >= 0 && startX < maxWidth && startY >= 0 && startY < maxHeight)
                                    {
                                        int v = startY * rowpix + (startX * 2);
                                        ushort colorB = (ushort)(*(ptr + v) | (*(ptr + v + 1) << 8));
                                        color = (ushort)(colorB + 0xffff - color);
                                        *(ptr + v) = (byte)(color & 0x00FF);
                                        *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                                    }
                                    tx++;
                                }
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
                                    int startX = baseX + x * 24 + y * 24 + tx;
                                    int startY = baseY + y * 12 + ty + y_offset;
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

        // 繪製 Tile 到緩衝區（直接使用像素座標）
        private unsafe void DrawTilToBufferDirect(int pixelX, int pixelY, int tileId, int indexId, int rowpix, byte* ptr, int maxWidth, int maxHeight)
        {
            try
            {
                // 使用快取減少重複讀取
                string cacheKey = $"{tileId}_{indexId}";
                byte[] tilData;

                if (!tileDataCache.TryGetValue(cacheKey, out tilData))
                {
                    // 快取中沒有，需要讀取
                    string key = $"{tileId}.til";
                    byte[] data = L1PakReader.UnPack("Tile", key);
                    if (data == null) return;

                    var tilArray = L1Til.Parse(data);
                    if (indexId >= tilArray.Count) return;

                    tilData = tilArray[indexId];

                    // 加入快取
                    tileDataCache[cacheKey] = tilData;
                }

                fixed (byte* til_ptr_fixed = tilData)
                {
                    byte* til_ptr = til_ptr_fixed;
                    byte type = *(til_ptr++);

                    if (type == 1 || type == 9 || type == 17)
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
                    else if (type == 0 || type == 8 || type == 16)
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
                    else if (type == 34 || type == 35)
                    {
                        // 壓縮格式 - 需要與背景混合
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
                                        ushort colorB = (ushort)(*(ptr + v) | (*(ptr + v + 1) << 8));
                                        color = (ushort)(colorB + 0xffff - color);
                                        *(ptr + v) = (byte)(color & 0x00FF);
                                        *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                                    }
                                    tx++;
                                }
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

        // 快取聚合的 Tile 資料（避免每次搜尋都重新計算）
        private Dictionary<int, TileInfo> cachedAggregatedTiles = new Dictionary<int, TileInfo>();

        // 更新 Tile 清單顯示 - 統計所有 S32 檔案的 tiles
        private void UpdateTileList()
        {
            UpdateTileList(null);  // 不帶搜尋條件
        }

        // 更新 Tile 清單顯示（支援搜尋過濾）
        private void UpdateTileList(string searchText)
        {
            lvTiles.Items.Clear();
            lvTiles.View = View.LargeIcon;

            // 創建 ImageList
            ImageList imageList = new ImageList();
            imageList.ImageSize = new Size(48, 48);
            imageList.ColorDepth = ColorDepth.Depth32Bit;
            lvTiles.LargeImageList = imageList;

            if (allS32DataDict.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "沒有 S32 檔案";
                return;
            }

            // 如果快取為空，重新聚合所有 S32 檔案的 UsedTiles
            if (cachedAggregatedTiles.Count == 0 || string.IsNullOrEmpty(searchText))
            {
                cachedAggregatedTiles.Clear();

                foreach (var s32Data in allS32DataDict.Values)
                {
                    foreach (var tileKvp in s32Data.UsedTiles)
                    {
                        int tileId = tileKvp.Key;
                        var tileInfo = tileKvp.Value;

                        if (cachedAggregatedTiles.ContainsKey(tileId))
                        {
                            // 累加使用次數
                            cachedAggregatedTiles[tileId].UsageCount += tileInfo.UsageCount;
                        }
                        else
                        {
                            // 新增 tile
                            cachedAggregatedTiles[tileId] = new TileInfo
                            {
                                TileId = tileInfo.TileId,
                                IndexId = tileInfo.IndexId,
                                UsageCount = tileInfo.UsageCount,
                                Thumbnail = null
                            };
                        }
                    }
                }
            }

            // 過濾 tiles
            var filteredTiles = cachedAggregatedTiles.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                searchText = searchText.Trim();
                // 支援多種搜尋方式：
                // 1. 精確 ID 搜尋（輸入數字）
                // 2. 範圍搜尋（如 "100-200"）
                // 3. 多個 ID 搜尋（如 "100,200,300"）
                if (searchText.Contains("-"))
                {
                    // 範圍搜尋
                    var parts = searchText.Split('-');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0].Trim(), out int minId) &&
                        int.TryParse(parts[1].Trim(), out int maxId))
                    {
                        filteredTiles = filteredTiles.Where(t => t.Key >= minId && t.Key <= maxId);
                    }
                }
                else if (searchText.Contains(","))
                {
                    // 多個 ID 搜尋
                    var ids = searchText.Split(',')
                        .Select(s => s.Trim())
                        .Where(s => int.TryParse(s, out _))
                        .Select(s => int.Parse(s))
                        .ToHashSet();
                    filteredTiles = filteredTiles.Where(t => ids.Contains(t.Key));
                }
                else if (int.TryParse(searchText, out int exactId))
                {
                    // 精確 ID 或前綴搜尋
                    filteredTiles = filteredTiles.Where(t => t.Key.ToString().StartsWith(searchText));
                }
                else
                {
                    // 文字搜尋（ID 包含此文字）
                    filteredTiles = filteredTiles.Where(t => t.Key.ToString().Contains(searchText));
                }
            }

            int index = 0;
            int totalCount = cachedAggregatedTiles.Count;
            foreach (var tileKvp in filteredTiles.OrderBy(t => t.Key))
            {
                var tile = tileKvp.Value;

                // 載入縮圖（如果還沒載入）
                if (tile.Thumbnail == null)
                {
                    tile.Thumbnail = LoadTileThumbnail(tile.TileId, tile.IndexId);
                }

                if (tile.Thumbnail != null)
                {
                    imageList.Images.Add(tile.Thumbnail);

                    var item = new ListViewItem
                    {
                        Text = $"ID:{tile.TileId}\n×{tile.UsageCount}",
                        ImageIndex = index,
                        Tag = tile
                    };
                    lvTiles.Items.Add(item);
                    index++;
                }
            }

            string statusText = string.IsNullOrWhiteSpace(searchText)
                ? $"顯示 {lvTiles.Items.Count} 個 Tile (來自 {allS32DataDict.Count} 個 S32 檔案)"
                : $"搜尋結果: {lvTiles.Items.Count}/{totalCount} 個 Tile";
            lblTileList.Text = statusText;
        }

        // Tile 搜尋框文字變更事件
        private void txtTileSearch_TextChanged(object sender, EventArgs e)
        {
            UpdateTileList(txtTileSearch.Text);
        }

        // 載入 Tile 縮圖
        private Bitmap LoadTileThumbnail(int tileId, int indexId)
        {
            try
            {
                string key = $"{tileId}.til";
                byte[] data = L1PakReader.UnPack("Tile", key);
                if (data == null) return CreatePlaceholderThumbnail(tileId);

                var tilArray = L1Til.Parse(data);
                if (indexId >= tilArray.Count) return CreatePlaceholderThumbnail(tileId);

                // 繪製實際的 tile 圖片
                byte[] tilData = tilArray[indexId];
                return RenderTileThumbnail(tilData, tileId);
            }
            catch
            {
                return CreatePlaceholderThumbnail(tileId);
            }
        }

        // 繪製 Tile 到縮圖
        private unsafe Bitmap RenderTileThumbnail(byte[] tilData, int tileId)
        {
            try
            {
                // 創建 48x48 的縮圖
                Bitmap thumbnail = new Bitmap(48, 48, PixelFormat.Format16bppRgb555);

                Rectangle rect = new Rectangle(0, 0, thumbnail.Width, thumbnail.Height);
                BitmapData bmpData = thumbnail.LockBits(rect, ImageLockMode.ReadWrite, thumbnail.PixelFormat);
                int rowpix = bmpData.Stride;
                byte* ptr = (byte*)bmpData.Scan0;

                // 固定 tilData 陣列以取得指標
                fixed (byte* til_ptr_fixed = tilData)
                {
                    byte* til_ptr = til_ptr_fixed;
                    byte type = *(til_ptr++);

                    // 縮圖偏移（置中）
                    int offsetX = 12;
                    int offsetY = 12;

                    if (type == 1 || type == 9 || type == 17)
                    {
                        // 下半部 2.5D 方塊
                        for (int ty = 0; ty < 24; ty++)
                        {
                            int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                            int tx = 0;
                            for (int p = 0; p < n; p++)
                            {
                                ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));
                                int px = offsetX + tx;
                                int py = offsetY + ty;
                                if (px >= 0 && px < 48 && py >= 0 && py < 48)
                                {
                                    int v = py * rowpix + (px * 2);
                                    *(ptr + v) = (byte)(color & 0x00FF);
                                    *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                                }
                                tx++;
                            }
                        }
                    }
                    else if (type == 0 || type == 8 || type == 16)
                    {
                        // 上半部 2.5D 方塊
                        for (int ty = 0; ty < 24; ty++)
                        {
                            int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                            int tx = 24 - n;
                            for (int p = 0; p < n; p++)
                            {
                                ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));
                                int px = offsetX + tx;
                                int py = offsetY + ty;
                                if (px >= 0 && px < 48 && py >= 0 && py < 48)
                                {
                                    int v = py * rowpix + (px * 2);
                                    *(ptr + v) = (byte)(color & 0x00FF);
                                    *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                                }
                                tx++;
                            }
                        }
                    }
                    else
                    {
                        // 壓縮格式（type 34, 35 或其他）
                        byte x_offset = *(til_ptr++);
                        byte y_offset = *(til_ptr++);
                        byte xxLen = *(til_ptr++);
                        byte yLen = *(til_ptr++);

                        for (int ty = 0; ty < yLen && ty < 48; ty++)
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
                                    int px = offsetX + tx;
                                    int py = offsetY + ty + y_offset;
                                    if (px >= 0 && px < 48 && py >= 0 && py < 48)
                                    {
                                        int v = py * rowpix + (px * 2);
                                        if (type == 34 || type == 35)
                                        {
                                            // 需要與背景混合
                                            ushort colorB = (ushort)(*(ptr + v) | (*(ptr + v + 1) << 8));
                                            color = (ushort)(colorB + 0xffff - color);
                                        }
                                        *(ptr + v) = (byte)(color & 0x00FF);
                                        *(ptr + v + 1) = (byte)((color & 0xFF00) >> 8);
                                    }
                                    tx++;
                                }
                            }
                        }
                    }
                }

                thumbnail.UnlockBits(bmpData);
                return thumbnail;
            }
            catch
            {
                return CreatePlaceholderThumbnail(tileId);
            }
        }

        // 創建佔位縮圖
        private Bitmap CreatePlaceholderThumbnail(int tileId)
        {
            Bitmap placeholder = new Bitmap(48, 48);
            using (Graphics g = Graphics.FromImage(placeholder))
            {
                g.Clear(Color.DarkGray);
                g.DrawRectangle(Pens.Red, 1, 1, 46, 46);
                using (Font font = new Font("Arial", 8))
                {
                    g.DrawString("?", font, Brushes.White, 18, 16);
                }
            }
            return placeholder;
        }

        // S32 地圖點擊事件 - 顯示該格子的四層 Tile
        private void s32PictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            // 如果正在選擇區域，不處理點擊
            if (isSelectingRegion)
                return;

            // 獲取當前地圖資訊以計算正確的 baseY
            if (string.IsNullOrEmpty(currentMapId) || !Share.MapDataList.ContainsKey(currentMapId))
                return;

            Struct.L1Map currentMap = Share.MapDataList[currentMapId];

            // 將點擊位置轉換為原始座標（考慮縮放）
            Point adjustedLocation = new Point(
                (int)(e.Location.X / s32ZoomLevel),
                (int)(e.Location.Y / s32ZoomLevel)
            );

            // 遍歷所有 S32 檔案
            foreach (var s32Data in allS32DataDict.Values)
            {
                // 使用 GetLoc 計算區塊位置
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 遍歷該 S32 的所有格子
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        // 使用 GetLoc + drawTilBlock 公式計算像素位置
                        int localBaseX = 0;
                        int localBaseY = 63 * 12;
                        localBaseX -= 24 * (x / 2);
                        localBaseY -= 12 * (x / 2);

                        int X = mx + localBaseX + x * 24 + y * 24;
                        int Y = my + localBaseY + y * 12;

                        // 菱形的四個頂點（24x24 的菱形）
                        Point p1 = new Point(X + 0, Y + 12);   // 左
                        Point p2 = new Point(X + 12, Y + 0);   // 上
                        Point p3 = new Point(X + 24, Y + 12);  // 右
                        Point p4 = new Point(X + 12, Y + 24);  // 下

                        // 檢查點擊位置是否在這個菱形內（使用調整後的座標）
                        if (IsPointInDiamond(adjustedLocation, p1, p2, p3, p4))
                        {
                            // 設置當前選中的 S32 檔案
                            currentS32FileItem = new S32FileItem
                            {
                                FilePath = s32Data.FilePath,
                                SegInfo = s32Data.SegInfo
                            };

                            // 記錄選中的格子並更新狀態列顯示第三層屬性
                            highlightedS32Data = s32Data;
                            highlightedCellX = x;
                            highlightedCellY = y;
                            UpdateStatusBarWithLayer3Info(s32Data, x, y);

                            // 通行性編輯模式
                            if (currentPassableEditMode != PassableEditMode.None && e.Button == MouseButtons.Left)
                            {
                                SetCellPassable(s32Data, x, y, currentPassableEditMode == PassableEditMode.SetPassable);
                                return;
                            }

                            // Ctrl + 左鍵：刪除該格子的所有第四層物件
                            if (e.Button == MouseButtons.Left && ModifierKeys == Keys.Control)
                            {
                                DeleteAllLayer4ObjectsAtCell(x, y);
                            }
                            else
                            {
                                // 重新渲染以顯示高亮
                                RenderS32Map();
                                // 正常顯示格子詳細資料
                                ShowCellLayersDialog(x, y);
                                // 更新 Layer4 群組清單
                                UpdateLayer4GroupsList(s32Data, x, y);
                            }
                            return;
                        }
                    }
                }
            }

            // 如果沒有點擊到任何 S32 區塊，檢查是否要新增 S32
            // 只有左鍵點擊才觸發新增
            if (e.Button == MouseButtons.Left)
            {
                TryCreateS32AtClickPosition(adjustedLocation, currentMap);
            }
        }

        // 嘗試在點擊位置創建新的 S32
        private void TryCreateS32AtClickPosition(Point clickPos, Struct.L1Map currentMap)
        {
            // 從點擊像素位置反推 Block 座標
            // GetLoc 公式：
            // baseX = 0;
            // baseY = (nMapBlockCountX - 1) * blockHeight / 2;
            // mx = blockX * blockWidth + my * 2 - blockX * blockWidth / 2 - blockY * blockWidth / 2;
            // my = baseY + blockY * blockHeight - blockX * blockHeight / 2 - blockY * blockHeight / 2;
            //
            // 簡化：對於菱形地圖，使用一個參考點來計算
            // 取得一個已知 S32 的位置作為參考

            if (allS32DataDict.Count == 0)
            {
                // 如果沒有任何 S32，使用地圖的 Min Block 作為基準
                int defaultBlockX = currentMap.nMinBlockX != 0xFFFF ? currentMap.nMinBlockX : 0x7FFF;
                int defaultBlockY = currentMap.nMinBlockY != 0xFFFF ? currentMap.nMinBlockY : 0x8000;
                CreateNewS32AtBlock(defaultBlockX, defaultBlockY, currentMap);
                return;
            }

            // 取得第一個 S32 作為參考點
            var refS32 = allS32DataDict.Values.First();
            int[] refLoc = refS32.SegInfo.GetLoc(1.0);
            int refMx = refLoc[0];
            int refMy = refLoc[1];
            int refBlockX = refS32.SegInfo.nBlockX - currentMap.nMinBlockX;
            int refBlockY = refS32.SegInfo.nBlockY - currentMap.nMinBlockY;

            // 每個 S32 區塊的像素大小
            int blockWidth = 64 * 24 * 2;  // 3072
            int blockHeight = 64 * 12 * 2; // 1536

            // 參考 S32 的中心像素位置
            int refCenterX = refMx + blockWidth / 2;
            int refCenterY = refMy + blockHeight / 2;

            // 計算點擊位置相對於參考 S32 的偏移量（像素）
            int deltaPixelX = clickPos.X - refCenterX;
            int deltaPixelY = clickPos.Y - refCenterY;

            // 菱形座標系轉換：
            // 根據 GetLoc 公式：
            // mx = baseX + blockX*W - blockX*W/2 - blockY*W/2 + blockY*W = baseX + blockX*W/2 + blockY*W/2
            // my = baseY + blockY*H - blockX*H/2 - blockY*H/2 = baseY - blockX*H/2 + blockY*H/2
            //
            // 所以：
            // deltaPixelX = (dBx + dBy) * blockWidth/2
            // deltaPixelY = (-dBx + dBy) * blockHeight/2
            //
            // 反推：
            // dBx + dBy = deltaPixelX * 2 / blockWidth
            // -dBx + dBy = deltaPixelY * 2 / blockHeight
            // 2*dBy = deltaPixelX * 2 / blockWidth + deltaPixelY * 2 / blockHeight
            // 2*dBx = deltaPixelX * 2 / blockWidth - deltaPixelY * 2 / blockHeight

            double sumDelta = (double)deltaPixelX * 2 / blockWidth;
            double diffDelta = (double)deltaPixelY * 2 / blockHeight;

            int deltaBlockX = (int)Math.Round((sumDelta - diffDelta) / 2);
            int deltaBlockY = (int)Math.Round((sumDelta + diffDelta) / 2);

            // 計算目標 Block 座標
            int targetRelBlockX = refBlockX + deltaBlockX;
            int targetRelBlockY = refBlockY + deltaBlockY;

            int targetBlockX = currentMap.nMinBlockX + targetRelBlockX;
            int targetBlockY = currentMap.nMinBlockY + targetRelBlockY;

            // 檢查是否已存在
            string fileName = $"{targetBlockX:X4}{targetBlockY:X4}.s32".ToLower();
            string filePath = Path.Combine(currentMap.szFullDirName, fileName);

            if (File.Exists(filePath) || allS32DataDict.Keys.Any(k => k.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)))
            {
                // 已存在，顯示提示
                this.toolStripStatusLabel1.Text = $"S32 已存在: {fileName}";
                return;
            }

            // 創建新 S32
            CreateNewS32AtBlock(targetBlockX, targetBlockY, currentMap);
        }

        // 在指定 Block 座標創建新的 S32
        private void CreateNewS32AtBlock(int blockX, int blockY, Struct.L1Map currentMap)
        {
            string fileName = $"{blockX:X4}{blockY:X4}.s32".ToLower();
            string filePath = Path.Combine(currentMap.szFullDirName, fileName);

            // 檢查是否已存在
            if (File.Exists(filePath))
            {
                MessageBox.Show($"S32 檔案已存在: {fileName}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (allS32DataDict.Keys.Any(k => k.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"S32 檔案已載入: {fileName}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 計算遊戲座標範圍
            int linEndX = (blockX - 0x7FFF) * 64 + 0x7FFF;
            int linEndY = (blockY - 0x7FFF) * 64 + 0x7FFF;
            int linBeginX = linEndX - 64 + 1;
            int linBeginY = linEndY - 64 + 1;

            // 確認新增
            var confirmResult = MessageBox.Show(
                $"要在此位置新增 S32 區塊嗎？\n\n" +
                $"檔案名稱: {fileName}\n" +
                $"Block座標: ({blockX:X4}, {blockY:X4})\n" +
                $"遊戲座標: ({linBeginX},{linBeginY}) ~ ({linEndX},{linEndY})\n" +
                $"路徑: {filePath}",
                "新增 S32",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes) return;

            // 創建空的 S32 資料
            S32Data newS32Data = CreateEmptyS32Data(blockX, blockY, filePath);

            // 創建 SegInfo
            Struct.L1MapSeg segInfo = new Struct.L1MapSeg(blockX, blockY, true);
            segInfo.isRemastered = false;
            segInfo.nMapMinBlockX = currentMap.nMinBlockX;
            segInfo.nMapMinBlockY = currentMap.nMinBlockY;
            segInfo.nMapBlockCountX = currentMap.nBlockCountX;

            newS32Data.SegInfo = segInfo;
            newS32Data.IsModified = true;

            // 加入到記憶體（先加入才能用 SaveS32File）
            allS32DataDict[filePath] = newS32Data;

            // 寫入檔案
            try
            {
                SaveS32File(filePath);  // 使用正確格式的保存方法
            }
            catch (Exception ex)
            {
                // 移除失敗的 S32
                allS32DataDict.Remove(filePath);
                MessageBox.Show($"寫入檔案失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 加入到 FullFileNameList
            currentMap.FullFileNameList[filePath] = segInfo;

            // 更新地圖邊界（如果新的 S32 超出現有邊界）
            if (blockX < currentMap.nMinBlockX || blockX > currentMap.nMaxBlockX ||
                blockY < currentMap.nMinBlockY || blockY > currentMap.nMaxBlockY)
            {
                currentMap.nMinBlockX = Math.Min(currentMap.nMinBlockX, blockX);
                currentMap.nMinBlockY = Math.Min(currentMap.nMinBlockY, blockY);
                currentMap.nMaxBlockX = Math.Max(currentMap.nMaxBlockX, blockX);
                currentMap.nMaxBlockY = Math.Max(currentMap.nMaxBlockY, blockY);
                currentMap.nBlockCountX = currentMap.nMaxBlockX - currentMap.nMinBlockX + 1;
                currentMap.nBlockCountY = currentMap.nMaxBlockY - currentMap.nMinBlockY + 1;
                currentMap.nLinLengthX = currentMap.nBlockCountX * 64;
                currentMap.nLinLengthY = currentMap.nBlockCountY * 64;
                currentMap.nLinEndX = (currentMap.nMaxBlockX - 0x7FFF) * 64 + 0x7FFF;
                currentMap.nLinEndY = (currentMap.nMaxBlockY - 0x7FFF) * 64 + 0x7FFF;
                currentMap.nLinBeginX = currentMap.nLinEndX - currentMap.nLinLengthX + 1;
                currentMap.nLinBeginY = currentMap.nLinEndY - currentMap.nLinLengthY + 1;

                // 更新所有 SegInfo 的共享值
                foreach (var seg in currentMap.FullFileNameList.Values)
                {
                    seg.nMapMinBlockX = currentMap.nMinBlockX;
                    seg.nMapMinBlockY = currentMap.nMinBlockY;
                    seg.nMapBlockCountX = currentMap.nBlockCountX;
                }
            }

            // 加入到 UI 清單
            string displayName = $"{fileName} ({blockX:X4},{blockY:X4}) [{segInfo.nLinBeginX},{segInfo.nLinBeginY}~{segInfo.nLinEndX},{segInfo.nLinEndY}]";
            S32FileItem item = new S32FileItem
            {
                FilePath = filePath,
                DisplayName = displayName,
                SegInfo = segInfo,
                IsChecked = true
            };
            int index = lstS32Files.Items.Add(item);
            lstS32Files.SetItemChecked(index, true);

            // 重新渲染地圖
            RenderS32Map();

            this.toolStripStatusLabel1.Text = $"已新增 S32: {fileName}";
        }

        // 更新狀態列顯示第三層屬性資訊
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
            selectedGameX = gameX;
            selectedGameY = gameY;
            toolStripCopyMoveCmd.Enabled = true;
            toolStripCopyMoveCmd.Text = $"移動 {gameX} {gameY} {currentMapId}";

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

        // 設定單個格子的通行性
        private void SetCellPassable(S32Data s32Data, int cellX, int cellY, bool passable)
        {
            // 計算第三層座標（第三層是 64x64，第一層是 64x128）
            int layer3X = cellX / 2;
            if (layer3X >= 64) layer3X = 63;

            // 計算遊戲座標（Layer3 尺度）
            int gameX = s32Data.SegInfo.nLinBeginX + layer3X;
            int gameY = s32Data.SegInfo.nLinBeginY + cellY;

            // 設定通行性屬性
            if (s32Data.Layer3[cellY, layer3X] == null)
            {
                s32Data.Layer3[cellY, layer3X] = new MapAttribute { Attribute1 = 0, Attribute2 = 0 };
            }

            if (passable)
            {
                // 清除不可通行屬性（Attribute1 & Attribute2 的 0x01 位元）
                s32Data.Layer3[cellY, layer3X].Attribute1 = (short)(s32Data.Layer3[cellY, layer3X].Attribute1 & ~0x01);
                s32Data.Layer3[cellY, layer3X].Attribute2 = (short)(s32Data.Layer3[cellY, layer3X].Attribute2 & ~0x01);
                this.toolStripStatusLabel1.Text = $"已設定 ({gameX},{gameY}) 為可通行";
            }
            else
            {
                // 設定不可通行屬性（Attribute1 & Attribute2 的 0x01 位元）
                s32Data.Layer3[cellY, layer3X].Attribute1 = (short)(s32Data.Layer3[cellY, layer3X].Attribute1 | 0x01);
                s32Data.Layer3[cellY, layer3X].Attribute2 = (short)(s32Data.Layer3[cellY, layer3X].Attribute2 | 0x01);
                this.toolStripStatusLabel1.Text = $"已設定 ({gameX},{gameY}) 為不可通行";
            }

            s32Data.IsModified = true;
            RenderS32Map();
        }

        // 批次設定區域通行性
        private void SetRegionPassable(List<SelectedCell> cells, bool passable)
        {
            int modifiedCount = 0;
            HashSet<S32Data> modifiedS32Files = new HashSet<S32Data>();

            foreach (var cell in cells)
            {
                // 計算第三層座標（第三層是 64x64，第一層是 64x128）
                int layer3X = cell.LocalX / 2;
                if (layer3X >= 64) layer3X = 63;

                // 設定通行性屬性
                if (cell.S32Data.Layer3[cell.LocalY, layer3X] == null)
                {
                    cell.S32Data.Layer3[cell.LocalY, layer3X] = new MapAttribute { Attribute1 = 0, Attribute2 = 0 };
                }

                if (passable)
                {
                    // 清除不可通行屬性（Attribute1 & Attribute2 的 0x01 位元）
                    cell.S32Data.Layer3[cell.LocalY, layer3X].Attribute1 = (short)(cell.S32Data.Layer3[cell.LocalY, layer3X].Attribute1 & ~0x01);
                    cell.S32Data.Layer3[cell.LocalY, layer3X].Attribute2 = (short)(cell.S32Data.Layer3[cell.LocalY, layer3X].Attribute2 & ~0x01);
                }
                else
                {
                    // 設定不可通行屬性（Attribute1 & Attribute2 的 0x01 位元）
                    cell.S32Data.Layer3[cell.LocalY, layer3X].Attribute1 = (short)(cell.S32Data.Layer3[cell.LocalY, layer3X].Attribute1 | 0x01);
                    cell.S32Data.Layer3[cell.LocalY, layer3X].Attribute2 = (short)(cell.S32Data.Layer3[cell.LocalY, layer3X].Attribute2 | 0x01);
                }

                modifiedCount++;
                modifiedS32Files.Add(cell.S32Data);
            }

            // 標記所有修改過的 S32 檔案
            foreach (var s32Data in modifiedS32Files)
            {
                s32Data.IsModified = true;
            }

            RenderS32Map();
            this.toolStripStatusLabel1.Text = $"已批次設定 {modifiedCount} 個格子為{(passable ? "可通行" : "不可通行")} (影響 {modifiedS32Files.Count} 個 S32 檔案)";
        }

        // S32 地圖鼠標按下事件 - 開始區域選擇或拖拽移動
        private void s32PictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            // 中鍵拖拽移動視圖
            if (e.Button == MouseButtons.Middle)
            {
                isMainMapDragging = true;
                mainMapDragStartPoint = e.Location;
                mainMapDragStartScroll = new Point(
                    -this.s32MapPanel.AutoScrollPosition.X,
                    -this.s32MapPanel.AutoScrollPosition.Y);
                this.s32PictureBox.Cursor = Cursors.SizeAll;
                return;
            }

            if (currentS32Data == null || currentS32FileItem == null)
                return;

            // Ctrl + 左鍵 + 通行性編輯模式：開始批次設定通行性
            if (e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.Control && currentPassableEditMode != PassableEditMode.None)
            {
                isSelectingRegion = true;
                isLayer4CopyMode = false;
                regionStartPoint = e.Location;
                regionEndPoint = e.Location;
                selectedRegion = new Rectangle();
                this.toolStripStatusLabel1.Text = "拖拽選擇區域...";
            }
            // Shift + 左鍵：開始區域選擇（複製 Layer4 物件）
            else if (e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.Shift)
            {
                isSelectingRegion = true;
                isLayer4CopyMode = true;  // 進入複製模式
                regionStartPoint = e.Location;
                regionEndPoint = e.Location;
                selectedRegion = new Rectangle();
                this.toolStripStatusLabel1.Text = "選取要複製的 Layer4 區域... (放開後按 Ctrl+C 複製)";
            }
        }

        // S32 地圖鼠標移動事件 - 更新選擇區域或拖拽移動
        private void s32PictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            // 中鍵拖拽移動視圖
            if (isMainMapDragging)
            {
                int deltaX = e.X - mainMapDragStartPoint.X;
                int deltaY = e.Y - mainMapDragStartPoint.Y;

                int newScrollX = mainMapDragStartScroll.X - deltaX;
                int newScrollY = mainMapDragStartScroll.Y - deltaY;

                // 限制在有效範圍內
                int maxScrollX = Math.Max(0, this.s32PictureBox.Width - this.s32MapPanel.Width);
                int maxScrollY = Math.Max(0, this.s32PictureBox.Height - this.s32MapPanel.Height);
                newScrollX = Math.Max(0, Math.Min(newScrollX, maxScrollX));
                newScrollY = Math.Max(0, Math.Min(newScrollY, maxScrollY));

                this.s32MapPanel.AutoScrollPosition = new Point(newScrollX, newScrollY);
                return;
            }

            if (isSelectingRegion)
            {
                regionEndPoint = e.Location;

                // 計算起點到終點之間的格子範圍（所有模式都對齊格線）
                currentSelectedCells = GetCellsInIsometricRange(regionStartPoint, regionEndPoint);
                if (currentSelectedCells.Count > 0)
                {
                    selectedRegion = GetAlignedBoundsFromCells(currentSelectedCells);
                }

                // 重繪以顯示選擇框
                s32PictureBox.Invalidate();
            }
        }

        // S32 地圖鼠標釋放事件 - 完成區域選擇並執行批量操作
        private void s32PictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            // 結束中鍵拖拽
            if (e.Button == MouseButtons.Middle && isMainMapDragging)
            {
                isMainMapDragging = false;
                this.s32PictureBox.Cursor = Cursors.Default;
                UpdateMiniMap();
                return;
            }

            if (isSelectingRegion && e.Button == MouseButtons.Left)
            {
                isSelectingRegion = false;

                // Layer4 複製模式：保留選取範圍，等待 Ctrl+C 或 Ctrl+V
                if (isLayer4CopyMode)
                {
                    // currentSelectedCells 已在 MouseMove 中更新
                    if (currentSelectedCells.Count > 0)
                    {
                        // 計算所有選中格子的螢幕座標邊界
                        copyRegionBounds = GetAlignedBoundsFromCells(currentSelectedCells);
                        selectedRegion = copyRegionBounds;
                    }
                    else
                    {
                        copyRegionBounds = selectedRegion;
                    }

                    // 計算選取區域的全域 Layer1 座標原點（使用選中格子中最小的 X, Y 作為原點）
                    int globalX = -1, globalY = -1;
                    int gameX = -1, gameY = -1;
                    if (currentSelectedCells.Count > 0)
                    {
                        // 找出所有選中格子的最小全域 Layer1 座標
                        int minGlobalX = int.MaxValue, minGlobalY = int.MaxValue;
                        foreach (var cell in currentSelectedCells)
                        {
                            // nLinBeginX 是 Layer3 座標，乘以 2 轉成 Layer1，再加上 cell.LocalX
                            int cellGlobalX = cell.S32Data.SegInfo.nLinBeginX * 2 + cell.LocalX;
                            int cellGlobalY = cell.S32Data.SegInfo.nLinBeginY + cell.LocalY;
                            if (cellGlobalX < minGlobalX) minGlobalX = cellGlobalX;
                            if (cellGlobalY < minGlobalY) minGlobalY = cellGlobalY;
                        }
                        globalX = minGlobalX;
                        globalY = minGlobalY;
                        // 計算遊戲座標（Layer3 尺度）
                        gameX = globalX / 2;
                        gameY = globalY;
                    }
                    copyRegionOrigin = new Point(globalX, globalY);

                    // 根據是否有剪貼簿資料顯示不同提示（顯示遊戲座標）
                    if (hasLayer4Clipboard && cellClipboard.Count > 0)
                    {
                        this.toolStripStatusLabel1.Text = $"已選取貼上位置 (遊戲座標: {gameX}, {gameY})，按 Ctrl+V 貼上 {cellClipboard.Count} 格資料，選中 {currentSelectedCells.Count} 格";
                    }
                    else
                    {
                        this.toolStripStatusLabel1.Text = $"已選取區域 (遊戲座標: {gameX}, {gameY})，選中 {currentSelectedCells.Count} 格，按 Ctrl+C 複製";
                    }

                    // 更新群組縮圖列表，顯示選取區域內的群組
                    UpdateGroupThumbnailsList(currentSelectedCells);

                    // 保留選取框顯示
                    s32PictureBox.Invalidate();
                    return;
                }

                // 找出選中區域內的所有格子
                List<SelectedCell> selectedCells = GetCellsInRegion(selectedRegion);

                if (selectedCells.Count > 0)
                {
                    // 通行性編輯模式：批次設定通行性
                    if (currentPassableEditMode != PassableEditMode.None)
                    {
                        SetRegionPassable(selectedCells, currentPassableEditMode == PassableEditMode.SetPassable);
                    }
                    // 刪除模式：批次刪除物件
                    else
                    {
                        DeleteAllLayer4ObjectsInRegion(selectedCells);
                    }
                }

                // 清除選擇框
                selectedRegion = new Rectangle();
                s32PictureBox.Invalidate();
            }
        }

        // S32 PictureBox 繪製事件 - 繪製選擇框
        private void s32PictureBox_Paint(object sender, PaintEventArgs e)
        {
            // 有選中的格子時，繪製對齊格線的菱形選取框
            if (currentSelectedCells.Count > 0)
            {
                Color color = isSelectingRegion ? Color.Green : Color.Orange;
                DrawSelectedCells(e.Graphics, currentSelectedCells, color);

                // 顯示選取的格子數量
                if (isSelectingRegion)
                {
                    string info = $"選取 {currentSelectedCells.Count} 格";
                    using (Font font = new Font("Arial", 10, FontStyle.Bold))
                    using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(180, Color.Black)))
                    using (SolidBrush textBrush = new SolidBrush(Color.White))
                    {
                        SizeF textSize = e.Graphics.MeasureString(info, font);
                        // 在滑鼠位置附近顯示
                        float textX = regionEndPoint.X + 15;
                        float textY = regionEndPoint.Y - 20;
                        e.Graphics.FillRectangle(bgBrush, textX - 2, textY - 2, textSize.Width + 4, textSize.Height + 4);
                        e.Graphics.DrawString(info, font, textBrush, textX, textY);
                    }
                }
            }
        }

        // 繪製選中的格子（每個格子繪製獨立的菱形）
        private void DrawSelectedCells(Graphics g, List<SelectedCell> cells, Color color)
        {
            if (cells.Count == 0 || string.IsNullOrEmpty(currentMapId) || !Share.MapDataList.ContainsKey(currentMapId))
                return;

            using (SolidBrush brush = new SolidBrush(Color.FromArgb(50, color)))
            using (Pen pen = new Pen(color, 2))
            {
                foreach (var cell in cells)
                {
                    // 與 DrawS32Grid 完全相同的座標計算（Layer3 格子，48x24）
                    int[] loc = cell.S32Data.SegInfo.GetLoc(1.0);
                    int mx = loc[0];
                    int my = loc[1];

                    int x = cell.LocalX;  // 已經是 Layer1 座標 (x3 * 2)
                    int y = cell.LocalY;

                    int localBaseX = 0;
                    int localBaseY = 63 * 12;
                    localBaseX -= 24 * (x / 2);
                    localBaseY -= 12 * (x / 2);

                    int X = mx + localBaseX + x * 24 + y * 24;
                    int Y = my + localBaseY + y * 12;

                    // Layer3 菱形四個頂點（48x24，與 DrawS32Grid 一致）
                    Point[] diamondPoints = new Point[]
                    {
                        new Point(X, Y + 12),       // 左
                        new Point(X + 24, Y),       // 上
                        new Point(X + 48, Y + 12),  // 右
                        new Point(X + 24, Y + 24)   // 下
                    };

                    g.FillPolygon(brush, diamondPoints);
                    g.DrawPolygon(pen, diamondPoints);
                }
            }
        }

        // 繪製等距菱形選取框（支援長方形）
        private void DrawIsometricSelectionBox(Graphics g, Rectangle region, Color color)
        {
            // 計算等距投影菱形的四個頂點
            float centerX = region.Left + region.Width / 2f;
            float centerY = region.Top + region.Height / 2f;

            // 使用實際的寬高，保持 2:1 的等距比例
            float halfWidth = region.Width / 2f;       // 水平方向半寬
            float halfHeight = region.Height / 2f;     // 垂直方向半高

            PointF[] diamondPoints = new PointF[]
            {
                new PointF(centerX, centerY - halfHeight),  // 上
                new PointF(centerX + halfWidth, centerY),   // 右
                new PointF(centerX, centerY + halfHeight),  // 下
                new PointF(centerX - halfWidth, centerY)    // 左
            };

            // 繪製半透明選擇框（菱形）
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(80, color)))
            {
                g.FillPolygon(brush, diamondPoints);
            }

            // 繪製邊框（菱形）
            using (Pen pen = new Pen(color, 3))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawPolygon(pen, diamondPoints);
            }
        }

        // 獲取矩形區域內的所有格子座標（返回格子所屬的 S32Data 和局部座標）
        private class SelectedCell
        {
            public S32Data S32Data { get; set; }
            public int LocalX { get; set; }
            public int LocalY { get; set; }
        }

        private List<SelectedCell> GetCellsInRegion(Rectangle region)
        {
            List<SelectedCell> cells = new List<SelectedCell>();

            // 獲取當前地圖資訊以計算正確的 baseY
            if (string.IsNullOrEmpty(currentMapId) || !Share.MapDataList.ContainsKey(currentMapId))
                return cells;

            Struct.L1Map currentMap = Share.MapDataList[currentMapId];

            // 將螢幕矩形的四個角轉換為遊戲座標，找出遊戲座標的範圍
            // 螢幕座標 -> 遊戲座標的反向轉換
            int baseYOffset = (currentMap.nLinLengthY - 1) * 12;

            // 矩形的四個角點
            Point[] corners = new Point[]
            {
                new Point(region.Left, region.Top),
                new Point(region.Right, region.Top),
                new Point(region.Left, region.Bottom),
                new Point(region.Right, region.Bottom)
            };

            // 計算每個角點對應的遊戲座標範圍
            int minGameX = int.MaxValue, maxGameX = int.MinValue;
            int minGameY = int.MaxValue, maxGameY = int.MinValue;

            foreach (var corner in corners)
            {
                // 反向計算遊戲座標 (近似值，用於確定搜索範圍)
                // X = baseX + globalX * 24 + globalY * 24
                // Y = baseY + globalY * 12
                // 其中 baseX = -24 * (globalX / 2), baseY = baseYOffset - 12 * (globalX / 2)

                // 簡化：假設 globalX 為偶數時
                // X ≈ globalX * 12 + globalY * 24
                // Y ≈ baseYOffset + globalY * 12 - globalX * 6

                // 從 Y 得：globalY ≈ (Y - baseYOffset + globalX * 6) / 12
                // 從 X 得：globalX ≈ (X - globalY * 24) / 12

                // 用迭代方式估算
                for (int gx = -50; gx < currentMap.nLinLengthX + 50; gx += 10)
                {
                    for (int gy = -50; gy < currentMap.nLinLengthY + 50; gy += 10)
                    {
                        int bx = -24 * (gx / 2);
                        int by = baseYOffset - 12 * (gx / 2);
                        int sx = bx + gx * 24 + gy * 24;
                        int sy = by + gy * 12;

                        if (Math.Abs(sx - corner.X) < 200 && Math.Abs(sy - corner.Y) < 100)
                        {
                            minGameX = Math.Min(minGameX, gx - 20);
                            maxGameX = Math.Max(maxGameX, gx + 20);
                            minGameY = Math.Min(minGameY, gy - 20);
                            maxGameY = Math.Max(maxGameY, gy + 20);
                        }
                    }
                }
            }

            // 遍歷所有 S32 檔案
            foreach (var s32Data in allS32DataDict.Values)
            {
                // 使用 GetLoc 計算區塊位置
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        // 使用 GetLoc + drawTilBlock 公式計算像素位置
                        int localBaseX = 0;
                        int localBaseY = 63 * 12;
                        localBaseX -= 24 * (x / 2);
                        localBaseY -= 12 * (x / 2);

                        int X = mx + localBaseX + x * 24 + y * 24;
                        int Y = my + localBaseY + y * 12;

                        // 菱形的中心點
                        Point centerPoint = new Point(X + 12, Y + 12);

                        // 檢查中心點是否在選擇區域內（使用45度菱形判斷）
                        // 將選擇矩形視為螢幕上的菱形區域
                        if (IsPointInIsometricRegion(centerPoint, region))
                        {
                            cells.Add(new SelectedCell
                            {
                                S32Data = s32Data,
                                LocalX = x,
                                LocalY = y
                            });
                        }
                    }
                }
            }

            return cells;
        }

        // 檢查點是否在等距投影的菱形區域內（2:1 比例）
        private bool IsPointInIsometricRegion(Point point, Rectangle screenRect)
        {
            // 螢幕矩形的中心點
            float centerX = screenRect.Left + screenRect.Width / 2f;
            float centerY = screenRect.Top + screenRect.Height / 2f;

            // 將點相對於中心的偏移
            float dx = point.X - centerX;
            float dy = point.Y - centerY;

            // 等距投影菱形（2:1 比例）
            // 取寬和高的較大值來決定菱形大小，保持 2:1 比例
            float size = Math.Max(screenRect.Width, screenRect.Height * 2);
            float halfWidth = size / 2f;      // 水平方向半寬
            float halfHeight = size / 4f;     // 垂直方向半高（2:1 比例）

            // 使用菱形的標準判斷公式: |x/a| + |y/b| <= 1
            float normalizedX = Math.Abs(dx) / halfWidth;
            float normalizedY = Math.Abs(dy) / halfHeight;

            return (normalizedX + normalizedY) <= 1.0f;
        }

        // 批量刪除區域內選中層的資料（根據複製設定 checkbox）
        private void DeleteAllLayer4ObjectsInRegion(List<SelectedCell> cells)
        {
            bool deleteLayer1 = copySettingLayer1;
            bool deleteLayer2 = copySettingLayer2;
            bool deleteLayer3 = copySettingLayer3;
            bool deleteLayer4 = copySettingLayer4;
            bool deleteLayer5to8 = copySettingLayer5to8;

            if (!deleteLayer1 && !deleteLayer2 && !deleteLayer3 && !deleteLayer4 && !deleteLayer5to8)
            {
                this.toolStripStatusLabel1.Text = "請點擊「複製設定...」按鈕選擇要刪除的圖層";
                return;
            }

            // 統計要刪除的資料
            int layer1Count = 0, layer2Count = 0, layer3Count = 0, layer4Count = 0, layer5to8Count = 0;
            Dictionary<S32Data, List<ObjectTile>> objectsToDeleteByS32 = new Dictionary<S32Data, List<ObjectTile>>();
            HashSet<S32Data> affectedS32 = new HashSet<S32Data>();

            // 先統計數量
            foreach (var cell in cells)
            {
                int layer3X = cell.LocalX / 2;

                // Layer1 資料統計
                if (deleteLayer1)
                {
                    int layer1X = cell.LocalX;  // cell.LocalX 已經是 Layer3 * 2
                    if (layer1X >= 0 && layer1X < 128)
                    {
                        var cell1 = cell.S32Data.Layer1[cell.LocalY, layer1X];
                        if (cell1 != null && cell1.TileId > 0) layer1Count++;
                    }
                    if (layer1X + 1 >= 0 && layer1X + 1 < 128)
                    {
                        var cell2 = cell.S32Data.Layer1[cell.LocalY, layer1X + 1];
                        if (cell2 != null && cell2.TileId > 0) layer1Count++;
                    }
                }

                // Layer3 資料統計
                if (deleteLayer3)
                {
                    if (layer3X >= 0 && layer3X < 64)
                    {
                        var attr = cell.S32Data.Layer3[cell.LocalY, layer3X];
                        if (attr != null && (attr.Attribute1 != 0 || attr.Attribute2 != 0)) layer3Count++;
                    }
                }

                // Layer4 物件統計已移至跨區塊搜索統一處理

                affectedS32.Add(cell.S32Data);
            }

            // Layer4 跨區塊物件搜索：遍歷所有 S32，找出座標精確落在選取範圍內的物件
            if (deleteLayer4 && cells.Count > 0)
            {
                // 建立選取格子的全域座標集合 (Layer3 座標系)
                var selectedGlobalCells = new HashSet<(int x, int y)>();
                foreach (var cell in cells)
                {
                    int globalX = cell.S32Data.SegInfo.nLinBeginX + cell.LocalX / 2;
                    int globalY = cell.S32Data.SegInfo.nLinBeginY + cell.LocalY;
                    selectedGlobalCells.Add((globalX, globalY));
                }

                // 遍歷所有 S32 檔案搜索物件（精確匹配選取格子）
                foreach (var s32Data in allS32DataDict.Values)
                {
                    int segStartX = s32Data.SegInfo.nLinBeginX;
                    int segStartY = s32Data.SegInfo.nLinBeginY;

                    foreach (var obj in s32Data.Layer4)
                    {
                        // 計算物件的全域座標 (考慮物件 X 可能超出 0-127 範圍，即溢出到相鄰區塊)
                        // 物件的 X 範圍可達 0-255，X/2 範圍為 0-127，可能超出當前 S32 的 64 格範圍
                        int objGlobalX = segStartX + obj.X / 2;
                        int objGlobalY = segStartY + obj.Y;

                        // 精確檢查是否在選取的格子內
                        if (selectedGlobalCells.Contains((objGlobalX, objGlobalY)))
                        {
                            // 檢查群組篩選
                            if (isFilteringLayer4Groups && selectedLayer4Groups.Count > 0 &&
                                !selectedLayer4Groups.Contains(obj.GroupId))
                                continue;

                            // 避免重複加入
                            if (!objectsToDeleteByS32.ContainsKey(s32Data))
                            {
                                objectsToDeleteByS32[s32Data] = new List<ObjectTile>();
                            }
                            if (!objectsToDeleteByS32[s32Data].Contains(obj))
                            {
                                objectsToDeleteByS32[s32Data].Add(obj);
                                layer4Count++;
                            }
                        }
                    }
                }
            }

            // Layer2 統計（整個 S32 共用的資料）
            if (deleteLayer2)
            {
                foreach (var s32 in affectedS32)
                {
                    layer2Count += s32.Layer2.Count;
                }
            }

            // Layer5 統計（按格子位置刪除透明圖塊）
            Dictionary<S32Data, List<Layer5Item>> layer5ToDeleteByS32 = new Dictionary<S32Data, List<Layer5Item>>();
            if (deleteLayer5to8)
            {
                foreach (var cell in cells)
                {
                    // Layer5 的 X, Y 是 0-63 範圍（與 Layer3 相同）
                    int layer3X = cell.LocalX / 2;
                    var layer5Items = cell.S32Data.Layer5.Where(l => l.X == layer3X && l.Y == cell.LocalY).ToList();
                    if (layer5Items.Count > 0)
                    {
                        if (!layer5ToDeleteByS32.ContainsKey(cell.S32Data))
                        {
                            layer5ToDeleteByS32[cell.S32Data] = new List<Layer5Item>();
                        }
                        layer5ToDeleteByS32[cell.S32Data].AddRange(layer5Items);
                        layer5to8Count += layer5Items.Count;
                    }
                }
            }

            if (layer1Count == 0 && layer2Count == 0 && layer3Count == 0 && layer4Count == 0 && layer5to8Count == 0)
            {
                this.toolStripStatusLabel1.Text = $"選中的 {cells.Count} 個格子內沒有可刪除的資料";
                return;
            }

            // 組合確認訊息
            var deleteParts = new List<string>();
            if (deleteLayer1 && layer1Count > 0) deleteParts.Add($"L1:{layer1Count}");
            if (deleteLayer2 && layer2Count > 0) deleteParts.Add($"L2:{layer2Count} (整個S32)");
            if (deleteLayer3 && layer3Count > 0) deleteParts.Add($"L3:{layer3Count}");
            if (deleteLayer4 && layer4Count > 0) deleteParts.Add($"L4:{layer4Count}");
            if (deleteLayer5to8 && layer5to8Count > 0) deleteParts.Add($"L5:{layer5to8Count}");

            string deleteInfo = string.Join(", ", deleteParts);

            // 確認刪除
            DialogResult result = MessageBox.Show(
                $"確定要刪除選中區域內的資料嗎？\n" +
                $"選中格子數: {cells.Count}\n" +
                $"刪除項目: {deleteInfo}\n" +
                $"影響 {affectedS32.Count} 個 S32 檔案",
                "確認批量刪除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                // 建立 Undo 記錄
                var undoAction = new UndoAction
                {
                    Description = $"批量刪除 {cells.Count} 格 ({deleteInfo})"
                };

                // 記錄 Layer4 物件到 Undo
                if (deleteLayer4)
                {
                    foreach (var kvp in objectsToDeleteByS32)
                    {
                        S32Data s32Data = kvp.Key;
                        int segStartX = s32Data.SegInfo.nLinBeginX;
                        int segStartY = s32Data.SegInfo.nLinBeginY;

                        foreach (var obj in kvp.Value)
                        {
                            undoAction.RemovedObjects.Add(new UndoObjectInfo
                            {
                                S32FilePath = s32Data.FilePath,
                                GameX = segStartX + obj.X / 2,
                                GameY = segStartY + obj.Y,
                                LocalX = obj.X,
                                LocalY = obj.Y,
                                GroupId = obj.GroupId,
                                Layer = obj.Layer,
                                IndexId = obj.IndexId,
                                TileId = obj.TileId
                            });
                        }
                    }
                }

                // 儲存 Undo 記錄
                PushUndoAction(undoAction);

                // 執行刪除
                foreach (var cell in cells)
                {
                    int layer3X = cell.LocalX / 2;

                    // 刪除 Layer1 資料（清空為預設值）
                    if (deleteLayer1)
                    {
                        int layer1X = cell.LocalX;
                        if (layer1X >= 0 && layer1X < 128)
                        {
                            cell.S32Data.Layer1[cell.LocalY, layer1X] = new TileCell { X = layer1X, Y = cell.LocalY, TileId = 0, IndexId = 0 };
                        }
                        if (layer1X + 1 >= 0 && layer1X + 1 < 128)
                        {
                            cell.S32Data.Layer1[cell.LocalY, layer1X + 1] = new TileCell { X = layer1X + 1, Y = cell.LocalY, TileId = 0, IndexId = 0 };
                        }
                    }

                    // 刪除 Layer3 資料（清空為預設值）
                    if (deleteLayer3)
                    {
                        if (layer3X >= 0 && layer3X < 64)
                        {
                            cell.S32Data.Layer3[cell.LocalY, layer3X] = new MapAttribute { Attribute1 = 0, Attribute2 = 0 };
                        }
                    }

                    cell.S32Data.IsModified = true;
                }

                // 刪除 Layer4 物件
                if (deleteLayer4)
                {
                    foreach (var kvp in objectsToDeleteByS32)
                    {
                        S32Data s32Data = kvp.Key;
                        foreach (var obj in kvp.Value)
                        {
                            s32Data.Layer4.Remove(obj);
                        }
                        s32Data.IsModified = true;
                    }
                }

                // 刪除 Layer2（整個 S32 的資料）
                int layer2Deleted = 0;
                if (deleteLayer2)
                {
                    foreach (var s32 in affectedS32)
                    {
                        layer2Deleted += s32.Layer2.Count;
                        s32.Layer2.Clear();
                        s32.IsModified = true;
                    }
                }

                // 刪除 Layer5（按格子刪除透明圖塊）
                int layer5Deleted = 0;
                if (deleteLayer5to8)
                {
                    foreach (var kvp in layer5ToDeleteByS32)
                    {
                        S32Data s32Data = kvp.Key;
                        foreach (var item in kvp.Value)
                        {
                            s32Data.Layer5.Remove(item);
                            layer5Deleted++;
                        }
                        s32Data.IsModified = true;
                    }
                }

                RenderS32Map();

                // 組合結果訊息
                var resultParts = new List<string>();
                if (deleteLayer1 && layer1Count > 0) resultParts.Add($"L1:{layer1Count}");
                if (deleteLayer2 && layer2Deleted > 0) resultParts.Add($"L2:{layer2Deleted}");
                if (deleteLayer3 && layer3Count > 0) resultParts.Add($"L3:{layer3Count}");
                if (deleteLayer4 && layer4Count > 0) resultParts.Add($"L4:{layer4Count}");
                if (deleteLayer5to8 && layer5Deleted > 0) resultParts.Add($"L5:{layer5Deleted}");

                string resultInfo = resultParts.Count > 0 ? string.Join(", ", resultParts) : "無";
                this.toolStripStatusLabel1.Text = $"已刪除 {cells.Count} 格 ({resultInfo})，影響 {affectedS32.Count} 個 S32 檔案";
            }
        }

        // 刪除指定格子的所有第四層物件
        private void DeleteAllLayer4ObjectsAtCell(int cellX, int cellY)
        {
            // 找出該格子的所有物件
            var objectsAtCell = currentS32Data.Layer4.Where(o => o.X == cellX && o.Y == cellY).ToList();

            // 如果有選擇群組，只刪除選中群組的物件
            if (isFilteringLayer4Groups && selectedLayer4Groups.Count > 0)
            {
                objectsAtCell = objectsAtCell.Where(o => selectedLayer4Groups.Contains(o.GroupId)).ToList();
            }

            if (objectsAtCell.Count == 0)
            {
                this.toolStripStatusLabel1.Text = $"格子 ({cellX},{cellY}) 沒有第四層物件";
                return;
            }

            // 確認刪除（計算遊戲座標，Layer3 尺度）
            int layer3X = cellX / 2;
            if (layer3X >= 64) layer3X = 63;
            int gameX = currentS32FileItem.SegInfo.nLinBeginX + layer3X;
            int gameY = currentS32FileItem.SegInfo.nLinBeginY + cellY;

            DialogResult result = MessageBox.Show(
                $"確定要刪除格子 ({cellX},{cellY}) 的所有第四層物件嗎？\n" +
                $"遊戲座標: ({gameX},{gameY})\n" +
                $"共有 {objectsAtCell.Count} 個物件",
                "確認刪除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                // 建立 Undo 記錄
                var undoAction = new UndoAction
                {
                    Description = $"刪除格子 ({gameX},{gameY}) 的 {objectsAtCell.Count} 個 Layer4 物件"
                };

                // 刪除所有物件
                foreach (var obj in objectsAtCell)
                {
                    // 記錄到 Undo（刪除的物件）
                    undoAction.RemovedObjects.Add(new UndoObjectInfo
                    {
                        S32FilePath = currentS32Data.FilePath,
                        GameX = gameX,
                        GameY = gameY,
                        LocalX = obj.X,
                        LocalY = obj.Y,
                        GroupId = obj.GroupId,
                        Layer = obj.Layer,
                        IndexId = obj.IndexId,
                        TileId = obj.TileId
                    });

                    currentS32Data.Layer4.Remove(obj);
                }

                // 儲存 Undo 記錄
                PushUndoAction(undoAction);

                isS32Modified = true;
                RenderS32Map();
                this.toolStripStatusLabel1.Text = $"已刪除格子 ({cellX},{cellY}) 的 {objectsAtCell.Count} 個第四層物件";
            }
        }

        // 檢查點是否在菱形內
        private bool IsPointInDiamond(Point p, Point p1, Point p2, Point p3, Point p4)
        {
            // 使用 Region 來檢測
            using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddPolygon(new Point[] { p1, p2, p3, p4 });
                using (Region region = new Region(path))
                {
                    return region.IsVisible(p);
                }
            }
        }

        // 顯示格子的四層 Tile 對話框
        private void ShowCellLayersDialog(int cellX, int cellY)
        {
            // 計算遊戲座標（Layer3 尺度）
            // cellX 是 Layer1 座標 (0-127)，需轉換為 Layer3 座標 (0-63)
            int layer3X = cellX / 2;
            if (layer3X >= 64) layer3X = 63;
            int gameX = currentS32FileItem.SegInfo.nLinBeginX + layer3X;
            int gameY = currentS32FileItem.SegInfo.nLinBeginY + cellY;

            // 創建對話框
            Form layerForm = new Form();
            layerForm.Text = $"格子詳細資訊 - 格子座標 ({cellX}, {cellY}) - 遊戲座標 ({gameX}, {gameY})";
            layerForm.Size = new Size(700, 600);
            layerForm.FormBorderStyle = FormBorderStyle.Sizable;
            layerForm.StartPosition = FormStartPosition.CenterParent;

            // 使用 TabControl 來組織不同的資訊
            TabControl tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;

            // Tab 1: 各層資料
            TabPage tabLayers = new TabPage("各層資料");
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 2;
            table.RowCount = 4;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

            // 第一層（地板）
            var layer1Panel = CreateLayerPanel(cellX, cellY, 1);
            table.Controls.Add(layer1Panel, 0, 0);

            // 第二層（目前沒有視覺化）
            var layer2Panel = CreateLayer2Panel(cellX, cellY);
            table.Controls.Add(layer2Panel, 1, 0);

            // 第三層（屬性）
            var layer3Panel = CreateLayer3Panel(cellX, cellY);
            table.Controls.Add(layer3Panel, 0, 1);

            // 第四層（物件） - 只顯示該位置的物件
            var layer4Panel = CreateLayer4Panel(cellX, cellY);
            table.Controls.Add(layer4Panel, 1, 1);

            // 第五層（透明圖塊）
            var layer5Panel = CreateLayer5Panel(cellX, cellY);
            table.Controls.Add(layer5Panel, 0, 2);

            // 第六層（使用的Til）
            var layer6Panel = CreateLayer6Panel(cellX, cellY);
            table.Controls.Add(layer6Panel, 1, 2);

            // 第七層（傳送點）
            var layer7Panel = CreateLayer7Panel(cellX, cellY);
            table.Controls.Add(layer7Panel, 0, 3);

            // 第八層（特效）
            var layer8Panel = CreateLayer8Panel(cellX, cellY);
            table.Controls.Add(layer8Panel, 1, 3);

            tabLayers.Controls.Add(table);
            tabControl.TabPages.Add(tabLayers);

            // Tab 2: 所有相關物件（包含周圍）
            TabPage tabAllObjects = new TabPage("所有相關物件");
            var allObjectsPanel = CreateAllRelatedObjectsPanel(cellX, cellY);
            tabAllObjects.Controls.Add(allObjectsPanel);
            tabControl.TabPages.Add(tabAllObjects);

            // Tab 3: 渲染資訊
            TabPage tabRenderInfo = new TabPage("渲染資訊");
            var renderInfoPanel = CreateRenderInfoPanel(cellX, cellY);
            tabRenderInfo.Controls.Add(renderInfoPanel);
            tabControl.TabPages.Add(tabRenderInfo);

            layerForm.Controls.Add(tabControl);
            layerForm.ShowDialog();
        }

        // 創建第一層面板
        private Panel CreateLayerPanel(int x, int y, int layer)
        {
            Panel panel = new Panel();
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Dock = DockStyle.Fill;

            Label title = new Label();
            title.Text = "第1層 (地板)";
            title.Font = new Font("Arial", 10, FontStyle.Bold);
            title.Dock = DockStyle.Top;
            title.Height = 25;
            title.TextAlign = ContentAlignment.MiddleCenter;
            panel.Controls.Add(title);

            PictureBox pb = new PictureBox();
            pb.Dock = DockStyle.Fill;
            pb.SizeMode = PictureBoxSizeMode.Zoom;
            pb.BackColor = Color.Black;

            var cell = currentS32Data.Layer1[y, x];
            if (cell != null && cell.TileId > 0)
            {
                pb.Image = LoadTileEnlarged(cell.TileId, cell.IndexId, 128);

                Panel bottomPanel = new Panel();
                bottomPanel.Dock = DockStyle.Bottom;
                bottomPanel.Height = 60;

                Label info = new Label();
                info.Text = $"Tile ID: {cell.TileId}\nIndex: {cell.IndexId}";
                info.Dock = DockStyle.Top;
                info.Height = 40;
                info.TextAlign = ContentAlignment.MiddleCenter;
                bottomPanel.Controls.Add(info);

                // 刪除按鈕
                Button btnDelete = new Button();
                btnDelete.Text = "刪除此 Tile";
                btnDelete.Dock = DockStyle.Bottom;
                btnDelete.Height = 25;
                btnDelete.BackColor = Color.Red;
                btnDelete.ForeColor = Color.White;
                btnDelete.Click += (s, e) =>
                {
                    if (MessageBox.Show("確定要刪除此 Tile 嗎？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        currentS32Data.Layer1[y, x] = new TileCell { X = x, Y = y, TileId = 0, IndexId = 0 };
                        isS32Modified = true;
                        RenderS32Map();
                        this.toolStripStatusLabel1.Text = $"已刪除第1層 ({x},{y}) 的 Tile";

                        // 更新當前面板顯示
                        pb.Image = null;
                        info.Text = "已刪除";
                        btnDelete.Enabled = false;
                    }
                };
                bottomPanel.Controls.Add(btnDelete);

                panel.Controls.Add(bottomPanel);
            }
            else
            {
                Label noData = new Label();
                noData.Text = "無資料";
                noData.Dock = DockStyle.Fill;
                noData.TextAlign = ContentAlignment.MiddleCenter;
                panel.Controls.Add(noData);
            }

            panel.Controls.Add(pb);
            return panel;
        }

        // 創建第二層面板
        private Panel CreateLayer2Panel(int x, int y)
        {
            Panel panel = new Panel();
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Dock = DockStyle.Fill;

            Label title = new Label();
            title.Text = "第2層 (資料)";
            title.Font = new Font("Arial", 10, FontStyle.Bold);
            title.Dock = DockStyle.Top;
            title.Height = 25;
            title.TextAlign = ContentAlignment.MiddleCenter;
            panel.Controls.Add(title);

            Label info = new Label();
            info.Text = $"共 {currentS32Data.Layer2.Count} 項資料\n(此層無對應格子資料)";
            info.Dock = DockStyle.Fill;
            info.TextAlign = ContentAlignment.MiddleCenter;
            panel.Controls.Add(info);

            return panel;
        }

        // 創建第三層面板
        private Panel CreateLayer3Panel(int x, int y)
        {
            Panel panel = new Panel();
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Dock = DockStyle.Fill;

            Label title = new Label();
            title.Text = "第3層 (屬性)";
            title.Font = new Font("Arial", 10, FontStyle.Bold);
            title.Dock = DockStyle.Top;
            title.Height = 25;
            title.TextAlign = ContentAlignment.MiddleCenter;
            panel.Controls.Add(title);

            // 注意：第3層是 64x64，第1層是 64x128
            // 所以需要將 x 座標除以 2
            int layer3X = x / 2;
            if (layer3X >= 64) layer3X = 63;

            var attr = currentS32Data.Layer3[y, layer3X];
            if (attr != null)
            {
                Label info = new Label();
                string attrText = $"第3層座標: [{layer3X}, {y}]\n\n";
                attrText += $"Attribute1 (左上邊):\n";
                attrText += $"  {attr.Attribute1} (0x{attr.Attribute1:X4})\n\n";
                attrText += $"Attribute2 (右上邊):\n";
                attrText += $"  {attr.Attribute2} (0x{attr.Attribute2:X4})\n";

                info.Text = attrText;
                info.Dock = DockStyle.Fill;
                info.TextAlign = ContentAlignment.TopCenter;
                panel.Controls.Add(info);

                // 刪除按鈕
                Button btnDelete = new Button();
                btnDelete.Text = "清除屬性";
                btnDelete.Dock = DockStyle.Bottom;
                btnDelete.Height = 25;
                btnDelete.BackColor = Color.Red;
                btnDelete.ForeColor = Color.White;
                btnDelete.Click += (s, e) =>
                {
                    if (MessageBox.Show("確定要清除此格的屬性嗎？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        currentS32Data.Layer3[y, layer3X] = new MapAttribute { Attribute1 = 0, Attribute2 = 0 };
                        isS32Modified = true;
                        RenderS32Map();
                        this.toolStripStatusLabel1.Text = $"已清除第3層 ({layer3X},{y}) 的屬性";

                        // 更新當前面板顯示
                        info.Text = "已清除屬性";
                        btnDelete.Enabled = false;
                    }
                };
                panel.Controls.Add(btnDelete);
            }
            else
            {
                Label noData = new Label();
                noData.Text = "無資料";
                noData.Dock = DockStyle.Fill;
                noData.TextAlign = ContentAlignment.MiddleCenter;
                panel.Controls.Add(noData);
            }

            return panel;
        }

        // 創建第四層面板
        private Panel CreateLayer4Panel(int x, int y)
        {
            Panel panel = new Panel();
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Dock = DockStyle.Fill;

            Label title = new Label();
            title.Text = "第4層 (物件)";
            title.Font = new Font("Arial", 10, FontStyle.Bold);
            title.Dock = DockStyle.Top;
            title.Height = 25;
            title.TextAlign = ContentAlignment.MiddleCenter;
            panel.Controls.Add(title);

            // 查找該位置的所有物件
            var objectsAtCell = currentS32Data.Layer4.Where(obj => obj.X == x && obj.Y == y).OrderBy(obj => obj.Layer).ToList();

            if (objectsAtCell.Count > 0)
            {
                FlowLayoutPanel flow = new FlowLayoutPanel();
                flow.Dock = DockStyle.Fill;
                flow.AutoScroll = true;
                flow.FlowDirection = FlowDirection.TopDown;
                flow.WrapContents = false;

                foreach (var obj in objectsAtCell)
                {
                    Panel objPanel = new Panel();
                    objPanel.Width = flow.Width - 25;
                    objPanel.Height = 210;
                    objPanel.BorderStyle = BorderStyle.FixedSingle;
                    objPanel.Margin = new Padding(5);

                    PictureBox pb = new PictureBox();
                    pb.Dock = DockStyle.Top;
                    pb.Height = 128;
                    pb.SizeMode = PictureBoxSizeMode.Zoom;
                    pb.BackColor = Color.Black;
                    pb.Image = LoadTileEnlarged(obj.TileId, obj.IndexId, 128);
                    objPanel.Controls.Add(pb);

                    Label info = new Label();
                    info.Text = $"Layer: {obj.Layer} | Group: {obj.GroupId}\nTile: {obj.TileId} | Index: {obj.IndexId}";
                    info.Dock = DockStyle.Bottom;
                    info.Height = 50;
                    info.TextAlign = ContentAlignment.MiddleCenter;
                    objPanel.Controls.Add(info);

                    // 刪除按鈕
                    Button btnDeleteObj = new Button();
                    btnDeleteObj.Text = "刪除此物件";
                    btnDeleteObj.Dock = DockStyle.Bottom;
                    btnDeleteObj.Height = 25;
                    btnDeleteObj.BackColor = Color.Red;
                    btnDeleteObj.ForeColor = Color.White;
                    var objToDelete = obj; // Capture for lambda
                    btnDeleteObj.Click += (s, e) =>
                    {
                        if (MessageBox.Show($"確定要刪除此物件嗎？\n(Group:{objToDelete.GroupId}, Layer:{objToDelete.Layer})", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            currentS32Data.Layer4.Remove(objToDelete);

                            isS32Modified = true;
                            RenderS32Map();
                            this.toolStripStatusLabel1.Text = $"已刪除第4層物件 ({x},{y})";

                            // 更新當前面板顯示
                            pb.Image = null;
                            info.Text = "已刪除";
                            btnDeleteObj.Enabled = false;
                        }
                    };
                    objPanel.Controls.Add(btnDeleteObj);

                    flow.Controls.Add(objPanel);
                }

                panel.Controls.Add(flow);
            }
            else
            {
                Label noData = new Label();
                noData.Text = "無物件";
                noData.Dock = DockStyle.Fill;
                noData.TextAlign = ContentAlignment.MiddleCenter;
                panel.Controls.Add(noData);
            }

            return panel;
        }

        // 創建第五層面板 - 可透明化的圖塊
        private Panel CreateLayer5Panel(int x, int y)
        {
            Panel panel = new Panel();
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Dock = DockStyle.Fill;

            Label title = new Label();
            title.Text = "第5層 (透明圖塊)";
            title.Font = new Font("Arial", 10, FontStyle.Bold);
            title.Dock = DockStyle.Top;
            title.Height = 25;
            title.TextAlign = ContentAlignment.MiddleCenter;
            panel.Controls.Add(title);

            if (currentS32Data.Layer5.Count > 0)
            {
                Label countLabel = new Label();
                countLabel.Text = $"數量: {currentS32Data.Layer5.Count}";
                countLabel.Dock = DockStyle.Top;
                countLabel.Height = 20;
                countLabel.TextAlign = ContentAlignment.MiddleCenter;
                panel.Controls.Add(countLabel);

                ListView listView = new ListView();
                listView.Dock = DockStyle.Fill;
                listView.View = View.Details;
                listView.FullRowSelect = true;
                listView.GridLines = true;
                listView.Font = new Font("Consolas", 9, FontStyle.Regular);

                listView.Columns.Add("索引", 40);
                listView.Columns.Add("X", 40);
                listView.Columns.Add("Y", 40);
                listView.Columns.Add("R", 40);
                listView.Columns.Add("G", 40);
                listView.Columns.Add("B", 40);

                for (int i = 0; i < currentS32Data.Layer5.Count; i++)
                {
                    var item5 = currentS32Data.Layer5[i];
                    var lvItem = new ListViewItem(i.ToString());
                    lvItem.SubItems.Add(item5.X.ToString());
                    lvItem.SubItems.Add(item5.Y.ToString());
                    lvItem.SubItems.Add(item5.R.ToString());
                    lvItem.SubItems.Add(item5.G.ToString());
                    lvItem.SubItems.Add(item5.B.ToString());
                    listView.Items.Add(lvItem);
                }

                panel.Controls.Add(listView);
            }
            else
            {
                Label info = new Label();
                info.Text = "無資料";
                info.Dock = DockStyle.Fill;
                info.TextAlign = ContentAlignment.MiddleCenter;
                panel.Controls.Add(info);
            }

            return panel;
        }

        // 創建第六層面板 - 使用的 til
        private Panel CreateLayer6Panel(int x, int y)
        {
            Panel panel = new Panel();
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Dock = DockStyle.Fill;

            Label title = new Label();
            title.Text = "第6層 (使用的Til)";
            title.Font = new Font("Arial", 10, FontStyle.Bold);
            title.Dock = DockStyle.Top;
            title.Height = 25;
            title.TextAlign = ContentAlignment.MiddleCenter;
            panel.Controls.Add(title);

            if (currentS32Data.Layer6.Count > 0)
            {
                Label countLabel = new Label();
                countLabel.Text = $"數量: {currentS32Data.Layer6.Count}";
                countLabel.Dock = DockStyle.Top;
                countLabel.Height = 20;
                countLabel.TextAlign = ContentAlignment.MiddleCenter;
                panel.Controls.Add(countLabel);

                ListView listView = new ListView();
                listView.Dock = DockStyle.Fill;
                listView.View = View.Details;
                listView.FullRowSelect = true;
                listView.GridLines = true;
                listView.Font = new Font("Consolas", 9, FontStyle.Regular);

                listView.Columns.Add("索引", 50);
                listView.Columns.Add("TilId", 80);
                listView.Columns.Add("十六進位", 80);

                for (int i = 0; i < currentS32Data.Layer6.Count; i++)
                {
                    var lvItem = new ListViewItem(i.ToString());
                    lvItem.SubItems.Add(currentS32Data.Layer6[i].ToString());
                    lvItem.SubItems.Add($"0x{currentS32Data.Layer6[i]:X8}");
                    listView.Items.Add(lvItem);
                }

                panel.Controls.Add(listView);
            }
            else
            {
                Label info = new Label();
                info.Text = "無資料";
                info.Dock = DockStyle.Fill;
                info.TextAlign = ContentAlignment.MiddleCenter;
                panel.Controls.Add(info);
            }

            return panel;
        }

        // 創建第七層面板 - 傳送點、入口點
        private Panel CreateLayer7Panel(int x, int y)
        {
            Panel panel = new Panel();
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Dock = DockStyle.Fill;

            Label title = new Label();
            title.Text = "第7層 (傳送點)";
            title.Font = new Font("Arial", 10, FontStyle.Bold);
            title.Dock = DockStyle.Top;
            title.Height = 25;
            title.TextAlign = ContentAlignment.MiddleCenter;
            panel.Controls.Add(title);

            if (currentS32Data.Layer7.Count > 0)
            {
                Label countLabel = new Label();
                countLabel.Text = $"數量: {currentS32Data.Layer7.Count}";
                countLabel.Dock = DockStyle.Top;
                countLabel.Height = 20;
                countLabel.TextAlign = ContentAlignment.MiddleCenter;
                panel.Controls.Add(countLabel);

                ListView listView = new ListView();
                listView.Dock = DockStyle.Fill;
                listView.View = View.Details;
                listView.FullRowSelect = true;
                listView.GridLines = true;
                listView.Font = new Font("Consolas", 9, FontStyle.Regular);

                listView.Columns.Add("名稱", 80);
                listView.Columns.Add("X", 30);
                listView.Columns.Add("Y", 30);
                listView.Columns.Add("目標地圖", 60);
                listView.Columns.Add("傳送點ID", 60);

                for (int i = 0; i < currentS32Data.Layer7.Count; i++)
                {
                    var item7 = currentS32Data.Layer7[i];
                    var lvItem = new ListViewItem(item7.Name);
                    lvItem.SubItems.Add(item7.X.ToString());
                    lvItem.SubItems.Add(item7.Y.ToString());
                    lvItem.SubItems.Add(item7.TargetMapId.ToString());
                    lvItem.SubItems.Add(item7.PortalId.ToString());
                    listView.Items.Add(lvItem);
                }

                panel.Controls.Add(listView);
            }
            else
            {
                Label info = new Label();
                info.Text = "無資料";
                info.Dock = DockStyle.Fill;
                info.TextAlign = ContentAlignment.MiddleCenter;
                panel.Controls.Add(info);
            }

            return panel;
        }

        // 創建第八層面板 - 特效、裝飾品
        private Panel CreateLayer8Panel(int x, int y)
        {
            Panel panel = new Panel();
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Dock = DockStyle.Fill;

            Label title = new Label();
            title.Text = "第8層 (特效)";
            title.Font = new Font("Arial", 10, FontStyle.Bold);
            title.Dock = DockStyle.Top;
            title.Height = 25;
            title.TextAlign = ContentAlignment.MiddleCenter;
            panel.Controls.Add(title);

            if (currentS32Data.Layer8.Count > 0)
            {
                Label countLabel = new Label();
                countLabel.Text = $"數量: {currentS32Data.Layer8.Count}";
                countLabel.Dock = DockStyle.Top;
                countLabel.Height = 20;
                countLabel.TextAlign = ContentAlignment.MiddleCenter;
                panel.Controls.Add(countLabel);

                ListView listView = new ListView();
                listView.Dock = DockStyle.Fill;
                listView.View = View.Details;
                listView.FullRowSelect = true;
                listView.GridLines = true;
                listView.Font = new Font("Consolas", 9, FontStyle.Regular);

                listView.Columns.Add("SprId", 60);
                listView.Columns.Add("X", 50);
                listView.Columns.Add("Y", 50);
                listView.Columns.Add("Unknown", 80);

                for (int i = 0; i < currentS32Data.Layer8.Count; i++)
                {
                    var item8 = currentS32Data.Layer8[i];
                    var lvItem = new ListViewItem(item8.SprId.ToString());
                    lvItem.SubItems.Add(item8.X.ToString());
                    lvItem.SubItems.Add(item8.Y.ToString());
                    lvItem.SubItems.Add($"0x{item8.Unknown:X8}");
                    listView.Items.Add(lvItem);
                }

                panel.Controls.Add(listView);
            }
            else
            {
                Label info = new Label();
                info.Text = "無資料";
                info.Dock = DockStyle.Fill;
                info.TextAlign = ContentAlignment.MiddleCenter;
                panel.Controls.Add(info);
            }

            return panel;
        }

        // Tile 雙擊事件 - 顯示預覽+詳細資料
        private void lvTiles_DoubleClick(object sender, EventArgs e)
        {
            if (lvTiles.SelectedItems.Count == 0)
                return;

            var selectedItem = lvTiles.SelectedItems[0];
            var tileInfo = selectedItem.Tag as TileInfo;
            if (tileInfo == null)
                return;

            // 顯示詳細資料視窗（含預覽）
            ShowTileInfoWithPreview(tileInfo);
        }

        // 顯示 Tile 預覽+詳細資料整合視窗
        private void ShowTileInfoWithPreview(TileInfo tileInfo)
        {
            // 收集所有使用此 TileId 的位置
            List<(int globalX, int globalY, string layer, int groupId, S32Data s32, int l4X, int l4Y)> locations =
                new List<(int, int, string, int, S32Data, int, int)>();

            foreach (var s32Data in allS32DataDict.Values)
            {
                int segStartX = s32Data.SegInfo.nLinBeginX;
                int segStartY = s32Data.SegInfo.nLinBeginY;

                // Layer1
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        var cell = s32Data.Layer1[y, x];
                        if (cell != null && cell.TileId == tileInfo.TileId)
                        {
                            int layer3X = x / 2;
                            int globalX = segStartX + layer3X;
                            int globalY = segStartY + y;
                            locations.Add((globalX, globalY, "L1", 0, s32Data, -1, -1));
                        }
                    }
                }

                // Layer4
                foreach (var obj in s32Data.Layer4)
                {
                    if (obj.TileId == tileInfo.TileId)
                    {
                        int layer3X = obj.X / 2;
                        int globalX = segStartX + layer3X;
                        int globalY = segStartY + obj.Y;
                        locations.Add((globalX, globalY, "L4", obj.GroupId, s32Data, obj.X, obj.Y));
                    }
                }
            }

            // 建立視窗
            Form infoForm = new Form
            {
                Text = $"Tile {tileInfo.TileId} 詳細資訊",
                Size = new Size(680, 480),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                MinimizeBox = false
            };

            // 左側預覽區
            Panel previewPanel = new Panel
            {
                Location = new Point(10, 10),
                Size = new Size(150, 180),
                BorderStyle = BorderStyle.FixedSingle
            };

            Bitmap enlargedTile = LoadTileEnlarged(tileInfo.TileId, tileInfo.IndexId, 144);
            PictureBox pbPreview = new PictureBox
            {
                Image = enlargedTile,
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            previewPanel.Controls.Add(pbPreview);

            // 基本資訊
            Label lblBasicInfo = new Label
            {
                Text = $"TileId: {tileInfo.TileId}\nIndexId: {tileInfo.IndexId}\n使用次數: {tileInfo.UsageCount}\n總共 {locations.Count} 個位置",
                Location = new Point(10, 195),
                Size = new Size(150, 80),
                Font = new Font(Font.FontFamily, 9, FontStyle.Regular)
            };

            // 右側區域 - 跳轉座標
            Label lblJump = new Label
            {
                Text = "跳轉座標:",
                Location = new Point(170, 10),
                Size = new Size(70, 23),
                TextAlign = ContentAlignment.MiddleLeft
            };

            TextBox txtJumpCoord = new TextBox
            {
                Location = new Point(240, 10),
                Size = new Size(120, 23),
                PlaceholderText = "輸入 X,Y"
            };

            Button btnJump = new Button
            {
                Text = "跳轉",
                Location = new Point(365, 9),
                Size = new Size(50, 25)
            };

            // 座標列表
            ListView lvLocations = new ListView
            {
                Location = new Point(170, 40),
                Size = new Size(480, 340),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            lvLocations.Columns.Add("X", 55);
            lvLocations.Columns.Add("Y", 55);
            lvLocations.Columns.Add("圖層", 40);
            lvLocations.Columns.Add("GroupId", 55);
            lvLocations.Columns.Add("L4座標", 70);
            lvLocations.Columns.Add("S32檔案", 180);

            // 右鍵選單 - 複製整行
            ContextMenuStrip lvContextMenu = new ContextMenuStrip();
            ToolStripMenuItem copyRowItem = new ToolStripMenuItem("複製整行");
            copyRowItem.Click += (s, ev) =>
            {
                if (lvLocations.SelectedItems.Count > 0)
                {
                    var item = lvLocations.SelectedItems[0];
                    string rowText = $"{item.SubItems[0].Text},{item.SubItems[1].Text},{item.SubItems[2].Text},{item.SubItems[3].Text},{item.SubItems[4].Text},{item.SubItems[5].Text}";
                    Clipboard.SetText(rowText);
                    this.toolStripStatusLabel1.Text = "已複製整行";
                }
            };
            lvContextMenu.Items.Add(copyRowItem);
            lvLocations.ContextMenuStrip = lvContextMenu;

            // 填充列表
            foreach (var loc in locations.OrderBy(l => l.globalX).ThenBy(l => l.globalY))
            {
                string s32FileName = Path.GetFileName(loc.s32.FilePath);
                var item = new ListViewItem(loc.globalX.ToString());
                item.SubItems.Add(loc.globalY.ToString());
                item.SubItems.Add(loc.layer);
                item.SubItems.Add(loc.groupId > 0 ? loc.groupId.ToString() : "-");
                item.SubItems.Add(loc.l4X >= 0 ? $"({loc.l4X},{loc.l4Y})" : "-");
                item.SubItems.Add(s32FileName);
                item.Tag = loc;
                lvLocations.Items.Add(item);
            }

            // 底部按鈕
            Button btnCopyAll = new Button
            {
                Text = "複製全部座標",
                Location = new Point(170, 390),
                Size = new Size(100, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            Button btnCopySelected = new Button
            {
                Text = "複製選中",
                Location = new Point(275, 390),
                Size = new Size(80, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            Button btnClose = new Button
            {
                Text = "關閉",
                Location = new Point(570, 390),
                Size = new Size(80, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            // 事件處理
            btnJump.Click += (s, ev) =>
            {
                string input = txtJumpCoord.Text.Trim();
                if (TryParseCoordinate(input, out int x, out int y))
                {
                    JumpToGameCoordinate(x, y);
                    this.toolStripStatusLabel1.Text = $"已跳轉到座標 ({x}, {y})";
                }
                else
                {
                    MessageBox.Show("請輸入正確的座標格式，例如: 32800,32700", "格式錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            txtJumpCoord.KeyDown += (s, ev) =>
            {
                if (ev.KeyCode == Keys.Enter)
                {
                    btnJump.PerformClick();
                    ev.Handled = true;
                    ev.SuppressKeyPress = true;
                }
            };

            // 單擊列表項目跳轉並高亮
            lvLocations.SelectedIndexChanged += (s, ev) =>
            {
                if (lvLocations.SelectedItems.Count > 0)
                {
                    var loc = ((int globalX, int globalY, string layer, int groupId, S32Data s32, int l4X, int l4Y))lvLocations.SelectedItems[0].Tag;
                    JumpToGameCoordinate(loc.globalX, loc.globalY);
                    string l4Info = loc.l4X >= 0 ? $" L4座標:({loc.l4X},{loc.l4Y})" : "";
                    this.toolStripStatusLabel1.Text = $"已跳轉到座標 ({loc.globalX}, {loc.globalY}) - {loc.layer}" +
                        (loc.groupId > 0 ? $" GroupId:{loc.groupId}" : "") + l4Info;
                }
            };

            btnCopyAll.Click += (s, ev) =>
            {
                var coords = locations.Select(l => $"{l.globalX},{l.globalY}");
                string text = string.Join("\n", coords);
                Clipboard.SetText(text);
                MessageBox.Show($"已複製 {locations.Count} 個座標到剪貼簿", "複製成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            btnCopySelected.Click += (s, ev) =>
            {
                if (lvLocations.SelectedItems.Count == 0)
                {
                    MessageBox.Show("請先選取要複製的項目", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                var selectedRows = new List<string>();
                foreach (ListViewItem item in lvLocations.SelectedItems)
                {
                    string rowText = $"{item.SubItems[0].Text},{item.SubItems[1].Text},{item.SubItems[2].Text},{item.SubItems[3].Text},{item.SubItems[4].Text},{item.SubItems[5].Text}";
                    selectedRows.Add(rowText);
                }
                Clipboard.SetText(string.Join("\n", selectedRows));
                MessageBox.Show($"已複製 {selectedRows.Count} 筆資料到剪貼簿", "複製成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            btnClose.Click += (s, ev) => infoForm.Close();

            infoForm.FormClosed += (s, ev) =>
            {
                enlargedTile?.Dispose();
            };

            // 加入控制項
            infoForm.Controls.Add(previewPanel);
            infoForm.Controls.Add(lblBasicInfo);
            infoForm.Controls.Add(lblJump);
            infoForm.Controls.Add(txtJumpCoord);
            infoForm.Controls.Add(btnJump);
            infoForm.Controls.Add(lvLocations);
            infoForm.Controls.Add(btnCopyAll);
            infoForm.Controls.Add(btnCopySelected);
            infoForm.Controls.Add(btnClose);

            // 使用 Show 而非 ShowDialog，讓主視窗可以即時更新
            infoForm.Show(this);
        }

        // Tile 右鍵選單
        private void lvTiles_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (lvTiles.SelectedItems.Count == 0)
                return;

            var selectedItem = lvTiles.SelectedItems[0];
            var tileInfo = selectedItem.Tag as TileInfo;
            if (tileInfo == null)
                return;

            // 計算使用此 TileId 的 Layer4 物件數量
            int layer4Count = 0;
            foreach (var s32Data in allS32DataDict.Values)
            {
                layer4Count += s32Data.Layer4.Count(o => o.TileId == tileInfo.TileId);
            }

            // 建立右鍵選單
            ContextMenuStrip menu = new ContextMenuStrip();

            // 刪除所有使用此 TileId 的 Layer4 物件
            ToolStripMenuItem deleteLayer4Item = new ToolStripMenuItem($"刪除所有 Layer4 物件 ({layer4Count} 個)");
            deleteLayer4Item.Enabled = layer4Count > 0;
            deleteLayer4Item.Click += (s, ev) =>
            {
                DeleteAllLayer4ByTileId(tileInfo.TileId);
            };

            // 高亮顯示使用此 TileId 的物件
            ToolStripMenuItem highlightItem = new ToolStripMenuItem("在地圖上高亮顯示");
            highlightItem.Click += (s, ev) =>
            {
                HighlightTileOnMap(tileInfo.TileId);
            };

            // 查看 Tile 詳細資訊
            ToolStripMenuItem infoItem = new ToolStripMenuItem("查看詳細資訊");
            infoItem.Click += (s, ev) =>
            {
                ShowTileInfoWithPreview(tileInfo);
            };

            menu.Items.Add(infoItem);
            menu.Items.Add(highlightItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(deleteLayer4Item);

            menu.Show(lvTiles, e.Location);
        }

        // 刪除所有使用指定 TileId 的 Layer4 物件
        private void DeleteAllLayer4ByTileId(int tileId)
        {
            // 計算要刪除的數量
            int totalCount = 0;
            foreach (var s32Data in allS32DataDict.Values)
            {
                totalCount += s32Data.Layer4.Count(o => o.TileId == tileId);
            }

            if (totalCount == 0)
            {
                MessageBox.Show($"沒有找到使用 TileId {tileId} 的 Layer4 物件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 確認刪除
            DialogResult result = MessageBox.Show(
                $"確定要刪除所有使用 TileId {tileId} 的 Layer4 物件嗎？\n" +
                $"這將移除 {totalCount} 個物件。",
                "確認刪除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            // 執行刪除
            int deletedCount = 0;
            foreach (var s32Data in allS32DataDict.Values)
            {
                int beforeCount = s32Data.Layer4.Count;
                s32Data.Layer4.RemoveAll(o => o.TileId == tileId);
                int removed = beforeCount - s32Data.Layer4.Count;
                if (removed > 0)
                {
                    deletedCount += removed;
                    s32Data.IsModified = true;
                }
            }

            // 重新渲染
            RenderS32Map();

            // 更新 Tile 清單和群組縮圖
            cachedAggregatedTiles.Clear();
            UpdateTileList(txtTileSearch.Text);
            UpdateGroupThumbnailsList();

            this.toolStripStatusLabel1.Text = $"已刪除 TileId {tileId} 的所有 Layer4 物件，共 {deletedCount} 個";
        }

        // 在地圖上高亮顯示使用指定 TileId 的物件
        private void HighlightTileOnMap(int tileId)
        {
            // 找到第一個使用此 TileId 的物件並跳轉
            foreach (var s32Data in allS32DataDict.Values)
            {
                var obj = s32Data.Layer4.FirstOrDefault(o => o.TileId == tileId);
                if (obj != null)
                {
                    // 計算全域座標並跳轉
                    int globalX = s32Data.SegInfo.nLinBeginX * 2 + obj.X;
                    int globalY = s32Data.SegInfo.nLinBeginY + obj.Y;

                    // 跳轉到該位置
                    JumpToGameCoordinate(globalX, globalY);

                    this.toolStripStatusLabel1.Text = $"跳轉到 TileId {tileId} 的物件位置 ({globalX}, {globalY})";
                    return;
                }
            }

            MessageBox.Show($"沒有找到使用 TileId {tileId} 的 Layer4 物件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // 顯示 Tile 詳細資訊（含座標列表和跳轉功能）
        private void ShowTileInfo(TileInfo tileInfo)
        {
            // 收集所有使用此 TileId 的位置 (增加 l4X, l4Y 儲存 Layer4 原始座標)
            List<(int globalX, int globalY, string layer, int groupId, S32Data s32, int l4X, int l4Y)> locations =
                new List<(int, int, string, int, S32Data, int, int)>();

            foreach (var s32Data in allS32DataDict.Values)
            {
                int segStartX = s32Data.SegInfo.nLinBeginX;
                int segStartY = s32Data.SegInfo.nLinBeginY;

                // Layer1 (64x128，但遊戲座標用 Layer3 的 64x64)
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        var cell = s32Data.Layer1[y, x];
                        if (cell != null && cell.TileId == tileInfo.TileId)
                        {
                            // Layer1 的 x 要除以 2 來對應 Layer3 座標
                            int layer3X = x / 2;
                            int globalX = segStartX + layer3X;
                            int globalY = segStartY + y;
                            locations.Add((globalX, globalY, "L1", 0, s32Data, -1, -1));
                        }
                    }
                }

                // Layer4 (obj.X 是 Layer1 座標 0-127，obj.Y 是 Layer3 座標 0-63)
                foreach (var obj in s32Data.Layer4)
                {
                    if (obj.TileId == tileInfo.TileId)
                    {
                        // Layer4 的 X 也要除以 2 來對應 Layer3 座標
                        int layer3X = obj.X / 2;
                        int globalX = segStartX + layer3X;
                        int globalY = segStartY + obj.Y;
                        locations.Add((globalX, globalY, "L4", obj.GroupId, s32Data, obj.X, obj.Y));
                    }
                }
            }

            // 建立詳細資訊視窗
            Form infoForm = new Form
            {
                Text = $"Tile {tileInfo.TileId} 詳細資訊",
                Size = new Size(500, 450),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                MinimizeBox = false
            };

            // 基本資訊標籤
            Label lblBasicInfo = new Label
            {
                Text = $"TileId: {tileInfo.TileId}  |  IndexId: {tileInfo.IndexId}  |  總共 {locations.Count} 個位置",
                Location = new Point(10, 10),
                Size = new Size(460, 20),
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold)
            };

            // 座標跳轉輸入框
            Label lblJump = new Label
            {
                Text = "跳轉座標:",
                Location = new Point(10, 38),
                Size = new Size(70, 23),
                TextAlign = ContentAlignment.MiddleLeft
            };

            TextBox txtJumpCoord = new TextBox
            {
                Location = new Point(80, 38),
                Size = new Size(150, 23),
                PlaceholderText = "輸入 X,Y 座標"
            };

            Button btnJump = new Button
            {
                Text = "跳轉",
                Location = new Point(235, 37),
                Size = new Size(60, 25)
            };

            // 座標列表
            ListView lvLocations = new ListView
            {
                Location = new Point(10, 70),
                Size = new Size(460, 280),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            lvLocations.Columns.Add("X", 55);
            lvLocations.Columns.Add("Y", 55);
            lvLocations.Columns.Add("圖層", 40);
            lvLocations.Columns.Add("GroupId", 55);
            lvLocations.Columns.Add("L4座標", 70);
            lvLocations.Columns.Add("S32檔案", 130);

            // 右鍵選單 - 複製整行
            ContextMenuStrip lvContextMenu = new ContextMenuStrip();
            ToolStripMenuItem copyRowItem = new ToolStripMenuItem("複製整行");
            copyRowItem.Click += (s, ev) =>
            {
                if (lvLocations.SelectedItems.Count > 0)
                {
                    var item = lvLocations.SelectedItems[0];
                    string rowText = $"{item.SubItems[0].Text},{item.SubItems[1].Text},{item.SubItems[2].Text},{item.SubItems[3].Text},{item.SubItems[4].Text},{item.SubItems[5].Text}";
                    Clipboard.SetText(rowText);
                    this.toolStripStatusLabel1.Text = "已複製整行";
                }
            };
            lvContextMenu.Items.Add(copyRowItem);
            lvLocations.ContextMenuStrip = lvContextMenu;

            // 填充座標列表
            foreach (var loc in locations.OrderBy(l => l.globalX).ThenBy(l => l.globalY))
            {
                string s32FileName = Path.GetFileName(loc.s32.FilePath);
                var item = new ListViewItem(loc.globalX.ToString());
                item.SubItems.Add(loc.globalY.ToString());
                item.SubItems.Add(loc.layer);
                item.SubItems.Add(loc.groupId > 0 ? loc.groupId.ToString() : "-");
                item.SubItems.Add(loc.l4X >= 0 ? $"({loc.l4X},{loc.l4Y})" : "-");
                item.SubItems.Add(s32FileName);
                item.Tag = loc;
                lvLocations.Items.Add(item);
            }

            // 複製按鈕
            Button btnCopyAll = new Button
            {
                Text = "複製全部座標",
                Location = new Point(10, 360),
                Size = new Size(100, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            Button btnCopySelected = new Button
            {
                Text = "複製選中",
                Location = new Point(115, 360),
                Size = new Size(80, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            Button btnClose = new Button
            {
                Text = "關閉",
                Location = new Point(390, 360),
                Size = new Size(80, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            // 事件處理
            btnJump.Click += (s, ev) =>
            {
                string input = txtJumpCoord.Text.Trim();
                if (TryParseCoordinate(input, out int x, out int y))
                {
                    JumpToGameCoordinate(x, y);
                    this.toolStripStatusLabel1.Text = $"已跳轉到座標 ({x}, {y})";
                }
                else
                {
                    MessageBox.Show("請輸入正確的座標格式，例如: 32800,32700", "格式錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            txtJumpCoord.KeyDown += (s, ev) =>
            {
                if (ev.KeyCode == Keys.Enter)
                {
                    btnJump.PerformClick();
                    ev.Handled = true;
                    ev.SuppressKeyPress = true;
                }
            };

            // 單擊列表項目跳轉並高亮
            lvLocations.SelectedIndexChanged += (s, ev) =>
            {
                if (lvLocations.SelectedItems.Count > 0)
                {
                    var loc = ((int globalX, int globalY, string layer, int groupId, S32Data s32, int l4X, int l4Y))lvLocations.SelectedItems[0].Tag;
                    JumpToGameCoordinate(loc.globalX, loc.globalY);
                    string l4Info = loc.l4X >= 0 ? $" L4座標:({loc.l4X},{loc.l4Y})" : "";
                    this.toolStripStatusLabel1.Text = $"已跳轉到座標 ({loc.globalX}, {loc.globalY}) - {loc.layer}" +
                        (loc.groupId > 0 ? $" GroupId:{loc.groupId}" : "") + l4Info;
                }
            };

            btnCopyAll.Click += (s, ev) =>
            {
                var coords = locations.Select(l => $"{l.globalX},{l.globalY}");
                string text = string.Join("\n", coords);
                Clipboard.SetText(text);
                MessageBox.Show($"已複製 {locations.Count} 個座標到剪貼簿", "複製成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            btnCopySelected.Click += (s, ev) =>
            {
                if (lvLocations.SelectedItems.Count == 0)
                {
                    MessageBox.Show("請先選取要複製的座標", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                var selectedCoords = new List<string>();
                foreach (ListViewItem item in lvLocations.SelectedItems)
                {
                    selectedCoords.Add(item.SubItems[4].Text);
                }
                Clipboard.SetText(string.Join("\n", selectedCoords));
                MessageBox.Show($"已複製 {selectedCoords.Count} 個座標到剪貼簿", "複製成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            btnClose.Click += (s, ev) => infoForm.Close();

            // 加入控制項
            infoForm.Controls.Add(lblBasicInfo);
            infoForm.Controls.Add(lblJump);
            infoForm.Controls.Add(txtJumpCoord);
            infoForm.Controls.Add(btnJump);
            infoForm.Controls.Add(lvLocations);
            infoForm.Controls.Add(btnCopyAll);
            infoForm.Controls.Add(btnCopySelected);
            infoForm.Controls.Add(btnClose);

            infoForm.ShowDialog(this);
        }

        // 解析座標字串 (支援 "X,Y" 或 "X Y" 格式)
        private bool TryParseCoordinate(string input, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // 支援逗號或空格分隔
            string[] parts = input.Split(new char[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 &&
                int.TryParse(parts[0].Trim(), out x) &&
                int.TryParse(parts[1].Trim(), out y))
            {
                return true;
            }
            return false;
        }

        // 狀態列座標跳轉按鈕點擊事件
        private void toolStripJumpButton_Click(object sender, EventArgs e)
        {
            PerformCoordinateJump();
        }

        // 狀態列座標輸入框按鍵事件
        private void toolStripJumpTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                PerformCoordinateJump();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        // 複製移動指令按鈕點擊事件
        private void toolStripCopyMoveCmd_Click(object sender, EventArgs e)
        {
            if (selectedGameX >= 0 && selectedGameY >= 0 && !string.IsNullOrEmpty(currentMapId))
            {
                string moveCmd = $"移動 {selectedGameX} {selectedGameY} {currentMapId}";
                Clipboard.SetText(moveCmd);
                this.toolStripStatusLabel1.Text = $"已複製: {moveCmd}";
            }
        }

        // 執行座標跳轉
        private void PerformCoordinateJump()
        {
            string input = toolStripJumpTextBox.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                this.toolStripStatusLabel1.Text = "請輸入座標，格式: X,Y";
                return;
            }

            if (TryParseCoordinate(input, out int x, out int y))
            {
                JumpToGameCoordinate(x, y);
                this.toolStripStatusLabel1.Text = $"已跳轉到座標 ({x}, {y})";
            }
            else
            {
                this.toolStripStatusLabel1.Text = "座標格式錯誤，請使用格式: X,Y (例如: 32800,32700)";
            }
        }

        // 跳轉到指定的遊戲座標 (Layer3 座標系：64x64)
        private void JumpToGameCoordinate(int globalX, int globalY)
        {
            if (!Share.MapDataList.ContainsKey(currentMapId))
                return;

            Struct.L1Map currentMap = Share.MapDataList[currentMapId];

            // 找到包含此座標的 S32 (使用 Layer3 座標系：64x64)
            foreach (var s32Data in allS32DataDict.Values)
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

                    int screenX = mx + localBaseX + localX * 24 + localY * 24;
                    int screenY = my + localBaseY + localY * 12;

                    // 捲動到該位置
                    int scrollX = screenX - s32MapPanel.Width / 2;
                    int scrollY = screenY - s32MapPanel.Height / 2;

                    scrollX = Math.Max(0, Math.Min(scrollX, s32PictureBox.Width - s32MapPanel.Width));
                    scrollY = Math.Max(0, Math.Min(scrollY, s32PictureBox.Height - s32MapPanel.Height));

                    s32MapPanel.AutoScrollPosition = new Point(scrollX, scrollY);

                    // 設定高亮 (使用 Layer1 座標)
                    highlightedS32Data = s32Data;
                    highlightedCellX = localX;
                    highlightedCellY = localY;

                    s32PictureBox.Invalidate();
                    UpdateMiniMap();
                    return;
                }
            }
        }

        // 載入放大的 Tile
        private Bitmap LoadTileEnlarged(int tileId, int indexId, int size)
        {
            try
            {
                string key = $"{tileId}.til";
                byte[] data = L1PakReader.UnPack("Tile", key);
                if (data == null) return CreatePlaceholderThumbnail(tileId);

                var tilArray = L1Til.Parse(data);
                if (indexId >= tilArray.Count) return CreatePlaceholderThumbnail(tileId);

                // 繪製實際的 tile 圖片（放大版本）
                byte[] tilData = tilArray[indexId];
                return RenderTileEnlarged(tilData, tileId, size);
            }
            catch
            {
                return CreatePlaceholderThumbnail(tileId);
            }
        }

        // 創建所有相關物件面板（包含周圍物件）
        private Panel CreateAllRelatedObjectsPanel(int cellX, int cellY)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.AutoScroll = true;

            // 找出所有可能影響該格子顯示的物件
            // 包含：該格子的物件 + 周圍格子的大型物件
            List<ObjectTile> relatedObjects = new List<ObjectTile>();

            // 搜索範圍：當前格子 ± 5 格（大型物件可能很大）
            int searchRange = 5;
            for (int dy = -searchRange; dy <= searchRange; dy++)
            {
                for (int dx = -searchRange; dx <= searchRange; dx++)
                {
                    int checkX = cellX + dx;
                    int checkY = cellY + dy;

                    // 在範圍內
                    if (checkX >= 0 && checkX < 128 && checkY >= 0 && checkY < 64)
                    {
                        var objectsAtPos = currentS32Data.Layer4.Where(o => o.X == checkX && o.Y == checkY).ToList();
                        foreach (var obj in objectsAtPos)
                        {
                            // 計算物件與目標格子的距離
                            int distance = Math.Abs(dx) + Math.Abs(dy);
                            relatedObjects.Add(obj);
                        }
                    }
                }
            }

            // 按距離和層次排序
            relatedObjects = relatedObjects
                .OrderBy(o => Math.Abs(o.X - cellX) + Math.Abs(o.Y - cellY))
                .ThenBy(o => o.Layer)
                .ToList();

            if (relatedObjects.Count > 0)
            {
                FlowLayoutPanel flow = new FlowLayoutPanel();
                flow.Dock = DockStyle.Fill;
                flow.AutoScroll = true;
                flow.FlowDirection = FlowDirection.TopDown;
                flow.WrapContents = false;

                Label header = new Label();
                header.Text = $"找到 {relatedObjects.Count} 個相關物件";
                header.Font = new Font("Arial", 10, FontStyle.Bold);
                header.Height = 30;
                header.TextAlign = ContentAlignment.MiddleLeft;
                flow.Controls.Add(header);

                foreach (var obj in relatedObjects)
                {
                    Panel objPanel = new Panel();
                    objPanel.Width = flow.Width - 25;
                    objPanel.Height = 240;
                    objPanel.BorderStyle = BorderStyle.FixedSingle;
                    objPanel.Margin = new Padding(5);
                    objPanel.BackColor = (obj.X == cellX && obj.Y == cellY) ? Color.LightYellow : Color.White;

                    // 位置標籤
                    Label posLabel = new Label();
                    int distance = Math.Abs(obj.X - cellX) + Math.Abs(obj.Y - cellY);
                    posLabel.Text = distance == 0
                        ? $"[此格子] 位置: ({obj.X},{obj.Y})"
                        : $"[距離{distance}] 位置: ({obj.X},{obj.Y})";
                    posLabel.Dock = DockStyle.Top;
                    posLabel.Height = 20;
                    posLabel.BackColor = distance == 0 ? Color.Yellow : Color.LightGray;
                    posLabel.TextAlign = ContentAlignment.MiddleCenter;
                    posLabel.Font = new Font("Arial", 8, FontStyle.Bold);
                    objPanel.Controls.Add(posLabel);

                    PictureBox pb = new PictureBox();
                    pb.Dock = DockStyle.Top;
                    pb.Height = 128;
                    pb.SizeMode = PictureBoxSizeMode.Zoom;
                    pb.BackColor = Color.Black;
                    pb.Image = LoadTileEnlarged(obj.TileId, obj.IndexId, 128);
                    objPanel.Controls.Add(pb);

                    Label info = new Label();
                    info.Text = $"Tile ID: {obj.TileId} | Index: {obj.IndexId}\n" +
                               $"Layer: {obj.Layer} | Group: {obj.GroupId}\n" +
                               $"位置: ({obj.X},{obj.Y})";
                    info.Dock = DockStyle.Bottom;
                    info.Height = 60;
                    info.TextAlign = ContentAlignment.MiddleCenter;
                    objPanel.Controls.Add(info);

                    // 刪除按鈕
                    Button btnDeleteObj = new Button();
                    btnDeleteObj.Text = "刪除此物件";
                    btnDeleteObj.Dock = DockStyle.Bottom;
                    btnDeleteObj.Height = 25;
                    btnDeleteObj.BackColor = Color.Red;
                    btnDeleteObj.ForeColor = Color.White;
                    var objToDelete = obj;
                    btnDeleteObj.Click += (s, e) =>
                    {
                        if (MessageBox.Show($"確定要刪除此物件嗎？\n位置:({objToDelete.X},{objToDelete.Y})\nGroup:{objToDelete.GroupId}, Layer:{objToDelete.Layer}",
                            "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            currentS32Data.Layer4.Remove(objToDelete);

                            isS32Modified = true;
                            RenderS32Map();
                            this.toolStripStatusLabel1.Text = $"已刪除物件 ({objToDelete.X},{objToDelete.Y})";

                            // 更新當前面板顯示
                            pb.Image = null;
                            info.Text = "已刪除";
                            btnDeleteObj.Enabled = false;
                            objPanel.BackColor = Color.LightGray;
                        }
                    };
                    objPanel.Controls.Add(btnDeleteObj);

                    flow.Controls.Add(objPanel);
                }

                panel.Controls.Add(flow);
            }
            else
            {
                Label noData = new Label();
                noData.Text = "附近沒有物件";
                noData.Dock = DockStyle.Fill;
                noData.TextAlign = ContentAlignment.MiddleCenter;
                panel.Controls.Add(noData);
            }

            return panel;
        }

        // 創建渲染資訊面板
        private Panel CreateRenderInfoPanel(int cellX, int cellY)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.AutoScroll = true;
            panel.BackColor = Color.White;

            TextBox txtInfo = new TextBox();
            txtInfo.Multiline = true;
            txtInfo.Dock = DockStyle.Fill;
            txtInfo.Font = new Font("Consolas", 9);
            txtInfo.ScrollBars = ScrollBars.Both;
            txtInfo.WordWrap = false;

            // 計算遊戲座標（Layer3 尺度）
            int layer3X = cellX / 2;
            if (layer3X >= 64) layer3X = 63;
            int gameX = currentS32FileItem.SegInfo.nLinBeginX + layer3X;
            int gameY = currentS32FileItem.SegInfo.nLinBeginY + cellY;

            StringBuilder info = new StringBuilder();
            info.AppendLine("==================== 格子渲染資訊 ====================");
            info.AppendLine($"格子座標: ({cellX}, {cellY})");
            info.AppendLine($"遊戲座標: ({gameX}, {gameY})");
            info.AppendLine();

            // 第一層資訊
            info.AppendLine("【第1層 - 地板】");
            var cell = currentS32Data.Layer1[cellY, cellX];
            if (cell != null && cell.TileId > 0)
            {
                info.AppendLine($"  Tile ID: {cell.TileId}");
                info.AppendLine($"  Index ID: {cell.IndexId}");
                info.AppendLine($"  檔案: {cell.TileId}.til");
                info.AppendLine($"  已修改: {(cell.IsModified ? "是" : "否")}");
            }
            else
            {
                info.AppendLine("  (空)");
            }
            info.AppendLine();

            // 第二層資訊
            info.AppendLine("【第2層 - 資料】");
            info.AppendLine($"  總共有 {currentS32Data.Layer2.Count} 項資料");
            info.AppendLine("  (此層不對應具體格子)");
            info.AppendLine();

            // 第三層資訊
            info.AppendLine("【第3層 - 屬性】");
            var attr = currentS32Data.Layer3[cellY, layer3X];
            if (attr != null)
            {
                info.AppendLine($"  左上邊: 0x{attr.Attribute1:X4} ({attr.Attribute1})");
                info.AppendLine($"  右上邊: 0x{attr.Attribute2:X4} ({attr.Attribute2})");

                // 左上邊標記
                List<string> flags1 = new List<string>();
                if ((attr.Attribute1 & 0x01) != 0)
                    flags1.Add("不可通行");
                else
                    flags1.Add("可通行");
                if ((attr.Attribute1 & 0x02) != 0) flags1.Add("安全區");
                if ((attr.Attribute1 & 0x04) != 0) flags1.Add("戰鬥區");
                if ((attr.Attribute1 & 0x08) != 0) flags1.Add("標記8");
                if ((attr.Attribute1 & 0x10) != 0) flags1.Add("標記16");
                info.AppendLine($"  左上邊標記: {string.Join(", ", flags1)}");

                // 右上邊標記
                List<string> flags2 = new List<string>();
                if ((attr.Attribute2 & 0x01) != 0)
                    flags2.Add("不可通行");
                else
                    flags2.Add("可通行");
                if ((attr.Attribute2 & 0x02) != 0) flags2.Add("安全區");
                if ((attr.Attribute2 & 0x04) != 0) flags2.Add("戰鬥區");
                if ((attr.Attribute2 & 0x08) != 0) flags2.Add("標記8");
                if ((attr.Attribute2 & 0x10) != 0) flags2.Add("標記16");
                info.AppendLine($"  右上邊標記: {string.Join(", ", flags2)}");
            }
            else
            {
                info.AppendLine("  (空)");
            }
            info.AppendLine();

            // 第四層資訊
            info.AppendLine("【第4層 - 物件】");
            var objectsAtCell = currentS32Data.Layer4.Where(o => o.X == cellX && o.Y == cellY).OrderBy(o => o.Layer).ToList();
            if (objectsAtCell.Count > 0)
            {
                info.AppendLine($"  此格子有 {objectsAtCell.Count} 個物件:");
                foreach (var obj in objectsAtCell)
                {
                    info.AppendLine($"    - Tile ID: {obj.TileId}, Index: {obj.IndexId}");
                    info.AppendLine($"      Layer: {obj.Layer}, Group: {obj.GroupId}");
                    info.AppendLine($"      檔案: {obj.TileId}.til 或 {obj.TileId}.spr");
                }
            }
            else
            {
                info.AppendLine("  (無物件)");
            }
            info.AppendLine();

            // 周圍物件統計
            info.AppendLine("【周圍物件統計】");
            int nearbyCount = currentS32Data.Layer4.Count(o =>
                Math.Abs(o.X - cellX) <= 5 && Math.Abs(o.Y - cellY) <= 5);
            info.AppendLine($"  5格範圍內物件數: {nearbyCount}");
            info.AppendLine();

            // 檔案資訊
            info.AppendLine("【S32 檔案資訊】");
            info.AppendLine($"  檔案: {Path.GetFileName(currentS32FileItem.FilePath)}");
            info.AppendLine($"  Block 座標: ({currentS32FileItem.SegInfo.nBlockX:X4}, {currentS32FileItem.SegInfo.nBlockY:X4})");
            info.AppendLine($"  遊戲座標範圍: [{currentS32FileItem.SegInfo.nLinBeginX},{currentS32FileItem.SegInfo.nLinBeginY}] ~ [{currentS32FileItem.SegInfo.nLinEndX},{currentS32FileItem.SegInfo.nLinEndY}]");
            info.AppendLine($"  已修改: {(isS32Modified ? "是" : "否")}");
            info.AppendLine();

            // 使用的 Tile 統計
            info.AppendLine("【使用的 Tile 統計】");
            info.AppendLine($"  總共使用 {currentS32Data.UsedTiles.Count} 種不同的 Tile");
            info.AppendLine($"  第1層格子數: {64 * 128} (64x128)");
            info.AppendLine($"  第3層格子數: {64 * 64} (64x64)");
            info.AppendLine($"  第4層物件數: {currentS32Data.Layer4.Count}");

            txtInfo.Text = info.ToString();
            panel.Controls.Add(txtInfo);

            return panel;
        }

        // 保存 S32 按鈕點擊事件 - 保存所有被修改的 S32 檔案
        private void btnSaveS32_Click(object sender, EventArgs e)
        {
            // 找出所有被修改的 S32 檔案
            var modifiedFiles = allS32DataDict.Where(kvp => kvp.Value.IsModified).ToList();

            if (modifiedFiles.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "沒有需要保存的修改";
                return;
            }

            int successCount = 0;
            int failCount = 0;
            StringBuilder errors = new StringBuilder();

            foreach (var kvp in modifiedFiles)
            {
                try
                {
                    SaveS32File(kvp.Key);
                    kvp.Value.IsModified = false;
                    successCount++;
                }
                catch (Exception ex)
                {
                    failCount++;
                    errors.AppendLine($"{Path.GetFileName(kvp.Key)}: {ex.Message}");
                }
            }

            // 只在狀態列顯示結果
            if (failCount == 0)
            {
                this.toolStripStatusLabel1.Text = $"成功保存 {successCount} 個 S32 檔案";
            }
            else
            {
                this.toolStripStatusLabel1.Text = $"保存完成：成功 {successCount} 個，失敗 {failCount} 個";
                // 只有失敗時才顯示錯誤訊息
                MessageBox.Show(
                    $"保存完成：\n成功: {successCount} 個\n失敗: {failCount} 個\n\n失敗詳情：\n{errors}",
                    "部分失敗",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        // 保存 S32 檔案（安全模式：只更新修改過的部分）
        private void SaveS32File(string filePath)
        {
            // 從字典中取得對應的 S32Data
            if (!allS32DataDict.ContainsKey(filePath))
            {
                throw new InvalidOperationException($"S32 檔案不在記憶體中: {filePath}");
            }

            S32Data s32Data = allS32DataDict[filePath];

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                // 第一步：寫入前四層之前的數據（如果有的話，通常第一層從0開始）
                if (s32Data.Layer1Offset > 0)
                {
                    bw.Write(s32Data.OriginalFileData, 0, s32Data.Layer1Offset);
                }

                // 第二步：寫入第一層（地板）- 64x128
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        var cell = s32Data.Layer1[y, x];
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

                // 第三步：寫入第二層
                bw.Write((ushort)s32Data.Layer2.Count);
                foreach (var item in s32Data.Layer2)
                {
                    bw.Write(item.Value1);
                    bw.Write(item.Value2);
                    bw.Write(item.Value3);
                }

                // 第四步：寫入第三層（地圖屬性）- 64x64
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        var attr = s32Data.Layer3[y, x];
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

                // 第五步：寫入第四層（物件）
                var groupedObjects = s32Data.Layer4
                    .GroupBy(o => o.GroupId)
                    .OrderBy(g => g.Key)
                    .ToList();

                bw.Write(groupedObjects.Count); // 組數

                foreach (var group in groupedObjects)
                {
                    var objects = group.OrderBy(o => o.Layer).ToList();
                    bw.Write((short)group.Key); // GroupId
                    bw.Write((ushort)objects.Count); // 該組的物件數

                    foreach (var obj in objects)
                    {
                        bw.Write((byte)obj.X);
                        bw.Write((byte)obj.Y);
                        bw.Write((byte)obj.Layer);
                        bw.Write((byte)obj.IndexId);
                        bw.Write((short)obj.TileId);
                        bw.Write((byte)0); // uk
                    }
                }

                // 第六步：寫入第5-8層數據（從解析後的資料重新生成）
                // 第五層 - 可透明化的圖塊
                bw.Write(s32Data.Layer5.Count);
                foreach (var item in s32Data.Layer5)
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
                        var cell = s32Data.Layer1[y, x];
                        if (cell != null && cell.TileId > 0)
                        {
                            usedTileIds.Add(cell.TileId);
                        }
                    }
                }

                // 從 Layer4 收集
                foreach (var obj in s32Data.Layer4)
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
                s32Data.Layer6.Clear();
                s32Data.Layer6.AddRange(sortedTileIds);

                // 第七層 - 傳送點、入口點
                bw.Write((ushort)s32Data.Layer7.Count);
                foreach (var item in s32Data.Layer7)
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
                bw.Write((byte)s32Data.Layer8.Count);
                bw.Write((byte)0); // 跳過一個 byte
                foreach (var item in s32Data.Layer8)
                {
                    bw.Write(item.SprId);
                    bw.Write(item.X);
                    bw.Write(item.Y);
                    bw.Write(item.Unknown);
                }

                // 第七步：將完整數據寫入文件
                byte[] outputData = ms.ToArray();
                File.WriteAllBytes(filePath, outputData);
            }
        }

        // 繪製放大的 Tile 到指定大小
        private unsafe Bitmap RenderTileEnlarged(byte[] tilData, int tileId, int size)
        {
            try
            {
                // 創建指定大小的縮圖
                Bitmap thumbnail = new Bitmap(size, size, PixelFormat.Format16bppRgb555);

                Rectangle rect = new Rectangle(0, 0, thumbnail.Width, thumbnail.Height);
                BitmapData bmpData = thumbnail.LockBits(rect, ImageLockMode.ReadWrite, thumbnail.PixelFormat);
                int rowpix = bmpData.Stride;
                byte* ptr = (byte*)bmpData.Scan0;

                // 固定 tilData 陣列以取得指標
                fixed (byte* til_ptr_fixed = tilData)
                {
                    byte* til_ptr = til_ptr_fixed;
                    byte type = *(til_ptr++);

                    // 計算縮放比例和偏移（置中並放大）
                    double scale = size / 48.0; // 48 是原始大小
                    int offsetX = (int)((size - 24 * scale) / 2);
                    int offsetY = (int)((size - 24 * scale) / 2);

                    if (type == 1 || type == 9 || type == 17)
                    {
                        // 下半部 2.5D 方塊
                        for (int ty = 0; ty < 24; ty++)
                        {
                            int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                            int tx = 0;
                            for (int p = 0; p < n; p++)
                            {
                                ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));

                                // 放大繪製
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
                        for (int ty = 0; ty < 24; ty++)
                        {
                            int n = (ty <= 11) ? (ty + 1) * 2 : (23 - ty) * 2;
                            int tx = 24 - n;
                            for (int p = 0; p < n; p++)
                            {
                                ushort color = (ushort)(*(til_ptr++) | (*(til_ptr++) << 8));

                                // 放大繪製
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
                    else
                    {
                        // 壓縮格式
                        byte x_offset = *(til_ptr++);
                        byte y_offset = *(til_ptr++);
                        byte xxLen = *(til_ptr++);
                        byte yLen = *(til_ptr++);

                        for (int ty = 0; ty < yLen && ty < size; ty++)
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

                                    // 放大繪製
                                    int baseX = (int)(offsetX + tx * scale);
                                    int baseY = (int)(offsetY + (ty + y_offset) * scale);

                                    for (int sy = 0; sy < (int)scale + 1; sy++)
                                    {
                                        for (int sx = 0; sx < (int)scale + 1; sx++)
                                        {
                                            int px = baseX + sx;
                                            int py = baseY + sy;
                                            if (px >= 0 && px < size && py >= 0 && py < size)
                                            {
                                                int v = py * rowpix + (px * 2);
                                                if (type == 34 || type == 35)
                                                {
                                                    // 需要與背景混合
                                                    ushort colorB = (ushort)(*(ptr + v) | (*(ptr + v + 1) << 8));
                                                    color = (ushort)(colorB + 0xffff - color);
                                                }
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
                }

                thumbnail.UnlockBits(bmpData);
                return thumbnail;
            }
            catch
            {
                return CreatePlaceholderThumbnail(tileId);
            }
         }

        // 更新 Layer4 群組清單
        private void UpdateLayer4GroupsList(S32Data s32Data, int cellX, int cellY)
        {
            lvLayer4Groups.Items.Clear();
            selectedLayer4Groups.Clear();
            isFilteringLayer4Groups = false;

            if (s32Data == null || s32Data.Layer4 == null)
                return;

            // 找出該格子的所有 Layer4 物件，按 GroupId 分組
            var objectsAtCell = s32Data.Layer4
                .Where(o => o.X == cellX && o.Y == cellY)
                .GroupBy(o => o.GroupId)
                .OrderBy(g => g.Key)
                .ToList();

            // 另外收集整個 S32 中所有 Group 的統計
            var allGroups = s32Data.Layer4
                .GroupBy(o => o.GroupId)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 顯示該格子有的群組
            foreach (var group in objectsAtCell)
            {
                int groupId = group.Key;
                int countAtCell = group.Count();
                int totalCount = allGroups.ContainsKey(groupId) ? allGroups[groupId].Count : countAtCell;

                // 取得該群組物件的位置範圍
                var groupObjects = allGroups.ContainsKey(groupId) ? allGroups[groupId] : group.ToList();
                int minX = groupObjects.Min(o => o.X);
                int maxX = groupObjects.Max(o => o.X);
                int minY = groupObjects.Min(o => o.Y);
                int maxY = groupObjects.Max(o => o.Y);
                string posRange = $"{minX}-{maxX},{minY}-{maxY}";

                ListViewItem item = new ListViewItem(groupId.ToString());
                item.SubItems.Add(totalCount.ToString());
                item.SubItems.Add(posRange);
                item.Tag = groupId;
                item.Checked = false;  // 預設不勾選
                lvLayer4Groups.Items.Add(item);
            }

            // 更新標籤顯示
            if (objectsAtCell.Count > 0)
            {
                lblLayer4Groups.Text = $"Layer4 群組 ({objectsAtCell.Count})";
            }
            else
            {
                lblLayer4Groups.Text = "Layer4 物件群組";
            }
        }

        // Layer4 群組勾選變更事件
        private void lvLayer4Groups_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (e.Item.Tag == null)
                return;

            int groupId = (int)e.Item.Tag;

            if (e.Item.Checked)
            {
                selectedLayer4Groups.Add(groupId);
            }
            else
            {
                selectedLayer4Groups.Remove(groupId);
            }

            // 只要有任何勾選就啟用篩選
            isFilteringLayer4Groups = selectedLayer4Groups.Count > 0;

            // 重新渲染地圖
            RenderS32Map();
        }

        // 更新群組縮圖列表（顯示所有已載入 S32 的群組）
        private void UpdateGroupThumbnailsList()
        {
            UpdateGroupThumbnailsList(null);  // 不帶選取區域時顯示全部
        }

        // 更新群組縮圖列表（可指定只顯示選取區域內的群組）
        private void UpdateGroupThumbnailsList(List<SelectedCell> selectedCells)
        {
            lvGroupThumbnails.Items.Clear();

            if (lvGroupThumbnails.LargeImageList != null)
            {
                lvGroupThumbnails.LargeImageList.Dispose();
            }

            if (allS32DataDict.Count == 0)
            {
                lblGroupThumbnails.Text = "群組縮圖列表";
                return;
            }

            // 收集群組（根據是否有選取區域決定範圍）
            var allGroupsDict = new Dictionary<int, List<(S32Data s32, ObjectTile obj)>>();

            if (selectedCells != null && selectedCells.Count > 0)
            {
                // 建立選取格子的全域座標集合 (Layer3 座標系)
                var selectedGlobalCells = new HashSet<(int x, int y)>();
                foreach (var cell in selectedCells)
                {
                    int globalX = cell.S32Data.SegInfo.nLinBeginX + cell.LocalX / 2;
                    int globalY = cell.S32Data.SegInfo.nLinBeginY + cell.LocalY;
                    selectedGlobalCells.Add((globalX, globalY));
                }

                // 已處理過的群組 ID（避免重複加入）
                HashSet<int> processedGroups = new HashSet<int>();

                // 遍歷所有 S32，找出全域座標精確落在選取格子內的 Layer4 物件
                foreach (var s32Data in allS32DataDict.Values)
                {
                    int segStartX = s32Data.SegInfo.nLinBeginX;
                    int segStartY = s32Data.SegInfo.nLinBeginY;

                    foreach (var obj in s32Data.Layer4)
                    {
                        // 計算物件的全域座標 (考慮物件 X 可能超出 0-127 範圍)
                        int objGlobalX = segStartX + obj.X / 2;
                        int objGlobalY = segStartY + obj.Y;

                        // 精確檢查是否在選取的格子內
                        if (selectedGlobalCells.Contains((objGlobalX, objGlobalY)))
                        {
                            // 避免重複處理同一群組
                            if (processedGroups.Contains(obj.GroupId))
                                continue;
                            processedGroups.Add(obj.GroupId);

                            if (!allGroupsDict.ContainsKey(obj.GroupId))
                            {
                                allGroupsDict[obj.GroupId] = new List<(S32Data, ObjectTile)>();
                            }

                            // 找出這個群組的所有物件（整個地圖範圍）
                            foreach (var s32 in allS32DataDict.Values)
                            {
                                foreach (var o in s32.Layer4)
                                {
                                    if (o.GroupId == obj.GroupId)
                                    {
                                        allGroupsDict[obj.GroupId].Add((s32, o));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // 收集所有 S32 中的群組
                foreach (var s32Data in allS32DataDict.Values)
                {
                    foreach (var obj in s32Data.Layer4)
                    {
                        if (!allGroupsDict.ContainsKey(obj.GroupId))
                        {
                            allGroupsDict[obj.GroupId] = new List<(S32Data, ObjectTile)>();
                        }
                        allGroupsDict[obj.GroupId].Add((s32Data, obj));
                    }
                }
            }

            if (allGroupsDict.Count == 0)
            {
                string label = selectedCells != null && selectedCells.Count > 0 ? "選取區域群組 (0)" : "群組縮圖列表 (0)";
                lblGroupThumbnails.Text = label;
                return;
            }

            // 建立 ImageList
            ImageList imageList = new ImageList();
            imageList.ImageSize = new Size(80, 80);
            imageList.ColorDepth = ColorDepth.Depth32Bit;

            int thumbnailIndex = 0;
            foreach (var kvp in allGroupsDict.OrderBy(k => k.Key))
            {
                int groupId = kvp.Key;
                var objects = kvp.Value;

                // 生成群組縮圖
                Bitmap thumbnail = GenerateGroupThumbnail(objects, 80);
                if (thumbnail != null)
                {
                    imageList.Images.Add(thumbnail);

                    ListViewItem item = new ListViewItem($"G{groupId} ({objects.Count})");
                    item.ImageIndex = thumbnailIndex;
                    item.Tag = new GroupThumbnailInfo { GroupId = groupId, Objects = objects };
                    lvGroupThumbnails.Items.Add(item);

                    thumbnailIndex++;
                }
            }

            lvGroupThumbnails.LargeImageList = imageList;
            string labelText = selectedCells != null && selectedCells.Count > 0
                ? $"選取區域群組 ({allGroupsDict.Count})"
                : $"群組縮圖列表 ({allGroupsDict.Count})";
            lblGroupThumbnails.Text = labelText;
        }

        // 群組縮圖資訊
        private class GroupThumbnailInfo
        {
            public int GroupId { get; set; }
            public List<(S32Data s32, ObjectTile obj)> Objects { get; set; }
        }

        // 「全部」按鈕點擊事件 - 顯示全部群組
        private void btnShowAllGroups_Click(object sender, EventArgs e)
        {
            UpdateGroupThumbnailsList(null);  // 傳入 null 顯示全部
        }

        // 生成群組縮圖（將同 GroupId 的物件按相對位置組裝，使用與主畫布相同的繪製方式）
        private Bitmap GenerateGroupThumbnail(List<(S32Data s32, ObjectTile obj)> objects, int thumbnailSize)
        {
            if (objects == null || objects.Count == 0)
                return null;

            try
            {
                // 計算物件的邊界範圍
                int minX = objects.Min(o => o.obj.X);
                int maxX = objects.Max(o => o.obj.X);
                int minY = objects.Min(o => o.obj.Y);
                int maxY = objects.Max(o => o.obj.Y);

                int rangeX = maxX - minX + 1;
                int rangeY = maxY - minY + 1;

                // 使用與主畫布相同的座標公式計算邊界
                // baseX = 0, baseY = 63 * 12 (對於完整 S32 區塊)
                // pixelX = baseX + x * 24 + y * 24 - 24 * (x / 2)
                // pixelY = baseY + y * 12 - 12 * (x / 2)
                // 簡化後: pixelX = x * 12 + y * 24, pixelY = 63*12 + y * 12 - x * 6

                // 計算所有物件的像素邊界
                int pixelMinX = int.MaxValue, pixelMaxX = int.MinValue;
                int pixelMinY = int.MaxValue, pixelMaxY = int.MinValue;

                foreach (var item in objects)
                {
                    var obj = item.obj;
                    // 使用與 RenderS32Block 相同的座標計算
                    int baseX = 0;
                    int baseY = 63 * 12;
                    baseX -= 24 * (obj.X / 2);
                    baseY -= 12 * (obj.X / 2);
                    int px = baseX + obj.X * 24 + obj.Y * 24;
                    int py = baseY + obj.Y * 12;

                    pixelMinX = Math.Min(pixelMinX, px);
                    pixelMaxX = Math.Max(pixelMaxX, px + 48);  // tile 寬度約 48
                    pixelMinY = Math.Min(pixelMinY, py);
                    pixelMaxY = Math.Max(pixelMaxY, py + 48);  // tile 高度預留空間
                }

                // 計算實際所需的圖片大小
                int actualWidth = pixelMaxX - pixelMinX + 48;
                int actualHeight = pixelMaxY - pixelMinY + 48;

                // 建立暫存圖片
                int tempWidth = Math.Max(actualWidth, 96);
                int tempHeight = Math.Max(actualHeight, 96);
                if (tempWidth > 2048) tempWidth = 2048;
                if (tempHeight > 2048) tempHeight = 2048;

                // 使用 16bpp 格式與主畫布相同
                Bitmap tempBitmap = new Bitmap(tempWidth, tempHeight, PixelFormat.Format16bppRgb555);

                Rectangle rect = new Rectangle(0, 0, tempBitmap.Width, tempBitmap.Height);
                BitmapData bmpData = tempBitmap.LockBits(rect, ImageLockMode.ReadWrite, tempBitmap.PixelFormat);
                int rowpix = bmpData.Stride;

                unsafe
                {
                    byte* ptr = (byte*)bmpData.Scan0;

                    // 填充白色背景 (RGB555: 0x7FFF = 白色)
                    for (int y = 0; y < tempHeight; y++)
                    {
                        for (int x = 0; x < tempWidth; x++)
                        {
                            int v = y * rowpix + (x * 2);
                            *(ptr + v) = 0xFF;
                            *(ptr + v + 1) = 0x7F;
                        }
                    }

                    // 計算偏移量，讓圖片置中
                    int offsetX = (tempWidth - actualWidth) / 2 - pixelMinX + 24;
                    int offsetY = (tempHeight - actualHeight) / 2 - pixelMinY + 24;

                    // 按 Layer 排序後繪製（使用與主畫布完全相同的繪製方式）
                    foreach (var item in objects.OrderBy(o => o.obj.Layer))
                    {
                        var obj = item.obj;

                        // 使用與 RenderS32Block 相同的座標計算
                        int baseX = 0;
                        int baseY = 63 * 12;
                        baseX -= 24 * (obj.X / 2);
                        baseY -= 12 * (obj.X / 2);

                        int pixelX = offsetX + baseX + obj.X * 24 + obj.Y * 24;
                        int pixelY = offsetY + baseY + obj.Y * 12;

                        // 使用與主畫布相同的繪製函數
                        DrawTilToBufferDirect(pixelX, pixelY, obj.TileId, obj.IndexId, rowpix, ptr, tempWidth, tempHeight);
                    }
                }

                tempBitmap.UnlockBits(bmpData);

                // 縮放到目標大小（白底）
                Bitmap result = new Bitmap(thumbnailSize, thumbnailSize, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(result))
                {
                    // 白色底
                    g.Clear(Color.White);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                    // 保持比例縮放
                    float scaleX = (float)(thumbnailSize - 4) / tempWidth;
                    float scaleY = (float)(thumbnailSize - 4) / tempHeight;
                    float scale = Math.Min(scaleX, scaleY);
                    int scaledWidth = (int)(tempWidth * scale);
                    int scaledHeight = (int)(tempHeight * scale);
                    int drawX = (thumbnailSize - scaledWidth) / 2;
                    int drawY = (thumbnailSize - scaledHeight) / 2;

                    g.DrawImage(tempBitmap, drawX, drawY, scaledWidth, scaledHeight);

                    // 加邊框
                    using (Pen borderPen = new Pen(Color.LightGray, 1))
                    {
                        g.DrawRectangle(borderPen, 0, 0, thumbnailSize - 1, thumbnailSize - 1);
                    }
                }

                tempBitmap.Dispose();
                return result;
            }
            catch
            {
                // 如果生成失敗，返回一個帶文字的預設圖片
                Bitmap fallback = new Bitmap(thumbnailSize, thumbnailSize);
                using (Graphics g = Graphics.FromImage(fallback))
                {
                    g.Clear(Color.White);
                    using (Font font = new Font("Arial", 10))
                    {
                        string text = $"G{objects[0].obj.GroupId}";
                        SizeF textSize = g.MeasureString(text, font);
                        g.DrawString(text, font, Brushes.Gray,
                            (thumbnailSize - textSize.Width) / 2,
                            (thumbnailSize - textSize.Height) / 2);
                    }
                }
                return fallback;
            }
        }

        // 群組縮圖單擊事件 - 左鍵顯示放大預覽
        private void lvGroupThumbnails_MouseClick(object sender, MouseEventArgs e)
        {
            // 只處理左鍵點擊（右鍵由 MouseUp 處理顯示 context menu）
            if (e.Button != MouseButtons.Left)
                return;

            if (lvGroupThumbnails.SelectedItems.Count == 0)
                return;

            var item = lvGroupThumbnails.SelectedItems[0];
            if (item.Tag is GroupThumbnailInfo info && info.Objects.Count > 0)
            {
                ShowGroupPreviewDialog(info);
            }
        }

        // 顯示群組預覽對話框（可縮放）
        private void ShowGroupPreviewDialog(GroupThumbnailInfo info)
        {
            // 生成高解析度預覽圖（800x800）
            int baseSize = 800;
            Bitmap previewImage = GenerateGroupThumbnail(info.Objects, baseSize);

            if (previewImage == null)
                return;

            // 縮放狀態
            float currentZoom = 1.0f;
            float minZoom = 0.25f;
            float maxZoom = 4.0f;
            Point dragStart = Point.Empty;
            Point scrollOffset = Point.Empty;
            bool isDragging = false;

            // 建立預覽對話框
            Form previewForm = new Form
            {
                Text = $"群組 {info.GroupId} - {info.Objects.Count} 個物件 (滾輪縮放, 拖曳平移)",
                Size = new Size(520, 600),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                MaximizeBox = true,
                MinimizeBox = false
            };

            // 使用 Panel 作為容器，支援滾動
            Panel container = new Panel
            {
                Location = new Point(10, 10),
                Size = new Size(480, 480),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                AutoScroll = true
            };

            PictureBox pb = new PictureBox
            {
                Image = previewImage,
                Size = previewImage.Size,
                Location = new Point(0, 0),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White
            };

            // 更新 PictureBox 大小的函數
            Action updateZoom = () =>
            {
                int newWidth = (int)(baseSize * currentZoom);
                int newHeight = (int)(baseSize * currentZoom);
                pb.Size = new Size(newWidth, newHeight);
                previewForm.Text = $"群組 {info.GroupId} - {info.Objects.Count} 個物件 ({(int)(currentZoom * 100)}%)";
            };

            // 滾輪縮放
            pb.MouseWheel += (s, ev) =>
            {
                float oldZoom = currentZoom;
                if (ev.Delta > 0)
                    currentZoom = Math.Min(currentZoom * 1.2f, maxZoom);
                else
                    currentZoom = Math.Max(currentZoom / 1.2f, minZoom);

                if (Math.Abs(oldZoom - currentZoom) > 0.001f)
                    updateZoom();
            };

            container.MouseWheel += (s, ev) =>
            {
                float oldZoom = currentZoom;
                if (ev.Delta > 0)
                    currentZoom = Math.Min(currentZoom * 1.2f, maxZoom);
                else
                    currentZoom = Math.Max(currentZoom / 1.2f, minZoom);

                if (Math.Abs(oldZoom - currentZoom) > 0.001f)
                    updateZoom();
            };

            // 拖曳平移
            pb.MouseDown += (s, ev) =>
            {
                if (ev.Button == MouseButtons.Left)
                {
                    isDragging = true;
                    dragStart = ev.Location;
                    pb.Cursor = Cursors.Hand;
                }
            };

            pb.MouseMove += (s, ev) =>
            {
                if (isDragging)
                {
                    int dx = ev.X - dragStart.X;
                    int dy = ev.Y - dragStart.Y;
                    container.AutoScrollPosition = new Point(
                        -container.AutoScrollPosition.X - dx,
                        -container.AutoScrollPosition.Y - dy);
                }
            };

            pb.MouseUp += (s, ev) =>
            {
                isDragging = false;
                pb.Cursor = Cursors.Default;
            };

            container.Controls.Add(pb);

            // 縮放按鈕
            Button btnZoomIn = new Button
            {
                Text = "+",
                Size = new Size(40, 30),
                Location = new Point(10, 500),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnZoomIn.Click += (s, ev) =>
            {
                currentZoom = Math.Min(currentZoom * 1.5f, maxZoom);
                updateZoom();
            };

            Button btnZoomOut = new Button
            {
                Text = "-",
                Size = new Size(40, 30),
                Location = new Point(55, 500),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnZoomOut.Click += (s, ev) =>
            {
                currentZoom = Math.Max(currentZoom / 1.5f, minZoom);
                updateZoom();
            };

            Button btnZoomReset = new Button
            {
                Text = "1:1",
                Size = new Size(40, 30),
                Location = new Point(100, 500),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnZoomReset.Click += (s, ev) =>
            {
                currentZoom = 1.0f;
                updateZoom();
                container.AutoScrollPosition = Point.Empty;
            };

            Button btnGoto = new Button
            {
                Text = "跳轉到位置",
                Size = new Size(100, 30),
                Location = new Point(160, 500),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnGoto.Click += (s, ev) =>
            {
                previewForm.Close();
                JumpToGroupLocation(info);
            };

            Button btnClose = new Button
            {
                Text = "關閉",
                Size = new Size(80, 30),
                Location = new Point(420, 500),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnClose.Click += (s, ev) => previewForm.Close();

            // 顯示物件資訊
            Label lblInfo = new Label
            {
                Text = $"GroupId: {info.GroupId} | 物件數: {info.Objects.Count}",
                Location = new Point(270, 505),
                Size = new Size(140, 20),
                ForeColor = Color.Gray,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            previewForm.Controls.Add(container);
            previewForm.Controls.Add(btnZoomIn);
            previewForm.Controls.Add(btnZoomOut);
            previewForm.Controls.Add(btnZoomReset);
            previewForm.Controls.Add(btnGoto);
            previewForm.Controls.Add(btnClose);
            previewForm.Controls.Add(lblInfo);

            previewForm.FormClosed += (s, ev) =>
            {
                previewImage.Dispose();
            };

            // 設定焦點讓滾輪可用
            previewForm.Shown += (s, ev) => container.Focus();

            previewForm.ShowDialog(this);
        }

        // 跳轉到群組位置
        private void JumpToGroupLocation(GroupThumbnailInfo info)
        {
            if (info.Objects.Count == 0)
                return;

            var firstObj = info.Objects[0];
            var s32Data = firstObj.s32;
            var obj = firstObj.obj;

            // 計算螢幕座標
            int[] loc = s32Data.SegInfo.GetLoc(1.0);
            int mx = loc[0];
            int my = loc[1];

            int localBaseX = 0;
            int localBaseY = 63 * 12;
            localBaseX -= 24 * (obj.X / 2);
            localBaseY -= 12 * (obj.X / 2);

            int screenX = mx + localBaseX + obj.X * 24 + obj.Y * 24;
            int screenY = my + localBaseY + obj.Y * 12;

            // 捲動到該位置
            int scrollX = screenX - s32MapPanel.Width / 2;
            int scrollY = screenY - s32MapPanel.Height / 2;

            if (scrollX < 0) scrollX = 0;
            if (scrollY < 0) scrollY = 0;
            if (scrollX > s32MapPanel.HorizontalScroll.Maximum) scrollX = s32MapPanel.HorizontalScroll.Maximum;
            if (scrollY > s32MapPanel.VerticalScroll.Maximum) scrollY = s32MapPanel.VerticalScroll.Maximum;

            s32MapPanel.AutoScrollPosition = new Point(scrollX, scrollY);

            // 高亮顯示該格子
            highlightedS32Data = s32Data;
            highlightedCellX = obj.X;
            highlightedCellY = obj.Y;

            s32PictureBox.Invalidate();
            UpdateMiniMap();

            this.toolStripStatusLabel1.Text = $"跳轉到群組 {info.GroupId}，位置 ({obj.X}, {obj.Y})，共 {info.Objects.Count} 個物件";
        }

        // 群組縮圖雙擊事件 - 跳轉到該群組位置
        private void lvGroupThumbnails_DoubleClick(object sender, EventArgs e)
        {
            if (lvGroupThumbnails.SelectedItems.Count == 0)
                return;

            var item = lvGroupThumbnails.SelectedItems[0];
            if (item.Tag is GroupThumbnailInfo info && info.Objects.Count > 0)
            {
                JumpToGroupLocation(info);
            }
        }

        // 群組縮圖右鍵刪除
        private void lvGroupThumbnails_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (lvGroupThumbnails.SelectedItems.Count == 0)
                return;

            var item = lvGroupThumbnails.SelectedItems[0];
            if (!(item.Tag is GroupThumbnailInfo info) || info.Objects.Count == 0)
                return;

            // 建立右鍵選單
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem deleteItem = new ToolStripMenuItem($"刪除群組 {info.GroupId} ({info.Objects.Count} 個物件)");
            deleteItem.Click += (s, ev) =>
            {
                DeleteGroupFromMap(info);
            };

            ToolStripMenuItem gotoItem = new ToolStripMenuItem("跳轉到位置");
            gotoItem.Click += (s, ev) =>
            {
                JumpToGroupLocation(info);
            };

            menu.Items.Add(gotoItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(deleteItem);

            menu.Show(lvGroupThumbnails, e.Location);
        }

        // 從地圖刪除群組
        private void DeleteGroupFromMap(GroupThumbnailInfo info)
        {
            int groupId = info.GroupId;

            // 先計算實際要刪除的物件數量（從當前 S32 資料中重新查找）
            int totalCount = 0;
            foreach (var s32Data in allS32DataDict.Values)
            {
                totalCount += s32Data.Layer4.Count(o => o.GroupId == groupId);
            }

            if (totalCount == 0)
            {
                MessageBox.Show($"群組 {groupId} 已不存在任何物件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 確認刪除
            DialogResult result = MessageBox.Show(
                $"確定要刪除群組 {groupId} 嗎？\n" +
                $"這將移除 {totalCount} 個 Layer4 物件。",
                "確認刪除群組",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            // 遍歷所有 S32，刪除該群組的所有物件
            int deletedCount = 0;
            foreach (var s32Data in allS32DataDict.Values)
            {
                int beforeCount = s32Data.Layer4.Count;
                s32Data.Layer4.RemoveAll(o => o.GroupId == groupId);
                int removed = beforeCount - s32Data.Layer4.Count;
                if (removed > 0)
                {
                    deletedCount += removed;
                    s32Data.IsModified = true;
                }
            }

            // 重新渲染
            RenderS32Map();

            // 更新群組縮圖列表
            UpdateGroupThumbnailsList();

            this.toolStripStatusLabel1.Text = $"已刪除群組 {groupId}，共 {deletedCount} 個物件";
        }

        // ===== 工具列按鈕事件處理 =====

        private void btnToolCopy_Click(object sender, EventArgs e)
        {
            CopySelectedCells();
        }

        private void btnToolPaste_Click(object sender, EventArgs e)
        {
            PasteSelectedCells();
        }

        private void btnToolDelete_Click(object sender, EventArgs e)
        {
            DeleteSelectedLayer4Objects();
        }

        private void btnToolUndo_Click(object sender, EventArgs e)
        {
            UndoLastAction();
        }

        private void btnToolRedo_Click(object sender, EventArgs e)
        {
            RedoLastAction();
        }

        private void btnToolSave_Click(object sender, EventArgs e)
        {
            btnSaveS32_Click(sender, e);
        }

        private void btnToolCellInfo_Click(object sender, EventArgs e)
        {
            // 如果有選取區域，顯示第一個選取格子的詳細資訊
            if (currentSelectedCells.Count > 0)
            {
                var firstCell = currentSelectedCells[0];
                ShowCellLayersDialog(firstCell.LocalX, firstCell.LocalY);
            }
            else
            {
                this.toolStripStatusLabel1.Text = "請先使用 Shift+左鍵 選取格子";
            }
        }

        private void btnToolReplaceTile_Click(object sender, EventArgs e)
        {
            // 檢查是否已載入地圖
            if (allS32DataDict.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 創建替換對話框
            Form replaceForm = new Form();
            replaceForm.Text = "批次替換地板";
            replaceForm.Size = new Size(400, 280);
            replaceForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            replaceForm.StartPosition = FormStartPosition.CenterParent;
            replaceForm.MaximizeBox = false;
            replaceForm.MinimizeBox = false;

            // 來源 TileId
            Label lblSrcTileId = new Label();
            lblSrcTileId.Text = "來源 TileId:";
            lblSrcTileId.Location = new Point(20, 20);
            lblSrcTileId.Size = new Size(100, 20);
            replaceForm.Controls.Add(lblSrcTileId);

            TextBox txtSrcTileId = new TextBox();
            txtSrcTileId.Location = new Point(130, 18);
            txtSrcTileId.Size = new Size(100, 20);
            replaceForm.Controls.Add(txtSrcTileId);

            // 來源 IndexId
            Label lblSrcIndexId = new Label();
            lblSrcIndexId.Text = "來源 IndexId:";
            lblSrcIndexId.Location = new Point(20, 50);
            lblSrcIndexId.Size = new Size(100, 20);
            replaceForm.Controls.Add(lblSrcIndexId);

            TextBox txtSrcIndexId = new TextBox();
            txtSrcIndexId.Location = new Point(130, 48);
            txtSrcIndexId.Size = new Size(100, 20);
            replaceForm.Controls.Add(txtSrcIndexId);

            // 分隔線
            Label lblSeparator = new Label();
            lblSeparator.Text = "↓ 替換為 ↓";
            lblSeparator.Location = new Point(20, 85);
            lblSeparator.Size = new Size(350, 20);
            lblSeparator.TextAlign = ContentAlignment.MiddleCenter;
            lblSeparator.ForeColor = Color.Blue;
            replaceForm.Controls.Add(lblSeparator);

            // 目標 TileId
            Label lblDstTileId = new Label();
            lblDstTileId.Text = "目標 TileId:";
            lblDstTileId.Location = new Point(20, 115);
            lblDstTileId.Size = new Size(100, 20);
            replaceForm.Controls.Add(lblDstTileId);

            TextBox txtDstTileId = new TextBox();
            txtDstTileId.Location = new Point(130, 113);
            txtDstTileId.Size = new Size(100, 20);
            replaceForm.Controls.Add(txtDstTileId);

            // 目標 IndexId
            Label lblDstIndexId = new Label();
            lblDstIndexId.Text = "目標 IndexId:";
            lblDstIndexId.Location = new Point(20, 145);
            lblDstIndexId.Size = new Size(100, 20);
            replaceForm.Controls.Add(lblDstIndexId);

            TextBox txtDstIndexId = new TextBox();
            txtDstIndexId.Location = new Point(130, 143);
            txtDstIndexId.Size = new Size(100, 20);
            replaceForm.Controls.Add(txtDstIndexId);

            // 預覽按鈕
            Button btnPreview = new Button();
            btnPreview.Text = "預覽";
            btnPreview.Location = new Point(80, 190);
            btnPreview.Size = new Size(80, 30);
            replaceForm.Controls.Add(btnPreview);

            // 執行按鈕
            Button btnExecute = new Button();
            btnExecute.Text = "執行替換";
            btnExecute.Location = new Point(170, 190);
            btnExecute.Size = new Size(80, 30);
            replaceForm.Controls.Add(btnExecute);

            // 取消按鈕
            Button btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.Location = new Point(260, 190);
            btnCancel.Size = new Size(80, 30);
            btnCancel.Click += (s, args) => replaceForm.Close();
            replaceForm.Controls.Add(btnCancel);

            // 預覽功能
            btnPreview.Click += (s, args) =>
            {
                if (!int.TryParse(txtSrcTileId.Text, out int srcTileId) ||
                    !int.TryParse(txtSrcIndexId.Text, out int srcIndexId))
                {
                    MessageBox.Show("請輸入有效的來源 TileId 和 IndexId", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                int matchCount = 0;
                int s32Count = 0;

                foreach (var kvp in allS32DataDict)
                {
                    S32Data s32Data = kvp.Value;
                    bool hasMatch = false;

                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 128; x++)
                        {
                            var cell = s32Data.Layer1[y, x];
                            if (cell.TileId == srcTileId && cell.IndexId == srcIndexId)
                            {
                                matchCount++;
                                hasMatch = true;
                            }
                        }
                    }

                    if (hasMatch) s32Count++;
                }

                MessageBox.Show($"找到 {matchCount} 個匹配的格子\n分布在 {s32Count} 個 S32 檔案中",
                    "預覽結果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // 執行替換功能
            btnExecute.Click += (s, args) =>
            {
                if (!int.TryParse(txtSrcTileId.Text, out int srcTileId) ||
                    !int.TryParse(txtSrcIndexId.Text, out int srcIndexId) ||
                    !int.TryParse(txtDstTileId.Text, out int dstTileId) ||
                    !int.TryParse(txtDstIndexId.Text, out int dstIndexId))
                {
                    MessageBox.Show("請輸入有效的 TileId 和 IndexId", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 確認執行
                var confirmResult = MessageBox.Show(
                    $"確定要將所有 TileId={srcTileId}, IndexId={srcIndexId} 的格子\n替換為 TileId={dstTileId}, IndexId={dstIndexId} 嗎？\n\n此操作會影響所有已載入的 S32 檔案。",
                    "確認替換",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes) return;

                int replacedCount = 0;
                HashSet<string> modifiedS32Files = new HashSet<string>();

                foreach (var kvp in allS32DataDict)
                {
                    S32Data s32Data = kvp.Value;
                    bool hasModified = false;

                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 128; x++)
                        {
                            var cell = s32Data.Layer1[y, x];
                            if (cell.TileId == srcTileId && cell.IndexId == srcIndexId)
                            {
                                // 替換 TileId 和 IndexId
                                cell.TileId = dstTileId;
                                cell.IndexId = dstIndexId;
                                cell.IsModified = true;
                                replacedCount++;
                                hasModified = true;
                            }
                        }
                    }

                    if (hasModified)
                    {
                        // 新增目標 TileId 到 Layer6（如果不存在）
                        if (!s32Data.Layer6.Contains(dstTileId))
                        {
                            s32Data.Layer6.Add(dstTileId);
                        }

                        // 標記 S32 為已修改
                        s32Data.IsModified = true;
                        modifiedS32Files.Add(kvp.Key);
                    }
                }

                // 重新渲染地圖
                RenderS32Map();

                // 更新 Tile 清單
                UpdateTileList();

                MessageBox.Show($"替換完成！\n共替換 {replacedCount} 個格子\n影響 {modifiedS32Files.Count} 個 S32 檔案\n\n請記得儲存修改。",
                    "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.toolStripStatusLabel1.Text = $"已替換 {replacedCount} 個格子，影響 {modifiedS32Files.Count} 個 S32 檔案";
                replaceForm.Close();
            };

            replaceForm.ShowDialog();
        }

        // 清除所有第七層（傳送點）資料
        private void btnToolClearLayer7_Click(object sender, EventArgs e)
        {
            if (allS32DataDict.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 計算總共有多少個第七層資料
            int totalLayer7Count = 0;
            int affectedS32Count = 0;
            foreach (var s32Data in allS32DataDict.Values)
            {
                if (s32Data.Layer7 != null && s32Data.Layer7.Count > 0)
                {
                    totalLayer7Count += s32Data.Layer7.Count;
                    affectedS32Count++;
                }
            }

            if (totalLayer7Count == 0)
            {
                MessageBox.Show("沒有第七層（傳送點）資料需要清除", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 確認刪除
            var confirmResult = MessageBox.Show(
                $"確定要清除所有第七層（傳送點）資料嗎？\n\n" +
                $"共 {totalLayer7Count} 筆傳送點資料\n" +
                $"分布在 {affectedS32Count} 個 S32 檔案中\n\n" +
                $"此操作可以使用 Undo 還原。",
                "確認清除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirmResult != DialogResult.Yes) return;

            // 建立 Undo 記錄
            var undoAction = new UndoAction
            {
                Description = $"清除所有第七層資料 ({totalLayer7Count} 筆)"
            };

            // 備份並清除所有第七層資料
            int clearedCount = 0;
            foreach (var kvp in allS32DataDict)
            {
                S32Data s32Data = kvp.Value;
                if (s32Data.Layer7 != null && s32Data.Layer7.Count > 0)
                {
                    // 備份到 Undo（儲存為 Layer7Backup）
                    foreach (var item in s32Data.Layer7)
                    {
                        undoAction.RemovedLayer7Items.Add(new UndoLayer7Info
                        {
                            S32FilePath = kvp.Key,
                            Name = item.Name,
                            X = item.X,
                            Y = item.Y,
                            TargetMapId = item.TargetMapId,
                            PortalId = item.PortalId
                        });
                    }

                    clearedCount += s32Data.Layer7.Count;
                    s32Data.Layer7.Clear();
                    s32Data.IsModified = true;
                }
            }

            // 加入 Undo 堆疊
            PushUndoAction(undoAction);

            // 重新渲染
            RenderS32Map();

            this.toolStripStatusLabel1.Text = $"已清除 {clearedCount} 筆第七層（傳送點）資料，請記得儲存";
        }

        // 清除指定格子的各層資料
        private void btnToolClearCell_Click(object sender, EventArgs e)
        {
            if (allS32DataDict.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 如果有選取區域，直接批量清除
            if (currentSelectedCells.Count > 0)
            {
                ClearSelectedCellsWithDialog();
                return;
            }

            // 沒有選取區域，顯示單格清除對話框
            ShowSingleCellClearDialog();
        }

        // 批量清除選取區域的對話框
        private void ClearSelectedCellsWithDialog()
        {
            Form clearForm = new Form();
            clearForm.Text = $"批量清除格子資料 - 已選取 {currentSelectedCells.Count} 格";
            clearForm.Size = new Size(320, 320);
            clearForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            clearForm.StartPosition = FormStartPosition.CenterParent;
            clearForm.MaximizeBox = false;
            clearForm.MinimizeBox = false;

            // 選擇要清除的層
            Label lblLayers = new Label();
            lblLayers.Text = $"選擇要清除的層 (共 {currentSelectedCells.Count} 格):";
            lblLayers.Location = new Point(20, 20);
            lblLayers.Size = new Size(250, 20);
            clearForm.Controls.Add(lblLayers);

            CheckBox chkL1 = new CheckBox();
            chkL1.Text = "第1層 (地板)";
            chkL1.Location = new Point(30, 50);
            chkL1.Size = new Size(150, 20);
            chkL1.Checked = true;
            clearForm.Controls.Add(chkL1);

            CheckBox chkL3 = new CheckBox();
            chkL3.Text = "第3層 (屬性) - 設為可通行";
            chkL3.Location = new Point(30, 75);
            chkL3.Size = new Size(200, 20);
            chkL3.Checked = true;
            clearForm.Controls.Add(chkL3);

            CheckBox chkL4 = new CheckBox();
            chkL4.Text = "第4層 (物件)";
            chkL4.Location = new Point(30, 100);
            chkL4.Size = new Size(150, 20);
            chkL4.Checked = true;
            clearForm.Controls.Add(chkL4);

            CheckBox chkL5 = new CheckBox();
            chkL5.Text = "第5層 (透明圖塊)";
            chkL5.Location = new Point(30, 125);
            chkL5.Size = new Size(150, 20);
            clearForm.Controls.Add(chkL5);

            CheckBox chkL7 = new CheckBox();
            chkL7.Text = "第7層 (傳送點)";
            chkL7.Location = new Point(30, 150);
            chkL7.Size = new Size(150, 20);
            clearForm.Controls.Add(chkL7);

            CheckBox chkL8 = new CheckBox();
            chkL8.Text = "第8層 (特效)";
            chkL8.Location = new Point(30, 175);
            chkL8.Size = new Size(150, 20);
            clearForm.Controls.Add(chkL8);

            // 執行按鈕
            Button btnExecute = new Button();
            btnExecute.Text = "清除";
            btnExecute.Location = new Point(60, 220);
            btnExecute.Size = new Size(80, 30);
            clearForm.Controls.Add(btnExecute);

            Button btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.Location = new Point(160, 220);
            btnCancel.Size = new Size(80, 30);
            btnCancel.Click += (s, args) => clearForm.Close();
            clearForm.Controls.Add(btnCancel);

            btnExecute.Click += (s, args) =>
            {
                int totalL1 = 0, totalL3 = 0, totalL4 = 0, totalL5 = 0, totalL7 = 0, totalL8 = 0;
                HashSet<S32Data> modifiedS32s = new HashSet<S32Data>();

                // 建立選取格子的全域座標集合 (用於 Layer4 跨區塊物件)
                var selectedGlobalCells = new HashSet<(int x, int y)>();
                foreach (var cell in currentSelectedCells)
                {
                    int globalX = cell.S32Data.SegInfo.nLinBeginX + cell.LocalX / 2;
                    int globalY = cell.S32Data.SegInfo.nLinBeginY + cell.LocalY;
                    selectedGlobalCells.Add((globalX, globalY));
                }

                foreach (var cell in currentSelectedCells)
                {
                    S32Data s32Data = cell.S32Data;
                    int layer1X = cell.LocalX;
                    int localY = cell.LocalY;
                    int layer3X = layer1X / 2;

                    // 清除第1層
                    if (chkL1.Checked && layer1X >= 0 && layer1X < 128 && localY >= 0 && localY < 64)
                    {
                        s32Data.Layer1[localY, layer1X] = new TileCell { X = layer1X, Y = localY, TileId = 0, IndexId = 0 };
                        totalL1++;
                        modifiedS32s.Add(s32Data);
                    }

                    // 清除第3層（設為可通行）- 只在偶數 X 時處理，避免重複
                    if (chkL3.Checked && layer1X % 2 == 0 && layer3X >= 0 && layer3X < 64 && localY >= 0 && localY < 64)
                    {
                        s32Data.Layer3[localY, layer3X] = new MapAttribute { Attribute1 = 0, Attribute2 = 0 };
                        totalL3++;
                        modifiedS32s.Add(s32Data);
                    }

                    // 清除第5層 - 只在偶數 X 時處理
                    if (chkL5.Checked && layer1X % 2 == 0)
                    {
                        totalL5 += s32Data.Layer5.RemoveAll(item => item.X == layer3X && item.Y == localY);
                        modifiedS32s.Add(s32Data);
                    }

                    // 清除第7層 - 只在偶數 X 時處理
                    if (chkL7.Checked && layer1X % 2 == 0)
                    {
                        totalL7 += s32Data.Layer7.RemoveAll(item => item.X == layer3X && item.Y == localY);
                        modifiedS32s.Add(s32Data);
                    }

                    // 清除第8層 - 只在偶數 X 時處理
                    if (chkL8.Checked && layer1X % 2 == 0)
                    {
                        totalL8 += s32Data.Layer8.RemoveAll(item => item.X == layer3X && item.Y == localY);
                        modifiedS32s.Add(s32Data);
                    }
                }

                // 清除第4層 - 需要遍歷所有 S32 找跨區塊物件
                if (chkL4.Checked)
                {
                    foreach (var s32Data in allS32DataDict.Values)
                    {
                        int segStartX = s32Data.SegInfo.nLinBeginX;
                        int segStartY = s32Data.SegInfo.nLinBeginY;

                        int removedCount = s32Data.Layer4.RemoveAll(obj =>
                        {
                            int objGlobalX = segStartX + obj.X / 2;
                            int objGlobalY = segStartY + obj.Y;
                            return selectedGlobalCells.Contains((objGlobalX, objGlobalY));
                        });

                        if (removedCount > 0)
                        {
                            totalL4 += removedCount;
                            modifiedS32s.Add(s32Data);
                        }
                    }
                }

                // 標記修改
                foreach (var s32 in modifiedS32s)
                {
                    s32.IsModified = true;
                }

                // 組合結果訊息
                List<string> results = new List<string>();
                if (totalL1 > 0) results.Add($"L1:{totalL1}");
                if (totalL3 > 0) results.Add($"L3:{totalL3}");
                if (totalL4 > 0) results.Add($"L4:{totalL4}");
                if (totalL5 > 0) results.Add($"L5:{totalL5}");
                if (totalL7 > 0) results.Add($"L7:{totalL7}");
                if (totalL8 > 0) results.Add($"L8:{totalL8}");

                // 保存數量後清除選取
                int clearedCellCount = currentSelectedCells.Count;
                currentSelectedCells.Clear();
                selectedRegion = new Rectangle();

                RenderS32Map();

                if (results.Count > 0)
                {
                    this.toolStripStatusLabel1.Text = $"已清除 {clearedCellCount} 格的 {string.Join(", ", results)} 資料";
                }
                else
                {
                    this.toolStripStatusLabel1.Text = "選取區域沒有資料需要清除";
                }

                clearForm.Close();
            };

            clearForm.ShowDialog();
        }

        // 單格清除對話框
        private void ShowSingleCellClearDialog()
        {
            Form clearForm = new Form();
            clearForm.Text = "清除格子資料";
            clearForm.Size = new Size(350, 380);
            clearForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            clearForm.StartPosition = FormStartPosition.CenterParent;
            clearForm.MaximizeBox = false;
            clearForm.MinimizeBox = false;

            // 座標輸入
            Label lblCoord = new Label();
            lblCoord.Text = "輸入遊戲座標:";
            lblCoord.Location = new Point(20, 20);
            lblCoord.Size = new Size(100, 20);
            clearForm.Controls.Add(lblCoord);

            Label lblX = new Label();
            lblX.Text = "X:";
            lblX.Location = new Point(20, 50);
            lblX.Size = new Size(30, 20);
            clearForm.Controls.Add(lblX);

            TextBox txtX = new TextBox();
            txtX.Location = new Point(50, 48);
            txtX.Size = new Size(80, 20);
            if (selectedGameX >= 0) txtX.Text = selectedGameX.ToString();
            clearForm.Controls.Add(txtX);

            Label lblY = new Label();
            lblY.Text = "Y:";
            lblY.Location = new Point(150, 50);
            lblY.Size = new Size(30, 20);
            clearForm.Controls.Add(lblY);

            TextBox txtY = new TextBox();
            txtY.Location = new Point(180, 48);
            txtY.Size = new Size(80, 20);
            if (selectedGameY >= 0) txtY.Text = selectedGameY.ToString();
            clearForm.Controls.Add(txtY);

            // 選擇要清除的層
            Label lblLayers = new Label();
            lblLayers.Text = "選擇要清除的層:";
            lblLayers.Location = new Point(20, 90);
            lblLayers.Size = new Size(150, 20);
            clearForm.Controls.Add(lblLayers);

            CheckBox chkL1 = new CheckBox();
            chkL1.Text = "第1層 (地板)";
            chkL1.Location = new Point(30, 115);
            chkL1.Size = new Size(150, 20);
            chkL1.Checked = true;
            clearForm.Controls.Add(chkL1);

            CheckBox chkL3 = new CheckBox();
            chkL3.Text = "第3層 (屬性) - 設為可通行";
            chkL3.Location = new Point(30, 140);
            chkL3.Size = new Size(200, 20);
            chkL3.Checked = true;
            clearForm.Controls.Add(chkL3);

            CheckBox chkL4 = new CheckBox();
            chkL4.Text = "第4層 (物件)";
            chkL4.Location = new Point(30, 165);
            chkL4.Size = new Size(150, 20);
            chkL4.Checked = true;
            clearForm.Controls.Add(chkL4);

            CheckBox chkL5 = new CheckBox();
            chkL5.Text = "第5層 (透明圖塊)";
            chkL5.Location = new Point(30, 190);
            chkL5.Size = new Size(150, 20);
            clearForm.Controls.Add(chkL5);

            CheckBox chkL7 = new CheckBox();
            chkL7.Text = "第7層 (傳送點)";
            chkL7.Location = new Point(30, 215);
            chkL7.Size = new Size(150, 20);
            clearForm.Controls.Add(chkL7);

            CheckBox chkL8 = new CheckBox();
            chkL8.Text = "第8層 (特效)";
            chkL8.Location = new Point(30, 240);
            chkL8.Size = new Size(150, 20);
            clearForm.Controls.Add(chkL8);

            // 執行按鈕
            Button btnExecute = new Button();
            btnExecute.Text = "清除";
            btnExecute.Location = new Point(80, 290);
            btnExecute.Size = new Size(80, 30);
            clearForm.Controls.Add(btnExecute);

            Button btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.Location = new Point(180, 290);
            btnCancel.Size = new Size(80, 30);
            btnCancel.Click += (s, args) => clearForm.Close();
            clearForm.Controls.Add(btnCancel);

            btnExecute.Click += (s, args) =>
            {
                if (!int.TryParse(txtX.Text, out int gameX) || !int.TryParse(txtY.Text, out int gameY))
                {
                    MessageBox.Show("請輸入有效的座標", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 找到對應的 S32
                S32Data targetS32 = null;
                foreach (var s32Data in allS32DataDict.Values)
                {
                    if (gameX >= s32Data.SegInfo.nLinBeginX && gameX <= s32Data.SegInfo.nLinEndX &&
                        gameY >= s32Data.SegInfo.nLinBeginY && gameY <= s32Data.SegInfo.nLinEndY)
                    {
                        targetS32 = s32Data;
                        break;
                    }
                }

                if (targetS32 == null)
                {
                    MessageBox.Show($"找不到座標 ({gameX}, {gameY}) 對應的 S32 區塊", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 計算局部座標
                int layer3X = gameX - targetS32.SegInfo.nLinBeginX;
                int localY = gameY - targetS32.SegInfo.nLinBeginY;
                int layer1X = layer3X * 2;  // Layer1 是 128 寬

                List<string> clearedLayers = new List<string>();

                // 清除第1層
                if (chkL1.Checked && layer1X >= 0 && layer1X < 128 && localY >= 0 && localY < 64)
                {
                    // 清除兩個 Layer1 格子（因為 Layer1 是 128 寬，Layer3 是 64 寬）
                    targetS32.Layer1[localY, layer1X] = new TileCell { X = layer1X, Y = localY, TileId = 0, IndexId = 0 };
                    if (layer1X + 1 < 128)
                        targetS32.Layer1[localY, layer1X + 1] = new TileCell { X = layer1X + 1, Y = localY, TileId = 0, IndexId = 0 };
                    clearedLayers.Add("L1");
                }

                // 清除第3層（設為可通行）
                if (chkL3.Checked && layer3X >= 0 && layer3X < 64 && localY >= 0 && localY < 64)
                {
                    targetS32.Layer3[localY, layer3X] = new MapAttribute { Attribute1 = 0, Attribute2 = 0 };
                    clearedLayers.Add("L3");
                }

                // 清除第4層
                if (chkL4.Checked)
                {
                    int removedCount = targetS32.Layer4.RemoveAll(obj =>
                        obj.X / 2 == layer3X && obj.Y == localY);
                    if (removedCount > 0) clearedLayers.Add($"L4({removedCount})");
                }

                // 清除第5層
                if (chkL5.Checked)
                {
                    int removedCount = targetS32.Layer5.RemoveAll(item =>
                        item.X == layer3X && item.Y == localY);
                    if (removedCount > 0) clearedLayers.Add($"L5({removedCount})");
                }

                // 清除第7層
                if (chkL7.Checked)
                {
                    int removedCount = targetS32.Layer7.RemoveAll(item =>
                        item.X == layer3X && item.Y == localY);
                    if (removedCount > 0) clearedLayers.Add($"L7({removedCount})");
                }

                // 清除第8層
                if (chkL8.Checked)
                {
                    int removedCount = targetS32.Layer8.RemoveAll(item =>
                        item.X == layer3X && item.Y == localY);
                    if (removedCount > 0) clearedLayers.Add($"L8({removedCount})");
                }

                if (clearedLayers.Count > 0)
                {
                    targetS32.IsModified = true;
                    RenderS32Map();
                    this.toolStripStatusLabel1.Text = $"已清除格子 ({gameX}, {gameY}) 的 {string.Join(", ", clearedLayers)} 資料";
                }
                else
                {
                    this.toolStripStatusLabel1.Text = $"格子 ({gameX}, {gameY}) 沒有資料需要清除";
                }

                clearForm.Close();
            };

            clearForm.ShowDialog();
        }

        // 查看與管理第六層（使用的TileId）資料
        private void btnToolCheckL6_Click(object sender, EventArgs e)
        {
            if (allS32DataDict.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 收集有 Layer6 資料的 S32
            List<(string filePath, string fileName, List<int> items)> s32WithL6 =
                new List<(string, string, List<int>)>();

            foreach (var kvp in allS32DataDict)
            {
                string filePath = kvp.Key;
                string fileName = Path.GetFileName(kvp.Key);
                S32Data s32Data = kvp.Value;

                if (s32Data.Layer6.Count > 0)
                {
                    s32WithL6.Add((filePath, fileName, s32Data.Layer6.ToList()));
                }
            }

            // 顯示結果
            Form resultForm = new Form();
            resultForm.Text = $"L6 查看與管理 - {s32WithL6.Count} 個 S32 有資料";
            resultForm.Size = new Size(750, 600);
            resultForm.FormBorderStyle = FormBorderStyle.Sizable;
            resultForm.StartPosition = FormStartPosition.CenterParent;

            int totalItems = s32WithL6.Sum(x => x.items.Count);
            Label lblSummary = new Label();
            lblSummary.Text = $"共 {s32WithL6.Count} 個 S32 檔案有 Layer6（使用的TileId）資料，總計 {totalItems} 項。勾選要刪除的項目：";
            lblSummary.Location = new Point(10, 10);
            lblSummary.Size = new Size(710, 20);
            resultForm.Controls.Add(lblSummary);

            CheckedListBox clbItems = new CheckedListBox();
            clbItems.Location = new Point(10, 35);
            clbItems.Size = new Size(710, 380);
            clbItems.Font = new Font("Consolas", 9);
            clbItems.CheckOnClick = true;

            List<(string filePath, int tileId)> itemInfoList = new List<(string, int)>();

            if (s32WithL6.Count == 0)
            {
                clbItems.Items.Add("沒有任何 S32 檔案有 Layer6 資料");
                clbItems.Enabled = false;
            }
            else
            {
                foreach (var (filePath, fileName, items) in s32WithL6)
                {
                    foreach (var tileId in items.OrderBy(x => x))
                    {
                        string displayText = $"[{fileName}] TileId={tileId}";
                        clbItems.Items.Add(displayText);
                        itemInfoList.Add((filePath, tileId));
                    }
                }
            }
            resultForm.Controls.Add(clbItems);

            Button btnSelectAll = new Button();
            btnSelectAll.Text = "全選";
            btnSelectAll.Location = new Point(10, 425);
            btnSelectAll.Size = new Size(80, 30);
            btnSelectAll.Click += (s, args) =>
            {
                for (int i = 0; i < clbItems.Items.Count; i++)
                    clbItems.SetItemChecked(i, true);
            };
            resultForm.Controls.Add(btnSelectAll);

            Button btnDeselectAll = new Button();
            btnDeselectAll.Text = "取消全選";
            btnDeselectAll.Location = new Point(100, 425);
            btnDeselectAll.Size = new Size(80, 30);
            btnDeselectAll.Click += (s, args) =>
            {
                for (int i = 0; i < clbItems.Items.Count; i++)
                    clbItems.SetItemChecked(i, false);
            };
            resultForm.Controls.Add(btnDeselectAll);

            // 檢查與自動修復缺失的 TileId
            Button btnCheckMissing = new Button();
            btnCheckMissing.Text = "檢查缺失並自動修復";
            btnCheckMissing.Location = new Point(200, 425);
            btnCheckMissing.Size = new Size(150, 30);
            btnCheckMissing.Click += (s, args) =>
            {
                int totalFixed = 0;
                foreach (var kvp in allS32DataDict)
                {
                    S32Data s32Data = kvp.Value;
                    HashSet<int> layer6Set = new HashSet<int>(s32Data.Layer6);
                    bool modified = false;

                    // 加入 Layer1 缺少的 TileId
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 128; x++)
                        {
                            var cell = s32Data.Layer1[y, x];
                            if (cell != null && cell.TileId > 0 && !layer6Set.Contains(cell.TileId))
                            {
                                s32Data.Layer6.Add(cell.TileId);
                                layer6Set.Add(cell.TileId);
                                modified = true;
                                totalFixed++;
                            }
                        }
                    }

                    // 加入 Layer4 缺少的 TileId
                    foreach (var obj in s32Data.Layer4)
                    {
                        if (obj.TileId > 0 && !layer6Set.Contains(obj.TileId))
                        {
                            s32Data.Layer6.Add(obj.TileId);
                            layer6Set.Add(obj.TileId);
                            modified = true;
                            totalFixed++;
                        }
                    }

                    if (modified)
                    {
                        s32Data.IsModified = true;
                    }
                }

                if (totalFixed > 0)
                {
                    MessageBox.Show($"已將 {totalFixed} 個缺少的 TileId 加入到 Layer6。\n請記得儲存修改。",
                        "修復完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    resultForm.Close();
                }
                else
                {
                    MessageBox.Show("所有 S32 的 Layer6 都已完整，無需修復。", "檢查完成",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            resultForm.Controls.Add(btnCheckMissing);

            Button btnClearSelected = new Button();
            btnClearSelected.Text = "刪除勾選項目";
            btnClearSelected.Location = new Point(10, 465);
            btnClearSelected.Size = new Size(120, 35);
            btnClearSelected.BackColor = Color.LightCoral;
            btnClearSelected.Enabled = s32WithL6.Count > 0;
            btnClearSelected.Click += (s, args) =>
            {
                if (clbItems.CheckedIndices.Count == 0)
                {
                    MessageBox.Show("請先勾選要刪除的項目", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var confirmResult = MessageBox.Show(
                    $"確定要刪除勾選的 {clbItems.CheckedIndices.Count} 個 Layer6 項目嗎？\n\n注意：刪除 L6 中的 TileId 可能會影響遊戲顯示。",
                    "確認刪除",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes) return;

                Dictionary<string, List<int>> toRemove = new Dictionary<string, List<int>>();
                foreach (int idx in clbItems.CheckedIndices)
                {
                    var info = itemInfoList[idx];
                    if (!toRemove.ContainsKey(info.filePath))
                        toRemove[info.filePath] = new List<int>();
                    toRemove[info.filePath].Add(info.tileId);
                }

                int removedCount = 0;
                foreach (var kvp in toRemove)
                {
                    if (allS32DataDict.TryGetValue(kvp.Key, out S32Data s32Data))
                    {
                        foreach (var tileId in kvp.Value)
                        {
                            if (s32Data.Layer6.Remove(tileId))
                                removedCount++;
                        }
                        s32Data.IsModified = true;
                    }
                }

                MessageBox.Show($"已刪除 {removedCount} 個 Layer6 項目。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                resultForm.Close();
                RenderS32Map();
            };
            resultForm.Controls.Add(btnClearSelected);

            Button btnClearAll = new Button();
            btnClearAll.Text = "清除全部 L6";
            btnClearAll.Location = new Point(140, 465);
            btnClearAll.Size = new Size(120, 35);
            btnClearAll.BackColor = Color.Salmon;
            btnClearAll.Enabled = s32WithL6.Count > 0;
            btnClearAll.Click += (s, args) =>
            {
                var confirmResult = MessageBox.Show(
                    $"確定要清除所有 {totalItems} 個 Layer6 項目嗎？\n\n警告：這可能會嚴重影響遊戲顯示！",
                    "確認清除全部",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes) return;

                int removedCount = 0;
                foreach (var (filePath, fileName, items) in s32WithL6)
                {
                    if (allS32DataDict.TryGetValue(filePath, out S32Data s32Data))
                    {
                        removedCount += s32Data.Layer6.Count;
                        s32Data.Layer6.Clear();
                        s32Data.IsModified = true;
                    }
                }

                MessageBox.Show($"已清除 {removedCount} 個 Layer6 項目。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                resultForm.Close();
                RenderS32Map();
            };
            resultForm.Controls.Add(btnClearAll);

            Button btnClose = new Button();
            btnClose.Text = "關閉";
            btnClose.Location = new Point(630, 465);
            btnClose.Size = new Size(90, 35);
            btnClose.Click += (s, args) => resultForm.Close();
            resultForm.Controls.Add(btnClose);

            resultForm.Resize += (s, args) =>
            {
                clbItems.Size = new Size(resultForm.ClientSize.Width - 20, resultForm.ClientSize.Height - 130);
                btnSelectAll.Location = new Point(10, resultForm.ClientSize.Height - 85);
                btnDeselectAll.Location = new Point(100, resultForm.ClientSize.Height - 85);
                btnCheckMissing.Location = new Point(200, resultForm.ClientSize.Height - 85);
                btnClearSelected.Location = new Point(10, resultForm.ClientSize.Height - 45);
                btnClearAll.Location = new Point(140, resultForm.ClientSize.Height - 45);
                btnClose.Location = new Point(resultForm.ClientSize.Width - 100, resultForm.ClientSize.Height - 45);
            };

            resultForm.ShowDialog();
        }

        // 查看與編輯第八層（特效）資料
        private void btnToolCheckL8_Click(object sender, EventArgs e)
        {
            if (allS32DataDict.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 收集有 Layer8 資料的 S32
            List<(string filePath, string fileName, int count, List<Layer8Item> items)> s32WithL8 =
                new List<(string, string, int, List<Layer8Item>)>();

            foreach (var kvp in allS32DataDict)
            {
                string filePath = kvp.Key;
                string fileName = Path.GetFileName(kvp.Key);
                S32Data s32Data = kvp.Value;

                if (s32Data.Layer8.Count > 0)
                {
                    s32WithL8.Add((filePath, fileName, s32Data.Layer8.Count, s32Data.Layer8.ToList()));
                }
            }

            // 顯示結果
            Form resultForm = new Form();
            resultForm.Text = $"L8 檢查、編輯與清除 - {s32WithL8.Count} 個 S32 有資料";
            resultForm.Size = new Size(750, 600);
            resultForm.FormBorderStyle = FormBorderStyle.Sizable;
            resultForm.StartPosition = FormStartPosition.CenterParent;

            int totalItems = s32WithL8.Sum(x => x.count);
            Label lblSummary = new Label();
            lblSummary.Text = $"共 {s32WithL8.Count} 個 S32 檔案有 Layer8（特效）資料，總計 {totalItems} 項。選取項目後可編輯或刪除：";
            lblSummary.Location = new Point(10, 10);
            lblSummary.Size = new Size(710, 20);
            resultForm.Controls.Add(lblSummary);

            // 使用 ListView 來顯示詳細資訊
            ListView lvItems = new ListView();
            lvItems.Location = new Point(10, 35);
            lvItems.Size = new Size(710, 350);
            lvItems.Font = new Font("Consolas", 9);
            lvItems.View = View.Details;
            lvItems.FullRowSelect = true;
            lvItems.CheckBoxes = true;
            lvItems.Columns.Add("S32 檔案", 120);
            lvItems.Columns.Add("SprId", 70);
            lvItems.Columns.Add("X", 60);
            lvItems.Columns.Add("Y", 60);
            lvItems.Columns.Add("Unknown", 80);

            List<(string filePath, Layer8Item item)> itemInfoList = new List<(string, Layer8Item)>();

            if (s32WithL8.Count == 0)
            {
                lvItems.Items.Add(new ListViewItem("沒有任何 S32 檔案有 Layer8 資料"));
                lvItems.Enabled = false;
            }
            else
            {
                foreach (var (filePath, fileName, count, items) in s32WithL8)
                {
                    foreach (var item in items)
                    {
                        ListViewItem lvi = new ListViewItem(fileName);
                        lvi.SubItems.Add(item.SprId.ToString());
                        lvi.SubItems.Add(item.X.ToString());
                        lvi.SubItems.Add(item.Y.ToString());
                        lvi.SubItems.Add(item.Unknown.ToString());
                        lvi.Tag = (filePath, item);
                        lvItems.Items.Add(lvi);
                        itemInfoList.Add((filePath, item));
                    }
                }
            }
            resultForm.Controls.Add(lvItems);

            // 編輯區域
            GroupBox gbEdit = new GroupBox();
            gbEdit.Text = "編輯選取的項目";
            gbEdit.Location = new Point(10, 395);
            gbEdit.Size = new Size(710, 80);

            Label lblSprId = new Label { Text = "SprId:", Location = new Point(10, 28), Size = new Size(45, 20) };
            TextBox txtSprId = new TextBox { Location = new Point(60, 25), Size = new Size(80, 23) };
            Label lblX = new Label { Text = "X:", Location = new Point(155, 28), Size = new Size(20, 20) };
            TextBox txtX = new TextBox { Location = new Point(175, 25), Size = new Size(80, 23) };
            Label lblY = new Label { Text = "Y:", Location = new Point(270, 28), Size = new Size(20, 20) };
            TextBox txtY = new TextBox { Location = new Point(290, 25), Size = new Size(80, 23) };
            Label lblUnknown = new Label { Text = "Unknown:", Location = new Point(385, 28), Size = new Size(60, 20) };
            TextBox txtUnknown = new TextBox { Location = new Point(450, 25), Size = new Size(80, 23) };

            Button btnApplyEdit = new Button();
            btnApplyEdit.Text = "套用修改";
            btnApplyEdit.Location = new Point(550, 22);
            btnApplyEdit.Size = new Size(80, 28);
            btnApplyEdit.Click += (s, args) =>
            {
                if (lvItems.SelectedItems.Count != 1)
                {
                    MessageBox.Show("請選取一個項目進行編輯", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var lvi = lvItems.SelectedItems[0];
                var (filePath, item) = ((string, Layer8Item))lvi.Tag;

                if (!ushort.TryParse(txtSprId.Text, out ushort newSprId) ||
                    !ushort.TryParse(txtX.Text, out ushort newX) ||
                    !ushort.TryParse(txtY.Text, out ushort newY) ||
                    !int.TryParse(txtUnknown.Text, out int newUnknown))
                {
                    MessageBox.Show("請輸入有效的數值", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 更新資料
                item.SprId = newSprId;
                item.X = newX;
                item.Y = newY;
                item.Unknown = newUnknown;

                // 更新 ListView 顯示
                lvi.SubItems[1].Text = item.SprId.ToString();
                lvi.SubItems[2].Text = item.X.ToString();
                lvi.SubItems[3].Text = item.Y.ToString();
                lvi.SubItems[4].Text = item.Unknown.ToString();

                // 標記已修改
                if (allS32DataDict.TryGetValue(filePath, out S32Data s32Data))
                {
                    s32Data.IsModified = true;
                }

                MessageBox.Show("已套用修改。請記得儲存 S32 檔案。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // 新增項目按鈕
            Button btnAddNew = new Button();
            btnAddNew.Text = "新增";
            btnAddNew.Location = new Point(640, 22);
            btnAddNew.Size = new Size(60, 28);
            btnAddNew.Click += (s, args) =>
            {
                if (allS32DataDict.Count == 0)
                {
                    MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (!ushort.TryParse(txtSprId.Text, out ushort newSprId) ||
                    !ushort.TryParse(txtX.Text, out ushort newX) ||
                    !ushort.TryParse(txtY.Text, out ushort newY) ||
                    !int.TryParse(txtUnknown.Text, out int newUnknown))
                {
                    MessageBox.Show("請輸入有效的數值", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 選擇要加入的 S32 檔案
                var s32Files = allS32DataDict.Keys.Select(k => Path.GetFileName(k)).ToArray();
                Form selectForm = new Form();
                selectForm.Text = "選擇 S32 檔案";
                selectForm.Size = new Size(300, 150);
                selectForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                selectForm.StartPosition = FormStartPosition.CenterParent;

                Label lblSelect = new Label { Text = "選擇要新增 Layer8 項目的 S32 檔案:", Location = new Point(10, 15), Size = new Size(260, 20) };
                ComboBox cmbS32 = new ComboBox { Location = new Point(10, 40), Size = new Size(260, 23), DropDownStyle = ComboBoxStyle.DropDownList };
                cmbS32.Items.AddRange(s32Files);
                if (cmbS32.Items.Count > 0) cmbS32.SelectedIndex = 0;

                Button btnOK = new Button { Text = "確定", Location = new Point(100, 75), Size = new Size(80, 28), DialogResult = DialogResult.OK };
                selectForm.Controls.AddRange(new Control[] { lblSelect, cmbS32, btnOK });
                selectForm.AcceptButton = btnOK;

                if (selectForm.ShowDialog() == DialogResult.OK && cmbS32.SelectedIndex >= 0)
                {
                    string selectedFileName = cmbS32.SelectedItem.ToString();
                    string selectedFilePath = allS32DataDict.Keys.FirstOrDefault(k => Path.GetFileName(k) == selectedFileName);

                    if (selectedFilePath != null && allS32DataDict.TryGetValue(selectedFilePath, out S32Data s32Data))
                    {
                        Layer8Item newItem = new Layer8Item
                        {
                            SprId = newSprId,
                            X = newX,
                            Y = newY,
                            Unknown = newUnknown
                        };
                        s32Data.Layer8.Add(newItem);
                        s32Data.IsModified = true;

                        // 更新 ListView
                        ListViewItem lvi = new ListViewItem(selectedFileName);
                        lvi.SubItems.Add(newItem.SprId.ToString());
                        lvi.SubItems.Add(newItem.X.ToString());
                        lvi.SubItems.Add(newItem.Y.ToString());
                        lvi.SubItems.Add(newItem.Unknown.ToString());
                        lvi.Tag = (selectedFilePath, newItem);
                        lvItems.Items.Add(lvi);
                        itemInfoList.Add((selectedFilePath, newItem));

                        MessageBox.Show($"已新增 Layer8 項目到 {selectedFileName}。\n\n請記得儲存 S32 檔案。", "完成",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };

            gbEdit.Controls.AddRange(new Control[] { lblSprId, txtSprId, lblX, txtX, lblY, txtY, lblUnknown, txtUnknown, btnApplyEdit, btnAddNew });
            resultForm.Controls.Add(gbEdit);

            // 選取項目時填入編輯區
            lvItems.SelectedIndexChanged += (s, args) =>
            {
                if (lvItems.SelectedItems.Count == 1)
                {
                    var lvi = lvItems.SelectedItems[0];
                    var (filePath, item) = ((string, Layer8Item))lvi.Tag;
                    txtSprId.Text = item.SprId.ToString();
                    txtX.Text = item.X.ToString();
                    txtY.Text = item.Y.ToString();
                    txtUnknown.Text = item.Unknown.ToString();
                }
            };

            Button btnSelectAll = new Button();
            btnSelectAll.Text = "全選";
            btnSelectAll.Location = new Point(10, 485);
            btnSelectAll.Size = new Size(80, 30);
            btnSelectAll.Click += (s, args) =>
            {
                foreach (ListViewItem lvi in lvItems.Items)
                    lvi.Checked = true;
            };
            resultForm.Controls.Add(btnSelectAll);

            Button btnDeselectAll = new Button();
            btnDeselectAll.Text = "取消全選";
            btnDeselectAll.Location = new Point(100, 485);
            btnDeselectAll.Size = new Size(80, 30);
            btnDeselectAll.Click += (s, args) =>
            {
                foreach (ListViewItem lvi in lvItems.Items)
                    lvi.Checked = false;
            };
            resultForm.Controls.Add(btnDeselectAll);

            Button btnClearSelected = new Button();
            btnClearSelected.Text = "刪除勾選項目";
            btnClearSelected.Location = new Point(10, 525);
            btnClearSelected.Size = new Size(120, 35);
            btnClearSelected.BackColor = Color.LightCoral;
            btnClearSelected.Enabled = s32WithL8.Count > 0;
            btnClearSelected.Click += (s, args) =>
            {
                int checkedCount = lvItems.CheckedItems.Count;
                if (checkedCount == 0)
                {
                    MessageBox.Show("請先勾選要刪除的項目", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var confirmResult = MessageBox.Show(
                    $"確定要刪除勾選的 {checkedCount} 個 Layer8 項目嗎？",
                    "確認刪除",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes) return;

                Dictionary<string, List<Layer8Item>> toRemove = new Dictionary<string, List<Layer8Item>>();
                foreach (ListViewItem lvi in lvItems.CheckedItems)
                {
                    var (filePath, item) = ((string, Layer8Item))lvi.Tag;
                    if (!toRemove.ContainsKey(filePath))
                        toRemove[filePath] = new List<Layer8Item>();
                    toRemove[filePath].Add(item);
                }

                int removedCount = 0;
                foreach (var kvp in toRemove)
                {
                    if (allS32DataDict.TryGetValue(kvp.Key, out S32Data s32Data))
                    {
                        foreach (var item in kvp.Value)
                        {
                            if (s32Data.Layer8.Remove(item))
                                removedCount++;
                        }
                        s32Data.IsModified = true;
                    }
                }

                MessageBox.Show($"已刪除 {removedCount} 個 Layer8 項目。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                resultForm.Close();
                RenderS32Map();
            };
            resultForm.Controls.Add(btnClearSelected);

            Button btnClearAll = new Button();
            btnClearAll.Text = "刪除全部 L8";
            btnClearAll.Location = new Point(140, 525);
            btnClearAll.Size = new Size(120, 35);
            btnClearAll.BackColor = Color.Salmon;
            btnClearAll.Enabled = s32WithL8.Count > 0;
            btnClearAll.Click += (s, args) =>
            {
                var confirmResult = MessageBox.Show(
                    $"確定要刪除所有 {totalItems} 個 Layer8 項目嗎？",
                    "確認刪除全部",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes) return;

                int removedCount = 0;
                foreach (var (filePath, fileName, count, items) in s32WithL8)
                {
                    if (allS32DataDict.TryGetValue(filePath, out S32Data s32Data))
                    {
                        removedCount += s32Data.Layer8.Count;
                        s32Data.Layer8.Clear();
                        s32Data.IsModified = true;
                    }
                }

                MessageBox.Show($"已刪除 {removedCount} 個 Layer8 項目。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                resultForm.Close();
                RenderS32Map();
            };
            resultForm.Controls.Add(btnClearAll);

            Button btnClose = new Button();
            btnClose.Text = "關閉";
            btnClose.Location = new Point(630, 525);
            btnClose.Size = new Size(90, 35);
            btnClose.Click += (s, args) => resultForm.Close();
            resultForm.Controls.Add(btnClose);

            resultForm.Resize += (s, args) =>
            {
                lvItems.Size = new Size(resultForm.ClientSize.Width - 20, resultForm.ClientSize.Height - 210);
                gbEdit.Location = new Point(10, resultForm.ClientSize.Height - 165);
                gbEdit.Size = new Size(resultForm.ClientSize.Width - 20, 80);
                btnSelectAll.Location = new Point(10, resultForm.ClientSize.Height - 75);
                btnDeselectAll.Location = new Point(100, resultForm.ClientSize.Height - 75);
                btnClearSelected.Location = new Point(10, resultForm.ClientSize.Height - 35);
                btnClearAll.Location = new Point(140, resultForm.ClientSize.Height - 35);
                btnClose.Location = new Point(resultForm.ClientSize.Width - 100, resultForm.ClientSize.Height - 35);
            };

            resultForm.ShowDialog();
        }

        // 查看與編輯第一層（地板圖塊）資料
        private void btnToolCheckL1_Click(object sender, EventArgs e)
        {
            if (allS32DataDict.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 顯示結果
            Form resultForm = new Form();
            resultForm.Text = $"L1 檢查與編輯 - {allS32DataDict.Count} 個 S32 檔案";
            resultForm.Size = new Size(750, 550);
            resultForm.FormBorderStyle = FormBorderStyle.Sizable;
            resultForm.StartPosition = FormStartPosition.CenterParent;

            Label lblSummary = new Label();
            lblSummary.Text = $"共 {allS32DataDict.Count} 個 S32 檔案。選擇 S32 檔案後可編輯指定座標的 Layer1 資料：";
            lblSummary.Location = new Point(10, 10);
            lblSummary.Size = new Size(710, 20);
            resultForm.Controls.Add(lblSummary);

            // S32 檔案選擇清單
            Label lblS32 = new Label { Text = "S32 檔案:", Location = new Point(10, 40), Size = new Size(70, 20) };
            resultForm.Controls.Add(lblS32);

            ComboBox cmbS32Files = new ComboBox();
            cmbS32Files.Location = new Point(85, 37);
            cmbS32Files.Size = new Size(200, 23);
            cmbS32Files.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var kvp in allS32DataDict)
            {
                cmbS32Files.Items.Add(Path.GetFileName(kvp.Key));
            }
            if (cmbS32Files.Items.Count > 0)
                cmbS32Files.SelectedIndex = 0;
            resultForm.Controls.Add(cmbS32Files);

            // 座標輸入
            Label lblLocX = new Label { Text = "X:", Location = new Point(300, 40), Size = new Size(20, 20) };
            TextBox txtLocX = new TextBox { Location = new Point(325, 37), Size = new Size(50, 23), Text = "0" };
            Label lblLocY = new Label { Text = "Y:", Location = new Point(385, 40), Size = new Size(20, 20) };
            TextBox txtLocY = new TextBox { Location = new Point(410, 37), Size = new Size(50, 23), Text = "0" };
            resultForm.Controls.AddRange(new Control[] { lblLocX, txtLocX, lblLocY, txtLocY });

            Button btnQuery = new Button();
            btnQuery.Text = "查詢";
            btnQuery.Location = new Point(470, 35);
            btnQuery.Size = new Size(60, 27);
            resultForm.Controls.Add(btnQuery);

            // 結果顯示區
            GroupBox gbResult = new GroupBox();
            gbResult.Text = "查詢結果 / 編輯區域";
            gbResult.Location = new Point(10, 75);
            gbResult.Size = new Size(710, 120);

            Label lblResultInfo = new Label { Text = "請選擇 S32 檔案並輸入座標後點擊「查詢」", Location = new Point(10, 25), Size = new Size(400, 20) };
            gbResult.Controls.Add(lblResultInfo);

            Label lblTileId = new Label { Text = "TileId:", Location = new Point(10, 55), Size = new Size(50, 20) };
            TextBox txtTileId = new TextBox { Location = new Point(65, 52), Size = new Size(80, 23), Enabled = false };
            Label lblIndexId = new Label { Text = "IndexId:", Location = new Point(160, 55), Size = new Size(55, 20) };
            TextBox txtIndexId = new TextBox { Location = new Point(220, 52), Size = new Size(80, 23), Enabled = false };
            gbResult.Controls.AddRange(new Control[] { lblTileId, txtTileId, lblIndexId, txtIndexId });

            Button btnApplyEdit = new Button();
            btnApplyEdit.Text = "套用修改";
            btnApplyEdit.Location = new Point(320, 50);
            btnApplyEdit.Size = new Size(80, 28);
            btnApplyEdit.Enabled = false;
            gbResult.Controls.Add(btnApplyEdit);

            resultForm.Controls.Add(gbResult);

            // 批量修改區域
            GroupBox gbBatch = new GroupBox();
            gbBatch.Text = "批量替換 TileId";
            gbBatch.Location = new Point(10, 205);
            gbBatch.Size = new Size(710, 80);

            Label lblOldTileId = new Label { Text = "原 TileId:", Location = new Point(10, 30), Size = new Size(60, 20) };
            TextBox txtOldTileId = new TextBox { Location = new Point(75, 27), Size = new Size(80, 23) };
            Label lblNewTileId = new Label { Text = "新 TileId:", Location = new Point(170, 30), Size = new Size(60, 20) };
            TextBox txtNewTileId = new TextBox { Location = new Point(235, 27), Size = new Size(80, 23) };
            Label lblBatchScope = new Label { Text = "範圍:", Location = new Point(330, 30), Size = new Size(40, 20) };
            ComboBox cmbBatchScope = new ComboBox { Location = new Point(375, 27), Size = new Size(150, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbBatchScope.Items.Add("當前選擇的 S32");
            cmbBatchScope.Items.Add("所有 S32 檔案");
            cmbBatchScope.SelectedIndex = 0;

            Button btnBatchReplace = new Button();
            btnBatchReplace.Text = "批量替換";
            btnBatchReplace.Location = new Point(540, 25);
            btnBatchReplace.Size = new Size(80, 28);
            btnBatchReplace.BackColor = Color.LightYellow;

            gbBatch.Controls.AddRange(new Control[] { lblOldTileId, txtOldTileId, lblNewTileId, txtNewTileId, lblBatchScope, cmbBatchScope, btnBatchReplace });
            resultForm.Controls.Add(gbBatch);

            // 統計資訊
            GroupBox gbStats = new GroupBox();
            gbStats.Text = "統計資訊";
            gbStats.Location = new Point(10, 295);
            gbStats.Size = new Size(710, 160);

            ListView lvStats = new ListView();
            lvStats.Location = new Point(10, 20);
            lvStats.Size = new Size(690, 130);
            lvStats.Font = new Font("Consolas", 9);
            lvStats.View = View.Details;
            lvStats.FullRowSelect = true;
            lvStats.Columns.Add("TileId", 80);
            lvStats.Columns.Add("使用次數", 80);
            lvStats.Columns.Add("IndexId", 80);
            gbStats.Controls.Add(lvStats);

            resultForm.Controls.Add(gbStats);

            Button btnClose = new Button();
            btnClose.Text = "關閉";
            btnClose.Location = new Point(630, 465);
            btnClose.Size = new Size(90, 35);
            btnClose.Click += (s, args) => resultForm.Close();
            resultForm.Controls.Add(btnClose);

            // 當前查詢的 TileCell
            TileCell currentCell = null;
            string currentFilePath = null;
            int currentX = -1, currentY = -1;

            // 更新統計資訊
            Action updateStats = () =>
            {
                lvStats.Items.Clear();
                if (cmbS32Files.SelectedItem == null) return;

                string selectedFileName = cmbS32Files.SelectedItem.ToString();
                string selectedFilePath = allS32DataDict.Keys.FirstOrDefault(k => Path.GetFileName(k) == selectedFileName);
                if (selectedFilePath == null || !allS32DataDict.TryGetValue(selectedFilePath, out S32Data s32Data)) return;

                // 統計 TileId 使用次數
                Dictionary<int, (int count, int indexId)> tileStats = new Dictionary<int, (int, int)>();
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        var cell = s32Data.Layer1[y, x];
                        if (cell != null)
                        {
                            if (!tileStats.ContainsKey(cell.TileId))
                                tileStats[cell.TileId] = (1, cell.IndexId);
                            else
                                tileStats[cell.TileId] = (tileStats[cell.TileId].count + 1, cell.IndexId);
                        }
                    }
                }

                // 排序顯示
                foreach (var kvp in tileStats.OrderByDescending(k => k.Value.count))
                {
                    ListViewItem lvi = new ListViewItem(kvp.Key.ToString());
                    lvi.SubItems.Add(kvp.Value.count.ToString());
                    lvi.SubItems.Add(kvp.Value.indexId.ToString());
                    lvStats.Items.Add(lvi);
                }
            };

            // 查詢按鈕事件
            btnQuery.Click += (s, args) =>
            {
                if (cmbS32Files.SelectedItem == null)
                {
                    MessageBox.Show("請選擇 S32 檔案", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (!int.TryParse(txtLocX.Text, out int locX) || !int.TryParse(txtLocY.Text, out int locY))
                {
                    MessageBox.Show("請輸入有效的座標", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (locX < 0 || locX >= 128 || locY < 0 || locY >= 64)
                {
                    MessageBox.Show("座標超出範圍。X 範圍: 0-127, Y 範圍: 0-63", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string selectedFileName = cmbS32Files.SelectedItem.ToString();
                string selectedFilePath = allS32DataDict.Keys.FirstOrDefault(k => Path.GetFileName(k) == selectedFileName);
                if (selectedFilePath == null || !allS32DataDict.TryGetValue(selectedFilePath, out S32Data s32Data))
                {
                    MessageBox.Show("無法載入 S32 資料", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var cell = s32Data.Layer1[locY, locX];
                if (cell == null)
                {
                    lblResultInfo.Text = $"座標 ({locX}, {locY}) 無資料";
                    txtTileId.Text = "";
                    txtIndexId.Text = "";
                    txtTileId.Enabled = false;
                    txtIndexId.Enabled = false;
                    btnApplyEdit.Enabled = false;
                    currentCell = null;
                }
                else
                {
                    lblResultInfo.Text = $"座標 ({locX}, {locY}) 的 Layer1 資料：";
                    txtTileId.Text = cell.TileId.ToString();
                    txtIndexId.Text = cell.IndexId.ToString();
                    txtTileId.Enabled = true;
                    txtIndexId.Enabled = true;
                    btnApplyEdit.Enabled = true;
                    currentCell = cell;
                    currentFilePath = selectedFilePath;
                    currentX = locX;
                    currentY = locY;
                }
            };

            // 套用修改按鈕事件
            btnApplyEdit.Click += (s, args) =>
            {
                if (currentCell == null || currentFilePath == null)
                {
                    MessageBox.Show("請先查詢一個有效的座標", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (!int.TryParse(txtTileId.Text, out int newTileId) || !int.TryParse(txtIndexId.Text, out int newIndexId))
                {
                    MessageBox.Show("請輸入有效的數值", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                currentCell.TileId = newTileId;
                currentCell.IndexId = newIndexId;

                if (allS32DataDict.TryGetValue(currentFilePath, out S32Data s32Data))
                {
                    s32Data.IsModified = true;
                }

                MessageBox.Show($"已修改座標 ({currentX}, {currentY}) 的 Layer1 資料。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                updateStats();
            };

            // 批量替換按鈕事件
            btnBatchReplace.Click += (s, args) =>
            {
                if (!int.TryParse(txtOldTileId.Text, out int oldTileId) || !int.TryParse(txtNewTileId.Text, out int newTileId))
                {
                    MessageBox.Show("請輸入有效的 TileId", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                bool allFiles = cmbBatchScope.SelectedIndex == 1;
                int replacedCount = 0;

                if (allFiles)
                {
                    var confirmResult = MessageBox.Show(
                        $"確定要在所有 S32 檔案中將 TileId {oldTileId} 替換為 {newTileId} 嗎？",
                        "確認批量替換",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (confirmResult != DialogResult.Yes) return;

                    foreach (var kvp in allS32DataDict)
                    {
                        S32Data s32Data = kvp.Value;
                        for (int y = 0; y < 64; y++)
                        {
                            for (int x = 0; x < 128; x++)
                            {
                                var cell = s32Data.Layer1[y, x];
                                if (cell != null && cell.TileId == oldTileId)
                                {
                                    cell.TileId = newTileId;
                                    replacedCount++;
                                }
                            }
                        }
                        if (replacedCount > 0)
                            s32Data.IsModified = true;
                    }
                }
                else
                {
                    if (cmbS32Files.SelectedItem == null)
                    {
                        MessageBox.Show("請選擇 S32 檔案", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    string selectedFileName = cmbS32Files.SelectedItem.ToString();
                    string selectedFilePath = allS32DataDict.Keys.FirstOrDefault(k => Path.GetFileName(k) == selectedFileName);
                    if (selectedFilePath == null || !allS32DataDict.TryGetValue(selectedFilePath, out S32Data s32Data))
                    {
                        MessageBox.Show("無法載入 S32 資料", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var confirmResult = MessageBox.Show(
                        $"確定要在 {selectedFileName} 中將 TileId {oldTileId} 替換為 {newTileId} 嗎？",
                        "確認批量替換",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (confirmResult != DialogResult.Yes) return;

                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 128; x++)
                        {
                            var cell = s32Data.Layer1[y, x];
                            if (cell != null && cell.TileId == oldTileId)
                            {
                                cell.TileId = newTileId;
                                replacedCount++;
                            }
                        }
                    }
                    if (replacedCount > 0)
                        s32Data.IsModified = true;
                }

                MessageBox.Show($"已替換 {replacedCount} 個 Layer1 項目。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                updateStats();
                RenderS32Map();
            };

            // S32 檔案選擇變更時更新統計
            cmbS32Files.SelectedIndexChanged += (s, args) =>
            {
                updateStats();
                // 清除查詢結果
                lblResultInfo.Text = "請選擇 S32 檔案並輸入座標後點擊「查詢」";
                txtTileId.Text = "";
                txtIndexId.Text = "";
                txtTileId.Enabled = false;
                txtIndexId.Enabled = false;
                btnApplyEdit.Enabled = false;
                currentCell = null;
            };

            // 初始載入統計
            updateStats();

            resultForm.Resize += (s, args) =>
            {
                gbResult.Size = new Size(resultForm.ClientSize.Width - 20, 120);
                gbBatch.Size = new Size(resultForm.ClientSize.Width - 20, 80);
                gbBatch.Location = new Point(10, 205);
                gbStats.Size = new Size(resultForm.ClientSize.Width - 20, resultForm.ClientSize.Height - 350);
                gbStats.Location = new Point(10, 295);
                lvStats.Size = new Size(gbStats.Width - 20, gbStats.Height - 30);
                btnClose.Location = new Point(resultForm.ClientSize.Width - 100, resultForm.ClientSize.Height - 45);
            };

            resultForm.ShowDialog();
        }

        // 查看與編輯第四層（物件）資料
        private void btnToolCheckL4_Click(object sender, EventArgs e)
        {
            if (allS32DataDict.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 收集所有 Layer4 資料
            List<(string filePath, string fileName, List<ObjectTile> items)> s32WithL4 =
                new List<(string, string, List<ObjectTile>)>();

            foreach (var kvp in allS32DataDict)
            {
                string filePath = kvp.Key;
                string fileName = Path.GetFileName(kvp.Key);
                S32Data s32Data = kvp.Value;

                if (s32Data.Layer4.Count > 0)
                {
                    s32WithL4.Add((filePath, fileName, s32Data.Layer4.ToList()));
                }
            }

            // 顯示結果
            Form resultForm = new Form();
            resultForm.Text = $"L4 檢查、編輯與清除 - {s32WithL4.Count} 個 S32 有資料";
            resultForm.Size = new Size(900, 650);
            resultForm.FormBorderStyle = FormBorderStyle.Sizable;
            resultForm.StartPosition = FormStartPosition.CenterParent;

            int totalItems = s32WithL4.Sum(x => x.items.Count);
            Label lblSummary = new Label();
            lblSummary.Text = $"共 {s32WithL4.Count} 個 S32 檔案有 Layer4（物件）資料，總計 {totalItems} 項。選取項目後可編輯或刪除：";
            lblSummary.Location = new Point(10, 10);
            lblSummary.Size = new Size(860, 20);
            resultForm.Controls.Add(lblSummary);

            // 使用 ListView 來顯示詳細資訊
            ListView lvItems = new ListView();
            lvItems.Location = new Point(10, 35);
            lvItems.Size = new Size(860, 380);
            lvItems.Font = new Font("Consolas", 9);
            lvItems.View = View.Details;
            lvItems.FullRowSelect = true;
            lvItems.CheckBoxes = true;
            lvItems.Columns.Add("S32 檔案", 100);
            lvItems.Columns.Add("GroupId", 65);
            lvItems.Columns.Add("X", 50);
            lvItems.Columns.Add("Y", 50);
            lvItems.Columns.Add("Layer", 50);
            lvItems.Columns.Add("IndexId", 65);
            lvItems.Columns.Add("TileId", 65);

            List<(string filePath, ObjectTile item)> itemInfoList = new List<(string, ObjectTile)>();

            if (s32WithL4.Count == 0)
            {
                lvItems.Items.Add(new ListViewItem("沒有任何 S32 檔案有 Layer4 資料"));
                lvItems.Enabled = false;
            }
            else
            {
                foreach (var (filePath, fileName, items) in s32WithL4)
                {
                    foreach (var item in items)
                    {
                        ListViewItem lvi = new ListViewItem(fileName);
                        lvi.SubItems.Add(item.GroupId.ToString());
                        lvi.SubItems.Add(item.X.ToString());
                        lvi.SubItems.Add(item.Y.ToString());
                        lvi.SubItems.Add(item.Layer.ToString());
                        lvi.SubItems.Add(item.IndexId.ToString());
                        lvi.SubItems.Add(item.TileId.ToString());
                        lvi.Tag = (filePath, item);
                        lvItems.Items.Add(lvi);
                        itemInfoList.Add((filePath, item));
                    }
                }
            }
            resultForm.Controls.Add(lvItems);

            // 編輯區域
            GroupBox gbEdit = new GroupBox();
            gbEdit.Text = "編輯選取的項目";
            gbEdit.Location = new Point(10, 425);
            gbEdit.Size = new Size(860, 80);

            Label lblGroupId = new Label { Text = "GroupId:", Location = new Point(10, 25), Size = new Size(55, 20) };
            TextBox txtGroupId = new TextBox { Location = new Point(70, 22), Size = new Size(60, 23) };
            Label lblX = new Label { Text = "X:", Location = new Point(140, 25), Size = new Size(20, 20) };
            TextBox txtX = new TextBox { Location = new Point(160, 22), Size = new Size(50, 23) };
            Label lblY = new Label { Text = "Y:", Location = new Point(220, 25), Size = new Size(20, 20) };
            TextBox txtY = new TextBox { Location = new Point(240, 22), Size = new Size(50, 23) };
            Label lblLayer = new Label { Text = "Layer:", Location = new Point(300, 25), Size = new Size(40, 20) };
            TextBox txtLayer = new TextBox { Location = new Point(345, 22), Size = new Size(50, 23) };
            Label lblIndexId = new Label { Text = "IndexId:", Location = new Point(405, 25), Size = new Size(50, 20) };
            TextBox txtIndexId = new TextBox { Location = new Point(460, 22), Size = new Size(60, 23) };
            Label lblTileId = new Label { Text = "TileId:", Location = new Point(530, 25), Size = new Size(40, 20) };
            TextBox txtTileId = new TextBox { Location = new Point(575, 22), Size = new Size(70, 23) };

            Button btnApplyEdit = new Button();
            btnApplyEdit.Text = "套用修改";
            btnApplyEdit.Location = new Point(660, 20);
            btnApplyEdit.Size = new Size(80, 28);
            btnApplyEdit.Click += (s, args) =>
            {
                if (lvItems.SelectedItems.Count != 1)
                {
                    MessageBox.Show("請選取一個項目進行編輯", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var lvi = lvItems.SelectedItems[0];
                var (filePath, item) = ((string, ObjectTile))lvi.Tag;

                if (!int.TryParse(txtGroupId.Text, out int newGroupId) ||
                    !int.TryParse(txtX.Text, out int newX) ||
                    !int.TryParse(txtY.Text, out int newY) ||
                    !int.TryParse(txtLayer.Text, out int newLayer) ||
                    !int.TryParse(txtIndexId.Text, out int newIndexId) ||
                    !int.TryParse(txtTileId.Text, out int newTileId))
                {
                    MessageBox.Show("請輸入有效的數值", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 更新資料
                item.GroupId = newGroupId;
                item.X = newX;
                item.Y = newY;
                item.Layer = newLayer;
                item.IndexId = newIndexId;
                item.TileId = newTileId;

                // 更新 ListView 顯示
                lvi.SubItems[1].Text = item.GroupId.ToString();
                lvi.SubItems[2].Text = item.X.ToString();
                lvi.SubItems[3].Text = item.Y.ToString();
                lvi.SubItems[4].Text = item.Layer.ToString();
                lvi.SubItems[5].Text = item.IndexId.ToString();
                lvi.SubItems[6].Text = item.TileId.ToString();

                // 標記已修改
                if (allS32DataDict.TryGetValue(filePath, out S32Data s32Data))
                {
                    s32Data.IsModified = true;
                }

                MessageBox.Show("已套用修改。請記得儲存 S32 檔案。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // 新增項目按鈕
            Button btnAddNew = new Button();
            btnAddNew.Text = "新增";
            btnAddNew.Location = new Point(760, 20);
            btnAddNew.Size = new Size(80, 28);
            btnAddNew.Click += (s, args) =>
            {
                // 選擇要新增到哪個 S32 檔案
                Form selectForm = new Form();
                selectForm.Text = "選擇 S32 檔案";
                selectForm.Size = new Size(400, 350);
                selectForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                selectForm.StartPosition = FormStartPosition.CenterParent;
                selectForm.MaximizeBox = false;
                selectForm.MinimizeBox = false;

                Label lblSelect = new Label();
                lblSelect.Text = "選擇要新增 Layer4 項目的 S32 檔案：";
                lblSelect.Location = new Point(10, 10);
                lblSelect.Size = new Size(360, 20);
                selectForm.Controls.Add(lblSelect);

                ListBox lbS32Files = new ListBox();
                lbS32Files.Location = new Point(10, 35);
                lbS32Files.Size = new Size(360, 220);
                foreach (var kvp in allS32DataDict)
                {
                    lbS32Files.Items.Add(Path.GetFileName(kvp.Key));
                }
                if (lbS32Files.Items.Count > 0)
                    lbS32Files.SelectedIndex = 0;
                selectForm.Controls.Add(lbS32Files);

                Button btnOK = new Button();
                btnOK.Text = "確定";
                btnOK.Location = new Point(100, 265);
                btnOK.Size = new Size(80, 30);
                btnOK.DialogResult = DialogResult.OK;
                selectForm.Controls.Add(btnOK);

                Button btnCancel = new Button();
                btnCancel.Text = "取消";
                btnCancel.Location = new Point(200, 265);
                btnCancel.Size = new Size(80, 30);
                btnCancel.DialogResult = DialogResult.Cancel;
                selectForm.Controls.Add(btnCancel);

                selectForm.AcceptButton = btnOK;
                selectForm.CancelButton = btnCancel;

                if (selectForm.ShowDialog() == DialogResult.OK && lbS32Files.SelectedItem != null)
                {
                    string selectedFileName = lbS32Files.SelectedItem.ToString();
                    string selectedFilePath = allS32DataDict.Keys.FirstOrDefault(k => Path.GetFileName(k) == selectedFileName);

                    if (selectedFilePath != null && allS32DataDict.TryGetValue(selectedFilePath, out S32Data s32Data))
                    {
                        // 驗證輸入
                        if (!int.TryParse(txtGroupId.Text, out int newGroupId))
                            newGroupId = 0;
                        if (!int.TryParse(txtX.Text, out int newX))
                            newX = 0;
                        if (!int.TryParse(txtY.Text, out int newY))
                            newY = 0;
                        if (!int.TryParse(txtLayer.Text, out int newLayer))
                            newLayer = 0;
                        if (!int.TryParse(txtIndexId.Text, out int newIndexId))
                            newIndexId = 0;
                        if (!int.TryParse(txtTileId.Text, out int newTileId))
                            newTileId = 0;

                        // 建立新項目
                        ObjectTile newItem = new ObjectTile
                        {
                            GroupId = newGroupId,
                            X = newX,
                            Y = newY,
                            Layer = newLayer,
                            IndexId = newIndexId,
                            TileId = newTileId
                        };

                        // 加入 S32 資料
                        s32Data.Layer4.Add(newItem);
                        s32Data.IsModified = true;

                        // 更新 ListView
                        ListViewItem newLvi = new ListViewItem(selectedFileName);
                        newLvi.SubItems.Add(newItem.GroupId.ToString());
                        newLvi.SubItems.Add(newItem.X.ToString());
                        newLvi.SubItems.Add(newItem.Y.ToString());
                        newLvi.SubItems.Add(newItem.Layer.ToString());
                        newLvi.SubItems.Add(newItem.IndexId.ToString());
                        newLvi.SubItems.Add(newItem.TileId.ToString());
                        newLvi.Tag = (selectedFilePath, newItem);
                        lvItems.Items.Add(newLvi);
                        itemInfoList.Add((selectedFilePath, newItem));

                        // 選取新增的項目
                        newLvi.Selected = true;
                        newLvi.EnsureVisible();

                        MessageBox.Show($"已新增 Layer4 項目到 {selectedFileName}。\n\n請記得儲存 S32 檔案。", "完成",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };

            gbEdit.Controls.AddRange(new Control[] { lblGroupId, txtGroupId, lblX, txtX, lblY, txtY, lblLayer, txtLayer, lblIndexId, txtIndexId, lblTileId, txtTileId, btnApplyEdit, btnAddNew });
            resultForm.Controls.Add(gbEdit);

            // 選取項目時填入編輯區
            lvItems.SelectedIndexChanged += (s, args) =>
            {
                if (lvItems.SelectedItems.Count == 1)
                {
                    var lvi = lvItems.SelectedItems[0];
                    var (filePath, item) = ((string, ObjectTile))lvi.Tag;
                    txtGroupId.Text = item.GroupId.ToString();
                    txtX.Text = item.X.ToString();
                    txtY.Text = item.Y.ToString();
                    txtLayer.Text = item.Layer.ToString();
                    txtIndexId.Text = item.IndexId.ToString();
                    txtTileId.Text = item.TileId.ToString();
                }
            };

            Button btnSelectAll = new Button();
            btnSelectAll.Text = "全選";
            btnSelectAll.Location = new Point(10, 515);
            btnSelectAll.Size = new Size(80, 30);
            btnSelectAll.Click += (s, args) =>
            {
                foreach (ListViewItem lvi in lvItems.Items)
                    lvi.Checked = true;
            };
            resultForm.Controls.Add(btnSelectAll);

            Button btnDeselectAll = new Button();
            btnDeselectAll.Text = "取消全選";
            btnDeselectAll.Location = new Point(100, 515);
            btnDeselectAll.Size = new Size(80, 30);
            btnDeselectAll.Click += (s, args) =>
            {
                foreach (ListViewItem lvi in lvItems.Items)
                    lvi.Checked = false;
            };
            resultForm.Controls.Add(btnDeselectAll);

            Button btnClearSelected = new Button();
            btnClearSelected.Text = "刪除勾選項目";
            btnClearSelected.Location = new Point(10, 555);
            btnClearSelected.Size = new Size(120, 35);
            btnClearSelected.BackColor = Color.LightCoral;
            btnClearSelected.Enabled = s32WithL4.Count > 0;
            btnClearSelected.Click += (s, args) =>
            {
                int checkedCount = lvItems.CheckedItems.Count;
                if (checkedCount == 0)
                {
                    MessageBox.Show("請先勾選要刪除的項目", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var confirmResult = MessageBox.Show(
                    $"確定要刪除勾選的 {checkedCount} 個 Layer4 項目嗎？",
                    "確認刪除",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes) return;

                Dictionary<string, List<ObjectTile>> toRemove = new Dictionary<string, List<ObjectTile>>();
                foreach (ListViewItem lvi in lvItems.CheckedItems)
                {
                    var (filePath, item) = ((string, ObjectTile))lvi.Tag;
                    if (!toRemove.ContainsKey(filePath))
                        toRemove[filePath] = new List<ObjectTile>();
                    toRemove[filePath].Add(item);
                }

                int removedCount = 0;
                foreach (var kvp in toRemove)
                {
                    if (allS32DataDict.TryGetValue(kvp.Key, out S32Data s32Data))
                    {
                        foreach (var item in kvp.Value)
                        {
                            if (s32Data.Layer4.Remove(item))
                                removedCount++;
                        }
                        s32Data.IsModified = true;
                    }
                }

                MessageBox.Show($"已刪除 {removedCount} 個 Layer4 項目。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                resultForm.Close();
                RenderS32Map();
            };
            resultForm.Controls.Add(btnClearSelected);

            Button btnClearAll = new Button();
            btnClearAll.Text = "刪除全部 L4";
            btnClearAll.Location = new Point(140, 555);
            btnClearAll.Size = new Size(120, 35);
            btnClearAll.BackColor = Color.Salmon;
            btnClearAll.Enabled = s32WithL4.Count > 0;
            btnClearAll.Click += (s, args) =>
            {
                var confirmResult = MessageBox.Show(
                    $"確定要刪除所有 {totalItems} 個 Layer4 項目嗎？",
                    "確認刪除全部",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes) return;

                int removedCount = 0;
                foreach (var (filePath, fileName, items) in s32WithL4)
                {
                    if (allS32DataDict.TryGetValue(filePath, out S32Data s32Data))
                    {
                        removedCount += s32Data.Layer4.Count;
                        s32Data.Layer4.Clear();
                        s32Data.IsModified = true;
                    }
                }

                MessageBox.Show($"已刪除 {removedCount} 個 Layer4 項目。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                resultForm.Close();
                RenderS32Map();
            };
            resultForm.Controls.Add(btnClearAll);

            Button btnClose = new Button();
            btnClose.Text = "關閉";
            btnClose.Location = new Point(780, 555);
            btnClose.Size = new Size(90, 35);
            btnClose.Click += (s, args) => resultForm.Close();
            resultForm.Controls.Add(btnClose);

            resultForm.Resize += (s, args) =>
            {
                lvItems.Size = new Size(resultForm.ClientSize.Width - 20, resultForm.ClientSize.Height - 230);
                gbEdit.Location = new Point(10, resultForm.ClientSize.Height - 185);
                gbEdit.Size = new Size(resultForm.ClientSize.Width - 20, 80);
                btnSelectAll.Location = new Point(10, resultForm.ClientSize.Height - 95);
                btnDeselectAll.Location = new Point(100, resultForm.ClientSize.Height - 95);
                btnClearSelected.Location = new Point(10, resultForm.ClientSize.Height - 55);
                btnClearAll.Location = new Point(140, resultForm.ClientSize.Height - 55);
                btnClose.Location = new Point(resultForm.ClientSize.Width - 100, resultForm.ClientSize.Height - 55);
            };

            resultForm.ShowDialog();
        }

        // 查看與管理第五層（透明圖塊）資料
        private void btnToolCheckL5_Click(object sender, EventArgs e)
        {
            if (allS32DataDict.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 收集有 Layer5 資料的 S32
            List<(string filePath, string fileName, int count, List<Layer5Item> items)> s32WithL5 =
                new List<(string, string, int, List<Layer5Item>)>();

            foreach (var kvp in allS32DataDict)
            {
                string filePath = kvp.Key;
                string fileName = Path.GetFileName(kvp.Key);
                S32Data s32Data = kvp.Value;

                if (s32Data.Layer5.Count > 0)
                {
                    s32WithL5.Add((filePath, fileName, s32Data.Layer5.Count, s32Data.Layer5.ToList()));
                }
            }

            // 顯示結果
            Form resultForm = new Form();
            resultForm.Text = $"L5 檢查與清除 - {s32WithL5.Count} 個 S32 有資料";
            resultForm.Size = new Size(700, 550);
            resultForm.FormBorderStyle = FormBorderStyle.Sizable;
            resultForm.StartPosition = FormStartPosition.CenterParent;

            int totalItems = s32WithL5.Sum(x => x.count);
            Label lblSummary = new Label();
            lblSummary.Text = $"共 {s32WithL5.Count} 個 S32 檔案有 Layer5（透明圖塊）資料，總計 {totalItems} 項。勾選要清除的項目：";
            lblSummary.Location = new Point(10, 10);
            lblSummary.Size = new Size(660, 20);
            resultForm.Controls.Add(lblSummary);

            CheckedListBox clbItems = new CheckedListBox();
            clbItems.Location = new Point(10, 35);
            clbItems.Size = new Size(660, 380);
            clbItems.Font = new Font("Consolas", 9);
            clbItems.CheckOnClick = true;

            List<(string filePath, int itemIndex, Layer5Item item)> itemInfoList =
                new List<(string, int, Layer5Item)>();

            if (s32WithL5.Count == 0)
            {
                clbItems.Items.Add("沒有任何 S32 檔案有 Layer5 資料");
                clbItems.Enabled = false;
            }
            else
            {
                foreach (var (filePath, fileName, count, items) in s32WithL5)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        string displayText = $"[{fileName}] X={item.X}, Y={item.Y}, RGB=({item.R},{item.G},{item.B})";
                        clbItems.Items.Add(displayText);
                        itemInfoList.Add((filePath, i, item));
                    }
                }
            }
            resultForm.Controls.Add(clbItems);

            Button btnSelectAll = new Button();
            btnSelectAll.Text = "全選";
            btnSelectAll.Location = new Point(10, 425);
            btnSelectAll.Size = new Size(80, 30);
            btnSelectAll.Click += (s, args) =>
            {
                for (int i = 0; i < clbItems.Items.Count; i++)
                    clbItems.SetItemChecked(i, true);
            };
            resultForm.Controls.Add(btnSelectAll);

            Button btnDeselectAll = new Button();
            btnDeselectAll.Text = "取消全選";
            btnDeselectAll.Location = new Point(100, 425);
            btnDeselectAll.Size = new Size(80, 30);
            btnDeselectAll.Click += (s, args) =>
            {
                for (int i = 0; i < clbItems.Items.Count; i++)
                    clbItems.SetItemChecked(i, false);
            };
            resultForm.Controls.Add(btnDeselectAll);

            Button btnClearSelected = new Button();
            btnClearSelected.Text = "清除勾選項目";
            btnClearSelected.Location = new Point(10, 465);
            btnClearSelected.Size = new Size(120, 35);
            btnClearSelected.BackColor = Color.LightCoral;
            btnClearSelected.Enabled = s32WithL5.Count > 0;
            btnClearSelected.Click += (s, args) =>
            {
                if (clbItems.CheckedIndices.Count == 0)
                {
                    MessageBox.Show("請先勾選要清除的項目", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var confirmResult = MessageBox.Show(
                    $"確定要清除勾選的 {clbItems.CheckedIndices.Count} 個 Layer5 項目嗎？",
                    "確認清除",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes) return;

                Dictionary<string, List<Layer5Item>> toRemove = new Dictionary<string, List<Layer5Item>>();
                foreach (int idx in clbItems.CheckedIndices)
                {
                    var info = itemInfoList[idx];
                    if (!toRemove.ContainsKey(info.filePath))
                        toRemove[info.filePath] = new List<Layer5Item>();
                    toRemove[info.filePath].Add(info.item);
                }

                int removedCount = 0;
                foreach (var kvp in toRemove)
                {
                    if (allS32DataDict.TryGetValue(kvp.Key, out S32Data s32Data))
                    {
                        foreach (var item in kvp.Value)
                        {
                            if (s32Data.Layer5.Remove(item))
                                removedCount++;
                        }
                        s32Data.IsModified = true;
                    }
                }

                MessageBox.Show($"已清除 {removedCount} 個 Layer5 項目。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                resultForm.Close();
                RenderS32Map();
            };
            resultForm.Controls.Add(btnClearSelected);

            Button btnClearAll = new Button();
            btnClearAll.Text = "清除全部 L5";
            btnClearAll.Location = new Point(140, 465);
            btnClearAll.Size = new Size(120, 35);
            btnClearAll.BackColor = Color.Salmon;
            btnClearAll.Enabled = s32WithL5.Count > 0;
            btnClearAll.Click += (s, args) =>
            {
                var confirmResult = MessageBox.Show(
                    $"確定要清除所有 {totalItems} 個 Layer5 項目嗎？",
                    "確認清除全部",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes) return;

                int removedCount = 0;
                foreach (var (filePath, fileName, count, items) in s32WithL5)
                {
                    if (allS32DataDict.TryGetValue(filePath, out S32Data s32Data))
                    {
                        removedCount += s32Data.Layer5.Count;
                        s32Data.Layer5.Clear();
                        s32Data.IsModified = true;
                    }
                }

                MessageBox.Show($"已清除 {removedCount} 個 Layer5 項目。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                resultForm.Close();
                RenderS32Map();
            };
            resultForm.Controls.Add(btnClearAll);

            Button btnClose = new Button();
            btnClose.Text = "關閉";
            btnClose.Location = new Point(580, 465);
            btnClose.Size = new Size(90, 35);
            btnClose.Click += (s, args) => resultForm.Close();
            resultForm.Controls.Add(btnClose);

            resultForm.Resize += (s, args) =>
            {
                clbItems.Size = new Size(resultForm.ClientSize.Width - 20, resultForm.ClientSize.Height - 130);
                btnSelectAll.Location = new Point(10, resultForm.ClientSize.Height - 85);
                btnDeselectAll.Location = new Point(100, resultForm.ClientSize.Height - 85);
                btnClearSelected.Location = new Point(10, resultForm.ClientSize.Height - 45);
                btnClearAll.Location = new Point(140, resultForm.ClientSize.Height - 45);
                btnClose.Location = new Point(resultForm.ClientSize.Width - 100, resultForm.ClientSize.Height - 45);
            };

            resultForm.ShowDialog();
        }

        // 查看與管理第七層（傳送點）資料 - 支援編輯
        private void btnToolCheckL7_Click(object sender, EventArgs e)
        {
            if (allS32DataDict.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 收集有 Layer7 資料的 S32
            List<(string filePath, string fileName, int count, List<Layer7Item> items)> s32WithL7 =
                new List<(string, string, int, List<Layer7Item>)>();

            foreach (var kvp in allS32DataDict)
            {
                string filePath = kvp.Key;
                string fileName = Path.GetFileName(kvp.Key);
                S32Data s32Data = kvp.Value;

                if (s32Data.Layer7.Count > 0)
                {
                    s32WithL7.Add((filePath, fileName, s32Data.Layer7.Count, s32Data.Layer7.ToList()));
                }
            }

            // 顯示結果
            Form resultForm = new Form();
            resultForm.Text = $"L7 檢查、編輯與清除 - {s32WithL7.Count} 個 S32 有資料";
            resultForm.Size = new Size(800, 600);
            resultForm.FormBorderStyle = FormBorderStyle.Sizable;
            resultForm.StartPosition = FormStartPosition.CenterParent;

            int totalItems = s32WithL7.Sum(x => x.count);
            Label lblSummary = new Label();
            lblSummary.Text = $"共 {s32WithL7.Count} 個 S32 檔案有 Layer7（傳送點）資料，總計 {totalItems} 項。選取項目後可編輯或刪除：";
            lblSummary.Location = new Point(10, 10);
            lblSummary.Size = new Size(760, 20);
            resultForm.Controls.Add(lblSummary);

            // 使用 ListView 來顯示詳細資訊
            ListView lvItems = new ListView();
            lvItems.Location = new Point(10, 35);
            lvItems.Size = new Size(760, 350);
            lvItems.Font = new Font("Consolas", 9);
            lvItems.View = View.Details;
            lvItems.FullRowSelect = true;
            lvItems.CheckBoxes = true;
            lvItems.Columns.Add("S32 檔案", 120);
            lvItems.Columns.Add("名稱", 150);
            lvItems.Columns.Add("X", 50);
            lvItems.Columns.Add("Y", 50);
            lvItems.Columns.Add("目標地圖", 80);
            lvItems.Columns.Add("PortalId", 80);

            List<(string filePath, Layer7Item item)> itemInfoList = new List<(string, Layer7Item)>();

            if (s32WithL7.Count == 0)
            {
                lvItems.Items.Add(new ListViewItem("沒有任何 S32 檔案有 Layer7 資料"));
                lvItems.Enabled = false;
            }
            else
            {
                foreach (var (filePath, fileName, count, items) in s32WithL7)
                {
                    foreach (var item in items)
                    {
                        ListViewItem lvi = new ListViewItem(fileName);
                        lvi.SubItems.Add(item.Name);
                        lvi.SubItems.Add(item.X.ToString());
                        lvi.SubItems.Add(item.Y.ToString());
                        lvi.SubItems.Add(item.TargetMapId.ToString());
                        lvi.SubItems.Add(item.PortalId.ToString());
                        lvi.Tag = (filePath, item);
                        lvItems.Items.Add(lvi);
                        itemInfoList.Add((filePath, item));
                    }
                }
            }
            resultForm.Controls.Add(lvItems);

            // 編輯區域
            GroupBox gbEdit = new GroupBox();
            gbEdit.Text = "編輯選取的項目";
            gbEdit.Location = new Point(10, 395);
            gbEdit.Size = new Size(760, 80);

            Label lblName = new Label { Text = "名稱:", Location = new Point(10, 25), Size = new Size(40, 20) };
            TextBox txtName = new TextBox { Location = new Point(55, 22), Size = new Size(120, 23) };
            Label lblX = new Label { Text = "X:", Location = new Point(185, 25), Size = new Size(20, 20) };
            TextBox txtX = new TextBox { Location = new Point(205, 22), Size = new Size(50, 23) };
            Label lblY = new Label { Text = "Y:", Location = new Point(265, 25), Size = new Size(20, 20) };
            TextBox txtY = new TextBox { Location = new Point(285, 22), Size = new Size(50, 23) };
            Label lblTarget = new Label { Text = "目標地圖:", Location = new Point(345, 25), Size = new Size(60, 20) };
            TextBox txtTarget = new TextBox { Location = new Point(410, 22), Size = new Size(60, 23) };
            Label lblPortal = new Label { Text = "PortalId:", Location = new Point(480, 25), Size = new Size(55, 20) };
            TextBox txtPortal = new TextBox { Location = new Point(540, 22), Size = new Size(60, 23) };

            Button btnApplyEdit = new Button();
            btnApplyEdit.Text = "套用修改";
            btnApplyEdit.Location = new Point(620, 20);
            btnApplyEdit.Size = new Size(80, 28);
            btnApplyEdit.Click += (s, args) =>
            {
                if (lvItems.SelectedItems.Count != 1)
                {
                    MessageBox.Show("請選取一個項目進行編輯", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var lvi = lvItems.SelectedItems[0];
                var (filePath, item) = ((string, Layer7Item))lvi.Tag;

                if (!byte.TryParse(txtX.Text, out byte newX) ||
                    !byte.TryParse(txtY.Text, out byte newY) ||
                    !ushort.TryParse(txtTarget.Text, out ushort newTarget) ||
                    !int.TryParse(txtPortal.Text, out int newPortal))
                {
                    MessageBox.Show("請輸入有效的數值", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 更新資料
                item.Name = txtName.Text;
                item.X = newX;
                item.Y = newY;
                item.TargetMapId = newTarget;
                item.PortalId = newPortal;

                // 更新 ListView 顯示
                lvi.SubItems[1].Text = item.Name;
                lvi.SubItems[2].Text = item.X.ToString();
                lvi.SubItems[3].Text = item.Y.ToString();
                lvi.SubItems[4].Text = item.TargetMapId.ToString();
                lvi.SubItems[5].Text = item.PortalId.ToString();

                // 標記已修改
                if (allS32DataDict.TryGetValue(filePath, out S32Data s32Data))
                {
                    s32Data.IsModified = true;
                }

                MessageBox.Show("已套用修改。請記得儲存 S32 檔案。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // 新增項目按鈕
            Button btnAddNew = new Button();
            btnAddNew.Text = "新增";
            btnAddNew.Location = new Point(620, 52);
            btnAddNew.Size = new Size(80, 28);
            btnAddNew.Click += (s, args) =>
            {
                // 選擇要新增到哪個 S32 檔案
                Form selectForm = new Form();
                selectForm.Text = "選擇 S32 檔案";
                selectForm.Size = new Size(400, 350);
                selectForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                selectForm.StartPosition = FormStartPosition.CenterParent;
                selectForm.MaximizeBox = false;
                selectForm.MinimizeBox = false;

                Label lblSelect = new Label();
                lblSelect.Text = "選擇要新增 Layer7 項目的 S32 檔案：";
                lblSelect.Location = new Point(10, 10);
                lblSelect.Size = new Size(360, 20);
                selectForm.Controls.Add(lblSelect);

                ListBox lbS32Files = new ListBox();
                lbS32Files.Location = new Point(10, 35);
                lbS32Files.Size = new Size(360, 220);
                foreach (var kvp in allS32DataDict)
                {
                    lbS32Files.Items.Add(Path.GetFileName(kvp.Key));
                }
                if (lbS32Files.Items.Count > 0)
                    lbS32Files.SelectedIndex = 0;
                selectForm.Controls.Add(lbS32Files);

                Button btnOK = new Button();
                btnOK.Text = "確定";
                btnOK.Location = new Point(100, 265);
                btnOK.Size = new Size(80, 30);
                btnOK.DialogResult = DialogResult.OK;
                selectForm.Controls.Add(btnOK);

                Button btnCancel = new Button();
                btnCancel.Text = "取消";
                btnCancel.Location = new Point(200, 265);
                btnCancel.Size = new Size(80, 30);
                btnCancel.DialogResult = DialogResult.Cancel;
                selectForm.Controls.Add(btnCancel);

                selectForm.AcceptButton = btnOK;
                selectForm.CancelButton = btnCancel;

                if (selectForm.ShowDialog() == DialogResult.OK && lbS32Files.SelectedItem != null)
                {
                    string selectedFileName = lbS32Files.SelectedItem.ToString();
                    string selectedFilePath = allS32DataDict.Keys.FirstOrDefault(k => Path.GetFileName(k) == selectedFileName);

                    if (selectedFilePath != null && allS32DataDict.TryGetValue(selectedFilePath, out S32Data s32Data))
                    {
                        // 驗證輸入
                        if (!byte.TryParse(txtX.Text, out byte newX))
                            newX = 0;
                        if (!byte.TryParse(txtY.Text, out byte newY))
                            newY = 0;
                        if (!ushort.TryParse(txtTarget.Text, out ushort newTarget))
                            newTarget = 0;
                        if (!int.TryParse(txtPortal.Text, out int newPortal))
                            newPortal = 0;

                        // 建立新項目
                        Layer7Item newItem = new Layer7Item
                        {
                            Name = string.IsNullOrEmpty(txtName.Text) ? "NewPortal" : txtName.Text,
                            X = newX,
                            Y = newY,
                            TargetMapId = newTarget,
                            PortalId = newPortal
                        };

                        // 加入 S32 資料
                        s32Data.Layer7.Add(newItem);
                        s32Data.IsModified = true;

                        // 更新 ListView
                        ListViewItem newLvi = new ListViewItem(selectedFileName);
                        newLvi.SubItems.Add(newItem.Name);
                        newLvi.SubItems.Add(newItem.X.ToString());
                        newLvi.SubItems.Add(newItem.Y.ToString());
                        newLvi.SubItems.Add(newItem.TargetMapId.ToString());
                        newLvi.SubItems.Add(newItem.PortalId.ToString());
                        newLvi.Tag = (selectedFilePath, newItem);
                        lvItems.Items.Add(newLvi);
                        itemInfoList.Add((selectedFilePath, newItem));

                        // 選取新增的項目
                        newLvi.Selected = true;
                        newLvi.EnsureVisible();

                        MessageBox.Show($"已新增 Layer7 項目到 {selectedFileName}。\n\n請記得儲存 S32 檔案。", "完成",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };

            gbEdit.Controls.AddRange(new Control[] { lblName, txtName, lblX, txtX, lblY, txtY, lblTarget, txtTarget, lblPortal, txtPortal, btnApplyEdit, btnAddNew });
            resultForm.Controls.Add(gbEdit);

            // 選取項目時填入編輯區
            lvItems.SelectedIndexChanged += (s, args) =>
            {
                if (lvItems.SelectedItems.Count == 1)
                {
                    var lvi = lvItems.SelectedItems[0];
                    var (filePath, item) = ((string, Layer7Item))lvi.Tag;
                    txtName.Text = item.Name;
                    txtX.Text = item.X.ToString();
                    txtY.Text = item.Y.ToString();
                    txtTarget.Text = item.TargetMapId.ToString();
                    txtPortal.Text = item.PortalId.ToString();
                }
            };

            Button btnSelectAll = new Button();
            btnSelectAll.Text = "全選";
            btnSelectAll.Location = new Point(10, 485);
            btnSelectAll.Size = new Size(80, 30);
            btnSelectAll.Click += (s, args) =>
            {
                foreach (ListViewItem lvi in lvItems.Items)
                    lvi.Checked = true;
            };
            resultForm.Controls.Add(btnSelectAll);

            Button btnDeselectAll = new Button();
            btnDeselectAll.Text = "取消全選";
            btnDeselectAll.Location = new Point(100, 485);
            btnDeselectAll.Size = new Size(80, 30);
            btnDeselectAll.Click += (s, args) =>
            {
                foreach (ListViewItem lvi in lvItems.Items)
                    lvi.Checked = false;
            };
            resultForm.Controls.Add(btnDeselectAll);

            Button btnClearSelected = new Button();
            btnClearSelected.Text = "刪除勾選項目";
            btnClearSelected.Location = new Point(10, 525);
            btnClearSelected.Size = new Size(120, 35);
            btnClearSelected.BackColor = Color.LightCoral;
            btnClearSelected.Enabled = s32WithL7.Count > 0;
            btnClearSelected.Click += (s, args) =>
            {
                int checkedCount = lvItems.CheckedItems.Count;
                if (checkedCount == 0)
                {
                    MessageBox.Show("請先勾選要刪除的項目", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var confirmResult = MessageBox.Show(
                    $"確定要刪除勾選的 {checkedCount} 個 Layer7 項目嗎？",
                    "確認刪除",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes) return;

                Dictionary<string, List<Layer7Item>> toRemove = new Dictionary<string, List<Layer7Item>>();
                foreach (ListViewItem lvi in lvItems.CheckedItems)
                {
                    var (filePath, item) = ((string, Layer7Item))lvi.Tag;
                    if (!toRemove.ContainsKey(filePath))
                        toRemove[filePath] = new List<Layer7Item>();
                    toRemove[filePath].Add(item);
                }

                int removedCount = 0;
                foreach (var kvp in toRemove)
                {
                    if (allS32DataDict.TryGetValue(kvp.Key, out S32Data s32Data))
                    {
                        foreach (var item in kvp.Value)
                        {
                            if (s32Data.Layer7.Remove(item))
                                removedCount++;
                        }
                        s32Data.IsModified = true;
                    }
                }

                MessageBox.Show($"已刪除 {removedCount} 個 Layer7 項目。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                resultForm.Close();
                RenderS32Map();
            };
            resultForm.Controls.Add(btnClearSelected);

            Button btnClearAll = new Button();
            btnClearAll.Text = "刪除全部 L7";
            btnClearAll.Location = new Point(140, 525);
            btnClearAll.Size = new Size(120, 35);
            btnClearAll.BackColor = Color.Salmon;
            btnClearAll.Enabled = s32WithL7.Count > 0;
            btnClearAll.Click += (s, args) =>
            {
                var confirmResult = MessageBox.Show(
                    $"確定要刪除所有 {totalItems} 個 Layer7 項目嗎？",
                    "確認刪除全部",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes) return;

                int removedCount = 0;
                foreach (var (filePath, fileName, count, items) in s32WithL7)
                {
                    if (allS32DataDict.TryGetValue(filePath, out S32Data s32Data))
                    {
                        removedCount += s32Data.Layer7.Count;
                        s32Data.Layer7.Clear();
                        s32Data.IsModified = true;
                    }
                }

                MessageBox.Show($"已刪除 {removedCount} 個 Layer7 項目。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                resultForm.Close();
                RenderS32Map();
            };
            resultForm.Controls.Add(btnClearAll);

            Button btnClose = new Button();
            btnClose.Text = "關閉";
            btnClose.Location = new Point(680, 525);
            btnClose.Size = new Size(90, 35);
            btnClose.Click += (s, args) => resultForm.Close();
            resultForm.Controls.Add(btnClose);

            resultForm.Resize += (s, args) =>
            {
                lvItems.Size = new Size(resultForm.ClientSize.Width - 20, resultForm.ClientSize.Height - 210);
                gbEdit.Location = new Point(10, resultForm.ClientSize.Height - 165);
                gbEdit.Size = new Size(resultForm.ClientSize.Width - 20, 80);
                btnSelectAll.Location = new Point(10, resultForm.ClientSize.Height - 75);
                btnDeselectAll.Location = new Point(100, resultForm.ClientSize.Height - 75);
                btnClearSelected.Location = new Point(10, resultForm.ClientSize.Height - 35);
                btnClearAll.Location = new Point(140, resultForm.ClientSize.Height - 35);
                btnClose.Location = new Point(resultForm.ClientSize.Width - 100, resultForm.ClientSize.Height - 35);
            };

            resultForm.ShowDialog();
        }

        private void btnToolAddS32_Click(object sender, EventArgs e)
        {
            // 檢查是否已載入地圖
            if (string.IsNullOrEmpty(currentMapId) || !Share.MapDataList.ContainsKey(currentMapId))
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Struct.L1Map currentMap = Share.MapDataList[currentMapId];

            // 創建新增 S32 對話框
            Form addS32Form = new Form();
            addS32Form.Text = "新增 S32 區塊";
            addS32Form.Size = new Size(400, 280);
            addS32Form.FormBorderStyle = FormBorderStyle.FixedDialog;
            addS32Form.StartPosition = FormStartPosition.CenterParent;
            addS32Form.MaximizeBox = false;
            addS32Form.MinimizeBox = false;

            // 說明
            Label lblInfo = new Label();
            lblInfo.Text = "輸入要新增的 S32 區塊座標（BlockX, BlockY）\n或輸入遊戲座標自動計算";
            lblInfo.Location = new Point(20, 15);
            lblInfo.Size = new Size(350, 35);
            addS32Form.Controls.Add(lblInfo);

            // BlockX
            Label lblBlockX = new Label();
            lblBlockX.Text = "BlockX (16進位):";
            lblBlockX.Location = new Point(20, 60);
            lblBlockX.Size = new Size(100, 20);
            addS32Form.Controls.Add(lblBlockX);

            TextBox txtBlockX = new TextBox();
            txtBlockX.Location = new Point(130, 58);
            txtBlockX.Size = new Size(80, 20);
            txtBlockX.Text = "7FFF";
            addS32Form.Controls.Add(txtBlockX);

            // BlockY
            Label lblBlockY = new Label();
            lblBlockY.Text = "BlockY (16進位):";
            lblBlockY.Location = new Point(20, 90);
            lblBlockY.Size = new Size(100, 20);
            addS32Form.Controls.Add(lblBlockY);

            TextBox txtBlockY = new TextBox();
            txtBlockY.Location = new Point(130, 88);
            txtBlockY.Size = new Size(80, 20);
            txtBlockY.Text = "8000";
            addS32Form.Controls.Add(txtBlockY);

            // 分隔線
            Label lblSeparator = new Label();
            lblSeparator.Text = "── 或用遊戲座標計算 ──";
            lblSeparator.Location = new Point(20, 120);
            lblSeparator.Size = new Size(350, 20);
            lblSeparator.ForeColor = Color.Gray;
            addS32Form.Controls.Add(lblSeparator);

            // 遊戲座標 X
            Label lblGameX = new Label();
            lblGameX.Text = "遊戲座標 X:";
            lblGameX.Location = new Point(20, 150);
            lblGameX.Size = new Size(100, 20);
            addS32Form.Controls.Add(lblGameX);

            TextBox txtGameX = new TextBox();
            txtGameX.Location = new Point(130, 148);
            txtGameX.Size = new Size(80, 20);
            addS32Form.Controls.Add(txtGameX);

            // 遊戲座標 Y
            Label lblGameY = new Label();
            lblGameY.Text = "遊戲座標 Y:";
            lblGameY.Location = new Point(220, 150);
            lblGameY.Size = new Size(80, 20);
            addS32Form.Controls.Add(lblGameY);

            TextBox txtGameY = new TextBox();
            txtGameY.Location = new Point(300, 148);
            txtGameY.Size = new Size(80, 20);
            addS32Form.Controls.Add(txtGameY);

            // 計算按鈕
            Button btnCalc = new Button();
            btnCalc.Text = "計算";
            btnCalc.Location = new Point(230, 58);
            btnCalc.Size = new Size(60, 50);
            addS32Form.Controls.Add(btnCalc);

            // 新增按鈕
            Button btnAdd = new Button();
            btnAdd.Text = "新增";
            btnAdd.Location = new Point(100, 195);
            btnAdd.Size = new Size(80, 30);
            addS32Form.Controls.Add(btnAdd);

            // 取消按鈕
            Button btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.Location = new Point(200, 195);
            btnCancel.Size = new Size(80, 30);
            btnCancel.Click += (s, args) => addS32Form.Close();
            addS32Form.Controls.Add(btnCancel);

            // 計算功能：從遊戲座標計算 BlockX, BlockY
            btnCalc.Click += (s, args) =>
            {
                if (!int.TryParse(txtGameX.Text, out int gameX) ||
                    !int.TryParse(txtGameY.Text, out int gameY))
                {
                    MessageBox.Show("請輸入有效的遊戲座標", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 從遊戲座標計算 BlockX, BlockY
                // 公式來自 L1MapSeg: nLinEndX = (nBlockX - 0x7fff) * 64 + 0x7fff
                // 反推: nBlockX = (nLinEndX - 0x7fff) / 64 + 0x7fff
                // 而 nLinEndX = nLinBeginX + 63，所以 nLinBeginX 在某個 block 內時
                // blockX = ((gameX - 0x7fff) / 64) + 0x7fff + (if gameX > 0x7fff then 1 else 0)
                int blockX = ((gameX - 0x7FFF - 1) / 64) + 0x8000;
                int blockY = ((gameY - 0x7FFF - 1) / 64) + 0x8000;

                txtBlockX.Text = blockX.ToString("X4");
                txtBlockY.Text = blockY.ToString("X4");
            };

            // 新增功能
            btnAdd.Click += (s, args) =>
            {
                int blockX, blockY;
                try
                {
                    blockX = Convert.ToInt32(txtBlockX.Text, 16);
                    blockY = Convert.ToInt32(txtBlockY.Text, 16);
                }
                catch
                {
                    MessageBox.Show("請輸入有效的 16 進位 BlockX 和 BlockY", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 檢查檔案是否已存在
                string fileName = $"{blockX:X4}{blockY:X4}.s32".ToLower();
                string filePath = Path.Combine(currentMap.szFullDirName, fileName);

                if (File.Exists(filePath))
                {
                    MessageBox.Show($"S32 檔案已存在: {fileName}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 檢查是否已在記憶體中
                if (allS32DataDict.Keys.Any(k => k.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show($"S32 檔案已載入: {fileName}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 確認新增
                var confirmResult = MessageBox.Show(
                    $"確定要新增 S32 區塊嗎？\n\n檔案名稱: {fileName}\nBlockX: {blockX:X4}, BlockY: {blockY:X4}\n路徑: {filePath}",
                    "確認新增",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult != DialogResult.Yes) return;

                // 創建空的 S32 資料
                S32Data newS32Data = CreateEmptyS32Data(blockX, blockY, filePath);

                // 創建 SegInfo
                Struct.L1MapSeg segInfo = new Struct.L1MapSeg(blockX, blockY, true);
                segInfo.isRemastered = false;
                segInfo.nMapMinBlockX = currentMap.nMinBlockX;
                segInfo.nMapMinBlockY = currentMap.nMinBlockY;
                segInfo.nMapBlockCountX = currentMap.nBlockCountX;

                newS32Data.SegInfo = segInfo;
                newS32Data.IsModified = true;

                // 加入到記憶體（先加入才能用 SaveS32File）
                allS32DataDict[filePath] = newS32Data;

                // 寫入檔案
                try
                {
                    SaveS32File(filePath);  // 使用正確格式的保存方法
                }
                catch (Exception ex)
                {
                    // 移除失敗的 S32
                    allS32DataDict.Remove(filePath);
                    MessageBox.Show($"寫入檔案失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 加入到 FullFileNameList
                currentMap.FullFileNameList[filePath] = segInfo;

                // 更新地圖邊界（如果新的 S32 超出現有邊界）
                if (blockX < currentMap.nMinBlockX || blockX > currentMap.nMaxBlockX ||
                    blockY < currentMap.nMinBlockY || blockY > currentMap.nMaxBlockY)
                {
                    currentMap.nMinBlockX = Math.Min(currentMap.nMinBlockX, blockX);
                    currentMap.nMinBlockY = Math.Min(currentMap.nMinBlockY, blockY);
                    currentMap.nMaxBlockX = Math.Max(currentMap.nMaxBlockX, blockX);
                    currentMap.nMaxBlockY = Math.Max(currentMap.nMaxBlockY, blockY);
                    currentMap.nBlockCountX = currentMap.nMaxBlockX - currentMap.nMinBlockX + 1;
                    currentMap.nBlockCountY = currentMap.nMaxBlockY - currentMap.nMinBlockY + 1;
                    currentMap.nLinLengthX = currentMap.nBlockCountX * 64;
                    currentMap.nLinLengthY = currentMap.nBlockCountY * 64;
                    currentMap.nLinEndX = (currentMap.nMaxBlockX - 0x7FFF) * 64 + 0x7FFF;
                    currentMap.nLinEndY = (currentMap.nMaxBlockY - 0x7FFF) * 64 + 0x7FFF;
                    currentMap.nLinBeginX = currentMap.nLinEndX - currentMap.nLinLengthX + 1;
                    currentMap.nLinBeginY = currentMap.nLinEndY - currentMap.nLinLengthY + 1;

                    // 更新所有 SegInfo 的共享值
                    foreach (var seg in currentMap.FullFileNameList.Values)
                    {
                        seg.nMapMinBlockX = currentMap.nMinBlockX;
                        seg.nMapMinBlockY = currentMap.nMinBlockY;
                        seg.nMapBlockCountX = currentMap.nBlockCountX;
                    }
                }

                // 加入到 UI 清單
                string displayName = $"{fileName} ({blockX:X4},{blockY:X4}) [{segInfo.nLinBeginX},{segInfo.nLinBeginY}~{segInfo.nLinEndX},{segInfo.nLinEndY}]";
                S32FileItem item = new S32FileItem
                {
                    FilePath = filePath,
                    DisplayName = displayName,
                    SegInfo = segInfo,
                    IsChecked = true
                };
                int index = lstS32Files.Items.Add(item);
                lstS32Files.SetItemChecked(index, true);

                // 重新渲染地圖
                RenderS32Map();

                MessageBox.Show($"S32 區塊已新增！\n\n檔案: {fileName}", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.toolStripStatusLabel1.Text = $"已新增 S32: {fileName}";
                addS32Form.Close();
            };

            addS32Form.ShowDialog();
        }

        // 創建空的 S32 資料
        private S32Data CreateEmptyS32Data(int blockX, int blockY, string filePath)
        {
            S32Data s32Data = new S32Data();
            s32Data.FilePath = filePath;

            // 初始化 Layer1（128x64 的格子，全部設為空白）
            s32Data.Layer1 = new TileCell[64, 128];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    s32Data.Layer1[y, x] = new TileCell
                    {
                        X = x,
                        Y = y,
                        TileId = 0,
                        IndexId = 0,
                        IsModified = false
                    };
                }
            }

            // 初始化 Layer2（空的）
            s32Data.Layer2 = new List<Layer2Item>();

            // 初始化 Layer3（64x64 的屬性，全部設為可通行）
            s32Data.Layer3 = new MapAttribute[64, 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    s32Data.Layer3[y, x] = new MapAttribute { Attribute1 = 0, Attribute2 = 0 }; // 預設可通行
                }
            }

            // 初始化 Layer4（物件列表，空的）
            s32Data.Layer4 = new List<ObjectTile>();

            // 初始化 Layer5-8
            s32Data.Layer5 = new List<Layer5Item>();
            s32Data.Layer6 = new List<int>();
            s32Data.Layer7 = new List<Layer7Item>();
            s32Data.Layer8 = new List<Layer8Item>();

            // 初始化其他欄位
            s32Data.UsedTiles = new Dictionary<int, TileInfo>();
            s32Data.OriginalFileData = new byte[0];
            s32Data.Layer1Offset = 0;
            s32Data.Layer2Offset = 0;
            s32Data.Layer3Offset = 0;
            s32Data.Layer4Offset = 0;
            s32Data.Layer4EndOffset = 0;
            s32Data.Layer5to8Data = new byte[0];
            s32Data.IsModified = true;

            return s32Data;
        }
    }
}
