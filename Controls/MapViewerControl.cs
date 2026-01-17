using System;
using System.ComponentModel;
using System.Diagnostics;
// using System.Drawing; // Replaced with Eto.Drawing
using System.Threading;
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.Compatibility;
using L1MapViewer.Models;
using L1MapViewer.Rendering;
using NLog;

namespace L1MapViewer.Controls
{
    /// <summary>
    /// 地圖檢視器控件 - 封裝渲染和使用者互動
    /// 支援拖曳、縮放、捲動等基本檢視功能
    /// </summary>
    public class MapViewerControl : UserControl
    {
        #region 私有欄位

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private Panel _mapPanel;
        private PictureBox _mapPictureBox;
        private readonly MapRenderingCore _renderingCore;

        // ViewState（可由外部注入或內部建立）
        private ViewState _viewState;

        // 渲染相關
        private Bitmap _viewportBitmap;
        private readonly object _viewportBitmapLock = new object();
        private CancellationTokenSource _renderCts;

        // 拖曳相關
        private bool _isDragging;
        private Point _dragStartPoint;
        private Point _dragStartScroll;

        // 防抖計時器
        private Timer _zoomDebounceTimer;
        private Timer _dragRenderTimer;

        // 縮放控制面板（Google Maps 風格）
        private Panel _zoomControlPanel;
        private Button _btnZoomIn;
        private Button _btnZoomOut;
        private Button _btnZoomReset;

        // 地圖資料
        private MapDocument _document;
        private RenderOptions _renderOptions = RenderOptions.Default;

        #endregion

        #region 公開屬性

        /// <summary>
        /// 地圖文件
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public MapDocument Document => _document;

        /// <summary>
        /// 檢視狀態
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ViewState ViewState => _viewState;

        /// <summary>
        /// 當前縮放級別
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double ZoomLevel
        {
            get => _viewState.ZoomLevel;
            set
            {
                var oldValue = _viewState.ZoomLevel;
                _viewState.ZoomLevel = Math.Max(_viewState.ZoomMin, Math.Min(_viewState.ZoomMax, value));
                if (Math.Abs(_viewState.ZoomLevel - oldValue) > 0.001)
                {
                    ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(_viewState.ZoomLevel, oldValue));
                    RequestRender();
                }
            }
        }

        /// <summary>
        /// 渲染選項
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public RenderOptions RenderOptions
        {
            get => _renderOptions;
            set
            {
                _renderOptions = value;
                RequestRender();
            }
        }

        /// <summary>
        /// 是否正在拖曳
        /// </summary>
        [Browsable(false)]
        public bool IsDragging => _isDragging;

        /// <summary>
        /// 渲染緩衝區邊距
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int RenderBufferMargin
        {
            get => _viewState.RenderBufferMargin;
            set => _viewState.RenderBufferMargin = value;
        }

        /// <summary>
        /// 是否顯示縮放控制按鈕
        /// </summary>
        [Browsable(true)]
        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowZoomControls
        {
            get => _zoomControlPanel?.Visible ?? false;
            set
            {
                if (_zoomControlPanel != null)
                    _zoomControlPanel.Visible = value;
            }
        }

        #endregion

        #region 事件

        /// <summary>
        /// 座標變更事件（滑鼠移動時）
        /// </summary>
        public event EventHandler<CoordinateChangedEventArgs> CoordinateChanged;

        /// <summary>
        /// 渲染完成事件
        /// </summary>
        public event EventHandler<RenderCompletedEventArgs> RenderCompleted;

        /// <summary>
        /// 縮放變更事件
        /// </summary>
        public event EventHandler<ZoomChangedEventArgs> ZoomChanged;

        /// <summary>
        /// 捲動變更事件
        /// </summary>
        public event EventHandler ScrollChanged;

        /// <summary>
        /// 地圖滑鼠按下事件（轉發給 MapForm 處理編輯）
        /// </summary>
        public event EventHandler<MapMouseEventArgs> MapMouseDown;

        /// <summary>
        /// 地圖滑鼠移動事件（轉發給 MapForm 處理編輯）
        /// </summary>
        public event EventHandler<MapMouseEventArgs> MapMouseMove;

        /// <summary>
        /// 地圖滑鼠放開事件（轉發給 MapForm 處理編輯）
        /// </summary>
        public event EventHandler<MapMouseEventArgs> MapMouseUp;

        /// <summary>
        /// 地圖滑鼠雙擊事件（轉發給 MapForm 處理編輯）
        /// </summary>
        public event EventHandler<MapMouseEventArgs> MapMouseDoubleClick;

        /// <summary>
        /// 繪製覆蓋層事件（讓 MapForm 繪製 L8 動畫和編輯層）
        /// </summary>
        public event EventHandler<PaintEventArgs> PaintOverlay;

        #endregion

        #region 建構函式

        /// <summary>
        /// 建立 MapViewerControl
        /// </summary>
        public MapViewerControl()
        {
            _renderingCore = new MapRenderingCore();
            _viewState = new ViewState();

            InitializeComponents();
            InitializeTimers();
            SetupZoomControls();
        }

        /// <summary>
        /// 建立 MapViewerControl（共享 ViewState）
        /// </summary>
        public MapViewerControl(ViewState viewState) : this()
        {
            if (viewState != null)
            {
                _viewState = viewState;
            }
        }

        #endregion

        #region 公開方法

        /// <summary>
        /// 設定共享的 ViewState（用於與外部同步）
        /// </summary>
        public void SetViewState(ViewState viewState)
        {
            if (viewState != null)
            {
                Console.WriteLine($"[MapViewerControl.SetViewState] replacing ViewState, old hashcode={_viewState?.GetHashCode()}, new hashcode={viewState.GetHashCode()}");

                // 取消訂閱舊的事件
                if (_viewState != null)
                {
                    _viewState.ZoomChanged -= ViewState_ZoomChanged;
                }

                _viewState = viewState;

                // 訂閱新的事件以同步按鈕文字
                _viewState.ZoomChanged += ViewState_ZoomChanged;

                // 立即更新按鈕文字
                UpdateZoomButtonText();
            }
        }

        private void ViewState_ZoomChanged(object sender, EventArgs e)
        {
            UpdateZoomButtonText();
        }

        /// <summary>
        /// 設定外部渲染的 Bitmap（用於舊版渲染整合）
        /// </summary>
        public void SetExternalBitmap(Bitmap bitmap)
        {
            var sw = Stopwatch.StartNew();
            _logger.Debug($"[UI] SetExternalBitmap start: bitmap={bitmap?.Width}x{bitmap?.Height}");
            Console.WriteLine($"[MapViewerControl.SetExternalBitmap] bitmap={bitmap?.Width}x{bitmap?.Height}, RenderWidth={_viewState.RenderWidth}, RenderHeight={_viewState.RenderHeight}, PictureBox.Size={_mapPictureBox.Width}x{_mapPictureBox.Height}, Visible={_mapPictureBox.Visible}");
            lock (_viewportBitmapLock)
            {
                _viewportBitmap?.Dispose();
                _viewportBitmap = bitmap;
            }
            _logger.Debug($"[UI] SetExternalBitmap calling Invalidate after {sw.ElapsedMilliseconds}ms");
            _mapPictureBox.Invalidate();
            sw.Stop();
            _logger.Debug($"[UI] SetExternalBitmap complete in {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// 載入地圖
        /// </summary>
        public void LoadMap(MapDocument document)
        {
            _document = document;

            // 更新 ViewState
            if (document.MapPixelWidth > 0 && document.MapPixelHeight > 0)
            {
                _viewState.MapWidth = document.MapPixelWidth;
                _viewState.MapHeight = document.MapPixelHeight;
            }

            // 確保 Viewport 尺寸有效
            if (_mapPanel.Width > 0 && _mapPanel.Height > 0)
            {
                _viewState.ViewportWidth = _mapPanel.Width;
                _viewState.ViewportHeight = _mapPanel.Height;
            }

            _viewState.UpdateScrollLimits(_viewState.MapWidth, _viewState.MapHeight);

            // 清除快取
            _renderingCore.ClearCache();

            // 觸發渲染
            RequestRender();
        }

        /// <summary>
        /// 捲動到指定世界座標（將該座標置中）
        /// </summary>
        public void ScrollTo(int worldX, int worldY)
        {
            _viewState.ScrollToCenter(worldX, worldY);
            RequestRenderIfNeeded();
        }

        /// <summary>
        /// 強制重新渲染
        /// </summary>
        public new void Refresh()
        {
            RequestRender(forceRerender: true);
        }

        /// <summary>
        /// 只重繪覆蓋層，不重新渲染地圖
        /// 用於選取區域變更等不需要重新渲染地圖的情況
        /// </summary>
        public void InvalidateOverlay()
        {
            _mapPictureBox.Invalidate();
        }

        /// <summary>
        /// 只重繪動畫覆蓋層（用於 L8 動畫等持續更新的內容）
        /// 不重新渲染地圖，只觸發 Paint 事件（bitmap 已快取，重繪很快）
        /// </summary>
        public void InvalidateAnimationOverlay()
        {
            _mapPictureBox?.Invalidate();
        }

        /// <summary>
        /// 使指定 S32 的快取失效
        /// </summary>
        public void InvalidateS32(string filePath)
        {
            _renderingCore.InvalidateS32Cache(filePath);
        }

        /// <summary>
        /// 螢幕座標轉世界座標
        /// </summary>
        public Point ScreenToWorld(Point screenPoint)
        {
            return _viewState.ScreenToWorld(screenPoint.X, screenPoint.Y);
        }

        /// <summary>
        /// 世界座標轉螢幕座標
        /// </summary>
        public Point WorldToScreen(Point worldPoint)
        {
            return _viewState.WorldToScreen(worldPoint.X, worldPoint.Y);
        }

        /// <summary>
        /// 清除渲染快取
        /// </summary>
        public void ClearCache()
        {
            _renderingCore.ClearCache();
        }

        #endregion

        #region 初始化

        private void InitializeComponents()
        {
            this.SuspendLayout();

            // 啟用雙緩衝
            this.SetDoubleBuffered(true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            // 建立 Panel
            _mapPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                BackColor = Eto.Drawing.Colors.Black
            };

            // 建立 PictureBox
            _mapPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Normal,
                BackColor = Colors.Black
            };

            // 註冊事件
            _mapPictureBox.Paint += MapPictureBox_Paint;
            _mapPictureBox.MouseDown += MapPictureBox_MouseDown;
            _mapPictureBox.MouseMove += MapPictureBox_MouseMove;
            _mapPictureBox.MouseUp += MapPictureBox_MouseUp;
            _mapPictureBox.MouseDoubleClick += MapPictureBox_MouseDoubleClick;
            _mapPanel.MouseWheel += MapPanel_MouseWheel;
            _mapPanel.Resize += MapPanel_Resize;

            _mapPanel.GetControls().Add(_mapPictureBox);
            this.GetControls().Add(_mapPanel);

            this.ResumeLayout(false);
        }

        private void InitializeTimers()
        {
            _zoomDebounceTimer = new Timer { Interval = 150 };
            _zoomDebounceTimer.Tick += ZoomDebounceTimer_Tick;

            _dragRenderTimer = new Timer { Interval = 100 };
            _dragRenderTimer.Tick += DragRenderTimer_Tick;
        }

        private void SetupZoomControls()
        {
            // 建立容器面板
            _zoomControlPanel = new Panel
            {
                Size = new Size(48, 118),
                BackColor = Colors.Transparent,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            };

            // + 按鈕
            _btnZoomIn = CreateZoomButton("+", 0);
            _btnZoomIn.Click += (s, e) =>
            {
                var oldZoom = _viewState.ZoomLevel;
                _viewState.ZoomIn();
                if (Math.Abs(_viewState.ZoomLevel - oldZoom) > 0.001)
                {
                    UpdateZoomButtonText();
                    ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(_viewState.ZoomLevel, oldZoom));
                    RequestRender();
                }
            };

            // - 按鈕
            _btnZoomOut = CreateZoomButton("\u2212", 40); // Unicode minus sign
            _btnZoomOut.Click += (s, e) =>
            {
                var oldZoom = _viewState.ZoomLevel;
                _viewState.ZoomOut();
                if (Math.Abs(_viewState.ZoomLevel - oldZoom) > 0.001)
                {
                    UpdateZoomButtonText();
                    ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(_viewState.ZoomLevel, oldZoom));
                    RequestRender();
                }
            };

            // 縮放比例按鈕（點擊重置為 1:1）
            _btnZoomReset = CreateZoomButton("1:1", 80);
            _btnZoomReset.Click += (s, e) =>
            {
                var oldZoom = _viewState.ZoomLevel;
                _viewState.ResetZoom();
                if (Math.Abs(_viewState.ZoomLevel - oldZoom) > 0.001)
                {
                    UpdateZoomButtonText();
                    ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(_viewState.ZoomLevel, oldZoom));
                    RequestRender();
                }
            };

            _zoomControlPanel.GetControls().Add(_btnZoomIn);
            _zoomControlPanel.GetControls().Add(_btnZoomOut);
            _zoomControlPanel.GetControls().Add(_btnZoomReset);

            // 設定初始位置（使用預設值，稍後會在 Resize 時更新）
            _zoomControlPanel.Location = new Point(10, 10);

            _mapPanel.GetControls().Add(_zoomControlPanel);
            _zoomControlPanel.BringToFront();

            // 設定位置並監聽 Resize
            UpdateZoomControlPosition();
            _mapPanel.Resize += (s, e) => UpdateZoomControlPosition();
        }

        private void UpdateZoomButtonText()
        {
            if (_btnZoomReset != null)
            {
                int pct = (int)Math.Round(_viewState.ZoomLevel * 100);
                _btnZoomReset.Text = pct == 100 ? "1:1" : $"{pct}%";
            }
        }

        private Button CreateZoomButton(string text, int top)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(48, 38),
                Location = new Point(0, top),
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.White,
                ForeColor = Color.FromArgb(102, 102, 102),
                Cursor = Cursors.Hand,
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(218, 218, 218);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 245, 245);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(235, 235, 235);
            return btn;
        }

        private void UpdateZoomControlPosition()
        {
            if (_zoomControlPanel != null && _mapPanel != null)
            {
                _zoomControlPanel.SetLocation(new Point(10, _mapPanel.Height - _zoomControlPanel.Height - 10));
            }
        }

        #endregion

        #region 滑鼠事件處理

        private void MapPictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            // 中鍵拖曳
            if (e.GetButton() == MouseButtons.Middle)
            {
                _isDragging = true;
                _dragStartPoint = e.Location.ToPoint();
                _dragStartScroll = new Point(_viewState.ScrollX, _viewState.ScrollY);
                this.Cursor = Cursors.SizeAll;

                // 取消進行中的渲染
                _renderCts?.Cancel();
                _dragRenderTimer.Stop();

                // 也觸發 MapMouseDown 事件，讓外部可以追蹤拖曳狀態
                var worldPoint = ScreenToWorld(e.Location.ToPoint());
                var (gameX, gameY) = WorldToGameCoords(worldPoint);
                MapMouseDown?.Invoke(this, new MapMouseEventArgs(
                    e.Button(), e.Location.ToPoint(), worldPoint, gameX, gameY, 0, ControlCompat.ModifierKeys));
            }
            else
            {
                // 轉發給外部處理編輯
                var worldPoint = ScreenToWorld(e.Location.ToPoint());
                var (gameX, gameY) = WorldToGameCoords(worldPoint);
                MapMouseDown?.Invoke(this, new MapMouseEventArgs(
                    e.Button(), e.Location.ToPoint(), worldPoint, gameX, gameY, 0, ControlCompat.ModifierKeys));
            }
        }

        private void MapPictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                int deltaX = e.X() - _dragStartPoint.X;
                int deltaY = e.Y() - _dragStartPoint.Y;

                int newScrollX = _dragStartScroll.X - (int)(deltaX / _viewState.ZoomLevel);
                int newScrollY = _dragStartScroll.Y - (int)(deltaY / _viewState.ZoomLevel);

                _viewState.SetScrollSilent(newScrollX, newScrollY);
                _mapPictureBox.Invalidate();
            }
            else
            {
                // 更新座標顯示
                var worldPoint = ScreenToWorld(e.Location.ToPoint());
                var (gameX, gameY) = WorldToGameCoords(worldPoint);
                CoordinateChanged?.Invoke(this, new CoordinateChangedEventArgs(worldPoint, gameX, gameY));

                // 轉發給外部處理編輯
                MapMouseMove?.Invoke(this, new MapMouseEventArgs(
                    e.Button(), e.Location.ToPoint(), worldPoint, gameX, gameY, 0, ControlCompat.ModifierKeys));
            }
        }

        private void MapPictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.GetButton() == MouseButtons.Middle && _isDragging)
            {
                _isDragging = false;
                this.Cursor = Cursors.Default;

                // 延遲渲染
                _dragRenderTimer.Start();

                // 也觸發 MapMouseUp 事件，讓外部可以處理拖曳結束
                var worldPoint = ScreenToWorld(e.Location.ToPoint());
                var (gameX, gameY) = WorldToGameCoords(worldPoint);
                MapMouseUp?.Invoke(this, new MapMouseEventArgs(
                    e.Button(), e.Location.ToPoint(), worldPoint, gameX, gameY, 0, ControlCompat.ModifierKeys));
            }
            else
            {
                // 轉發給外部處理編輯
                var worldPoint = ScreenToWorld(e.Location.ToPoint());
                var (gameX, gameY) = WorldToGameCoords(worldPoint);
                MapMouseUp?.Invoke(this, new MapMouseEventArgs(
                    e.Button(), e.Location.ToPoint(), worldPoint, gameX, gameY, 0, ControlCompat.ModifierKeys));
            }
        }

        private void MapPictureBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // 轉發給外部處理雙擊
            var worldPoint = ScreenToWorld(e.Location.ToPoint());
            var (gameX, gameY) = WorldToGameCoords(worldPoint);
            MapMouseDoubleClick?.Invoke(this, new MapMouseEventArgs(
                e.Button(), e.Location.ToPoint(), worldPoint, gameX, gameY, 0, ControlCompat.ModifierKeys));
        }

        private void MapPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            // 縮放改用按鈕控制，滾輪只處理捲動
            if (ControlCompat.ModifierKeys == Keys.Shift)
            {
                // Shift + 滾輪 = 水平捲動
                int scrollAmount = (int)(100 / _viewState.ZoomLevel);
                _viewState.ScrollBy(e.Delta.Height > 0 ? -scrollAmount : scrollAmount, 0);
            }
            else
            {
                // 普通滾輪 = 垂直捲動
                int scrollAmount = (int)(100 / _viewState.ZoomLevel);
                _viewState.ScrollBy(0, e.Delta.Height > 0 ? -scrollAmount : scrollAmount);
            }

            RequestRenderIfNeeded();
            ScrollChanged?.Invoke(this, EventArgs.Empty);
            // 嘗試設置 Handled，避免 Eto.Forms 原生事件類型造成的類型轉換錯誤
            if (e is HandledMouseEventArgs handledArgs)
            {
                handledArgs.Handled = true;
            }
        }

        private void MapPanel_Resize(object sender, EventArgs e)
        {
            // Eto.Forms doesn't support DockStyle.Fill, so we need to manually resize the PictureBox
            _mapPictureBox.Size = new Eto.Drawing.Size(_mapPanel.Width, _mapPanel.Height);

            if (_document != null)
            {
                _viewState.ViewportWidth = _mapPanel.Width;
                _viewState.ViewportHeight = _mapPanel.Height;
                RequestRenderIfNeeded();
            }
        }

        #endregion

        #region 計時器事件

        private void ZoomDebounceTimer_Tick(object sender, EventArgs e)
        {
            _zoomDebounceTimer.Stop();
            // 縮放已在滾輪事件中立即設定，這裡只需要觸發渲染
            RequestRender();
        }

        private void DragRenderTimer_Tick(object sender, EventArgs e)
        {
            _dragRenderTimer.Stop();
            RequestRenderIfNeeded();
        }

        #endregion

        #region 渲染

        private void RequestRender(bool forceRerender = false)
        {
            if (_document == null) return;

            // 確保有有效的地圖尺寸
            if (_viewState.MapWidth <= 0 || _viewState.MapHeight <= 0) return;
            if (_viewState.ViewportWidth <= 0 || _viewState.ViewportHeight <= 0) return;

            // 取消之前的渲染
            _renderCts?.Cancel();
            _renderCts = new CancellationTokenSource();

            var token = _renderCts.Token;
            var worldRect = _viewState.GetRenderWorldRect();

            // 確保 worldRect 有效
            if (worldRect.Width <= 0 || worldRect.Height <= 0) return;

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    var bitmap = await _renderingCore.RenderViewportAsync(
                        worldRect, _document, _renderOptions, token);

                    sw.Stop();

                    if (!token.IsCancellationRequested && bitmap != null)
                    {
                        // 回到 UI 執行緒更新
                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            lock (_viewportBitmapLock)
                            {
                                _viewportBitmap?.Dispose();
                                _viewportBitmap = bitmap;
                            }

                            _viewState.SetRenderResult(
                                worldRect.X, worldRect.Y,
                                worldRect.Width, worldRect.Height,
                                _viewState.ZoomLevel);

                            _mapPictureBox.Invalidate();
                            RenderCompleted?.Invoke(this, new RenderCompletedEventArgs(sw.ElapsedMilliseconds));
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，忽略
                }
            }, token);
        }

        private void RequestRenderIfNeeded()
        {
            if (_viewState.NeedsRerender())
            {
                RequestRender();
            }
            else
            {
                _mapPictureBox.Invalidate();
            }
        }

        private void MapPictureBox_Paint(object sender, PaintEventArgs e)
        {
            var sw = Stopwatch.StartNew();
            _logger.Debug("[UI] MapPictureBox_Paint start");

            // DEBUG: 無條件繪製邊框確認 Paint 被呼叫
            using (var pen = new Pen(Colors.Yellow, 2))
            {
                e.Graphics.DrawRectangle(pen, 1, 1, _mapPictureBox.Width - 3, _mapPictureBox.Height - 3);
            }

            lock (_viewportBitmapLock)
            {
                if (_viewportBitmap != null && _viewState.RenderWidth > 0)
                {
                    int drawX = (int)((_viewState.RenderOriginX - _viewState.ScrollX) * _viewState.ZoomLevel);
                    int drawY = (int)((_viewState.RenderOriginY - _viewState.ScrollY) * _viewState.ZoomLevel);
                    int drawW = (int)(_viewState.RenderWidth * _viewState.ZoomLevel);
                    int drawH = (int)(_viewState.RenderHeight * _viewState.ZoomLevel);

                    // DEBUG: 計算 bitmap 右邊緣和 viewport 右邊緣
                    int bitmapRightEdge = drawX + drawW;
                    int viewportWidth = _mapPictureBox.Width;
                    int gap = viewportWidth - bitmapRightEdge;

                    var drawSw = Stopwatch.StartNew();
                    e.Graphics.SetInterpolationMode(InterpolationMode.NearestNeighbor);
                    e.Graphics.DrawImage(_viewportBitmap, drawX, drawY, drawW, drawH);
                    drawSw.Stop();
                    _logger.Debug($"[UI] DrawImage took {drawSw.ElapsedMilliseconds}ms for {drawW}x{drawH}");

                    // DEBUG: 如果 bitmap 沒有覆蓋整個 viewport，顯示提示
                    if (gap > 10)
                    {
                        using (var font = new Font("Consolas", 9))
                        using (var brush = new SolidBrush(Colors.Orange))
                        {
                            string info = $"Gap: {gap}px | RenderOrigin=({_viewState.RenderOriginX},{_viewState.RenderOriginY}) | Map={_viewState.MapWidth}x{_viewState.MapHeight}";
                            e.Graphics.DrawString(info, font, brush, bitmapRightEdge + 5, 30);
                        }
                    }
                }
                else
                {
                    // DEBUG: 顯示狀態資訊
                    string debugInfo = $"bitmap={(_viewportBitmap != null)}, RenderW={_viewState.RenderWidth}, VS.hash={_viewState.GetHashCode()}";
                    using (var font = new Font("Consolas", 10))
                    using (var brush = new SolidBrush(Colors.Red))
                    {
                        e.Graphics.DrawString(debugInfo, font, brush, 10, 10);
                    }
                }
            }

            // 讓外部繪製覆蓋層（L8 動畫 + 編輯層）
            var overlaySw = Stopwatch.StartNew();
            PaintOverlay?.Invoke(this, e);
            overlaySw.Stop();

            sw.Stop();
            _logger.Debug($"[UI] MapPictureBox_Paint complete: total={sw.ElapsedMilliseconds}ms, overlay={overlaySw.ElapsedMilliseconds}ms");
        }

        #endregion

        #region 座標轉換

        /// <summary>
        /// 世界座標轉遊戲座標（簡化版，需要 S32Data 才能精確計算）
        /// </summary>
        private (int gameX, int gameY) WorldToGameCoords(Point worldPoint)
        {
            // 簡化版：假設從地圖原點開始
            // 實際使用時應該根據 S32Data 計算
            int gameX = worldPoint.X / 48;
            int gameY = worldPoint.Y / 24;
            return (gameX, gameY);
        }

        #endregion

        #region Dispose

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _zoomDebounceTimer?.Dispose();
                _dragRenderTimer?.Dispose();
                _renderCts?.Cancel();
                _renderCts?.Dispose();

                lock (_viewportBitmapLock)
                {
                    _viewportBitmap?.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}
