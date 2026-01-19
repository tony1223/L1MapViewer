using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Forms;
using Eto.Drawing;
using NLog;

namespace L1MapViewer.Controls
{
    /// <summary>
    /// IconTextList 項目
    /// </summary>
    public class IconTextListItem
    {
        public string Text { get; set; }
        public string SubText { get; set; }
        public Image Image { get; set; }
        public int ImageIndex { get; set; } = -1;
        public object Tag { get; set; }
        public bool Selected { get; set; }

        // 相容 ListViewItem 的屬性
        public Color BackgroundColor { get; set; } = Colors.Transparent;
        public Color TextColor { get; set; } = Colors.Black;
    }

    /// <summary>
    /// HitTest 結果
    /// </summary>
    public class IconTextListHitTestInfo
    {
        public IconTextListItem Item { get; set; }
        public int ItemIndex { get; set; } = -1;
    }

    /// <summary>
    /// 選取項目集合（支援 Count 和 indexer）
    /// </summary>
    public class IconTextListSelectedItemCollection : IEnumerable<IconTextListItem>
    {
        private readonly List<IconTextListItem> _items;

        public IconTextListSelectedItemCollection(IEnumerable<IconTextListItem> items)
        {
            _items = items.ToList();
        }

        public int Count => _items.Count;

        public IconTextListItem this[int index] => _items[index];

        public IEnumerator<IconTextListItem> GetEnumerator() => _items.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// IconText 列表控件 - 網格排列，支援單選
    /// </summary>
    public class IconTextListControl : Scrollable
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly Drawable _drawable;
        private readonly List<IconTextListItem> _items = new List<IconTextListItem>();
        private int _selectedIndex = -1;
        private ImageList _imageList;

        // Tile 尺寸設定
        private int _tileWidth = 90;
        private int _tileHeight = 115;
        private int _imageSize = 64;
        private int _tilePadding = 4;

        public int TileWidth
        {
            get => _tileWidth;
            set { _tileWidth = value; UpdateContentSize(); Invalidate(); }
        }

        public int TileHeight
        {
            get => _tileHeight;
            set { _tileHeight = value; UpdateContentSize(); Invalidate(); }
        }

        public int ImageSize
        {
            get => _imageSize;
            set { _imageSize = value; Invalidate(); }
        }

        public int TilePadding
        {
            get => _tilePadding;
            set { _tilePadding = value; UpdateContentSize(); Invalidate(); }
        }

        // 多選模式
        public bool MultiSelect { get; set; } = false;

        // ImageList
        public ImageList LargeImageList
        {
            get => _imageList;
            set
            {
                _imageList = value;
                Invalidate();
            }
        }

        // 選取變更事件
        public event EventHandler SelectionChanged;
        public event EventHandler ItemDoubleClick;
        public event EventHandler<MouseEventArgs> MouseUp;

        // 相容 ListView 的顏色屬性
        public Color BackColor
        {
            get => BackgroundColor;
            set => BackgroundColor = value;
        }

        public Color ForeColor { get; set; } = Colors.Black;

        public IconTextListControl()
        {
            _drawable = new Drawable();
            _drawable.Paint += OnPaint;
            _drawable.MouseDown += OnMouseDown;
            _drawable.MouseDoubleClick += OnMouseDoubleClick;
            _drawable.MouseUp += OnMouseUp;

            Content = _drawable;
            BackgroundColor = Colors.White;
        }

        /// <summary>
        /// 項目集合
        /// </summary>
        public List<IconTextListItem> Items => _items;

        /// <summary>
        /// BeginUpdate - 暫停重繪（相容 ListView API）
        /// </summary>
        public void BeginUpdate() { }

        /// <summary>
        /// EndUpdate - 恢復重繪並重新計算大小（相容 ListView API）
        /// </summary>
        public void EndUpdate()
        {
            UpdateContentSize();  // 重新計算內容大小
            Invalidate();
        }

        /// <summary>
        /// 選取的項目數量
        /// </summary>
        public int SelectedCount => _items.Count(i => i.Selected);

        /// <summary>
        /// 選取的索引
        /// </summary>
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value)
                {
                    // 取消之前的選取
                    if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
                        _items[_selectedIndex].Selected = false;

                    _selectedIndex = value;

                    // 設定新選取
                    if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
                        _items[_selectedIndex].Selected = true;

                    Invalidate();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// 選取的項目
        /// </summary>
        public IconTextListItem SelectedItem =>
            _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

        /// <summary>
        /// 選取的項目集合（多選模式）
        /// </summary>
        public IconTextListSelectedItemCollection SelectedItems =>
            new IconTextListSelectedItemCollection(_items.Where(i => i.Selected));

        /// <summary>
        /// 清除所有項目
        /// </summary>
        public void Clear()
        {
            _items.Clear();
            _selectedIndex = -1;
            UpdateContentSize();
            Invalidate();
        }

        /// <summary>
        /// 添加項目
        /// </summary>
        public void Add(IconTextListItem item)
        {
            _items.Add(item);
            UpdateContentSize();
            Invalidate();
        }

        /// <summary>
        /// 批量添加項目
        /// </summary>
        public void AddRange(IEnumerable<IconTextListItem> items)
        {
            _items.AddRange(items);
            UpdateContentSize();
            Invalidate();
        }

        /// <summary>
        /// 更新內容大小（根據項目數量和控件寬度）
        /// </summary>
        private void UpdateContentSize()
        {
            int controlWidth = (int)Width;
            int controlHeight = (int)Height;
            int availableWidth = Math.Max(100, controlWidth - 20); // 減去捲軸寬度
            int cols = Math.Max(1, availableWidth / TileWidth);
            int rows = (_items.Count + cols - 1) / cols;
            int contentHeight = rows * TileHeight + TilePadding * 2;

            // 確保最小高度為控件高度，這樣才能正確顯示
            int finalHeight = Math.Max(contentHeight, controlHeight);

            _drawable.Size = new Size(availableWidth, finalHeight);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateContentSize();
            Invalidate();
        }

        private void Invalidate()
        {
            _drawable.Invalidate();
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            int width = (int)_drawable.Width;
            int cols = Math.Max(1, width / TileWidth);

            var font = SystemFonts.Default();
            var smallFont = SystemFonts.Default(8);

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                int col = i % cols;
                int row = i / cols;

                float x = col * TileWidth + TilePadding;
                float y = row * TileHeight + TilePadding;

                // 繪製選取框（使用邊框而非填滿）
                if (item.Selected)
                {
                    // 淺藍色背景
                    g.FillRectangle(new Color(0.8f, 0.9f, 1.0f), x, y, TileWidth - TilePadding, TileHeight - TilePadding);
                    // 藍色邊框
                    g.DrawRectangle(new Color(0, 120, 215), x, y, TileWidth - TilePadding - 1, TileHeight - TilePadding - 1);
                }

                // 繪製圖片
                Image img = item.Image;
                if (img == null && _imageList != null && item.ImageIndex >= 0 && item.ImageIndex < _imageList.Images.Count)
                {
                    img = _imageList.Images[item.ImageIndex];
                }

                float imgX = x + (TileWidth - TilePadding - ImageSize) / 2;
                float imgY = y + 4;

                if (img != null)
                {
                    try
                    {
                        g.DrawImage(img, new RectangleF(imgX, imgY, ImageSize, ImageSize));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"[IconTextListControl] DrawImage failed for {item.Text}");
                    }
                }

                // 繪製文字（選取時使用黑色，因為背景是淺色）
                var textColor = Colors.Black;
                string text = item.Text ?? "";
                var textSize = g.MeasureString(font, text);
                float textX = x + (TileWidth - TilePadding - textSize.Width) / 2;
                float textY = imgY + ImageSize + 2;
                g.DrawText(font, textColor, textX, textY, text);

                // 繪製副文字
                if (!string.IsNullOrEmpty(item.SubText))
                {
                    var subColor = Colors.Gray;
                    var subSize = g.MeasureString(smallFont, item.SubText);
                    float subX = x + (TileWidth - TilePadding - subSize.Width) / 2;
                    float subY = textY + 14;
                    g.DrawText(smallFont, subColor, subX, subY, item.SubText);
                }
            }
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            int width = (int)_drawable.Width;
            int cols = Math.Max(1, width / TileWidth);

            int col = (int)(e.Location.X / TileWidth);
            int row = (int)(e.Location.Y / TileHeight);
            int index = row * cols + col;

            if (index >= 0 && index < _items.Count)
            {
                if (MultiSelect && e.Modifiers.HasFlag(Keys.Control))
                {
                    // Ctrl+點擊：切換選取
                    _items[index].Selected = !_items[index].Selected;
                    Invalidate();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    // 單選
                    SelectedIndex = index;
                }
            }
        }

        private void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (_selectedIndex >= 0)
            {
                ItemDoubleClick?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            // 轉換座標：從 _drawable 內部座標轉換為相對於此控件的座標
            // e.Location 是相對於 _drawable 的，需要考慮捲動位置
            var scrollPosition = ScrollPosition;
            var adjustedLocation = new PointF(
                e.Location.X - scrollPosition.X,
                e.Location.Y - scrollPosition.Y
            );

            // 建立新的 MouseEventArgs，使用調整後的座標
            var adjustedArgs = new MouseEventArgs(e.Buttons, e.Modifiers, adjustedLocation, e.Delta);
            MouseUp?.Invoke(this, adjustedArgs);
        }

        /// <summary>
        /// HitTest - 根據位置取得項目
        /// </summary>
        public IconTextListHitTestInfo HitTest(Point location)
        {
            int width = (int)_drawable.Width;
            int cols = Math.Max(1, width / TileWidth);

            int col = (int)(location.X / TileWidth);
            int row = (int)(location.Y / TileHeight);
            int index = row * cols + col;

            if (index >= 0 && index < _items.Count)
            {
                return new IconTextListHitTestInfo
                {
                    Item = _items[index],
                    ItemIndex = index
                };
            }

            return new IconTextListHitTestInfo();
        }

        /// <summary>
        /// HitTest - 根據 Eto PointF 位置取得項目
        /// </summary>
        public IconTextListHitTestInfo HitTest(PointF location)
        {
            return HitTest(new Point((int)location.X, (int)location.Y));
        }
    }
}
