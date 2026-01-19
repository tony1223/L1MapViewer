using System;
using System.ComponentModel;
using System.Diagnostics;
// using System.Drawing; // Replaced with Eto.Drawing
using System.Threading;
using Eto.Forms;
using Eto.Drawing;
using Eto.SkiaDraw;
using L1MapViewer.Compatibility;
using L1MapViewer.Models;
using L1MapViewer.Rendering;
using NLog;
using SkiaSharp;

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
        private MapSkiaDrawable _mapDrawable;
        private readonly MapRenderingCore _renderingCore;

        // ViewState（可由外部注入或內部建立）
        private ViewState _viewState;

        // 渲染相關
        private SKBitmap _skViewportBitmap;
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
        /// SK 繪製覆蓋層 callback（讓 MapForm 繪製 Layer8、選取格子等覆蓋層）
        /// 參數: (SKCanvas canvas, float zoomLevel, int scrollX, int scrollY)
        /// </summary>
        public Action<SKCanvas, float, int, int> PaintOverlaySK { get; set; }

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
        /// 設定外部渲染的 SKBitmap（用於 SkiaSharp 渲染整合）
        /// </summary>
        public void SetExternalBitmap(SKBitmap skBitmap)
        {
            var sw = Stopwatch.StartNew();
            int threadId = Environment.CurrentManagedThreadId;
            _logger.Debug($"[UI] SetExternalBitmap start: skBitmap={skBitmap?.Width}x{skBitmap?.Height}, threadId={threadId}");
            Console.WriteLine($"[MapViewerControl.SetExternalBitmap] skBitmap={skBitmap?.Width}x{skBitmap?.Height}, RenderWidth={_viewState.RenderWidth}, RenderHeight={_viewState.RenderHeight}, Drawable.Size={_mapDrawable.Width}x{_mapDrawable.Height}");

            SKBitmap oldBitmap = null;
            lock (_viewportBitmapLock)
            {
                oldBitmap = _skViewportBitmap;
                _skViewportBitmap = skBitmap;
            }

            // 先 Invalidate，再 Dispose 舊的 bitmap（避免 OnPaint 存取到 disposed bitmap）
            _logger.Debug($"[UI] SetExternalBitmap calling Invalidate after {sw.ElapsedMilliseconds}ms, oldBitmap={oldBitmap?.Width}x{oldBitmap?.Height}, threadId={threadId}, DrawableVisible={_mapDrawable.Visible}, DrawableSize={_mapDrawable.Width}x{_mapDrawable.Height}");

            // 使用 Invalidate 標記需要重繪
            _logger.Debug($"[UI] SetExternalBitmap calling _mapDrawable.Invalidate, _mapDrawable.Visible={_mapDrawable.Visible}");
            _mapDrawable.Invalidate();
            _logger.Debug("[UI] SetExternalBitmap _mapDrawable.Invalidate returned");

            // 確認設定後的狀態
            lock (_viewportBitmapLock)
            {
                _logger.Debug($"[UI] SetExternalBitmap after set: _skViewportBitmap={_skViewportBitmap?.Width}x{_skViewportBitmap?.Height}, IsNull={_skViewportBitmap == null}");
            }

            // 強制同步重繪（Invalidate 只標記需重繪，Update 才會立即執行）
            _logger.Debug("[UI] SetExternalBitmap calling Update to force synchronous repaint");
            try
            {
                // 嘗試使用 Refresh 來強制重繪 (Refresh = Invalidate + Update)
                // Eto.Forms 可能不支援 Update，改用多次 Invalidate
                _mapDrawable.Invalidate();
                // 使用 SuspendLayout/ResumeLayout 來確保布局更新
                _mapPanel?.Invalidate();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[UI] SetExternalBitmap Update failed");
            }

            // 額外使用 AsyncInvoke 確保訊息迴圈有機會處理重繪
            Eto.Forms.Application.Instance.AsyncInvoke(() =>
            {
                lock (_viewportBitmapLock)
                {
                    _logger.Debug($"[UI] SetExternalBitmap AsyncInvoke callback: _skViewportBitmap={_skViewportBitmap?.Width}x{_skViewportBitmap?.Height}, IsNull={_skViewportBitmap == null}");
                }
                _logger.Debug($"[UI] AsyncInvoke: _mapDrawable.Visible={_mapDrawable.Visible}, Parent={_mapDrawable.Parent?.GetType().Name}, Size={_mapDrawable.Width}x{_mapDrawable.Height}");
                // 再次 Invalidate 確保重繪
                _mapDrawable.Invalidate();
                _logger.Debug("[UI] AsyncInvoke: Invalidate called");
            });

            // 現在可以安全 dispose 舊 bitmap（在 Invalidate 之後）
            oldBitmap?.Dispose();

            sw.Stop();
            _logger.Debug($"[UI] SetExternalBitmap complete in {sw.ElapsedMilliseconds}ms, threadId={threadId}");
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
            _mapDrawable.Invalidate();
        }

        /// <summary>
        /// 只重繪動畫覆蓋層（用於 L8 動畫等持續更新的內容）
        /// 不重新渲染地圖，只觸發 Paint 事件（bitmap 已快取，重繪很快）
        /// </summary>
        public void InvalidateAnimationOverlay()
        {
            _mapDrawable?.Invalidate();
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

            // 建立 SkiaDrawable（直接用 SKCanvas 繪製，不需轉換）
            _mapDrawable = new MapSkiaDrawable(this);

            // 註冊事件
            _mapDrawable.MouseDown += MapDrawable_MouseDown;
            _mapDrawable.MouseMove += MapDrawable_MouseMove;
            _mapDrawable.MouseUp += MapDrawable_MouseUp;
            _mapDrawable.MouseDoubleClick += MapDrawable_MouseDoubleClick;
            _mapPanel.MouseWheel += MapPanel_MouseWheel;
            _mapPanel.Resize += MapPanel_Resize;

            _mapPanel.GetControls().Add(_mapDrawable);
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

        private void MapDrawable_MouseDown(object sender, MouseEventArgs e)
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

        private void MapDrawable_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                int deltaX = e.X() - _dragStartPoint.X;
                int deltaY = e.Y() - _dragStartPoint.Y;

                int newScrollX = _dragStartScroll.X - (int)(deltaX / _viewState.ZoomLevel);
                int newScrollY = _dragStartScroll.Y - (int)(deltaY / _viewState.ZoomLevel);

                _viewState.SetScrollSilent(newScrollX, newScrollY);
                _mapDrawable.Invalidate();
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

        private void MapDrawable_MouseUp(object sender, MouseEventArgs e)
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

        private void MapDrawable_MouseDoubleClick(object sender, MouseEventArgs e)
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
            _mapDrawable.Size = new Eto.Drawing.Size(_mapPanel.Width, _mapPanel.Height);

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
                        // 注意：這是內部 Render 路徑，目前使用外部 SetExternalBitmap，此程式碼可能不執行
                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            // 內部渲染返回的是 Eto.Bitmap，需要轉換（或者暫時不支援）
                            // 目前改為使用外部 SKBitmap，此路徑暫時不支援
                            _logger.Warn("[Internal Render] This path is deprecated. Use SetExternalBitmap with SKBitmap instead.");
                            bitmap?.Dispose();

                            _viewState.SetRenderResult(
                                worldRect.X, worldRect.Y,
                                worldRect.Width, worldRect.Height,
                                _viewState.ZoomLevel);

                            _mapDrawable.Invalidate();
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
                _mapDrawable.Invalidate();
            }
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
                    _skViewportBitmap?.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        #endregion

        #region 內部 SkiaDrawable 類

        /// <summary>
        /// 地圖繪製控件 - 使用 SkiaSharp 直接繪製，無需轉換
        /// </summary>
        private class MapSkiaDrawable : SkiaDrawable
        {
            private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
            private readonly MapViewerControl _parent;

            public MapSkiaDrawable(MapViewerControl parent)
            {
                _parent = parent;
                BackgroundColor = Eto.Drawing.Colors.Black;
            }

            protected override void OnPaint(SKPaintEventArgs e)
            {
                var sw = Stopwatch.StartNew();
                var canvas = e.Surface.Canvas;

                // 清除背景
                canvas.Clear(SKColors.Black);

                // 詳細記錄 bitmap 狀態
                SKBitmap bitmapRef;
                lock (_parent._viewportBitmapLock)
                {
                    bitmapRef = _parent._skViewportBitmap;
                }
                bool bitmapIsNull = bitmapRef == null;
                bool bitmapIsDisposed = false;
                int bitmapWidth = 0, bitmapHeight = 0;
                if (!bitmapIsNull)
                {
                    try
                    {
                        bitmapWidth = bitmapRef.Width;
                        bitmapHeight = bitmapRef.Height;
                    }
                    catch (ObjectDisposedException)
                    {
                        bitmapIsDisposed = true;
                    }
                }
                _logger.Debug($"[UI] MapSkiaDrawable.OnPaint start: bitmapIsNull={bitmapIsNull}, bitmapIsDisposed={bitmapIsDisposed}, bitmapSize={bitmapWidth}x{bitmapHeight}, RenderWidth={_parent._viewState.RenderWidth}");

                // DEBUG: 繪製邊框確認 Paint 被呼叫
                using (var borderPaint = new SKPaint { Color = SKColors.Yellow, Style = SKPaintStyle.Stroke, StrokeWidth = 2 })
                {
                    canvas.DrawRect(1, 1, Width - 3, Height - 3, borderPaint);
                }

                lock (_parent._viewportBitmapLock)
                {
                    if (_parent._skViewportBitmap != null && _parent._viewState.RenderWidth > 0)
                    {
                        float drawX = (float)((_parent._viewState.RenderOriginX - _parent._viewState.ScrollX) * _parent._viewState.ZoomLevel);
                        float drawY = (float)((_parent._viewState.RenderOriginY - _parent._viewState.ScrollY) * _parent._viewState.ZoomLevel);
                        float drawW = (float)(_parent._viewState.RenderWidth * _parent._viewState.ZoomLevel);
                        float drawH = (float)(_parent._viewState.RenderHeight * _parent._viewState.ZoomLevel);

                        var drawSw = Stopwatch.StartNew();

                        // 直接用 SKCanvas 繪製 SKBitmap - 零轉換！
                        var destRect = new SKRect(drawX, drawY, drawX + drawW, drawY + drawH);
                        using (var paint = new SKPaint { FilterQuality = SKFilterQuality.None }) // NearestNeighbor
                        {
                            canvas.DrawBitmap(_parent._skViewportBitmap, destRect, paint);
                        }

                        drawSw.Stop();
                        _logger.Debug($"[UI] DrawBitmap took {drawSw.ElapsedMilliseconds}ms for {drawW}x{drawH}");

                        // DEBUG: 如果 bitmap 沒有覆蓋整個 viewport，顯示提示
                        float bitmapRightEdge = drawX + drawW;
                        float gap = Width - bitmapRightEdge;
                        if (gap > 10)
                        {
                            using (var textPaint = new SKPaint { Color = SKColors.Orange, TextSize = 12 })
                            {
                                string info = $"Gap: {gap:F0}px | RenderOrigin=({_parent._viewState.RenderOriginX},{_parent._viewState.RenderOriginY})";
                                canvas.DrawText(info, bitmapRightEdge + 5, 30, textPaint);
                            }
                        }
                    }
                    else
                    {
                        // DEBUG: 顯示狀態資訊
                        string debugInfo = $"skBitmap={(_parent._skViewportBitmap != null)}, RenderW={_parent._viewState.RenderWidth}";
                        using (var textPaint = new SKPaint { Color = SKColors.Red, TextSize = 14 })
                        {
                            canvas.DrawText(debugInfo, 10, 20, textPaint);
                        }
                    }
                }

                // 讓外部繪製覆蓋層（選取格子等編輯層）
                var overlaySw = Stopwatch.StartNew();
                _parent.PaintOverlaySK?.Invoke(canvas, (float)_parent._viewState.ZoomLevel, _parent._viewState.ScrollX, _parent._viewState.ScrollY);
                overlaySw.Stop();
                if (overlaySw.ElapsedMilliseconds > 0)
                    _logger.Debug($"[UI] PaintOverlaySK took {overlaySw.ElapsedMilliseconds}ms");

                sw.Stop();
                _logger.Debug($"[UI] MapSkiaDrawable.OnPaint complete: total={sw.ElapsedMilliseconds}ms");
            }
        }

        #endregion
    }
}
