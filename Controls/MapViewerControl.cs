using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using L1MapViewer.Models;
using L1MapViewer.Rendering;

namespace L1MapViewer.Controls
{
    /// <summary>
    /// 地圖檢視器控件 - 封裝渲染和使用者互動
    /// 支援拖曳、縮放、捲動等基本檢視功能
    /// </summary>
    public class MapViewerControl : UserControl
    {
        #region 私有欄位

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
        private System.Windows.Forms.Timer _zoomDebounceTimer;
        private System.Windows.Forms.Timer _dragRenderTimer;
        private double _pendingZoomLevel;

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
        /// 繪製覆蓋層事件（讓 MapForm 繪製編輯層）
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
                _viewState = viewState;
            }
        }

        /// <summary>
        /// 設定外部渲染的 Bitmap（用於舊版渲染整合）
        /// </summary>
        public void SetExternalBitmap(Bitmap bitmap)
        {
            Console.WriteLine($"[MapViewerControl.SetExternalBitmap] bitmap={bitmap?.Width}x{bitmap?.Height}, RenderWidth={_viewState.RenderWidth}, RenderHeight={_viewState.RenderHeight}, PictureBox.Size={_mapPictureBox.Width}x{_mapPictureBox.Height}, Visible={_mapPictureBox.Visible}");
            lock (_viewportBitmapLock)
            {
                _viewportBitmap?.Dispose();
                _viewportBitmap = bitmap;
            }
            _mapPictureBox.Invalidate();
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
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            // 建立 Panel
            _mapPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                BackColor = Color.Black
            };

            // 建立 PictureBox
            _mapPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Normal,
                BackColor = Color.Black
            };

            // 註冊事件
            _mapPictureBox.Paint += MapPictureBox_Paint;
            _mapPictureBox.MouseDown += MapPictureBox_MouseDown;
            _mapPictureBox.MouseMove += MapPictureBox_MouseMove;
            _mapPictureBox.MouseUp += MapPictureBox_MouseUp;
            _mapPictureBox.MouseDoubleClick += MapPictureBox_MouseDoubleClick;
            _mapPanel.MouseWheel += MapPanel_MouseWheel;
            _mapPanel.Resize += MapPanel_Resize;

            _mapPanel.Controls.Add(_mapPictureBox);
            this.Controls.Add(_mapPanel);

            this.ResumeLayout(false);
        }

        private void InitializeTimers()
        {
            _zoomDebounceTimer = new System.Windows.Forms.Timer { Interval = 150 };
            _zoomDebounceTimer.Tick += ZoomDebounceTimer_Tick;

            _dragRenderTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _dragRenderTimer.Tick += DragRenderTimer_Tick;
        }

        #endregion

        #region 滑鼠事件處理

        private void MapPictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            // 中鍵拖曳
            if (e.Button == MouseButtons.Middle)
            {
                _isDragging = true;
                _dragStartPoint = e.Location;
                _dragStartScroll = new Point(_viewState.ScrollX, _viewState.ScrollY);
                this.Cursor = Cursors.SizeAll;

                // 取消進行中的渲染
                _renderCts?.Cancel();
                _dragRenderTimer.Stop();

                // 也觸發 MapMouseDown 事件，讓外部可以追蹤拖曳狀態
                var worldPoint = ScreenToWorld(e.Location);
                var (gameX, gameY) = WorldToGameCoords(worldPoint);
                MapMouseDown?.Invoke(this, new MapMouseEventArgs(
                    e.Button, e.Location, worldPoint, gameX, gameY, 0, Control.ModifierKeys));
            }
            else
            {
                // 轉發給外部處理編輯
                var worldPoint = ScreenToWorld(e.Location);
                var (gameX, gameY) = WorldToGameCoords(worldPoint);
                MapMouseDown?.Invoke(this, new MapMouseEventArgs(
                    e.Button, e.Location, worldPoint, gameX, gameY, 0, Control.ModifierKeys));
            }
        }

        private void MapPictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                int deltaX = e.X - _dragStartPoint.X;
                int deltaY = e.Y - _dragStartPoint.Y;

                int newScrollX = _dragStartScroll.X - (int)(deltaX / _viewState.ZoomLevel);
                int newScrollY = _dragStartScroll.Y - (int)(deltaY / _viewState.ZoomLevel);

                _viewState.SetScrollSilent(newScrollX, newScrollY);
                _mapPictureBox.Invalidate();
            }
            else
            {
                // 更新座標顯示
                var worldPoint = ScreenToWorld(e.Location);
                var (gameX, gameY) = WorldToGameCoords(worldPoint);
                CoordinateChanged?.Invoke(this, new CoordinateChangedEventArgs(worldPoint, gameX, gameY));

                // 轉發給外部處理編輯
                MapMouseMove?.Invoke(this, new MapMouseEventArgs(
                    e.Button, e.Location, worldPoint, gameX, gameY, 0, Control.ModifierKeys));
            }
        }

        private void MapPictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle && _isDragging)
            {
                _isDragging = false;
                this.Cursor = Cursors.Default;

                // 延遲渲染
                _dragRenderTimer.Start();

                // 也觸發 MapMouseUp 事件，讓外部可以處理拖曳結束
                var worldPoint = ScreenToWorld(e.Location);
                var (gameX, gameY) = WorldToGameCoords(worldPoint);
                MapMouseUp?.Invoke(this, new MapMouseEventArgs(
                    e.Button, e.Location, worldPoint, gameX, gameY, 0, Control.ModifierKeys));
            }
            else
            {
                // 轉發給外部處理編輯
                var worldPoint = ScreenToWorld(e.Location);
                var (gameX, gameY) = WorldToGameCoords(worldPoint);
                MapMouseUp?.Invoke(this, new MapMouseEventArgs(
                    e.Button, e.Location, worldPoint, gameX, gameY, 0, Control.ModifierKeys));
            }
        }

        private void MapPictureBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // 轉發給外部處理雙擊
            var worldPoint = ScreenToWorld(e.Location);
            var (gameX, gameY) = WorldToGameCoords(worldPoint);
            MapMouseDoubleClick?.Invoke(this, new MapMouseEventArgs(
                e.Button, e.Location, worldPoint, gameX, gameY, 0, Control.ModifierKeys));
        }

        private void MapPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Control)
            {
                // Ctrl + 滾輪 = 縮放
                double delta = e.Delta > 0 ? 0.2 : -0.2;
                _pendingZoomLevel = Math.Max(_viewState.ZoomMin,
                    Math.Min(_viewState.ZoomMax, _viewState.ZoomLevel + delta));

                _zoomDebounceTimer.Stop();
                _zoomDebounceTimer.Start();
            }
            else if (Control.ModifierKeys == Keys.Shift)
            {
                // Shift + 滾輪 = 水平捲動
                int scrollAmount = (int)(100 / _viewState.ZoomLevel);
                _viewState.ScrollBy(e.Delta > 0 ? -scrollAmount : scrollAmount, 0);
                RequestRenderIfNeeded();
            }
            else
            {
                // 普通滾輪 = 垂直捲動
                int scrollAmount = (int)(100 / _viewState.ZoomLevel);
                _viewState.ScrollBy(0, e.Delta > 0 ? -scrollAmount : scrollAmount);
                RequestRenderIfNeeded();
            }

            ((HandledMouseEventArgs)e).Handled = true;
        }

        private void MapPanel_Resize(object sender, EventArgs e)
        {
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

            var oldZoom = _viewState.ZoomLevel;
            _viewState.ZoomLevel = _pendingZoomLevel;

            if (Math.Abs(_viewState.ZoomLevel - oldZoom) > 0.001)
            {
                ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(_viewState.ZoomLevel, oldZoom));
                RequestRender();
            }
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
            // DEBUG: 無條件繪製邊框確認 Paint 被呼叫
            using (var pen = new Pen(Color.Yellow, 2))
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

                    Console.WriteLine($"[MapViewerControl.Paint] bmp={_viewportBitmap.Width}x{_viewportBitmap.Height}, draw=({drawX},{drawY},{drawW},{drawH}), scroll=({_viewState.ScrollX},{_viewState.ScrollY}), zoom={_viewState.ZoomLevel}");

                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    e.Graphics.DrawImage(_viewportBitmap, drawX, drawY, drawW, drawH);
                }
                else
                {
                    Console.WriteLine($"[MapViewerControl.Paint] SKIP: bitmap={(_viewportBitmap != null)}, RenderWidth={_viewState.RenderWidth}");

                    // DEBUG: 顯示狀態資訊
                    string debugInfo = $"bitmap={(_viewportBitmap != null)}, RenderW={_viewState.RenderWidth}, VS.hash={_viewState.GetHashCode()}";
                    using (var font = new Font("Consolas", 10))
                    using (var brush = new SolidBrush(Color.Red))
                    {
                        e.Graphics.DrawString(debugInfo, font, brush, 10, 10);
                    }
                }
            }

            // 讓外部繪製編輯層
            PaintOverlay?.Invoke(this, e);
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
