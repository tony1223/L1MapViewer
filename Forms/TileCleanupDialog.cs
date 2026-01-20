using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.Compatibility;
using L1MapViewer.Helper;
using L1MapViewer.Localization;

namespace L1MapViewer.Forms
{
    /// <summary>
    /// Tile 清理確認對話框
    /// </summary>
    public class TileCleanupDialog : Dialog
    {
        private Label lblWarning;
        private Label lblSummary;
        private ListBox lstTiles;
        private Button btnSelectAll;
        private Button btnSelectNone;
        private CheckBox chkCreateBackup;
        private Button btnConfirm;
        private Button btnCancel;
        private ProgressBar progressBar;
        private Label lblProgress;
        private StackLayout progressPanel;
        private StackLayout listPanel;

        private TileCleanupHelper.ScanResult _scanResult;
        private HashSet<int> _checkedTileIds = new HashSet<int>();
        private bool _confirmed = false;

        /// <summary>
        /// 使用者選擇要刪除的 Tile ID
        /// </summary>
        public List<int> SelectedTileIds { get; private set; } = new List<int>();

        /// <summary>
        /// 是否建立備份
        /// </summary>
        public bool CreateBackup => chkCreateBackup?.Checked ?? true;

        /// <summary>
        /// 使用者是否確認執行
        /// </summary>
        public bool Confirmed => _confirmed;

        public TileCleanupDialog()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            Title = LocalizationManager.L("Dialog_TileCleanup_Title");
            MinimumSize = new Size(500, 400);
            Size = new Size(600, 550);
            Resizable = false;

            // 警告標籤（紅色）
            lblWarning = new Label
            {
                Text = "⚠️ " + LocalizationManager.L("Dialog_TileCleanup_Warning"),
                TextColor = Colors.Red,
                Font = new Font(SystemFont.Bold, 10),
            };

            // 摘要標籤
            lblSummary = new Label
            {
                Text = LocalizationManager.L("Dialog_TileCleanup_Scanning"),
            };

            // 進度條
            progressBar = new ProgressBar();

            // 進度文字
            lblProgress = new Label { Text = "" };

            // 進度面板（掃描時顯示）
            progressPanel = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 5,
                Items = { progressBar, lblProgress }
            };

            // Tile 列表
            lstTiles = new ListBox { Height = 250 };
            lstTiles.SelectedIndexChanged += LstTiles_SelectedIndexChanged;

            // 全選按鈕
            btnSelectAll = new Button { Text = LocalizationManager.L("Button_SelectAll") };
            btnSelectAll.Click += BtnSelectAll_Click;

            // 取消全選按鈕
            btnSelectNone = new Button { Text = LocalizationManager.L("Button_SelectNone") };
            btnSelectNone.Click += BtnSelectNone_Click;

            // 建立備份選項
            chkCreateBackup = new CheckBox
            {
                Text = LocalizationManager.L("Dialog_TileCleanup_CreateBackup"),
                Checked = true
            };

            // 列表面板（掃描完成後顯示）
            listPanel = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 5,
                Visible = false,
                Items =
                {
                    lstTiles,
                    new StackLayout
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        Items = { btnSelectAll, btnSelectNone }
                    },
                    chkCreateBackup
                }
            };

            // 確認按鈕（紅色背景）
            btnConfirm = new Button { Text = LocalizationManager.L("Dialog_TileCleanup_Confirm") };
            btnConfirm.Click += BtnConfirm_Click;
            btnConfirm.Visible = false;

            // 取消按鈕
            btnCancel = new Button { Text = LocalizationManager.L("Button_Cancel") };
            btnCancel.Click += (s, e) => Close();

            // 主要佈局
            Content = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Padding = new Padding(15),
                Spacing = 10,
                Items =
                {
                    lblWarning,
                    lblSummary,
                    progressPanel,
                    new StackLayoutItem(listPanel, true),
                    new StackLayout
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        Items =
                        {
                            null, // 彈性空間
                            btnConfirm,
                            btnCancel
                        }
                    }
                }
            };
        }

        /// <summary>
        /// 點擊列表項目時切換勾選狀態
        /// </summary>
        private void LstTiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstTiles.SelectedIndex < 0) return;

            var item = lstTiles.SelectedValue as TileListItem;
            if (item != null)
            {
                item.IsChecked = !item.IsChecked;
                if (item.IsChecked)
                    _checkedTileIds.Add(item.TileId);
                else
                    _checkedTileIds.Remove(item.TileId);

                // 更新顯示
                int idx = lstTiles.SelectedIndex;
                lstTiles.Items[idx] = item;
                lstTiles.SelectedIndex = idx;
            }
        }

        /// <summary>
        /// 開始掃描
        /// </summary>
        public void StartScan(int threshold = 5000)
        {
            progressBar.Value = 0;
            progressBar.MaxValue = 100;

            // 在背景執行緒中執行掃描
            var worker = new System.ComponentModel.BackgroundWorker();
            worker.WorkerReportsProgress = true;

            worker.DoWork += (s, e) =>
            {
                _scanResult = TileCleanupHelper.ScanUnusedTiles(threshold, (current, total, fileName) =>
                {
                    int percent = total > 0 ? (current * 100 / total) : 0;
                    worker.ReportProgress(percent, $"[{current}/{total}] {fileName}");
                });
            };

            worker.ProgressChanged += (s, e) =>
            {
                Application.Instance.Invoke(() =>
                {
                    progressBar.Value = e.ProgressPercentage;
                    lblProgress.Text = e.UserState?.ToString() ?? "";
                });
            };

            worker.RunWorkerCompleted += (s, e) =>
            {
                Application.Instance.Invoke(() => OnScanCompleted());
            };

            worker.RunWorkerAsync();
        }

        private void OnScanCompleted()
        {
            progressBar.Value = 100;
            progressPanel.Visible = false;

            if (_scanResult == null)
            {
                lblSummary.Text = LocalizationManager.L("Dialog_TileCleanup_ScanFailed");
                return;
            }

            // 顯示摘要
            lblSummary.Text = string.Format(
                LocalizationManager.L("Dialog_TileCleanup_Summary"),
                _scanResult.MapFolderCount,
                _scanResult.FileCount,
                _scanResult.UsedTileIds.Count,
                _scanResult.AllTileIds.Count,
                _scanResult.UnusedHighTileIds.Count,
                _scanResult.Threshold
            );

            // 填充列表（預設全選）
            lstTiles.Items.Clear();
            _checkedTileIds.Clear();
            foreach (int tileId in _scanResult.UnusedHighTileIds)
            {
                var item = new TileListItem(tileId, true);
                lstTiles.Items.Add(item);
                _checkedTileIds.Add(tileId);
            }

            // 顯示列表面板和確認按鈕
            listPanel.Visible = true;
            btnConfirm.Visible = true;

            // 如果沒有要清理的項目
            if (_scanResult.UnusedHighTileIds.Count == 0)
            {
                lstTiles.Items.Add(new TileListItem(-1, false) { DisplayText = LocalizationManager.L("Dialog_TileCleanup_NoUnusedTiles") });
                btnConfirm.Enabled = false;
            }

            // 顯示錯誤（如果有）
            if (_scanResult.Errors.Count > 0)
            {
                string errorSummary = string.Join("\n", _scanResult.Errors.Take(5));
                if (_scanResult.Errors.Count > 5)
                {
                    errorSummary += $"\n... ({_scanResult.Errors.Count - 5} more errors)";
                }
                WinFormsMessageBox.Show(errorSummary, LocalizationManager.L("Dialog_TileCleanup_ScanErrors"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnSelectAll_Click(object sender, EventArgs e)
        {
            _checkedTileIds.Clear();
            for (int i = 0; i < lstTiles.Items.Count; i++)
            {
                var item = lstTiles.Items[i] as TileListItem;
                if (item != null && item.TileId > 0)
                {
                    item.IsChecked = true;
                    _checkedTileIds.Add(item.TileId);
                    lstTiles.Items[i] = item;
                }
            }
        }

        private void BtnSelectNone_Click(object sender, EventArgs e)
        {
            _checkedTileIds.Clear();
            for (int i = 0; i < lstTiles.Items.Count; i++)
            {
                var item = lstTiles.Items[i] as TileListItem;
                if (item != null)
                {
                    item.IsChecked = false;
                    lstTiles.Items[i] = item;
                }
            }
        }

        private void BtnConfirm_Click(object sender, EventArgs e)
        {
            SelectedTileIds = _checkedTileIds.ToList();

            if (SelectedTileIds.Count == 0)
            {
                WinFormsMessageBox.Show(LocalizationManager.L("Dialog_TileCleanup_NoSelection"), LocalizationManager.L("Dialog_TileCleanup_Title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 最終確認
            var result = WinFormsMessageBox.Show(
                string.Format(LocalizationManager.L("Dialog_TileCleanup_FinalConfirm"), SelectedTileIds.Count),
                LocalizationManager.L("Dialog_TileCleanup_Title"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                _confirmed = true;
                Close();
            }
        }

        /// <summary>
        /// 列表項目類別（支援勾選狀態）
        /// </summary>
        private class TileListItem
        {
            public int TileId { get; set; }
            public bool IsChecked { get; set; }
            public string DisplayText { get; set; }

            public TileListItem(int tileId, bool isChecked)
            {
                TileId = tileId;
                IsChecked = isChecked;
                DisplayText = tileId > 0 ? $"{tileId}.til" : "";
            }

            public override string ToString()
            {
                string check = IsChecked ? "☑" : "☐";
                return TileId > 0 ? $"{check} {TileId}.til" : DisplayText;
            }
        }
    }
}
