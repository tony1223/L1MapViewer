using System;
using System.Collections.Generic;
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.Compatibility;
using L1MapViewer.Models;

namespace L1MapViewer.Forms
{
    /// <summary>
    /// L4 物件編輯對話框
    /// </summary>
    public class L4EditDialog : Dialog<DialogResult>
    {
        private NumericStepper numGroupId;
        private NumericStepper numX;
        private NumericStepper numY;
        private NumericStepper numLayer;
        private NumericStepper numIndexId;
        private NumericStepper numTileId;
        private DropDown cmbTargetS32;
        private Label lblCoordInfo;
        private Button btnOK;
        private Button btnCancel;

        private S32Data _originalS32;
        private ObjectTile _originalObject;

        public int GroupId => (int)numGroupId.Value;
        public int NewX => (int)numX.Value;
        public int NewY => (int)numY.Value;
        public int Layer => (int)numLayer.Value;
        public int IndexId => (int)numIndexId.Value;
        public int TileId => (int)numTileId.Value;
        public S32Data SelectedS32 => (cmbTargetS32?.SelectedValue as S32ComboItem)?.S32;
        public bool S32Changed { get; private set; } = false;

        /// <summary>
        /// L4 編輯對話框
        /// </summary>
        /// <param name="obj">要編輯的物件</param>
        /// <param name="currentS32">物件所屬的 S32</param>
        /// <param name="availableS32s">可用的 S32 清單</param>
        public L4EditDialog(ObjectTile obj, S32Data currentS32, IEnumerable<S32Data> availableS32s)
        {
            _originalS32 = currentS32;
            _originalObject = obj;

            Title = "編輯 L4 物件";
            MinimumSize = new Size(420, 100);
            Resizable = false;

            // 建立控制項
            cmbTargetS32 = new DropDown { Width = 220 };
            foreach (var s32 in availableS32s)
            {
                string displayName = System.IO.Path.GetFileName(s32.FilePath);
                var item = new S32ComboItem { S32 = s32, DisplayName = displayName };
                cmbTargetS32.Items.Add(new ListItem { Text = displayName, Tag = item });
                if (s32 == currentS32)
                {
                    cmbTargetS32.SelectedIndex = cmbTargetS32.Items.Count - 1;
                }
            }
            cmbTargetS32.SelectedIndexChanged += (s, e) =>
            {
                var item = (cmbTargetS32.SelectedValue as ListItem)?.Tag as S32ComboItem;
                S32Changed = (item?.S32 != _originalS32);
                RecalculateCoordinates();
            };

            lblCoordInfo = new Label { TextColor = Colors.Blue };

            numX = new NumericStepper { MinValue = 0, MaxValue = 255, Value = obj.X, Width = 100 };
            numY = new NumericStepper { MinValue = 0, MaxValue = 255, Value = obj.Y, Width = 100 };
            numGroupId = new NumericStepper { MinValue = 0, MaxValue = 65535, Value = obj.GroupId, Width = 100 };
            numLayer = new NumericStepper { MinValue = 0, MaxValue = 255, Value = obj.Layer, Width = 100 };
            numTileId = new NumericStepper { MinValue = 0, MaxValue = 65535, Value = obj.TileId, Width = 100 };
            numIndexId = new NumericStepper { MinValue = 0, MaxValue = 255, Value = obj.IndexId, Width = 100 };

            numX.ValueChanged += (s, e) => UpdateCoordInfo();
            numY.ValueChanged += (s, e) => UpdateCoordInfo();

            btnOK = new Button { Text = "確定" };
            btnOK.Click += (s, e) =>
            {
                Result = DialogResult.Ok;
                Close();
            };

            btnCancel = new Button { Text = "取消" };
            btnCancel.Click += (s, e) =>
            {
                Result = DialogResult.Cancel;
                Close();
            };

            // 使用 DynamicLayout 建立布局
            var layout = new DynamicLayout { DefaultSpacing = new Size(8, 6), Padding = new Padding(15) };

            // S32 選擇區
            var s32Group = new GroupBox { Text = "所屬區塊" };
            var s32Layout = new DynamicLayout { DefaultSpacing = new Size(8, 6), Padding = new Padding(10) };
            s32Layout.AddRow(new Label { Text = "S32 檔案:", VerticalAlignment = VerticalAlignment.Center }, cmbTargetS32);
            s32Layout.AddRow(lblCoordInfo);
            s32Group.Content = s32Layout;
            layout.AddRow(s32Group);

            // 座標區
            var coordGroup = new GroupBox { Text = "座標 (S32 內部 L1 座標)" };
            var coordLayout = new TableLayout
            {
                Spacing = new Size(15, 6),
                Padding = new Padding(10),
                Rows =
                {
                    new TableRow(
                        new Label { Text = "X (0-255):", VerticalAlignment = VerticalAlignment.Center },
                        numX,
                        new Label { Text = "Y (0-255):", VerticalAlignment = VerticalAlignment.Center },
                        numY
                    )
                }
            };
            coordGroup.Content = coordLayout;
            layout.AddRow(coordGroup);

            // 物件屬性區
            var attrGroup = new GroupBox { Text = "物件屬性" };
            var attrLayout = new TableLayout
            {
                Spacing = new Size(15, 6),
                Padding = new Padding(10),
                Rows =
                {
                    new TableRow(
                        new Label { Text = "GroupId:", VerticalAlignment = VerticalAlignment.Center },
                        numGroupId,
                        new Label { Text = "Layer (高度):", VerticalAlignment = VerticalAlignment.Center },
                        numLayer
                    ),
                    new TableRow(
                        new Label { Text = "TileId:", VerticalAlignment = VerticalAlignment.Center },
                        numTileId,
                        new Label { Text = "IndexId:", VerticalAlignment = VerticalAlignment.Center },
                        numIndexId
                    )
                }
            };
            attrGroup.Content = attrLayout;
            layout.AddRow(attrGroup);

            // 按鈕區
            layout.AddRow(new Panel { Height = 5 });
            layout.AddRow(new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows = { new TableRow(null, btnOK, btnCancel, null) }
            });

            Content = layout;

            DefaultButton = btnOK;
            AbortButton = btnCancel;

            UpdateCoordInfo();
        }

        /// <summary>
        /// 顯示對話框 (WinForms 相容)
        /// </summary>
        public DialogResult ShowDialog(Control parent)
        {
            ShowModal(parent);
            return Result;
        }

        private void UpdateCoordInfo()
        {
            if (lblCoordInfo == null) return;

            var item = (cmbTargetS32?.SelectedValue as ListItem)?.Tag as S32ComboItem;
            var s32 = item?.S32 ?? _originalS32;
            if (s32 == null) return;

            int localX = (int)(numX?.Value ?? _originalObject.X);
            int localY = (int)(numY?.Value ?? _originalObject.Y);
            int globalX = s32.SegInfo.nLinBeginX * 2 + localX;
            int globalY = s32.SegInfo.nLinBeginY + localY;
            int gameX = globalX / 2;
            int gameY = globalY;

            lblCoordInfo.Text = $"全域L1座標: ({globalX}, {globalY}) | 遊戲座標: ({gameX}, {gameY})";
        }

        private void RecalculateCoordinates()
        {
            if (numX == null || numY == null || _originalS32 == null) return;

            var item = (cmbTargetS32.SelectedValue as ListItem)?.Tag as S32ComboItem;
            if (item == null) return;

            var targetS32 = item.S32;

            // 計算原始的全域座標
            int globalX = _originalS32.SegInfo.nLinBeginX * 2 + _originalObject.X;
            int globalY = _originalS32.SegInfo.nLinBeginY + _originalObject.Y;

            // 計算目標 S32 中的本地座標
            int newLocalX = globalX - targetS32.SegInfo.nLinBeginX * 2;
            int newLocalY = globalY - targetS32.SegInfo.nLinBeginY;

            // 座標必須 >= 0 且 <= 255（byte 範圍）
            bool isValid = newLocalX >= 0 && newLocalX <= 255 && newLocalY >= 0 && newLocalY <= 255;

            if (isValid)
            {
                numX.Value = newLocalX;
                numY.Value = newLocalY;
                numX.Enabled = true;
                numY.Enabled = true;
                lblCoordInfo.TextColor = Colors.Blue;
                btnOK.Enabled = true;
            }
            else
            {
                // 座標超出範圍，禁止確定
                numX.Enabled = false;
                numY.Enabled = false;
                lblCoordInfo.Text = $"座標超出範圍! ({newLocalX}, {newLocalY}) - 無法移動到此 S32";
                lblCoordInfo.TextColor = Colors.Red;
                btnOK.Enabled = false;
            }

            UpdateCoordInfo();
        }

        /// <summary>
        /// ComboBox 項目包裝類
        /// </summary>
        private class S32ComboItem
        {
            public S32Data S32 { get; set; }
            public string DisplayName { get; set; }
            public override string ToString() => DisplayName;
        }
    }
}
