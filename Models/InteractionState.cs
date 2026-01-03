using System.Drawing;

namespace L1MapViewer.Models
{
    /// <summary>
    /// 互動狀態 - 管理拖曳、選取等互動相關的狀態
    /// </summary>
    public class InteractionState
    {
        /// <summary>
        /// 拖曳模式
        /// </summary>
        public enum DragMode
        {
            None,           // 無拖曳
            MainMap,        // 主地圖拖曳（中鍵移動視圖）
            MiniMap,        // 小地圖拖曳
            Selection       // 選取區域拖曳
        }

        /// <summary>
        /// 當前拖曳模式
        /// </summary>
        public DragMode CurrentDragMode { get; set; } = DragMode.None;

        /// <summary>
        /// 滑鼠按下位置（用於判斷是否開始拖曳）
        /// </summary>
        public Point MouseDownPoint { get; set; }

        /// <summary>
        /// 是否正在滑鼠拖曳（已超過閾值）
        /// </summary>
        public bool IsMouseDrag { get; set; }

        /// <summary>
        /// 主地圖拖曳開始的螢幕座標
        /// </summary>
        public Point MainMapDragStartPoint { get; set; }

        /// <summary>
        /// 主地圖拖曳開始時的捲動位置
        /// </summary>
        public Point MainMapDragStartScroll { get; set; }

        /// <summary>
        /// 小地圖是否有焦點（用於方向鍵導航）
        /// </summary>
        public bool IsMiniMapFocused { get; set; }

        /// <summary>
        /// 區域選取起始點（遊戲座標）
        /// </summary>
        public Point RegionStartPoint { get; set; }

        /// <summary>
        /// 是否在 Layer4 複製選取模式
        /// </summary>
        public bool IsLayer4CopyMode { get; set; }

        /// <summary>
        /// 是否正在拖曳
        /// </summary>
        public bool IsDragging => CurrentDragMode != DragMode.None;

        /// <summary>
        /// 是否正在拖曳主地圖
        /// </summary>
        public bool IsMainMapDragging => CurrentDragMode == DragMode.MainMap;

        /// <summary>
        /// 是否正在拖曳小地圖
        /// </summary>
        public bool IsMiniMapDragging => CurrentDragMode == DragMode.MiniMap;

        /// <summary>
        /// 開始主地圖拖曳
        /// </summary>
        public void StartMainMapDrag(Point screenPoint, int scrollX, int scrollY)
        {
            CurrentDragMode = DragMode.MainMap;
            MainMapDragStartPoint = screenPoint;
            MainMapDragStartScroll = new Point(scrollX, scrollY);
        }

        /// <summary>
        /// 開始小地圖拖曳
        /// </summary>
        public void StartMiniMapDrag()
        {
            CurrentDragMode = DragMode.MiniMap;
        }

        /// <summary>
        /// 開始選取區域拖曳
        /// </summary>
        public void StartSelectionDrag(Point startPoint)
        {
            CurrentDragMode = DragMode.Selection;
            RegionStartPoint = startPoint;
        }

        /// <summary>
        /// 結束拖曳
        /// </summary>
        public void EndDrag()
        {
            CurrentDragMode = DragMode.None;
            IsMouseDrag = false;
        }

        /// <summary>
        /// 重置所有狀態
        /// </summary>
        public void Reset()
        {
            CurrentDragMode = DragMode.None;
            MouseDownPoint = Point.Empty;
            IsMouseDrag = false;
            MainMapDragStartPoint = Point.Empty;
            MainMapDragStartScroll = Point.Empty;
            IsMiniMapFocused = false;
            RegionStartPoint = Point.Empty;
            IsLayer4CopyMode = false;
        }
    }
}
