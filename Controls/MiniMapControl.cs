using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using L1MapViewer.Helper;
using L1MapViewer.Models;
using L1MapViewer.Rendering;

namespace L1MapViewer.Controls
{
    /// <summary>
    /// 小地圖控件 - 顯示整張地圖的縮圖並支援導航
    /// </summary>
    public class MiniMapControl : UserControl
    {
        #region 私有欄位

        private PictureBox _pictureBox;
        private readonly MapRenderingCore _renderingCore;
        private Bitmap _miniMapBitmap;
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
        public Color ViewportRectColor { get; set; } = Color.Red;

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
            this.BackColor = Color.Black;
            this.TabStop = false; // 不搶奪鍵盤焦點

            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black,
                TabStop = false // 不搶奪鍵盤焦點
            };

            _pictureBox.MouseDown += PictureBox_MouseDown;
            _pictureBox.MouseMove += PictureBox_MouseMove;
            _pictureBox.MouseUp += PictureBox_MouseUp;
            _pictureBox.Paint += PictureBox_Paint;

            this.Controls.Add(_pictureBox);
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

            var bitmap = _renderingCore.RenderMiniMap(document, MiniMapSize, out var bounds);
            _bounds = bounds;

            _miniMapBitmap?.Dispose();
            _miniMapBitmap = bitmap;
            _pictureBox.Invalidate();
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
                    var bitmap = _renderingCore.RenderMiniMap(document, MiniMapSize, out var bounds);

                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        _miniMapBitmap?.Dispose();
                        _miniMapBitmap = bitmap;
                        _bounds = bounds;
                        _isRendering = false;
                        _pictureBox.Invalidate();
                        RenderCompleted?.Invoke(this, EventArgs.Empty);
                    });
                }
                catch
                {
                    _isRendering = false;
                }
            });
        }

        /// <summary>
        /// 顯示載入中佔位圖
        /// </summary>
        public void ShowPlaceholder(string text = "小地圖繪製中...")
        {
            var placeholder = new Bitmap(MiniMapSize, MiniMapSize);
            using (var g = Graphics.FromImage(placeholder))
            {
                g.Clear(Color.FromArgb(30, 30, 30));
                using (var font = new Font("Microsoft JhengHei", 12))
                using (var brush = new SolidBrush(Color.Gray))
                {
                    var size = g.MeasureString(text, font);
                    g.DrawString(text, font, brush,
                        (MiniMapSize - size.Width) / 2,
                        (MiniMapSize - size.Height) / 2);
                }
            }

            _miniMapBitmap?.Dispose();
            _miniMapBitmap = placeholder;
            _pictureBox.Invalidate();
        }

        /// <summary>
        /// 重繪視窗位置框
        /// </summary>
        public void RefreshViewportRect()
        {
            _pictureBox.Invalidate();
        }

        /// <summary>
        /// 清除小地圖
        /// </summary>
        public void Clear()
        {
            _miniMapBitmap?.Dispose();
            _miniMapBitmap = null;
            _document = null;
            _pictureBox.Invalidate();
        }

        #endregion

        #region 滑鼠事件

        private void PictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                NavigateToPosition(e.Location);
            }
            else if (e.Button == MouseButtons.Right)
            {
                HandleRightClick(e.Location);
            }
        }

        private void PictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                NavigateToPosition(e.Location);
            }
        }

        private void PictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = false;
            }
        }

        private void NavigateToPosition(Point mouseLocation)
        {
            if (ViewState == null || _document == null || _miniMapBitmap == null) return;
            if (_bounds == null || _bounds.ContentWidth <= 0 || _bounds.ContentHeight <= 0) return;

            // 計算 minimap bitmap 在控制項上的顯示區域
            float scaleX = (float)this.Width / _miniMapBitmap.Width;
            float scaleY = (float)this.Height / _miniMapBitmap.Height;
            float displayScale = Math.Min(scaleX, scaleY);

            int drawWidth = (int)(_miniMapBitmap.Width * displayScale);
            int drawHeight = (int)(_miniMapBitmap.Height * displayScale);
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
            if (ViewState == null || _document == null || _miniMapBitmap == null) return;
            if (ViewState.MapWidth <= 0 || ViewState.MapHeight <= 0) return;

            // 計算 minimap bitmap 在控制項上的顯示區域
            float scaleX = (float)this.Width / _miniMapBitmap.Width;
            float scaleY = (float)this.Height / _miniMapBitmap.Height;
            float displayScale = Math.Min(scaleX, scaleY);

            int drawWidth = (int)(_miniMapBitmap.Width * displayScale);
            int drawHeight = (int)(_miniMapBitmap.Height * displayScale);
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

        #region 繪製

        private void PictureBox_Paint(object sender, PaintEventArgs e)
        {
            if (_miniMapBitmap != null)
            {
                // 計算置中繪製位置
                float scaleX = (float)this.Width / _miniMapBitmap.Width;
                float scaleY = (float)this.Height / _miniMapBitmap.Height;
                float scale = Math.Min(scaleX, scaleY);

                int drawWidth = (int)(_miniMapBitmap.Width * scale);
                int drawHeight = (int)(_miniMapBitmap.Height * scale);
                int drawX = (this.Width - drawWidth) / 2;
                int drawY = (this.Height - drawHeight) / 2;

                e.Graphics.DrawImage(_miniMapBitmap, drawX, drawY, drawWidth, drawHeight);
            }

            // 繪製視窗位置紅框（渲染中不顯示紅框）
            if (!_isRendering && ViewState != null && ViewState.MapWidth > 0)
            {
                DrawViewportRect(e.Graphics);
            }
        }

        private void DrawViewportRect(Graphics g)
        {
            if (_miniMapBitmap == null || _bounds == null) return;
            if (_bounds.ContentWidth <= 0 || _bounds.ContentHeight <= 0) return;

            // 計算 minimap bitmap 在控制項上的顯示區域
            float scaleX = (float)this.Width / _miniMapBitmap.Width;
            float scaleY = (float)this.Height / _miniMapBitmap.Height;
            float displayScale = Math.Min(scaleX, scaleY);

            int drawWidth = (int)(_miniMapBitmap.Width * displayScale);
            int drawHeight = (int)(_miniMapBitmap.Height * displayScale);
            int offsetX = (this.Width - drawWidth) / 2;
            int offsetY = (this.Height - drawHeight) / 2;

            // 計算 viewport 在世界座標中的位置和大小
            int viewportWorldX = ViewState.ScrollX;
            int viewportWorldY = ViewState.ScrollY;
            int viewportWorldW = (int)(ViewState.ViewportWidth / ViewState.ZoomLevel);
            int viewportWorldH = (int)(ViewState.ViewportHeight / ViewState.ZoomLevel);

            // 使用 MiniMapBounds 的座標轉換公式
            var (miniX, miniY) = _bounds.WorldToMiniMap(viewportWorldX, viewportWorldY);
            float ratioW = (float)viewportWorldW / _bounds.ContentWidth;
            float ratioH = (float)viewportWorldH / _bounds.ContentHeight;

            // 映射到顯示區域
            float rectX = offsetX + miniX * displayScale;
            float rectY = offsetY + miniY * displayScale;
            float rectW = ratioW * drawWidth;
            float rectH = ratioH * drawHeight;

            // 畫矩形
            using (var pen = new Pen(ViewportRectColor, ViewportRectWidth))
            {
                g.DrawRectangle(pen, rectX, rectY, rectW, rectH);
            }
        }

        #endregion

        #region Dispose

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _miniMapBitmap?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
