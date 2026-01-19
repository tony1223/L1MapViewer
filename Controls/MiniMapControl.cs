using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Eto.Forms;
using Eto.Drawing;
using Eto.SkiaDraw;
using SkiaSharp;
using L1MapViewer.Compatibility;
using L1MapViewer.Helper;
using L1MapViewer.Models;
using L1MapViewer.Rendering;
using NLog;

namespace L1MapViewer.Controls
{
    /// <summary>
    /// 小地圖控件 - 顯示整張地圖的縮圖並支援導航
    /// </summary>
    public class MiniMapControl : UserControl
    {
        #region 私有欄位

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private MiniMapSkiaDrawable _skiaDrawable;
        private readonly MapRenderingCore _renderingCore;
        private SKBitmap _skMiniMapBitmap;
        private MiniMapRenderer.MiniMapBounds _bounds;

        private bool _isDragging;
        private bool _isRendering;
        private MapDocument _document;

        #endregion

        #region 公開屬性

        /// <summary>
        /// 關聯的 MapViewerControl
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public MapViewerControl MapViewer { get; set; }

        /// <summary>
        /// 關聯的 ViewState
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ViewState ViewState { get; set; }

        /// <summary>
        /// 小地圖大小
        /// </summary>
        [DefaultValue(200)]
        public int MiniMapSize { get; set; } = 200;

        /// <summary>
        /// 視窗框顏色
        /// </summary>
        [DefaultValue(typeof(Color), "Red")]
        public Color ViewportRectColor { get; set; } = Colors.Red;

        /// <summary>
        /// 視窗框寬度
        /// </summary>
        [DefaultValue(2f)]
        public float ViewportRectWidth { get; set; } = 2f;

        #endregion

        #region 事件

        /// <summary>
        /// 導航請求事件（點擊/拖曳時）
        /// </summary>
        public event EventHandler<Point> NavigateRequested;

        /// <summary>
        /// 右鍵查詢 S32 事件
        /// </summary>
        public event EventHandler<S32QueryEventArgs> S32RightClicked;

        /// <summary>
        /// 渲染開始事件
        /// </summary>
        public event EventHandler RenderStarted;

        /// <summary>
        /// 渲染完成事件
        /// </summary>
        public event EventHandler RenderCompleted;

        #endregion

        #region 事件參數類別

        /// <summary>
        /// S32 查詢事件參數
        /// </summary>
        public class S32QueryEventArgs : EventArgs
        {
            public int WorldX { get; }
            public int WorldY { get; }
            public int GameX { get; }
            public int GameY { get; }
            public string S32FileName { get; }

            public S32QueryEventArgs(int worldX, int worldY, int gameX, int gameY, string s32FileName)
            {
                WorldX = worldX;
                WorldY = worldY;
                GameX = gameX;
                GameY = gameY;
                S32FileName = s32FileName;
            }
        }

        #endregion

        #region 建構函式

        public MiniMapControl()
        {
            _renderingCore = new MapRenderingCore();
            InitializeComponents();

            // 訂閱 TileProvider 變更事件
            TileProvider.Instance.TileChanged += TileProvider_TileChanged;
        }

        public MiniMapControl(MapViewerControl mapViewer) : this()
        {
            MapViewer = mapViewer;
            if (mapViewer != null)
            {
                ViewState = mapViewer.ViewState;
            }
        }

        #endregion

        #region 初始化

        private void InitializeComponents()
        {
            this.Size = new Size(MiniMapSize, MiniMapSize);
            this.BackgroundColor = Colors.Black;
            this.SetTabStop(false); // 不搶奪鍵盤焦點

            // 使用 SkiaDrawable 取代 PictureBox
            _skiaDrawable = new MiniMapSkiaDrawable(this);

            _skiaDrawable.MouseDown += SkiaDrawable_MouseDown;
            _skiaDrawable.MouseMove += SkiaDrawable_MouseMove;
            _skiaDrawable.MouseUp += SkiaDrawable_MouseUp;

            // Eto.Forms doesn't support DockStyle.Fill, so we need to manually resize
            this.Resize += (s, e) =>
            {
                _skiaDrawable.Size = new Size(this.Width, this.Height);
            };

            this.GetControls().Add(_skiaDrawable);

            // Set initial size
            _skiaDrawable.Size = new Size(MiniMapSize, MiniMapSize);
        }

        #endregion

        #region 公開方法

        /// <summary>
        /// 更新小地圖（同步版本）
        /// </summary>
        public void UpdateMiniMap(MapDocument document)
        {
            _document = document;
            if (document == null) return;

            var sw = Stopwatch.StartNew();
            var skBitmap = _renderingCore.RenderMiniMapSK(document, MiniMapSize, out var bounds);
            _bounds = bounds;

            _skMiniMapBitmap?.Dispose();
            _skMiniMapBitmap = skBitmap;
            _skiaDrawable.Invalidate();
            _logger.Debug($"[MiniMap] UpdateMiniMap completed in {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// 更新小地圖（異步版本，先顯示佔位圖）
        /// </summary>
        public void UpdateMiniMapAsync(MapDocument document)
        {
            _document = document;
            if (document == null) return;

            if (_isRendering) return;
            _isRendering = true;

            // 先顯示佔位圖
            ShowPlaceholder();
            RenderStarted?.Invoke(this, EventArgs.Empty);

            // 背景渲染
            Task.Run(() =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var skBitmap = _renderingCore.RenderMiniMapSK(document, MiniMapSize, out var bounds);

                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        _skMiniMapBitmap?.Dispose();
                        _skMiniMapBitmap = skBitmap;
                        _bounds = bounds;
                        _isRendering = false;
                        _skiaDrawable.Invalidate();
                        _logger.Debug($"[MiniMap] UpdateMiniMapAsync completed in {sw.ElapsedMilliseconds}ms");
                        RenderCompleted?.Invoke(this, EventArgs.Empty);
                    });
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "[MiniMap] UpdateMiniMapAsync failed");
                    _isRendering = false;
                }
            });
        }

        /// <summary>
        /// 顯示載入中佔位圖
        /// </summary>
        public void ShowPlaceholder(string text = "小地圖繪製中...")
        {
            // 使用 SKBitmap 繪製佔位圖
            var placeholder = new SKBitmap(MiniMapSize, MiniMapSize, SKColorType.Rgb565, SKAlphaType.Opaque);
            using (var canvas = new SKCanvas(placeholder))
            {
                canvas.Clear(new SKColor(30, 30, 30));
                using (var paint = new SKPaint
                {
                    IsAntialias = true,
                    Color = SKColors.Gray,
                    TextSize = 14,
                    Typeface = SKTypeface.FromFamilyName("Microsoft JhengHei")
                })
                {
                    var bounds = new SKRect();
                    paint.MeasureText(text, ref bounds);
                    float x = (MiniMapSize - bounds.Width) / 2;
                    float y = (MiniMapSize + bounds.Height) / 2;
                    canvas.DrawText(text, x, y, paint);
                }
            }

            _skMiniMapBitmap?.Dispose();
            _skMiniMapBitmap = placeholder;
            _skiaDrawable.Invalidate();
        }

        /// <summary>
        /// 重繪視窗位置框
        /// </summary>
        public void RefreshViewportRect()
        {
            _skiaDrawable.Invalidate();
        }

        /// <summary>
        /// 清除小地圖
        /// </summary>
        public void Clear()
        {
            _skMiniMapBitmap?.Dispose();
            _skMiniMapBitmap = null;
            _document = null;
            _skiaDrawable.Invalidate();
        }

        #endregion

        #region 滑鼠事件

        private void SkiaDrawable_MouseDown(object sender, Eto.Forms.MouseEventArgs e)
        {
            if (e.Buttons == Eto.Forms.MouseButtons.Primary)
            {
                _isDragging = true;
                NavigateToPosition(new Point((int)e.Location.X, (int)e.Location.Y));
            }
            else if (e.Buttons == Eto.Forms.MouseButtons.Alternate)
            {
                HandleRightClick(new Point((int)e.Location.X, (int)e.Location.Y));
            }
        }

        private void SkiaDrawable_MouseMove(object sender, Eto.Forms.MouseEventArgs e)
        {
            if (_isDragging)
            {
                NavigateToPosition(new Point((int)e.Location.X, (int)e.Location.Y));
            }
        }

        private void SkiaDrawable_MouseUp(object sender, Eto.Forms.MouseEventArgs e)
        {
            if (e.Buttons == Eto.Forms.MouseButtons.Primary)
            {
                _isDragging = false;
            }
        }

        private void NavigateToPosition(Point mouseLocation)
        {
            if (ViewState == null || _document == null || _skMiniMapBitmap == null) return;
            if (_bounds == null || _bounds.ContentWidth <= 0 || _bounds.ContentHeight <= 0) return;

            // 計算 minimap bitmap 在控制項上的顯示區域
            float scaleX = (float)this.Width / _skMiniMapBitmap.Width;
            float scaleY = (float)this.Height / _skMiniMapBitmap.Height;
            float displayScale = Math.Min(scaleX, scaleY);

            int drawWidth = (int)(_skMiniMapBitmap.Width * displayScale);
            int drawHeight = (int)(_skMiniMapBitmap.Height * displayScale);
            int offsetX = (this.Width - drawWidth) / 2;
            int offsetY = (this.Height - drawHeight) / 2;

            // 滑鼠位置在 bitmap 顯示區域內的位置
            float miniMapX = (mouseLocation.X - offsetX) / displayScale;
            float miniMapY = (mouseLocation.Y - offsetY) / displayScale;

            // 使用 MiniMapBounds 轉換為世界座標（與 DrawViewportRect 一致）
            var (worldX, worldY) = _bounds.MiniMapToWorld(miniMapX, miniMapY);

            // 限制在有效範圍內
            worldX = Math.Max(0, Math.Min(ViewState.MapWidth, worldX));
            worldY = Math.Max(0, Math.Min(ViewState.MapHeight, worldY));

            // 觸發導航事件
            NavigateRequested?.Invoke(this, new Point(worldX, worldY));

            // 如果有關聯的 MapViewer，直接導航
            MapViewer?.ScrollTo(worldX, worldY);
        }

        private void HandleRightClick(Point mouseLocation)
        {
            if (ViewState == null || _document == null || _skMiniMapBitmap == null) return;
            if (ViewState.MapWidth <= 0 || ViewState.MapHeight <= 0) return;

            // 計算 minimap bitmap 在控制項上的顯示區域
            float scaleX = (float)this.Width / _skMiniMapBitmap.Width;
            float scaleY = (float)this.Height / _skMiniMapBitmap.Height;
            float displayScale = Math.Min(scaleX, scaleY);

            int drawWidth = (int)(_skMiniMapBitmap.Width * displayScale);
            int drawHeight = (int)(_skMiniMapBitmap.Height * displayScale);
            int offsetX = (this.Width - drawWidth) / 2;
            int offsetY = (this.Height - drawHeight) / 2;

            // 檢查是否在有效範圍內
            int clickX = mouseLocation.X - offsetX;
            int clickY = mouseLocation.Y - offsetY;

            if (clickX < 0 || clickY < 0 || clickX > drawWidth || clickY > drawHeight)
                return;

            // 滑鼠位置在 bitmap 顯示區域內的比例
            float ratioX = (float)clickX / drawWidth;
            float ratioY = (float)clickY / drawHeight;

            // 比例 → 世界座標
            int worldX = (int)(ratioX * ViewState.MapWidth);
            int worldY = (int)(ratioY * ViewState.MapHeight);

            // 使用 L1MapHelper 轉換為遊戲座標
            var linLoc = L1MapHelper.GetLinLocation(worldX, worldY);
            if (linLoc == null) return;

            // 計算 S32 檔案名稱
            int blockX = ((linLoc.x - 0x7FFF) / 64) + 0x7FFF;
            int blockY = ((linLoc.y - 0x7FFF) / 64) + 0x7FFF;
            string s32FileName = $"{blockX:X4}{blockY:X4}.s32";

            // 觸發事件
            S32RightClicked?.Invoke(this, new S32QueryEventArgs(
                worldX, worldY, linLoc.x, linLoc.y, s32FileName));
        }

        #endregion

        #region 內部 SkiaDrawable 類

        /// <summary>
        /// 小地圖繪製控件 - 使用 SkiaSharp 直接繪製
        /// </summary>
        private class MiniMapSkiaDrawable : SkiaDrawable
        {
            private readonly MiniMapControl _parent;

            public MiniMapSkiaDrawable(MiniMapControl parent)
            {
                _parent = parent;
                BackgroundColor = Eto.Drawing.Colors.Black;
            }

            protected override void OnPaint(SKPaintEventArgs e)
            {
                var canvas = e.Surface.Canvas;
                canvas.Clear(SKColors.Black);

                var skBitmap = _parent._skMiniMapBitmap;
                if (skBitmap != null)
                {
                    // 計算置中繪製位置
                    float scaleX = (float)Width / skBitmap.Width;
                    float scaleY = (float)Height / skBitmap.Height;
                    float scale = Math.Min(scaleX, scaleY);

                    float drawWidth = skBitmap.Width * scale;
                    float drawHeight = skBitmap.Height * scale;
                    float drawX = (Width - drawWidth) / 2;
                    float drawY = (Height - drawHeight) / 2;

                    // 使用 NearestNeighbor 保持像素清晰
                    var destRect = new SKRect(drawX, drawY, drawX + drawWidth, drawY + drawHeight);
                    using (var paint = new SKPaint { FilterQuality = SKFilterQuality.Low })
                    {
                        canvas.DrawBitmap(skBitmap, destRect, paint);
                    }
                }

                // 繪製視窗位置紅框（渲染中不顯示紅框）
                if (!_parent._isRendering && _parent.ViewState != null && _parent.ViewState.MapWidth > 0)
                {
                    DrawViewportRectSK(canvas);
                }
            }

            private void DrawViewportRectSK(SKCanvas canvas)
            {
                var skBitmap = _parent._skMiniMapBitmap;
                var bounds = _parent._bounds;
                if (skBitmap == null || bounds == null) return;
                if (bounds.ContentWidth <= 0 || bounds.ContentHeight <= 0) return;

                // 計算 minimap bitmap 在控制項上的顯示區域
                float scaleX = (float)Width / skBitmap.Width;
                float scaleY = (float)Height / skBitmap.Height;
                float displayScale = Math.Min(scaleX, scaleY);

                float drawWidth = skBitmap.Width * displayScale;
                float drawHeight = skBitmap.Height * displayScale;
                float offsetX = (Width - drawWidth) / 2;
                float offsetY = (Height - drawHeight) / 2;

                // 計算 viewport 在世界座標中的位置和大小
                int viewportWorldX = _parent.ViewState.ScrollX;
                int viewportWorldY = _parent.ViewState.ScrollY;
                int viewportWorldW = (int)(_parent.ViewState.ViewportWidth / _parent.ViewState.ZoomLevel);
                int viewportWorldH = (int)(_parent.ViewState.ViewportHeight / _parent.ViewState.ZoomLevel);

                // 使用 MiniMapBounds 的座標轉換公式
                var (miniX, miniY) = bounds.WorldToMiniMap(viewportWorldX, viewportWorldY);
                float ratioW = (float)viewportWorldW / bounds.ContentWidth;
                float ratioH = (float)viewportWorldH / bounds.ContentHeight;

                // 映射到顯示區域
                float rectX = offsetX + miniX * displayScale;
                float rectY = offsetY + miniY * displayScale;
                float rectW = ratioW * drawWidth;
                float rectH = ratioH * drawHeight;

                // 畫矩形
                using (var paint = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = _parent.ViewportRectWidth,
                    Color = new SKColor(
                        (byte)(_parent.ViewportRectColor.R * 255),
                        (byte)(_parent.ViewportRectColor.G * 255),
                        (byte)(_parent.ViewportRectColor.B * 255),
                        (byte)(_parent.ViewportRectColor.A * 255))
                })
                {
                    canvas.DrawRect(rectX, rectY, rectW, rectH, paint);
                }
            }
        }

        #endregion

        #region Tile Override 事件處理

        /// <summary>
        /// TileProvider 變更事件處理
        /// </summary>
        private void TileProvider_TileChanged(object sender, TileChangedEventArgs e)
        {
            // 清除相關快取
            _renderingCore.InvalidateTileCache(e.TileIds);

            // 如果有 document，重新渲染小地圖
            if (_document != null)
            {
                // 使用 Application.Instance.Invoke 確保在 UI 執行緒上執行
                Eto.Forms.Application.Instance.Invoke(() =>
                {
                    UpdateMiniMap(_document);
                });
            }
        }

        #endregion

        #region Dispose

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 取消訂閱事件
                TileProvider.Instance.TileChanged -= TileProvider_TileChanged;
                _skMiniMapBitmap?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
