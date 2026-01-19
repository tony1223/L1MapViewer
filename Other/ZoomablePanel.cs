using Eto.Forms;
using Eto.Drawing;

namespace L1MapViewer.Other
{
    /// <summary>
    /// 支援縮放功能的自訂 Panel，可阻止滾輪事件在縮放時觸發滾動
    /// Cross-platform version using Eto.Forms
    /// </summary>
    public class ZoomablePanel : Drawable
    {
        // BorderStyle for WinForms compatibility
        public BorderStyle BorderStyle { get; set; } = BorderStyle.None;

        public ZoomablePanel()
        {
            // Eto.Forms Drawable supports double buffering by default
            CanFocus = true;
        }

        // PointToClient for WinForms compatibility
        public Point PointToClient(Point screenPoint)
        {
            var screenLoc = PointToScreen(new PointF(0, 0));
            return new Point(screenPoint.X - (int)screenLoc.X, screenPoint.Y - (int)screenLoc.Y);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // 先觸發事件處理器（讓 MouseWheel 事件能被處理）
            base.OnMouseWheel(e);

            // 如果按住 Ctrl 鍵，標記為已處理，阻止滾動
            if (e.Modifiers.HasFlag(Keys.Control))
            {
                e.Handled = true;
            }
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            // 當滑鼠進入時取得焦點，確保可以接收滾輪事件
            this.Focus();
            base.OnMouseEnter(e);
        }
    }
}
