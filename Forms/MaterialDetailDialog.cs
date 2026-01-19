using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.Models;
using NLog;

namespace L1MapViewer.Forms
{
    /// <summary>
    /// 素材詳情對話框
    /// </summary>
    public class MaterialDetailDialog : Dialog<DialogResult>
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly Fs3pData _material;
        private readonly string _filePath;
        private TabControl _tabControl;
        private string _fullText;

        /// <summary>
        /// 狀態列更新事件
        /// </summary>
        public event Action<string> StatusUpdated;

        public MaterialDetailDialog(Fs3pData material, string filePath)
        {
            _material = material ?? throw new ArgumentNullException(nameof(material));
            _filePath = filePath ?? string.Empty;

            Title = $"素材詳情 - {material.Name}";
            MinimumSize = new Size(650, 500);
            Size = new Size(720, 580);
            Resizable = true;

            BuildContent();
        }

        private void BuildContent()
        {
            // 頂部基本資訊區
            var infoGroup = new GroupBox { Text = "基本資訊" };
            var infoLayout = new TableLayout
            {
                Spacing = new Size(20, 4),
                Padding = new Padding(10),
                Rows =
                {
                    new TableRow(
                        new Label { Text = "素材名稱:", Font = SystemFonts.Bold() },
                        new Label { Text = _material.Name },
                        new Label { Text = "尺寸:", Font = SystemFonts.Bold() },
                        new Label { Text = $"{_material.Width} x {_material.Height}" }
                    ),
                    new TableRow(
                        new Label { Text = "原點偏移:", Font = SystemFonts.Bold() },
                        new Label { Text = $"({_material.OriginOffsetX}, {_material.OriginOffsetY})" },
                        new Label { Text = "Tile 數量:", Font = SystemFonts.Bold() },
                        new Label { Text = _material.Tiles.Count.ToString() }
                    )
                }
            };
            infoGroup.Content = infoLayout;

            // 圖層統計
            var statsLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 15,
                Padding = new Padding(5)
            };

            if (_material.HasLayer1)
                statsLayout.Items.Add(CreateStatBadge("Layer1 地板", _material.Layer1Items.Count, Colors.SteelBlue));
            if (_material.HasLayer2)
                statsLayout.Items.Add(CreateStatBadge("Layer2 裝飾", _material.Layer2Items.Count, Colors.SeaGreen));
            if (_material.HasLayer3)
                statsLayout.Items.Add(CreateStatBadge("Layer3 屬性", _material.Layer3Items.Count, Colors.Orange));
            if (_material.HasLayer4)
                statsLayout.Items.Add(CreateStatBadge("Layer4 物件", _material.Layer4Items.Count, Colors.Purple));

            // Tab 頁籤區域
            _tabControl = new TabControl();

            // 總覽頁籤
            _tabControl.Pages.Add(new TabPage { Text = "總覽", Content = CreateOverviewTab() });

            // Layer1 頁籤
            if (_material.HasLayer1 && _material.Layer1Items.Count > 0)
                _tabControl.Pages.Add(new TabPage { Text = "Layer1 地板", Content = CreateLayer1Tab() });

            // Layer2 頁籤
            if (_material.HasLayer2 && _material.Layer2Items.Count > 0)
                _tabControl.Pages.Add(new TabPage { Text = "Layer2 裝飾", Content = CreateLayer2Tab() });

            // Layer4 頁籤
            if (_material.HasLayer4 && _material.Layer4Items.Count > 0)
                _tabControl.Pages.Add(new TabPage { Text = "Layer4 物件", Content = CreateLayer4Tab() });

            // Tile 資料頁籤
            _tabControl.Pages.Add(new TabPage { Text = "Tile 資料", Content = CreateTilesTab() });

            // 按鈕區
            var btnCopy = new Button { Text = "複製全部" };
            btnCopy.Click += (s, e) =>
            {
                BuildFullText();
                var clipboard = new Clipboard();
                clipboard.Text = _fullText;
                StatusUpdated?.Invoke("已複製素材詳情到剪貼簿");
            };

            var btnClose = new Button { Text = "關閉" };
            btnClose.Click += (s, e) =>
            {
                Result = DialogResult.Ok;
                Close();
            };

            var buttonLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalContentAlignment = Eto.Forms.HorizontalAlignment.Right,
                Items = { null, btnCopy, btnClose }
            };

            // 使用 TableLayout 做主布局，讓 TabControl 填滿中間區域
            var mainLayout = new TableLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(8, 8),
                Rows =
                {
                    new TableRow(infoGroup),
                    new TableRow(statsLayout),
                    new TableRow(new TableCell(_tabControl, true)) { ScaleHeight = true },
                    new TableRow(buttonLayout)
                }
            };

            Content = mainLayout;
            DefaultButton = btnClose;
            AbortButton = btnClose;
        }

        private Control CreateStatBadge(string label, int count, Color color)
        {
            var panel = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            var colorBox = new Drawable { Size = new Size(12, 12) };
            colorBox.Paint += (s, e) =>
            {
                e.Graphics.FillRectangle(color, 0, 0, 12, 12);
            };

            panel.Items.Add(colorBox);
            panel.Items.Add(new Label { Text = $"{label}: {count}" });

            return panel;
        }

        private Control CreateOverviewTab()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"檔案路徑: {_filePath}");
            sb.AppendLine();

            // 中繼資料
            sb.AppendLine("【中繼資料】");
            if (_material.CreatedTime > 0)
            {
                var created = DateTimeOffset.FromUnixTimeSeconds(_material.CreatedTime).LocalDateTime;
                sb.AppendLine($"  建立時間: {created:yyyy-MM-dd HH:mm:ss}");
            }
            if (_material.ModifiedTime > 0)
            {
                var modified = DateTimeOffset.FromUnixTimeSeconds(_material.ModifiedTime).LocalDateTime;
                sb.AppendLine($"  修改時間: {modified:yyyy-MM-dd HH:mm:ss}");
            }
            if (_material.Tags.Count > 0)
            {
                sb.AppendLine($"  標籤: {string.Join(", ", _material.Tags)}");
            }
            sb.AppendLine();

            // 使用的 TileId 統計
            var usedTileIds = new HashSet<int>();
            foreach (var item in _material.Layer1Items)
                if (item.TileId > 0) usedTileIds.Add(item.TileId);
            foreach (var item in _material.Layer2Items)
                if (item.TileId > 0) usedTileIds.Add(item.TileId);
            foreach (var item in _material.Layer4Items)
                if (item.TileId > 0) usedTileIds.Add(item.TileId);

            if (usedTileIds.Count > 0)
            {
                sb.AppendLine($"【使用的 TileId ({usedTileIds.Count} 個)】");
                var sortedIds = usedTileIds.OrderBy(x => x).ToList();
                for (int i = 0; i < sortedIds.Count; i += 10)
                {
                    var batch = sortedIds.Skip(i).Take(10);
                    sb.AppendLine("  " + string.Join(", ", batch));
                }
            }

            return new TextArea
            {
                Text = sb.ToString(),
                ReadOnly = true,
                Font = new Font("Consolas", 10)
            };
        }

        private Control CreateLayer1Tab()
        {
            var grid = new GridView { AllowMultipleSelection = false };

            grid.Columns.Add(new GridColumn
            {
                HeaderText = "X",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer1Item, string>(i => i.RelativeX.ToString()) },
                Width = 60
            });
            grid.Columns.Add(new GridColumn
            {
                HeaderText = "Y",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer1Item, string>(i => i.RelativeY.ToString()) },
                Width = 60
            });
            grid.Columns.Add(new GridColumn
            {
                HeaderText = "TileId",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer1Item, string>(i => i.TileId.ToString()) },
                Width = 80
            });
            grid.Columns.Add(new GridColumn
            {
                HeaderText = "IndexId",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer1Item, string>(i => i.IndexId.ToString()) },
                Width = 80
            });

            var sortedItems = _material.Layer1Items
                .OrderBy(i => i.RelativeY)
                .ThenBy(i => i.RelativeX)
                .ToList();

            grid.DataStore = sortedItems;

            return grid;
        }

        private Control CreateLayer2Tab()
        {
            var grid = new GridView { AllowMultipleSelection = false };

            grid.Columns.Add(new GridColumn
            {
                HeaderText = "X",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer2Item, string>(i => i.RelativeX.ToString()) },
                Width = 60
            });
            grid.Columns.Add(new GridColumn
            {
                HeaderText = "Y",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer2Item, string>(i => i.RelativeY.ToString()) },
                Width = 60
            });
            grid.Columns.Add(new GridColumn
            {
                HeaderText = "TileId",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer2Item, string>(i => i.TileId.ToString()) },
                Width = 80
            });
            grid.Columns.Add(new GridColumn
            {
                HeaderText = "IndexId",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer2Item, string>(i => i.IndexId.ToString()) },
                Width = 80
            });
            grid.Columns.Add(new GridColumn
            {
                HeaderText = "UK",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer2Item, string>(i => i.UK.ToString()) },
                Width = 60
            });

            var sortedItems = _material.Layer2Items
                .OrderBy(i => i.RelativeY)
                .ThenBy(i => i.RelativeX)
                .ToList();

            grid.DataStore = sortedItems;

            return grid;
        }

        private Control CreateLayer4Tab()
        {
            var grid = new GridView { AllowMultipleSelection = false };

            grid.Columns.Add(new GridColumn
            {
                HeaderText = "X",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer4Item, string>(i => i.RelativeX.ToString()) },
                Width = 50
            });
            grid.Columns.Add(new GridColumn
            {
                HeaderText = "Y",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer4Item, string>(i => i.RelativeY.ToString()) },
                Width = 50
            });
            grid.Columns.Add(new GridColumn
            {
                HeaderText = "GroupId",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer4Item, string>(i => i.GroupId.ToString()) },
                Width = 70
            });
            grid.Columns.Add(new GridColumn
            {
                HeaderText = "Layer",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer4Item, string>(i => i.Layer.ToString()) },
                Width = 60
            });
            grid.Columns.Add(new GridColumn
            {
                HeaderText = "TileId",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer4Item, string>(i => i.TileId.ToString()) },
                Width = 70
            });
            grid.Columns.Add(new GridColumn
            {
                HeaderText = "IndexId",
                DataCell = new TextBoxCell { Binding = Binding.Property<Fs3pLayer4Item, string>(i => i.IndexId.ToString()) },
                Width = 70
            });

            var sortedItems = _material.Layer4Items
                .OrderBy(i => i.GroupId)
                .ThenBy(i => i.Layer)
                .ToList();

            grid.DataStore = sortedItems;

            return grid;
        }

        private Control CreateTilesTab()
        {
            if (_material.Tiles.Count == 0)
            {
                return new Label
                {
                    Text = "(此素材未包含 Tile 資料)",
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            var grid = new GridView { AllowMultipleSelection = false };

            grid.Columns.Add(new GridColumn
            {
                HeaderText = "Tile ID",
                DataCell = new TextBoxCell { Binding = Binding.Property<TileDisplayItem, string>(i => i.TileId) },
                Width = 100
            });
            grid.Columns.Add(new GridColumn
            {
                HeaderText = "大小 (bytes)",
                DataCell = new TextBoxCell { Binding = Binding.Property<TileDisplayItem, string>(i => i.Size) },
                Width = 120
            });
            grid.Columns.Add(new GridColumn
            {
                HeaderText = "MD5",
                DataCell = new TextBoxCell { Binding = Binding.Property<TileDisplayItem, string>(i => i.Md5) },
                Width = 180
            });

            var displayItems = _material.Tiles
                .OrderBy(t => t.Key)
                .Select(kv => new TileDisplayItem
                {
                    TileId = kv.Key.ToString(),
                    Size = (kv.Value.TilData?.Length ?? 0).ToString("N0"),
                    Md5 = GetMd5Preview(kv.Value.Md5Hash)
                })
                .ToList();
            grid.DataStore = displayItems;

            return grid;
        }

        private void BuildFullText()
        {
            if (_fullText != null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"素材名稱: {_material.Name}");
            sb.AppendLine($"檔案路徑: {_filePath}");
            sb.AppendLine();
            sb.AppendLine($"尺寸: {_material.Width} x {_material.Height}");
            sb.AppendLine($"原點偏移: ({_material.OriginOffsetX}, {_material.OriginOffsetY})");
            sb.AppendLine();

            // Layer 資訊
            sb.AppendLine("=== 圖層資料 ===");
            if (_material.HasLayer1)
                sb.AppendLine($"Layer1 (地板): {_material.Layer1Items.Count} 項");
            if (_material.HasLayer2)
                sb.AppendLine($"Layer2 (裝飾): {_material.Layer2Items.Count} 項");
            if (_material.HasLayer3)
                sb.AppendLine($"Layer3 (屬性): {_material.Layer3Items.Count} 項");
            if (_material.HasLayer4)
                sb.AppendLine($"Layer4 (物件): {_material.Layer4Items.Count} 項");

            if (!_material.HasLayer1 && !_material.HasLayer2 && !_material.HasLayer3 && !_material.HasLayer4)
                sb.AppendLine("(無圖層資料)");

            // Layer1 詳細列表
            if (_material.HasLayer1 && _material.Layer1Items.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== Layer1 明細 (地板) ===");
                sb.AppendLine("  X\t  Y\tIndexId\tTileId");
                sb.AppendLine("────────────────────────────────");
                foreach (var item in _material.Layer1Items.OrderBy(i => i.RelativeY).ThenBy(i => i.RelativeX))
                {
                    sb.AppendLine($"{item.RelativeX,4}\t{item.RelativeY,4}\t{item.IndexId,4}\t{item.TileId,6}");
                }
            }

            // Layer2 詳細列表
            if (_material.HasLayer2 && _material.Layer2Items.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== Layer2 明細 (裝飾) ===");
                sb.AppendLine("  X\t  Y\tIndexId\tTileId\tUK");
                sb.AppendLine("────────────────────────────────────────");
                foreach (var item in _material.Layer2Items.OrderBy(i => i.RelativeY).ThenBy(i => i.RelativeX))
                {
                    sb.AppendLine($"{item.RelativeX,4}\t{item.RelativeY,4}\t{item.IndexId,4}\t{item.TileId,6}\t{item.UK,3}");
                }
            }

            // Layer4 詳細列表
            if (_material.HasLayer4 && _material.Layer4Items.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== Layer4 明細 (物件) ===");
                sb.AppendLine("  X\t  Y\tGroupId\tLayer\tIndexId\tTileId");
                sb.AppendLine("──────────────────────────────────────────────────");
                foreach (var item in _material.Layer4Items.OrderBy(i => i.GroupId).ThenBy(i => i.Layer))
                {
                    sb.AppendLine($"{item.RelativeX,4}\t{item.RelativeY,4}\t{item.GroupId,5}\t{item.Layer,3}\t{item.IndexId,4}\t{item.TileId,6}");
                }
            }

            sb.AppendLine();

            // Tile 資訊
            sb.AppendLine("=== Tile 資料 ===");
            if (_material.Tiles.Count > 0)
            {
                sb.AppendLine($"包含 {_material.Tiles.Count} 個 Tiles:");
                foreach (var tile in _material.Tiles.OrderBy(t => t.Key))
                {
                    sb.AppendLine($"  - Tile {tile.Key} ({tile.Value.TilData?.Length ?? 0} bytes)");
                }
            }
            else
            {
                sb.AppendLine("(未包含 Tile 資料)");
            }

            sb.AppendLine();

            // 使用的 TileId 統計
            var usedTileIds = new HashSet<int>();
            foreach (var item in _material.Layer1Items)
                if (item.TileId > 0) usedTileIds.Add(item.TileId);
            foreach (var item in _material.Layer2Items)
                if (item.TileId > 0) usedTileIds.Add(item.TileId);
            foreach (var item in _material.Layer4Items)
                if (item.TileId > 0) usedTileIds.Add(item.TileId);

            if (usedTileIds.Count > 0)
            {
                sb.AppendLine($"=== 使用的 TileId ({usedTileIds.Count} 個) ===");
                var sortedIds = usedTileIds.OrderBy(x => x).ToList();
                for (int i = 0; i < sortedIds.Count; i += 10)
                {
                    var batch = sortedIds.Skip(i).Take(10);
                    sb.AppendLine(string.Join(", ", batch));
                }
            }

            // Metadata
            sb.AppendLine();
            sb.AppendLine("=== 中繼資料 ===");
            if (_material.CreatedTime > 0)
            {
                var created = DateTimeOffset.FromUnixTimeSeconds(_material.CreatedTime).LocalDateTime;
                sb.AppendLine($"建立時間: {created:yyyy-MM-dd HH:mm:ss}");
            }
            if (_material.ModifiedTime > 0)
            {
                var modified = DateTimeOffset.FromUnixTimeSeconds(_material.ModifiedTime).LocalDateTime;
                sb.AppendLine($"修改時間: {modified:yyyy-MM-dd HH:mm:ss}");
            }
            if (_material.Tags.Count > 0)
            {
                sb.AppendLine($"標籤: {string.Join(", ", _material.Tags)}");
            }

            _fullText = sb.ToString();
        }

        private static string GetMd5Preview(byte[] md5Hash)
        {
            if (md5Hash == null || md5Hash.Length == 0)
                return "(無)";
            var hex = BitConverter.ToString(md5Hash).Replace("-", "").ToLower();
            if (hex.Length > 16)
                return hex.Substring(0, 16) + "...";
            return hex;
        }

        /// <summary>
        /// 顯示對話框 (WinForms 相容)
        /// </summary>
        public DialogResult ShowDialog(Control parent)
        {
            ShowModal(parent);
            return Result;
        }

        /// <summary>
        /// Tile 顯示項目
        /// </summary>
        private class TileDisplayItem
        {
            public string TileId { get; set; }
            public string Size { get; set; }
            public string Md5 { get; set; }
        }
    }
}
