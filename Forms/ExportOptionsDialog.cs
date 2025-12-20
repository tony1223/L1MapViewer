using System;
using System.Drawing;
using System.Windows.Forms;

namespace L1MapViewer.Forms
{
    /// <summary>
    /// 匯出選項對話框
    /// </summary>
    public class ExportOptionsDialog : Form
    {
        /// <summary>
        /// 匯出模式
        /// </summary>
        public enum ExportMode
        {
            WholeMap,
            SelectedBlocks,
            SelectedRegion
        }

        /// <summary>
        /// 選擇的匯出模式
        /// </summary>
        public ExportMode SelectedMode { get; private set; }

        /// <summary>
        /// Layer Flags (bit0-7 = Layer1-8)
        /// </summary>
        public ushort LayerFlags { get; private set; }

        /// <summary>
        /// 是否包含 Tile 資料
        /// </summary>
        public bool IncludeTiles { get; private set; }

        /// <summary>
        /// 素材名稱 (fs3p 使用)
        /// </summary>
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
        private TextBox txtMaterialName;
        private Label lblMaterialName;
        private Button btnExport;
        private Button btnCancel;

        private bool _isFs3p;
        private bool _hasSelection;

        /// <summary>
        /// 建立匯出對話框
        /// </summary>
        /// <param name="isFs3p">是否為 fs3p 格式 (素材)</param>
        /// <param name="hasSelection">是否有選取區域</param>
        public ExportOptionsDialog(bool isFs3p = false, bool hasSelection = false)
        {
            _isFs3p = isFs3p;
            _hasSelection = hasSelection;
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            Text = _isFs3p ? "儲存為素材" : "匯出選項";
            Size = new Size(320, _isFs3p ? 400 : 380);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            int y = 15;

            // 素材名稱 (fs3p)
            if (_isFs3p)
            {
                lblMaterialName = new Label
                {
                    Text = "素材名稱:",
                    Location = new Point(15, y),
                    Size = new Size(80, 20)
                };
                Controls.Add(lblMaterialName);

                txtMaterialName = new TextBox
                {
                    Location = new Point(95, y - 3),
                    Size = new Size(190, 23),
                    Text = "新素材"
                };
                Controls.Add(txtMaterialName);
                y += 35;
            }

            // 模式選擇 (fs32 only)
            if (!_isFs3p)
            {
                var grpMode = new GroupBox
                {
                    Text = "匯出模式",
                    Location = new Point(15, y),
                    Size = new Size(270, 100)
                };
                Controls.Add(grpMode);

                rbWholeMap = new RadioButton
                {
                    Text = "整張地圖",
                    Location = new Point(15, 25),
                    Size = new Size(240, 20),
                    Checked = !_hasSelection
                };
                grpMode.Controls.Add(rbWholeMap);

                rbSelectedBlocks = new RadioButton
                {
                    Text = "選取的區塊",
                    Location = new Point(15, 48),
                    Size = new Size(240, 20),
                    Enabled = _hasSelection,
                    Checked = _hasSelection
                };
                grpMode.Controls.Add(rbSelectedBlocks);

                rbSelectedRegion = new RadioButton
                {
                    Text = "選取的區域 (精確範圍)",
                    Location = new Point(15, 71),
                    Size = new Size(240, 20),
                    Enabled = _hasSelection
                };
                grpMode.Controls.Add(rbSelectedRegion);

                y += 110;
            }

            // 圖層選擇
            var grpLayers = new GroupBox
            {
                Text = "包含圖層",
                Location = new Point(15, y),
                Size = new Size(270, _isFs3p ? 130 : 160)
            };
            Controls.Add(grpLayers);

            cbLayer1 = new CheckBox
            {
                Text = "Layer1 地板",
                Location = new Point(15, 22),
                Size = new Size(110, 20),
                Checked = true
            };
            grpLayers.Controls.Add(cbLayer1);

            cbLayer2 = new CheckBox
            {
                Text = "Layer2 裝飾",
                Location = new Point(140, 22),
                Size = new Size(110, 20),
                Checked = true
            };
            grpLayers.Controls.Add(cbLayer2);

            cbLayer3 = new CheckBox
            {
                Text = "Layer3 屬性",
                Location = new Point(15, 45),
                Size = new Size(110, 20),
                Checked = true
            };
            grpLayers.Controls.Add(cbLayer3);

            cbLayer4 = new CheckBox
            {
                Text = "Layer4 物件",
                Location = new Point(140, 45),
                Size = new Size(110, 20),
                Checked = true
            };
            grpLayers.Controls.Add(cbLayer4);

            if (!_isFs3p)
            {
                cbLayer5 = new CheckBox
                {
                    Text = "Layer5",
                    Location = new Point(15, 68),
                    Size = new Size(110, 20),
                    Checked = true
                };
                grpLayers.Controls.Add(cbLayer5);

                cbLayer6 = new CheckBox
                {
                    Text = "Layer6",
                    Location = new Point(140, 68),
                    Size = new Size(110, 20),
                    Checked = true
                };
                grpLayers.Controls.Add(cbLayer6);

                cbLayer7 = new CheckBox
                {
                    Text = "Layer7",
                    Location = new Point(15, 91),
                    Size = new Size(110, 20),
                    Checked = true
                };
                grpLayers.Controls.Add(cbLayer7);

                cbLayer8 = new CheckBox
                {
                    Text = "Layer8",
                    Location = new Point(140, 91),
                    Size = new Size(110, 20),
                    Checked = true
                };
                grpLayers.Controls.Add(cbLayer8);
            }

            // Tiles 選項
            int tilesY = _isFs3p ? 75 : 120;
            cbIncludeTiles = new CheckBox
            {
                Text = "包含 Tile 資料",
                Location = new Point(15, tilesY),
                Size = new Size(240, 20),
                Checked = true
            };
            grpLayers.Controls.Add(cbIncludeTiles);

            y += grpLayers.Height + 15;

            // 按鈕
            btnExport = new Button
            {
                Text = _isFs3p ? "儲存" : "匯出",
                Location = new Point(110, y),
                Size = new Size(80, 28),
                DialogResult = DialogResult.OK
            };
            btnExport.Click += BtnExport_Click;
            Controls.Add(btnExport);

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(200, y),
                Size = new Size(80, 28),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(btnCancel);

            AcceptButton = btnExport;
            CancelButton = btnCancel;
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            // 收集選項
            if (_isFs3p)
            {
                MaterialName = txtMaterialName?.Text ?? "新素材";
                SelectedMode = ExportMode.SelectedRegion;
            }
            else
            {
                if (rbWholeMap.Checked)
                    SelectedMode = ExportMode.WholeMap;
                else if (rbSelectedBlocks.Checked)
                    SelectedMode = ExportMode.SelectedBlocks;
                else
                    SelectedMode = ExportMode.SelectedRegion;
            }

            // 收集 Layer Flags
            ushort flags = 0;
            if (cbLayer1.Checked) flags |= 0x01;
            if (cbLayer2.Checked) flags |= 0x02;
            if (cbLayer3.Checked) flags |= 0x04;
            if (cbLayer4.Checked) flags |= 0x08;
            if (!_isFs3p)
            {
                if (cbLayer5?.Checked == true) flags |= 0x10;
                if (cbLayer6?.Checked == true) flags |= 0x20;
                if (cbLayer7?.Checked == true) flags |= 0x40;
                if (cbLayer8?.Checked == true) flags |= 0x80;
            }
            LayerFlags = flags;

            IncludeTiles = cbIncludeTiles.Checked;
        }
    }
}
