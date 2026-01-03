using System;
using System.Drawing;
using System.Windows.Forms;
using L1MapViewer.Controls;

namespace L1FlyMapViewer
{
    /// <summary>
    /// MapForm - 小地圖操作（使用 MiniMapControl）
    /// </summary>
    public partial class MapForm
    {
        /// <summary>
        /// 設定 MiniMapControl（取代 miniMapPictureBox）
        /// </summary>
        private void SetupMiniMapControl()
        {
            // 建立 MiniMapControl
            _miniMapControl = new MiniMapControl
            {
                Location = miniMapPictureBox.Location,
                Size = miniMapPictureBox.Size,
                MiniMapSize = MINIMAP_SIZE,
                MapViewer = _mapViewerControl,
                ViewState = _viewState
            };

            // 隱藏原本的 PictureBox，加入新控制項
            miniMapPictureBox.Visible = false;
            miniMapPictureBox.Parent.Controls.Add(_miniMapControl);
            _miniMapControl.BringToFront();

            // 訂閱導航事件
            _miniMapControl.NavigateRequested += MiniMapControl_NavigateRequested;
            _miniMapControl.S32RightClicked += MiniMapControl_S32RightClicked;

            // 監聽滑鼠按下以設定焦點標記
            _miniMapControl.MouseDown += (s, e) =>
            {
                _interaction.IsMiniMapFocused = true;
                if (e.Button == MouseButtons.Left)
                {
                    _interaction.StartMiniMapDrag();
                }
            };

            _miniMapControl.MouseUp += (s, e) =>
            {
                if (_interaction.IsMiniMapDragging)
                {
                    _interaction.EndDrag();
                    CheckAndRerenderIfNeeded();
                }
            };
        }

        /// <summary>
        /// 小地圖導航事件處理
        /// </summary>
        private void MiniMapControl_NavigateRequested(object sender, Point worldPos)
        {
            // 讓點擊位置成為視窗中央
            int viewportWidthWorld = (int)(s32MapPanel.Width / s32ZoomLevel);
            int viewportHeightWorld = (int)(s32MapPanel.Height / s32ZoomLevel);
            int newScrollX = worldPos.X - viewportWidthWorld / 2;
            int newScrollY = worldPos.Y - viewportHeightWorld / 2;

            // 限制在有效範圍內（含緩衝區）
            newScrollX = Math.Max(_viewState.MinScrollX, Math.Min(newScrollX, _viewState.MaxScrollX));
            newScrollY = Math.Max(_viewState.MinScrollY, Math.Min(newScrollY, _viewState.MaxScrollY));

            _viewState.SetScrollSilent(newScrollX, newScrollY);

            // 如果不在拖曳中，立即重新渲染
            if (!_interaction.IsMiniMapDragging)
            {
                CheckAndRerenderIfNeeded();
                _miniMapControl.RefreshViewportRect();
            }
            else
            {
                // 拖曳中只刷新主地圖和紅框
                _mapViewerControl.Refresh();
                _miniMapControl.RefreshViewportRect();
            }
        }

        /// <summary>
        /// 小地圖右鍵查詢 S32 事件處理
        /// </summary>
        private void MiniMapControl_S32RightClicked(object sender, MiniMapControl.S32QueryEventArgs e)
        {
            this.toolStripStatusLabel1.Text = $"S32 檔案: {e.S32FileName} (座標: {e.GameX},{e.GameY})";
        }

        /// <summary>
        /// 更新小地圖（重新渲染底圖，僅在地圖內容變更時呼叫）
        /// </summary>
        private void UpdateMiniMap()
        {
            if (_miniMapControl == null) return;

            int mapWidth = _viewState.MapWidth;
            int mapHeight = _viewState.MapHeight;

            if (mapWidth <= 0 || mapHeight <= 0)
                return;

            // 使用異步渲染
            _miniMapControl.UpdateMiniMapAsync(_document);
        }

        /// <summary>
        /// 只更新小地圖的視窗紅框（捲動/縮放時呼叫，不重新渲染底圖）
        /// </summary>
        private void UpdateMiniMapViewportRect()
        {
            _miniMapControl?.RefreshViewportRect();
        }

        /// <summary>
        /// 清除小地圖快取（地圖變更時呼叫）
        /// </summary>
        private void ClearMiniMapCache()
        {
            _miniMapControl?.Clear();
        }

        // ============================================
        // 以下事件處理器保留（Designer.cs 有引用）
        // 但已改為空實作或委託給 MiniMapControl
        // ============================================

        private void miniMapPictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            // 已由 MiniMapControl 處理
        }

        private void miniMapPictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            // 已由 MiniMapControl 處理
        }

        private void miniMapPictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            // 已由 MiniMapControl 處理
        }

        private void miniMapPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            // 已由 MiniMapControl 處理
        }

        private void miniMapPictureBox_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            // 保留給未來使用
        }

        private void miniMapPictureBox_KeyDown(object sender, KeyEventArgs e)
        {
            // 保留給未來使用
        }

        /// <summary>
        /// 方向鍵移動小地圖視圖
        /// </summary>
        private void MoveMiniMapByArrowKey(Keys keyCode)
        {
            if (_viewState.MapWidth <= 0 || _viewState.MapHeight <= 0)
                return;

            // 計算移動量（移動一個 viewport 的大小）
            var viewport = _viewState.GetViewportWorldRect();
            int moveX = 0, moveY = 0;

            switch (keyCode)
            {
                case Keys.Up:
                    moveY = -viewport.Height;
                    break;
                case Keys.Down:
                    moveY = viewport.Height;
                    break;
                case Keys.Left:
                    moveX = -viewport.Width;
                    break;
                case Keys.Right:
                    moveX = viewport.Width;
                    break;
                default:
                    return;
            }

            // 計算新的捲動位置
            int newScrollX = _viewState.ScrollX + moveX;
            int newScrollY = _viewState.ScrollY + moveY;

            // 限制在地圖範圍內
            newScrollX = Math.Max(0, Math.Min(newScrollX, _viewState.MaxScrollX));
            newScrollY = Math.Max(0, Math.Min(newScrollY, _viewState.MaxScrollY));

            // 更新捲動位置
            _viewState.SetScrollSilent(newScrollX, newScrollY);

            // 重新渲染並更新小地圖
            RenderS32Map();
            _miniMapControl?.RefreshViewportRect();
        }
    }
}
