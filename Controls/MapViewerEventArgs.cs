using System;
// using System.Drawing; // Replaced with Eto.Drawing
using Eto.Forms;
using Eto.Drawing;

namespace L1MapViewer.Controls
{
    /// <summary>
    /// 地圖滑鼠事件參數
    /// </summary>
    public class MapMouseEventArgs : EventArgs
    {
        /// <summary>滑鼠按鍵</summary>
        public MouseButtons Button { get; }

        /// <summary>滑鼠按鍵 (Eto.Forms compatibility alias)</summary>
        public Eto.Forms.MouseButtons Buttons => L1MapViewer.Compatibility.MouseButtonsHelper.ToEto(Button);

        /// <summary>螢幕座標</summary>
        public Point ScreenLocation { get; }

        /// <summary>世界座標</summary>
        public Point WorldLocation { get; }

        /// <summary>遊戲座標 X（Layer3 座標系）</summary>
        public int GameX { get; }

        /// <summary>遊戲座標 Y（Layer3 座標系）</summary>
        public int GameY { get; }

        /// <summary>滾輪增量</summary>
        public int Delta { get; }

        /// <summary>修飾鍵</summary>
        public Keys Modifiers { get; }

        public MapMouseEventArgs(
            MouseButtons button,
            Point screenLocation,
            Point worldLocation,
            int gameX,
            int gameY,
            int delta = 0,
            Keys modifiers = Keys.None)
        {
            Button = button;
            ScreenLocation = screenLocation;
            WorldLocation = worldLocation;
            GameX = gameX;
            GameY = gameY;
            Delta = delta;
            Modifiers = modifiers;
        }
    }

    /// <summary>
    /// 座標變更事件參數
    /// </summary>
    public class CoordinateChangedEventArgs : EventArgs
    {
        /// <summary>世界座標</summary>
        public Point WorldLocation { get; }

        /// <summary>遊戲座標 X</summary>
        public int GameX { get; }

        /// <summary>遊戲座標 Y</summary>
        public int GameY { get; }

        public CoordinateChangedEventArgs(Point worldLocation, int gameX, int gameY)
        {
            WorldLocation = worldLocation;
            GameX = gameX;
            GameY = gameY;
        }
    }

    /// <summary>
    /// 渲染完成事件參數
    /// </summary>
    public class RenderCompletedEventArgs : EventArgs
    {
        /// <summary>渲染時間（毫秒）</summary>
        public long RenderTimeMs { get; }

        /// <summary>區塊數量</summary>
        public int BlockCount { get; }

        /// <summary>Tile 數量</summary>
        public int TileCount { get; }

        public RenderCompletedEventArgs(long renderTimeMs, int blockCount = 0, int tileCount = 0)
        {
            RenderTimeMs = renderTimeMs;
            BlockCount = blockCount;
            TileCount = tileCount;
        }
    }

    /// <summary>
    /// 縮放變更事件參數
    /// </summary>
    public class ZoomChangedEventArgs : EventArgs
    {
        /// <summary>新的縮放級別</summary>
        public double ZoomLevel { get; }

        /// <summary>舊的縮放級別</summary>
        public double OldZoomLevel { get; }

        public ZoomChangedEventArgs(double zoomLevel, double oldZoomLevel)
        {
            ZoomLevel = zoomLevel;
            OldZoomLevel = oldZoomLevel;
        }
    }
}
