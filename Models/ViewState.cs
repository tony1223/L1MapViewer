using System;
using System.Drawing;

namespace L1MapViewer.Models
{
    /// <summary>
    /// 檢視狀態 - 管理 scroll、zoom、顯示選項
    /// </summary>
    public class ViewState
    {
        #region 縮放

        private double _zoomLevel = 1.0;

        /// <summary>
        /// 縮放比例
        /// </summary>
        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                double newValue = Math.Max(ZoomMin, Math.Min(ZoomMax, value));
                if (Math.Abs(_zoomLevel - newValue) > 0.001)
                {
                    _zoomLevel = newValue;
                    ZoomChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// 最小縮放比例
        /// </summary>
        public double ZoomMin { get; set; } = 0.1;

        /// <summary>
        /// 最大縮放比例
        /// </summary>
        public double ZoomMax { get; set; } = 5.0;

        /// <summary>
        /// 縮放步進
        /// </summary>
        public double ZoomStep { get; set; } = 0.2;

        /// <summary>
        /// 縮放變更事件
        /// </summary>
        public event EventHandler ZoomChanged;

        /// <summary>
        /// 放大
        /// </summary>
        public void ZoomIn()
        {
            ZoomLevel += ZoomStep;
        }

        /// <summary>
        /// 縮小
        /// </summary>
        public void ZoomOut()
        {
            ZoomLevel -= ZoomStep;
        }

        /// <summary>
        /// 重設縮放
        /// </summary>
        public void ResetZoom()
        {
            ZoomLevel = 1.0;
        }

        #endregion

        #region 捲動位置

        private int _scrollX;
        private int _scrollY;

        /// <summary>
        /// 水平捲動位置（虛擬地圖座標）
        /// </summary>
        public int ScrollX
        {
            get => _scrollX;
            set
            {
                int newValue = Math.Max(0, Math.Min(value, MaxScrollX));
                if (_scrollX != newValue)
                {
                    _scrollX = newValue;
                    ScrollChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// 垂直捲動位置（虛擬地圖座標）
        /// </summary>
        public int ScrollY
        {
            get => _scrollY;
            set
            {
                int newValue = Math.Max(0, Math.Min(value, MaxScrollY));
                if (_scrollY != newValue)
                {
                    _scrollY = newValue;
                    ScrollChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// 最大水平捲動值
        /// </summary>
        public int MaxScrollX { get; set; }

        /// <summary>
        /// 最大垂直捲動值
        /// </summary>
        public int MaxScrollY { get; set; }

        /// <summary>
        /// Viewport 寬度（螢幕像素）
        /// </summary>
        public int ViewportWidth { get; set; }

        /// <summary>
        /// Viewport 高度（螢幕像素）
        /// </summary>
        public int ViewportHeight { get; set; }

        /// <summary>
        /// 捲動變更事件
        /// </summary>
        public event EventHandler ScrollChanged;

        /// <summary>
        /// 設置捲動位置（不觸發事件）
        /// </summary>
        public void SetScrollSilent(int x, int y)
        {
            _scrollX = Math.Max(0, Math.Min(x, MaxScrollX));
            _scrollY = Math.Max(0, Math.Min(y, MaxScrollY));
        }

        /// <summary>
        /// 捲動指定量
        /// </summary>
        public void ScrollBy(int deltaX, int deltaY)
        {
            int newX = _scrollX + deltaX;
            int newY = _scrollY + deltaY;
            ScrollX = newX;
            ScrollY = newY;
        }

        /// <summary>
        /// 捲動到指定位置（置中）
        /// </summary>
        public void ScrollToCenter(int worldX, int worldY)
        {
            ScrollX = worldX - ViewportWidth / 2;
            ScrollY = worldY - ViewportHeight / 2;
        }

        /// <summary>
        /// 更新最大捲動值
        /// </summary>
        public void UpdateScrollLimits(int mapWidth, int mapHeight)
        {
            int scaledWidth = (int)(mapWidth * ZoomLevel);
            int scaledHeight = (int)(mapHeight * ZoomLevel);
            MaxScrollX = Math.Max(0, scaledWidth - ViewportWidth);
            MaxScrollY = Math.Max(0, scaledHeight - ViewportHeight);

            // 確保當前位置不超過限制
            if (_scrollX > MaxScrollX) _scrollX = MaxScrollX;
            if (_scrollY > MaxScrollY) _scrollY = MaxScrollY;
        }

        #endregion

        #region 顯示選項

        private bool _showLayer1 = true;
        private bool _showLayer3 = false;
        private bool _showLayer4 = true;
        private bool _showPassability = false;
        private bool _showSafeZones = false;
        private bool _showCombatZones = false;
        private bool _showGrid = false;
        private bool _showS32Boundary = false;

        /// <summary>
        /// 顯示第一層（地板）
        /// </summary>
        public bool ShowLayer1
        {
            get => _showLayer1;
            set { if (_showLayer1 != value) { _showLayer1 = value; DisplayOptionsChanged?.Invoke(this, EventArgs.Empty); } }
        }

        /// <summary>
        /// 顯示第三層（屬性）
        /// </summary>
        public bool ShowLayer3
        {
            get => _showLayer3;
            set { if (_showLayer3 != value) { _showLayer3 = value; DisplayOptionsChanged?.Invoke(this, EventArgs.Empty); } }
        }

        /// <summary>
        /// 顯示第四層（物件）
        /// </summary>
        public bool ShowLayer4
        {
            get => _showLayer4;
            set { if (_showLayer4 != value) { _showLayer4 = value; DisplayOptionsChanged?.Invoke(this, EventArgs.Empty); } }
        }

        /// <summary>
        /// 顯示通行性
        /// </summary>
        public bool ShowPassability
        {
            get => _showPassability;
            set { if (_showPassability != value) { _showPassability = value; DisplayOptionsChanged?.Invoke(this, EventArgs.Empty); } }
        }

        /// <summary>
        /// 顯示安全區域（藍色）
        /// </summary>
        public bool ShowSafeZones
        {
            get => _showSafeZones;
            set { if (_showSafeZones != value) { _showSafeZones = value; DisplayOptionsChanged?.Invoke(this, EventArgs.Empty); } }
        }

        /// <summary>
        /// 顯示戰鬥區域（紅色）
        /// </summary>
        public bool ShowCombatZones
        {
            get => _showCombatZones;
            set { if (_showCombatZones != value) { _showCombatZones = value; DisplayOptionsChanged?.Invoke(this, EventArgs.Empty); } }
        }

        /// <summary>
        /// 顯示格線
        /// </summary>
        public bool ShowGrid
        {
            get => _showGrid;
            set { if (_showGrid != value) { _showGrid = value; DisplayOptionsChanged?.Invoke(this, EventArgs.Empty); } }
        }

        /// <summary>
        /// 顯示 S32 邊界
        /// </summary>
        public bool ShowS32Boundary
        {
            get => _showS32Boundary;
            set { if (_showS32Boundary != value) { _showS32Boundary = value; DisplayOptionsChanged?.Invoke(this, EventArgs.Empty); } }
        }

        private bool _showLayer8 = true;

        /// <summary>
        /// 顯示 Layer8 (SPR 特效標記)
        /// </summary>
        public bool ShowLayer8
        {
            get => _showLayer8;
            set { if (_showLayer8 != value) { _showLayer8 = value; DisplayOptionsChanged?.Invoke(this, EventArgs.Empty); } }
        }

        /// <summary>
        /// 顯示選項變更事件
        /// </summary>
        public event EventHandler DisplayOptionsChanged;

        #endregion

        #region Viewport 渲染

        /// <summary>
        /// 整張地圖寬度（世界座標像素）
        /// </summary>
        public int MapWidth { get; set; }

        /// <summary>
        /// 整張地圖高度（世界座標像素）
        /// </summary>
        public int MapHeight { get; set; }

        /// <summary>
        /// 渲染緩衝區邊距（超出可見範圍多渲染的像素）
        /// 減少緩衝區可提升大地圖拖曳效能，但會增加重渲染頻率
        /// </summary>
        public int RenderBufferMargin { get; set; } = 2048;

        /// <summary>
        /// 當前渲染結果的世界座標原點 X
        /// </summary>
        public int RenderOriginX { get; private set; }

        /// <summary>
        /// 當前渲染結果的世界座標原點 Y
        /// </summary>
        public int RenderOriginY { get; private set; }

        /// <summary>
        /// 當前渲染結果的寬度（世界座標）
        /// </summary>
        public int RenderWidth { get; private set; }

        /// <summary>
        /// 當前渲染結果的高度（世界座標）
        /// </summary>
        public int RenderHeight { get; private set; }

        /// <summary>
        /// 當前渲染結果的縮放級別
        /// </summary>
        public double RenderZoomLevel { get; private set; }

        /// <summary>
        /// 取得當前可見區域的世界座標（未縮放）
        /// </summary>
        public Rectangle GetViewportWorldRect()
        {
            // ScrollX/ScrollY 已經是世界座標，不需要除以 ZoomLevel
            int x = ScrollX;
            int y = ScrollY;
            // ViewportWidth/Height 是螢幕像素，需要除以 ZoomLevel 轉成世界座標
            int width = (int)(ViewportWidth / ZoomLevel);
            int height = (int)(ViewportHeight / ZoomLevel);
            return new Rectangle(x, y, width, height);
        }

        /// <summary>
        /// 取得需要渲染的範圍（含緩衝區，世界座標）
        /// </summary>
        public Rectangle GetRenderWorldRect()
        {
            var viewport = GetViewportWorldRect();
            int x = Math.Max(0, viewport.X - RenderBufferMargin);
            int y = Math.Max(0, viewport.Y - RenderBufferMargin);
            int right = Math.Min(MapWidth, viewport.Right + RenderBufferMargin);
            int bottom = Math.Min(MapHeight, viewport.Bottom + RenderBufferMargin);
            return new Rectangle(x, y, right - x, bottom - y);
        }

        /// <summary>
        /// 更新渲染結果的元數據
        /// </summary>
        public void SetRenderResult(int originX, int originY, int width, int height, double zoomLevel)
        {
            RenderOriginX = originX;
            RenderOriginY = originY;
            RenderWidth = width;
            RenderHeight = height;
            RenderZoomLevel = zoomLevel;
        }

        /// <summary>
        /// 檢查是否需要重新渲染（當前 Viewport 是否超出已渲染範圍的安全區域）
        /// </summary>
        public bool NeedsRerender()
        {
            // 如果縮放級別改變，需要重新渲染
            if (Math.Abs(ZoomLevel - RenderZoomLevel) > 0.001)
                return true;

            // 如果還沒有渲染過
            if (RenderWidth == 0 || RenderHeight == 0)
                return true;

            var viewport = GetViewportWorldRect();
            int safeMargin = RenderBufferMargin / 2;

            // 檢查 Viewport 是否超出已渲染範圍的安全區域
            return viewport.X < RenderOriginX + safeMargin ||
                   viewport.Y < RenderOriginY + safeMargin ||
                   viewport.Right > RenderOriginX + RenderWidth - safeMargin ||
                   viewport.Bottom > RenderOriginY + RenderHeight - safeMargin;
        }

        /// <summary>
        /// Viewport 變更事件（需要重新渲染時觸發）
        /// </summary>
        public event EventHandler ViewportChanged;

        /// <summary>
        /// 觸發 Viewport 變更事件
        /// </summary>
        public void OnViewportChanged()
        {
            ViewportChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region 座標轉換

        /// <summary>
        /// 螢幕座標轉世界座標
        /// </summary>
        public Point ScreenToWorld(int screenX, int screenY)
        {
            int worldX = (int)(screenX / ZoomLevel) + (int)(ScrollX / ZoomLevel);
            int worldY = (int)(screenY / ZoomLevel) + (int)(ScrollY / ZoomLevel);
            return new Point(worldX, worldY);
        }

        /// <summary>
        /// 螢幕座標轉虛擬地圖座標
        /// </summary>
        public Point ScreenToWorld(Point screenPoint)
        {
            return ScreenToWorld(screenPoint.X, screenPoint.Y);
        }

        /// <summary>
        /// 虛擬地圖座標轉螢幕座標
        /// </summary>
        public Point WorldToScreen(int worldX, int worldY)
        {
            int screenX = (int)(worldX * ZoomLevel) - ScrollX;
            int screenY = (int)(worldY * ZoomLevel) - ScrollY;
            return new Point(screenX, screenY);
        }

        /// <summary>
        /// 虛擬地圖座標轉螢幕座標
        /// </summary>
        public Point WorldToScreen(Point worldPoint)
        {
            return WorldToScreen(worldPoint.X, worldPoint.Y);
        }

        #endregion

        #region 重置

        /// <summary>
        /// 重置所有狀態
        /// </summary>
        public void Reset()
        {
            _zoomLevel = 1.0;
            _scrollX = 0;
            _scrollY = 0;
            MaxScrollX = 0;
            MaxScrollY = 0;
            MapWidth = 0;
            MapHeight = 0;
            RenderOriginX = 0;
            RenderOriginY = 0;
            RenderWidth = 0;
            RenderHeight = 0;
            RenderZoomLevel = 0;
            _showLayer1 = true;
            _showLayer3 = false;
            _showLayer4 = true;
            _showPassability = false;
            _showGrid = false;
            _showS32Boundary = false;
        }

        #endregion
    }
}
