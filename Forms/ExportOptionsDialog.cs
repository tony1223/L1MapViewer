using System;
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.Compatibility;
using L1MapViewer.Localization;
using NLog;

namespace L1MapViewer.Forms
{
    /// <summary>
    /// 匯出選項對話框
    /// </summary>
    public class ExportOptionsDialog : Dialog<DialogResult>
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public enum ExportMode
        {
            WholeMap,
            SelectedBlocks,
            SelectedRegion
        }

        public ExportMode SelectedMode { get; private set; }
        public ushort LayerFlags { get; private set; }
        public bool IncludeTiles { get; private set; }
        public bool IncludeLayer5 { get; private set; }
        public bool StripLayer8Ext { get; private set; }
        public string MaterialName { get; private set; }

        private RadioButton rbWholeMap;
        private RadioButton rbSelectedBlocks;
        private RadioButton rbSelectedRegion;
        private CheckBox cbLayer1;
        private CheckBox cbLayer2;
        private CheckBox cbLayer3;
        private CheckBox cbLayer4;
        private CheckBox cbLayer5;
        private CheckBox cbLayer6;
        private CheckBox cbLayer7;
        private CheckBox cbLayer8;
        private CheckBox cbIncludeTiles;
        private CheckBox cbIncludeLayer5;
        private TextBox txtMaterialName;

        private bool _isFs3p;
        private bool _hasSelection;

        public ExportOptionsDialog(bool isFs3p = false, bool hasSelection = false)
        {
            _isFs3p = isFs3p;
            _hasSelection = hasSelection;

            Title = _isFs3p ? "儲存為素材" : "匯出 FS32 地圖包";
            MinimumSize = new Size(320, 100);
            Resizable = false;

            var layout = new DynamicLayout { DefaultSpacing = new Size(5, 5), Padding = new Padding(10) };

            // 素材名稱 (fs3p only)
            if (_isFs3p)
            {
                txtMaterialName = new TextBox { Text = "新素材", Width = 200 };
                layout.AddRow(new Label { Text = "素材名稱:" }, txtMaterialName);
                layout.AddRow(new Panel { Height = 10 }); // spacer
            }

            // 匯出模式 (fs32 only)
            if (!_isFs3p)
            {
                var modeGroup = new GroupBox { Text = "匯出模式" };
                var modeLayout = new DynamicLayout { DefaultSpacing = new Size(5, 5), Padding = new Padding(10) };

                rbWholeMap = new RadioButton { Text = "整張地圖", Checked = !_hasSelection };
                rbSelectedBlocks = new RadioButton(rbWholeMap) { Text = "選取的區塊", Enabled = _hasSelection, Checked = _hasSelection };
                rbSelectedRegion = new RadioButton(rbWholeMap) { Text = "選取的區域 (精確範圍)", Enabled = _hasSelection };

                modeLayout.AddRow(rbWholeMap);
                modeLayout.AddRow(rbSelectedBlocks);
                modeLayout.AddRow(rbSelectedRegion);
                modeGroup.Content = modeLayout;
                layout.AddRow(modeGroup);
            }

            // 圖層選擇
            var layersGroup = new GroupBox { Text = "包含圖層" };
            var layersLayout = new DynamicLayout { DefaultSpacing = new Size(5, 5), Padding = new Padding(10) };

            cbLayer1 = new CheckBox { Text = "Layer1 地板", Checked = true };
            cbLayer2 = new CheckBox { Text = "Layer2 裝飾", Checked = true };
            cbLayer3 = new CheckBox { Text = "Layer3 屬性", Checked = true };
            cbLayer4 = new CheckBox { Text = "Layer4 物件", Checked = true };

            layersLayout.AddRow(cbLayer1, cbLayer2);
            layersLayout.AddRow(cbLayer3, cbLayer4);

            if (!_isFs3p)
            {
                cbLayer5 = new CheckBox { Text = "Layer5", Checked = true };
                cbLayer6 = new CheckBox { Text = "Layer6", Checked = true };
                cbLayer7 = new CheckBox { Text = "Layer7", Checked = true };
                cbLayer8 = new CheckBox { Text = "Layer8", Checked = false };

                layersLayout.AddRow(cbLayer5, cbLayer6);
                layersLayout.AddRow(cbLayer7, cbLayer8);
            }

            cbIncludeTiles = new CheckBox { Text = "包含 Tile 資料", Checked = true };
            layersLayout.AddRow(cbIncludeTiles);

            if (_isFs3p)
            {
                cbIncludeLayer5 = new CheckBox { Text = "包含 Layer5 事件 (隨 Layer4 物件)", Checked = false };
                layersLayout.AddRow(cbIncludeLayer5);
            }

            layersGroup.Content = layersLayout;
            layout.AddRow(layersGroup);

            // 按鈕
            var btnExport = new Button { Text = _isFs3p ? "儲存" : "匯出" };
            btnExport.Click += (s, e) =>
            {
                CollectValues();
                Result = DialogResult.Ok;
                Close();
            };

            var btnCancel = new Button { Text = "取消" };
            btnCancel.Click += (s, e) =>
            {
                Result = DialogResult.Cancel;
                Close();
            };

            layout.AddRow(new Panel { Height = 10 }); // spacer
            layout.AddRow(new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows = { new TableRow(null, btnExport, btnCancel, null) }
            });

            Content = layout;

            DefaultButton = btnExport;
            AbortButton = btnCancel;

            _logger.Debug($"[ExportOptionsDialog] Created: isFs3p={_isFs3p}, hasSelection={_hasSelection}");
        }

        /// <summary>
        /// 顯示對話框 (WinForms 相容)
        /// </summary>
        public DialogResult ShowDialog(Control parent)
        {
            ShowModal(parent);
            return Result;
        }

        private void CollectValues()
        {
            // 收集匯出模式
            if (_isFs3p)
            {
                MaterialName = txtMaterialName?.Text ?? "新素材";
                SelectedMode = ExportMode.SelectedRegion;
            }
            else
            {
                if (rbWholeMap?.Checked == true)
                    SelectedMode = ExportMode.WholeMap;
                else if (rbSelectedBlocks?.Checked == true)
                    SelectedMode = ExportMode.SelectedBlocks;
                else
                    SelectedMode = ExportMode.SelectedRegion;
            }

            // 收集 Layer Flags
            ushort flags = 0;
            if (cbLayer1?.Checked == true) flags |= 0x01;
            if (cbLayer2?.Checked == true) flags |= 0x02;
            if (cbLayer3?.Checked == true) flags |= 0x04;
            if (cbLayer4?.Checked == true) flags |= 0x08;
            if (!_isFs3p)
            {
                if (cbLayer5?.Checked == true) flags |= 0x10;
                if (cbLayer6?.Checked == true) flags |= 0x20;
                if (cbLayer7?.Checked == true) flags |= 0x40;
                if (cbLayer8?.Checked == true) flags |= 0x80;
            }
            LayerFlags = flags;

            IncludeTiles = cbIncludeTiles?.Checked == true;
            IncludeLayer5 = _isFs3p && cbIncludeLayer5?.Checked == true;
            StripLayer8Ext = false;
        }
    }
}
