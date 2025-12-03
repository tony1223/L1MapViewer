using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        // ===== Model 層 =====
        /// <summary>
        /// 地圖文件 Model - 管理所有 S32 資料
        /// </summary>
        private readonly MapDocument _document = new MapDocument();

        /// <summary>
        /// 編輯狀態 Model - 管理選取、剪貼簿、Undo
        /// </summary>
        private readonly EditState _editState = new EditState();

        /// <summary>
        /// 檢視狀態 Model - 管理縮放、捲動、顯示選項
        /// </summary>
        private readonly ViewState _viewState = new ViewState();

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
        private const double ZOOM_MIN = 0.5;
        private const double ZOOM_MAX = 5.0;
        private const double ZOOM_STEP = 0.2;
        private Image originalMapImage;

        // S32 編輯器縮放相關
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public double s32ZoomLevel { get; set; } = 1.0;
        private Image originalS32Image;
        private double pendingS32ZoomLevel = 1.0;

        // 小地圖完整渲染 Bitmap（整張地圖的縮圖）
        private Bitmap _miniMapFullBitmap = null;

        // 圖層切換防抖Timer
        private System.Windows.Forms.Timer renderDebounceTimer;

        // 縮放防抖Timer
        private System.Windows.Forms.Timer zoomDebounceTimer;

        // 拖曳結束後延遲渲染 Timer
        private System.Windows.Forms.Timer dragRenderTimer;

        // 效能 Log 檔案路徑
        private static readonly string _perfLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "perf.log");
        private void LogPerf(string message)
        {
            try { File.AppendAllText(_perfLogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}"); } catch { }
        }

        // 通行性編輯模式
        private enum PassableEditMode
        {
            None,           // 無編輯模式
            SetPassable,    // 設定為可通行
            SetImpassable   // 設定為不可通行
        }
        private PassableEditMode currentPassableEditMode = PassableEditMode.None;
        private Label lblPassabilityHelp; // 通行性編輯操作說明標籤

        // Layer5 透明編輯模式（狀態存於 _editState.IsLayer5EditMode）
        private Label lblLayer5Help; // Layer5 編輯操作說明標籤

        // Undo 相關常數
        private const int MAX_UNDO_HISTORY = 5;

        // 小地圖拖拽
        private bool isMiniMapDragging = false;

        // 主地圖拖拽（中鍵拖拽移動視圖）
        private bool isMainMapDragging = false;
        private Point mainMapDragStartPoint;
        private Point mainMapDragStartScroll;

        // Viewport 渲染相關
        private Bitmap _viewportBitmap;  // 當前渲染的 Viewport Bitmap
        private readonly object _viewportBitmapLock = new object();  // 保護 _viewportBitmap 的鎖

        // Tile 資料快取 - key: "tileId_indexId" (使用 ConcurrentDictionary 支援多執行緒)
        private System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> tileDataCache = new System.Collections.Concurrent.ConcurrentDictionary<string, byte[]>();

        // 整個 .til 檔案快取 - key: tileId, value: parsed tile array
        private System.Collections.Concurrent.ConcurrentDictionary<int, List<byte[]>> _tilFileCache = new System.Collections.Concurrent.ConcurrentDictionary<int, List<byte[]>>();

        /// <summary>
        /// 預載入地圖用到的所有 tile 檔案（背景執行）
        /// </summary>
        private void PreloadTilesAsync(IEnumerable<S32Data> s32Files)
        {
            Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();

                // 收集所有用到的 tileId
                var tileIds = new HashSet<int>();
                foreach (var s32 in s32Files)
                {
                    // Layer1
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 128; x++)
                        {
                            var cell = s32.Layer1[y, x];
                            if (cell != null && cell.TileId > 0)
                                tileIds.Add(cell.TileId);
                        }
                    }
                    // Layer4
                    foreach (var obj in s32.Layer4)
                    {
                        if (obj.TileId > 0)
                            tileIds.Add(obj.TileId);
                    }
                }

                // 並行載入所有 til 檔案
                int loadedCount = 0;
                System.Threading.Tasks.Parallel.ForEach(tileIds, tileId =>
                {
                    _tilFileCache.GetOrAdd(tileId, _ =>
                    {
                        string key = $"{tileId}.til";
                        byte[] data = L1PakReader.UnPack("Tile", key);
                        if (data == null) return null;
                        return L1Til.Parse(data);
                    });
                    System.Threading.Interlocked.Increment(ref loadedCount);
                });

                sw.Stop();
                LogPerf($"[PRELOAD] Loaded {loadedCount} til files in {sw.ElapsedMilliseconds}ms");
            });
        }

        /// <summary>
        /// 建立 S32 空間索引，用於快速查找指定區域內的 S32 檔案
        /// </summary>
        private void BuildS32SpatialIndex()
        {
            var sw = Stopwatch.StartNew();
            _s32SpatialIndex.Clear();

            int blockWidth = 64 * 24 * 2;   // 3072
            int blockHeight = 64 * 12 * 2;  // 1536

            foreach (var kvp in _document.S32Files)
            {
                string filePath = kvp.Key;
                S32Data s32Data = kvp.Value;

                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 計算這個 S32 block 覆蓋的格子範圍
                int minGridX = mx / SPATIAL_GRID_SIZE;
                int minGridY = my / SPATIAL_GRID_SIZE;
                int maxGridX = (mx + blockWidth - 1) / SPATIAL_GRID_SIZE;
                int maxGridY = (my + blockHeight - 1) / SPATIAL_GRID_SIZE;

                // 將 S32 加入所有覆蓋的格子
                for (int gx = minGridX; gx <= maxGridX; gx++)
                {
                    for (int gy = minGridY; gy <= maxGridY; gy++)
                    {
                        var key = (gx, gy);
                        if (!_s32SpatialIndex.TryGetValue(key, out var list))
                        {
                            list = new List<string>();
                            _s32SpatialIndex[key] = list;
                        }
                        list.Add(filePath);
                    }
                }
            }

            sw.Stop();
            LogPerf($"[SPATIAL-INDEX] Built index for {_document.S32Files.Count} S32 files, {_s32SpatialIndex.Count} grid cells, in {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// 使用空間索引快速查找與指定區域相交的 S32 檔案
        /// </summary>
        private HashSet<string> GetS32FilesInRect(Rectangle worldRect)
        {
            var result = new HashSet<string>();

            // 計算 worldRect 覆蓋的格子範圍
            int minGridX = worldRect.X / SPATIAL_GRID_SIZE;
            int minGridY = worldRect.Y / SPATIAL_GRID_SIZE;
            int maxGridX = (worldRect.Right - 1) / SPATIAL_GRID_SIZE;
            int maxGridY = (worldRect.Bottom - 1) / SPATIAL_GRID_SIZE;

            // 收集所有可能相交的 S32
            for (int gx = minGridX; gx <= maxGridX; gx++)
            {
                for (int gy = minGridY; gy <= maxGridY; gy++)
                {
                    if (_s32SpatialIndex.TryGetValue((gx, gy), out var list))
                    {
                        foreach (var filePath in list)
                        {
                            result.Add(filePath);
                        }
                    }
                }
            }

            return result;
        }

        // S32 Block 渲染快取 - key: filePath, value: rendered bitmap (Layer1+Layer4)
        private System.Collections.Concurrent.ConcurrentDictionary<string, Bitmap> _s32BlockCache = new System.Collections.Concurrent.ConcurrentDictionary<string, Bitmap>();

        // 記錄已經繪製到 viewport bitmap 的 S32 檔案路徑（用於增量渲染）
        private HashSet<string> _renderedS32Blocks = new HashSet<string>();

        // S32 空間索引 - key: (gridX, gridY), value: 該格子內的 S32 檔案路徑列表
        // 每個 S32 block 大小為 3072x1536，使用此索引可快速查找 worldRect 內的 S32
        private Dictionary<(int gridX, int gridY), List<string>> _s32SpatialIndex = new Dictionary<(int, int), List<string>>();
        private const int SPATIAL_GRID_SIZE = 3072;  // 與 S32 block 寬度相同

        // 勾選的 S32 檔案快取（避免每次渲染都遍歷 UI）
        private HashSet<string> _checkedS32Files = new HashSet<string>();

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
                if (_document.S32Files.Count > 0)
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

            // 初始化拖曳渲染延遲Timer（150ms延遲）
            dragRenderTimer = new System.Windows.Forms.Timer();
            dragRenderTimer.Interval = 150;
            dragRenderTimer.Tick += (s, e) =>
            {
                var timerSw = Stopwatch.StartNew();
                LogPerf($"[DRAG-TIMER] tick start");
                dragRenderTimer.Stop();
                CheckAndRerenderIfNeeded();
                timerSw.Stop();
                LogPerf($"[DRAG-TIMER] tick end, total={timerSw.ElapsedMilliseconds}ms");
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

            // 拖曳移動視圖時更新小地圖（使用防抖避免過度更新）
            // 注意：現在使用中鍵拖曳移動視圖，不再使用 Panel AutoScroll

            // 建立通行性編輯操作說明標籤
            lblPassabilityHelp = new Label
            {
                AutoSize = true,
                BackColor = Color.FromArgb(200, 30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Microsoft JhengHei", 9, FontStyle.Regular),
                Padding = new Padding(8),
                Visible = false,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.s32MapPanel.Controls.Add(lblPassabilityHelp);
            lblPassabilityHelp.BringToFront();
            lblPassabilityHelp.Location = new Point(10, 10);

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
            if (!isLayer4CopyMode || _editState.SelectedCells.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "請先使用左鍵選取要刪除的區域";
                return;
            }

            // 呼叫現有的批次刪除功能
            DeleteAllLayer4ObjectsInRegion(_editState.SelectedCells);

            // 清除選取狀態
            isLayer4CopyMode = false;
            selectedRegion = new Rectangle();
            copyRegionBounds = new Rectangle();
            _editState.SelectedCells.Clear();
            s32PictureBox.Invalidate();
        }

        // 複製 Layer4 物件
        private void CopySelectedCells()
        {
            if (!isLayer4CopyMode || copyRegionBounds.Width == 0 || copyRegionBounds.Height == 0)
            {
                this.toolStripStatusLabel1.Text = "請先使用 左鍵 選取要複製的區域";
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

            _editState.CellClipboard.Clear();

            // 使用已經在 MouseMove/MouseUp 中計算好的 _editState.SelectedCells
            List<SelectedCell> selectedCells = _editState.SelectedCells;

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

                _editState.CellClipboard.Add(cellData);
            }

            // Layer4 物件搜索：遍歷所有 S32，找出座標精確落在選取範圍內的物件
            if (copyLayer4 && selectedCells.Count > 0)
            {
                // 建立選取格子的全域座標集合 (Layer3 座標系)
                var selectedGlobalCells = new HashSet<(int x, int y)>();
                foreach (var cell in selectedCells)
                {
                    int globalL1X = cell.S32Data.SegInfo.nLinBeginX*2 + cell.LocalX;
                    int globalL1Y = cell.S32Data.SegInfo.nLinBeginY + cell.LocalY;
                    selectedGlobalCells.Add((globalL1X, globalL1Y));
                    selectedGlobalCells.Add((globalL1X +1 , globalL1Y));
                }

                // 遍歷所有 S32 檔案搜索物件（精確匹配選取格子）
                foreach (var s32Data in _document.S32Files.Values)
                {
                    int segStartX = s32Data.SegInfo.nLinBeginX;
                    int segStartY = s32Data.SegInfo.nLinBeginY;

                    for (int i = 0; i < s32Data.Layer4.Count; i++)
                    {
                        var obj = s32Data.Layer4[i];

                        // 計算物件的全域座標 (考慮物件 X 可能超出 0-127 範圍)
                        int objGlobalL3X = segStartX + obj.X / 2;
                        int objGlobalL1X = segStartX*2 + obj.X ;
                        int objGlobalL3Y = segStartY + obj.Y;

                        // 精確檢查是否在選取的格子內
                        if (selectedGlobalCells.Contains((objGlobalL1X, objGlobalL3Y)))
                        {
                            // 檢查群組篩選
                            if (_editState.IsFilteringLayer4Groups && _editState.SelectedLayer4Groups.Count > 0 &&
                                !_editState.SelectedLayer4Groups.Contains(obj.GroupId))
                                continue;

                            // obj.X 本身就是 Layer1 局部座標 (0-127)
                            int objGlobalLayer1X = segStartX * 2 + obj.X;
                            int objGlobalY = segStartY + obj.Y;
                            int relX = objGlobalLayer1X - minGlobalX;
                            int relY = objGlobalY - minGlobalY;

                            bool alreadyCopied = _editState.CellClipboard.Any(cd =>
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
                                    OriginalIndex = i,
                                    // 記錄 Layer1 座標系統 (0-127) 的局部座標
                                    OriginalLocalLayer1X = obj.X,
                                    OriginalLocalY = obj.Y
                                });
                                _editState.CellClipboard.Add(objCellData);
                                layer4Count++;
                            }
                        }
                    }
                }
            }

            hasLayer4Clipboard = _editState.CellClipboard.Count > 0;
            _editState.SourceMapId = _document.MapId;
            _editState.CopySourceOrigin = new Point(minGlobalX, minGlobalY);  // 記錄複製時的原點

            // 複製 Layer2 和 Layer5-8 資料（從所有涉及的 S32 收集，根據設定）
            _editState.Layer2Clipboard.Clear();
            _editState.Layer5Clipboard.Clear();
            _editState.Layer6Clipboard.Clear();
            _editState.Layer7Clipboard.Clear();
            _editState.Layer8Clipboard.Clear();
            bool copyLayer2 = copySettingLayer2;
            bool copyLayer5 = copySettingLayer5;
            bool copyLayer6to8 = copySettingLayer6to8;

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
                            if (!_editState.Layer2Clipboard.Any(l => l.X == item.X && l.Y == item.Y && l.TileId == item.TileId))
                            {
                                _editState.Layer2Clipboard.Add(new Layer2Item
                                {
                                    X = item.X,
                                    Y = item.Y,
                                    IndexId = item.IndexId,
                                    TileId = item.TileId,
                                    UK = item.UK
                                });
                            }
                        }
                    }
                }
            }

            // Layer5-8：只複製座標落在選取格子範圍內的項目
            if (copyLayer5 || copyLayer6to8)
            {
                // 建立選取格子的本地座標集合（按 S32 檔案分組）
                // Layer5 使用 Layer1 座標系 (X=0-127, Y=0-63)
                // Layer7 使用 Layer3 座標系 (X=0-63, Y=0-63)
                var selectedL1CellsByS32 = new Dictionary<S32Data, HashSet<(int x, int y)>>();  // Layer5 用
                var selectedL3CellsByS32 = new Dictionary<S32Data, HashSet<(int x, int y)>>();  // Layer7 用
                foreach (var cell in selectedCells)
                {
                    if (!selectedL1CellsByS32.ContainsKey(cell.S32Data))
                    {
                        selectedL1CellsByS32[cell.S32Data] = new HashSet<(int, int)>();
                        selectedL3CellsByS32[cell.S32Data] = new HashSet<(int, int)>();
                    }
                    // Layer5: X 是 0-127 (Layer1 座標)，Y 是 0-63
                    selectedL1CellsByS32[cell.S32Data].Add((cell.LocalX, cell.LocalY));
                    // Layer7: X 是 0-63 (Layer3 座標)，Y 是 0-63
                    int localL3X = cell.LocalX / 2;
                    selectedL3CellsByS32[cell.S32Data].Add((localL3X, cell.LocalY));
                }

                foreach (var kvp in selectedL1CellsByS32)
                {
                    var s32Data = kvp.Key;
                    var l1Cells = kvp.Value;
                    var l3Cells = selectedL3CellsByS32[s32Data];
                    int segStartX = s32Data.SegInfo.nLinBeginX;
                    int segStartY = s32Data.SegInfo.nLinBeginY;

                    // Layer5 - 透明圖塊（檢查 X, Y 是否在選取範圍內）
                    if (copyLayer5)
                    {
                        foreach (var item in s32Data.Layer5)
                        {
                            // Layer5 的 X 是 0-127 (Layer1 座標)，Y 是 0-63
                            if (l1Cells.Contains((item.X, item.Y)))
                            {
                                // 計算相對於複製原點的全域座標
                                int globalL1X = segStartX * 2 + item.X;
                                int globalY = segStartY + item.Y;
                                int relX = globalL1X - minGlobalX;
                                int relY = globalY - minGlobalY;

                                // 儲存相對座標（使用 X, Y 欄位暫存）
                                if (!_editState.Layer5Clipboard.Any(l => l.X == relX && l.Y == relY && l.ObjectIndex == item.ObjectIndex))
                                {
                                    _editState.Layer5Clipboard.Add(new Layer5Item { X = (byte)relX, Y = (byte)relY, ObjectIndex = item.ObjectIndex, Type = item.Type });
                                }
                            }
                        }
                    }

                    // Layer6 - 使用的 TilId（合併不重複的，這個是整個 S32 的）
                    if (copyLayer6to8)
                    {
                        foreach (var tilId in s32Data.Layer6)
                        {
                            if (!_editState.Layer6Clipboard.Contains(tilId))
                            {
                                _editState.Layer6Clipboard.Add(tilId);
                            }
                        }
                    }

                    // Layer7 - 傳送點（檢查 X, Y 是否在選取範圍內）
                    if (copyLayer6to8)
                    {
                        foreach (var item in s32Data.Layer7)
                        {
                            // Layer7 的 X, Y 是 64x64 座標系 (Layer3)
                            if (l3Cells.Contains((item.X, item.Y)))
                            {
                                // 計算相對於複製原點的座標（Layer3 座標系）
                                int globalL3X = segStartX + item.X;
                                int globalY = segStartY + item.Y;
                                int relL3X = globalL3X - (minGlobalX / 2);  // minGlobalX 是 Layer1 座標，除以 2 得到 Layer3
                                int relY = globalY - minGlobalY;

                                if (!_editState.Layer7Clipboard.Any(l => l.Name == item.Name && l.X == relL3X && l.Y == relY))
                                {
                                    _editState.Layer7Clipboard.Add(new Layer7Item { Name = item.Name, X = (byte)relL3X, Y = (byte)relY, TargetMapId = item.TargetMapId, PortalId = item.PortalId });
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
                            if (l3Cells.Contains((cellX, cellY)))
                            {
                                // 計算相對於複製原點的座標
                                int globalL3X = segStartX + cellX;
                                int globalY = segStartY + cellY;
                                int relL3X = globalL3X - (minGlobalX / 2);
                                int relY = globalY - minGlobalY;

                                if (!_editState.Layer8Clipboard.Any(l => l.SprId == item.SprId && l.X == relL3X && l.Y == relY))
                                {
                                    _editState.Layer8Clipboard.Add(new Layer8Item { SprId = item.SprId, X = (ushort)relL3X, Y = (ushort)relY, ExtendedData = item.ExtendedData });
                                }
                            }
                        }
                    }
                }
            }

            // 組合提示訊息
            var parts = new List<string>();
            if (copyLayer1 && layer1Count > 0) parts.Add($"L1:{layer1Count}");
            if (copyLayer2 && _editState.Layer2Clipboard.Count > 0) parts.Add($"L2:{_editState.Layer2Clipboard.Count}");
            if (copyLayer3 && layer3Count > 0) parts.Add($"L3:{layer3Count}");
            if (copyLayer4 && layer4Count > 0) parts.Add($"L4:{layer4Count}");
            if (copyLayer5 && _editState.Layer5Clipboard.Count > 0) parts.Add($"L5:{_editState.Layer5Clipboard.Count}");
            if (copyLayer6to8 && (_editState.Layer6Clipboard.Count > 0 || _editState.Layer7Clipboard.Count > 0 || _editState.Layer8Clipboard.Count > 0))
                parts.Add($"L6:{_editState.Layer6Clipboard.Count} L7:{_editState.Layer7Clipboard.Count} L8:{_editState.Layer8Clipboard.Count}");

            string layerInfo = parts.Count > 0 ? string.Join(", ", parts) : "無資料";
            this.toolStripStatusLabel1.Text = $"已複製 {selectedCells.Count} 格 ({layerInfo}) 來源: {_document.MapId}，左鍵選取貼上位置後按 Ctrl+V";

            // 清除選取框但保留複製資料
            isLayer4CopyMode = false;
            selectedRegion = new Rectangle();
            copyRegionBounds = new Rectangle();
            _editState.SelectedCells.Clear();
            s32PictureBox.Invalidate();
        }

        // 貼上選取區域
        private void PasteSelectedCells()
        {
            if (!hasLayer4Clipboard || _editState.CellClipboard.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "剪貼簿沒有資料，請先使用左鍵選取區域後按 Ctrl+C 複製";
                return;
            }

            // 需要先選取貼上位置（檢查 CopyRegionOrigin 是否有效）
            if (_editState.CopyRegionOrigin.X < 0 || _editState.CopyRegionOrigin.Y < 0)
            {
                this.toolStripStatusLabel1.Text = "請使用左鍵選取貼上位置";
                return;
            }

            // 取得貼上位置的全域 Layer1 座標
            int pasteOriginX = _editState.CopyRegionOrigin.X;
            int pasteOriginY = _editState.CopyRegionOrigin.Y;


            int layer1Count = 0, layer3Count = 0, layer4Count = 0;
            int skippedCount = 0;

            // 建立 Undo 記錄
            var undoAction = new UndoAction
            {
                Description = $"貼上 {_editState.CellClipboard.Count} 格資料"
            };

            // 貼上每個格子的資料
            foreach (var cellData in _editState.CellClipboard)
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

                // Layer1 資料（地板）- 記錄舊值和新值到 Undo
                if (cellData.Layer1Cell1 != null && localX >= 0 && localX < 128)
                {
                    var oldCell = targetS32.Layer1[localY, localX];
                    undoAction.ModifiedLayer1.Add(new UndoLayer1Info
                    {
                        S32FilePath = targetS32.FilePath,
                        LocalX = localX,
                        LocalY = localY,
                        OldTileId = oldCell?.TileId ?? 0,
                        OldIndexId = oldCell?.IndexId ?? 0,
                        NewTileId = cellData.Layer1Cell1.TileId,
                        NewIndexId = cellData.Layer1Cell1.IndexId
                    });

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
                    var oldCell = targetS32.Layer1[localY, localX + 1];
                    undoAction.ModifiedLayer1.Add(new UndoLayer1Info
                    {
                        S32FilePath = targetS32.FilePath,
                        LocalX = localX + 1,
                        LocalY = localY,
                        OldTileId = oldCell?.TileId ?? 0,
                        OldIndexId = oldCell?.IndexId ?? 0,
                        NewTileId = cellData.Layer1Cell2.TileId,
                        NewIndexId = cellData.Layer1Cell2.IndexId
                    });

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

                // Layer3 資料（屬性）- 記錄舊值和新值到 Undo
                if (cellData.Layer3Attr != null)
                {
                    int layer3X = localX / 2;
                    if (layer3X >= 0 && layer3X < 64)
                    {
                        var oldAttr = targetS32.Layer3[localY, layer3X];
                        undoAction.ModifiedLayer3.Add(new UndoLayer3Info
                        {
                            S32FilePath = targetS32.FilePath,
                            LocalX = layer3X,
                            LocalY = localY,
                            OldAttribute1 = oldAttr?.Attribute1 ?? 0,
                            OldAttribute2 = oldAttr?.Attribute2 ?? 0,
                            NewAttribute1 = cellData.Layer3Attr.Attribute1,
                            NewAttribute2 = cellData.Layer3Attr.Attribute2
                        });

                        targetS32.Layer3[localY, layer3X] = new MapAttribute
                        {
                            Attribute1 = cellData.Layer3Attr.Attribute1,
                            Attribute2 = cellData.Layer3Attr.Attribute2
                        };
                        layer3Count++;
                        targetS32.IsModified = true;
                    }
                }

                // Layer4 資料（物件）- 加入新物件並記錄到 Undo
                if (cellData.Layer4Objects.Count > 0)
                {
                    // 加入新物件
                    foreach (var objData in cellData.Layer4Objects.OrderBy(o => o.OriginalIndex))
                    {
                        // 計算物件在目標 S32 內的局部座標（Layer1 座標系統 0-127）
                        int objTargetGlobalX = pasteOriginX + objData.RelativeX;
                        int objTargetGlobalY = pasteOriginY + objData.RelativeY;
                        int objLocalLayer1X = objTargetGlobalX - targetS32.SegInfo.nLinBeginX * 2;
                        int objLocalY = objTargetGlobalY - targetS32.SegInfo.nLinBeginY;

                        // obj.X 是 Layer1 座標，可能超過 127（物件可以跨格子）
                        if (objLocalLayer1X >= 0 && objLocalY >= 0 && objLocalY < 64)
                        {
                            targetS32.Layer4.Add(new ObjectTile
                            {
                                GroupId = objData.GroupId,
                                X = objLocalLayer1X,  // 使用 Layer1 座標
                                Y = objLocalY,
                                Layer = objData.Layer,
                                IndexId = objData.IndexId,
                                TileId = objData.TileId
                            });
                            layer4Count++;

                            // 記錄新增的物件到 Undo（還原時要刪除）
                            undoAction.AddedObjects.Add(new UndoObjectInfo
                            {
                                S32FilePath = targetS32.FilePath,
                                GameX = targetS32.SegInfo.nLinBeginX + objLocalLayer1X / 2,
                                GameY = targetS32.SegInfo.nLinBeginY + objLocalY,
                                LocalX = objLocalLayer1X,
                                LocalY = objLocalY,
                                GroupId = objData.GroupId,
                                Layer = objData.Layer,
                                IndexId = objData.IndexId,
                                TileId = objData.TileId
                            });
                        }
                    }
                    targetS32.IsModified = true;
                }
            }

            // 儲存 Undo 記錄（如果有任何修改）
            if (undoAction.AddedObjects.Count > 0 || undoAction.ModifiedLayer1.Count > 0 || undoAction.ModifiedLayer3.Count > 0)
            {
                PushUndoAction(undoAction);
            }

            // 合併 Layer2 和 Layer5-8 到所有受影響的目標 S32（根據設定）
            int layer2AddedCount = 0;
            int layer5to8CopiedCount = 0;
            var affectedS32Set = new HashSet<S32Data>();
            foreach (var cellData in _editState.CellClipboard)
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
                if (copySettingLayer2 && _editState.Layer2Clipboard.Count > 0)
                {
                    foreach (var item in _editState.Layer2Clipboard)
                    {
                        if (!targetS32.Layer2.Any(l => l.X == item.X && l.Y == item.Y && l.TileId == item.TileId))
                        {
                            targetS32.Layer2.Add(new Layer2Item
                            {
                                X = item.X,
                                Y = item.Y,
                                IndexId = item.IndexId,
                                TileId = item.TileId,
                                UK = item.UK
                            });
                            layer2AddedCount++;
                            targetS32.IsModified = true;
                        }
                    }
                }

                // Layer6 - 使用的 TilId（合併不重複的）- 根據設定
                if (copySettingLayer6to8)
                {
                    int layer6Added = 0;
                    foreach (var tilId in _editState.Layer6Clipboard)
                    {
                        if (!targetS32.Layer6.Contains(tilId))
                        {
                            targetS32.Layer6.Add(tilId);
                            targetS32.IsModified = true;
                            layer6Added++;
                        }
                    }
                    if (layer6Added > 0) layer5to8CopiedCount++;
                }
            }

            // Layer5 - 透明圖塊（需要計算正確的目標座標）- 根據設定
            if (copySettingLayer5 && _editState.Layer5Clipboard.Count > 0)
            {
                foreach (var item in _editState.Layer5Clipboard)
                {
                    // item.X, item.Y 是相對座標（相對於複製原點）
                    int targetGlobalL1X = pasteOriginX + item.X;
                    int targetGlobalY = pasteOriginY + item.Y;

                    // 找到目標 S32
                    int targetGameX = targetGlobalL1X / 2;
                    int targetGameY = targetGlobalY;
                    S32Data l5TargetS32 = GetS32DataByGameCoords(targetGameX, targetGameY);
                    if (l5TargetS32 == null) continue;

                    // 計算目標 S32 內的局部座標
                    int localL1X = targetGlobalL1X - l5TargetS32.SegInfo.nLinBeginX * 2;
                    int localY = targetGlobalY - l5TargetS32.SegInfo.nLinBeginY;

                    // 檢查座標是否有效
                    if (localL1X < 0 || localL1X >= 128 || localY < 0 || localY >= 64)
                        continue;

                    // 檢查是否已存在
                    if (!l5TargetS32.Layer5.Any(l => l.X == localL1X && l.Y == localY && l.ObjectIndex == item.ObjectIndex))
                    {
                        l5TargetS32.Layer5.Add(new Layer5Item
                        {
                            X = (byte)localL1X,
                            Y = (byte)localY,
                            ObjectIndex = item.ObjectIndex,
                            Type = item.Type
                        });
                        l5TargetS32.IsModified = true;
                    }
                }
            }

            // Layer7 - 傳送點（需要計算正確的目標座標）- 根據設定
            if (copySettingLayer6to8 && _editState.Layer7Clipboard.Count > 0)
            {
                foreach (var item in _editState.Layer7Clipboard)
                {
                    // item.X, item.Y 是相對座標（相對於複製原點的 Layer3 座標）
                    // 先轉為 Layer1 座標系，再計算目標
                    int targetGlobalL1X = pasteOriginX + item.X * 2;
                    int targetGlobalY = pasteOriginY + item.Y;

                    // 找到目標 S32
                    int targetGameX = targetGlobalL1X / 2;
                    int targetGameY = targetGlobalY;
                    S32Data l7TargetS32 = GetS32DataByGameCoords(targetGameX, targetGameY);
                    if (l7TargetS32 == null) continue;

                    // 計算目標 S32 內的局部 Layer3 座標
                    int localL3X = targetGameX - l7TargetS32.SegInfo.nLinBeginX;
                    int localY = targetGlobalY - l7TargetS32.SegInfo.nLinBeginY;

                    // 檢查座標是否有效
                    if (localL3X < 0 || localL3X >= 64 || localY < 0 || localY >= 64)
                        continue;

                    // 檢查是否已存在
                    if (!l7TargetS32.Layer7.Any(l => l.Name == item.Name && l.X == localL3X && l.Y == localY))
                    {
                        l7TargetS32.Layer7.Add(new Layer7Item
                        {
                            Name = item.Name,
                            X = (byte)localL3X,
                            Y = (byte)localY,
                            TargetMapId = item.TargetMapId,
                            PortalId = item.PortalId
                        });
                        l7TargetS32.IsModified = true;
                    }
                }
            }

            // Layer8 - 特效（需要計算正確的目標座標）- 根據設定
            if (copySettingLayer6to8 && _editState.Layer8Clipboard.Count > 0)
            {
                foreach (var item in _editState.Layer8Clipboard)
                {
                    // item.X, item.Y 可能是像素座標或格子座標，這裡假設是格子座標
                    int targetGlobalL1X = pasteOriginX + item.X * 2;
                    int targetGlobalY = pasteOriginY + item.Y;

                    // 找到目標 S32
                    int targetGameX = targetGlobalL1X / 2;
                    int targetGameY = targetGlobalY;
                    S32Data l8TargetS32 = GetS32DataByGameCoords(targetGameX, targetGameY);
                    if (l8TargetS32 == null) continue;

                    // 計算目標 S32 內的局部座標
                    int localL3X = targetGameX - l8TargetS32.SegInfo.nLinBeginX;
                    int localY = targetGlobalY - l8TargetS32.SegInfo.nLinBeginY;

                    // 檢查座標是否有效
                    if (localL3X < 0 || localL3X >= 64 || localY < 0 || localY >= 64)
                        continue;

                    // 檢查是否已存在
                    if (!l8TargetS32.Layer8.Any(l => l.SprId == item.SprId && l.X == localL3X && l.Y == localY))
                    {
                        l8TargetS32.Layer8.Add(new Layer8Item
                        {
                            SprId = item.SprId,
                            X = (ushort)localL3X,
                            Y = (ushort)localY,
                            ExtendedData = item.ExtendedData
                        });
                        l8TargetS32.IsModified = true;
                    }
                }
            }

            // 清除選取模式
            isLayer4CopyMode = false;
            selectedRegion = new Rectangle();
            copyRegionBounds = new Rectangle();
            _editState.SelectedCells.Clear();

            // 重新渲染地圖
            RenderS32Map();

            // 檢查是否跨地圖貼上
            bool isCrossMap = !string.IsNullOrEmpty(_editState.SourceMapId) && _editState.SourceMapId != _document.MapId;

            // 組合提示訊息
            var parts = new List<string>();
            if (layer1Count > 0) parts.Add($"L1:{layer1Count}");
            if (layer2AddedCount > 0) parts.Add($"L2:+{layer2AddedCount}");
            if (layer3Count > 0) parts.Add($"L3:{layer3Count}");
            if (layer4Count > 0) parts.Add($"L4:{layer4Count}");
            if (layer5to8CopiedCount > 0) parts.Add($"L6:+{layer5to8CopiedCount}");

            string layerInfo = parts.Count > 0 ? string.Join(", ", parts) : "無資料";
            string message = $"已貼上 {_editState.CellClipboard.Count} 格 ({layerInfo})";
            if (isCrossMap)
                message += $" (從 {_editState.SourceMapId} 跨地圖貼上)";
            if (skippedCount > 0)
                message += $"，{skippedCount} 格超出範圍被跳過";
            this.toolStripStatusLabel1.Text = message;

            // 清除快取並重新渲染
            ClearS32BlockCache();
            RenderS32Map();

            // 更新 Layer5 異常檢查按鈕
            UpdateLayer5InvalidButton();
        }

        // 取消複製/貼上模式
        private void CancelLayer4CopyPaste()
        {
            isLayer4CopyMode = false;
            // 恢復顯示全部群組
            UpdateGroupThumbnailsList();
            selectedRegion = new Rectangle();
            copyRegionBounds = new Rectangle();
            _editState.SelectedCells.Clear();
            s32PictureBox.Invalidate();
            this.toolStripStatusLabel1.Text = "已取消複製/貼上模式";
        }

        // 新增 Undo 記錄
        private void PushUndoAction(UndoAction action)
        {
            _editState.UndoHistory.Push(action);
            // 新操作會清空 redo 歷史
            _editState.RedoHistory.Clear();
            // 限制歷史記錄數量
            if (_editState.UndoHistory.Count > MAX_UNDO_HISTORY)
            {
                var tempStack = new Stack<UndoAction>();
                for (int i = 0; i < MAX_UNDO_HISTORY; i++)
                {
                    tempStack.Push(_editState.UndoHistory.Pop());
                }
                _editState.UndoHistory.Clear();
                while (tempStack.Count > 0)
                {
                    _editState.UndoHistory.Push(tempStack.Pop());
                }
            }
        }

        // 執行還原 (Ctrl+Z)
        private void UndoLastAction()
        {
            if (_editState.UndoHistory.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "沒有可還原的操作";
                return;
            }

            var action = _editState.UndoHistory.Pop();

            // 還原刪除的物件（重新新增）
            foreach (var objInfo in action.RemovedObjects)
            {
                S32Data targetS32 = null;
                if (_document.S32Files.TryGetValue(objInfo.S32FilePath, out targetS32))
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
                if (_document.S32Files.TryGetValue(objInfo.S32FilePath, out targetS32))
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
                if (_document.S32Files.TryGetValue(layer7Info.S32FilePath, out targetS32))
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

            // 還原修改的第一層資料（地板）
            foreach (var layer1Info in action.ModifiedLayer1)
            {
                S32Data targetS32 = null;
                if (_document.S32Files.TryGetValue(layer1Info.S32FilePath, out targetS32))
                {
                    targetS32.Layer1[layer1Info.LocalY, layer1Info.LocalX] = new TileCell
                    {
                        X = layer1Info.LocalX,
                        Y = layer1Info.LocalY,
                        TileId = layer1Info.OldTileId,
                        IndexId = layer1Info.OldIndexId
                    };
                    targetS32.IsModified = true;
                }
            }

            // 還原修改的第三層資料（屬性）
            foreach (var layer3Info in action.ModifiedLayer3)
            {
                S32Data targetS32 = null;
                if (_document.S32Files.TryGetValue(layer3Info.S32FilePath, out targetS32))
                {
                    targetS32.Layer3[layer3Info.LocalY, layer3Info.LocalX] = new MapAttribute
                    {
                        Attribute1 = layer3Info.OldAttribute1,
                        Attribute2 = layer3Info.OldAttribute2
                    };
                    targetS32.IsModified = true;
                }
            }

            // 將此動作放入 redo 歷史
            _editState.RedoHistory.Push(action);

            // 清除快取並重新渲染地圖
            ClearS32BlockCache();
            RenderS32Map();

            // 更新 Layer5 異常檢查按鈕
            UpdateLayer5InvalidButton();

            this.toolStripStatusLabel1.Text = $"已還原: {action.Description} (Ctrl+Z: {_editState.UndoHistory.Count} / Ctrl+Y: {_editState.RedoHistory.Count})";
        }

        // 執行重做 (Ctrl+Y)
        private void RedoLastAction()
        {
            if (_editState.RedoHistory.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "沒有可重做的操作";
                return;
            }

            var action = _editState.RedoHistory.Pop();

            // 重做新增的物件（重新新增）
            foreach (var objInfo in action.AddedObjects)
            {
                S32Data targetS32 = null;
                if (_document.S32Files.TryGetValue(objInfo.S32FilePath, out targetS32))
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
                if (_document.S32Files.TryGetValue(objInfo.S32FilePath, out targetS32))
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
                if (_document.S32Files.TryGetValue(layer7Info.S32FilePath, out targetS32))
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

            // 重做修改的第一層資料（套用新值）
            foreach (var layer1Info in action.ModifiedLayer1)
            {
                S32Data targetS32 = null;
                if (_document.S32Files.TryGetValue(layer1Info.S32FilePath, out targetS32))
                {
                    targetS32.Layer1[layer1Info.LocalY, layer1Info.LocalX] = new TileCell
                    {
                        X = layer1Info.LocalX,
                        Y = layer1Info.LocalY,
                        TileId = layer1Info.NewTileId,
                        IndexId = layer1Info.NewIndexId
                    };
                    targetS32.IsModified = true;
                }
            }

            // 重做修改的第三層資料（套用新值）
            foreach (var layer3Info in action.ModifiedLayer3)
            {
                S32Data targetS32 = null;
                if (_document.S32Files.TryGetValue(layer3Info.S32FilePath, out targetS32))
                {
                    targetS32.Layer3[layer3Info.LocalY, layer3Info.LocalX] = new MapAttribute
                    {
                        Attribute1 = layer3Info.NewAttribute1,
                        Attribute2 = layer3Info.NewAttribute2
                    };
                    targetS32.IsModified = true;
                }
            }

            // 將此動作放回 undo 歷史
            _editState.UndoHistory.Push(action);

            // 清除快取並重新渲染地圖
            ClearS32BlockCache();
            RenderS32Map();

            // 更新 Layer5 異常檢查按鈕
            UpdateLayer5InvalidButton();

            this.toolStripStatusLabel1.Text = $"已重做: {action.Description} (Ctrl+Z: {_editState.UndoHistory.Count} / Ctrl+Y: {_editState.RedoHistory.Count})";
        }

        // 取得等距菱形區域內的所有格子（支援長方形）
        private List<SelectedCell> GetCellsInIsometricRegion(Rectangle region)
        {
            List<SelectedCell> result = new List<SelectedCell>();

            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
                return result;

            Struct.L1Map currentMap = Share.MapDataList[_document.MapId];

            // 計算菱形的參數（使用實際寬高）
            float centerX = region.Left + region.Width / 2f;
            float centerY = region.Top + region.Height / 2f;
            float halfWidth = region.Width / 2f;
            float halfHeight = region.Height / 2f;

            foreach (var s32Data in _document.S32Files.Values)
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
            if (cells.Count == 0 || string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
                return new Rectangle();

            Struct.L1Map currentMap = Share.MapDataList[_document.MapId];

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

            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
                return result;

            Struct.L1Map currentMap = Share.MapDataList[_document.MapId];

            foreach (var s32Data in _document.S32Files.Values)
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

            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
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

            // 計算螢幕範圍對應的世界座標矩形，用於空間索引查詢
            int minScreenX = Math.Min(startPoint.X, endPoint.X);
            int minScreenY = Math.Min(startPoint.Y, endPoint.Y);
            int maxScreenX = Math.Max(startPoint.X, endPoint.X);
            int maxScreenY = Math.Max(startPoint.Y, endPoint.Y);

            int worldLeft = (int)(minScreenX / s32ZoomLevel) + _viewState.ScrollX;
            int worldTop = (int)(minScreenY / s32ZoomLevel) + _viewState.ScrollY;
            int worldRight = (int)(maxScreenX / s32ZoomLevel) + _viewState.ScrollX;
            int worldBottom = (int)(maxScreenY / s32ZoomLevel) + _viewState.ScrollY;

            // 擴大查詢範圍以確保不漏掉邊界的 S32
            Rectangle queryRect = new Rectangle(worldLeft - 3072, worldTop - 1536,
                                                 worldRight - worldLeft + 6144,
                                                 worldBottom - worldTop + 3072);
            var candidateFiles = GetS32FilesInRect(queryRect);

            // 只遍歷候選的 S32 檔案
            foreach (var filePath in candidateFiles)
            {
                if (!_document.S32Files.TryGetValue(filePath, out var s32Data))
                    continue;

                // 先檢查這個 S32 的遊戲座標範圍是否與選取範圍有交集
                int s32MinGameX = s32Data.SegInfo.nLinBeginX;
                int s32MaxGameX = s32Data.SegInfo.nLinBeginX + 63;
                int s32MinGameY = s32Data.SegInfo.nLinBeginY;
                int s32MaxGameY = s32Data.SegInfo.nLinBeginY + 63;

                // 快速排除不相交的 S32
                if (s32MaxGameX < minGameX || s32MinGameX > maxGameX ||
                    s32MaxGameY < minGameY || s32MinGameY > maxGameY)
                    continue;

                // 計算在這個 S32 內需要檢查的範圍
                int localMinX3 = Math.Max(0, minGameX - s32Data.SegInfo.nLinBeginX);
                int localMaxX3 = Math.Min(63, maxGameX - s32Data.SegInfo.nLinBeginX);
                int localMinY = Math.Max(0, minGameY - s32Data.SegInfo.nLinBeginY);
                int localMaxY = Math.Min(63, maxGameY - s32Data.SegInfo.nLinBeginY);

                for (int y = localMinY; y <= localMaxY; y++)
                {
                    for (int x3 = localMinX3; x3 <= localMaxX3; x3++)
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

            return result;
        }

        // 重新載入當前地圖
        private void ReloadCurrentMap()
        {
            if (string.IsNullOrEmpty(_document.MapId))
            {
                this.toolStripStatusLabel1.Text = "沒有選擇地圖";
                return;
            }

            // 清除快取
            tileDataCache.Clear();
            _editState.HighlightedS32Data = null;
            _editState.HighlightedCellX = -1;
            _editState.HighlightedCellY = -1;

            // 重新載入 S32 檔案
            this.toolStripStatusLabel1.Text = "正在重新載入...";
            LoadS32FileList(_document.MapId);
        }

        private void MapForm_Load(object sender, EventArgs e)
        {
            // 初始化浮動圖層面板狀態（必須在載入地圖前執行）
            SyncFloatPanelCheckboxes();
            s32EditorPanel_Resize(null, null);
            layerFloatPanel.BringToFront();

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
            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
            {
                MessageBox.Show("請先載入地圖！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_document.S32Files.Count == 0)
            {
                MessageBox.Show("請先在 S32 編輯器中載入地圖資料！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "文字檔 (*.txt)|*.txt";
                saveDialog.FileName = $"{_document.MapId}.txt";
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
            Struct.L1Map currentMap = Share.MapDataList[_document.MapId];

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
            foreach (var s32Data in _document.S32Files.Values)
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

            this.toolStripStatusLabel1.Text = $"已匯出 {_document.MapId}.txt ({xLength}x{yLength})";
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
            // 先在 UI 執行緒檢查路徑是否有效（避免 MessageBox 在背景執行緒彈出）
            string szMapPath = string.Format(@"{0}\map\", selectedPath);
            if (!Directory.Exists(szMapPath))
            {
                MessageBox.Show("錯誤的天堂路徑");
                return;
            }

            Utils.ShowProgressBar(true, this);
            this.toolStripStatusLabel1.Text = "正在讀取地圖列表...";

            // 在背景執行緒載入地圖資料
            Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();
                var dictionary = L1MapHelper.Read(selectedPath);
                stopwatch.Stop();
                long readMs = stopwatch.ElapsedMilliseconds;

                // 回到 UI 執行緒更新介面
                this.BeginInvoke((MethodInvoker)delegate
                {
                    stopwatch.Restart();

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

                    long uiMs = stopwatch.ElapsedMilliseconds;

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

                    this.toolStripStatusLabel2.Text = $"Maps={dictionary.Count}, Files={L1MapHelper.LastTotalFileCount}";
                    this.toolStripStatusLabel1.Text = $"載入完成 - Zone3desc:{L1MapHelper.LastLoadZone3descMs}ms, ZoneXml:{L1MapHelper.LastLoadZoneXmlMs}ms, 掃描目錄:{L1MapHelper.LastScanDirectoriesMs}ms, UI:{uiMs}ms (總計:{readMs + uiMs}ms)";
                    Utils.ShowProgressBar(false, this);
                });
            });
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

        // ===== 小地圖 =====
        // 小地圖尺寸常數
        private const int MINIMAP_SIZE = 400;

        // 小地圖的縮放比例和偏移（快取計算結果）
        private float _miniMapScale = 1.0f;
        private int _miniMapOffsetX = 0;
        private int _miniMapOffsetY = 0;
        private readonly object _miniMapLock = new object();
        private bool _miniMapRendering = false;  // 是否正在渲染中

        /// <summary>
        /// 更新小地圖（如果沒有快取則背景渲染，否則只更新紅框）
        /// </summary>
        private void UpdateMiniMap()
        {
            try
            {
                int mapWidth = _viewState.MapWidth;
                int mapHeight = _viewState.MapHeight;

                if (mapWidth <= 0 || mapHeight <= 0)
                    return;

                // 如果沒有快取且沒有在渲染中，啟動背景渲染
                if (_miniMapFullBitmap == null && !_miniMapRendering)
                {
                    // 先顯示一個「渲染中」的佔位圖
                    ShowMiniMapPlaceholder();
                    RenderMiniMapFullAsync();
                    return;
                }

                // 更新紅框顯示
                UpdateMiniMapRedBox();
            }
            catch
            {
                // 忽略錯誤
            }
        }

        /// <summary>
        /// 顯示小地圖佔位圖（渲染中提示）
        /// </summary>
        private void ShowMiniMapPlaceholder()
        {
            Bitmap placeholder = new Bitmap(MINIMAP_SIZE, MINIMAP_SIZE);
            using (Graphics g = Graphics.FromImage(placeholder))
            {
                g.Clear(Color.FromArgb(30, 30, 30));
                using (var font = new Font("Microsoft JhengHei", 12))
                using (var brush = new SolidBrush(Color.Gray))
                {
                    string text = "小地圖渲染中...";
                    var size = g.MeasureString(text, font);
                    g.DrawString(text, font, brush,
                        (MINIMAP_SIZE - size.Width) / 2,
                        (MINIMAP_SIZE - size.Height) / 2);
                }
            }
            miniMapPictureBox.Image?.Dispose();
            miniMapPictureBox.Image = placeholder;
        }

        /// <summary>
        /// 背景渲染完整的小地圖（與主地圖 RenderS32Map 邏輯相同，只是縮小）
        /// </summary>
        private void RenderMiniMapFullAsync()
        {
            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
                return;

            int mapWidth = _viewState.MapWidth;
            int mapHeight = _viewState.MapHeight;
            if (mapWidth <= 0 || mapHeight <= 0)
                return;

            // 標記正在渲染
            _miniMapRendering = true;

            // 計算縮放比例（在 UI 執行緒計算並快取）
            float scale = Math.Min((float)MINIMAP_SIZE / mapWidth, (float)MINIMAP_SIZE / mapHeight);
            int scaledWidth = (int)(mapWidth * scale);
            int scaledHeight = (int)(mapHeight * scale);

            _miniMapScale = scale;
            _miniMapOffsetX = (MINIMAP_SIZE - scaledWidth) / 2;
            _miniMapOffsetY = (MINIMAP_SIZE - scaledHeight) / 2;

            // 複製需要的資料（避免跨執行緒存取）
            var s32FilesSnapshot = _document.S32Files.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // 建立勾選的 S32 檔案清單
            HashSet<string> checkedFilePaths = new HashSet<string>();
            for (int i = 0; i < lstS32Files.Items.Count; i++)
            {
                if (lstS32Files.GetItemChecked(i) && lstS32Files.Items[i] is S32FileItem item)
                {
                    checkedFilePaths.Add(item.FilePath);
                }
            }

            // 決定渲染模式：超過 10 個 S32 時使用簡化渲染
            int s32Count = checkedFilePaths.Count;
            bool useSimplifiedRendering = s32Count > 10;

            // 背景執行緒渲染
            Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    int blockWidth = 64 * 24 * 2;  // 3072
                    int blockHeight = 64 * 12 * 2; // 1536

                    // 建立小地圖 Bitmap
                    Bitmap miniBitmap = new Bitmap(scaledWidth, scaledHeight, PixelFormat.Format16bppRgb555);

                    // 透明色設定
                    ImageAttributes vAttr = new ImageAttributes();
                    vAttr.SetColorKey(Color.FromArgb(0), Color.FromArgb(0));

                    using (Graphics g = Graphics.FromImage(miniBitmap))
                    {
                        g.Clear(Color.Black);

                        // 使用與主地圖相同的排序方式
                        var sortedFilePaths = Utils.SortDesc(s32FilesSnapshot.Keys);

                        if (useSimplifiedRendering)
                        {
                            // 簡化渲染：直接在小地圖上繪製取樣的格子
                            RenderMiniMapSimplified(g, sortedFilePaths, s32FilesSnapshot, checkedFilePaths, scale);
                        }
                        else
                        {
                            // 完整渲染：渲染每個 S32 Block 後縮小
                            foreach (object filePathObj in sortedFilePaths)
                            {
                                string filePath = filePathObj as string;
                                if (filePath == null || !s32FilesSnapshot.ContainsKey(filePath)) continue;
                                if (!checkedFilePaths.Contains(filePath)) continue;

                                var s32Data = s32FilesSnapshot[filePath];

                                // 使用與主地圖相同的座標計算
                                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                                int blockX = loc[0];
                                int blockY = loc[1];

                                // 渲染這個 S32 區塊（Layer1 + Layer4，小地圖不顯示 Layer2）- 使用快取
                                Bitmap blockBmp = GetOrRenderS32Block(s32Data, true, false, true);

                                // 縮小繪製到小地圖
                                int destX = (int)(blockX * scale);
                                int destY = (int)(blockY * scale);
                                int destW = (int)(blockWidth * scale);
                                int destH = (int)(blockHeight * scale);

                                g.DrawImage(blockBmp,
                                    new Rectangle(destX, destY, destW, destH),
                                    0, 0, blockBmp.Width, blockBmp.Height,
                                    GraphicsUnit.Pixel, vAttr);
                                // 注意：不 Dispose，因為 blockBmp 可能來自快取
                            }
                        }
                    }

                    sw.Stop();
                    string mode = useSimplifiedRendering ? "simplified" : "full";
                    LogPerf($"[MINIMAP] Rendered {s32Count} S32 blocks in {sw.ElapsedMilliseconds}ms, size={scaledWidth}x{scaledHeight}, mode={mode}");

                    // 回到 UI 執行緒更新
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        lock (_miniMapLock)
                        {
                            if (_miniMapFullBitmap != null)
                                _miniMapFullBitmap.Dispose();
                            _miniMapFullBitmap = miniBitmap;
                        }
                        _miniMapRendering = false;

                        // 渲染完成，更新顯示
                        UpdateMiniMapRedBox();
                    });
                }
                catch
                {
                    _miniMapRendering = false;
                }
            });
        }

        /// <summary>
        /// 只更新小地圖紅框位置（不重新渲染底圖）
        /// </summary>
        private void UpdateMiniMapRedBox()
        {
            lock (_miniMapLock)
            {
                if (_miniMapFullBitmap == null)
                    return;

                // 建立顯示圖（底圖 + 紅框）
                Bitmap displayBitmap = new Bitmap(MINIMAP_SIZE, MINIMAP_SIZE);
                using (Graphics g = Graphics.FromImage(displayBitmap))
                {
                    g.Clear(Color.Black);

                    // 繪製小地圖底圖（置中）
                    g.DrawImage(_miniMapFullBitmap, _miniMapOffsetX, _miniMapOffsetY);

                    // 繪製視窗位置紅框
                    if (s32MapPanel.Width > 0 && s32MapPanel.Height > 0)
                    {
                        int scrollX = _viewState.ScrollX;
                        int scrollY = _viewState.ScrollY;
                        int viewportWidthWorld = (int)(s32MapPanel.Width / s32ZoomLevel);
                        int viewportHeightWorld = (int)(s32MapPanel.Height / s32ZoomLevel);

                        int viewX = (int)(scrollX * _miniMapScale) + _miniMapOffsetX;
                        int viewY = (int)(scrollY * _miniMapScale) + _miniMapOffsetY;
                        int viewWidth = (int)(viewportWidthWorld * _miniMapScale);
                        int viewHeight = (int)(viewportHeightWorld * _miniMapScale);

                        using (Pen viewPortPen = new Pen(Color.Red, 2))
                        {
                            g.DrawRectangle(viewPortPen, viewX, viewY, viewWidth, viewHeight);
                        }
                    }
                }

                if (miniMapPictureBox.Image != null)
                    miniMapPictureBox.Image.Dispose();
                miniMapPictureBox.Image = displayBitmap;
            }
        }

        /// <summary>
        /// 清除小地圖快取（地圖變更時呼叫）
        /// </summary>
        private void ClearMiniMapCache()
        {
            lock (_miniMapLock)
            {
                if (_miniMapFullBitmap != null)
                {
                    _miniMapFullBitmap.Dispose();
                    _miniMapFullBitmap = null;
                }
            }
        }

        /// <summary>
        /// 簡化版 Mini Map 渲染：取樣渲染 + 快取
        /// </summary>
        private void RenderMiniMapSimplified(Graphics g, object[] sortedFilePaths,
            Dictionary<string, S32Data> s32FilesSnapshot, HashSet<string> checkedFilePaths, float scale)
        {
            // 根據壓縮比例動態計算取樣率
            // scale 越小，取樣間隔越大
            int sampleStep = Math.Max(1, (int)(1.0 / (scale * 24)));
            sampleStep = Math.Min(sampleStep, 8);

            int blockWidth = 64 * 24 * 2;  // 3072
            int blockHeight = 64 * 12 * 2; // 1536

            // 透明色設定
            ImageAttributes vAttr = new ImageAttributes();
            vAttr.SetColorKey(Color.FromArgb(0), Color.FromArgb(0));

            foreach (object filePathObj in sortedFilePaths)
            {
                string filePath = filePathObj as string;
                if (filePath == null || !s32FilesSnapshot.ContainsKey(filePath)) continue;
                if (!checkedFilePaths.Contains(filePath)) continue;

                var s32Data = s32FilesSnapshot[filePath];

                // 取樣渲染（帶快取）
                Bitmap blockBmp = GetOrRenderS32BlockSampled(s32Data, sampleStep);
                if (blockBmp == null) continue;

                // S32 Block 的世界座標位置
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int blockX = loc[0];
                int blockY = loc[1];

                // 縮小繪製到小地圖
                int destX = (int)(blockX * scale);
                int destY = (int)(blockY * scale);
                int destW = (int)(blockWidth * scale);
                int destH = (int)(blockHeight * scale);

                g.DrawImage(blockBmp,
                    new Rectangle(destX, destY, destW, destH),
                    0, 0, blockBmp.Width, blockBmp.Height,
                    GraphicsUnit.Pixel, vAttr);
                // 不 Dispose，因為是快取
            }
        }

        // Mini map 專用的取樣 S32 Block 快取
        private System.Collections.Concurrent.ConcurrentDictionary<string, Bitmap> _s32BlockCacheMiniMap
            = new System.Collections.Concurrent.ConcurrentDictionary<string, Bitmap>();

        /// <summary>
        /// 取得取樣版的 S32 Block（帶快取）
        /// </summary>
        private Bitmap GetOrRenderS32BlockSampled(S32Data s32Data, int sampleStep)
        {
            string cacheKey = $"{s32Data.FilePath}_s{sampleStep}";
            if (_s32BlockCacheMiniMap.TryGetValue(cacheKey, out Bitmap cached))
            {
                return cached;
            }

            Bitmap rendered = RenderS32BlockSampled(s32Data, sampleStep);
            _s32BlockCacheMiniMap.TryAdd(cacheKey, rendered);
            return rendered;
        }

        /// <summary>
        /// 渲染 S32 Block 的取樣版本（Layer1 + Layer4，每個取樣點重複畫填滿區域）
        /// </summary>
        private Bitmap RenderS32BlockSampled(S32Data s32Data, int sampleStep)
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

                // Layer1（地板）- 取樣並填滿
                for (int sy = 0; sy < 64; sy += sampleStep)
                {
                    for (int sx = 0; sx < 128; sx += sampleStep)
                    {
                        var cell = s32Data.Layer1[sy, sx];
                        if (cell == null || cell.TileId == 0) continue;

                        // 用取樣的 tile 填滿整個區域
                        for (int dy = 0; dy < sampleStep && sy + dy < 64; dy++)
                        {
                            for (int dx = 0; dx < sampleStep && sx + dx < 128; dx++)
                            {
                                int x = sx + dx;
                                int y = sy + dy;

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

                // Layer4（物件）- 也取樣，只畫落在取樣格子上的物件
                var sortedObjects = s32Data.Layer4.OrderBy(o => o.Layer).ToList();
                foreach (var obj in sortedObjects)
                {
                    // 只畫落在取樣點上的物件
                    if (obj.X % sampleStep != 0 || obj.Y % sampleStep != 0) continue;

                    int baseX = 0;
                    int baseY = 63 * 12;
                    baseX -= 24 * (obj.X / 2);
                    baseY -= 12 * (obj.X / 2);

                    int pixelX = baseX + obj.X * 24 + obj.Y * 24;
                    int pixelY = baseY + obj.Y * 12;

                    DrawTilToBufferDirect(pixelX, pixelY, obj.TileId, obj.IndexId, rowpix, ptr, blockWidth, blockHeight);
                }
            }

            result.UnlockBits(bmpData);
            return result;
        }

        /// <summary>
        /// 直接複製 bitmap 像素（比 Graphics.DrawImage 快，支援透明色 0）
        /// </summary>
        private void CopyBitmapDirect(Bitmap src, Bitmap dst, int dstX, int dstY)
        {
            int srcW = src.Width;
            int srcH = src.Height;
            int dstW = dst.Width;
            int dstH = dst.Height;

            // 計算實際複製範圍
            int startX = Math.Max(0, -dstX);
            int startY = Math.Max(0, -dstY);
            int endX = Math.Min(srcW, dstW - dstX);
            int endY = Math.Min(srcH, dstH - dstY);

            if (startX >= endX || startY >= endY) return;

            Rectangle srcRect = new Rectangle(0, 0, srcW, srcH);
            Rectangle dstRect = new Rectangle(0, 0, dstW, dstH);

            BitmapData srcData = src.LockBits(srcRect, ImageLockMode.ReadOnly, PixelFormat.Format16bppRgb555);
            BitmapData dstData = dst.LockBits(dstRect, ImageLockMode.ReadWrite, PixelFormat.Format16bppRgb555);

            unsafe
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;
                int srcStride = srcData.Stride;
                int dstStride = dstData.Stride;

                for (int y = startY; y < endY; y++)
                {
                    ushort* srcRow = (ushort*)(srcPtr + y * srcStride + startX * 2);
                    ushort* dstRow = (ushort*)(dstPtr + (y + dstY) * dstStride + (startX + dstX) * 2);

                    for (int x = startX; x < endX; x++)
                    {
                        ushort pixel = *srcRow++;
                        if (pixel != 0)  // 跳過透明色
                        {
                            *dstRow = pixel;
                        }
                        dstRow++;
                    }
                }
            }

            src.UnlockBits(srcData);
            dst.UnlockBits(dstData);
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
            LogPerf($"[MOUSE-WHEEL] delta={e.Delta}, modifiers={Control.ModifierKeys}");

            // Ctrl+滾輪 = 縮放
            if (Control.ModifierKeys == Keys.Control)
            {
                // 檢查是否有載入地圖
                if (_viewState.MapWidth <= 0 || _viewState.MapHeight <= 0)
                {
                    LogPerf($"[MOUSE-WHEEL] no map loaded, mapWidth={_viewState.MapWidth}, mapHeight={_viewState.MapHeight}");
                    return;
                }

                double oldZoom = pendingS32ZoomLevel;
                if (e.Delta > 0)
                {
                    pendingS32ZoomLevel = Math.Min(ZOOM_MAX, pendingS32ZoomLevel + ZOOM_STEP);
                }
                else
                {
                    pendingS32ZoomLevel = Math.Max(ZOOM_MIN, pendingS32ZoomLevel - ZOOM_STEP);
                }

                LogPerf($"[MOUSE-WHEEL] zoom oldZoom={oldZoom}, newZoom={pendingS32ZoomLevel}");

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
            int scrollAmount = (int)(100 / s32ZoomLevel);  // 捲動量（世界座標像素）
            int currentX = _viewState.ScrollX;
            int currentY = _viewState.ScrollY;

            // 計算最大捲動值（世界座標）
            int maxScrollX = Math.Max(0, _viewState.MapWidth - (int)(s32MapPanel.Width / s32ZoomLevel));
            int maxScrollY = Math.Max(0, _viewState.MapHeight - (int)(s32MapPanel.Height / s32ZoomLevel));

            if (Control.ModifierKeys == Keys.Shift)
            {
                // 左右捲動
                int newX = currentX - (e.Delta > 0 ? scrollAmount : -scrollAmount);
                newX = Math.Max(0, Math.Min(newX, maxScrollX));
                _viewState.SetScrollSilent(newX, currentY);
            }
            else
            {
                // 上下捲動
                int newY = currentY - (e.Delta > 0 ? scrollAmount : -scrollAmount);
                newY = Math.Max(0, Math.Min(newY, maxScrollY));
                _viewState.SetScrollSilent(currentX, newY);
            }

            // 檢查是否需要重新渲染
            CheckAndRerenderIfNeeded();

            // 更新小地圖
            UpdateMiniMap();

            // 阻止事件繼續傳遞
            ((HandledMouseEventArgs)e).Handled = true;
        }

        // 執行實際的縮放操作（使用 Viewport 渲染）
        private void ApplyS32Zoom(double targetZoomLevel)
        {
            try
            {
                LogPerf($"[APPLY-ZOOM] start, targetZoom={targetZoomLevel}, currentZoom={s32ZoomLevel}");

                // 檢查是否有載入地圖
                if (_viewState.MapWidth <= 0 || _viewState.MapHeight <= 0)
                {
                    LogPerf($"[APPLY-ZOOM] no map loaded");
                    return;
                }

                // 更新縮放級別
                s32ZoomLevel = targetZoomLevel;
                _viewState.ZoomLevel = targetZoomLevel;

                // 注意：不要清除舊的渲染狀態，讓舊 bitmap 繼續顯示直到新的準備好
                // NeedsRerender() 會檢測到縮放改變並觸發重新渲染

                LogPerf($"[APPLY-ZOOM] calling RenderS32Map");

                // 重新渲染（縮放改變會觸發重新渲染）
                RenderS32Map();

                LogPerf($"[APPLY-ZOOM] calling UpdateMiniMap");

                // 更新小地圖
                UpdateMiniMap();

                // 更新狀態欄顯示縮放級別
                this.lblS32Info.Text = $"縮放: {s32ZoomLevel:P0}";

                LogPerf($"[APPLY-ZOOM] done");
            }
            catch (Exception ex)
            {
                LogPerf($"[APPLY-ZOOM] error: {ex.Message}");
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
                // 使用 ViewState 的地圖大小
                int pictureWidth = _viewState.MapWidth > 0 ? _viewState.MapWidth : this.s32PictureBox.Width;
                int pictureHeight = _viewState.MapHeight > 0 ? _viewState.MapHeight : this.s32PictureBox.Height;

                if (pictureWidth <= 0 || pictureHeight <= 0)
                    return;

                int miniWidth = MINIMAP_SIZE;
                int miniHeight = MINIMAP_SIZE;

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

                // 計算點擊位置對應的主地圖世界座標
                int mapPosX = (int)((float)clickX / scaledWidth * pictureWidth);
                int mapPosY = (int)((float)clickY / scaledHeight * pictureHeight);

                // 計算捲動位置（世界座標），讓點擊位置成為視窗中央
                int viewportWidthWorld = (int)(s32MapPanel.Width / s32ZoomLevel);
                int viewportHeightWorld = (int)(s32MapPanel.Height / s32ZoomLevel);
                int newScrollX = mapPosX - viewportWidthWorld / 2;
                int newScrollY = mapPosY - viewportHeightWorld / 2;

                // 限制在有效範圍內（世界座標）
                int maxScrollX = Math.Max(0, pictureWidth - viewportWidthWorld);
                int maxScrollY = Math.Max(0, pictureHeight - viewportHeightWorld);
                newScrollX = Math.Max(0, Math.Min(newScrollX, maxScrollX));
                newScrollY = Math.Max(0, Math.Min(newScrollY, maxScrollY));

                // 設定 ViewState 的捲動位置
                _viewState.SetScrollSilent(newScrollX, newScrollY);

                // 根據參數決定是否更新小地圖和重新渲染
                if (updateMiniMapFlag)
                {
                    CheckAndRerenderIfNeeded();
                    UpdateMiniMap();
                }
                else
                {
                    // 拖拽時只更新小地圖紅框位置和重繪（快速繪製）
                    s32PictureBox.Invalidate();
                    UpdateMiniMapRedBox();
                }
            }
            catch
            {
                // 忽略錯誤
            }
        }

        // 小地圖點擊跳轉（保留給滑鼠右鍵查詢 S32 檔案用）
        private void miniMapPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            // 右鍵點擊顯示 S32 檔案資訊
            if (e.Button == MouseButtons.Right)
            {
                try
                {
                    // 使用 ViewState 的地圖大小
                    int pictureWidth = _viewState.MapWidth > 0 ? _viewState.MapWidth : this.s32PictureBox.Width;
                    int pictureHeight = _viewState.MapHeight > 0 ? _viewState.MapHeight : this.s32PictureBox.Height;

                    if (pictureWidth <= 0 || pictureHeight <= 0)
                        return;

                    int miniWidth = MINIMAP_SIZE;
                    int miniHeight = MINIMAP_SIZE;

                    float scaleX = (float)miniWidth / pictureWidth;
                    float scaleY = (float)miniHeight / pictureHeight;
                    float scale = Math.Min(scaleX, scaleY);

                    int scaledWidth = (int)(pictureWidth * scale);
                    int scaledHeight = (int)(pictureHeight * scale);
                    int offsetX = (miniWidth - scaledWidth) / 2;
                    int offsetY = (miniHeight - scaledHeight) / 2;

                    int clickX = e.X - offsetX;
                    int clickY = e.Y - offsetY;

                    if (clickX < 0 || clickY < 0 || clickX > scaledWidth || clickY > scaledHeight)
                        return;

                    float clickRatioX = (float)clickX / scaledWidth;
                    float clickRatioY = (float)clickY / scaledHeight;

                    int mapX = (int)(clickRatioX * pictureWidth);
                    int mapY = (int)(clickRatioY * pictureHeight);

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
        // 類型定義已移至 Models/S32DataModels.cs

        // 當前選擇的 S32 檔案資訊（用於顯示）
        private S32FileItem currentS32FileItem;

        // 便捷屬性：從字典中獲取當前選中的 S32 資料
        private S32Data currentS32Data
        {
            get
            {
                if (currentS32FileItem != null && _document.S32Files.ContainsKey(currentS32FileItem.FilePath))
                {
                    return _document.S32Files[currentS32FileItem.FilePath];
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
        private Rectangle copyRegionBounds;               // 複製區域的範圍（螢幕座標）

        // 複製/刪除設定（由 Dialog 設定）
        private bool copySettingLayer1 = true;
        private bool copySettingLayer2 = true;
        private bool copySettingLayer3 = true;
        private bool copySettingLayer4 = true;
        private bool copySettingLayer5 = true;
        private bool copySettingLayer6to8 = true;
        // 向後相容屬性
        private bool copySettingLayer5to8 => copySettingLayer5 || copySettingLayer6to8;

        // 根據遊戲座標找到對應的 S32Data
        private S32Data GetS32DataByGameCoords(int gameX, int gameY)
        {
            foreach (var s32Data in _document.S32Files.Values)
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
            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
                return (-1, -1, null, -1, -1);

            // 使用 ViewState 的捲動位置（世界座標）
            // 將螢幕座標轉換為世界座標（考慮縮放和捲動）
            int worldX = (int)(screenX / s32ZoomLevel) + _viewState.ScrollX;
            int worldY = (int)(screenY / s32ZoomLevel) + _viewState.ScrollY;

            // 使用空間索引快速查找可能包含這個點的 S32
            // 建立一個小範圍的查詢矩形（點周圍的區域）
            Rectangle queryRect = new Rectangle(worldX - 48, worldY - 24, 96, 48);
            var candidateFiles = GetS32FilesInRect(queryRect);

            int blockWidth = 64 * 24 * 2;   // 3072
            int blockHeight = 64 * 12 * 2;  // 1536

            foreach (var filePath in candidateFiles)
            {
                if (!_document.S32Files.TryGetValue(filePath, out var s32Data))
                    continue;

                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 先檢查點是否在這個 S32 block 範圍內（粗略檢查）
                if (worldX < mx || worldX > mx + blockWidth || worldY < my || worldY > my + blockHeight)
                    continue;

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

            Struct.L1Map currentMap = Share.MapDataList[_document.MapId];

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
            int screenX = (int)((worldX - _viewState.ScrollX) * s32ZoomLevel);
            int screenY = (int)((worldY - _viewState.ScrollY) * s32ZoomLevel);

            return (screenX, screenY);
        }

        // 載入當前地圖的 s32 檔案清單並載入所有 S32 資料
        private void LoadS32FileList(string mapId)
        {
            var totalStopwatch = Stopwatch.StartNew();
            var phaseStopwatch = new Stopwatch();

            lstS32Files.Items.Clear();
            _checkedS32Files.Clear();  // 清空快取
            _document.S32Files.Clear();
            _document.MapId = mapId;
            lblS32Files.Text = "S32 檔案清單";  // 重置標籤

            // 清除所有快取
            ClearS32BlockCache();
            ClearMiniMapCache();
            cachedAggregatedTiles.Clear();
            tileDataCache.Clear();
            _tilFileCache.Clear();

            // 清除 viewport bitmap
            lock (_viewportBitmapLock)
            {
                if (_viewportBitmap != null)
                {
                    _viewportBitmap.Dispose();
                    _viewportBitmap = null;
                }
            }

            // 重置 ViewState
            _viewState.Reset();

            // 清除編輯狀態（保留剪貼簿以支援跨地圖複製）
            _editState.SelectedCells.Clear();
            // 注意：不清除 CellClipboard、Layer2Clipboard、Layer5-8Clipboard，以支援跨地圖複製
            // _editState.CellClipboard.Clear();
            // _editState.Layer2Clipboard.Clear();
            // _editState.Layer5Clipboard.Clear();
            // _editState.Layer6Clipboard.Clear();
            // _editState.Layer7Clipboard.Clear();
            // _editState.Layer8Clipboard.Clear();
            _editState.UndoHistory.Clear();
            _editState.RedoHistory.Clear();
            _editState.SelectedLayer4Groups.Clear();
            _editState.IsFilteringLayer4Groups = false;
            // hasLayer4Clipboard 也保留，不清除
            isLayer4CopyMode = false;
            selectedRegion = new Rectangle();
            copyRegionBounds = new Rectangle();

            // 清除群組縮圖
            lvGroupThumbnails.Items.Clear();

            // 隱藏 Layer5 異常按鈕
            btnToolCheckL5Invalid.Visible = false;

            // 從 Share.MapDataList 取得地圖資料
            if (!Share.MapDataList.ContainsKey(mapId))
                return;

            Struct.L1Map currentMap = Share.MapDataList[mapId];

            // 階段 1: UI 清單建立
            phaseStopwatch.Restart();
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
                    _checkedS32Files.Add(filePath);  // 加入快取
                    s32FileItems.Add(item);
                }
            }
            long uiListMs = phaseStopwatch.ElapsedMilliseconds;

            // 自動選擇第一個S32檔案
            if (lstS32Files.Items.Count > 0)
            {
                lstS32Files.SelectedIndex = 0;
            }

            // 更新 S32 檔案清單標籤顯示數量
            lblS32Files.Text = $"S32 檔案清單 ({s32FileItems.Count})";

            this.toolStripStatusLabel1.Text = $"找到 {s32FileItems.Count} 個 S32 檔案，正在載入...";

            // 使用背景執行緒順序載入（避免並行造成磁碟競爭）
            Task.Run(() =>
            {
                long totalFileReadMs = 0;
                long totalParseMs = 0;
                int loadedCount = 0;

                // 階段 1: 循序讀取所有檔案（避免磁碟競爭）
                var fileDataList = new List<(S32FileItem item, byte[] data)>();
                var readSw = Stopwatch.StartNew();
                foreach (var item in s32FileItems)
                {
                    try
                    {
                        byte[] data = File.ReadAllBytes(item.FilePath);
                        fileDataList.Add((item, data));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"讀取 S32 檔案失敗: {item.FilePath}, 錯誤: {ex.Message}");
                    }
                }
                readSw.Stop();
                totalFileReadMs = readSw.ElapsedMilliseconds;

                // 階段 2: 平行解析所有檔案
                var parsedResults = new System.Collections.Concurrent.ConcurrentDictionary<string, S32Data>();
                var parseSw = Stopwatch.StartNew();
                System.Threading.Tasks.Parallel.ForEach(fileDataList, fileData =>
                {
                    try
                    {
                        S32Data s32Data = ParseS32File(fileData.data);
                        s32Data.FilePath = fileData.item.FilePath;
                        s32Data.SegInfo = fileData.item.SegInfo;
                        s32Data.IsModified = false;
                        parsedResults[fileData.item.FilePath] = s32Data;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"解析 S32 檔案失敗: {fileData.item.FilePath}, 錯誤: {ex.Message}");
                    }
                });
                parseSw.Stop();
                totalParseMs = parseSw.ElapsedMilliseconds;

                // 將結果複製到 _document.S32Files
                foreach (var kvp in parsedResults)
                {
                    _document.S32Files[kvp.Key] = kvp.Value;
                }
                loadedCount = parsedResults.Count;

                LogPerf($"[S32-LOAD] {loadedCount} files, read={totalFileReadMs}ms (seq), parse={totalParseMs}ms (parallel)");

                // 建立空間索引（用於快速查找 worldRect 內的 S32）
                BuildS32SpatialIndex();

                // 預載入所有 tile 檔案（背景執行，與 UI 更新並行）
                PreloadTilesAsync(_document.S32Files.Values.ToList());

                // 記錄背景載入完成時間
                long bgLoadMs = totalStopwatch.ElapsedMilliseconds;

                // 載入完成後更新 UI
                this.Invoke((MethodInvoker)delegate
                {
                    long invokeStartMs = totalStopwatch.ElapsedMilliseconds;
                    long invokeOverheadMs = invokeStartMs - bgLoadMs;

                    // 階段 3: 設定初始捲動位置到地圖中央（在渲染之前）
                    phaseStopwatch.Restart();
                    if (_document.S32Files.Count > 0)
                    {
                        SetInitialScrollToCenter(currentMap);
                    }
                    long scrollSetupMs = phaseStopwatch.ElapsedMilliseconds;

                    // 階段 4: Viewport 渲染（已經在中央位置）
                    phaseStopwatch.Restart();
                    if (_document.S32Files.Count > 0)
                    {
                        RenderS32Map();
                    }
                    long viewportRenderMs = phaseStopwatch.ElapsedMilliseconds;

                    // 階段 5: 小地圖更新
                    phaseStopwatch.Restart();
                    if (_document.S32Files.Count > 0)
                    {
                        UpdateMiniMap();
                    }
                    long miniMapMs = phaseStopwatch.ElapsedMilliseconds;

                    // 階段 6: Tile 列表更新（背景執行）
                    phaseStopwatch.Restart();
                    if (_document.S32Files.Count > 0)
                    {
                        UpdateTileListAsync();
                    }
                    long tileListMs = phaseStopwatch.ElapsedMilliseconds;  // 只計算啟動時間

                    // 階段 7: 群組縮圖更新（背景執行，不計入載入時間）
                    phaseStopwatch.Restart();
                    if (_document.S32Files.Count > 0)
                    {
                        UpdateGroupThumbnailsList();
                        UpdateLayer5InvalidButton();
                    }
                    long thumbnailStartMs = phaseStopwatch.ElapsedMilliseconds;

                    totalStopwatch.Stop();
                    long totalMs = totalStopwatch.ElapsedMilliseconds;

                    // 計算各階段佔比
                    long sumMs = uiListMs + totalFileReadMs + totalParseMs + invokeOverheadMs + scrollSetupMs + viewportRenderMs + miniMapMs + tileListMs + thumbnailStartMs;
                    long unmeasuredMs = totalMs - sumMs;

                    // 顯示載入時間統計（群組縮圖在背景執行，完成後會更新自己的標籤）
                    string stats = $"Loaded ({_document.S32Files.Count} S32) | " +
                                   $"Total: {totalMs}ms | " +
                                   $"UI: {uiListMs}ms | " +
                                   $"FileRead: {totalFileReadMs}ms | " +
                                   $"Parse: {totalParseMs}ms | " +
                                   $"Viewport: {viewportRenderMs}ms | " +
                                   $"TileList: {tileListMs}ms | " +
                                   $"Thumbnails: background";

                    this.toolStripStatusLabel1.Text = stats;

                    // Console log detailed timing
                    Console.WriteLine("========================================");
                    Console.WriteLine($"[LOAD TIMING] Map: {mapId}, S32 Files: {_document.S32Files.Count}");
                    Console.WriteLine("========================================");
                    Console.WriteLine($"  UI List:        {uiListMs,6} ms  ({(uiListMs * 100.0 / totalMs),5:F1}%)");
                    Console.WriteLine($"  File Read:      {totalFileReadMs,6} ms  ({(totalFileReadMs * 100.0 / totalMs),5:F1}%)  [{s32FileItems.Count} files]");
                    Console.WriteLine($"  S32 Parse:      {totalParseMs,6} ms  ({(totalParseMs * 100.0 / totalMs),5:F1}%)");
                    Console.WriteLine($"  Invoke Wait:    {invokeOverheadMs,6} ms  ({(invokeOverheadMs * 100.0 / totalMs),5:F1}%)  [thread switch]");
                    Console.WriteLine($"  Scroll Setup:   {scrollSetupMs,6} ms  ({(scrollSetupMs * 100.0 / totalMs),5:F1}%)  [set center pos]");
                    Console.WriteLine($"  Viewport:       {viewportRenderMs,6} ms  ({(viewportRenderMs * 100.0 / totalMs),5:F1}%)");
                    Console.WriteLine($"  Mini Map:       {miniMapMs,6} ms  ({(miniMapMs * 100.0 / totalMs),5:F1}%)");
                    Console.WriteLine($"  Tile List:      {tileListMs,6} ms  ({(tileListMs * 100.0 / totalMs),5:F1}%)");
                    Console.WriteLine($"  Thumb Start:    {thumbnailStartMs,6} ms  ({(thumbnailStartMs * 100.0 / totalMs),5:F1}%)  [async start]");
                    Console.WriteLine($"  Unmeasured:     {unmeasuredMs,6} ms  ({(unmeasuredMs * 100.0 / totalMs),5:F1}%)");
                    Console.WriteLine("----------------------------------------");
                    Console.WriteLine($"  TOTAL:          {totalMs,6} ms  (thumbnails in background)");
                    Console.WriteLine("========================================");
                });
            });
        }

        // 分析第三層屬性類型
        private void AnalyzeLayer3Attributes()
        {
            // 統計 Attribute1 和 Attribute2 的所有不同值
            Dictionary<short, int> attr1Values = new Dictionary<short, int>();
            Dictionary<short, int> attr2Values = new Dictionary<short, int>();

            foreach (var s32Data in _document.S32Files.Values)
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
            sb.AppendLine($"地圖: {_document.MapId}");
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

        // S32 檔案清單右鍵跳轉
        private void lstS32Files_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            // 取得點擊位置的項目
            int index = lstS32Files.IndexFromPoint(e.Location);
            if (index < 0 || index >= lstS32Files.Items.Count)
                return;

            var item = lstS32Files.Items[index] as S32FileItem;
            if (item == null)
                return;

            // 選取該項目
            lstS32Files.SelectedIndex = index;

            // 建立右鍵選單
            ContextMenuStrip menu = new ContextMenuStrip();

            // 跳轉選項
            ToolStripMenuItem jumpItem = new ToolStripMenuItem("跳轉至此區塊");
            jumpItem.Click += (s, args) =>
            {
                JumpToS32Block(item);
            };
            menu.Items.Add(jumpItem);

            // 查看詳細選項
            ToolStripMenuItem detailItem = new ToolStripMenuItem("查看詳細資料");
            detailItem.Click += (s, args) =>
            {
                ShowS32Details(item);
            };
            menu.Items.Add(detailItem);

            menu.Show(lstS32Files, e.Location);
        }

        // 跳轉至指定 S32 區塊
        private void JumpToS32Block(S32FileItem item)
        {
            // 計算該 S32 區塊的中心世界座標
            var segInfo = item.SegInfo;
            int centerX = (segInfo.nLinBeginX + segInfo.nLinEndX) / 2;
            int centerY = (segInfo.nLinBeginY + segInfo.nLinEndY) / 2;

            // 轉換為世界像素座標（每格 24x24 像素）
            int worldPixelX = centerX * 24;
            int worldPixelY = centerY * 24;

            // 計算目標捲動位置（讓該區塊置中）
            int targetScrollX = worldPixelX - _viewState.ViewportWidth / 2;
            int targetScrollY = worldPixelY - _viewState.ViewportHeight / 2;

            // 設定捲動位置
            _viewState.ScrollX = Math.Max(0, Math.Min(targetScrollX, _viewState.MaxScrollX));
            _viewState.ScrollY = Math.Max(0, Math.Min(targetScrollY, _viewState.MaxScrollY));

            // 更新捲軸
            hScrollBar1.Value = Math.Min(_viewState.ScrollX, hScrollBar1.Maximum);
            vScrollBar1.Value = Math.Min(_viewState.ScrollY, vScrollBar1.Maximum);

            // 重新渲染
            RenderS32Map();

            this.toolStripStatusLabel1.Text = $"跳轉至 {item.DisplayName}";
        }

        // 顯示 S32 詳細資料
        private void ShowS32Details(S32FileItem item)
        {
            if (!_document.S32Files.TryGetValue(item.FilePath, out S32Data s32Data))
            {
                MessageBox.Show("無法載入 S32 資料", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 統計各層資料
            int layer1Count = 0;
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    var cell = s32Data.Layer1[y, x];
                    if (cell != null && cell.TileId > 0)
                        layer1Count++;
                }
            }

            int layer2Count = s32Data.Layer2.Count;

            int layer3Count = 0;
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    var attr = s32Data.Layer3[y, x];
                    if (attr != null && (attr.Attribute1 != 0 || attr.Attribute2 != 0))
                        layer3Count++;
                }
            }

            int layer4Count = s32Data.Layer4.Count;
            int layer4GroupCount = s32Data.Layer4.Select(o => o.GroupId).Distinct().Count();

            int layer5Count = s32Data.Layer5.Count;
            int layer6Count = s32Data.Layer6.Count;
            int layer7Count = s32Data.Layer7.Count;
            int layer8Count = s32Data.Layer8.Count;

            // 建立詳細資訊視窗
            Form detailForm = new Form();
            detailForm.Text = $"S32 詳細資料 - {item.DisplayName}";
            detailForm.Size = new Size(450, 420);
            detailForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            detailForm.StartPosition = FormStartPosition.CenterParent;
            detailForm.MaximizeBox = false;
            detailForm.MinimizeBox = false;

            // 檔案資訊
            GroupBox gbFile = new GroupBox { Text = "檔案資訊", Location = new Point(10, 10), Size = new Size(410, 80) };
            gbFile.Controls.Add(new Label { Text = $"檔案路徑: {item.FilePath}", Location = new Point(10, 20), Size = new Size(390, 20), AutoEllipsis = true });
            gbFile.Controls.Add(new Label { Text = $"區塊座標: ({item.SegInfo.nBlockX}, {item.SegInfo.nBlockY})", Location = new Point(10, 40), Size = new Size(190, 20) });
            gbFile.Controls.Add(new Label { Text = $"遊戲座標: ({item.SegInfo.nLinBeginX}~{item.SegInfo.nLinEndX}, {item.SegInfo.nLinBeginY}~{item.SegInfo.nLinEndY})", Location = new Point(200, 40), Size = new Size(200, 20) });
            detailForm.Controls.Add(gbFile);

            // 各層統計
            GroupBox gbLayers = new GroupBox { Text = "各層資料統計", Location = new Point(10, 100), Size = new Size(410, 230) };

            ListView lvLayers = new ListView();
            lvLayers.Location = new Point(10, 20);
            lvLayers.Size = new Size(390, 200);
            lvLayers.View = View.Details;
            lvLayers.FullRowSelect = true;
            lvLayers.GridLines = true;
            lvLayers.Columns.Add("層級", 80);
            lvLayers.Columns.Add("說明", 150);
            lvLayers.Columns.Add("數量", 80);
            lvLayers.Columns.Add("備註", 70);

            lvLayers.Items.Add(new ListViewItem(new[] { "Layer1", "地板圖塊", layer1Count.ToString(), $"/{64 * 128}" }));
            lvLayers.Items.Add(new ListViewItem(new[] { "Layer2", "第二層資料", layer2Count.ToString(), "" }));
            lvLayers.Items.Add(new ListViewItem(new[] { "Layer3", "地圖屬性", layer3Count.ToString(), $"/{64 * 64}" }));
            lvLayers.Items.Add(new ListViewItem(new[] { "Layer4", "物件", layer4Count.ToString(), $"{layer4GroupCount} 群組" }));
            lvLayers.Items.Add(new ListViewItem(new[] { "Layer5", "事件", layer5Count.ToString(), "" }));
            lvLayers.Items.Add(new ListViewItem(new[] { "Layer6", "使用的 TileId", layer6Count.ToString(), "" }));
            lvLayers.Items.Add(new ListViewItem(new[] { "Layer7", "傳送點", layer7Count.ToString(), "" }));
            lvLayers.Items.Add(new ListViewItem(new[] { "Layer8", "特效", layer8Count.ToString(), s32Data.Layer8HasExtendedData ? "擴展" : "" }));

            gbLayers.Controls.Add(lvLayers);
            detailForm.Controls.Add(gbLayers);

            // 按鈕
            Button btnClose = new Button { Text = "關閉", Location = new Point(350, 340), Size = new Size(80, 30) };
            btnClose.Click += (s, args) => detailForm.Close();
            detailForm.Controls.Add(btnClose);

            Button btnJump = new Button { Text = "跳轉至此", Location = new Point(260, 340), Size = new Size(80, 30) };
            btnJump.Click += (s, args) =>
            {
                JumpToS32Block(item);
                detailForm.Close();
            };
            detailForm.Controls.Add(btnJump);

            detailForm.ShowDialog();
        }

        // S32 清單勾選狀態變更事件
        private void lstS32Files_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // 更新項目的勾選狀態
            if (lstS32Files.Items[e.Index] is S32FileItem item)
            {
                item.IsChecked = (e.NewValue == CheckState.Checked);

                // 更新勾選檔案快取
                if (e.NewValue == CheckState.Checked)
                    _checkedS32Files.Add(item.FilePath);
                else
                    _checkedS32Files.Remove(item.FilePath);

                // 延遲觸發重新渲染（因為 ItemCheck 在狀態變更前觸發）
                if (!_isBatchCheckUpdate)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        RenderS32Map();
                    });
                }
            }
        }

        // 批次勾選更新標記（避免每次勾選都觸發渲染）
        private bool _isBatchCheckUpdate = false;

        // 全選 S32 檔案
        private void btnS32SelectAll_Click(object sender, EventArgs e)
        {
            _isBatchCheckUpdate = true;
            for (int i = 0; i < lstS32Files.Items.Count; i++)
            {
                lstS32Files.SetItemChecked(i, true);
            }
            _isBatchCheckUpdate = false;
            RenderS32Map();
        }

        // 全不選 S32 檔案
        private void btnS32SelectNone_Click(object sender, EventArgs e)
        {
            _isBatchCheckUpdate = true;
            for (int i = 0; i < lstS32Files.Items.Count; i++)
            {
                lstS32Files.SetItemChecked(i, false);
            }
            _isBatchCheckUpdate = false;
            RenderS32Map();
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
                        X = br.ReadByte(),
                        Y = br.ReadByte(),
                        IndexId = br.ReadByte(),
                        TileId = br.ReadUInt16(),
                        UK = br.ReadByte()
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
                            // 第五層 - 事件
                            if (layerStream.Position + 4 <= layerStream.Length)
                            {
                                int lv5Count = layerReader.ReadInt32();
                                for (int i = 0; i < lv5Count && layerStream.Position + 5 <= layerStream.Length; i++)
                                {
                                    s32Data.Layer5.Add(new Layer5Item
                                    {
                                        X = layerReader.ReadByte(),
                                        Y = layerReader.ReadByte(),
                                        ObjectIndex = layerReader.ReadUInt16(),
                                        Type = layerReader.ReadByte()
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
                                ushort lv8Num = layerReader.ReadUInt16();
                                bool hasExtendedData = (lv8Num >= 0x8000);
                                if (hasExtendedData)
                                {
                                    lv8Num = (ushort)(lv8Num & 0x7FFF);  // 取消高位
                                }
                                s32Data.Layer8HasExtendedData = hasExtendedData;

                                int itemSize = hasExtendedData ? 10 : 6;  // 6 bytes 基本, +4 bytes 擴展
                                for (int i = 0; i < lv8Num && layerStream.Position + itemSize <= layerStream.Length; i++)
                                {
                                    s32Data.Layer8.Add(new Layer8Item
                                    {
                                        SprId = layerReader.ReadUInt16(),
                                        X = layerReader.ReadUInt16(),
                                        Y = layerReader.ReadUInt16(),
                                        ExtendedData = hasExtendedData ? layerReader.ReadInt32() : 0
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
                if (!_document.S32Files.ContainsKey(filePath))
                {
                    this.toolStripStatusLabel1.Text = "選中的 S32 檔案不在記憶體中";
                    return;
                }

                S32Data s32Data = _document.S32Files[filePath];

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
            // 清除快取（因為快取的 bitmap 是用特定圖層設定渲染的）
            ClearS32BlockCache();

            // 使用防抖Timer，避免快速切換時多次渲染
            renderDebounceTimer.Stop();
            renderDebounceTimer.Start();
        }


        // 浮動圖層面板選項變更
        private void chkFloatLayer_CheckedChanged(object sender, EventArgs e)
        {
            // 同步到原本的 CheckBox（會觸發 S32Layer_CheckedChanged）
            if (sender == chkFloatLayer1)
            {
                chkLayer1.Checked = chkFloatLayer1.Checked;
            }
            else if (sender == chkFloatLayer2)
            {
                chkLayer2.Checked = chkFloatLayer2.Checked;
            }
            else if (sender == chkFloatLayer4)
            {
                chkLayer4.Checked = chkFloatLayer4.Checked;
            }
            else if (sender == chkFloatPassable)
            {
                chkShowPassable.Checked = chkFloatPassable.Checked;
            }
            else if (sender == chkFloatGrid)
            {
                chkShowGrid.Checked = chkFloatGrid.Checked;
            }
            else if (sender == chkFloatS32Boundary)
            {
                chkShowS32Boundary.Checked = chkFloatS32Boundary.Checked;
            }
            else if (sender == chkFloatLayer5)
            {
                chkShowLayer5.Checked = chkFloatLayer5.Checked;
            }

            // 更新圖示顯示狀態
            UpdateLayerIconText();
        }

        // 更新浮動圖層圖示文字（顯示目前啟用的層）
        private void UpdateLayerIconText()
        {
            // 圖示用不同顏色表示狀態 - 根據啟用層數量
            int enabledCount = 0;
            if (chkFloatLayer1.Checked) enabledCount++;
            if (chkFloatLayer2.Checked) enabledCount++;
            if (chkFloatLayer4.Checked) enabledCount++;
            if (chkFloatPassable.Checked) enabledCount++;
            if (chkFloatGrid.Checked) enabledCount++;
            if (chkFloatS32Boundary.Checked) enabledCount++;
            if (chkFloatLayer5.Checked) enabledCount++;

            if (enabledCount == 0)
            {
                lblLayerIcon.ForeColor = Color.Gray;
            }
            else if (enabledCount == 7)
            {
                lblLayerIcon.ForeColor = Color.LightGreen;
            }
            else
            {
                lblLayerIcon.ForeColor = Color.Yellow;
            }
        }

        // s32EditorPanel 大小變更時調整浮動面板位置
        private void s32EditorPanel_Resize(object sender, EventArgs e)
        {
            // 將浮動面板放在 s32MapPanel 區域的右上角（相對於 s32EditorPanel）
            int rightMargin = 10;
            int topMargin = 10;
            // s32MapPanel 的起點是 s32LayerControlPanel 下方
            int mapPanelTop = s32MapPanel.Top;
            int mapPanelRight = s32MapPanel.Right;
            layerFloatPanel.Location = new Point(mapPanelRight - layerFloatPanel.Width - rightMargin - 20, mapPanelTop + topMargin);
        }

        // 同步浮動面板與原本 CheckBox 的狀態
        private void SyncFloatPanelCheckboxes()
        {
            chkFloatLayer1.Checked = chkLayer1.Checked;
            chkFloatLayer2.Checked = chkLayer2.Checked;
            chkFloatLayer4.Checked = chkLayer4.Checked;
            chkFloatPassable.Checked = chkShowPassable.Checked;
            chkFloatGrid.Checked = chkShowGrid.Checked;
            chkFloatS32Boundary.Checked = chkShowS32Boundary.Checked;
            chkFloatLayer5.Checked = chkShowLayer5.Checked;
            UpdateLayerIconText();
        }

        // 複製設定按鈕點擊事件
        private void btnCopySettings_Click(object sender, EventArgs e)
        {
            using (var dialog = new CopySettingsDialog(copySettingLayer1, copySettingLayer2, copySettingLayer3, copySettingLayer4, copySettingLayer5, copySettingLayer6to8))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    copySettingLayer1 = dialog.CopyLayer1;
                    copySettingLayer2 = dialog.CopyLayer2;
                    copySettingLayer3 = dialog.CopyLayer3;
                    copySettingLayer4 = dialog.CopyLayer4;
                    copySettingLayer5 = dialog.CopyLayer5;
                    copySettingLayer6to8 = dialog.CopyLayer6to8;

                    // 更新按鈕文字顯示目前設定
                    var layers = new List<string>();
                    if (copySettingLayer1) layers.Add("L1");
                    if (copySettingLayer2) layers.Add("L2");
                    if (copySettingLayer3) layers.Add("L3");
                    if (copySettingLayer4) layers.Add("L4");
                    if (copySettingLayer5) layers.Add("L5");
                    if (copySettingLayer6to8) layers.Add("L6-8");
                    string layerInfo = layers.Count > 0 ? string.Join(",", layers) : "無";

                    this.toolStripStatusLabel1.Text = $"複製/刪除設定已更新: {layerInfo}";
                }
            }
        }

        // 複製地圖座標按鈕點擊事件
        private void btnCopyMapCoords_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Struct.L1Map currentMap = Share.MapDataList[_document.MapId];

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
                UpdatePassabilityHelpLabel();
            }
            else
            {
                // 啟用允許通行模式
                currentPassableEditMode = PassableEditMode.SetPassable;
                btnSetPassable.BackColor = Color.LightGreen;
                btnSetImpassable.BackColor = SystemColors.Control;
                // 取消 Layer5 編輯模式
                _editState.IsLayer5EditMode = false;
                btnEditLayer5.BackColor = SystemColors.Control;
                UpdateLayer5HelpLabel();
                // 自動顯示通行性覆蓋層
                EnsurePassabilityLayerVisible();
                this.toolStripStatusLabel1.Text = "允許通行模式：點擊格子設定 | Ctrl+左鍵繪製多邊形，右鍵完成";
                UpdatePassabilityHelpLabel();
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
                UpdatePassabilityHelpLabel();
            }
            else
            {
                // 啟用禁止通行模式
                currentPassableEditMode = PassableEditMode.SetImpassable;
                btnSetImpassable.BackColor = Color.LightCoral;
                btnSetPassable.BackColor = SystemColors.Control;
                // 取消 Layer5 編輯模式
                _editState.IsLayer5EditMode = false;
                btnEditLayer5.BackColor = SystemColors.Control;
                UpdateLayer5HelpLabel();
                // 自動顯示通行性覆蓋層
                EnsurePassabilityLayerVisible();
                this.toolStripStatusLabel1.Text = "禁止通行模式：點擊格子設定 | Ctrl+左鍵繪製多邊形，右鍵完成";
                UpdatePassabilityHelpLabel();
            }
        }

        // 確保通行性圖層可見
        private void EnsurePassabilityLayerVisible()
        {
            if (!chkShowPassable.Checked)
            {
                chkShowPassable.Checked = true;
                chkFloatPassable.Checked = true;
                UpdateLayerIconText();
                RenderS32Map();
            }
        }

        // 透明編輯按鈕點擊事件
        private void btnEditLayer5_Click(object sender, EventArgs e)
        {
            if (_editState.IsLayer5EditMode)
            {
                // 取消模式
                _editState.IsLayer5EditMode = false;
                btnEditLayer5.BackColor = SystemColors.Control;
                this.toolStripStatusLabel1.Text = "已取消透明編輯模式";
                UpdateLayer5HelpLabel();
                RenderS32Map();  // 重新渲染以移除群組覆蓋層
            }
            else
            {
                // 啟用透明編輯模式
                _editState.IsLayer5EditMode = true;
                btnEditLayer5.BackColor = Color.FromArgb(100, 180, 255);
                // 取消通行性編輯模式
                currentPassableEditMode = PassableEditMode.None;
                btnSetPassable.BackColor = SystemColors.Control;
                btnSetImpassable.BackColor = SystemColors.Control;
                UpdatePassabilityHelpLabel();
                // 自動顯示 Layer5 覆蓋層
                EnsureLayer5Visible();
                this.toolStripStatusLabel1.Text = "透明編輯模式：左鍵添加/右鍵刪除透明設定";
                UpdateLayer5HelpLabel();
                RenderS32Map();  // 重新渲染以顯示群組覆蓋層
            }
        }

        // 確保 Layer5 圖層可見
        private void EnsureLayer5Visible()
        {
            if (!chkShowLayer5.Checked)
            {
                chkShowLayer5.Checked = true;
                chkFloatLayer5.Checked = true;
                UpdateLayerIconText();
                RenderS32Map();
            }
        }

        // 更新 Layer5 編輯操作說明標籤
        private void UpdateLayer5HelpLabel()
        {
            if (lblLayer5Help == null)
            {
                lblLayer5Help = new Label();
                lblLayer5Help.AutoSize = false;
                lblLayer5Help.Size = new Size(200, 80);
                lblLayer5Help.BackColor = Color.FromArgb(220, 30, 30, 50);
                lblLayer5Help.ForeColor = Color.FromArgb(100, 180, 255);
                lblLayer5Help.Font = new Font("Microsoft JhengHei", 9F, FontStyle.Regular);
                lblLayer5Help.Padding = new Padding(8);
                lblLayer5Help.BorderStyle = BorderStyle.FixedSingle;
                s32MapPanel.Controls.Add(lblLayer5Help);
            }

            if (!_editState.IsLayer5EditMode)
            {
                lblLayer5Help.Visible = false;
                return;
            }

            lblLayer5Help.Text = "【透明編輯模式】\n" +
                                 "• 左鍵：添加透明設定\n" +
                                 "• 右鍵：刪除透明設定\n" +
                                 "• 再按按鈕：取消模式";
            lblLayer5Help.Location = new Point(s32MapPanel.Width - lblLayer5Help.Width - 20, 200);
            lblLayer5Help.Visible = true;
            lblLayer5Help.BringToFront();
        }

        // 更新通行性編輯操作說明標籤
        private void UpdatePassabilityHelpLabel()
        {
            if (currentPassableEditMode == PassableEditMode.None)
            {
                lblPassabilityHelp.Visible = false;
                return;
            }

            string modeText = currentPassableEditMode == PassableEditMode.SetPassable ? "允許通行" : "禁止通行";
            Color borderColor = currentPassableEditMode == PassableEditMode.SetPassable ? Color.LimeGreen : Color.Red;

            lblPassabilityHelp.Text = $"【{modeText}模式】\n" +
                                      "• 點擊格子：設定整格通行性\n" +
                                      "• Ctrl+左鍵：新增多邊形頂點\n" +
                                      "• 右鍵：完成多邊形 (≥3點)\n" +
                                      "• 再按按鈕：取消模式";
            lblPassabilityHelp.ForeColor = borderColor;
            lblPassabilityHelp.Visible = true;
            lblPassabilityHelp.BringToFront();
        }

        // 重新載入按鈕點擊事件
        private void btnReloadMap_Click(object sender, EventArgs e)
        {
            ReloadCurrentMap();
        }

        // 渲染 S32 地圖（Viewport 渲染 - 只渲染可見區域）
        private void RenderS32Map()
        {
            try
            {
                if (_document.S32Files.Count == 0 || string.IsNullOrEmpty(_document.MapId))
                {
                    lblS32Info.Text = "請選擇一個地圖";
                    return;
                }

                if (!Share.MapDataList.ContainsKey(_document.MapId))
                {
                    lblS32Info.Text = "地圖資料不存在";
                    return;
                }

                // 清除小地圖快取（viewport 移動時需要更新紅框）
                // 注意：S32 Block Cache 不在這裡清除，只在地圖切換或編輯時清除


                Struct.L1Map currentMap = Share.MapDataList[_document.MapId];

                // 計算整張地圖的大小（使用與 L1MapHelper 相同的公式）
                // 每個 block 的像素大小: BMP_W = 64 * 24 * 2 = 3072, BMP_H = 64 * 12 * 2 = 1536
                int blockWidth = 64 * 24 * 2;  // 3072
                int blockHeight = 64 * 12 * 2; // 1536

                // 地圖像素大小（與 L1MapHelper.LoadMap 相同）
                int mapWidth = currentMap.nBlockCountX * blockWidth;
                int mapHeight = currentMap.nBlockCountX * blockHeight / 2 + currentMap.nBlockCountY * blockHeight / 2;

                // 更新 ViewState 的地圖大小
                _viewState.MapWidth = mapWidth;
                _viewState.MapHeight = mapHeight;
                _viewState.ViewportWidth = s32MapPanel.Width;
                _viewState.ViewportHeight = s32MapPanel.Height;
                _viewState.ZoomLevel = s32ZoomLevel;

                // 更新捲動限制（保留現有的捲動位置，ViewState.ScrollX/ScrollY 會在限制範圍內調整）
                _viewState.UpdateScrollLimits(mapWidth, mapHeight);

                // 取得需要渲染的世界座標範圍（含緩衝區）
                Rectangle renderRect = _viewState.GetRenderWorldRect();
                LogPerf($"[RENDER-MAP] ScrollX={_viewState.ScrollX}, ScrollY={_viewState.ScrollY}, renderRect=({renderRect.X},{renderRect.Y},{renderRect.Width},{renderRect.Height})");

                // 渲染 Viewport（使用快取的勾選檔案清單）
                RenderViewport(renderRect, currentMap, _checkedS32Files);

                // 統計勾選的 S32 數量和物件數量
                int checkedCount = _checkedS32Files.Count;
                int totalObjects = _document.S32Files.Values
                    .Where(s => _checkedS32Files.Contains(s.FilePath))
                    .Sum(s => s.Layer4.Count);

                lblS32Info.Text = $"已渲染 {checkedCount}/{_document.S32Files.Count} 個S32檔案 | 地圖: {mapWidth}x{mapHeight} | Viewport: {renderRect.Width}x{renderRect.Height} | 第1層:{(chkLayer1.Checked ? "顯示" : "隱藏")} 第3層:{(chkLayer3.Checked ? "顯示" : "隱藏")} 第4層:{(chkLayer4.Checked ? "顯示" : "隱藏")} ({totalObjects}個物件)";
            }
            catch (Exception ex)
            {
                lblS32Info.Text = $"渲染失敗: {ex.Message}";
            }
        }

        // Viewport 渲染取消 token
        private System.Threading.CancellationTokenSource _viewportRenderCts = null;
        private readonly object _viewportRenderLock = new object();
        private volatile bool _isRendering = false;
        private volatile int _renderRequestId = 0;

        // 渲染指定範圍的 Viewport（非同步背景渲染）
        private void RenderViewport(Rectangle worldRect, Struct.L1Map currentMap, HashSet<string> checkedFilePaths)
        {
            // 取消之前的渲染任務
            int currentRequestId;
            lock (_viewportRenderLock)
            {
                if (_viewportRenderCts != null)
                {
                    _viewportRenderCts.Cancel();
                    _viewportRenderCts.Dispose();
                }
                _viewportRenderCts = new System.Threading.CancellationTokenSource();
                currentRequestId = ++_renderRequestId;
            }
            var cancellationToken = _viewportRenderCts.Token;

            // 預先讀取 UI 狀態（在 UI Thread）
            bool showLayer1 = chkLayer1.Checked;
            bool showLayer2 = chkLayer2.Checked;
            bool showLayer4 = chkLayer4.Checked;
            bool showLayer3 = chkLayer3.Checked;
            bool showPassable = chkShowPassable.Checked;
            bool showGrid = chkShowGrid.Checked;
            bool showS32Boundary = chkShowS32Boundary.Checked;
            bool showLayer5 = chkShowLayer5.Checked;
            bool isLayer5Edit = _editState.IsLayer5EditMode;
            bool hasHighlight = _editState.HighlightedS32Data != null && _editState.HighlightedCellX >= 0 && _editState.HighlightedCellY >= 0;
            int panelWidth = s32MapPanel.Width;
            int panelHeight = s32MapPanel.Height;

            // 複製需要的資料（避免跨執行緒存取）
            var s32FilesSnapshot = _document.S32Files.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // 先設定 PictureBox（讓 UI 立即反應）
            s32PictureBox.Size = new Size(panelWidth, panelHeight);
            s32PictureBox.Location = new Point(0, 0);
            s32MapPanel.AutoScroll = false;

            // 不使用增量渲染，每次完整重新渲染（避免多執行緒 bitmap 存取問題）

            // 背景執行渲染
            Task.Run(() =>
            {
                // 檢查是否已經被更新的請求取代
                if (cancellationToken.IsCancellationRequested || currentRequestId != _renderRequestId)
                    return;

                // 標記正在渲染
                _isRendering = true;
                var renderSw = Stopwatch.StartNew();
                int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                LogPerf($"[RENDER-START] ThreadId={threadId}, requestId={currentRequestId}, worldRect=({worldRect.X},{worldRect.Y},{worldRect.Width},{worldRect.Height})");

                int blockWidth = 64 * 24 * 2;  // 3072
                int blockHeight = 64 * 12 * 2; // 1536

                // 創建新的 Viewport Bitmap
                var createBmpSw = Stopwatch.StartNew();
                Bitmap viewportBitmap = new Bitmap(worldRect.Width, worldRect.Height, PixelFormat.Format16bppRgb555);
                createBmpSw.Stop();
                long createBmpMs = createBmpSw.ElapsedMilliseconds;
                HashSet<string> newRenderedBlocks = new HashSet<string>();

                ImageAttributes vAttr = new ImageAttributes();
                vAttr.SetColorKey(Color.FromArgb(0), Color.FromArgb(0)); // 透明色

                int renderedCount = 0;
                int skippedCount = 0;
                long totalGetBlockMs = 0;
                long totalDrawImageMs = 0;

                using (Graphics g = Graphics.FromImage(viewportBitmap))
                {
                    // 使用空間索引快速查找與 worldRect 相交的 S32 檔案
                    var candidateFiles = GetS32FilesInRect(worldRect);
                    LogPerf($"[SPATIAL-QUERY] worldRect=({worldRect.X},{worldRect.Y},{worldRect.Width},{worldRect.Height}), candidates={candidateFiles.Count}, total={s32FilesSnapshot.Count}");

                    // 使用與原始 L1MapHelper.LoadMap 完全相同的排序方式（Utils.SortDesc）
                    var sortedFilePaths = Utils.SortDesc(candidateFiles.ToList());

                    // 遍歷候選 S32 檔案（已經過空間索引過濾）
                    foreach (object filePathObj in sortedFilePaths)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            viewportBitmap.Dispose();
                            return;
                        }

                        string filePath = filePathObj as string;
                        if (filePath == null || !s32FilesSnapshot.ContainsKey(filePath)) continue;

                        // 只渲染有勾選的 S32
                        if (!checkedFilePaths.Contains(filePath)) continue;

                        var s32Data = s32FilesSnapshot[filePath];
                        // 使用 GetLoc 計算區塊位置（世界座標）
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int mx = loc[0];
                        int my = loc[1];

                        // 精確檢查是否與渲染範圍相交（空間索引可能有誤差）
                        Rectangle blockRect = new Rectangle(mx, my, blockWidth, blockHeight);
                        if (!blockRect.IntersectsWith(worldRect))
                        {
                            skippedCount++;
                            continue;
                        }

                        newRenderedBlocks.Add(filePath);
                        renderedCount++;

                        // 為這個 S32 生成獨立的 bitmap（使用快取）
                        var getBlockSw = Stopwatch.StartNew();
                        Bitmap blockBmp = GetOrRenderS32Block(s32Data, showLayer1, showLayer2, showLayer4);
                        getBlockSw.Stop();
                        totalGetBlockMs += getBlockSw.ElapsedMilliseconds;

                        // 計算繪製位置（減去 worldRect 原點偏移）
                        int drawX = mx - worldRect.X;
                        int drawY = my - worldRect.Y;

                        // 合併到 Viewport Bitmap（使用 unsafe 直接複製，比 DrawImage 快）
                        var drawSw = Stopwatch.StartNew();
                        CopyBitmapDirect(blockBmp, viewportBitmap, drawX, drawY);
                        drawSw.Stop();
                        totalDrawImageMs += drawSw.ElapsedMilliseconds;

                        // 注意：如果是快取的 bitmap 不要 Dispose
                        // 快取會在 ClearS32BlockCache 時統一釋放
                    }
                }
                renderSw.Stop();
                LogPerf($"[RENDER] total={renderSw.ElapsedMilliseconds}ms, createBmp={createBmpMs}ms, getBlock={totalGetBlockMs}ms, drawImage={totalDrawImageMs}ms, rendered={renderedCount}, skipped={skippedCount}, cacheHit={_cacheHits}, cacheMiss={_cacheMisses}");
                _cacheHits = 0;
                _cacheMisses = 0;

                // 更新已渲染的 S32 清單
                lock (_renderedS32Blocks)
                {
                    _renderedS32Blocks.Clear();
                    foreach (var path in newRenderedBlocks)
                        _renderedS32Blocks.Add(path);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    viewportBitmap.Dispose();
                    return;
                }

                // 繪製覆蓋層（需要傳入世界座標偏移）
                if (showLayer3)
                {
                    DrawLayer3AttributesViewport(viewportBitmap, currentMap, worldRect);
                }

                if (showPassable)
                {
                    DrawPassableOverlayViewport(viewportBitmap, currentMap, worldRect);
                }

                if (showGrid)
                {
                    DrawS32GridViewport(viewportBitmap, currentMap, worldRect);
                    DrawCoordinateLabelsViewport(viewportBitmap, currentMap, worldRect);
                }

                if (showS32Boundary)
                {
                    DrawS32BoundaryOnlyViewport(viewportBitmap, currentMap, worldRect);
                }

                if (showLayer5)
                {
                    DrawLayer5OverlayViewport(viewportBitmap, currentMap, worldRect, isLayer5Edit);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    viewportBitmap.Dispose();
                    return;
                }

                // 回到 UI Thread 更新
                LogPerf($"[RENDER-INVOKE] queuing BeginInvoke from thread {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                try
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        var invokeSw = Stopwatch.StartNew();
                        LogPerf($"[RENDER-INVOKE] callback start");
                        _isRendering = false;  // 渲染完成

                        if (cancellationToken.IsCancellationRequested)
                        {
                            viewportBitmap.Dispose();
                            LogPerf($"[RENDER-INVOKE] cancelled, total={invokeSw.ElapsedMilliseconds}ms");
                            return;
                        }

                        // 如果有高亮，在 UI Thread 繪製（因為需要存取 _editState）
                        if (hasHighlight && _editState.HighlightedS32Data != null)
                        {
                            DrawHighlightedCellViewport(viewportBitmap, currentMap, worldRect);
                        }

                        // 保存渲染結果元數據
                        _viewState.SetRenderResult(worldRect.X, worldRect.Y, worldRect.Width, worldRect.Height, _viewState.ZoomLevel);

                        // 釋放舊的 Viewport Bitmap（加鎖保護）
                        lock (_viewportBitmapLock)
                        {
                            if (_viewportBitmap != null)
                                _viewportBitmap.Dispose();
                            _viewportBitmap = viewportBitmap;
                        }

                        invokeSw.Stop();
                        LogPerf($"[RENDER-COMPLETE] bitmap assigned, size={viewportBitmap.Width}x{viewportBitmap.Height}, renderOrigin=({worldRect.X},{worldRect.Y}), invokeTime={invokeSw.ElapsedMilliseconds}ms");

                        // 強制重繪
                        s32PictureBox.Invalidate();
                    });
                }
                catch
                {
                    _isRendering = false;  // 發生錯誤也要重置
                    viewportBitmap.Dispose();
                }
            });
        }

        // 檢查是否需要重新渲染並執行
        private void CheckAndRerenderIfNeeded()
        {
            LogPerf($"[CHECK-RERENDER] start, s32Count={_document.S32Files.Count}, isDragging={isMainMapDragging}");

            if (_document.S32Files.Count == 0 || string.IsNullOrEmpty(_document.MapId))
                return;

            // 拖曳中不重新渲染，只更新顯示
            if (isMainMapDragging)
            {
                s32PictureBox.Invalidate();
                return;
            }

            // 更新縮放和 Viewport 大小到 ViewState
            _viewState.ZoomLevel = s32ZoomLevel;
            _viewState.ViewportWidth = s32MapPanel.Width;
            _viewState.ViewportHeight = s32MapPanel.Height;
            // ScrollX/ScrollY 已經在拖曳時更新了，這裡不需要再設定

            // 檢查是否需要重新渲染
            bool needsRerender = _viewState.NeedsRerender();
            LogPerf($"[CHECK-RERENDER] needsRerender={needsRerender}, ScrollX={_viewState.ScrollX}, ScrollY={_viewState.ScrollY}, RenderOrigin=({_viewState.RenderOriginX},{_viewState.RenderOriginY}), RenderSize={_viewState.RenderWidth}x{_viewState.RenderHeight}");

            if (needsRerender)
            {
                RenderS32Map();
            }
            else
            {
                // 只需要重繪（不需要重新渲染）
                s32PictureBox.Invalidate();
            }
        }

        /// <summary>
        /// 取得快取的 S32 Block 或渲染新的（用於 Viewport 渲染）
        /// </summary>
        private int _cacheHits = 0;
        private int _cacheMisses = 0;

        private Bitmap GetOrRenderS32Block(S32Data s32Data, bool showLayer1, bool showLayer2, bool showLayer4)
        {
            // 只有 Layer1+Layer2+Layer4 都開啟時才使用快取
            if (showLayer1 && showLayer2 && showLayer4)
            {
                string cacheKey = s32Data.FilePath;
                if (_s32BlockCache.TryGetValue(cacheKey, out Bitmap cached))
                {
                    _cacheHits++;
                    return cached;
                }

                _cacheMisses++;
                // 渲染並快取
                Bitmap rendered = RenderS32Block(s32Data, showLayer1, showLayer2, showLayer4);
                _s32BlockCache.TryAdd(cacheKey, rendered);
                return rendered;
            }

            // 其他情況直接渲染（不快取）
            return RenderS32Block(s32Data, showLayer1, showLayer2, showLayer4);
        }

        /// <summary>
        /// 清除 S32 Block 快取（地圖變更或編輯時呼叫）
        /// </summary>
        private void ClearS32BlockCache()
        {
            foreach (var bmp in _s32BlockCache.Values)
            {
                bmp?.Dispose();
            }
            _s32BlockCache.Clear();
            lock (_renderedS32Blocks)
            {
                _renderedS32Blocks.Clear();
            }
            // 注意：不重置 _viewState.RenderResult，讓舊的 viewport bitmap 繼續顯示
            // 直到新的渲染完成後由 RenderViewport 更新
        }

        /// <summary>
        /// 清除單個 S32 Block 的快取（編輯後呼叫）
        /// </summary>
        private void InvalidateS32BlockCache(string filePath)
        {
            if (_s32BlockCache.TryRemove(filePath, out Bitmap bmp))
            {
                bmp?.Dispose();
            }
            lock (_renderedS32Blocks)
            {
                _renderedS32Blocks.Remove(filePath);
            }
        }

        // 渲染單個 S32 區塊為 bitmap（與 L1MapHelper.s32FileToBmp 相同的方式）
        private Bitmap RenderS32Block(S32Data s32Data, bool showLayer1, bool showLayer2, bool showLayer4)
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

                // 第二層 - 與第一層渲染方式相同
                if (showLayer2)
                {
                    foreach (var item in s32Data.Layer2)
                    {
                        if (item.TileId > 0)
                        {
                            int x = item.X;
                            int y = item.Y;

                            int baseX = 0;
                            int baseY = 63 * 12;
                            baseX -= 24 * (x / 2);
                            baseY -= 12 * (x / 2);

                            int pixelX = baseX + x * 24 + y * 24;
                            int pixelY = baseY + y * 12;

                            DrawTilToBufferDirect(pixelX, pixelY, item.TileId, item.IndexId, rowpix, ptr, blockWidth, blockHeight);
                        }
                    }
                }

                // 第四層（物件）
                if (showLayer4)
                {
                    var sortedObjects = s32Data.Layer4.OrderBy(o => o.Layer).ToList();

                    // 如果有篩選條件，只渲染選中的群組
                    if (_editState.IsFilteringLayer4Groups && _editState.SelectedLayer4Groups.Count > 0)
                    {
                        sortedObjects = sortedObjects.Where(o => _editState.SelectedLayer4Groups.Contains(o.GroupId)).ToList();
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

        // 設定初始捲動位置到地圖中央（不渲染，用於載入時先設定位置）
        private void SetInitialScrollToCenter(Struct.L1Map currentMap)
        {
            // 計算地圖像素大小
            int blockWidth = 64 * 24 * 2;  // 3072
            int blockHeight = 64 * 12 * 2; // 1536
            int mapWidth = currentMap.nBlockCountX * blockWidth;
            int mapHeight = currentMap.nBlockCountX * blockHeight / 2 + currentMap.nBlockCountY * blockHeight / 2;

            // 更新 ViewState（RenderS32Map 會用到）
            _viewState.MapWidth = mapWidth;
            _viewState.MapHeight = mapHeight;
            _viewState.ViewportWidth = s32MapPanel.Width;
            _viewState.ViewportHeight = s32MapPanel.Height;
            _viewState.ZoomLevel = s32ZoomLevel;

            // 計算實際 S32 block 的範圍（而不是假設從 0,0 開始）
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            foreach (var s32Data in _document.S32Files.Values)
            {
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int bx = loc[0];
                int by = loc[1];
                minX = Math.Min(minX, bx);
                minY = Math.Min(minY, by);
                maxX = Math.Max(maxX, bx + blockWidth);
                maxY = Math.Max(maxY, by + blockHeight);
            }

            // 如果沒有 S32 檔案，使用地圖中央
            if (minX == int.MaxValue)
            {
                minX = 0; minY = 0;
                maxX = mapWidth; maxY = mapHeight;
            }

            // 計算 S32 實際範圍的中央位置
            int actualCenterX = (minX + maxX) / 2;
            int actualCenterY = (minY + maxY) / 2;

            // 計算中央位置（世界座標）- viewport 中央對準 S32 範圍中央
            int viewportWidthWorld = (int)(s32MapPanel.Width / s32ZoomLevel);
            int viewportHeightWorld = (int)(s32MapPanel.Height / s32ZoomLevel);
            int centerX = actualCenterX - viewportWidthWorld / 2;
            int centerY = actualCenterY - viewportHeightWorld / 2;

            // 限制在有效範圍內
            int maxScrollX = Math.Max(0, mapWidth - viewportWidthWorld);
            int maxScrollY = Math.Max(0, mapHeight - viewportHeightWorld);
            centerX = Math.Max(0, Math.Min(centerX, maxScrollX));
            centerY = Math.Max(0, Math.Min(centerY, maxScrollY));

            LogPerf($"[SCROLL-CENTER] S32 range=({minX},{minY})-({maxX},{maxY}), center=({actualCenterX},{actualCenterY}), scroll=({centerX},{centerY}), mapSize=({mapWidth},{mapHeight}), zoom={s32ZoomLevel}");

            // 只設定捲動位置，不觸發渲染
            _viewState.SetScrollSilent(centerX, centerY);
            _viewState.UpdateScrollLimits(mapWidth, mapHeight);
            LogPerf($"[SCROLL-AFTER] ScrollX={_viewState.ScrollX}, ScrollY={_viewState.ScrollY}, MaxScrollX={_viewState.MaxScrollX}, MaxScrollY={_viewState.MaxScrollY}");
        }

        // 捲動到地圖中央（含重新渲染）
        private void ScrollToMapCenter()
        {
            int mapWidth = _viewState.MapWidth;
            int mapHeight = _viewState.MapHeight;

            if (mapWidth <= 0 || mapHeight <= 0)
                return;

            // 計算中央位置（世界座標）
            int viewportWidthWorld = (int)(s32MapPanel.Width / s32ZoomLevel);
            int viewportHeightWorld = (int)(s32MapPanel.Height / s32ZoomLevel);
            int centerX = mapWidth / 2 - viewportWidthWorld / 2;
            int centerY = mapHeight / 2 - viewportHeightWorld / 2;

            // 限制在有效範圍內
            int maxScrollX = Math.Max(0, mapWidth - viewportWidthWorld);
            int maxScrollY = Math.Max(0, mapHeight - viewportHeightWorld);
            centerX = Math.Max(0, Math.Min(centerX, maxScrollX));
            centerY = Math.Max(0, Math.Min(centerY, maxScrollY));

            // 設定 ViewState 的捲動位置
            _viewState.SetScrollSilent(centerX, centerY);

            // 重新渲染並更新小地圖
            CheckAndRerenderIfNeeded();
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
                foreach (var s32Data in _document.S32Files.Values)
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
                    foreach (var s32Data in _document.S32Files.Values)
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
            if (_editState.HighlightedS32Data == null) return;

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // 使用 GetLoc 計算區塊位置
                int[] loc = _editState.HighlightedS32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 使用 GetLoc + drawTilBlock 公式計算像素位置
                int localBaseX = 0;
                int localBaseY = 63 * 12;
                localBaseX -= 24 * (_editState.HighlightedCellX / 2);
                localBaseY -= 12 * (_editState.HighlightedCellX / 2);

                int X = mx + localBaseX + _editState.HighlightedCellX * 24 + _editState.HighlightedCellY * 24;
                int Y = my + localBaseY + _editState.HighlightedCellY * 12;

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

                foreach (var s32Data in _document.S32Files.Values)
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
                    foreach (var s32Data in _document.S32Files.Values)
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
                foreach (var s32Data in _document.S32Files.Values)
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

        #region Viewport 版本的繪圖方法

        // 繪製第三層屬性（Viewport 版本）
        private void DrawLayer3AttributesViewport(Bitmap bitmap, Struct.L1Map currentMap, Rectangle worldRect)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                foreach (var s32Data in _document.S32Files.Values)
                {
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    int mx = loc[0];
                    int my = loc[1];

                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 64; x++)
                        {
                            var attr = s32Data.Layer3[y, x];
                            if (attr == null) continue;
                            if (attr.Attribute1 == 0 && attr.Attribute2 == 0) continue;

                            int x1 = x * 2;
                            int localBaseX = 0 - 24 * (x1 / 2);
                            int localBaseY = 63 * 12 - 12 * (x1 / 2);

                            int X = mx + localBaseX + x1 * 24 + y * 24 - worldRect.X;
                            int Y = my + localBaseY + y * 12 - worldRect.Y;

                            // 跳過不在 Viewport 內的格子
                            if (X + 48 < 0 || X > worldRect.Width || Y + 24 < 0 || Y > worldRect.Height)
                                continue;

                            Point pLeft = new Point(X + 0, Y + 12);
                            Point pTop = new Point(X + 24, Y + 0);
                            Point pRight = new Point(X + 48, Y + 12);

                            if (attr.Attribute1 != 0)
                            {
                                Color color = GetAttributeColor(attr.Attribute1);
                                using (Pen pen = new Pen(color, 3))
                                {
                                    g.DrawLine(pen, pLeft, pTop);
                                }
                            }

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

        // 繪製通行性覆蓋層（Viewport 版本）
        private void DrawPassableOverlayViewport(Bitmap bitmap, Struct.L1Map currentMap, Rectangle worldRect)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                using (Pen penImpassable = new Pen(Color.FromArgb(255, 128, 0, 128), 3))
                using (Pen penPassable = new Pen(Color.FromArgb(255, 50, 200, 255), 2))
                {
                    foreach (var s32Data in _document.S32Files.Values)
                    {
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int mx = loc[0];
                        int my = loc[1];

                        for (int y = 0; y < 64; y++)
                        {
                            for (int x = 0; x < 64; x++)
                            {
                                var attr = s32Data.Layer3[y, x];
                                if (attr == null) continue;

                                int x1 = x * 2;
                                int localBaseX = 0 - 24 * (x1 / 2);
                                int localBaseY = 63 * 12 - 12 * (x1 / 2);

                                int X = mx + localBaseX + x1 * 24 + y * 24 - worldRect.X;
                                int Y = my + localBaseY + y * 12 - worldRect.Y;

                                // 跳過不在 Viewport 內的格子
                                if (X + 48 < 0 || X > worldRect.Width || Y + 24 < 0 || Y > worldRect.Height)
                                    continue;

                                Point pLeft = new Point(X + 0, Y + 12);
                                Point pTop = new Point(X + 24, Y + 0);
                                Point pRight = new Point(X + 48, Y + 12);

                                Pen penLeft = (attr.Attribute1 & 0x01) != 0 ? penImpassable : penPassable;
                                g.DrawLine(penLeft, pLeft, pTop);

                                Pen penRight = (attr.Attribute2 & 0x01) != 0 ? penImpassable : penPassable;
                                g.DrawLine(penRight, pTop, pRight);
                            }
                        }
                    }
                }
            }
        }

        // 繪製 Layer5 覆蓋層（透明圖塊標記）
        private void DrawLayer5OverlayViewport(Bitmap bitmap, Struct.L1Map currentMap, Rectangle worldRect, bool isLayer5Edit)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // 收集所有 Layer5 位置（去重）
                var drawnPositions = new HashSet<(int mx, int my, int x, int y)>();

                // 半透明藍色填充和邊框
                using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(80, 60, 140, 255)))
                using (Pen borderPen = new Pen(Color.FromArgb(180, 80, 160, 255), 1.5f))
                using (Pen highlightPen = new Pen(Color.FromArgb(200, 150, 200, 255), 1f))
                {
                    foreach (var s32Data in _document.S32Files.Values)
                    {
                        if (s32Data.Layer5.Count == 0) continue;

                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int mx = loc[0];
                        int my = loc[1];

                        foreach (var item in s32Data.Layer5)
                        {
                            // 同位置只畫一次
                            var posKey = (mx, my, (int)item.X, (int)item.Y);
                            if (drawnPositions.Contains(posKey)) continue;
                            drawnPositions.Add(posKey);
                            // Layer5 的 X 是 0-127（Layer1 座標系），Y 是 0-63
                            // 繪製半格大小的三角形（X 切半）
                            int x1 = item.X;  // 0-127
                            int y = item.Y;   // 0-63

                            int localBaseX = 0 - 24 * (x1 / 2);
                            int localBaseY = 63 * 12 - 12 * (x1 / 2);

                            int X = mx + localBaseX + x1 * 24 + y * 24 - worldRect.X;
                            int Y = my + localBaseY + y * 12 - worldRect.Y;

                            // 跳過不在 Viewport 內的格子
                            if (X + 24 < 0 || X > worldRect.Width || Y + 12 < 0 || Y > worldRect.Height)
                                continue;

                            // 繪製半格三角形（根據 X 奇偶決定左半或右半）
                            Point[] triangle;
                            if (x1 % 2 == 0)
                            {
                                // 偶數 X：左半三角形
                                Point pLeft = new Point(X + 0, Y + 12);
                                Point pTop = new Point(X + 24, Y + 0);
                                Point pBottom = new Point(X + 24, Y + 24);
                                triangle = new Point[] { pLeft, pTop, pBottom };

                                // 填充
                                g.FillPolygon(fillBrush, triangle);
                                // 邊框（上亮下暗）
                                g.DrawLine(highlightPen, pLeft, pTop);
                                g.DrawLine(borderPen, pTop, pBottom);
                                g.DrawLine(borderPen, pBottom, pLeft);
                            }
                            else
                            {
                                // 奇數 X：右半三角形
                                Point pTop = new Point(X + 0, Y + 0);
                                Point pRight = new Point(X + 24, Y + 12);
                                Point pBottom = new Point(X + 0, Y + 24);
                                triangle = new Point[] { pTop, pRight, pBottom };

                                // 填充
                                g.FillPolygon(fillBrush, triangle);
                                // 邊框（上亮下暗）
                                g.DrawLine(highlightPen, pTop, pRight);
                                g.DrawLine(borderPen, pRight, pBottom);
                                g.DrawLine(borderPen, pBottom, pTop);
                            }
                        }
                    }
                }

                // 在透明編輯模式下，繪製已設定 Layer5 的群組物件覆蓋層
                if (isLayer5Edit)
                {
                    DrawLayer5GroupOverlay(g, worldRect);
                }
            }
        }

        // 繪製已設定 Layer5 的群組物件覆蓋層
        private void DrawLayer5GroupOverlay(Graphics g, Rectangle worldRect)
        {
            // 只有在有選取格子時才顯示
            if (_editState.SelectedCells.Count == 0) return;

            // 從選取的格子收集 Layer5 的 GroupId 及其 Type
            var groupLayer5Info = new Dictionary<int, byte>(); // GroupId -> Type
            foreach (var selectedCell in _editState.SelectedCells)
            {
                var s32Data = selectedCell.S32Data;
                int localX = selectedCell.LocalX;  // Layer1 座標 (0-127)
                int localY = selectedCell.LocalY;  // Layer3 座標 (0-63)

                // 查找該格子位置對應的 Layer5 設定
                // Layer5 的 X 是 0-127，Y 是 0-63
                // selectedCell.LocalX 是 Layer1 座標 (0-127)，LocalY 是 (0-63)
                // 一個遊戲格子對應兩個 Layer1 X 座標（localX 和 localX+1）
                foreach (var item in s32Data.Layer5)
                {
                    if ((item.X == localX || item.X == localX + 1) && item.Y == localY)
                    {
                        // 如果同一個 GroupId 有多個設定，保留第一個
                        if (!groupLayer5Info.ContainsKey(item.ObjectIndex))
                        {
                            groupLayer5Info[item.ObjectIndex] = item.Type;
                        }
                    }
                }
            }

            if (groupLayer5Info.Count == 0) return;

            // 半透明覆蓋色：Type=0 紫色（高對比），Type=1 紅色
            using (SolidBrush type0Brush = new SolidBrush(Color.FromArgb(100, 180, 0, 255)))
            using (SolidBrush type1Brush = new SolidBrush(Color.FromArgb(100, 255, 80, 80)))
            {
                foreach (var s32Data in _document.S32Files.Values)
                {
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    int mx = loc[0];
                    int my = loc[1];

                    foreach (var obj in s32Data.Layer4)
                    {
                        // 檢查該群組是否有 Layer5 設定
                        if (!groupLayer5Info.TryGetValue(obj.GroupId, out byte type))
                            continue;

                        // 使用與高亮格子相同的座標計算方式
                        int x1 = obj.X;  // 0-127 (Layer1 座標系)
                        int y = obj.Y;   // 0-63

                        int localBaseX = 0 - 24 * (x1 / 2);
                        int localBaseY = 63 * 12 - 12 * (x1 / 2);

                        int X = mx + localBaseX + x1 * 24 + y * 24 - worldRect.X;
                        int Y = my + localBaseY + y * 12 - worldRect.Y;

                        // 跳過不在 Viewport 內的物件
                        if (X + 24 < 0 || X > worldRect.Width || Y + 24 < 0 || Y > worldRect.Height)
                            continue;

                        // 繪製半格菱形覆蓋（與格子高亮相同大小）
                        SolidBrush brush = type == 0 ? type0Brush : type1Brush;
                        Point[] diamond = new Point[]
                        {
                            new Point(X + 0, Y + 12),   // 左
                            new Point(X + 12, Y + 0),   // 上
                            new Point(X + 24, Y + 12),  // 右
                            new Point(X + 12, Y + 24)   // 下
                        };
                        g.FillPolygon(brush, diamond);
                    }
                }
            }
        }

        // 繪製選中格子的高亮（Viewport 版本）
        private void DrawHighlightedCellViewport(Bitmap bitmap, Struct.L1Map currentMap, Rectangle worldRect)
        {
            if (_editState.HighlightedS32Data == null) return;

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                int[] loc = _editState.HighlightedS32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                int localBaseX = 0 - 24 * (_editState.HighlightedCellX / 2);
                int localBaseY = 63 * 12 - 12 * (_editState.HighlightedCellX / 2);

                int X = mx + localBaseX + _editState.HighlightedCellX * 24 + _editState.HighlightedCellY * 24 - worldRect.X;
                int Y = my + localBaseY + _editState.HighlightedCellY * 12 - worldRect.Y;

                Point p1 = new Point(X + 0, Y + 12);
                Point p2 = new Point(X + 12, Y + 0);
                Point p3 = new Point(X + 24, Y + 12);
                Point p4 = new Point(X + 12, Y + 24);

                using (SolidBrush brush = new SolidBrush(Color.FromArgb(120, 255, 255, 0)))
                {
                    g.FillPolygon(brush, new Point[] { p1, p2, p3, p4 });
                }

                using (Pen pen = new Pen(Color.FromArgb(255, 255, 200, 0), 3))
                {
                    g.DrawPolygon(pen, new Point[] { p1, p2, p3, p4 });
                }
            }
        }

        // 繪製 S32 邊界框（Viewport 版本）
        private void DrawS32BoundaryOnlyViewport(Bitmap bitmap, Struct.L1Map currentMap, Rectangle worldRect)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                Font font = new Font("Arial", 9, FontStyle.Bold);
                Pen boundaryPen = new Pen(Color.Cyan, 2);

                foreach (var s32Data in _document.S32Files.Values)
                {
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    int mx = loc[0];
                    int my = loc[1];

                    Point[] corners = new Point[4];
                    int[][] cornerCoords = new int[][] {
                        new int[] { 0, 0 },
                        new int[] { 64, 0 },
                        new int[] { 64, 64 },
                        new int[] { 0, 64 }
                    };

                    for (int i = 0; i < 4; i++)
                    {
                        int x3 = cornerCoords[i][0];
                        int y = cornerCoords[i][1];
                        int x = x3 * 2;

                        int localBaseX = 0 - 24 * (x / 2);
                        int localBaseY = 63 * 12 - 12 * (x / 2);
                        int X = mx + localBaseX + x * 24 + y * 24 - worldRect.X;
                        int Y = my + localBaseY + y * 12 - worldRect.Y;

                        corners[i] = new Point(X, Y + 12);
                    }

                    g.DrawLine(boundaryPen, corners[0], corners[1]);
                    g.DrawLine(boundaryPen, corners[1], corners[2]);
                    g.DrawLine(boundaryPen, corners[2], corners[3]);
                    g.DrawLine(boundaryPen, corners[3], corners[0]);

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
                }

                font.Dispose();
                boundaryPen.Dispose();
            }
        }

        // 繪製格線（Viewport 版本）
        private void DrawS32GridViewport(Bitmap bitmap, Struct.L1Map currentMap, Rectangle worldRect)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                using (Pen gridPen = new Pen(Color.FromArgb(100, Color.Red), 1))
                {
                    foreach (var s32Data in _document.S32Files.Values)
                    {
                        int[] loc = s32Data.SegInfo.GetLoc(1.0);
                        int mx = loc[0];
                        int my = loc[1];

                        for (int y = 0; y < 64; y++)
                        {
                            for (int x3 = 0; x3 < 64; x3++)
                            {
                                int x = x3 * 2;

                                int localBaseX = 0 - 24 * (x / 2);
                                int localBaseY = 63 * 12 - 12 * (x / 2);

                                int X = mx + localBaseX + x * 24 + y * 24 - worldRect.X;
                                int Y = my + localBaseY + y * 12 - worldRect.Y;

                                // 跳過不在 Viewport 內的格子
                                if (X + 48 < 0 || X > worldRect.Width || Y + 24 < 0 || Y > worldRect.Height)
                                    continue;

                                Point p1 = new Point(X, Y + 12);
                                Point p2 = new Point(X + 24, Y);
                                Point p3 = new Point(X + 48, Y + 12);
                                Point p4 = new Point(X + 24, Y + 24);

                                g.DrawLine(gridPen, p1, p2);
                                g.DrawLine(gridPen, p2, p3);
                                g.DrawLine(gridPen, p3, p4);
                                g.DrawLine(gridPen, p4, p1);
                            }
                        }
                    }
                }
            }
        }

        // 繪製座標標籤（Viewport 版本）
        private void DrawCoordinateLabelsViewport(Bitmap bitmap, Struct.L1Map currentMap, Rectangle worldRect)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                Font font = new Font("Arial", 8, FontStyle.Bold);
                int interval = 10;

                foreach (var s32Data in _document.S32Files.Values)
                {
                    int[] loc = s32Data.SegInfo.GetLoc(1.0);
                    int mx = loc[0];
                    int my = loc[1];

                    for (int y = 0; y < 64; y += interval)
                    {
                        for (int x = 0; x < 128; x += interval)
                        {
                            int localBaseX = 0 - 24 * (x / 2);
                            int localBaseY = 63 * 12 - 12 * (x / 2);

                            int X = mx + localBaseX + x * 24 + y * 24 - worldRect.X;
                            int Y = my + localBaseY + y * 12 - worldRect.Y;

                            // 跳過不在 Viewport 內的格子
                            if (X + 24 < 0 || X > worldRect.Width || Y + 24 < 0 || Y > worldRect.Height)
                                continue;

                            int gameX = s32Data.SegInfo.nLinBeginX + x;
                            int gameY = s32Data.SegInfo.nLinBeginY + y;

                            string coordText = $"{gameX},{gameY}";
                            SizeF textSize = g.MeasureString(coordText, font);

                            int textX = X + 12 - (int)textSize.Width / 2;
                            int textY = Y + 12 - (int)textSize.Height / 2;

                            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(180, Color.White)))
                            {
                                g.FillRectangle(bgBrush, textX - 2, textY - 1, textSize.Width + 4, textSize.Height + 2);
                            }

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

        #endregion

        // 繪製 Tile 到緩衝區（簡化版）
        private unsafe void DrawTilToBuffer(int x, int y, int tileId, int indexId, int rowpix, byte* ptr, int maxWidth, int maxHeight, int mapHeightInCells)
        {
            try
            {
                // 使用快取減少重複讀取（ConcurrentDictionary.GetOrAdd 是執行緒安全的）
                string cacheKey = $"{tileId}_{indexId}";
                byte[] tilData = tileDataCache.GetOrAdd(cacheKey, _ =>
                {
                    string key = $"{tileId}.til";
                    byte[] data = L1PakReader.UnPack("Tile", key);
                    if (data == null) return null;

                    var tilArray = L1Til.Parse(data);
                    if (indexId >= tilArray.Count) return null;

                    return tilArray[indexId];
                });

                if (tilData == null) return;

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
                // 先從 til 檔案快取取得整個 til array
                List<byte[]> tilArray = _tilFileCache.GetOrAdd(tileId, _ =>
                {
                    string key = $"{tileId}.til";
                    byte[] data = L1PakReader.UnPack("Tile", key);
                    if (data == null) return null;
                    return L1Til.Parse(data);
                });

                if (tilArray == null || indexId >= tilArray.Count) return;
                byte[] tilData = tilArray[indexId];
                if (tilData == null) return;

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

        /// <summary>
        /// 背景載入 Tile 列表
        /// </summary>
        private void UpdateTileListAsync()
        {
            Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();

                // 聚合所有 S32 檔案的 UsedTiles（在背景執行緒）
                var aggregatedTiles = new Dictionary<int, TileInfo>();
                foreach (var s32Data in _document.S32Files.Values)
                {
                    foreach (var tileKvp in s32Data.UsedTiles)
                    {
                        int tileId = tileKvp.Key;
                        var tileInfo = tileKvp.Value;

                        if (aggregatedTiles.ContainsKey(tileId))
                        {
                            aggregatedTiles[tileId].UsageCount += tileInfo.UsageCount;
                        }
                        else
                        {
                            aggregatedTiles[tileId] = new TileInfo
                            {
                                TileId = tileInfo.TileId,
                                IndexId = tileInfo.IndexId,
                                UsageCount = tileInfo.UsageCount,
                                Thumbnail = null
                            };
                        }
                    }
                }

                // 預先載入所有縮圖（在背景執行緒）
                foreach (var tile in aggregatedTiles.Values)
                {
                    if (tile.Thumbnail == null)
                    {
                        tile.Thumbnail = LoadTileThumbnail(tile.TileId, tile.IndexId);
                    }
                }

                sw.Stop();
                LogPerf($"[TILELIST] Loaded {aggregatedTiles.Count} tiles in {sw.ElapsedMilliseconds}ms");

                // 回到 UI 執行緒更新列表
                this.BeginInvoke((MethodInvoker)delegate
                {
                    cachedAggregatedTiles = aggregatedTiles;

                    lvTiles.Items.Clear();
                    lvTiles.View = View.LargeIcon;

                    ImageList imageList = new ImageList();
                    imageList.ImageSize = new Size(48, 48);
                    imageList.ColorDepth = ColorDepth.Depth32Bit;
                    lvTiles.LargeImageList = imageList;

                    int index = 0;
                    foreach (var tileKvp in aggregatedTiles.OrderBy(t => t.Key))
                    {
                        var tile = tileKvp.Value;
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

                    lblTileList.Text = $"顯示 {lvTiles.Items.Count} 個 Tile (來自 {_document.S32Files.Count} 個 S32 檔案)";
                });
            });
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

            if (_document.S32Files.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "沒有 S32 檔案";
                return;
            }

            // 如果快取為空，重新聚合所有 S32 檔案的 UsedTiles
            if (cachedAggregatedTiles.Count == 0 || string.IsNullOrEmpty(searchText))
            {
                cachedAggregatedTiles.Clear();

                foreach (var s32Data in _document.S32Files.Values)
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
                ? $"顯示 {lvTiles.Items.Count} 個 Tile (來自 {_document.S32Files.Count} 個 S32 檔案)"
                : $"搜尋結果: {lvTiles.Items.Count}/{totalCount} 個 Tile";
            lblTileList.Text = statusText;
        }

        // Tile 搜尋框文字變更事件
        private void txtTileSearch_TextChanged(object sender, EventArgs e)
        {
            UpdateTileList(txtTileSearch.Text);
        }

        // 載入 Tile 縮圖（使用快取）
        private Bitmap LoadTileThumbnail(int tileId, int indexId)
        {
            try
            {
                // 使用已存在的 til 檔案快取
                List<byte[]> tilArray = _tilFileCache.GetOrAdd(tileId, _ =>
                {
                    string key = $"{tileId}.til";
                    byte[] data = L1PakReader.UnPack("Tile", key);
                    if (data == null) return null;
                    return L1Til.Parse(data);
                });

                if (tilArray == null || indexId >= tilArray.Count)
                    return CreatePlaceholderThumbnail(tileId);

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

        // S32 地圖點擊事件 - 更新高亮和狀態列
        private void s32PictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            // 如果正在選擇區域，不處理點擊
            if (isSelectingRegion)
                return;

            // 獲取當前地圖資訊以計算正確的 baseY
            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
                return;

            Struct.L1Map currentMap = Share.MapDataList[_document.MapId];

            // 將點擊位置轉換為世界座標（考慮縮放和捲動位置）
            Point adjustedLocation = new Point(
                (int)(e.Location.X / s32ZoomLevel) + _viewState.ScrollX,
                (int)(e.Location.Y / s32ZoomLevel) + _viewState.ScrollY
            );

            // 遍歷所有 S32 檔案
            foreach (var s32Data in _document.S32Files.Values)
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
                            _editState.HighlightedS32Data = s32Data;
                            _editState.HighlightedCellX = x;
                            _editState.HighlightedCellY = y;
                            UpdateStatusBarWithLayer3Info(s32Data, x, y);

                            // 通行性編輯模式：單擊設定通行性
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

                                // 透明編輯模式下：顯示附近群組縮圖
                                if (_editState.IsLayer5EditMode)
                                {
                                    UpdateNearbyGroupThumbnails(s32Data, x, y, 10);
                                }
                                else
                                {
                                    // 更新 Layer4 群組清單
                                    UpdateLayer4GroupsList(s32Data, x, y);
                                }
                            }
                            return;
                        }
                    }
                }
            }
        }

        // S32 地圖雙擊事件 - 顯示格子詳細資訊或新增 S32
        private void s32PictureBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // 獲取當前地圖資訊
            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
                return;

            Struct.L1Map currentMap = Share.MapDataList[_document.MapId];

            // 將點擊位置轉換為世界座標（考慮縮放和捲動位置）
            Point adjustedLocation = new Point(
                (int)(e.Location.X / s32ZoomLevel) + _viewState.ScrollX,
                (int)(e.Location.Y / s32ZoomLevel) + _viewState.ScrollY
            );

            // 遍歷所有 S32 檔案
            foreach (var s32Data in _document.S32Files.Values)
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

                        // 檢查雙擊位置是否在這個菱形內
                        if (IsPointInDiamond(adjustedLocation, p1, p2, p3, p4))
                        {
                            // 設置當前選中的 S32 檔案
                            currentS32FileItem = new S32FileItem
                            {
                                FilePath = s32Data.FilePath,
                                SegInfo = s32Data.SegInfo
                            };

                            // 記錄選中的格子
                            _editState.HighlightedS32Data = s32Data;
                            _editState.HighlightedCellX = x;
                            _editState.HighlightedCellY = y;

                            // 顯示格子詳細資料
                            ShowCellLayersDialog(x, y);
                            return;
                        }
                    }
                }
            }

            // 如果沒有雙擊到任何 S32 區塊，檢查是否要新增 S32
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

            if (_document.S32Files.Count == 0)
            {
                // 如果沒有任何 S32，使用地圖的 Min Block 作為基準
                int defaultBlockX = currentMap.nMinBlockX != 0xFFFF ? currentMap.nMinBlockX : 0x7FFF;
                int defaultBlockY = currentMap.nMinBlockY != 0xFFFF ? currentMap.nMinBlockY : 0x8000;
                CreateNewS32AtBlock(defaultBlockX, defaultBlockY, currentMap);
                return;
            }

            // 取得第一個 S32 作為參考點
            var refS32 = _document.S32Files.Values.First();
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

            if (File.Exists(filePath) || _document.S32Files.Keys.Any(k => k.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)))
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

            if (_document.S32Files.Keys.Any(k => k.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)))
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
            _document.S32Files[filePath] = newS32Data;

            // 寫入檔案
            try
            {
                SaveS32File(filePath);  // 使用正確格式的保存方法
            }
            catch (Exception ex)
            {
                // 移除失敗的 S32
                _document.S32Files.Remove(filePath);
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

        // 多邊形通行性設定：找出多邊形內的格子邊界，設定對應的通行屬性
        private void SetPolygonPassable(List<Point> polygonPoints, bool passable)
        {
            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
                return;

            if (polygonPoints.Count < 3)
            {
                this.toolStripStatusLabel1.Text = "多邊形至少需要 3 個頂點";
                return;
            }

            // 將螢幕座標轉換為世界座標（考慮縮放和捲動偏移）
            var scaledPolygon = polygonPoints.Select(p => new PointF(
                (float)(p.X / s32ZoomLevel) + _viewState.ScrollX,
                (float)(p.Y / s32ZoomLevel) + _viewState.ScrollY
            )).ToArray();

            // 收集多邊形內的邊界資訊 (S32Data, layer3X, layer3Y, isAttribute1)
            var includedEdges = new List<(S32Data s32, int layer3X, int layer3Y, bool isAttribute1)>();

            // 遍歷所有 S32 的所有格子，檢查邊界中點是否在多邊形內
            foreach (var s32Data in _document.S32Files.Values)
            {
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // Layer3 是 64x64
                for (int layer3Y = 0; layer3Y < 64; layer3Y++)
                {
                    for (int layer3X = 0; layer3X < 64; layer3X++)
                    {
                        // Layer3 座標轉 Layer1 座標（取偶數 x）
                        int x1 = layer3X * 2;

                        // 與 DrawPassabilityOverlay 完全相同的座標計算
                        int localBaseX = 0;
                        int localBaseY = 63 * 12;
                        localBaseX -= 24 * (x1 / 2);
                        localBaseY -= 12 * (x1 / 2);

                        int X = mx + localBaseX + x1 * 24 + layer3Y * 24;
                        int Y = my + localBaseY + layer3Y * 12;

                        // Layer3 菱形的四個頂點（48x24，與 DrawPassabilityOverlay 一致）
                        float pLeftX = X + 0, pLeftY = Y + 12;       // 左
                        float pTopX = X + 24, pTopY = Y + 0;         // 上
                        float pRightX = X + 48, pRightY = Y + 12;    // 右

                        // 計算左上邊的中點
                        float leftTopMidX = (pLeftX + pTopX) / 2;
                        float leftTopMidY = (pLeftY + pTopY) / 2;

                        // 計算右上邊的中點
                        float rightTopMidX = (pTopX + pRightX) / 2;
                        float rightTopMidY = (pTopY + pRightY) / 2;

                        // 檢查左上邊中點是否在多邊形內
                        if (IsPointInPolygon(leftTopMidX, leftTopMidY, scaledPolygon))
                        {
                            includedEdges.Add((s32Data, layer3X, layer3Y, true));  // Attribute1
                        }

                        // 檢查右上邊中點是否在多邊形內
                        if (IsPointInPolygon(rightTopMidX, rightTopMidY, scaledPolygon))
                        {
                            includedEdges.Add((s32Data, layer3X, layer3Y, false)); // Attribute2
                        }
                    }
                }
            }

            // 去除重複
            var uniqueEdges = includedEdges.Distinct().ToList();

            if (uniqueEdges.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "多邊形內沒有任何格子邊界";
                return;
            }

            // 設定通行性
            int modifiedCount = 0;
            HashSet<S32Data> modifiedS32Files = new HashSet<S32Data>();

            foreach (var (s32Data, layer3X, layer3Y, isAttribute1) in uniqueEdges)
            {
                if (s32Data.Layer3[layer3Y, layer3X] == null)
                {
                    s32Data.Layer3[layer3Y, layer3X] = new MapAttribute { Attribute1 = 0, Attribute2 = 0 };
                }

                if (isAttribute1)
                {
                    if (passable)
                        s32Data.Layer3[layer3Y, layer3X].Attribute1 = (short)(s32Data.Layer3[layer3Y, layer3X].Attribute1 & ~0x01);
                    else
                        s32Data.Layer3[layer3Y, layer3X].Attribute1 = (short)(s32Data.Layer3[layer3Y, layer3X].Attribute1 | 0x01);
                }
                else
                {
                    if (passable)
                        s32Data.Layer3[layer3Y, layer3X].Attribute2 = (short)(s32Data.Layer3[layer3Y, layer3X].Attribute2 & ~0x01);
                    else
                        s32Data.Layer3[layer3Y, layer3X].Attribute2 = (short)(s32Data.Layer3[layer3Y, layer3X].Attribute2 | 0x01);
                }

                modifiedCount++;
                modifiedS32Files.Add(s32Data);
            }

            // 標記所有修改過的 S32 檔案
            foreach (var s32Data in modifiedS32Files)
            {
                s32Data.IsModified = true;
            }

            RenderS32Map();
            this.toolStripStatusLabel1.Text = $"已設定 {modifiedCount} 個邊界為{(passable ? "可通行" : "不可通行")} (影響 {modifiedS32Files.Count} 個 S32 檔案)";
        }

        // 檢查點是否在多邊形內（射線法）
        private bool IsPointInPolygon(float px, float py, PointF[] polygon)
        {
            bool inside = false;
            int j = polygon.Length - 1;

            for (int i = 0; i < polygon.Length; i++)
            {
                if ((polygon[i].Y < py && polygon[j].Y >= py || polygon[j].Y < py && polygon[i].Y >= py)
                    && (polygon[i].X <= px || polygon[j].X <= px))
                {
                    if (polygon[i].X + (py - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) * (polygon[j].X - polygon[i].X) < px)
                    {
                        inside = !inside;
                    }
                }
                j = i;
            }

            return inside;
        }

        // S32 地圖鼠標按下事件 - 開始區域選擇或拖拽移動
        private void s32PictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            // 中鍵拖拽移動視圖
            if (e.Button == MouseButtons.Middle)
            {
                isMainMapDragging = true;
                mainMapDragStartPoint = e.Location;
                // 使用 ViewState 的捲動位置
                mainMapDragStartScroll = new Point(_viewState.ScrollX, _viewState.ScrollY);
                this.s32PictureBox.Cursor = Cursors.SizeAll;
                return;
            }

            if (currentS32Data == null || currentS32FileItem == null)
                return;

            // Ctrl + 左鍵 + 通行性編輯模式：繪製多邊形頂點
            if (e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.Control && currentPassableEditMode != PassableEditMode.None)
            {
                _editState.IsDrawingPassabilityPolygon = true;
                _editState.PassabilityPolygonPoints.Add(e.Location);
                s32PictureBox.Invalidate();
                this.toolStripStatusLabel1.Text = $"多邊形頂點: {_editState.PassabilityPolygonPoints.Count} 個 (Ctrl+左鍵繼續新增，右鍵完成)";
                return;
            }
            // 右鍵完成多邊形繪製
            if (e.Button == MouseButtons.Right && _editState.IsDrawingPassabilityPolygon && _editState.PassabilityPolygonPoints.Count >= 3)
            {
                // 確保顯示通行性覆蓋層，以便看到修改結果
                EnsurePassabilityLayerVisible();
                SetPolygonPassable(_editState.PassabilityPolygonPoints, currentPassableEditMode == PassableEditMode.SetPassable);
                _editState.PassabilityPolygonPoints.Clear();
                _editState.IsDrawingPassabilityPolygon = false;
                s32PictureBox.Invalidate();
                return;
            }
            // 右鍵取消多邊形繪製（頂點不足）
            if (e.Button == MouseButtons.Right && _editState.IsDrawingPassabilityPolygon)
            {
                _editState.PassabilityPolygonPoints.Clear();
                _editState.IsDrawingPassabilityPolygon = false;
                s32PictureBox.Invalidate();
                this.toolStripStatusLabel1.Text = "已取消多邊形繪製";
                return;
            }
            // 左鍵：開始區域選擇
            else if (e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.None)
            {
                isSelectingRegion = true;
                isLayer4CopyMode = true;  // 進入複製模式
                regionStartPoint = e.Location;
                regionEndPoint = e.Location;
                selectedRegion = new Rectangle();
                this.toolStripStatusLabel1.Text = "選取區域... (放開後按 Ctrl+C 複製)";
            }
        }

        // S32 地圖鼠標移動事件 - 更新選擇區域或拖拽移動
        private void s32PictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            var totalSw = Stopwatch.StartNew();

            // 中鍵拖拽移動視圖
            if (isMainMapDragging)
            {
                int deltaX = e.X - mainMapDragStartPoint.X;
                int deltaY = e.Y - mainMapDragStartPoint.Y;

                // 計算新的捲動位置（世界座標，需要除以縮放）
                int newScrollX = mainMapDragStartScroll.X - (int)(deltaX / s32ZoomLevel);
                int newScrollY = mainMapDragStartScroll.Y - (int)(deltaY / s32ZoomLevel);

                // 限制在有效範圍內（使用世界座標）
                int maxScrollX = Math.Max(0, _viewState.MapWidth - (int)(s32MapPanel.Width / s32ZoomLevel));
                int maxScrollY = Math.Max(0, _viewState.MapHeight - (int)(s32MapPanel.Height / s32ZoomLevel));
                newScrollX = Math.Max(0, Math.Min(newScrollX, maxScrollX));
                newScrollY = Math.Max(0, Math.Min(newScrollY, maxScrollY));

                // 更新 ViewState 的捲動位置
                _viewState.SetScrollSilent(newScrollX, newScrollY);

                // 強制立即重繪（不等待消息循環）
                var invalidateSw = Stopwatch.StartNew();
                s32PictureBox.Invalidate();
                s32PictureBox.Update();
                invalidateSw.Stop();
                totalSw.Stop();
                LogPerf($"[MOUSE-MOVE-DRAG] invalidate+update={invalidateSw.ElapsedMilliseconds}ms, total={totalSw.ElapsedMilliseconds}ms");
                return;
            }

            // 更新狀態列顯示遊戲座標（拖曳時跳過）
            var statusSw = Stopwatch.StartNew();
            UpdateStatusBarWithGameCoords(e.X, e.Y);
            statusSw.Stop();

            if (isSelectingRegion)
            {
                regionEndPoint = e.Location;

                // 計算起點到終點之間的格子範圍（所有模式都對齊格線）
                var cellsSw = Stopwatch.StartNew();
                _editState.SelectedCells = GetCellsInIsometricRange(regionStartPoint, regionEndPoint);
                cellsSw.Stop();

                var boundsSw = Stopwatch.StartNew();
                if (_editState.SelectedCells.Count > 0)
                {
                    selectedRegion = GetAlignedBoundsFromCells(_editState.SelectedCells);
                }
                boundsSw.Stop();

                // 重繪以顯示選擇框
                var invalidateSw = Stopwatch.StartNew();
                s32PictureBox.Invalidate();
                invalidateSw.Stop();

                totalSw.Stop();
                LogPerf($"[MOUSE-MOVE-SELECT] status={statusSw.ElapsedMilliseconds}ms, cells={cellsSw.ElapsedMilliseconds}ms ({_editState.SelectedCells.Count}), bounds={boundsSw.ElapsedMilliseconds}ms, invalidate={invalidateSw.ElapsedMilliseconds}ms, total={totalSw.ElapsedMilliseconds}ms");
            }
        }

        // 更新狀態列顯示遊戲座標
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

        // S32 地圖鼠標釋放事件 - 完成區域選擇並執行批量操作
        private void s32PictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            var totalSw = Stopwatch.StartNew();

            // 結束中鍵拖拽
            if (e.Button == MouseButtons.Middle && isMainMapDragging)
            {
                var miniMapSw = Stopwatch.StartNew();
                isMainMapDragging = false;
                this.s32PictureBox.Cursor = Cursors.Default;
                UpdateMiniMap();
                miniMapSw.Stop();

                // 拖曳結束後延遲渲染（避免快速連續拖曳時頻繁重渲染）
                dragRenderTimer.Stop();
                dragRenderTimer.Start();
                totalSw.Stop();
                LogPerf($"[MOUSE-UP-DRAG] miniMap={miniMapSw.ElapsedMilliseconds}ms, total={totalSw.ElapsedMilliseconds}ms");
                return;
            }

            if (isSelectingRegion && e.Button == MouseButtons.Left)
            {
                isSelectingRegion = false;

                // Layer4 複製模式：保留選取範圍，等待 Ctrl+C 或 Ctrl+V
                if (isLayer4CopyMode)
                {
                    var boundsSw = Stopwatch.StartNew();
                    // _editState.SelectedCells 已在 MouseMove 中更新
                    if (_editState.SelectedCells.Count > 0)
                    {
                        // 計算所有選中格子的螢幕座標邊界
                        copyRegionBounds = GetAlignedBoundsFromCells(_editState.SelectedCells);
                        selectedRegion = copyRegionBounds;
                    }
                    else
                    {
                        copyRegionBounds = selectedRegion;
                    }
                    boundsSw.Stop();

                    // 計算選取區域的全域 Layer1 座標原點（使用選中格子中最小的 X, Y 作為原點）
                    var originSw = Stopwatch.StartNew();
                    int globalX = -1, globalY = -1;
                    int gameX = -1, gameY = -1;
                    if (_editState.SelectedCells.Count > 0)
                    {
                        // 找出所有選中格子的最小全域 Layer1 座標
                        int minGlobalX = int.MaxValue, minGlobalY = int.MaxValue;
                        foreach (var cell in _editState.SelectedCells)
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
                    _editState.CopyRegionOrigin = new Point(globalX, globalY);
                    originSw.Stop();

                    // 更新移動指令（顯示選取第一格的座標）
                    if (gameX >= 0 && gameY >= 0)
                    {
                        _editState.SelectedGameX = gameX;
                        _editState.SelectedGameY = gameY;
                        toolStripCopyMoveCmd.Enabled = true;
                        toolStripCopyMoveCmd.Text = $"移動 {gameX} {gameY} {_document.MapId}";
                    }

                    // 根據是否有剪貼簿資料顯示不同提示（顯示遊戲座標）
                    if (hasLayer4Clipboard && _editState.CellClipboard.Count > 0)
                    {
                        this.toolStripStatusLabel1.Text = $"已選取貼上位置 (遊戲座標: {gameX}, {gameY})，按 Ctrl+V 貼上 {_editState.CellClipboard.Count} 格資料，選中 {_editState.SelectedCells.Count} 格";
                    }
                    else
                    {
                        this.toolStripStatusLabel1.Text = $"已選取區域 (遊戲座標: {gameX}, {gameY})，選中 {_editState.SelectedCells.Count} 格，按 Ctrl+C 複製";
                    }

                    // 更新群組縮圖列表
                    var thumbSw = Stopwatch.StartNew();
                    if (_editState.IsLayer5EditMode && _editState.SelectedCells.Count > 0)
                    {
                        // 透明編輯模式：使用選取區域第一格的位置顯示附近群組（有 L5 設定的排前面）
                        var firstCell = _editState.SelectedCells[0];
                        UpdateNearbyGroupThumbnails(firstCell.S32Data, firstCell.LocalX, firstCell.LocalY, 10);
                    }
                    else
                    {
                        // 一般模式：顯示選取區域內的群組
                        UpdateGroupThumbnailsList(_editState.SelectedCells);
                    }
                    thumbSw.Stop();

                    // 在透明編輯模式下，需要重新渲染以顯示 Layer5 群組覆蓋層
                    if (_editState.IsLayer5EditMode)
                    {
                        RenderS32Map();
                    }
                    else
                    {
                        // 保留選取框顯示
                        s32PictureBox.Invalidate();
                    }
                    totalSw.Stop();
                    LogPerf($"[MOUSE-UP-SELECT] bounds={boundsSw.ElapsedMilliseconds}ms, origin={originSw.ElapsedMilliseconds}ms, thumb={thumbSw.ElapsedMilliseconds}ms, total={totalSw.ElapsedMilliseconds}ms, cells={_editState.SelectedCells.Count}");
                    return;
                }

                // 找出選中區域內的所有格子
                var cellsSw = Stopwatch.StartNew();
                List<SelectedCell> selectedCells = GetCellsInRegion(selectedRegion);
                cellsSw.Stop();

                if (selectedCells.Count > 0)
                {
                    // 刪除模式：批次刪除物件
                    var deleteSw = Stopwatch.StartNew();
                    DeleteAllLayer4ObjectsInRegion(selectedCells);
                    deleteSw.Stop();
                    totalSw.Stop();
                    LogPerf($"[MOUSE-UP-DELETE] cells={cellsSw.ElapsedMilliseconds}ms, delete={deleteSw.ElapsedMilliseconds}ms, total={totalSw.ElapsedMilliseconds}ms");
                }

                // 清除選擇框
                selectedRegion = new Rectangle();
                s32PictureBox.Invalidate();
            }
        }

        // S32 PictureBox 繪製事件 - 繪製 Viewport 和選擇框或多邊形
        private void s32PictureBox_Paint(object sender, PaintEventArgs e)
        {
            var paintSw = Stopwatch.StartNew();

            // 繪製 Viewport Bitmap（加鎖保護避免多執行緒衝突）
            lock (_viewportBitmapLock)
            {
                if (_viewportBitmap != null && _viewState.RenderWidth > 0)
                {
                    // 計算 Viewport Bitmap 在 PictureBox 上的繪製位置
                    // _viewState.RenderOriginX/Y 是已渲染區域的世界座標原點
                    // _viewState.ScrollX/Y 是當前視圖的世界座標位置
                    // 繪製位置 = (RenderOrigin - Scroll) * ZoomLevel
                    int drawX = (int)((_viewState.RenderOriginX - _viewState.ScrollX) * s32ZoomLevel);
                    int drawY = (int)((_viewState.RenderOriginY - _viewState.ScrollY) * s32ZoomLevel);

                    // Viewport Bitmap 是未縮放的，需要縮放繪製
                    int drawWidth = (int)(_viewState.RenderWidth * s32ZoomLevel);
                    int drawHeight = (int)(_viewState.RenderHeight * s32ZoomLevel);

                    var drawSw = Stopwatch.StartNew();
                    e.Graphics.DrawImage(_viewportBitmap, drawX, drawY, drawWidth, drawHeight);
                    drawSw.Stop();
                    LogPerf($"[PAINT] drawImage={drawSw.ElapsedMilliseconds}ms, bmpSize={_viewportBitmap.Width}x{_viewportBitmap.Height}, drawPos=({drawX},{drawY}), drawSize={drawWidth}x{drawHeight}");
                }
                else
                {
                    LogPerf($"[PAINT] no bitmap, _viewportBitmap={(_viewportBitmap != null ? "exists" : "null")}, RenderWidth={_viewState.RenderWidth}");
                }
            }

            // 通行性編輯模式：繪製多邊形
            if (_editState.IsDrawingPassabilityPolygon && _editState.PassabilityPolygonPoints.Count > 0)
            {
                Color polygonColor = currentPassableEditMode == PassableEditMode.SetPassable ? Color.LimeGreen : Color.Red;

                // 繪製已有的多邊形邊
                using (Pen pen = new Pen(polygonColor, 3))
                {
                    for (int i = 0; i < _editState.PassabilityPolygonPoints.Count - 1; i++)
                    {
                        e.Graphics.DrawLine(pen, _editState.PassabilityPolygonPoints[i], _editState.PassabilityPolygonPoints[i + 1]);
                    }
                    // 如果有3個以上頂點，繪製封閉線（虛線預覽）
                    if (_editState.PassabilityPolygonPoints.Count >= 3)
                    {
                        using (Pen dashPen = new Pen(polygonColor, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                        {
                            e.Graphics.DrawLine(dashPen, _editState.PassabilityPolygonPoints[_editState.PassabilityPolygonPoints.Count - 1], _editState.PassabilityPolygonPoints[0]);
                        }
                    }
                }

                // 繪製頂點標記
                using (SolidBrush brush = new SolidBrush(polygonColor))
                {
                    foreach (var pt in _editState.PassabilityPolygonPoints)
                    {
                        e.Graphics.FillEllipse(brush, pt.X - 5, pt.Y - 5, 10, 10);
                    }
                }

                // 繪製半透明填充預覽（如果有3個以上頂點）
                if (_editState.PassabilityPolygonPoints.Count >= 3)
                {
                    using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(50, polygonColor)))
                    {
                        e.Graphics.FillPolygon(fillBrush, _editState.PassabilityPolygonPoints.ToArray());
                    }
                }
                return;
            }

            // 有選中的格子時，繪製對齊格線的菱形選取框
            if (_editState.SelectedCells.Count > 0)
            {
                Color color = isSelectingRegion ? Color.Green : Color.Orange;
                var drawCellsSw = Stopwatch.StartNew();
                DrawSelectedCells(e.Graphics, _editState.SelectedCells, color);
                drawCellsSw.Stop();
                LogPerf($"[PAINT] DrawSelectedCells={drawCellsSw.ElapsedMilliseconds}ms, cellCount={_editState.SelectedCells.Count}");

                // 顯示選取的格子數量
                if (isSelectingRegion)
                {
                    string info = $"選取 {_editState.SelectedCells.Count} 格";
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

            paintSw.Stop();
            LogPerf($"[PAINT] total={paintSw.ElapsedMilliseconds}ms");
        }

        // 繪製選中的格子（每個格子繪製獨立的菱形）
        private void DrawSelectedCells(Graphics g, List<SelectedCell> cells, Color color)
        {
            if (cells.Count == 0 || string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
                return;

            // 使用 ViewState 的捲動位置（世界座標）
            int scrollX = _viewState.ScrollX;
            int scrollY = _viewState.ScrollY;

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

                    // 計算世界座標
                    int worldX = mx + localBaseX + x * 24 + y * 24;
                    int worldY = my + localBaseY + y * 12;

                    // 轉換為螢幕座標（考慮捲動位置和縮放）
                    // 螢幕座標 = (世界座標 - 捲動位置) * 縮放
                    int screenX = (int)((worldX - scrollX) * s32ZoomLevel);
                    int screenY = (int)((worldY - scrollY) * s32ZoomLevel);
                    int scaledWidth = (int)(48 * s32ZoomLevel);
                    int scaledHeight = (int)(24 * s32ZoomLevel);

                    // Layer3 菱形四個頂點（48x24，與 DrawS32Grid 一致）
                    Point[] diamondPoints = new Point[]
                    {
                        new Point(screenX, screenY + scaledHeight / 2),       // 左
                        new Point(screenX + scaledWidth / 2, screenY),        // 上
                        new Point(screenX + scaledWidth, screenY + scaledHeight / 2),  // 右
                        new Point(screenX + scaledWidth / 2, screenY + scaledHeight)   // 下
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
        // SelectedCell 已移至 Models/S32DataModels.cs

        private List<SelectedCell> GetCellsInRegion(Rectangle region)
        {
            List<SelectedCell> cells = new List<SelectedCell>();

            // 獲取當前地圖資訊以計算正確的 baseY
            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
                return cells;

            Struct.L1Map currentMap = Share.MapDataList[_document.MapId];

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
            foreach (var s32Data in _document.S32Files.Values)
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
                foreach (var s32Data in _document.S32Files.Values)
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
                            if (_editState.IsFilteringLayer4Groups && _editState.SelectedLayer4Groups.Count > 0 &&
                                !_editState.SelectedLayer4Groups.Contains(obj.GroupId))
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
                    // Layer5 的 X 是 0-127 (Layer1 座標)，Y 是 0-63
                    // cell.LocalX 已經是 Layer1 座標 (0-127)
                    // 一個遊戲格子對應兩個 Layer1 X 座標（LocalX 和 LocalX+1）
                    int layer1X = cell.LocalX;
                    var layer5Items = cell.S32Data.Layer5.Where(l => (l.X == layer1X || l.X == layer1X + 1) && l.Y == cell.LocalY).ToList();
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

                // 清除快取並重新渲染
                ClearS32BlockCache();
                RenderS32Map();

                // 更新 Layer5 異常檢查按鈕
                UpdateLayer5InvalidButton();

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
            if (_editState.IsFilteringLayer4Groups && _editState.SelectedLayer4Groups.Count > 0)
            {
                objectsAtCell = objectsAtCell.Where(o => _editState.SelectedLayer4Groups.Contains(o.GroupId)).ToList();
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

        // 創建第五層面板 - 可透明化的圖塊（只顯示該格子相關的項目）
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

            // 只篩選該格子相關的 Layer5 項目
            // x 是 Layer1 座標 (0-127)，y 是 Layer3 座標 (0-63)
            // Layer5 的 X 是 0-127，Y 是 0-63
            // 一個遊戲格子對應兩個 Layer1 X 座標（x 和 x+1）
            var cellLayer5Items = new List<(int index, Layer5Item item)>();
            for (int i = 0; i < currentS32Data.Layer5.Count; i++)
            {
                var item5 = currentS32Data.Layer5[i];
                if ((item5.X == x || item5.X == x + 1) && item5.Y == y)
                {
                    cellLayer5Items.Add((i, item5));
                }
            }

            if (cellLayer5Items.Count > 0)
            {
                Label countLabel = new Label();
                countLabel.Text = $"此格數量: {cellLayer5Items.Count}";
                countLabel.Dock = DockStyle.Top;
                countLabel.Height = 20;
                countLabel.TextAlign = ContentAlignment.MiddleCenter;

                ListView listView = new ListView();
                listView.Dock = DockStyle.Fill;
                listView.View = View.Details;
                listView.FullRowSelect = true;
                listView.GridLines = true;
                listView.Font = new Font("Consolas", 9, FontStyle.Regular);

                listView.Columns.Add("索引", 40);
                listView.Columns.Add("X", 40);
                listView.Columns.Add("Y", 40);
                listView.Columns.Add("ObjIdx", 60);
                listView.Columns.Add("Type", 50);

                foreach (var (idx, item5) in cellLayer5Items)
                {
                    var lvItem = new ListViewItem(idx.ToString());
                    lvItem.SubItems.Add(item5.X.ToString());
                    lvItem.SubItems.Add(item5.Y.ToString());
                    lvItem.SubItems.Add(item5.ObjectIndex.ToString());
                    lvItem.SubItems.Add(item5.Type.ToString());
                    listView.Items.Add(lvItem);
                }

                // 先加入 Fill 的控件，再加入 Top 的控件（Dock 順序）
                panel.Controls.Add(listView);
                panel.Controls.Add(countLabel);
            }
            else
            {
                Label info = new Label();
                info.Text = "此格無資料";
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
                    lvItem.SubItems.Add($"0x{item8.ExtendedData:X8}");
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

            foreach (var s32Data in _document.S32Files.Values)
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
            foreach (var s32Data in _document.S32Files.Values)
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
            foreach (var s32Data in _document.S32Files.Values)
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
            foreach (var s32Data in _document.S32Files.Values)
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
            foreach (var s32Data in _document.S32Files.Values)
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

            foreach (var s32Data in _document.S32Files.Values)
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
            if (_editState.SelectedGameX >= 0 && _editState.SelectedGameY >= 0 && !string.IsNullOrEmpty(_document.MapId))
            {
                string moveCmd = $"移動 {_editState.SelectedGameX} {_editState.SelectedGameY} {_document.MapId}";
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
            if (!Share.MapDataList.ContainsKey(_document.MapId))
                return;

            Struct.L1Map currentMap = Share.MapDataList[_document.MapId];

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
                    int viewportWidthWorld = (int)(s32MapPanel.Width / s32ZoomLevel);
                    int viewportHeightWorld = (int)(s32MapPanel.Height / s32ZoomLevel);
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
            var modifiedFiles = _document.S32Files.Where(kvp => kvp.Value.IsModified).ToList();

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
            if (!_document.S32Files.ContainsKey(filePath))
            {
                throw new InvalidOperationException($"S32 檔案不在記憶體中: {filePath}");
            }

            S32Data s32Data = _document.S32Files[filePath];

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
                    bw.Write(item.X);
                    bw.Write(item.Y);
                    bw.Write(item.IndexId);
                    bw.Write(item.TileId);
                    bw.Write(item.UK);
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
                // 第五層 - 事件
                bw.Write(s32Data.Layer5.Count);
                foreach (var item in s32Data.Layer5)
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
                ushort lv8Count = (ushort)s32Data.Layer8.Count;
                if (s32Data.Layer8HasExtendedData)
                {
                    lv8Count |= 0x8000;  // 設置高位表示有擴展資料
                }
                bw.Write(lv8Count);
                foreach (var item in s32Data.Layer8)
                {
                    bw.Write(item.SprId);
                    bw.Write(item.X);
                    bw.Write(item.Y);
                    if (s32Data.Layer8HasExtendedData)
                    {
                        bw.Write(item.ExtendedData);
                    }
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
            _editState.SelectedLayer4Groups.Clear();
            _editState.IsFilteringLayer4Groups = false;

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

        // 更新附近群組縮圖（透明編輯模式用）
        private void UpdateNearbyGroupThumbnails(S32Data clickedS32Data, int cellX, int cellY, int radius)
        {
            // 計算點擊位置的遊戲座標
            int clickedGameX = clickedS32Data.SegInfo.nLinBeginX + cellX / 2;
            int clickedGameY = clickedS32Data.SegInfo.nLinBeginY + cellY;

            // 先收集點擊格子的 Layer5 設定（用於判斷群組是否有設定）
            var clickedCellLayer5 = new Dictionary<int, byte>();  // GroupId -> Type
            foreach (var item in clickedS32Data.Layer5)
            {
                if (item.X == cellX && item.Y == cellY)
                {
                    if (!clickedCellLayer5.ContainsKey(item.ObjectIndex))
                    {
                        clickedCellLayer5[item.ObjectIndex] = item.Type;
                    }
                }
            }

            // 收集附近的群組，記錄距離和 Layer5 設定
            var nearbyGroups = new Dictionary<int, (int distance, List<(S32Data s32, ObjectTile obj)> objects, bool hasLayer5, byte layer5Type)>();

            // 遍歷所有 S32 檔案搜索附近的物件
            foreach (var s32Data in _document.S32Files.Values)
            {
                int segStartX = s32Data.SegInfo.nLinBeginX;
                int segStartY = s32Data.SegInfo.nLinBeginY;

                foreach (var obj in s32Data.Layer4)
                {
                    // 計算物件的遊戲座標
                    int objGameX = segStartX + obj.X / 2;
                    int objGameY = segStartY + obj.Y;

                    // 計算距離（曼哈頓距離）
                    int distance = Math.Abs(objGameX - clickedGameX) + Math.Abs(objGameY - clickedGameY);

                    if (distance <= radius)
                    {
                        if (!nearbyGroups.ContainsKey(obj.GroupId))
                        {
                            // 檢查該群組是否有 Layer5 設定（從點擊格子的 Layer5 中查找）
                            bool hasLayer5 = clickedCellLayer5.TryGetValue(obj.GroupId, out byte layer5Type);
                            nearbyGroups[obj.GroupId] = (distance, new List<(S32Data, ObjectTile)>(), hasLayer5, layer5Type);
                        }

                        // 更新最小距離
                        var current = nearbyGroups[obj.GroupId];
                        if (distance < current.distance)
                        {
                            nearbyGroups[obj.GroupId] = (distance, current.objects, current.hasLayer5, current.layer5Type);
                        }
                        nearbyGroups[obj.GroupId].objects.Add((s32Data, obj));
                    }
                }
            }

            if (nearbyGroups.Count == 0)
            {
                lblGroupThumbnails.Text = "附近群組 (0)";
                lvGroupThumbnails.Items.Clear();
                return;
            }

            // 排序：有 Layer5 設定的優先，然後按距離排序
            var sortedGroups = nearbyGroups
                .OrderByDescending(g => g.Value.hasLayer5)  // 有 Layer5 設定的排前面
                .ThenBy(g => g.Value.distance)              // 距離近的排前面
                .ThenBy(g => g.Key)                         // 相同時按 GroupId 排序
                .ToList();

            // Debug: 輸出排序結果
            Console.WriteLine($"[Layer5Edit] clickedCellLayer5 count: {clickedCellLayer5.Count}");
            foreach (var g in sortedGroups.Take(10))
            {
                Console.WriteLine($"  GroupId={g.Key}, hasL5={g.Value.hasLayer5}, dist={g.Value.distance}");
            }

            // 計算有 Layer5 設定的群組數量
            int l5Count = sortedGroups.Count(g => g.Value.hasLayer5);

            // 更新群組縮圖列表
            lblGroupThumbnails.Text = $"附近群組 ({sortedGroups.Count}, L5:{l5Count}) 載入中...";

            // 取消之前的縮圖產生任務
            if (_groupThumbnailCts != null)
            {
                _groupThumbnailCts.Cancel();
                _groupThumbnailCts.Dispose();
            }
            _groupThumbnailCts = new System.Threading.CancellationTokenSource();
            var cancellationToken = _groupThumbnailCts.Token;

            int totalGroups = sortedGroups.Count;

            // 在背景執行緒並行產生縮圖
            Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();
                var thumbnailResults = new System.Collections.Concurrent.ConcurrentDictionary<int, (int groupId, int objectCount, Bitmap thumbnail, List<(S32Data s32, ObjectTile obj)> objects, bool hasLayer5, byte layer5Type, int distance)>();

                Parallel.ForEach(sortedGroups, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (kvp, state) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        state.Stop();
                        return;
                    }

                    int groupId = kvp.Key;
                    var info = kvp.Value;

                    // 生成群組縮圖（傳遞 Layer5 設定以繪製邊框）
                    Bitmap thumbnail = GenerateGroupThumbnail(info.objects, 80, info.hasLayer5, info.layer5Type);

                    if (thumbnail != null && !cancellationToken.IsCancellationRequested)
                    {
                        thumbnailResults[groupId] = (groupId, info.objects.Count, thumbnail, info.objects, info.hasLayer5, info.layer5Type, info.distance);
                    }
                });

                stopwatch.Stop();
                long elapsedMs = stopwatch.ElapsedMilliseconds;

                if (cancellationToken.IsCancellationRequested)
                {
                    foreach (var result in thumbnailResults.Values)
                    {
                        result.thumbnail?.Dispose();
                    }
                    return;
                }

                // 在 UI 執行緒更新 ListView（保持排序順序）
                try
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        if (cancellationToken.IsCancellationRequested) return;

                        ImageList imageList = new ImageList();
                        imageList.ImageSize = new Size(80, 80);
                        imageList.ColorDepth = ColorDepth.Depth32Bit;

                        lvGroupThumbnails.Items.Clear();
                        if (lvGroupThumbnails.LargeImageList != null)
                        {
                            lvGroupThumbnails.LargeImageList.Dispose();
                        }

                        int thumbnailIndex = 0;
                        // 按原始排序順序添加（有 Layer5 的優先，然後按距離）
                        foreach (var kvp in sortedGroups)
                        {
                            if (!thumbnailResults.TryGetValue(kvp.Key, out var result)) continue;

                            imageList.Images.Add(result.thumbnail);

                            string distanceText = result.distance == 0 ? "●" : $"D{result.distance}";
                            ListViewItem item = new ListViewItem($"{distanceText} G{result.groupId} ({result.objectCount})");
                            item.ImageIndex = thumbnailIndex;
                            item.Tag = new GroupThumbnailInfo
                            {
                                GroupId = result.groupId,
                                Objects = result.objects,
                                HasLayer5Setting = result.hasLayer5,
                                Layer5Type = result.layer5Type
                            };
                            lvGroupThumbnails.Items.Add(item);

                            thumbnailIndex++;
                        }

                        lvGroupThumbnails.LargeImageList = imageList;
                        lblGroupThumbnails.Text = $"附近群組 ({totalGroups}) [{elapsedMs}ms]";
                    });
                }
                catch { }
            });
        }

        // Layer4 群組勾選變更事件
        private void lvLayer4Groups_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (e.Item.Tag == null)
                return;

            int groupId = (int)e.Item.Tag;

            if (e.Item.Checked)
            {
                _editState.SelectedLayer4Groups.Add(groupId);
            }
            else
            {
                _editState.SelectedLayer4Groups.Remove(groupId);
            }

            // 只要有任何勾選就啟用篩選
            _editState.IsFilteringLayer4Groups = _editState.SelectedLayer4Groups.Count > 0;

            // 重新渲染地圖
            RenderS32Map();
        }

        // 更新群組縮圖列表（顯示所有已載入 S32 的群組）
        private void UpdateGroupThumbnailsList()
        {
            UpdateGroupThumbnailsList(null);  // 不帶選取區域時顯示全部
        }

        // 群組縮圖產生取消 token
        private System.Threading.CancellationTokenSource _groupThumbnailCts = null;

        // 更新群組縮圖列表（可指定只顯示選取區域內的群組）- 非同步版本
        private void UpdateGroupThumbnailsList(List<SelectedCell> selectedCells)
        {
            // 取消之前的縮圖產生任務
            if (_groupThumbnailCts != null)
            {
                _groupThumbnailCts.Cancel();
                _groupThumbnailCts.Dispose();
            }
            _groupThumbnailCts = new System.Threading.CancellationTokenSource();
            var cancellationToken = _groupThumbnailCts.Token;

            lvGroupThumbnails.Items.Clear();

            if (lvGroupThumbnails.LargeImageList != null)
            {
                lvGroupThumbnails.LargeImageList.Dispose();
            }

            if (_document.S32Files.Count == 0)
            {
                lblGroupThumbnails.Text = "群組縮圖列表";
                return;
            }

            // 收集群組（根據是否有選取區域決定範圍）
            var allGroupsDict = new Dictionary<int, List<(S32Data s32, ObjectTile obj)>>();

            if (selectedCells != null && selectedCells.Count > 0)
            {
                // 建立選取格子的全域座標集合 (使用 Layer1 座標系統 0-127)
                // 注意：每個 SelectedCell 對應兩個 Layer1 格子（偶數和奇數 X）
                var selectedLayer1Cells = new HashSet<(int x, int y)>();
                foreach (var cell in selectedCells)
                {
                    int layer1GlobalX = cell.S32Data.SegInfo.nLinBeginX * 2 + cell.LocalX;
                    int layer1GlobalY = cell.S32Data.SegInfo.nLinBeginY + cell.LocalY;
                    // 加入偶數 X 座標
                    selectedLayer1Cells.Add((layer1GlobalX, layer1GlobalY));
                    // 加入奇數 X 座標（同一個遊戲格子的另一半）
                    selectedLayer1Cells.Add((layer1GlobalX + 1, layer1GlobalY));
                }

                // 遍歷所有 S32，找出全域座標落在選取格子內的 Layer4 物件
                // 只收集選取區域內的物件（交集）
                foreach (var s32Data in _document.S32Files.Values)
                {
                    int segStartX = s32Data.SegInfo.nLinBeginX;
                    int segStartY = s32Data.SegInfo.nLinBeginY;

                    foreach (var obj in s32Data.Layer4)
                    {
                        // obj.X 本身就是 Layer1 局部座標 (0-127)
                        int layer1GlobalX = segStartX * 2 + obj.X;
                        int layer1GlobalY = segStartY + obj.Y;

                        // 檢查物件是否在選取範圍內
                        bool inSelection = selectedLayer1Cells.Contains((layer1GlobalX, layer1GlobalY));

                        if (inSelection)
                        {
                            if (!allGroupsDict.ContainsKey(obj.GroupId))
                            {
                                allGroupsDict[obj.GroupId] = new List<(S32Data, ObjectTile)>();
                            }
                            // 只加入選取區域內的物件
                            allGroupsDict[obj.GroupId].Add((s32Data, obj));
                        }
                    }
                }
            }
            else
            {
                // 收集所有 S32 中的群組
                foreach (var s32Data in _document.S32Files.Values)
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

            int totalGroups = allGroupsDict.Count;
            bool isSelectedMode = selectedCells != null && selectedCells.Count > 0;

            // 收集所有 Layer5 的 GroupId -> Type 對應（用於顯示邊框顏色）
            var groupLayer5Info = new Dictionary<int, byte>();
            if (selectedCells != null && selectedCells.Count > 0)
            {
                // 只收集選取格子相關的 Layer5 設定
                foreach (var cell in selectedCells)
                {
                    foreach (var item in cell.S32Data.Layer5)
                    {
                        if (item.X == cell.LocalX && item.Y == cell.LocalY)
                        {
                            if (!groupLayer5Info.ContainsKey(item.ObjectIndex))
                            {
                                groupLayer5Info[item.ObjectIndex] = item.Type;
                            }
                        }
                    }
                }
            }
            else
            {
                // 收集所有 S32 的 Layer5 設定
                foreach (var s32Data in _document.S32Files.Values)
                {
                    foreach (var item in s32Data.Layer5)
                    {
                        if (!groupLayer5Info.ContainsKey(item.ObjectIndex))
                        {
                            groupLayer5Info[item.ObjectIndex] = item.Type;
                        }
                    }
                }
            }

            // 顯示載入中狀態
            lblGroupThumbnails.Text = isSelectedMode
                ? $"選取區域群組 (載入中 0/{totalGroups})"
                : $"群組縮圖列表 (載入中 0/{totalGroups})";

            // 準備群組資料供背景執行緒使用
            var groupList = allGroupsDict.OrderBy(k => k.Key).ToList();

            // 在背景執行緒並行產生縮圖
            Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();

                // 使用 Parallel.ForEach 並行產生縮圖
                var thumbnailResults = new System.Collections.Concurrent.ConcurrentDictionary<int, (int groupId, int objectCount, Bitmap thumbnail, List<(S32Data s32, ObjectTile obj)> objects, bool hasLayer5, byte layer5Type)>();

                int processedCount = 0;
                int lastReportedCount = 0;

                Parallel.ForEach(groupList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (kvp, state) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        state.Stop();
                        return;
                    }

                    int groupId = kvp.Key;
                    var objects = kvp.Value;

                    // 檢查該群組是否有 Layer5 設定
                    bool hasLayer5 = groupLayer5Info.TryGetValue(groupId, out byte layer5Type);

                    // 生成群組縮圖（傳遞 Layer5 設定以繪製邊框）
                    Bitmap thumbnail = GenerateGroupThumbnail(objects, 80, hasLayer5, layer5Type);

                    if (thumbnail != null && !cancellationToken.IsCancellationRequested)
                    {
                        thumbnailResults[groupId] = (groupId, objects.Count, thumbnail, objects, hasLayer5, layer5Type);
                    }

                    // 更新進度（每處理 10 個或處理完成時更新 UI）
                    int current = System.Threading.Interlocked.Increment(ref processedCount);
                    if (current - lastReportedCount >= 10 || current == totalGroups)
                    {
                        lastReportedCount = current;
                        try
                        {
                            this.BeginInvoke((MethodInvoker)delegate
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    lblGroupThumbnails.Text = isSelectedMode
                                        ? $"選取區域群組 (載入中 {current}/{totalGroups})"
                                        : $"群組縮圖列表 (載入中 {current}/{totalGroups})";
                                }
                            });
                        }
                        catch { }
                    }
                });

                stopwatch.Stop();
                long elapsedMs = stopwatch.ElapsedMilliseconds;

                // 如果被取消就不更新 UI
                if (cancellationToken.IsCancellationRequested)
                {
                    // 清理已產生的 Bitmap
                    foreach (var result in thumbnailResults.Values)
                    {
                        result.thumbnail?.Dispose();
                    }
                    return;
                }

                // 在 UI 執行緒更新 ListView
                try
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            foreach (var result in thumbnailResults.Values)
                            {
                                result.thumbnail?.Dispose();
                            }
                            return;
                        }

                        // 建立 ImageList
                        ImageList imageList = new ImageList();
                        imageList.ImageSize = new Size(80, 80);
                        imageList.ColorDepth = ColorDepth.Depth32Bit;

                        lvGroupThumbnails.Items.Clear();
                        if (lvGroupThumbnails.LargeImageList != null)
                        {
                            lvGroupThumbnails.LargeImageList.Dispose();
                        }

                        int thumbnailIndex = 0;
                        foreach (var groupId in thumbnailResults.Keys.OrderBy(k => k))
                        {
                            var result = thumbnailResults[groupId];
                            imageList.Images.Add(result.thumbnail);

                            ListViewItem item = new ListViewItem($"G{result.groupId} ({result.objectCount})");
                            item.ImageIndex = thumbnailIndex;
                            item.Tag = new GroupThumbnailInfo
                            {
                                GroupId = result.groupId,
                                Objects = result.objects,
                                HasLayer5Setting = result.hasLayer5,
                                Layer5Type = result.layer5Type
                            };
                            lvGroupThumbnails.Items.Add(item);

                            thumbnailIndex++;
                        }

                        lvGroupThumbnails.LargeImageList = imageList;

                        string labelText = isSelectedMode
                            ? $"選取區域群組 ({totalGroups}) [{elapsedMs}ms]"
                            : $"群組縮圖列表 ({totalGroups}) [{elapsedMs}ms]";
                        lblGroupThumbnails.Text = labelText;

                        // 更新狀態列，將「background」替換為實際時間
                        if (this.toolStripStatusLabel1.Text.Contains("Thumbnails: background"))
                        {
                            this.toolStripStatusLabel1.Text = this.toolStripStatusLabel1.Text.Replace(
                                "Thumbnails: background",
                                $"Thumbnails: {elapsedMs}ms");
                        }

                        // Console log thumbnail timing
                        Console.WriteLine($"[THUMBNAILS] Completed: {totalGroups} groups in {elapsedMs} ms");
                    });
                }
                catch { }
            });
        }

        // 群組縮圖資訊
        private class GroupThumbnailInfo
        {
            public int GroupId { get; set; }
            public List<(S32Data s32, ObjectTile obj)> Objects { get; set; }
            public bool HasLayer5Setting { get; set; }  // 是否有 Layer5 設定
            public byte Layer5Type { get; set; }        // Layer5 Type (0=半透明, 1=其他)
        }

        // 「全部」按鈕點擊事件 - 顯示全部群組
        private void btnShowAllGroups_Click(object sender, EventArgs e)
        {
            UpdateGroupThumbnailsList(null);  // 傳入 null 顯示全部
        }

        // 縮圖邊框畫筆（重用避免重複建立）
        private static readonly Pen _thumbnailBorderPen = new Pen(Color.LightGray, 1);
        private static readonly Pen _thumbnailBorderPenType0 = new Pen(Color.FromArgb(180, 0, 255), 3);  // 紫色 - Type=0
        private static readonly Pen _thumbnailBorderPenType1 = new Pen(Color.FromArgb(255, 80, 80), 3);  // 紅色 - Type=1

        // 生成群組縮圖（將同 GroupId 的物件按相對位置組裝，使用與主畫布相同的繪製方式）
        private Bitmap GenerateGroupThumbnail(List<(S32Data s32, ObjectTile obj)> objects, int thumbnailSize, bool hasLayer5Setting = false, byte layer5Type = 0)
        {
            if (objects == null || objects.Count == 0)
                return null;

            try
            {
                // 計算所有物件的像素邊界
                // 使用與 RenderS32Block 完全相同的座標計算方式
                int pixelMinX = int.MaxValue, pixelMaxX = int.MinValue;
                int pixelMinY = int.MaxValue, pixelMaxY = int.MinValue;

                // 預先計算每個物件的像素座標，避免重複計算
                var objectPixels = new List<(ObjectTile obj, int px, int py)>(objects.Count);

                foreach (var item in objects)
                {
                    var obj = item.obj;
                    // 使用與 RenderS32Block 完全相同的座標計算
                    int halfX = obj.X / 2;
                    int baseX = -24 * halfX;
                    int baseY = 63 * 12 - 12 * halfX;
                    int px = baseX + obj.X * 24 + obj.Y * 24;
                    int py = baseY + obj.Y * 12;

                    objectPixels.Add((obj, px, py));

                    pixelMinX = Math.Min(pixelMinX, px);
                    pixelMaxX = Math.Max(pixelMaxX, px + 48);  // tile 寬度約 48
                    pixelMinY = Math.Min(pixelMinY, py);
                    pixelMaxY = Math.Max(pixelMaxY, py + 48);  // tile 高度預留空間
                }

                // 計算實際所需的圖片大小（加上邊距）
                int margin = 8;
                int actualWidth = pixelMaxX - pixelMinX + margin * 2;
                int actualHeight = pixelMaxY - pixelMinY + margin * 2;

                // 限制 tempBitmap 大小：縮圖只有 80px，不需要太大的暫存圖
                // 最大 512x512 足夠，超過的會被縮放
                int maxTempSize = 512;
                int tempWidth = Math.Min(Math.Max(actualWidth, 64), maxTempSize);
                int tempHeight = Math.Min(Math.Max(actualHeight, 64), maxTempSize);

                // 如果實際大小超過限制，計算縮放比例
                float preScale = 1.0f;
                if (actualWidth > maxTempSize || actualHeight > maxTempSize)
                {
                    preScale = Math.Min((float)maxTempSize / actualWidth, (float)maxTempSize / actualHeight);
                    tempWidth = (int)(actualWidth * preScale);
                    tempHeight = (int)(actualHeight * preScale);
                }

                // 使用 16bpp 格式與主畫布相同
                Bitmap tempBitmap = new Bitmap(tempWidth, tempHeight, PixelFormat.Format16bppRgb555);

                Rectangle rect = new Rectangle(0, 0, tempBitmap.Width, tempBitmap.Height);
                BitmapData bmpData = tempBitmap.LockBits(rect, ImageLockMode.ReadWrite, tempBitmap.PixelFormat);
                int rowpix = bmpData.Stride;

                unsafe
                {
                    byte* ptr = (byte*)bmpData.Scan0;

                    // 使用 Marshal.Copy 批次填充白色背景 (RGB555: 0x7FFF = 白色)
                    // 建立一行白色資料
                    byte[] whiteLine = new byte[rowpix];
                    for (int x = 0; x < tempWidth; x++)
                    {
                        whiteLine[x * 2] = 0xFF;
                        whiteLine[x * 2 + 1] = 0x7F;
                    }
                    // 批次複製到每一行
                    for (int y = 0; y < tempHeight; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(whiteLine, 0, (IntPtr)(ptr + y * rowpix), rowpix);
                    }

                    // 計算偏移量，讓圖片置中
                    int offsetX = (int)((tempWidth / 2.0f) - ((pixelMinX + pixelMaxX) / 2.0f - pixelMinX) * preScale);
                    int offsetY = (int)((tempHeight / 2.0f) - ((pixelMinY + pixelMaxY) / 2.0f - pixelMinY) * preScale);

                    // 按 Layer 排序後繪製（使用與主畫布完全相同的繪製方式）
                    foreach (var item in objectPixels.OrderBy(o => o.obj.Layer))
                    {
                        var obj = item.obj;

                        // 使用預先計算的座標，套用縮放和偏移
                        int pixelX = (int)((item.px - pixelMinX) * preScale) + offsetX - (int)(tempWidth / 2.0f - actualWidth * preScale / 2.0f);
                        int pixelY = (int)((item.py - pixelMinY) * preScale) + offsetY - (int)(tempHeight / 2.0f - actualHeight * preScale / 2.0f);

                        // 簡化座標計算
                        pixelX = (int)((item.px - pixelMinX + margin) * preScale);
                        pixelY = (int)((item.py - pixelMinY + margin) * preScale);

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
                    // 使用較快的插值模式（縮圖不需要高品質）
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;

                    // 保持比例縮放
                    float scaleX = (float)(thumbnailSize - 4) / tempWidth;
                    float scaleY = (float)(thumbnailSize - 4) / tempHeight;
                    float scale = Math.Min(scaleX, scaleY);
                    int scaledWidth = (int)(tempWidth * scale);
                    int scaledHeight = (int)(tempHeight * scale);
                    int drawX = (thumbnailSize - scaledWidth) / 2;
                    int drawY = (thumbnailSize - scaledHeight) / 2;

                    g.DrawImage(tempBitmap, drawX, drawY, scaledWidth, scaledHeight);

                    // 加邊框（根據 Layer5 設定使用不同顏色）
                    if (hasLayer5Setting)
                    {
                        Pen borderPen = layer5Type == 0 ? _thumbnailBorderPenType0 : _thumbnailBorderPenType1;
                        g.DrawRectangle(borderPen, 1, 1, thumbnailSize - 3, thumbnailSize - 3);
                    }
                    else
                    {
                        g.DrawRectangle(_thumbnailBorderPen, 0, 0, thumbnailSize - 1, thumbnailSize - 1);
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

        // 群組縮圖單擊事件 - 左鍵複製群組（支援多選）
        private void lvGroupThumbnails_MouseClick(object sender, MouseEventArgs e)
        {
            // 只處理左鍵點擊（右鍵由 MouseUp 處理顯示 context menu）
            if (e.Button != MouseButtons.Left)
                return;

            if (lvGroupThumbnails.SelectedItems.Count == 0)
                return;

            // 收集所有選中群組的資訊
            var selectedInfos = new List<GroupThumbnailInfo>();
            foreach (ListViewItem item in lvGroupThumbnails.SelectedItems)
            {
                if (item.Tag is GroupThumbnailInfo info && info.Objects.Count > 0)
                {
                    selectedInfos.Add(info);
                }
            }

            if (selectedInfos.Count > 0)
            {
                CopyMultipleGroupsToClipboard(selectedInfos);
            }
        }

        // 群組選擇變更事件 - 更新過濾狀態
        private void lvGroupThumbnails_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 更新選取的群組 ID 列表
            _editState.SelectedLayer4Groups.Clear();
            foreach (ListViewItem item in lvGroupThumbnails.SelectedItems)
            {
                if (item.Tag is GroupThumbnailInfo info)
                {
                    _editState.SelectedLayer4Groups.Add(info.GroupId);
                }
            }
            _editState.IsFilteringLayer4Groups = _editState.SelectedLayer4Groups.Count > 0;

            // 更新狀態列
            if (_editState.SelectedLayer4Groups.Count > 0)
            {
                string groupIds = string.Join(", ", _editState.SelectedLayer4Groups);
                this.toolStripStatusLabel1.Text = $"已選取 {_editState.SelectedLayer4Groups.Count} 個群組: {groupIds}";
            }
        }

        // 複製多個群組到剪貼簿
        private void CopyMultipleGroupsToClipboard(List<GroupThumbnailInfo> infos)
        {
            _editState.CellClipboard.Clear();

            // 收集所有要複製的物件
            var allObjects = new List<(S32Data s32, ObjectTile obj)>();
            foreach (var info in infos)
            {
                allObjects.AddRange(info.Objects);
            }

            if (allObjects.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "選取的群組內沒有任何物件";
                return;
            }

            // 計算要複製物件的座標範圍（使用 Layer1 座標系統）
            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var (s32, obj) in allObjects)
            {
                int globalLayer1X = s32.SegInfo.nLinBeginX * 2 + obj.X;
                int globalLayer1Y = s32.SegInfo.nLinBeginY + obj.Y;
                if (globalLayer1X < minX) minX = globalLayer1X;
                if (globalLayer1Y < minY) minY = globalLayer1Y;
            }

            // 複製物件（使用 Layer1 座標系統）
            foreach (var (s32, obj) in allObjects)
            {
                int globalLayer1X = s32.SegInfo.nLinBeginX * 2 + obj.X;
                int globalLayer1Y = s32.SegInfo.nLinBeginY + obj.Y;

                var cellData = new CopiedCellData
                {
                    RelativeX = globalLayer1X - minX,
                    RelativeY = globalLayer1Y - minY
                };

                cellData.Layer4Objects.Add(new CopiedObjectTile
                {
                    RelativeX = globalLayer1X - minX,
                    RelativeY = globalLayer1Y - minY,
                    GroupId = obj.GroupId,
                    Layer = obj.Layer,
                    IndexId = obj.IndexId,
                    TileId = obj.TileId,
                    OriginalIndex = s32.Layer4.IndexOf(obj),
                    OriginalLocalLayer1X = obj.X,
                    OriginalLocalY = obj.Y
                });

                _editState.CellClipboard.Add(cellData);
            }

            // 設定剪貼簿狀態
            hasLayer4Clipboard = true;
            _editState.SourceMapId = _document.MapId;

            // 顯示訊息
            string groupIds = string.Join(", ", infos.Select(i => i.GroupId));
            this.toolStripStatusLabel1.Text = $"已複製 {infos.Count} 個群組 ({groupIds})，共 {allObjects.Count} 個物件 (選取位置後按 Ctrl+V 貼上)";
        }

        // 複製單一群組到剪貼簿（保留供相容）
        private void CopyGroupToClipboard(GroupThumbnailInfo info)
        {
            CopyMultipleGroupsToClipboard(new List<GroupThumbnailInfo> { info });
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

            // 計算世界座標
            int worldX = mx + localBaseX + obj.X * 24 + obj.Y * 24;
            int worldY = my + localBaseY + obj.Y * 12;

            // 捲動到該位置（世界座標）
            int viewportWidthWorld = (int)(s32MapPanel.Width / s32ZoomLevel);
            int viewportHeightWorld = (int)(s32MapPanel.Height / s32ZoomLevel);
            int scrollX = worldX - viewportWidthWorld / 2;
            int scrollY = worldY - viewportHeightWorld / 2;

            int maxScrollX = Math.Max(0, _viewState.MapWidth - viewportWidthWorld);
            int maxScrollY = Math.Max(0, _viewState.MapHeight - viewportHeightWorld);
            scrollX = Math.Max(0, Math.Min(scrollX, maxScrollX));
            scrollY = Math.Max(0, Math.Min(scrollY, maxScrollY));

            _viewState.SetScrollSilent(scrollX, scrollY);

            // 高亮顯示該格子
            _editState.HighlightedS32Data = s32Data;
            _editState.HighlightedCellX = obj.X;
            _editState.HighlightedCellY = obj.Y;

            s32PictureBox.Invalidate();
            UpdateMiniMap();

            this.toolStripStatusLabel1.Text = $"跳轉到群組 {info.GroupId}，位置 ({obj.X}, {obj.Y})，共 {info.Objects.Count} 個物件";
        }

        // 群組縮圖雙擊事件 - 顯示放大預覽
        private void lvGroupThumbnails_DoubleClick(object sender, EventArgs e)
        {
            if (lvGroupThumbnails.SelectedItems.Count == 0)
                return;

            var item = lvGroupThumbnails.SelectedItems[0];
            if (item.Tag is GroupThumbnailInfo info && info.Objects.Count > 0)
            {
                ShowGroupPreviewDialog(info);
            }
        }

        // 群組縮圖右鍵選單（支援多選）
        private void lvGroupThumbnails_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (lvGroupThumbnails.SelectedItems.Count == 0)
                return;

            // 收集所有選中群組的資訊
            var selectedInfos = new List<GroupThumbnailInfo>();
            int totalObjects = 0;
            foreach (ListViewItem item in lvGroupThumbnails.SelectedItems)
            {
                if (item.Tag is GroupThumbnailInfo info && info.Objects.Count > 0)
                {
                    selectedInfos.Add(info);
                    totalObjects += info.Objects.Count;
                }
            }

            if (selectedInfos.Count == 0)
                return;

            // 建立右鍵選單
            ContextMenuStrip menu = new ContextMenuStrip();

            if (selectedInfos.Count == 1)
            {
                // 單選模式 - 顯示原有選項
                var info = selectedInfos[0];

                ToolStripMenuItem copyItem = new ToolStripMenuItem($"複製群組 {info.GroupId}");
                copyItem.Click += (s, ev) => CopyGroupToClipboard(info);

                ToolStripMenuItem gotoItem = new ToolStripMenuItem("跳轉到位置");
                gotoItem.Click += (s, ev) => JumpToGroupLocation(info);

                ToolStripMenuItem deleteItem = new ToolStripMenuItem($"刪除群組 {info.GroupId} ({info.Objects.Count} 個物件)");
                deleteItem.Click += (s, ev) => DeleteGroupFromMap(info);

                menu.Items.Add(copyItem);
                menu.Items.Add(gotoItem);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(deleteItem);

                // Layer5 設定選項（僅在透明編輯模式或有選取格子時顯示）
                if (_editState.IsLayer5EditMode && _editState.SelectedCells.Count > 0)
                {
                    menu.Items.Add(new ToolStripSeparator());

                    ToolStripMenuItem setTransparentItem = new ToolStripMenuItem($"設定群組 {info.GroupId} 為透明 (Type=0)");
                    setTransparentItem.Click += (s, ev) => SetGroupLayer5Setting(new List<GroupThumbnailInfo> { info }, 0);

                    ToolStripMenuItem setDisappearItem = new ToolStripMenuItem($"設定群組 {info.GroupId} 為消失 (Type=1)");
                    setDisappearItem.Click += (s, ev) => SetGroupLayer5Setting(new List<GroupThumbnailInfo> { info }, 1);

                    ToolStripMenuItem removeLayer5Item = new ToolStripMenuItem($"移除群組 {info.GroupId} 的 Layer5 設定");
                    removeLayer5Item.Click += (s, ev) => RemoveGroupLayer5Setting(new List<GroupThumbnailInfo> { info });

                    menu.Items.Add(setTransparentItem);
                    menu.Items.Add(setDisappearItem);
                    menu.Items.Add(removeLayer5Item);
                }
            }
            else
            {
                // 多選模式 - 顯示批次操作選項
                string groupIds = string.Join(", ", selectedInfos.Select(i => i.GroupId));

                ToolStripMenuItem copyItem = new ToolStripMenuItem($"複製 {selectedInfos.Count} 個群組 ({totalObjects} 個物件)");
                copyItem.Click += (s, ev) => CopyMultipleGroupsToClipboard(selectedInfos);

                ToolStripMenuItem deleteItem = new ToolStripMenuItem($"刪除 {selectedInfos.Count} 個群組 ({totalObjects} 個物件)");
                deleteItem.Click += (s, ev) => DeleteMultipleGroupsFromMap(selectedInfos);

                menu.Items.Add(copyItem);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(deleteItem);

                // Layer5 設定選項（僅在透明編輯模式或有選取格子時顯示）
                if (_editState.IsLayer5EditMode && _editState.SelectedCells.Count > 0)
                {
                    menu.Items.Add(new ToolStripSeparator());

                    ToolStripMenuItem setTransparentItem = new ToolStripMenuItem($"設定 {selectedInfos.Count} 個群組為透明 (Type=0)");
                    setTransparentItem.Click += (s, ev) => SetGroupLayer5Setting(selectedInfos, 0);

                    ToolStripMenuItem setDisappearItem = new ToolStripMenuItem($"設定 {selectedInfos.Count} 個群組為消失 (Type=1)");
                    setDisappearItem.Click += (s, ev) => SetGroupLayer5Setting(selectedInfos, 1);

                    ToolStripMenuItem removeLayer5Item = new ToolStripMenuItem($"移除 {selectedInfos.Count} 個群組的 Layer5 設定");
                    removeLayer5Item.Click += (s, ev) => RemoveGroupLayer5Setting(selectedInfos);

                    menu.Items.Add(setTransparentItem);
                    menu.Items.Add(setDisappearItem);
                    menu.Items.Add(removeLayer5Item);
                }
            }

            menu.Show(lvGroupThumbnails, e.Location);
        }

        // 從地圖刪除多個群組
        private void DeleteMultipleGroupsFromMap(List<GroupThumbnailInfo> infos)
        {
            // 收集所有要刪除的物件
            var allObjects = new List<(S32Data s32, ObjectTile obj)>();
            foreach (var info in infos)
            {
                allObjects.AddRange(info.Objects);
            }

            if (allObjects.Count == 0)
            {
                MessageBox.Show("選取的群組內沒有物件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string groupIds = string.Join(", ", infos.Select(i => i.GroupId));

            // 確認刪除
            DialogResult result = MessageBox.Show(
                $"確定要刪除 {infos.Count} 個群組嗎？\n" +
                $"群組 ID: {groupIds}\n" +
                $"這將移除選取區域內的 {allObjects.Count} 個 Layer4 物件。",
                "確認刪除多個群組",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            // 建立 Undo 記錄
            var undoAction = new UndoAction
            {
                Description = $"刪除 {infos.Count} 個群組 ({allObjects.Count} 個物件)"
            };

            // 刪除物件
            int deletedCount = 0;
            foreach (var (s32Data, obj) in allObjects)
            {
                // 記錄到 Undo
                undoAction.RemovedObjects.Add(new UndoObjectInfo
                {
                    S32FilePath = s32Data.FilePath,
                    GameX = s32Data.SegInfo.nLinBeginX + obj.X / 2,
                    GameY = s32Data.SegInfo.nLinBeginY + obj.Y,
                    LocalX = obj.X,
                    LocalY = obj.Y,
                    GroupId = obj.GroupId,
                    Layer = obj.Layer,
                    IndexId = obj.IndexId,
                    TileId = obj.TileId
                });

                s32Data.Layer4.Remove(obj);
                s32Data.IsModified = true;
                deletedCount++;
            }

            // 記錄 Undo
            PushUndoAction(undoAction);

            // 清除已刪除群組的選取狀態（避免渲染時過濾不到物件）
            foreach (var info in infos)
            {
                _editState.SelectedLayer4Groups.Remove(info.GroupId);
            }
            _editState.IsFilteringLayer4Groups = _editState.SelectedLayer4Groups.Count > 0;

            // 清除快取並重新渲染
            ClearS32BlockCache();
            RenderS32Map();

            // 更新 Layer5 異常檢查按鈕
            UpdateLayer5InvalidButton();

            // 更新群組縮圖列表
            if (_editState.SelectedCells.Count > 0)
            {
                UpdateGroupThumbnailsList(_editState.SelectedCells);
            }
            else
            {
                UpdateGroupThumbnailsList();
            }

            this.toolStripStatusLabel1.Text = $"已刪除 {infos.Count} 個群組，共 {deletedCount} 個物件";
        }

        // 從地圖刪除群組（只刪除 info.Objects 中的物件，即選取區域內的物件）
        private void DeleteGroupFromMap(GroupThumbnailInfo info)
        {
            int groupId = info.GroupId;

            // 使用 info.Objects（已經是選取區域的交集）
            if (info.Objects == null || info.Objects.Count == 0)
            {
                MessageBox.Show($"群組 {groupId} 在選取區域內沒有物件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int totalCount = info.Objects.Count;

            // 確認刪除
            DialogResult result = MessageBox.Show(
                $"確定要刪除群組 {groupId} 嗎？\n" +
                $"這將移除選取區域內的 {totalCount} 個 Layer4 物件。",
                "確認刪除群組",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            // 建立 Undo 記錄
            var undoAction = new UndoAction
            {
                Description = $"刪除群組 {groupId} ({totalCount} 個物件)"
            };

            // 只刪除 info.Objects 中的物件（選取區域內的物件）
            int deletedCount = 0;
            foreach (var (s32Data, obj) in info.Objects)
            {
                // 記錄到 Undo
                undoAction.RemovedObjects.Add(new UndoObjectInfo
                {
                    S32FilePath = s32Data.FilePath,
                    GameX = s32Data.SegInfo.nLinBeginX + obj.X / 2,
                    GameY = s32Data.SegInfo.nLinBeginY + obj.Y,
                    LocalX = obj.X,
                    LocalY = obj.Y,
                    GroupId = obj.GroupId,
                    Layer = obj.Layer,
                    IndexId = obj.IndexId,
                    TileId = obj.TileId
                });

                // 刪除物件
                if (s32Data.Layer4.Remove(obj))
                {
                    deletedCount++;
                    s32Data.IsModified = true;
                }
            }

            // 儲存 Undo 記錄
            if (undoAction.RemovedObjects.Count > 0)
            {
                PushUndoAction(undoAction);
            }

            // 清除已刪除群組的選取狀態（避免渲染時過濾不到物件）
            _editState.SelectedLayer4Groups.Remove(groupId);
            _editState.IsFilteringLayer4Groups = _editState.SelectedLayer4Groups.Count > 0;

            // 清除快取並重新渲染（RenderS32Map 內部會在完成後自動 Invalidate）
            ClearS32BlockCache();
            RenderS32Map();

            // 更新 Layer5 異常檢查按鈕
            UpdateLayer5InvalidButton();

            // 更新群組縮圖列表（保持選取區域的交集）
            if (_editState.SelectedCells != null && _editState.SelectedCells.Count > 0)
            {
                UpdateGroupThumbnailsList(_editState.SelectedCells);
            }
            else
            {
                UpdateGroupThumbnailsList();
            }

            this.toolStripStatusLabel1.Text = $"已刪除群組 {groupId}，共 {deletedCount} 個物件";
        }

        // 設定群組的 Layer5 設定（透明或消失）
        private void SetGroupLayer5Setting(List<GroupThumbnailInfo> infos, byte type)
        {
            if (_editState.SelectedCells.Count == 0)
            {
                MessageBox.Show("請先選取格子", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int addedCount = 0;
            int updatedCount = 0;
            var groupIds = infos.Select(i => i.GroupId).ToHashSet();

            foreach (var cell in _editState.SelectedCells)
            {
                var s32Data = cell.S32Data;
                // Layer5 的 X 是 0-127 (Layer1 座標)，Y 是 0-63
                // 一個遊戲格子對應兩個 Layer1 X 座標（LocalX 和 LocalX+1）
                int layer5X1 = cell.LocalX;
                int layer5X2 = cell.LocalX + 1;
                int layer5Y = cell.LocalY;

                foreach (var groupId in groupIds)
                {
                    // 檢查兩個 X 座標是否已存在 Layer5 設定
                    for (int layer5X = layer5X1; layer5X <= layer5X2 && layer5X < 128; layer5X++)
                    {
                        var existingItem = s32Data.Layer5.FirstOrDefault(l =>
                            l.X == layer5X && l.Y == layer5Y && l.ObjectIndex == groupId);

                        if (existingItem != null)
                        {
                            // 更新現有項目
                            if (existingItem.Type != type)
                            {
                                existingItem.Type = type;
                                updatedCount++;
                                s32Data.IsModified = true;
                            }
                        }
                        else
                        {
                            // 新增 Layer5 項目
                            s32Data.Layer5.Add(new Layer5Item
                            {
                                X = (byte)layer5X,
                                Y = (byte)layer5Y,
                                ObjectIndex = (ushort)groupId,
                                Type = type
                            });
                            addedCount++;
                            s32Data.IsModified = true;
                        }
                    }
                }
            }

            // 重新渲染
            ClearS32BlockCache();
            RenderS32Map();

            // 更新 Layer5 異常檢查按鈕
            UpdateLayer5InvalidButton();

            // 更新群組縮圖列表（使用第一個選取格子的資訊）
            if (_editState.SelectedCells.Count > 0)
            {
                var firstCell = _editState.SelectedCells[0];
                UpdateNearbyGroupThumbnails(firstCell.S32Data, firstCell.LocalX, firstCell.LocalY, 10);
            }

            string typeStr = type == 0 ? "透明" : "消失";
            string message = $"已設定 {infos.Count} 個群組為{typeStr}";
            if (addedCount > 0) message += $"，新增 {addedCount} 筆";
            if (updatedCount > 0) message += $"，更新 {updatedCount} 筆";
            this.toolStripStatusLabel1.Text = message;
        }

        // 移除群組的 Layer5 設定
        private void RemoveGroupLayer5Setting(List<GroupThumbnailInfo> infos)
        {
            if (_editState.SelectedCells.Count == 0)
            {
                MessageBox.Show("請先選取格子", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int removedCount = 0;
            var groupIds = infos.Select(i => i.GroupId).ToHashSet();

            foreach (var cell in _editState.SelectedCells)
            {
                var s32Data = cell.S32Data;
                // Layer5 的 X 是 0-127 (Layer1 座標)，Y 是 0-63
                // 一個遊戲格子對應兩個 Layer1 X 座標（LocalX 和 LocalX+1）
                int layer5X1 = cell.LocalX;
                int layer5X2 = cell.LocalX + 1;
                int layer5Y = cell.LocalY;

                // 找出並移除符合條件的 Layer5 項目（兩個 X 座標都要檢查）
                var itemsToRemove = s32Data.Layer5
                    .Where(l => (l.X == layer5X1 || l.X == layer5X2) && l.Y == layer5Y && groupIds.Contains(l.ObjectIndex))
                    .ToList();

                foreach (var item in itemsToRemove)
                {
                    s32Data.Layer5.Remove(item);
                    removedCount++;
                    s32Data.IsModified = true;
                }
            }

            // 重新渲染
            ClearS32BlockCache();
            RenderS32Map();

            // 更新 Layer5 異常檢查按鈕
            UpdateLayer5InvalidButton();

            // 更新群組縮圖列表（使用第一個選取格子的資訊）
            if (_editState.SelectedCells.Count > 0)
            {
                var firstCell = _editState.SelectedCells[0];
                UpdateNearbyGroupThumbnails(firstCell.S32Data, firstCell.LocalX, firstCell.LocalY, 10);
            }

            this.toolStripStatusLabel1.Text = $"已移除 {removedCount} 筆 Layer5 設定";
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
            if (_editState.SelectedCells.Count > 0)
            {
                var firstCell = _editState.SelectedCells[0];
                ShowCellLayersDialog(firstCell.LocalX, firstCell.LocalY);
            }
            else
            {
                this.toolStripStatusLabel1.Text = "請先使用左鍵選取格子";
            }
        }

        private void btnToolReplaceTile_Click(object sender, EventArgs e)
        {
            // 檢查是否已載入地圖
            if (_document.S32Files.Count == 0)
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

                foreach (var kvp in _document.S32Files)
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

                foreach (var kvp in _document.S32Files)
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
            if (_document.S32Files.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 計算總共有多少個第七層資料
            int totalLayer7Count = 0;
            int affectedS32Count = 0;
            foreach (var s32Data in _document.S32Files.Values)
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
            foreach (var kvp in _document.S32Files)
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
            if (_document.S32Files.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 如果有選取區域，直接批量清除
            if (_editState.SelectedCells.Count > 0)
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
            clearForm.Text = $"批量清除格子資料 - 已選取 {_editState.SelectedCells.Count} 格";
            clearForm.Size = new Size(320, 320);
            clearForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            clearForm.StartPosition = FormStartPosition.CenterParent;
            clearForm.MaximizeBox = false;
            clearForm.MinimizeBox = false;

            // 選擇要清除的層
            Label lblLayers = new Label();
            lblLayers.Text = $"選擇要清除的層 (共 {_editState.SelectedCells.Count} 格):";
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
                foreach (var cell in _editState.SelectedCells)
                {
                    int globalX = cell.S32Data.SegInfo.nLinBeginX + cell.LocalX / 2;
                    int globalY = cell.S32Data.SegInfo.nLinBeginY + cell.LocalY;
                    selectedGlobalCells.Add((globalX, globalY));
                }

                foreach (var cell in _editState.SelectedCells)
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

                    // 清除第5層 - Layer5.X 是 0-127 (Layer1 座標)
                    // 一個遊戲格子對應兩個 Layer1 X 座標，只在偶數 X 時處理避免重複
                    if (chkL5.Checked && layer1X % 2 == 0)
                    {
                        totalL5 += s32Data.Layer5.RemoveAll(item => (item.X == layer1X || item.X == layer1X + 1) && item.Y == localY);
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
                    foreach (var s32Data in _document.S32Files.Values)
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
                int clearedCellCount = _editState.SelectedCells.Count;
                _editState.SelectedCells.Clear();
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
            if (_editState.SelectedGameX >= 0) txtX.Text = _editState.SelectedGameX.ToString();
            clearForm.Controls.Add(txtX);

            Label lblY = new Label();
            lblY.Text = "Y:";
            lblY.Location = new Point(150, 50);
            lblY.Size = new Size(30, 20);
            clearForm.Controls.Add(lblY);

            TextBox txtY = new TextBox();
            txtY.Location = new Point(180, 48);
            txtY.Size = new Size(80, 20);
            if (_editState.SelectedGameY >= 0) txtY.Text = _editState.SelectedGameY.ToString();
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
                foreach (var s32Data in _document.S32Files.Values)
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
            if (_document.S32Files.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 收集有 Layer6 資料的 S32
            List<(string filePath, string fileName, List<int> items)> s32WithL6 =
                new List<(string, string, List<int>)>();

            foreach (var kvp in _document.S32Files)
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
                foreach (var kvp in _document.S32Files)
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
                    if (_document.S32Files.TryGetValue(kvp.Key, out S32Data s32Data))
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
                    if (_document.S32Files.TryGetValue(filePath, out S32Data s32Data))
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
            if (_document.S32Files.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 收集有 Layer8 資料的 S32
            List<(string filePath, string fileName, int count, List<Layer8Item> items)> s32WithL8 =
                new List<(string, string, int, List<Layer8Item>)>();

            foreach (var kvp in _document.S32Files)
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
            resultForm.Size = new Size(850, 650);
            resultForm.FormBorderStyle = FormBorderStyle.Sizable;
            resultForm.StartPosition = FormStartPosition.CenterParent;

            int totalItems = s32WithL8.Sum(x => x.count);
            int extendedCount = _document.S32Files.Values.Count(s => s.Layer8HasExtendedData);
            Label lblSummary = new Label();
            lblSummary.Text = $"共 {s32WithL8.Count} 個 S32 有 Layer8 資料，總計 {totalItems} 項。{extendedCount} 個 S32 使用擴展格式。";
            lblSummary.Location = new Point(10, 10);
            lblSummary.Size = new Size(810, 20);
            resultForm.Controls.Add(lblSummary);

            // 預先宣告 ListView（因為按鈕事件會用到）
            ListView lvItems = new ListView();
            lvItems.Location = new Point(10, 115);
            lvItems.Size = new Size(810, 300);
            lvItems.Font = new Font("Consolas", 9);
            lvItems.View = View.Details;
            lvItems.FullRowSelect = true;
            lvItems.CheckBoxes = true;
            lvItems.Columns.Add("S32 檔案", 120);
            lvItems.Columns.Add("擴展", 50);
            lvItems.Columns.Add("SprId", 70);
            lvItems.Columns.Add("X", 60);
            lvItems.Columns.Add("Y", 60);
            lvItems.Columns.Add("ExtData", 80);

            // 擴展格式設定區
            GroupBox gbExtended = new GroupBox();
            gbExtended.Text = "擴展格式設定";
            gbExtended.Location = new Point(10, 35);
            gbExtended.Size = new Size(810, 75);

            Label lblExtendedInfo = new Label();
            lblExtendedInfo.Text = "擴展格式 (Extended) 表示 Layer8 項目包含額外 4 bytes 資料。選擇 S32 檔案後可切換其擴展格式設定：";
            lblExtendedInfo.Location = new Point(10, 18);
            lblExtendedInfo.Size = new Size(550, 20);
            gbExtended.Controls.Add(lblExtendedInfo);

            ComboBox cmbS32Extended = new ComboBox();
            cmbS32Extended.Location = new Point(10, 42);
            cmbS32Extended.Size = new Size(200, 23);
            cmbS32Extended.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var kvp in _document.S32Files)
            {
                string fileName = Path.GetFileName(kvp.Key);
                string extMark = kvp.Value.Layer8HasExtendedData ? " [擴展]" : "";
                cmbS32Extended.Items.Add(new { FilePath = kvp.Key, Display = $"{fileName}{extMark}" });
            }
            cmbS32Extended.DisplayMember = "Display";
            if (cmbS32Extended.Items.Count > 0) cmbS32Extended.SelectedIndex = 0;
            gbExtended.Controls.Add(cmbS32Extended);

            Label lblCurrentStatus = new Label();
            lblCurrentStatus.Location = new Point(220, 45);
            lblCurrentStatus.Size = new Size(150, 20);
            lblCurrentStatus.Text = "目前：未選擇";
            gbExtended.Controls.Add(lblCurrentStatus);

            Button btnSetExtended = new Button();
            btnSetExtended.Text = "設為擴展";
            btnSetExtended.Location = new Point(380, 40);
            btnSetExtended.Size = new Size(90, 28);
            btnSetExtended.Click += (s, args) =>
            {
                if (cmbS32Extended.SelectedItem == null) return;
                dynamic selected = cmbS32Extended.SelectedItem;
                string filePath = selected.FilePath;
                if (_document.S32Files.TryGetValue(filePath, out S32Data s32Data))
                {
                    s32Data.Layer8HasExtendedData = true;
                    s32Data.IsModified = true;
                    lblCurrentStatus.Text = "目前：擴展格式";
                    // 更新 ComboBox 顯示
                    int idx = cmbS32Extended.SelectedIndex;
                    cmbS32Extended.Items[idx] = new { FilePath = filePath, Display = $"{Path.GetFileName(filePath)} [擴展]" };
                    cmbS32Extended.SelectedIndex = idx;
                    // 更新 ListView 中該 S32 的項目
                    foreach (ListViewItem lvi in lvItems.Items)
                    {
                        var (lvFilePath, lvItem) = ((string, Layer8Item))lvi.Tag;
                        if (lvFilePath == filePath)
                        {
                            lvi.SubItems[1].Text = "是";  // 擴展欄位
                        }
                    }
                    // 更新摘要
                    int newExtCount = _document.S32Files.Values.Count(x => x.Layer8HasExtendedData);
                    lblSummary.Text = $"共 {s32WithL8.Count} 個 S32 有 Layer8 資料，總計 {totalItems} 項。{newExtCount} 個 S32 使用擴展格式。";
                    MessageBox.Show($"已將 {Path.GetFileName(filePath)} 設為擴展格式。\n請記得儲存 S32 檔案。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            gbExtended.Controls.Add(btnSetExtended);

            Button btnSetNormal = new Button();
            btnSetNormal.Text = "設為一般";
            btnSetNormal.Location = new Point(480, 40);
            btnSetNormal.Size = new Size(90, 28);
            btnSetNormal.Click += (s, args) =>
            {
                if (cmbS32Extended.SelectedItem == null) return;
                dynamic selected = cmbS32Extended.SelectedItem;
                string filePath = selected.FilePath;
                if (_document.S32Files.TryGetValue(filePath, out S32Data s32Data))
                {
                    s32Data.Layer8HasExtendedData = false;
                    // 清除所有 Layer8 項目的 ExtendedData
                    foreach (var item in s32Data.Layer8)
                    {
                        item.ExtendedData = 0;
                    }
                    s32Data.IsModified = true;
                    lblCurrentStatus.Text = "目前：一般格式";
                    // 更新 ComboBox 顯示
                    int idx = cmbS32Extended.SelectedIndex;
                    cmbS32Extended.Items[idx] = new { FilePath = filePath, Display = Path.GetFileName(filePath) };
                    cmbS32Extended.SelectedIndex = idx;
                    // 更新 ListView 中該 S32 的項目
                    foreach (ListViewItem lvi in lvItems.Items)
                    {
                        var (lvFilePath, lvItem) = ((string, Layer8Item))lvi.Tag;
                        if (lvFilePath == filePath)
                        {
                            lvi.SubItems[1].Text = "";  // 擴展欄位
                            lvi.SubItems[5].Text = "0"; // ExtData 欄位
                        }
                    }
                    // 更新摘要
                    int newExtCount = _document.S32Files.Values.Count(x => x.Layer8HasExtendedData);
                    lblSummary.Text = $"共 {s32WithL8.Count} 個 S32 有 Layer8 資料，總計 {totalItems} 項。{newExtCount} 個 S32 使用擴展格式。";
                    MessageBox.Show($"已將 {Path.GetFileName(filePath)} 設為一般格式，並清除 {s32Data.Layer8.Count} 個項目的 ExtendedData。\n請記得儲存 S32 檔案。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            gbExtended.Controls.Add(btnSetNormal);

            Button btnResetAllExtended = new Button();
            btnResetAllExtended.Text = "全部重設為一般";
            btnResetAllExtended.Location = new Point(580, 40);
            btnResetAllExtended.Size = new Size(120, 28);
            btnResetAllExtended.BackColor = Color.LightYellow;
            btnResetAllExtended.Click += (s, args) =>
            {
                int currentExtCount = _document.S32Files.Values.Count(x => x.Layer8HasExtendedData);
                if (currentExtCount == 0)
                {
                    MessageBox.Show("沒有使用擴展格式的 S32 檔案。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                var confirmResult = MessageBox.Show(
                    $"確定要將所有 {currentExtCount} 個 S32 檔案的 Layer8 重設為一般格式嗎？\n\n注意：這會清除所有 ExtendedData 欄位的資料。",
                    "確認重設",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (confirmResult != DialogResult.Yes) return;

                int clearedItemCount = 0;
                foreach (var s32Data in _document.S32Files.Values)
                {
                    if (s32Data.Layer8HasExtendedData)
                    {
                        s32Data.Layer8HasExtendedData = false;
                        // 清除所有 Layer8 項目的 ExtendedData
                        foreach (var item in s32Data.Layer8)
                        {
                            item.ExtendedData = 0;
                            clearedItemCount++;
                        }
                        s32Data.IsModified = true;
                    }
                }
                // 重新填入 ComboBox
                cmbS32Extended.Items.Clear();
                foreach (var kvp in _document.S32Files)
                {
                    cmbS32Extended.Items.Add(new { FilePath = kvp.Key, Display = Path.GetFileName(kvp.Key) });
                }
                if (cmbS32Extended.Items.Count > 0) cmbS32Extended.SelectedIndex = 0;
                lblCurrentStatus.Text = "目前：一般格式";
                // 更新 ListView 中所有項目的擴展欄位
                foreach (ListViewItem lvi in lvItems.Items)
                {
                    lvi.SubItems[1].Text = "";  // 擴展欄位
                    lvi.SubItems[5].Text = "0"; // ExtData 欄位
                }
                lblSummary.Text = $"共 {s32WithL8.Count} 個 S32 有 Layer8 資料，總計 {totalItems} 項。0 個 S32 使用擴展格式。";
                MessageBox.Show($"已將 {currentExtCount} 個 S32 檔案重設為一般格式，並清除 {clearedItemCount} 個項目的 ExtendedData。\n請記得儲存 S32 檔案。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            gbExtended.Controls.Add(btnResetAllExtended);

            cmbS32Extended.SelectedIndexChanged += (s, args) =>
            {
                if (cmbS32Extended.SelectedItem == null) return;
                dynamic selected = cmbS32Extended.SelectedItem;
                string filePath = selected.FilePath;
                if (_document.S32Files.TryGetValue(filePath, out S32Data s32Data))
                {
                    lblCurrentStatus.Text = s32Data.Layer8HasExtendedData ? "目前：擴展格式" : "目前：一般格式";
                }
            };
            // 初始顯示
            if (cmbS32Extended.Items.Count > 0)
            {
                dynamic firstItem = cmbS32Extended.Items[0];
                if (_document.S32Files.TryGetValue(firstItem.FilePath, out S32Data firstS32))
                {
                    lblCurrentStatus.Text = firstS32.Layer8HasExtendedData ? "目前：擴展格式" : "目前：一般格式";
                }
            }
            resultForm.Controls.Add(gbExtended);

            // 填入 ListView 資料
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
                    bool hasExtended = _document.S32Files.TryGetValue(filePath, out S32Data s32) && s32.Layer8HasExtendedData;
                    foreach (var item in items)
                    {
                        ListViewItem lvi = new ListViewItem(fileName);
                        lvi.SubItems.Add(hasExtended ? "是" : "");
                        lvi.SubItems.Add(item.SprId.ToString());
                        lvi.SubItems.Add(item.X.ToString());
                        lvi.SubItems.Add(item.Y.ToString());
                        lvi.SubItems.Add(item.ExtendedData.ToString());
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
            gbEdit.Size = new Size(810, 80);

            Label lblSprId = new Label { Text = "SprId:", Location = new Point(10, 28), Size = new Size(45, 20) };
            TextBox txtSprId = new TextBox { Location = new Point(60, 25), Size = new Size(80, 23) };
            Label lblX = new Label { Text = "X:", Location = new Point(155, 28), Size = new Size(20, 20) };
            TextBox txtX = new TextBox { Location = new Point(175, 25), Size = new Size(80, 23) };
            Label lblY = new Label { Text = "Y:", Location = new Point(270, 28), Size = new Size(20, 20) };
            TextBox txtY = new TextBox { Location = new Point(290, 25), Size = new Size(80, 23) };
            Label lblExtData = new Label { Text = "ExtData:", Location = new Point(385, 28), Size = new Size(55, 20) };
            TextBox txtExtData = new TextBox { Location = new Point(445, 25), Size = new Size(80, 23) };

            Button btnApplyEdit = new Button();
            btnApplyEdit.Text = "套用修改";
            btnApplyEdit.Location = new Point(545, 22);
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
                    !int.TryParse(txtExtData.Text, out int newExtData))
                {
                    MessageBox.Show("請輸入有效的數值", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 更新資料
                item.SprId = newSprId;
                item.X = newX;
                item.Y = newY;
                item.ExtendedData = newExtData;

                // 更新 ListView 顯示 (索引: 0=檔案, 1=擴展, 2=SprId, 3=X, 4=Y, 5=ExtData)
                lvi.SubItems[2].Text = item.SprId.ToString();
                lvi.SubItems[3].Text = item.X.ToString();
                lvi.SubItems[4].Text = item.Y.ToString();
                lvi.SubItems[5].Text = item.ExtendedData.ToString();

                // 標記已修改
                if (_document.S32Files.TryGetValue(filePath, out S32Data s32Data))
                {
                    s32Data.IsModified = true;
                }

                MessageBox.Show("已套用修改。請記得儲存 S32 檔案。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // 新增項目按鈕
            Button btnAddNew = new Button();
            btnAddNew.Text = "新增";
            btnAddNew.Location = new Point(635, 22);
            btnAddNew.Size = new Size(60, 28);
            btnAddNew.Click += (s, args) =>
            {
                if (_document.S32Files.Count == 0)
                {
                    MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (!ushort.TryParse(txtSprId.Text, out ushort newSprId) ||
                    !ushort.TryParse(txtX.Text, out ushort newX) ||
                    !ushort.TryParse(txtY.Text, out ushort newY) ||
                    !int.TryParse(txtExtData.Text, out int newExtData))
                {
                    MessageBox.Show("請輸入有效的數值", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 選擇要加入的 S32 檔案
                var s32Files = _document.S32Files.Keys.Select(k => Path.GetFileName(k)).ToArray();
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
                    string selectedFilePath = _document.S32Files.Keys.FirstOrDefault(k => Path.GetFileName(k) == selectedFileName);

                    if (selectedFilePath != null && _document.S32Files.TryGetValue(selectedFilePath, out S32Data s32Data))
                    {
                        Layer8Item newItem = new Layer8Item
                        {
                            SprId = newSprId,
                            X = newX,
                            Y = newY,
                            ExtendedData = newExtData
                        };
                        s32Data.Layer8.Add(newItem);
                        s32Data.IsModified = true;

                        // 更新 ListView (索引: 0=檔案, 1=擴展, 2=SprId, 3=X, 4=Y, 5=ExtData)
                        ListViewItem lvi = new ListViewItem(selectedFileName);
                        lvi.SubItems.Add(s32Data.Layer8HasExtendedData ? "是" : "");
                        lvi.SubItems.Add(newItem.SprId.ToString());
                        lvi.SubItems.Add(newItem.X.ToString());
                        lvi.SubItems.Add(newItem.Y.ToString());
                        lvi.SubItems.Add(newItem.ExtendedData.ToString());
                        lvi.Tag = (selectedFilePath, newItem);
                        lvItems.Items.Add(lvi);
                        itemInfoList.Add((selectedFilePath, newItem));

                        MessageBox.Show($"已新增 Layer8 項目到 {selectedFileName}。\n\n請記得儲存 S32 檔案。", "完成",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };

            gbEdit.Controls.AddRange(new Control[] { lblSprId, txtSprId, lblX, txtX, lblY, txtY, lblExtData, txtExtData, btnApplyEdit, btnAddNew });
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
                    txtExtData.Text = item.ExtendedData.ToString();
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
                    if (_document.S32Files.TryGetValue(kvp.Key, out S32Data s32Data))
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
            btnClearAll.Location = new Point(140, 555);
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
                    if (_document.S32Files.TryGetValue(filePath, out S32Data s32Data))
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
            btnClose.Location = new Point(730, 555);
            btnClose.Size = new Size(90, 35);
            btnClose.Click += (s, args) => resultForm.Close();
            resultForm.Controls.Add(btnClose);

            resultForm.Resize += (s, args) =>
            {
                gbExtended.Size = new Size(resultForm.ClientSize.Width - 20, 75);
                lvItems.Size = new Size(resultForm.ClientSize.Width - 20, resultForm.ClientSize.Height - 260);
                gbEdit.Location = new Point(10, resultForm.ClientSize.Height - 135);
                gbEdit.Size = new Size(resultForm.ClientSize.Width - 20, 80);
                btnSelectAll.Location = new Point(10, resultForm.ClientSize.Height - 45);
                btnDeselectAll.Location = new Point(100, resultForm.ClientSize.Height - 45);
                btnClearSelected.Location = new Point(200, resultForm.ClientSize.Height - 45);
                btnClearAll.Location = new Point(330, resultForm.ClientSize.Height - 45);
                btnClose.Location = new Point(resultForm.ClientSize.Width - 100, resultForm.ClientSize.Height - 45);
            };

            resultForm.ShowDialog();
        }

        // 查看與編輯第一層（地板圖塊）資料
        private void btnToolCheckL1_Click(object sender, EventArgs e)
        {
            if (_document.S32Files.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 顯示結果
            Form resultForm = new Form();
            resultForm.Text = $"L1 檢查與編輯 - {_document.S32Files.Count} 個 S32 檔案";
            resultForm.Size = new Size(750, 620);
            resultForm.FormBorderStyle = FormBorderStyle.Sizable;
            resultForm.StartPosition = FormStartPosition.CenterParent;

            Label lblSummary = new Label();
            lblSummary.Text = $"共 {_document.S32Files.Count} 個 S32 檔案。選擇 S32 檔案後可編輯指定座標的 Layer1 資料：";
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
            foreach (var kvp in _document.S32Files)
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

            // 批量刪除區域
            GroupBox gbDelete = new GroupBox();
            gbDelete.Text = "批量刪除（將 TileId 設為 0）";
            gbDelete.Location = new Point(10, 290);
            gbDelete.Size = new Size(710, 80);

            Label lblDelTileId = new Label { Text = "TileId:", Location = new Point(10, 30), Size = new Size(50, 20) };
            TextBox txtDelTileId = new TextBox { Location = new Point(65, 27), Size = new Size(80, 23) };
            Label lblDelScope = new Label { Text = "範圍:", Location = new Point(160, 30), Size = new Size(40, 20) };
            ComboBox cmbDelScope = new ComboBox { Location = new Point(205, 27), Size = new Size(150, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbDelScope.Items.Add("當前選擇的 S32");
            cmbDelScope.Items.Add("所有 S32 檔案");
            cmbDelScope.SelectedIndex = 0;

            Button btnBatchDelete = new Button();
            btnBatchDelete.Text = "批量刪除";
            btnBatchDelete.Location = new Point(370, 25);
            btnBatchDelete.Size = new Size(80, 28);
            btnBatchDelete.BackColor = Color.LightCoral;

            Button btnDeleteFromStats = new Button();
            btnDeleteFromStats.Text = "刪除選中統計項";
            btnDeleteFromStats.Location = new Point(460, 25);
            btnDeleteFromStats.Size = new Size(110, 28);
            btnDeleteFromStats.BackColor = Color.LightCoral;
            this.toolTip1.SetToolTip(btnDeleteFromStats, "在下方統計清單中選擇項目後，點擊此按鈕刪除");

            gbDelete.Controls.AddRange(new Control[] { lblDelTileId, txtDelTileId, lblDelScope, cmbDelScope, btnBatchDelete, btnDeleteFromStats });
            resultForm.Controls.Add(gbDelete);

            // 統計資訊
            GroupBox gbStats = new GroupBox();
            gbStats.Text = "統計資訊（點擊項目可填入 TileId）";
            gbStats.Location = new Point(10, 375);
            gbStats.Size = new Size(710, 130);

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
            btnClose.Location = new Point(630, 525);
            btnClose.Size = new Size(90, 35);
            btnClose.Click += (s, args) => resultForm.Close();
            resultForm.Controls.Add(btnClose);

            // ListView 項目點擊填入 TileId
            lvStats.Click += (s, args) =>
            {
                if (lvStats.SelectedItems.Count > 0)
                {
                    string tileIdStr = lvStats.SelectedItems[0].Text;
                    txtDelTileId.Text = tileIdStr;
                    txtOldTileId.Text = tileIdStr;
                }
            };

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
                string selectedFilePath = _document.S32Files.Keys.FirstOrDefault(k => Path.GetFileName(k) == selectedFileName);
                if (selectedFilePath == null || !_document.S32Files.TryGetValue(selectedFilePath, out S32Data s32Data)) return;

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
                string selectedFilePath = _document.S32Files.Keys.FirstOrDefault(k => Path.GetFileName(k) == selectedFileName);
                if (selectedFilePath == null || !_document.S32Files.TryGetValue(selectedFilePath, out S32Data s32Data))
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

                if (_document.S32Files.TryGetValue(currentFilePath, out S32Data s32Data))
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

                    foreach (var kvp in _document.S32Files)
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
                    string selectedFilePath = _document.S32Files.Keys.FirstOrDefault(k => Path.GetFileName(k) == selectedFileName);
                    if (selectedFilePath == null || !_document.S32Files.TryGetValue(selectedFilePath, out S32Data s32Data))
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

            // 批量刪除按鈕事件
            btnBatchDelete.Click += (s, args) =>
            {
                if (!int.TryParse(txtDelTileId.Text, out int delTileId))
                {
                    MessageBox.Show("請輸入有效的 TileId", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                bool allFiles = cmbDelScope.SelectedIndex == 1;
                int deletedCount = 0;

                if (allFiles)
                {
                    var confirmResult = MessageBox.Show(
                        $"確定要在所有 S32 檔案中刪除 TileId = {delTileId} 的所有項目嗎？\n\n（將 TileId 設為 0）",
                        "確認批量刪除",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (confirmResult != DialogResult.Yes) return;

                    foreach (var kvp in _document.S32Files)
                    {
                        S32Data s32Data = kvp.Value;
                        bool modified = false;
                        for (int y = 0; y < 64; y++)
                        {
                            for (int x = 0; x < 128; x++)
                            {
                                var cell = s32Data.Layer1[y, x];
                                if (cell != null && cell.TileId == delTileId)
                                {
                                    cell.TileId = 0;
                                    cell.IndexId = 0;
                                    deletedCount++;
                                    modified = true;
                                }
                            }
                        }
                        if (modified)
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
                    string selectedFilePath = _document.S32Files.Keys.FirstOrDefault(k => Path.GetFileName(k) == selectedFileName);
                    if (selectedFilePath == null || !_document.S32Files.TryGetValue(selectedFilePath, out S32Data s32Data))
                    {
                        MessageBox.Show("無法載入 S32 資料", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var confirmResult = MessageBox.Show(
                        $"確定要在 {selectedFileName} 中刪除 TileId = {delTileId} 的所有項目嗎？\n\n（將 TileId 設為 0）",
                        "確認批量刪除",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (confirmResult != DialogResult.Yes) return;

                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 128; x++)
                        {
                            var cell = s32Data.Layer1[y, x];
                            if (cell != null && cell.TileId == delTileId)
                            {
                                cell.TileId = 0;
                                cell.IndexId = 0;
                                deletedCount++;
                            }
                        }
                    }
                    if (deletedCount > 0)
                        s32Data.IsModified = true;
                }

                MessageBox.Show($"已刪除 {deletedCount} 個 Layer1 項目（TileId 設為 0）。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                updateStats();
                ClearS32BlockCache();
                RenderS32Map();
            };

            // 從統計清單刪除選中項目
            btnDeleteFromStats.Click += (s, args) =>
            {
                if (lvStats.SelectedItems.Count == 0)
                {
                    MessageBox.Show("請先在統計清單中選擇要刪除的 TileId", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 收集所有選中的 TileId
                var selectedTileIds = new List<int>();
                foreach (ListViewItem item in lvStats.SelectedItems)
                {
                    if (int.TryParse(item.Text, out int tileId))
                        selectedTileIds.Add(tileId);
                }

                if (selectedTileIds.Count == 0) return;

                bool allFiles = cmbDelScope.SelectedIndex == 1;
                string scopeText = allFiles ? "所有 S32 檔案" : cmbS32Files.SelectedItem?.ToString() ?? "當前 S32";
                string tileIdsText = string.Join(", ", selectedTileIds);

                var confirmResult = MessageBox.Show(
                    $"確定要在 {scopeText} 中刪除以下 TileId 嗎？\n\nTileId: {tileIdsText}\n\n（將 TileId 設為 0）",
                    "確認批量刪除",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (confirmResult != DialogResult.Yes) return;

                int deletedCount = 0;
                var tileIdSet = new HashSet<int>(selectedTileIds);

                if (allFiles)
                {
                    foreach (var kvp in _document.S32Files)
                    {
                        S32Data s32Data = kvp.Value;
                        bool modified = false;
                        for (int y = 0; y < 64; y++)
                        {
                            for (int x = 0; x < 128; x++)
                            {
                                var cell = s32Data.Layer1[y, x];
                                if (cell != null && tileIdSet.Contains(cell.TileId))
                                {
                                    cell.TileId = 0;
                                    cell.IndexId = 0;
                                    deletedCount++;
                                    modified = true;
                                }
                            }
                        }
                        if (modified)
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
                    string selectedFilePath = _document.S32Files.Keys.FirstOrDefault(k => Path.GetFileName(k) == selectedFileName);
                    if (selectedFilePath != null && _document.S32Files.TryGetValue(selectedFilePath, out S32Data s32Data))
                    {
                        for (int y = 0; y < 64; y++)
                        {
                            for (int x = 0; x < 128; x++)
                            {
                                var cell = s32Data.Layer1[y, x];
                                if (cell != null && tileIdSet.Contains(cell.TileId))
                                {
                                    cell.TileId = 0;
                                    cell.IndexId = 0;
                                    deletedCount++;
                                }
                            }
                        }
                        if (deletedCount > 0)
                            s32Data.IsModified = true;
                    }
                }

                MessageBox.Show($"已刪除 {deletedCount} 個 Layer1 項目（TileId 設為 0）。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                updateStats();
                ClearS32BlockCache();
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
                gbDelete.Size = new Size(resultForm.ClientSize.Width - 20, 80);
                gbDelete.Location = new Point(10, 290);
                gbStats.Size = new Size(resultForm.ClientSize.Width - 20, resultForm.ClientSize.Height - 430);
                gbStats.Location = new Point(10, 375);
                lvStats.Size = new Size(gbStats.Width - 20, gbStats.Height - 30);
                btnClose.Location = new Point(resultForm.ClientSize.Width - 100, resultForm.ClientSize.Height - 45);
            };

            resultForm.ShowDialog();
        }

        // 查看與清除第二層資料
        private void btnToolCheckL2_Click(object sender, EventArgs e)
        {
            if (_document.S32Files.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 收集所有 S32 的 Layer2 資料
            var s32WithL2 = new List<(string filePath, string fileName, int count, List<Layer2Item> items)>();
            int totalItems = 0;

            foreach (var kvp in _document.S32Files)
            {
                string filePath = kvp.Key;
                string fileName = Path.GetFileName(kvp.Key);
                S32Data s32Data = kvp.Value;

                if (s32Data.Layer2.Count > 0)
                {
                    s32WithL2.Add((filePath, fileName, s32Data.Layer2.Count, s32Data.Layer2.ToList()));
                    totalItems += s32Data.Layer2.Count;
                }
            }

            if (totalItems == 0)
            {
                MessageBox.Show("目前沒有任何 Layer2 資料。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 顯示結果
            Form resultForm = new Form();
            resultForm.Text = $"L2 查看與清除 - {s32WithL2.Count} 個 S32 有資料，共 {totalItems} 項";
            resultForm.Size = new Size(850, 600);
            resultForm.FormBorderStyle = FormBorderStyle.Sizable;
            resultForm.StartPosition = FormStartPosition.CenterParent;

            Label lblSummary = new Label();
            lblSummary.Text = $"共 {s32WithL2.Count} 個 S32 檔案有 Layer2 資料，總計 {totalItems} 項。勾選後可清除：";
            lblSummary.Location = new Point(10, 10);
            lblSummary.Size = new Size(810, 20);
            resultForm.Controls.Add(lblSummary);

            // 使用 CheckedListBox 顯示所有 Layer2 項目
            CheckedListBox clbItems = new CheckedListBox();
            clbItems.Location = new Point(10, 35);
            clbItems.Size = new Size(810, resultForm.ClientSize.Height - 130);
            clbItems.Font = new Font("Consolas", 9);
            clbItems.CheckOnClick = true;
            clbItems.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // 建立項目對應表（用於刪除）
            var itemMap = new List<(string filePath, Layer2Item item)>();

            foreach (var (filePath, fileName, count, items) in s32WithL2)
            {
                foreach (var item in items)
                {
                    string displayText = $"[{fileName}] X={item.X}, Y={item.Y}, Tile={item.TileId}, Idx={item.IndexId}, UK={item.UK}";
                    clbItems.Items.Add(displayText);
                    itemMap.Add((filePath, item));
                }
            }
            resultForm.Controls.Add(clbItems);

            // 按鈕面板
            Panel pnlButtons = new Panel();
            pnlButtons.Location = new Point(10, resultForm.ClientSize.Height - 90);
            pnlButtons.Size = new Size(810, 80);
            pnlButtons.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            resultForm.Controls.Add(pnlButtons);

            Button btnSelectAll = new Button { Text = "全選", Location = new Point(0, 0), Size = new Size(80, 30) };
            btnSelectAll.Click += (s, args) => { for (int i = 0; i < clbItems.Items.Count; i++) clbItems.SetItemChecked(i, true); };
            pnlButtons.Controls.Add(btnSelectAll);

            Button btnDeselectAll = new Button { Text = "取消全選", Location = new Point(90, 0), Size = new Size(80, 30) };
            btnDeselectAll.Click += (s, args) => { for (int i = 0; i < clbItems.Items.Count; i++) clbItems.SetItemChecked(i, false); };
            pnlButtons.Controls.Add(btnDeselectAll);

            // 按 S32 選擇
            Button btnSelectByS32 = new Button { Text = "按S32選", Location = new Point(180, 0), Size = new Size(80, 30) };
            btnSelectByS32.Click += (s, args) =>
            {
                // 顯示 S32 選擇對話框
                var s32Names = s32WithL2.Select(x => x.fileName).Distinct().ToList();
                using (var selectForm = new Form())
                {
                    selectForm.Text = "選擇 S32 檔案";
                    selectForm.Size = new Size(300, 400);
                    selectForm.StartPosition = FormStartPosition.CenterParent;

                    CheckedListBox clbS32 = new CheckedListBox();
                    clbS32.Location = new Point(10, 10);
                    clbS32.Size = new Size(260, 300);
                    clbS32.CheckOnClick = true;
                    foreach (var name in s32Names) clbS32.Items.Add(name);
                    selectForm.Controls.Add(clbS32);

                    Button btnOk = new Button { Text = "確定", Location = new Point(100, 320), Size = new Size(80, 30) };
                    btnOk.Click += (s2, args2) =>
                    {
                        var selectedS32 = new HashSet<string>();
                        foreach (int idx in clbS32.CheckedIndices)
                            selectedS32.Add(s32Names[idx]);

                        for (int i = 0; i < itemMap.Count; i++)
                        {
                            string fileName = Path.GetFileName(itemMap[i].filePath);
                            clbItems.SetItemChecked(i, selectedS32.Contains(fileName));
                        }
                        selectForm.Close();
                    };
                    selectForm.Controls.Add(btnOk);
                    selectForm.ShowDialog();
                }
            };
            pnlButtons.Controls.Add(btnSelectByS32);

            Button btnClearSelected = new Button { Text = "清除勾選", Location = new Point(0, 40), Size = new Size(100, 35), BackColor = Color.LightCoral };
            btnClearSelected.Click += (s, args) =>
            {
                if (clbItems.CheckedIndices.Count == 0)
                {
                    MessageBox.Show("請先勾選要清除的項目", "提示");
                    return;
                }

                if (MessageBox.Show($"確定要清除勾選的 {clbItems.CheckedIndices.Count} 個 Layer2 項目嗎？",
                    "確認刪除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                // 按 S32 分組要刪除的項目
                var toDelete = new Dictionary<string, List<Layer2Item>>();
                foreach (int idx in clbItems.CheckedIndices)
                {
                    var (filePath, item) = itemMap[idx];
                    if (!toDelete.ContainsKey(filePath))
                        toDelete[filePath] = new List<Layer2Item>();
                    toDelete[filePath].Add(item);
                }

                int deletedCount = 0;
                foreach (var kvp in toDelete)
                {
                    if (_document.S32Files.TryGetValue(kvp.Key, out var s32Data))
                    {
                        foreach (var item in kvp.Value)
                        {
                            s32Data.Layer2.Remove(item);
                            deletedCount++;
                        }
                        s32Data.IsModified = true;
                    }
                }

                MessageBox.Show($"已清除 {deletedCount} 個 Layer2 項目", "清除完成");
                ClearS32BlockCache();
                resultForm.Close();
                RenderS32Map();
            };
            pnlButtons.Controls.Add(btnClearSelected);

            Button btnClearAll = new Button { Text = "清除全部", Location = new Point(110, 40), Size = new Size(100, 35), BackColor = Color.Salmon };
            btnClearAll.Click += (s, args) =>
            {
                if (MessageBox.Show($"確定要清除所有 {totalItems} 個 Layer2 項目嗎？\n\n這將清除所有 S32 檔案中的 Layer2 資料！",
                    "確認刪除全部", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                int deletedCount = 0;
                foreach (var (filePath, _, _, _) in s32WithL2)
                {
                    if (_document.S32Files.TryGetValue(filePath, out var s32Data))
                    {
                        deletedCount += s32Data.Layer2.Count;
                        s32Data.Layer2.Clear();
                        s32Data.IsModified = true;
                    }
                }

                MessageBox.Show($"已清除 {deletedCount} 個 Layer2 項目", "清除完成");
                ClearS32BlockCache();
                resultForm.Close();
                RenderS32Map();
            };
            pnlButtons.Controls.Add(btnClearAll);

            Button btnClose = new Button { Text = "關閉", Location = new Point(pnlButtons.Width - 90, 40), Size = new Size(80, 35), Anchor = AnchorStyles.Right };
            btnClose.Click += (s, args) => resultForm.Close();
            pnlButtons.Controls.Add(btnClose);

            resultForm.ShowDialog();
        }

        // 查看與編輯第四層（物件）資料
        private void btnToolCheckL4_Click(object sender, EventArgs e)
        {
            if (_document.S32Files.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 收集所有 Layer4 資料
            List<(string filePath, string fileName, List<ObjectTile> items)> s32WithL4 =
                new List<(string, string, List<ObjectTile>)>();

            foreach (var kvp in _document.S32Files)
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
                if (_document.S32Files.TryGetValue(filePath, out S32Data s32Data))
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
                foreach (var kvp in _document.S32Files)
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
                    string selectedFilePath = _document.S32Files.Keys.FirstOrDefault(k => Path.GetFileName(k) == selectedFileName);

                    if (selectedFilePath != null && _document.S32Files.TryGetValue(selectedFilePath, out S32Data s32Data))
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
                    if (_document.S32Files.TryGetValue(kvp.Key, out S32Data s32Data))
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
                    if (_document.S32Files.TryGetValue(filePath, out S32Data s32Data))
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
            if (_document.S32Files.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 收集有 Layer5 資料的 S32
            List<(string filePath, string fileName, int count, List<Layer5Item> items)> s32WithL5 =
                new List<(string, string, int, List<Layer5Item>)>();

            foreach (var kvp in _document.S32Files)
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
            lblSummary.Text = $"共 {s32WithL5.Count} 個 S32 有 Layer5 資料，總計 {totalItems} 項。";
            lblSummary.Location = new Point(10, 10);
            lblSummary.Size = new Size(660, 20);
            resultForm.Controls.Add(lblSummary);

            // 搜尋區域
            Label lblSearch = new Label();
            lblSearch.Text = "搜尋:";
            lblSearch.Location = new Point(10, 35);
            lblSearch.Size = new Size(40, 20);
            resultForm.Controls.Add(lblSearch);

            TextBox txtSearchX = new TextBox();
            txtSearchX.Location = new Point(50, 32);
            txtSearchX.Size = new Size(60, 22);
            txtSearchX.PlaceholderText = "X";
            resultForm.Controls.Add(txtSearchX);

            TextBox txtSearchY = new TextBox();
            txtSearchY.Location = new Point(115, 32);
            txtSearchY.Size = new Size(60, 22);
            txtSearchY.PlaceholderText = "Y";
            resultForm.Controls.Add(txtSearchY);

            TextBox txtSearchObjIdx = new TextBox();
            txtSearchObjIdx.Location = new Point(180, 32);
            txtSearchObjIdx.Size = new Size(70, 22);
            txtSearchObjIdx.PlaceholderText = "ObjIdx";
            resultForm.Controls.Add(txtSearchObjIdx);

            Button btnSearch = new Button();
            btnSearch.Text = "搜尋";
            btnSearch.Location = new Point(255, 31);
            btnSearch.Size = new Size(50, 24);
            resultForm.Controls.Add(btnSearch);

            Button btnClearSearch = new Button();
            btnClearSearch.Text = "清除";
            btnClearSearch.Location = new Point(310, 31);
            btnClearSearch.Size = new Size(50, 24);
            resultForm.Controls.Add(btnClearSearch);

            Label lblSearchResult = new Label();
            lblSearchResult.Text = "";
            lblSearchResult.Location = new Point(370, 35);
            lblSearchResult.Size = new Size(290, 20);
            lblSearchResult.ForeColor = Color.Blue;
            resultForm.Controls.Add(lblSearchResult);

            CheckedListBox clbItems = new CheckedListBox();
            clbItems.Location = new Point(10, 60);
            clbItems.Size = new Size(660, 355);
            clbItems.Font = new Font("Consolas", 9);
            clbItems.CheckOnClick = true;

            List<(string filePath, int itemIndex, Layer5Item item)> itemInfoList =
                new List<(string, int, Layer5Item)>();
            List<(string filePath, int itemIndex, Layer5Item item, string fileName)> allItems =
                new List<(string, int, Layer5Item, string)>();

            // 建立完整項目列表
            foreach (var (filePath, fileName, count, items) in s32WithL5)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    allItems.Add((filePath, i, items[i], fileName));
                }
            }

            // 顯示項目的方法
            Action<List<(string filePath, int itemIndex, Layer5Item item, string fileName)>> displayItems = (itemsToShow) =>
            {
                clbItems.Items.Clear();
                itemInfoList.Clear();
                if (itemsToShow.Count == 0)
                {
                    clbItems.Items.Add("沒有符合條件的項目");
                    clbItems.Enabled = false;
                }
                else
                {
                    clbItems.Enabled = true;
                    foreach (var (filePath, itemIndex, item, fileName) in itemsToShow)
                    {
                        string displayText = $"[{fileName}] X={item.X}, Y={item.Y}, ObjIdx={item.ObjectIndex}, Type={item.Type}";
                        clbItems.Items.Add(displayText);
                        itemInfoList.Add((filePath, itemIndex, item));
                    }
                }
            };

            // 搜尋方法
            Action doSearch = () =>
            {
                string xText = txtSearchX.Text.Trim();
                string yText = txtSearchY.Text.Trim();
                string objIdxText = txtSearchObjIdx.Text.Trim();

                if (string.IsNullOrEmpty(xText) && string.IsNullOrEmpty(yText) && string.IsNullOrEmpty(objIdxText))
                {
                    displayItems(allItems);
                    lblSearchResult.Text = "";
                    return;
                }

                int? searchX = null, searchY = null;
                ushort? searchObjIdx = null;

                if (!string.IsNullOrEmpty(xText) && int.TryParse(xText, out int x)) searchX = x;
                if (!string.IsNullOrEmpty(yText) && int.TryParse(yText, out int y)) searchY = y;
                if (!string.IsNullOrEmpty(objIdxText) && ushort.TryParse(objIdxText, out ushort objIdx)) searchObjIdx = objIdx;

                var filtered = allItems.Where(a =>
                    (!searchX.HasValue || a.item.X == searchX.Value) &&
                    (!searchY.HasValue || a.item.Y == searchY.Value) &&
                    (!searchObjIdx.HasValue || a.item.ObjectIndex == searchObjIdx.Value)
                ).ToList();

                displayItems(filtered);

                var conditions = new List<string>();
                if (searchX.HasValue) conditions.Add($"X={searchX}");
                if (searchY.HasValue) conditions.Add($"Y={searchY}");
                if (searchObjIdx.HasValue) conditions.Add($"ObjIdx={searchObjIdx}");
                lblSearchResult.Text = $"找到 {filtered.Count} 個 ({string.Join(", ", conditions)})";
            };

            // 初始顯示全部
            if (s32WithL5.Count == 0)
            {
                clbItems.Items.Add("沒有任何 S32 檔案有 Layer5 資料");
                clbItems.Enabled = false;
            }
            else
            {
                displayItems(allItems);
            }

            // 搜尋事件
            btnSearch.Click += (s, args) => doSearch();

            // Enter 鍵搜尋
            EventHandler<KeyEventArgs> searchOnEnter = (s, args) =>
            {
                if (args.KeyCode == Keys.Enter)
                {
                    doSearch();
                    args.SuppressKeyPress = true;
                }
            };
            txtSearchX.KeyDown += (s, args) => searchOnEnter(s, args);
            txtSearchY.KeyDown += (s, args) => searchOnEnter(s, args);
            txtSearchObjIdx.KeyDown += (s, args) => searchOnEnter(s, args);

            // 清除搜尋
            btnClearSearch.Click += (s, args) =>
            {
                txtSearchX.Text = "";
                txtSearchY.Text = "";
                txtSearchObjIdx.Text = "";
                displayItems(allItems);
                lblSearchResult.Text = "";
            };

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
                    if (_document.S32Files.TryGetValue(kvp.Key, out S32Data s32Data))
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

                UpdateLayer5InvalidButton();
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
                    if (_document.S32Files.TryGetValue(filePath, out S32Data s32Data))
                    {
                        removedCount += s32Data.Layer5.Count;
                        s32Data.Layer5.Clear();
                        s32Data.IsModified = true;
                    }
                }

                MessageBox.Show($"已清除 {removedCount} 個 Layer5 項目。\n\n請記得儲存 S32 檔案。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                UpdateLayer5InvalidButton();
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

            resultForm.Show();
        }

        // 檢查 Layer5 異常並更新按鈕顯示狀態
        private void UpdateLayer5InvalidButton()
        {
            var invalidL5Items = GetInvalidLayer5Items();
            var invalidTileItems = GetInvalidTileIds();
            var layer8ExtendedS32 = GetLayer8ExtendedS32Files();
            int totalInvalid = invalidL5Items.Count + invalidTileItems.Count + layer8ExtendedS32.Count;

            btnToolCheckL5Invalid.Visible = totalInvalid > 0;
            if (totalInvalid > 0)
            {
                var tooltipParts = new List<string>();
                if (invalidL5Items.Count > 0)
                    tooltipParts.Add($"Layer5異常: {invalidL5Items.Count}");
                if (invalidTileItems.Count > 0)
                    tooltipParts.Add($"無效TileId: {invalidTileItems.Count}");
                if (layer8ExtendedS32.Count > 0)
                    tooltipParts.Add($"L8擴展: {layer8ExtendedS32.Count}");
                toolTip1.SetToolTip(btnToolCheckL5Invalid, $"發現異常: {string.Join(", ", tooltipParts)}");
            }
        }

        // 取得使用 Layer8 擴展格式的 S32 檔案
        private List<(string filePath, string fileName, int layer8Count)> GetLayer8ExtendedS32Files()
        {
            var result = new List<(string filePath, string fileName, int layer8Count)>();

            if (_document.S32Files.Count == 0)
                return result;

            foreach (var kvp in _document.S32Files)
            {
                if (kvp.Value.Layer8HasExtendedData)
                {
                    result.Add((kvp.Key, Path.GetFileName(kvp.Key), kvp.Value.Layer8.Count));
                }
            }

            return result;
        }

        // 取得 Layer5 中 ObjectIndex 不存在於 Layer4 GroupId 的項目
        // 或雖然存在但該格找不到對應 GroupId 的物件
        private List<(string filePath, string fileName, Layer5Item item, int itemIndex, string reason)> GetInvalidLayer5Items()
        {
            if (_document.S32Files.Count == 0)
                return new List<(string filePath, string fileName, Layer5Item item, int itemIndex, string reason)>();

            // 取得目前 viewport 範圍
            var viewportRect = _viewState.GetViewportWorldRect();

            // 過濾出 viewport 內的 S32 檔案
            var viewportS32Files = new Dictionary<string, S32Data>();
            foreach (var kvp in _document.S32Files)
            {
                S32Data s32Data = kvp.Value;

                // 檢查 S32 是否在 viewport 內
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int blockWidth = 64 * 24 * 2;  // 3072
                int blockHeight = 64 * 12 * 2; // 1536
                Rectangle blockRect = new Rectangle(loc[0], loc[1], blockWidth, blockHeight);
                if (blockRect.IntersectsWith(viewportRect))
                {
                    viewportS32Files[kvp.Key] = s32Data;
                }
            }

            // 使用共用的 Layer5Checker 檢查（radius=0 表示只檢查該格）
            var results = Layer5Checker.Check(
                viewportS32Files,
                radius: -1,
                getSegInfo: s32 => (s32.SegInfo.nLinBeginX, s32.SegInfo.nLinBeginY));

            // 轉換為舊格式
            return results.Select(r => (r.FilePath, r.FileName, r.Item, r.ItemIndex, r.Reason)).ToList();
        }

        // 無效 TileId 資訊類別
        private class InvalidTileInfo
        {
            public string FilePath { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string Layer { get; set; } = string.Empty;  // "Layer1", "Layer2", "Layer4"
            public int X { get; set; }
            public int Y { get; set; }
            public int TileId { get; set; }
            public int IndexId { get; set; }
            public string Reason { get; set; } = string.Empty;  // "Til檔案不存在" 或 "IndexId超出範圍"
        }

        // 取得無效的 TileId（Layer1, Layer2, Layer4）
        private List<InvalidTileInfo> GetInvalidTileIds()
        {
            var invalidTiles = new List<InvalidTileInfo>();

            if (_document.S32Files.Count == 0)
                return invalidTiles;

            // 快取已檢查過的 TileId -> (是否存在, tilArray Count)
            var tilCache = new Dictionary<int, (bool exists, int count)>();

            // 檢查 TileId 和 IndexId 是否有效
            bool CheckTileValid(int tileId, int indexId, out string reason)
            {
                reason = string.Empty;
                if (tileId <= 0) return true;  // TileId = 0 表示空格子，不算無效

                if (!tilCache.TryGetValue(tileId, out var cacheInfo))
                {
                    string key = $"{tileId}.til";
                    byte[] data = L1PakReader.UnPack("Tile", key);
                    if (data == null)
                    {
                        tilCache[tileId] = (false, 0);
                        cacheInfo = (false, 0);
                    }
                    else
                    {
                        var tilArray = L1Til.Parse(data);
                        tilCache[tileId] = (true, tilArray.Count);
                        cacheInfo = (true, tilArray.Count);
                    }
                }

                if (!cacheInfo.exists)
                {
                    reason = "Til檔案不存在";
                    return false;
                }

                if (indexId >= cacheInfo.count)
                {
                    reason = $"IndexId超出範圍(max={cacheInfo.count - 1})";
                    return false;
                }

                return true;
            }

            foreach (var kvp in _document.S32Files)
            {
                string filePath = kvp.Key;
                string fileName = Path.GetFileName(kvp.Key);
                S32Data s32Data = kvp.Value;

                // 檢查 Layer1（地板）
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        var cell = s32Data.Layer1[y, x];
                        if (cell != null && cell.TileId > 0)
                        {
                            if (!CheckTileValid(cell.TileId, cell.IndexId, out string reason))
                            {
                                invalidTiles.Add(new InvalidTileInfo
                                {
                                    FilePath = filePath,
                                    FileName = fileName,
                                    Layer = "Layer1",
                                    X = x,
                                    Y = y,
                                    TileId = cell.TileId,
                                    IndexId = cell.IndexId,
                                    Reason = reason
                                });
                            }
                        }
                    }
                }

                // 檢查 Layer2
                for (int i = 0; i < s32Data.Layer2.Count; i++)
                {
                    var item = s32Data.Layer2[i];
                    if (item.TileId > 0)
                    {
                        if (!CheckTileValid(item.TileId, item.IndexId, out string reason))
                        {
                            invalidTiles.Add(new InvalidTileInfo
                            {
                                FilePath = filePath,
                                FileName = fileName,
                                Layer = "Layer2",
                                X = item.X,
                                Y = item.Y,
                                TileId = item.TileId,
                                IndexId = item.IndexId,
                                Reason = reason
                            });
                        }
                    }
                }

                // 檢查 Layer4（物件）
                for (int i = 0; i < s32Data.Layer4.Count; i++)
                {
                    var obj = s32Data.Layer4[i];
                    if (obj.TileId > 0)
                    {
                        if (!CheckTileValid(obj.TileId, obj.IndexId, out string reason))
                        {
                            invalidTiles.Add(new InvalidTileInfo
                            {
                                FilePath = filePath,
                                FileName = fileName,
                                Layer = "Layer4",
                                X = obj.X,
                                Y = obj.Y,
                                TileId = obj.TileId,
                                IndexId = obj.IndexId,
                                Reason = reason
                            });
                        }
                    }
                }
            }

            return invalidTiles;
        }

        // 檢查 Layer5 異常和無效 TileId
        private void btnToolCheckL5Invalid_Click(object sender, EventArgs e)
        {
            var invalidL5Items = GetInvalidLayer5Items();
            var invalidTileItems = GetInvalidTileIds();
            var layer8ExtendedS32 = GetLayer8ExtendedS32Files();

            if (invalidL5Items.Count == 0 && invalidTileItems.Count == 0 && layer8ExtendedS32.Count == 0)
            {
                MessageBox.Show("檢查完成，沒有發現任何異常。",
                    "檢查完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                btnToolCheckL5Invalid.Visible = false;
                return;
            }

            // 建立訊息
            var msgParts = new List<string>();
            if (invalidL5Items.Count > 0)
            {
                int noGroupCount = invalidL5Items.Count(x => x.reason == "GroupId不存在");
                int noObjCount = invalidL5Items.Count(x => x.reason == "周圍無對應物件");
                var l5Parts = new List<string>();
                if (noGroupCount > 0) l5Parts.Add($"GroupId不存在:{noGroupCount}");
                if (noObjCount > 0) l5Parts.Add($"周圍無物件:{noObjCount}");
                msgParts.Add($"• {invalidL5Items.Count} 個 Layer5 異常 ({string.Join(", ", l5Parts)})");
            }
            if (invalidTileItems.Count > 0)
            {
                // 統計各層的無效 TileId 數量
                int l1Count = invalidTileItems.Count(t => t.Layer == "Layer1");
                int l2Count = invalidTileItems.Count(t => t.Layer == "Layer2");
                int l4Count = invalidTileItems.Count(t => t.Layer == "Layer4");
                var tileParts = new List<string>();
                if (l1Count > 0) tileParts.Add($"L1:{l1Count}");
                if (l2Count > 0) tileParts.Add($"L2:{l2Count}");
                if (l4Count > 0) tileParts.Add($"L4:{l4Count}");
                msgParts.Add($"• {invalidTileItems.Count} 個無效的 TileId ({string.Join(", ", tileParts)})");
            }
            if (layer8ExtendedS32.Count > 0)
            {
                int totalL8Items = layer8ExtendedS32.Sum(x => x.layer8Count);
                msgParts.Add($"• {layer8ExtendedS32.Count} 個 S32 使用 Layer8 擴展格式（共 {totalL8Items} 個項目，可能導致閃退）");
            }

            // 顯示確認對話框
            string message = $"發現以下異常：\n\n{string.Join("\n", msgParts)}\n\n是否要查看詳細資訊？";
            var confirmResult = MessageBox.Show(message, "異常檢查結果",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirmResult != DialogResult.Yes)
                return;

            // 顯示清單讓使用者選擇要清除的項目
            Form resultForm = new Form();
            resultForm.Text = $"異常檢查結果";
            resultForm.Size = new Size(850, 600);
            resultForm.FormBorderStyle = FormBorderStyle.Sizable;
            resultForm.StartPosition = FormStartPosition.CenterParent;

            // 使用 TabControl 分頁顯示
            TabControl tabControl = new TabControl();
            tabControl.Location = new Point(10, 10);
            tabControl.Size = new Size(resultForm.ClientSize.Width - 20, resultForm.ClientSize.Height - 60);
            tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            resultForm.Controls.Add(tabControl);

            // ===== Tab 1: Layer5 異常 =====
            if (invalidL5Items.Count > 0)
            {
                TabPage tabL5 = new TabPage($"Layer5 異常 ({invalidL5Items.Count})");
                tabControl.TabPages.Add(tabL5);

                Label lblL5Summary = new Label();
                lblL5Summary.Text = $"以下 {invalidL5Items.Count} 個 Layer5 項目異常（GroupId不存在或周圍一格內無對應物件）：";
                lblL5Summary.Location = new Point(5, 5);
                lblL5Summary.Size = new Size(tabL5.ClientSize.Width - 10, 20);
                lblL5Summary.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                tabL5.Controls.Add(lblL5Summary);

                CheckedListBox clbL5Items = new CheckedListBox();
                clbL5Items.Location = new Point(5, 30);
                clbL5Items.Size = new Size(tabL5.ClientSize.Width - 10, tabL5.ClientSize.Height - 110);
                clbL5Items.Font = new Font("Consolas", 9);
                clbL5Items.CheckOnClick = true;
                clbL5Items.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

                foreach (var (filePath, fileName, item, itemIndex, reason) in invalidL5Items)
                {
                    string displayText = $"[{fileName}] X={item.X}, Y={item.Y}, ObjIdx={item.ObjectIndex}, Type={item.Type} [{reason}]";
                    clbL5Items.Items.Add(displayText);
                }
                tabL5.Controls.Add(clbL5Items);

                // 按鈕面板
                Panel pnlL5Buttons = new Panel();
                pnlL5Buttons.Location = new Point(5, tabL5.ClientSize.Height - 75);
                pnlL5Buttons.Size = new Size(tabL5.ClientSize.Width - 10, 70);
                pnlL5Buttons.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                tabL5.Controls.Add(pnlL5Buttons);

                Button btnL5SelectAll = new Button { Text = "全選", Location = new Point(0, 0), Size = new Size(80, 30) };
                btnL5SelectAll.Click += (s, args) => { for (int i = 0; i < clbL5Items.Items.Count; i++) clbL5Items.SetItemChecked(i, true); };
                pnlL5Buttons.Controls.Add(btnL5SelectAll);

                Button btnL5DeselectAll = new Button { Text = "取消全選", Location = new Point(90, 0), Size = new Size(80, 30) };
                btnL5DeselectAll.Click += (s, args) => { for (int i = 0; i < clbL5Items.Items.Count; i++) clbL5Items.SetItemChecked(i, false); };
                pnlL5Buttons.Controls.Add(btnL5DeselectAll);

                Button btnL5ClearSelected = new Button { Text = "清除勾選", Location = new Point(0, 35), Size = new Size(100, 30), BackColor = Color.LightCoral };
                btnL5ClearSelected.Click += (s, args) =>
                {
                    if (clbL5Items.CheckedIndices.Count == 0) { MessageBox.Show("請先勾選要清除的項目", "提示"); return; }
                    if (MessageBox.Show($"確定要清除勾選的 {clbL5Items.CheckedIndices.Count} 個項目嗎？", "確認刪除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                    var toDelete = new Dictionary<string, List<Layer5Item>>();
                    foreach (int idx in clbL5Items.CheckedIndices)
                    {
                        var (filePath, _, item, _, _) = invalidL5Items[idx];
                        if (!toDelete.ContainsKey(filePath)) toDelete[filePath] = new List<Layer5Item>();
                        toDelete[filePath].Add(item);
                    }
                    int deletedCount = 0;
                    foreach (var kvp in toDelete)
                    {
                        if (_document.S32Files.TryGetValue(kvp.Key, out var s32Data))
                        {
                            foreach (var item in kvp.Value) { s32Data.Layer5.Remove(item); deletedCount++; }
                            s32Data.IsModified = true;
                        }
                    }
                    MessageBox.Show($"已清除 {deletedCount} 個異常的 Layer5 項目", "清除完成");
                    ClearS32BlockCache(); resultForm.Close(); RenderS32Map();
                };
                pnlL5Buttons.Controls.Add(btnL5ClearSelected);

                Button btnL5ClearAll = new Button { Text = "清除全部", Location = new Point(110, 35), Size = new Size(100, 30), BackColor = Color.Salmon };
                btnL5ClearAll.Click += (s, args) =>
                {
                    if (MessageBox.Show($"確定要清除所有 {invalidL5Items.Count} 個異常項目嗎？", "確認刪除全部", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    var toDelete = new Dictionary<string, List<Layer5Item>>();
                    foreach (var (filePath, _, item, _, _) in invalidL5Items)
                    {
                        if (!toDelete.ContainsKey(filePath)) toDelete[filePath] = new List<Layer5Item>();
                        toDelete[filePath].Add(item);
                    }
                    int deletedCount = 0;
                    foreach (var kvp in toDelete)
                    {
                        if (_document.S32Files.TryGetValue(kvp.Key, out var s32Data))
                        {
                            foreach (var item in kvp.Value) { s32Data.Layer5.Remove(item); deletedCount++; }
                            s32Data.IsModified = true;
                        }
                    }
                    MessageBox.Show($"已清除 {deletedCount} 個異常的 Layer5 項目", "清除完成");
                    ClearS32BlockCache(); resultForm.Close(); RenderS32Map();
                };
                pnlL5Buttons.Controls.Add(btnL5ClearAll);
            }

            // ===== Tab 2: 無效 TileId =====
            if (invalidTileItems.Count > 0)
            {
                TabPage tabTile = new TabPage($"無效 TileId ({invalidTileItems.Count})");
                tabControl.TabPages.Add(tabTile);

                // 統計資訊
                int l1Count = invalidTileItems.Count(t => t.Layer == "Layer1");
                int l2Count = invalidTileItems.Count(t => t.Layer == "Layer2");
                int l4Count = invalidTileItems.Count(t => t.Layer == "Layer4");

                Label lblTileSummary = new Label();
                lblTileSummary.Text = $"發現 {invalidTileItems.Count} 個無效 TileId (L1:{l1Count}, L2:{l2Count}, L4:{l4Count})。這些 Tile 檔案不存在或 IndexId 超出範圍：";
                lblTileSummary.Location = new Point(5, 5);
                lblTileSummary.Size = new Size(tabTile.ClientSize.Width - 10, 20);
                lblTileSummary.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                tabTile.Controls.Add(lblTileSummary);

                CheckedListBox clbTileItems = new CheckedListBox();
                clbTileItems.Location = new Point(5, 30);
                clbTileItems.Size = new Size(tabTile.ClientSize.Width - 10, tabTile.ClientSize.Height - 110);
                clbTileItems.Font = new Font("Consolas", 9);
                clbTileItems.CheckOnClick = true;
                clbTileItems.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

                foreach (var tile in invalidTileItems)
                {
                    string displayText = $"[{tile.FileName}] {tile.Layer} X={tile.X}, Y={tile.Y}, Tile={tile.TileId}, Idx={tile.IndexId} - {tile.Reason}";
                    clbTileItems.Items.Add(displayText);
                }
                tabTile.Controls.Add(clbTileItems);

                // 按鈕面板
                Panel pnlTileButtons = new Panel();
                pnlTileButtons.Location = new Point(5, tabTile.ClientSize.Height - 75);
                pnlTileButtons.Size = new Size(tabTile.ClientSize.Width - 10, 70);
                pnlTileButtons.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                tabTile.Controls.Add(pnlTileButtons);

                Button btnTileSelectAll = new Button { Text = "全選", Location = new Point(0, 0), Size = new Size(80, 30) };
                btnTileSelectAll.Click += (s, args) => { for (int i = 0; i < clbTileItems.Items.Count; i++) clbTileItems.SetItemChecked(i, true); };
                pnlTileButtons.Controls.Add(btnTileSelectAll);

                Button btnTileDeselectAll = new Button { Text = "取消全選", Location = new Point(90, 0), Size = new Size(80, 30) };
                btnTileDeselectAll.Click += (s, args) => { for (int i = 0; i < clbTileItems.Items.Count; i++) clbTileItems.SetItemChecked(i, false); };
                pnlTileButtons.Controls.Add(btnTileDeselectAll);

                // 篩選按鈕
                Button btnFilterL1 = new Button { Text = "只選L1", Location = new Point(180, 0), Size = new Size(70, 30) };
                btnFilterL1.Click += (s, args) => { for (int i = 0; i < invalidTileItems.Count; i++) clbTileItems.SetItemChecked(i, invalidTileItems[i].Layer == "Layer1"); };
                pnlTileButtons.Controls.Add(btnFilterL1);

                Button btnFilterL2 = new Button { Text = "只選L2", Location = new Point(255, 0), Size = new Size(70, 30) };
                btnFilterL2.Click += (s, args) => { for (int i = 0; i < invalidTileItems.Count; i++) clbTileItems.SetItemChecked(i, invalidTileItems[i].Layer == "Layer2"); };
                pnlTileButtons.Controls.Add(btnFilterL2);

                Button btnFilterL4 = new Button { Text = "只選L4", Location = new Point(330, 0), Size = new Size(70, 30) };
                btnFilterL4.Click += (s, args) => { for (int i = 0; i < invalidTileItems.Count; i++) clbTileItems.SetItemChecked(i, invalidTileItems[i].Layer == "Layer4"); };
                pnlTileButtons.Controls.Add(btnFilterL4);

                Button btnTileClearSelected = new Button { Text = "清除勾選", Location = new Point(0, 35), Size = new Size(100, 30), BackColor = Color.LightCoral };
                btnTileClearSelected.Click += (s, args) =>
                {
                    if (clbTileItems.CheckedIndices.Count == 0) { MessageBox.Show("請先勾選要清除的項目", "提示"); return; }
                    if (MessageBox.Show($"確定要清除勾選的 {clbTileItems.CheckedIndices.Count} 個無效 Tile 嗎？\n\n" +
                        "• Layer1: 將 TileId 設為 0\n• Layer2: 移除該項目\n• Layer4: 移除該物件",
                        "確認刪除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                    int deletedCount = 0;
                    var checkedIndices = clbTileItems.CheckedIndices.Cast<int>().OrderByDescending(i => i).ToList();
                    foreach (int idx in checkedIndices)
                    {
                        var tile = invalidTileItems[idx];
                        if (_document.S32Files.TryGetValue(tile.FilePath, out var s32Data))
                        {
                            if (tile.Layer == "Layer1")
                            {
                                var cell = s32Data.Layer1[tile.Y, tile.X];
                                if (cell != null) { cell.TileId = 0; cell.IndexId = 0; deletedCount++; }
                            }
                            else if (tile.Layer == "Layer2")
                            {
                                var item = s32Data.Layer2.FirstOrDefault(l => l.X == tile.X && l.Y == tile.Y && l.TileId == tile.TileId && l.IndexId == tile.IndexId);
                                if (item != null) { s32Data.Layer2.Remove(item); deletedCount++; }
                            }
                            else if (tile.Layer == "Layer4")
                            {
                                var obj = s32Data.Layer4.FirstOrDefault(o => o.X == tile.X && o.Y == tile.Y && o.TileId == tile.TileId && o.IndexId == tile.IndexId);
                                if (obj != null) { s32Data.Layer4.Remove(obj); deletedCount++; }
                            }
                            s32Data.IsModified = true;
                        }
                    }
                    MessageBox.Show($"已清除 {deletedCount} 個無效 Tile", "清除完成");
                    ClearS32BlockCache(); resultForm.Close(); RenderS32Map();
                };
                pnlTileButtons.Controls.Add(btnTileClearSelected);

                Button btnTileClearAll = new Button { Text = "清除全部", Location = new Point(110, 35), Size = new Size(100, 30), BackColor = Color.Salmon };
                btnTileClearAll.Click += (s, args) =>
                {
                    if (MessageBox.Show($"確定要清除所有 {invalidTileItems.Count} 個無效 Tile 嗎？\n\n" +
                        "• Layer1: 將 TileId 設為 0\n• Layer2: 移除該項目\n• Layer4: 移除該物件",
                        "確認刪除全部", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                    int deletedCount = 0;
                    foreach (var tile in invalidTileItems)
                    {
                        if (_document.S32Files.TryGetValue(tile.FilePath, out var s32Data))
                        {
                            if (tile.Layer == "Layer1")
                            {
                                var cell = s32Data.Layer1[tile.Y, tile.X];
                                if (cell != null) { cell.TileId = 0; cell.IndexId = 0; deletedCount++; }
                            }
                            else if (tile.Layer == "Layer2")
                            {
                                var item = s32Data.Layer2.FirstOrDefault(l => l.X == tile.X && l.Y == tile.Y && l.TileId == tile.TileId && l.IndexId == tile.IndexId);
                                if (item != null) { s32Data.Layer2.Remove(item); deletedCount++; }
                            }
                            else if (tile.Layer == "Layer4")
                            {
                                var obj = s32Data.Layer4.FirstOrDefault(o => o.X == tile.X && o.Y == tile.Y && o.TileId == tile.TileId && o.IndexId == tile.IndexId);
                                if (obj != null) { s32Data.Layer4.Remove(obj); deletedCount++; }
                            }
                            s32Data.IsModified = true;
                        }
                    }
                    MessageBox.Show($"已清除 {deletedCount} 個無效 Tile", "清除完成");
                    ClearS32BlockCache(); resultForm.Close(); RenderS32Map();
                };
                pnlTileButtons.Controls.Add(btnTileClearAll);
            }

            // ===== Tab 3: Layer8 擴展格式 =====
            if (layer8ExtendedS32.Count > 0)
            {
                int totalL8Items = layer8ExtendedS32.Sum(x => x.layer8Count);
                TabPage tabL8 = new TabPage($"L8 擴展格式 ({layer8ExtendedS32.Count})");
                tabControl.TabPages.Add(tabL8);

                Label lblL8Summary = new Label();
                lblL8Summary.Text = $"以下 {layer8ExtendedS32.Count} 個 S32 使用 Layer8 擴展格式（共 {totalL8Items} 個項目）。擴展格式可能導致遊戲閃退：";
                lblL8Summary.Location = new Point(5, 5);
                lblL8Summary.Size = new Size(tabL8.ClientSize.Width - 10, 20);
                lblL8Summary.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                tabL8.Controls.Add(lblL8Summary);

                CheckedListBox clbL8Items = new CheckedListBox();
                clbL8Items.Location = new Point(5, 30);
                clbL8Items.Size = new Size(tabL8.ClientSize.Width - 10, tabL8.ClientSize.Height - 110);
                clbL8Items.Font = new Font("Consolas", 9);
                clbL8Items.CheckOnClick = true;
                clbL8Items.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

                foreach (var (filePath, fileName, layer8Count) in layer8ExtendedS32)
                {
                    string displayText = $"[{fileName}] Layer8 項目數: {layer8Count}";
                    clbL8Items.Items.Add(displayText);
                }
                tabL8.Controls.Add(clbL8Items);

                // 按鈕面板
                Panel pnlL8Buttons = new Panel();
                pnlL8Buttons.Location = new Point(5, tabL8.ClientSize.Height - 75);
                pnlL8Buttons.Size = new Size(tabL8.ClientSize.Width - 10, 70);
                pnlL8Buttons.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                tabL8.Controls.Add(pnlL8Buttons);

                Button btnL8SelectAll = new Button { Text = "全選", Location = new Point(0, 0), Size = new Size(80, 30) };
                btnL8SelectAll.Click += (s, args) => { for (int i = 0; i < clbL8Items.Items.Count; i++) clbL8Items.SetItemChecked(i, true); };
                pnlL8Buttons.Controls.Add(btnL8SelectAll);

                Button btnL8DeselectAll = new Button { Text = "取消全選", Location = new Point(90, 0), Size = new Size(80, 30) };
                btnL8DeselectAll.Click += (s, args) => { for (int i = 0; i < clbL8Items.Items.Count; i++) clbL8Items.SetItemChecked(i, false); };
                pnlL8Buttons.Controls.Add(btnL8DeselectAll);

                Button btnL8ResetSelected = new Button { Text = "重設勾選為一般格式", Location = new Point(0, 35), Size = new Size(150, 30), BackColor = Color.LightCoral };
                btnL8ResetSelected.Click += (s, args) =>
                {
                    if (clbL8Items.CheckedIndices.Count == 0) { MessageBox.Show("請先勾選要重設的項目", "提示"); return; }
                    if (MessageBox.Show($"確定要將勾選的 {clbL8Items.CheckedIndices.Count} 個 S32 重設為一般格式嗎？\n\n這會清除這些 S32 所有 Layer8 項目的 ExtendedData。",
                        "確認重設", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                    int resetCount = 0;
                    int clearedItems = 0;
                    foreach (int idx in clbL8Items.CheckedIndices)
                    {
                        var (filePath, _, _) = layer8ExtendedS32[idx];
                        if (_document.S32Files.TryGetValue(filePath, out var s32Data))
                        {
                            s32Data.Layer8HasExtendedData = false;
                            foreach (var item in s32Data.Layer8)
                            {
                                item.ExtendedData = 0;
                                clearedItems++;
                            }
                            s32Data.IsModified = true;
                            resetCount++;
                        }
                    }
                    MessageBox.Show($"已重設 {resetCount} 個 S32 為一般格式，清除了 {clearedItems} 個項目的 ExtendedData。\n\n請記得儲存 S32 檔案。", "重設完成");
                    UpdateLayer5InvalidButton();
                    resultForm.Close();
                };
                pnlL8Buttons.Controls.Add(btnL8ResetSelected);

                Button btnL8ResetAll = new Button { Text = "全部重設為一般格式", Location = new Point(160, 35), Size = new Size(150, 30), BackColor = Color.Salmon };
                btnL8ResetAll.Click += (s, args) =>
                {
                    if (MessageBox.Show($"確定要將所有 {layer8ExtendedS32.Count} 個 S32 重設為一般格式嗎？\n\n這會清除所有 Layer8 項目的 ExtendedData。",
                        "確認重設全部", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                    int resetCount = 0;
                    int clearedItems = 0;
                    foreach (var (filePath, _, _) in layer8ExtendedS32)
                    {
                        if (_document.S32Files.TryGetValue(filePath, out var s32Data))
                        {
                            s32Data.Layer8HasExtendedData = false;
                            foreach (var item in s32Data.Layer8)
                            {
                                item.ExtendedData = 0;
                                clearedItems++;
                            }
                            s32Data.IsModified = true;
                            resetCount++;
                        }
                    }
                    MessageBox.Show($"已重設 {resetCount} 個 S32 為一般格式，清除了 {clearedItems} 個項目的 ExtendedData。\n\n請記得儲存 S32 檔案。", "重設完成");
                    UpdateLayer5InvalidButton();
                    resultForm.Close();
                };
                pnlL8Buttons.Controls.Add(btnL8ResetAll);
            }

            // 關閉按鈕
            Button btnClose = new Button();
            btnClose.Text = "關閉";
            btnClose.Location = new Point(resultForm.ClientSize.Width - 90, resultForm.ClientSize.Height - 40);
            btnClose.Size = new Size(80, 30);
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Click += (s, args) => resultForm.Close();
            resultForm.Controls.Add(btnClose);

            resultForm.ShowDialog();
        }

        // 查看與管理第七層（傳送點）資料 - 支援編輯
        private void btnToolCheckL7_Click(object sender, EventArgs e)
        {
            if (_document.S32Files.Count == 0)
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 收集有 Layer7 資料的 S32
            List<(string filePath, string fileName, int count, List<Layer7Item> items)> s32WithL7 =
                new List<(string, string, int, List<Layer7Item>)>();

            foreach (var kvp in _document.S32Files)
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
                if (_document.S32Files.TryGetValue(filePath, out S32Data s32Data))
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
                foreach (var kvp in _document.S32Files)
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
                    string selectedFilePath = _document.S32Files.Keys.FirstOrDefault(k => Path.GetFileName(k) == selectedFileName);

                    if (selectedFilePath != null && _document.S32Files.TryGetValue(selectedFilePath, out S32Data s32Data))
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
                    if (_document.S32Files.TryGetValue(kvp.Key, out S32Data s32Data))
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
                    if (_document.S32Files.TryGetValue(filePath, out S32Data s32Data))
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
            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
            {
                MessageBox.Show("請先載入地圖", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Struct.L1Map currentMap = Share.MapDataList[_document.MapId];

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
                if (_document.S32Files.Keys.Any(k => k.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)))
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
                _document.S32Files[filePath] = newS32Data;

                // 寫入檔案
                try
                {
                    SaveS32File(filePath);  // 使用正確格式的保存方法
                }
                catch (Exception ex)
                {
                    // 移除失敗的 S32
                    _document.S32Files.Remove(filePath);
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
