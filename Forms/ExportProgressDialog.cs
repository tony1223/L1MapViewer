using System;
using System.Threading;
using System.Threading.Tasks;
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.Helper;
using L1MapViewer.Localization;
using NLog;

namespace L1MapViewer.Forms
{
    /// <summary>
    /// 匯出進度對話框
    /// </summary>
    public class ExportProgressDialog : Dialog<bool>
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private ProgressBar progressBar;
        private Label lblStatus;
        private Label lblProgress;
        private Button btnCancel;

        private CancellationTokenSource _cts;
        private Func<CancellationToken, Task> _exportTask;
        private bool _isCompleted = false;

        /// <summary>
        /// 建立匯出進度對話框
        /// </summary>
        public ExportProgressDialog()
        {
            Title = LocalizationManager.L("ExportImage_Exporting");
            Resizable = false;
            Closeable = false; // 禁止直接關閉，必須使用取消按鈕

            _cts = new CancellationTokenSource();

            // 進度條
            progressBar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                Width = 300
            };

            // 狀態文字
            lblStatus = new Label
            {
                Text = LocalizationManager.L("ExportImage_Exporting"),
                TextAlignment = TextAlignment.Center
            };

            // 進度文字
            lblProgress = new Label
            {
                Text = "0%",
                TextAlignment = TextAlignment.Center
            };

            // 取消按鈕
            btnCancel = new Button
            {
                Text = LocalizationManager.L("Button_Cancel"),
                Width = 80
            };
            btnCancel.Click += OnCancelClick;

            // 佈局
            Content = new StackLayout
            {
                Padding = new Padding(20),
                Spacing = 15,
                HorizontalContentAlignment = Eto.Forms.HorizontalAlignment.Center,
                Items =
                {
                    lblStatus,
                    progressBar,
                    lblProgress,
                    btnCancel
                }
            };

            ClientSize = new Size(360, 160);
        }

        /// <summary>
        /// 設定匯出任務
        /// </summary>
        public void SetExportTask(Func<CancellationToken, Task> exportTask)
        {
            _exportTask = exportTask;
        }

        /// <summary>
        /// 更新進度
        /// </summary>
        public void UpdateProgress(ExportProgress progress)
        {
            if (_isCompleted) return;

            Application.Instance.Invoke(() =>
            {
                progressBar.Value = (int)progress.Percentage;
                lblProgress.Text = $"{progress.CurrentTile}/{progress.TotalTiles} ({progress.Percentage:F0}%)";
                if (!string.IsNullOrEmpty(progress.Status))
                {
                    lblStatus.Text = progress.Status;
                }
            });
        }

        /// <summary>
        /// 顯示對話框並執行匯出
        /// </summary>
        public async Task<bool> ShowAndExportAsync(Control parent)
        {
            if (_exportTask == null)
            {
                throw new InvalidOperationException("Export task not set. Call SetExportTask first.");
            }

            // 在對話框顯示後開始匯出任務
            Shown += async (s, e) =>
            {
                try
                {
                    await _exportTask(_cts.Token);
                    _isCompleted = true;
                    Result = true;

                    Application.Instance.Invoke(() =>
                    {
                        Close();
                    });
                }
                catch (OperationCanceledException)
                {
                    _logger.Info("[ExportProgressDialog] Export cancelled");
                    _isCompleted = true;
                    Result = false;

                    Application.Instance.Invoke(() =>
                    {
                        Close();
                    });
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "[ExportProgressDialog] Export failed");
                    _isCompleted = true;
                    Result = false;

                    Application.Instance.Invoke(() =>
                    {
                        Eto.Forms.MessageBox.Show(
                            this,
                            LocalizationManager.L("Message_ExportFailed", ex.Message),
                            LocalizationManager.L("Title_Error"),
                            Eto.Forms.MessageBoxButtons.OK,
                            Eto.Forms.MessageBoxType.Error);
                        Close();
                    });
                }
            };

            ShowModal(parent);
            return Result;
        }

        /// <summary>
        /// 取消按鈕點擊
        /// </summary>
        private void OnCancelClick(object sender, EventArgs e)
        {
            if (_isCompleted) return;

            btnCancel.Enabled = false;
            lblStatus.Text = LocalizationManager.L("ExportImage_Cancelling");
            _cts.Cancel();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
