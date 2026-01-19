using System;
// using System.Drawing; // Replaced with Eto.Drawing
using L1MapViewer.Helper;

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
        public double ZoomMin { get; set; } = 0.4;

        /// <summary>
        /// 最大縮放比例
        /// </summary>
        public double ZoomMax { get; set; } = 1.5;

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
                int newValue = Math.Max(MinScrollX, Math.Min(value, MaxScrollX));
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
                int newValue = Math.Max(MinScrollY, Math.Min(value, MaxScrollY));
                if (_scrollY != newValue)
                {
                    _scrollY = newValue;
                    ScrollChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// S32 區塊寬度（像素）- 用於計算捲動緩衝區
        /// </summary>
        public const int S32BlockWidth = 3072;

        /// <summary>
        /// S32 區塊高度（像素）- 用於計算捲動緩衝區
        /// </summary>
        public const int S32BlockHeight = 1536;

        /// <summary>
        /// 最小水平捲動值（支援左側緩衝區）
        /// </summary>
        public int MinScrollX { get; set; }

        /// <summary>
        /// 最小垂直捲動值（支援上側緩衝區）
        /// </summary>
        public int MinScrollY { get; set; }

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
            _scrollX = Math.Max(MinScrollX, Math.Min(x, MaxScrollX));
            _scrollY = Math.Max(MinScrollY, Math.Min(y, MaxScrollY));
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
            // ViewportWidth/Height 是螢幕像素，需轉換為世界座標
            int halfViewportWorldW = (int)(ViewportWidth / (2 * ZoomLevel));
            int halfViewportWorldH = (int)(ViewportHeight / (2 * ZoomLevel));
            ScrollX = worldX - halfViewportWorldW;
            ScrollY = worldY - halfViewportWorldH;
        }

        /// <summary>
        /// 更新最大捲動值（含緩衝區）
        /// 注意：ScrollX/ScrollY 是世界座標，所有計算都在世界座標系進行
        /// </summary>
        public void UpdateScrollLimits(int mapWidth, int mapHeight)
        {
            // Viewport 尺寸轉換為世界座標（受縮放影響）
            int viewportWorldWidth = (int)(ViewportWidth / ZoomLevel);
            int viewportWorldHeight = (int)(ViewportHeight / ZoomLevel);

            // 緩衝區大小（世界座標，一個 S32 區塊）
            // 但限制緩衝區最多只佔 viewport 的一半，確保地圖內容始終佔據至少一半 viewport
            int bufferX = Math.Min(S32BlockWidth, viewportWorldWidth / 2);
            int bufferY = Math.Min(S32BlockHeight, viewportWorldHeight / 2);

            // 捲動限制（世界座標）
            // 額外加一個 S32 區塊，確保地圖邊緣內容完整顯示
            MinScrollX = -S32BlockWidth - bufferX;
            MinScrollY = -S32BlockHeight - bufferY;

            // MaxScrollX: 確保在最右側時，地圖右邊緣位於 viewport 中央左側
            // 這樣至少有一半的 viewport 顯示地圖內容
            int normalMaxX = Math.Max(0, mapWidth - viewportWorldWidth);
            int normalMaxY = Math.Max(0, mapHeight - viewportWorldHeight);
            MaxScrollX = normalMaxX + bufferX;
            MaxScrollY = normalMaxY + bufferY;

            // 確保當前位置在新限制範圍內
            if (_scrollX > MaxScrollX) _scrollX = MaxScrollX;
            if (_scrollX < MinScrollX) _scrollX = MinScrollX;
            if (_scrollY > MaxScrollY) _scrollY = MaxScrollY;
            if (_scrollY < MinScrollY) _scrollY = MinScrollY;
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

        private bool _showLayer8Spr = true;

        /// <summary>
        /// 顯示 Layer8 SPR 圖片
        /// </summary>
        public bool ShowLayer8Spr
        {
            get => _showLayer8Spr;
            set { if (_showLayer8Spr != value) { _showLayer8Spr = value; DisplayOptionsChanged?.Invoke(this, EventArgs.Empty); } }
        }

        private bool _showLayer8Marker = true;

        /// <summary>
        /// 顯示 Layer8 輔助標記
        /// </summary>
        public bool ShowLayer8Marker
        {
            get => _showLayer8Marker;
            set { if (_showLayer8Marker != value) { _showLayer8Marker = value; DisplayOptionsChanged?.Invoke(this, EventArgs.Empty); } }
        }

        /// <summary>
        /// 保留舊的 ShowLayer8 屬性以便相容（同時控制 SPR 和 Marker）
        /// </summary>
        public bool ShowLayer8
        {
            get => _showLayer8Spr || _showLayer8Marker;
            set { ShowLayer8Spr = value; ShowLayer8Marker = value; }
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
        /// 注意：會限制在地圖實際範圍內，緩衝區只是允許捲動超出，不會渲染地圖外的內容
        /// </summary>
        public Rectangle GetRenderWorldRect()
        {
            var viewport = GetViewportWorldRect();
            // 計算渲染範圍
            // 允許負值以支援地圖邊緣的額外緩衝區顯示
            int x = Math.Max(-S32BlockWidth, viewport.X - RenderBufferMargin);
            int y = Math.Max(-S32BlockHeight, viewport.Y - RenderBufferMargin);
            int right = Math.Min(MapWidth, viewport.Right + RenderBufferMargin);
            int bottom = Math.Min(MapHeight, viewport.Bottom + RenderBufferMargin);

            // 確保寬高不為負數
            int width = Math.Max(0, right - x);
            int height = Math.Max(0, bottom - y);

            return new Rectangle(x, y, width, height);
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

            // 計算 viewport 與地圖實際內容的交集
            // 超出地圖範圍的部分是緩衝區（空白），不需要重新渲染
            int effectiveLeft = Math.Max(0, viewport.X);
            int effectiveTop = Math.Max(0, viewport.Y);
            int effectiveRight = Math.Min(MapWidth, viewport.Right);
            int effectiveBottom = Math.Min(MapHeight, viewport.Bottom);

            // 如果 viewport 完全在地圖外（純緩衝區），不需要重新渲染
            if (effectiveRight <= effectiveLeft || effectiveBottom <= effectiveTop)
                return false;

            // 檢查有效 Viewport 區域是否超出已渲染範圍的安全區域
            return effectiveLeft < RenderOriginX + safeMargin ||
                   effectiveTop < RenderOriginY + safeMargin ||
                   effectiveRight > RenderOriginX + RenderWidth - safeMargin ||
                   effectiveBottom > RenderOriginY + RenderHeight - safeMargin;
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
            // ScrollX/ScrollY 已經是世界座標，不需要除以 ZoomLevel
            int worldX = (int)(screenX / ZoomLevel) + ScrollX;
            int worldY = (int)(screenY / ZoomLevel) + ScrollY;
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
            // 先計算世界座標差，再乘以 ZoomLevel
            int screenX = (int)((worldX - ScrollX) * ZoomLevel);
            int screenY = (int)((worldY - ScrollY) * ZoomLevel);
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

        #region Viewport 遊戲座標

        /// <summary>
        /// Viewport 四角的遊戲座標
        /// </summary>
        public struct ViewportGameBounds
        {
            public Point TopLeft;      // 左上角遊戲座標
            public Point TopRight;     // 右上角遊戲座標
            public Point BottomLeft;   // 左下角遊戲座標
            public Point BottomRight;  // 右下角遊戲座標

            /// <summary>
            /// 取得最小遊戲座標 X
            /// </summary>
            public int MinGameX => Math.Min(Math.Min(TopLeft.X, TopRight.X), Math.Min(BottomLeft.X, BottomRight.X));

            /// <summary>
            /// 取得最大遊戲座標 X
            /// </summary>
            public int MaxGameX => Math.Max(Math.Max(TopLeft.X, TopRight.X), Math.Max(BottomLeft.X, BottomRight.X));

            /// <summary>
            /// 取得最小遊戲座標 Y
            /// </summary>
            public int MinGameY => Math.Min(Math.Min(TopLeft.Y, TopRight.Y), Math.Min(BottomLeft.Y, BottomRight.Y));

            /// <summary>
            /// 取得最大遊戲座標 Y
            /// </summary>
            public int MaxGameY => Math.Max(Math.Max(TopLeft.Y, TopRight.Y), Math.Max(BottomLeft.Y, BottomRight.Y));

            /// <summary>
            /// 是否有效
            /// </summary>
            public bool IsValid => TopLeft.X != 0 || TopLeft.Y != 0 || TopRight.X != 0 || BottomLeft.X != 0;
        }

        /// <summary>
        /// 取得 Viewport 四角的遊戲座標
        /// </summary>
        public ViewportGameBounds GetViewportGameBounds()
        {
            var bounds = new ViewportGameBounds();

            // Viewport 的四個角落（世界像素座標）
            int left = ScrollX;
            int top = ScrollY;
            int right = ScrollX + (int)(ViewportWidth / ZoomLevel);
            int bottom = ScrollY + (int)(ViewportHeight / ZoomLevel);

            // 轉換為遊戲座標
            var topLeftLoc = L1MapHelper.GetLinLocation(left, top);
            var topRightLoc = L1MapHelper.GetLinLocation(right, top);
            var bottomLeftLoc = L1MapHelper.GetLinLocation(left, bottom);
            var bottomRightLoc = L1MapHelper.GetLinLocation(right, bottom);

            if (topLeftLoc != null)
                bounds.TopLeft = new Point(topLeftLoc.x, topLeftLoc.y);
            if (topRightLoc != null)
                bounds.TopRight = new Point(topRightLoc.x, topRightLoc.y);
            if (bottomLeftLoc != null)
                bounds.BottomLeft = new Point(bottomLeftLoc.x, bottomLeftLoc.y);
            if (bottomRightLoc != null)
                bounds.BottomRight = new Point(bottomRightLoc.x, bottomRightLoc.y);

            return bounds;
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
            MinScrollX = 0;
            MinScrollY = 0;
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
