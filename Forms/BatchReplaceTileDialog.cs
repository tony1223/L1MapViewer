using System;
using System.Collections.Generic;
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.Compatibility;
using L1MapViewer.Localization;

namespace L1MapViewer.Forms
{
    /// <summary>
    /// 批次替換 Tile 對話框
    /// </summary>
    public class BatchReplaceTileDialog : WinFormsDialog
    {
        #region 公開屬性

        /// <summary>
        /// 選擇的圖層 (1, 2, 或 4)
        /// </summary>
        public int SelectedLayer
        {
            get
            {
                if (_rbLayer1.Checked) return 1;
                if (_rbLayer2.Checked) return 2;
                if (_rbLayer4.Checked) return 4;
                return 1;
            }
        }

        /// <summary>
        /// 來源 TileId
        /// </summary>
        public int SourceTileId => (int)_nudSrcTileId.Value;

        /// <summary>
        /// 來源 IndexId
        /// </summary>
        public int SourceIndexId => (int)_nudSrcIndexId.Value;

        /// <summary>
        /// 是否比對 IndexId
        /// </summary>
        public bool MatchIndexId => _chkMatchIndexId.Checked ?? false;

        /// <summary>
        /// 目標 TileId
        /// </summary>
        public int TargetTileId => (int)_nudDstTileId.Value;

        /// <summary>
        /// 目標 IndexId
        /// </summary>
        public int TargetIndexId => (int)_nudDstIndexId.Value;

        /// <summary>
        /// 是否替換 IndexId
        /// </summary>
        public bool ReplaceIndexId => _chkReplaceIndexId.Checked ?? false;

        /// <summary>
        /// 預覽結果文字
        /// </summary>
        public string PreviewResult
        {
            get => _lblResult.Text;
            set => _lblResult.Text = value;
        }

        #endregion

        #region 事件

        /// <summary>
        /// 點擊預覽按鈕時觸發
        /// </summary>
        public event EventHandler PreviewClicked;

        /// <summary>
        /// 點擊執行按鈕時觸發
        /// </summary>
        public event EventHandler ExecuteClicked;

        #endregion

        #region 私有欄位

        private GroupBox _grpLayer;
        private GroupBox _grpSource;
        private GroupBox _grpTarget;

        private RadioButton _rbLayer1;
        private RadioButton _rbLayer2;
        private RadioButton _rbLayer4;

        private NumericUpDown _nudSrcTileId;
        private NumericUpDown _nudSrcIndexId;
        private CheckBox _chkMatchIndexId;

        private NumericUpDown _nudDstTileId;
        private NumericUpDown _nudDstIndexId;
        private CheckBox _chkReplaceIndexId;

        private Button _btnPreview;
        private Button _btnExecute;
        private Button _btnCancel;
        private Label _lblResult;

        #endregion

        public BatchReplaceTileDialog()
        {
            InitializeComponents();
            UpdateLocalization();
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (this.GetInvokeRequired())
                this.Invoke(new Action(() => UpdateLocalization()));
            else
                UpdateLocalization();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                LocalizationManager.LanguageChanged -= OnLanguageChanged;
            }
            base.Dispose(disposing);
        }

        private void InitializeComponents()
        {
            Text = "批次替換 TileId";
            Size = new Size(440, 380);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Padding = new Padding(20);

            int y = 15;
            int groupWidth = 385;
            int leftMargin = 15;

            // === 圖層選擇 ===
            _grpLayer = new GroupBox
            {
                Text = "選擇圖層",
                Location = new Point(leftMargin, y),
                Size = new Size(groupWidth, 55)
            };
            Controls.Add(_grpLayer);

            _rbLayer1 = new RadioButton
            {
                Text = "Layer1 (地板)",
                Location = new Point(20, 22),
                Size = new Size(110, 22),
                Checked = true
            };

            _rbLayer2 = new RadioButton
            {
                Text = "Layer2 (索引)",
                Location = new Point(140, 22),
                Size = new Size(110, 22)
            };

            _rbLayer4 = new RadioButton
            {
                Text = "Layer4 (物件)",
                Location = new Point(260, 22),
                Size = new Size(110, 22)
            };

            _grpLayer.GetControls().Add(_rbLayer1);
            _grpLayer.GetControls().Add(_rbLayer2);
            _grpLayer.GetControls().Add(_rbLayer4);

            y += 65;

            // === 來源設定 ===
            _grpSource = new GroupBox
            {
                Text = "來源",
                Location = new Point(leftMargin, y),
                Size = new Size(groupWidth, 90)
            };
            Controls.Add(_grpSource);

            var lblSrcTileId = new Label
            {
                Text = "TileId:",
                Location = new Point(20, 28),
                Size = new Size(50, 22)
            };
            _grpSource.GetControls().Add(lblSrcTileId);

            _nudSrcTileId = new NumericUpDown
            {
                Location = new Point(75, 25),
                Size = new Size(100, 24),
                Minimum = 0,
                Maximum = 65535,
                Value = 0
            };
            _grpSource.GetControls().Add(_nudSrcTileId);

            var lblSrcIndexId = new Label
            {
                Text = "IndexId:",
                Location = new Point(195, 28),
                Size = new Size(55, 22)
            };
            _grpSource.GetControls().Add(lblSrcIndexId);

            _nudSrcIndexId = new NumericUpDown
            {
                Location = new Point(255, 25),
                Size = new Size(100, 24),
                Minimum = 0,
                Maximum = 255,
                Value = 0
            };
            _grpSource.GetControls().Add(_nudSrcIndexId);

            _chkMatchIndexId = new CheckBox
            {
                Text = "比對 IndexId",
                Location = new Point(20, 58),
                Size = new Size(150, 22),
                Checked = true
            };
            _chkMatchIndexId.CheckedChanged += (s, e) =>
            {
                _nudSrcIndexId.Enabled = _chkMatchIndexId.Checked ?? false;
            };
            _grpSource.GetControls().Add(_chkMatchIndexId);

            y += 100;

            // === 目標設定 ===
            _grpTarget = new GroupBox
            {
                Text = "替換為",
                Location = new Point(leftMargin, y),
                Size = new Size(groupWidth, 90)
            };
            Controls.Add(_grpTarget);

            var lblDstTileId = new Label
            {
                Text = "TileId:",
                Location = new Point(20, 28),
                Size = new Size(50, 22)
            };
            _grpTarget.GetControls().Add(lblDstTileId);

            _nudDstTileId = new NumericUpDown
            {
                Location = new Point(75, 25),
                Size = new Size(100, 24),
                Minimum = 0,
                Maximum = 65535,
                Value = 0
            };
            _grpTarget.GetControls().Add(_nudDstTileId);

            var lblDstIndexId = new Label
            {
                Text = "IndexId:",
                Location = new Point(195, 28),
                Size = new Size(55, 22)
            };
            _grpTarget.GetControls().Add(lblDstIndexId);

            _nudDstIndexId = new NumericUpDown
            {
                Location = new Point(255, 25),
                Size = new Size(100, 24),
                Minimum = 0,
                Maximum = 255,
                Value = 0
            };
            _grpTarget.GetControls().Add(_nudDstIndexId);

            _chkReplaceIndexId = new CheckBox
            {
                Text = "替換 IndexId",
                Location = new Point(20, 58),
                Size = new Size(150, 22),
                Checked = true
            };
            _chkReplaceIndexId.CheckedChanged += (s, e) =>
            {
                _nudDstIndexId.Enabled = _chkReplaceIndexId.Checked ?? false;
            };
            _grpTarget.GetControls().Add(_chkReplaceIndexId);

            y += 105;

            // === 按鈕列 ===
            int btnWidth = 90;
            int btnHeight = 32;
            int btnSpacing = 15;
            int totalBtnWidth = btnWidth * 3 + btnSpacing * 2;
            int btnStartX = (groupWidth - totalBtnWidth) / 2 + leftMargin;

            _btnPreview = new Button
            {
                Text = "預覽",
                Location = new Point(btnStartX, y),
                Size = new Size(btnWidth, btnHeight)
            };
            _btnPreview.Click += (s, e) => PreviewClicked?.Invoke(this, EventArgs.Empty);
            Controls.Add(_btnPreview);

            _btnExecute = new Button
            {
                Text = "執行替換",
                Location = new Point(btnStartX + btnWidth + btnSpacing, y),
                Size = new Size(btnWidth, btnHeight)
            };
            _btnExecute.Click += (s, e) => ExecuteClicked?.Invoke(this, EventArgs.Empty);
            Controls.Add(_btnExecute);

            _btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(btnStartX + (btnWidth + btnSpacing) * 2, y),
                Size = new Size(btnWidth, btnHeight),
                DialogResult = DialogResult.Cancel
            };
            _btnCancel.Click += (s, e) => Close();
            Controls.Add(_btnCancel);

            y += 45;

            // === 結果標籤 ===
            _lblResult = new Label
            {
                Text = "",
                Location = new Point(leftMargin, y),
                Size = new Size(groupWidth, 22),
                ForeColor = Color.FromArgb(0, 100, 180)
            };
            Controls.Add(_lblResult);

            CancelButton = _btnCancel;
        }

        private void UpdateLocalization()
        {
            Text = LocalizationManager.L("Form_BatchReplaceTile_Title");
            _grpLayer.Text = LocalizationManager.L("BatchReplaceTile_SelectLayer");
            _rbLayer1.Text = LocalizationManager.L("BatchReplaceTile_Layer1");
            _rbLayer2.Text = LocalizationManager.L("BatchReplaceTile_Layer2");
            _rbLayer4.Text = LocalizationManager.L("BatchReplaceTile_Layer4");
            _grpSource.Text = LocalizationManager.L("BatchReplaceTile_Source");
            _grpTarget.Text = LocalizationManager.L("BatchReplaceTile_Target");
            _chkMatchIndexId.Text = LocalizationManager.L("BatchReplaceTile_MatchIndexId");
            _chkReplaceIndexId.Text = LocalizationManager.L("BatchReplaceTile_ReplaceIndexId");
            _btnPreview.Text = LocalizationManager.L("Button_Preview");
            _btnExecute.Text = LocalizationManager.L("BatchReplaceTile_Execute");
            _btnCancel.Text = LocalizationManager.L("Button_Cancel");
        }

        /// <summary>
        /// 取得圖層名稱
        /// </summary>
        public string GetLayerName()
        {
            return SelectedLayer switch
            {
                1 => "Layer1",
                2 => "Layer2",
                4 => "Layer4",
                _ => "Layer1"
            };
        }
    }
}
