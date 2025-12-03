using System;
using System.Drawing;
using System.Windows.Forms;

namespace L1FlyMapViewer
{
    public class CopySettingsDialog : Form
    {
        private CheckBox chkLayer1 = null!;
        private CheckBox chkLayer2 = null!;
        private CheckBox chkLayer3 = null!;
        private CheckBox chkLayer4 = null!;
        private CheckBox chkLayer5 = null!;
        private CheckBox chkLayer6to8 = null!;
        private Button btnOK = null!;
        private Button btnCancel = null!;
        private Label lblDescription = null!;

        public bool CopyLayer1 { get; private set; }
        public bool CopyLayer2 { get; private set; }
        public bool CopyLayer3 { get; private set; }
        public bool CopyLayer4 { get; private set; }
        public bool CopyLayer5 { get; private set; }
        public bool CopyLayer6to8 { get; private set; }

        // 保持向後相容
        public bool CopyLayer5to8 => CopyLayer5 || CopyLayer6to8;

        public CopySettingsDialog(bool currentLayer1, bool currentLayer2, bool currentLayer3, bool currentLayer4, bool currentLayer5, bool currentLayer6to8)
        {
            CopyLayer1 = currentLayer1;
            CopyLayer2 = currentLayer2;
            CopyLayer3 = currentLayer3;
            CopyLayer4 = currentLayer4;
            CopyLayer5 = currentLayer5;
            CopyLayer6to8 = currentLayer6to8;
            InitializeComponent();
        }

        // 舊版建構子（向後相容）
        public CopySettingsDialog(bool currentLayer1, bool currentLayer2, bool currentLayer3, bool currentLayer4, bool currentLayer5to8)
            : this(currentLayer1, currentLayer2, currentLayer3, currentLayer4, currentLayer5to8, currentLayer5to8)
        {
        }

        private void InitializeComponent()
        {
            this.Text = "複製/刪除設定";
            this.Size = new Size(300, 310);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // 說明文字
            lblDescription = new Label
            {
                Text = "選擇要複製/刪除的圖層：",
                Location = new Point(15, 15),
                Size = new Size(260, 20),
                Font = new Font(this.Font, FontStyle.Bold)
            };

            // Layer 1 選項
            chkLayer1 = new CheckBox
            {
                Text = "Layer 1 - 地板 (Tile)",
                Location = new Point(25, 45),
                Size = new Size(240, 24),
                Checked = CopyLayer1
            };

            // Layer 2 選項
            chkLayer2 = new CheckBox
            {
                Text = "Layer 2 - Tile 索引表",
                Location = new Point(25, 72),
                Size = new Size(240, 24),
                Checked = CopyLayer2
            };

            // Layer 3 選項
            chkLayer3 = new CheckBox
            {
                Text = "Layer 3 - 屬性 (通行性)",
                Location = new Point(25, 99),
                Size = new Size(240, 24),
                Checked = CopyLayer3
            };

            // Layer 4 選項
            chkLayer4 = new CheckBox
            {
                Text = "Layer 4 - 物件 (Object)",
                Location = new Point(25, 126),
                Size = new Size(240, 24),
                Checked = CopyLayer4
            };

            // Layer 5 選項（透明/消失設定）
            chkLayer5 = new CheckBox
            {
                Text = "Layer 5 - 透明/消失設定",
                Location = new Point(25, 153),
                Size = new Size(240, 24),
                Checked = CopyLayer5
            };

            // Layer 6-8 選項
            chkLayer6to8 = new CheckBox
            {
                Text = "Layer 6-8 - Til索引/傳送點/特效",
                Location = new Point(25, 180),
                Size = new Size(240, 24),
                Checked = CopyLayer6to8
            };

            // 確定按鈕
            btnOK = new Button
            {
                Text = "確定",
                Location = new Point(100, 225),
                Size = new Size(75, 28),
                DialogResult = DialogResult.OK
            };
            btnOK.Click += BtnOK_Click;

            // 取消按鈕
            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(190, 225),
                Size = new Size(75, 28),
                DialogResult = DialogResult.Cancel
            };

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;

            this.Controls.AddRange(new Control[] {
                lblDescription,
                chkLayer1,
                chkLayer2,
                chkLayer3,
                chkLayer4,
                chkLayer5,
                chkLayer6to8,
                btnOK,
                btnCancel
            });
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            if (!chkLayer1.Checked && !chkLayer2.Checked && !chkLayer3.Checked && !chkLayer4.Checked && !chkLayer5.Checked && !chkLayer6to8.Checked)
            {
                MessageBox.Show("請至少選擇一個圖層", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            CopyLayer1 = chkLayer1.Checked;
            CopyLayer2 = chkLayer2.Checked;
            CopyLayer3 = chkLayer3.Checked;
            CopyLayer4 = chkLayer4.Checked;
            CopyLayer5 = chkLayer5.Checked;
            CopyLayer6to8 = chkLayer6to8.Checked;
        }
    }
}
