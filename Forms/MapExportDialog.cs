using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.Compatibility;
using L1MapViewer.Helper;
using L1MapViewer.Localization;
using L1MapViewer.Models;
using NLog;
using SkiaSharp;

namespace L1MapViewer.Forms
{
    /// <summary>
    /// 地圖圖片輸出對話框
    /// </summary>
    public class MapExportDialog : Dialog<DialogResult>
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly MapDocument _document;
        private readonly bool _hasDocument;

        // 圖層選項
        private CheckBox cbLayer1;
        private CheckBox cbLayer2;
        private CheckBox cbLayer4;
        private CheckBox cbLayer8;

        // 輸出模式
        private RadioButton rbSingleMap;
        private RadioButton rbBatchAll;

        // 縮放等級
        private Label lblScale;
        private Label lblMaxSize;
        private DropDown cmbScale;


        // 自訂標記
        private GroupBox grpCustomMarkers;
        private TextArea txtCustomMarkers;
        private Label lblMarkersHint;
        private Label lblMarkerSize;
        private NumericStepper numMarkerSize;

        // 進度
        private ProgressBar progressBar;
        private Label lblProgress;

        // 按鈕
        private Button btnExport;
        private Button btnCancel;

        // 批次輸出取消
        private CancellationTokenSource _cts;

        public MapExportDialog(MapDocument document)
        {
            _document = document;
            _hasDocument = document != null;

            Title = LocalizationManager.L("Dialog_MapExport_Title");
            MinimumSize = new Size(480, 450);
            Resizable = false;

            BuildUI();
            UpdateUIState();

            _logger.Debug($"[MapExportDialog] Created: hasDocument={_hasDocument}");
        }

        private void BuildUI()
        {
            var layout = new DynamicLayout { DefaultSpacing = new Size(5, 5), Padding = new Padding(10) };

            // 圖層選項群組
            var layersGroup = new GroupBox { Text = LocalizationManager.L("Dialog_MapExport_Layers") };
            var layersLayout = new DynamicLayout { DefaultSpacing = new Size(10, 5), Padding = new Padding(10) };

            cbLayer1 = new CheckBox { Text = "Layer1 " + LocalizationManager.L("Layer1_Name"), Checked = true };
            cbLayer2 = new CheckBox { Text = "Layer2 " + LocalizationManager.L("Layer2_Name"), Checked = true };
            cbLayer4 = new CheckBox { Text = "Layer4 " + LocalizationManager.L("Layer4_Name"), Checked = true };
            cbLayer8 = new CheckBox { Text = "Layer8 " + LocalizationManager.L("Layer8_Name"), Checked = true };

            layersLayout.AddRow(cbLayer1, cbLayer2);
            layersLayout.AddRow(cbLayer4, cbLayer8);
            layersGroup.Content = layersLayout;
            layout.AddRow(layersGroup);

            // 輸出模式群組
            var modeGroup = new GroupBox { Text = LocalizationManager.L("Dialog_MapExport_Mode") };
            var modeLayout = new DynamicLayout { DefaultSpacing = new Size(5, 5), Padding = new Padding(10) };

            rbSingleMap = new RadioButton { Text = LocalizationManager.L("Dialog_MapExport_SingleMap"), Checked = true, Enabled = _hasDocument };
            rbBatchAll = new RadioButton(rbSingleMap) { Text = LocalizationManager.L("Dialog_MapExport_BatchAll") };

            rbSingleMap.CheckedChanged += (s, e) => UpdateUIState();
            rbBatchAll.CheckedChanged += (s, e) => UpdateUIState();

            modeLayout.AddRow(rbSingleMap);
            modeLayout.AddRow(rbBatchAll);
            modeGroup.Content = modeLayout;
            layout.AddRow(modeGroup);

            // 縮放等級群組
            var scaleGroup = new GroupBox { Text = LocalizationManager.L("Dialog_MapExport_Scale") };
            var scaleLayout = new DynamicLayout { DefaultSpacing = new Size(5, 5), Padding = new Padding(10) };

            lblMaxSize = new Label { Text = "", TextColor = Colors.Blue };
            lblScale = new Label { Text = LocalizationManager.L("Dialog_MapExport_ScaleHint"), TextColor = Colors.Gray };
            cmbScale = new DropDown { Width = 200 };
            cmbScale.Items.Add(new ListItem { Text = "100%", Key = "100" });
            cmbScale.Items.Add(new ListItem { Text = "80%", Key = "80" });
            cmbScale.Items.Add(new ListItem { Text = "60%", Key = "60" });
            cmbScale.Items.Add(new ListItem { Text = "40%", Key = "40" });
            cmbScale.Items.Add(new ListItem { Text = "20%", Key = "20" });
            cmbScale.Items.Add(new ListItem { Text = "10%", Key = "10" });
            cmbScale.SelectedIndex = 0;

            scaleLayout.AddRow(lblMaxSize);
            scaleLayout.AddRow(cmbScale, null);
            scaleLayout.AddRow(lblScale);
            scaleGroup.Content = scaleLayout;
            layout.AddRow(scaleGroup);

            // 自訂標記群組
            grpCustomMarkers = new GroupBox { Text = LocalizationManager.L("Dialog_MapExport_CustomMarkers") };
            var markersLayout = new DynamicLayout { DefaultSpacing = new Size(5, 5), Padding = new Padding(10) };

            lblMarkersHint = new Label
            {
                Text = LocalizationManager.L("Dialog_MapExport_MarkersHint"),
                TextColor = Colors.Gray
            };
            txtCustomMarkers = new TextArea
            {
                Height = 100,
                Font = new Font("Consolas", 9)
            };

            // 標記大小等級 (1-10)
            lblMarkerSize = new Label { Text = LocalizationManager.L("Dialog_MapExport_MarkerSize") };
            numMarkerSize = new NumericStepper
            {
                MinValue = 1,
                MaxValue = 10,
                Value = 5,
                Increment = 1,
                Width = 60
            };

            markersLayout.AddRow(lblMarkersHint);
            markersLayout.AddRow(txtCustomMarkers);
            markersLayout.AddRow(lblMarkerSize, numMarkerSize, null);
            grpCustomMarkers.Content = markersLayout;
            layout.AddRow(grpCustomMarkers);

            // 進度區域
            progressBar = new ProgressBar { Visible = false };
            lblProgress = new Label { Text = "", Visible = false };
            layout.AddRow(progressBar);
            layout.AddRow(lblProgress);

            // 按鈕
            btnExport = new Button { Text = LocalizationManager.L("Dialog_MapExport_Export") };
            btnExport.Click += BtnExport_Click;

            btnCancel = new Button { Text = LocalizationManager.L("Button_Cancel") };
            btnCancel.Click += BtnCancel_Click;

            layout.AddRow(new Panel { Height = 10 }); // spacer
            layout.AddRow(new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows = { new TableRow(null, btnExport, btnCancel, null) }
            });

            Content = layout;
            DefaultButton = btnExport;
            AbortButton = btnCancel;
        }

        private void UpdateUIState()
        {
            bool isBatchMode = rbBatchAll.Checked;

            // 批次模式時隱藏自訂標記
            grpCustomMarkers.Visible = !isBatchMode;

            // 無地圖時強制批次模式
            if (!_hasDocument)
            {
                rbBatchAll.Checked = true;
            }

            // 更新最大尺寸顯示
            UpdateMaxSizeLabel();
        }

        /// <summary>
        /// 更新最大輸出尺寸顯示
        /// </summary>
        private void UpdateMaxSizeLabel()
        {
            if (_hasDocument && !rbBatchAll.Checked)
            {
                // 單張模式：顯示此地圖的最大可輸出尺寸
                var (maxW, maxH) = GetMaxOutputSize(_document);
                lblMaxSize.Text = string.Format(LocalizationManager.L("Dialog_MapExport_MaxSizeFormat"), maxW, maxH);
            }
            else
            {
                // 批次模式：隱藏或顯示通用提示
                lblMaxSize.Text = LocalizationManager.L("Dialog_MapExport_BatchSizeHint");
            }
        }

        /// <summary>
        /// 取得地圖的最大可輸出尺寸（在記憶體上限內）
        /// </summary>
        private (int width, int height) GetMaxOutputSize(MapDocument doc)
        {
            if (doc == null)
                return (0, 0);

            long originalPixels = (long)doc.MapPixelWidth * doc.MapPixelHeight;
            long maxPixels = MapExporter.MAX_MEMORY_BYTES / 4;

            if (originalPixels <= maxPixels)
            {
                // 原始尺寸在上限內
                return (doc.MapPixelWidth, doc.MapPixelHeight);
            }

            // 計算最大縮放比例
            double maxScale = Math.Sqrt((double)maxPixels / originalPixels);
            int maxW = (int)(doc.MapPixelWidth * maxScale);
            int maxH = (int)(doc.MapPixelHeight * maxScale);
            return (maxW, maxH);
        }

        /// <summary>
        /// 顯示檔案/資料夾選擇對話框
        /// </summary>
        /// <returns>選擇的路徑，取消則回傳 null</returns>
        private string ShowOutputPathDialog(bool isBatchMode)
        {
            if (isBatchMode)
            {
                using var dialog = new SelectFolderDialog
                {
                    Title = LocalizationManager.L("Dialog_MapExport_OutputPath"),
                    Directory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (dialog.ShowDialog(this) == DialogResult.Ok)
                {
                    return dialog.Directory;
                }
            }
            else
            {
                string defaultFileName = _hasDocument ? $"{_document.MapId}.png" : "map.png";
                using var dialog = new SaveFileDialog
                {
                    Title = LocalizationManager.L("Dialog_MapExport_OutputPath"),
                    FileName = defaultFileName,
                    Directory = new Uri(Environment.GetFolderPath(Environment.SpecialFolder.Desktop))
                };
                dialog.Filters.Add(new FileFilter("PNG Image", ".png"));

                if (dialog.ShowDialog(this) == DialogResult.Ok)
                {
                    return dialog.FileName;
                }
            }

            return null;
        }

        private async void BtnExport_Click(object sender, EventArgs e)
        {
            try
            {
                // 取得縮放等級（相對於最大可輸出尺寸的百分比）
                int relativeScalePercent = 100;
                if (cmbScale.SelectedKey != null && int.TryParse(cmbScale.SelectedKey, out int parsedScale))
                {
                    relativeScalePercent = parsedScale;
                }

                bool isBatchMode = rbBatchAll.Checked;

                // 單張輸出時先檢查是否有地圖
                if (!isBatchMode && _document == null)
                {
                    WinFormsMessageBox.Show("沒有開啟的地圖", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 彈出檔案/資料夾選擇對話框
                string outputPath = ShowOutputPathDialog(isBatchMode);
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    // 使用者取消選擇
                    return;
                }

                // 計算實際縮放比例（相對於原始尺寸）
                // 縮放比例 = 最大可輸出比例 × 使用者選擇的相對比例
                int actualScalePercent = relativeScalePercent;
                if (!isBatchMode && _document != null)
                {
                    int maxScale = MapExporter.GetMaxScaleWithinLimit(_document);
                    actualScalePercent = maxScale * relativeScalePercent / 100;
                }

                var options = new MapExporter.ExportOptions
                {
                    ShowLayer1 = cbLayer1.Checked == true,
                    ShowLayer2 = cbLayer2.Checked == true,
                    ShowLayer4 = cbLayer4.Checked == true,
                    ShowLayer8 = cbLayer8.Checked == true,
                    Layer8FramePreference = 2, // 第 3 帧 (0-based)
                    ScalePercent = actualScalePercent,
                    RelativeScalePercent = relativeScalePercent // 批次輸出時使用
                };

                var exporter = new MapExporter();

                if (isBatchMode)
                {
                    // 批次輸出
                    await DoBatchExport(exporter, outputPath, options);
                }
                else
                {
                    // 解析自訂標記
                    if (!string.IsNullOrWhiteSpace(txtCustomMarkers.Text))
                    {
                        options.CustomMarkers = MapExporter.CustomMarker.Parse(txtCustomMarkers.Text);
                        options.MarkerSizeLevel = (int)numMarkerSize.Value;
                        _logger.Info($"[MapExportDialog] Parsed {options.CustomMarkers.Count} custom markers, size level: {options.MarkerSizeLevel}");
                    }

                    await DoSingleExportAsync(exporter, outputPath, options);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[MapExportDialog] Export failed");
                WinFormsMessageBox.Show($"輸出失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DoSingleExportAsync(MapExporter exporter, string outputPath, MapExporter.ExportOptions options)
        {
            btnExport.Enabled = false;
            btnCancel.Enabled = false;
            lblProgress.Text = LocalizationManager.L("Dialog_MapExport_Exporting");
            lblProgress.Visible = true;
            progressBar.Visible = true;
            progressBar.Indeterminate = true;

            try
            {
                _logger.Info($"[MapExportDialog] Exporting single map: {_document.MapId} to {outputPath}");

                // 在背景執行渲染
                var (success, errorMessage) = await Task.Run(() =>
                {
                    try
                    {
                        using var bitmap = exporter.ExportMap(_document, options);
                        if (bitmap != null)
                        {
                            exporter.SaveToPng(bitmap, outputPath);
                            return (true, (string)null);
                        }
                        return (false, "無法渲染地圖");
                    }
                    catch (Exception ex)
                    {
                        return (false, ex.Message);
                    }
                });

                if (success)
                {
                    _logger.Info($"[MapExportDialog] Export completed: {outputPath}");
                    WinFormsMessageBox.Show(LocalizationManager.L("Dialog_MapExport_Success"), Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Result = DialogResult.Ok;
                    Close();
                }
                else
                {
                    WinFormsMessageBox.Show($"輸出失敗: {errorMessage}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                btnExport.Enabled = true;
                btnCancel.Enabled = true;
                lblProgress.Visible = false;
                progressBar.Visible = false;
                progressBar.Indeterminate = false;
            }
        }

        private async Task DoBatchExport(MapExporter exporter, string outputFolder, MapExporter.ExportOptions options)
        {
            btnExport.Enabled = false;
            progressBar.Visible = true;
            lblProgress.Visible = true;
            progressBar.Value = 0;

            _cts = new CancellationTokenSource();

            try
            {
                _logger.Info($"[MapExportDialog] Starting batch export to {outputFolder}");

                var progress = new Progress<(int current, int total, string mapId)>(p =>
                {
                    Application.Instance.Invoke(() =>
                    {
                        progressBar.MaxValue = p.total;
                        progressBar.Value = p.current;
                        lblProgress.Text = $"{LocalizationManager.L("Dialog_MapExport_Exporting")} {p.mapId} ({p.current}/{p.total})";
                    });
                });

                await exporter.BatchExportAsync(outputFolder, options, progress, _cts.Token);

                _logger.Info($"[MapExportDialog] Batch export completed");
                WinFormsMessageBox.Show(LocalizationManager.L("Dialog_MapExport_Success"), Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Result = DialogResult.Ok;
                Close();
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"[MapExportDialog] Batch export cancelled");
                WinFormsMessageBox.Show("輸出已取消", Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                btnExport.Enabled = true;
                progressBar.Visible = false;
                lblProgress.Visible = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            // 如果正在批次輸出，取消操作
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                return;
            }

            Result = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// 顯示對話框 (WinForms 相容)
        /// </summary>
        public DialogResult ShowDialog(Control parent)
        {
            ShowModal(parent);
            return Result;
        }
    }
}
