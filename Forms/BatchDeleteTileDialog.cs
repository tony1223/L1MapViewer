using System;
// using System.Drawing; // Replaced with Eto.Drawing
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.Compatibility;
using L1MapViewer.Localization;

namespace L1MapViewer.Forms
{
    /// <summary>
    /// 批次刪除 Tile 對話框
    /// </summary>
    public class BatchDeleteTileDialog : WinFormsDialog
    {
        /// <summary>
        /// TileId 起始值
        /// </summary>
        public int TileIdStart => (int)nudTileIdStart.Value;

        /// <summary>
        /// TileId 結束值
        /// </summary>
        public int TileIdEnd => (int)nudTileIdEnd.Value;

        /// <summary>
        /// IndexId 起始值
        /// </summary>
        public int IndexIdStart => (int)nudIndexIdStart.Value;

        /// <summary>
        /// IndexId 結束值
        /// </summary>
        public int IndexIdEnd => (int)nudIndexIdEnd.Value;

        /// <summary>
        /// 是否處理所有地圖
        /// </summary>
        public bool ProcessAllMaps => rbAllMaps.Checked;

        private GroupBox grpTileId;
        private GroupBox grpIndexId;
        private GroupBox grpScope;
        private NumericUpDown nudTileIdStart;
        private NumericUpDown nudTileIdEnd;
        private NumericUpDown nudIndexIdStart;
        private NumericUpDown nudIndexIdEnd;
        private RadioButton rbCurrentMap;
        private RadioButton rbAllMaps;
        private Label lblTileIdStart;
        private Label lblTileIdEnd;
        private Label lblIndexIdStart;
        private Label lblIndexIdEnd;
        private Label lblWarning;
        private Button btnDelete;
        private Button btnCancel;

        private bool _hasCurrentMap;

        /// <summary>
        /// 建立批次刪除 Tile 對話框
        /// </summary>
        /// <param name="hasCurrentMap">是否已載入地圖</param>
        public BatchDeleteTileDialog(bool hasCurrentMap = true)
        {
            _hasCurrentMap = hasCurrentMap;
            InitializeComponents();
            UpdateLocalization();
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
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
            Text = "批次刪除 Tile";
            Size = new Size(340, 340);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            int y = 15;

            // TileId 範圍
            grpTileId = new GroupBox
            {
                Text = "TileId 範圍",
                Location = new Point(15, y),
                Size = new Size(295, 60)
            };
            Controls.Add(grpTileId);

            lblTileIdStart = new Label
            {
                Text = "起始:",
                Location = new Point(15, 25),
                Size = new Size(40, 20)
            };
            grpTileId.GetControls().Add(lblTileIdStart);

            nudTileIdStart = new NumericUpDown
            {
                Location = new Point(55, 22),
                Size = new Size(80, 23),
                Minimum = 0,
                Maximum = 65535,
                Value = 0
            };
            grpTileId.GetControls().Add(nudTileIdStart);

            lblTileIdEnd = new Label
            {
                Text = "結束:",
                Location = new Point(150, 25),
                Size = new Size(40, 20)
            };
            grpTileId.GetControls().Add(lblTileIdEnd);

            nudTileIdEnd = new NumericUpDown
            {
                Location = new Point(190, 22),
                Size = new Size(80, 23),
                Minimum = 0,
                Maximum = 65535,
                Value = 65535
            };
            grpTileId.GetControls().Add(nudTileIdEnd);

            y += 70;

            // IndexId 範圍
            grpIndexId = new GroupBox
            {
                Text = "IndexId 範圍",
                Location = new Point(15, y),
                Size = new Size(295, 60)
            };
            Controls.Add(grpIndexId);

            lblIndexIdStart = new Label
            {
                Text = "起始:",
                Location = new Point(15, 25),
                Size = new Size(40, 20)
            };
            grpIndexId.GetControls().Add(lblIndexIdStart);

            nudIndexIdStart = new NumericUpDown
            {
                Location = new Point(55, 22),
                Size = new Size(80, 23),
                Minimum = 0,
                Maximum = 255,
                Value = 0
            };
            grpIndexId.GetControls().Add(nudIndexIdStart);

            lblIndexIdEnd = new Label
            {
                Text = "結束:",
                Location = new Point(150, 25),
                Size = new Size(40, 20)
            };
            grpIndexId.GetControls().Add(lblIndexIdEnd);

            nudIndexIdEnd = new NumericUpDown
            {
                Location = new Point(190, 22),
                Size = new Size(80, 23),
                Minimum = 0,
                Maximum = 255,
                Value = 255
            };
            grpIndexId.GetControls().Add(nudIndexIdEnd);

            y += 70;

            // 範圍選擇
            grpScope = new GroupBox
            {
                Text = "處理範圍",
                Location = new Point(15, y),
                Size = new Size(295, 75)
            };
            Controls.Add(grpScope);

            rbCurrentMap = new RadioButton
            {
                Text = "當前地圖",
                Location = new Point(15, 25),
                Size = new Size(260, 20),
                Checked = _hasCurrentMap,
                Enabled = _hasCurrentMap
            };
            grpScope.GetControls().Add(rbCurrentMap);

            rbAllMaps = new RadioButton
            {
                Text = "所有地圖 (maps 資料夾)",
                Location = new Point(15, 48),
                Size = new Size(260, 20),
                Checked = !_hasCurrentMap
            };
            grpScope.GetControls().Add(rbAllMaps);

            y += 85;

            // 警告訊息
            lblWarning = new Label
            {
                Text = "此操作會直接修改 S32 檔案，請先備份！",
                Location = new Point(15, y),
                Size = new Size(295, 20),
                ForeColor = Colors.Red
            };
            Controls.Add(lblWarning);

            y += 30;

            // 按鈕
            btnDelete = new Button
            {
                Text = "刪除",
                Location = new Point(130, y),
                Size = new Size(80, 28),
                DialogResult = DialogResult.Ok
            };
            btnDelete.Click += BtnDelete_Click;
            Controls.Add(btnDelete);

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(220, y),
                Size = new Size(80, 28),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(btnCancel);

            AcceptButton = btnDelete;
            CancelButton = btnCancel;
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            // 驗證範圍
            if (nudTileIdStart.Value > nudTileIdEnd.Value)
            {
                WinFormsMessageBox.Show(
                    LocalizationManager.L("BatchDeleteTile_InvalidTileIdRange"),
                    LocalizationManager.L("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            if (nudIndexIdStart.Value > nudIndexIdEnd.Value)
            {
                WinFormsMessageBox.Show(
                    LocalizationManager.L("BatchDeleteTile_InvalidIndexIdRange"),
                    LocalizationManager.L("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            // 確認刪除
            var scope = rbAllMaps.Checked
                ? LocalizationManager.L("BatchDeleteTile_AllMaps")
                : LocalizationManager.L("BatchDeleteTile_CurrentMap");
            var message = string.Format(
                LocalizationManager.L("BatchDeleteTile_ConfirmMessage"),
                TileIdStart, TileIdEnd, IndexIdStart, IndexIdEnd, scope);

            var result = WinFormsMessageBox.Show(
                message,
                LocalizationManager.L("BatchDeleteTile_Confirm"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
            {
                DialogResult = DialogResult.None;
            }
        }

        private void UpdateLocalization()
        {
            Text = LocalizationManager.L("Form_BatchDeleteTile_Title");
            grpTileId.Text = LocalizationManager.L("BatchDeleteTile_TileIdRange");
            grpIndexId.Text = LocalizationManager.L("BatchDeleteTile_IndexIdRange");
            grpScope.Text = LocalizationManager.L("BatchDeleteTile_Scope");
            lblTileIdStart.Text = LocalizationManager.L("BatchDeleteTile_Start") + ":";
            lblTileIdEnd.Text = LocalizationManager.L("BatchDeleteTile_End") + ":";
            lblIndexIdStart.Text = LocalizationManager.L("BatchDeleteTile_Start") + ":";
            lblIndexIdEnd.Text = LocalizationManager.L("BatchDeleteTile_End") + ":";
            rbCurrentMap.Text = LocalizationManager.L("BatchDeleteTile_CurrentMap");
            rbAllMaps.Text = LocalizationManager.L("BatchDeleteTile_AllMaps");
            lblWarning.Text = LocalizationManager.L("BatchDeleteTile_Warning");
            btnDelete.Text = LocalizationManager.L("Button_Delete");
            btnCancel.Text = LocalizationManager.L("Button_Cancel");
        }
    }
}
