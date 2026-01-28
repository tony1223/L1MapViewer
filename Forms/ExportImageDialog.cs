using System;
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.Localization;
using NLog;

namespace L1MapViewer.Forms
{
    /// <summary>
    /// 匯出地圖圖片對話框
    /// </summary>
    public class ExportImageDialog : Dialog<DialogResult>
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 選擇的縮放比例 (0.1 = 10%, 0.25 = 25%, 0.5 = 50%, 1.0 = 100%)
        /// </summary>
        public float Scale { get; private set; } = 0.25f;

        /// <summary>
        /// 選擇的圖片格式 ("png" 或 "bmp")
        /// </summary>
        public string ImageFormat { get; private set; } = "png";

        private DropDown cmbScale;
        private DropDown cmbFormat;
        private Label lblEstimatedSize;
        private Label lblMemoryWarning;

        private int _mapWidth;
        private int _mapHeight;

        /// <summary>
        /// 建立匯出圖片對話框
        /// </summary>
        /// <param name="mapWidth">地圖寬度（像素）</param>
        /// <param name="mapHeight">地圖高度（像素）</param>
        public ExportImageDialog(int mapWidth, int mapHeight)
        {
            _mapWidth = mapWidth;
            _mapHeight = mapHeight;

            Title = LocalizationManager.L("ExportImage_Title");
            Resizable = false;

            // 縮放比例
            cmbScale = new DropDown { Width = 150 };
            cmbScale.Items.Add(new ListItem { Text = "10%", Key = "0.1" });
            cmbScale.Items.Add(new ListItem { Text = "25%", Key = "0.25" });
            cmbScale.Items.Add(new ListItem { Text = "50%", Key = "0.5" });
            cmbScale.Items.Add(new ListItem { Text = "100%", Key = "1.0" });
            cmbScale.SelectedIndex = 1; // 預設 25%
            cmbScale.SelectedIndexChanged += (s, e) => UpdateEstimatedSize();

            // 圖片格式
            cmbFormat = new DropDown { Width = 150 };
            cmbFormat.Items.Add(new ListItem { Text = "PNG", Key = "png" });
            cmbFormat.Items.Add(new ListItem { Text = "BMP", Key = "bmp" });
            cmbFormat.SelectedIndex = 0; // 預設 PNG

            // 預估大小
            lblEstimatedSize = new Label { Text = "" };

            // 預估記憶體
            lblMemoryWarning = new Label { Text = "" };

            // 按鈕
            var btnExport = new Button { Text = LocalizationManager.L("ExportImage_Export"), Width = 80 };
            btnExport.Click += (s, e) =>
            {
                CollectValues();
                Result = DialogResult.Ok;
                Close();
            };

            var btnCancel = new Button { Text = LocalizationManager.L("Button_Cancel"), Width = 80 };
            btnCancel.Click += (s, e) =>
            {
                Result = DialogResult.Cancel;
                Close();
            };

            // 使用 TableLayout 建立固定佈局
            var mainTable = new TableLayout
            {
                Padding = new Padding(15),
                Spacing = new Size(10, 10),
                Rows =
                {
                    new TableRow(
                        new TableCell(new Label { Text = LocalizationManager.L("ExportImage_Scale"), VerticalAlignment = VerticalAlignment.Center }),
                        new TableCell(cmbScale)
                    ),
                    new TableRow(
                        new TableCell(new Label { Text = LocalizationManager.L("ExportImage_Format"), VerticalAlignment = VerticalAlignment.Center }),
                        new TableCell(cmbFormat)
                    ),
                    new TableRow(
                        new TableCell(new Label { Text = LocalizationManager.L("ExportImage_EstimatedSize"), VerticalAlignment = VerticalAlignment.Center }),
                        new TableCell(lblEstimatedSize)
                    ),
                    new TableRow(
                        new TableCell(),
                        new TableCell(lblMemoryWarning)
                    ),
                    new TableRow { ScaleHeight = true }, // 彈性空間
                    new TableRow(
                        new TableCell(),
                        new TableCell(new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 10,
                            Items = { btnExport, btnCancel }
                        })
                    )
                }
            };

            Content = mainTable;

            DefaultButton = btnExport;
            AbortButton = btnCancel;

            // 設定視窗大小
            ClientSize = new Size(320, 220);

            UpdateEstimatedSize();

            _logger.Debug($"[ExportImageDialog] Created: mapSize={_mapWidth}x{_mapHeight}");
        }

        private void UpdateEstimatedSize()
        {
            float scale = GetSelectedScale();
            int width = (int)(_mapWidth * scale);
            int height = (int)(_mapHeight * scale);
            lblEstimatedSize.Text = LocalizationManager.L("ExportImage_Pixels", width, height);

            // 使用分塊渲染後，記憶體使用量固定為約 50-100 MB
            // - 單一區塊 (2048×2048): 8 MB
            // - Tile 資料與排序: ~10 MB
            // - PNG/BMP 編碼緩衝: ~10 MB
            // - 其他開銷: ~20 MB
            // 總計峰值約 50 MB，不隨輸出大小線性增長
            const double fixedMemoryMB = 50.0;

            lblMemoryWarning.Text = LocalizationManager.L("ExportImage_EstimatedMemory", $"~{fixedMemoryMB:F0} MB");
        }

        private float GetSelectedScale()
        {
            if (cmbScale.SelectedValue is ListItem item && float.TryParse(item.Key, out float scale))
            {
                return scale;
            }
            return 0.25f;
        }

        private string GetSelectedFormat()
        {
            if (cmbFormat.SelectedValue is ListItem item)
            {
                return item.Key;
            }
            return "png";
        }

        private void CollectValues()
        {
            Scale = GetSelectedScale();
            ImageFormat = GetSelectedFormat();
            _logger.Debug($"[ExportImageDialog] CollectValues: scale={Scale}, format={ImageFormat}");
        }

        /// <summary>
        /// 顯示對話框
        /// </summary>
        public DialogResult ShowDialog(Control parent)
        {
            ShowModal(parent);
            return Result;
        }
    }
}
