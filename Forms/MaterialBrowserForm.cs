using System;
using System.Collections.Generic;
// using System.Drawing; // Replaced with Eto.Drawing
using System.IO;
using System.Linq;
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.CLI;
using L1MapViewer.Compatibility;
using L1MapViewer.Helper;
using L1MapViewer.Localization;
using L1MapViewer.Models;

namespace L1MapViewer.Forms
{
    /// <summary>
    /// 素材庫瀏覽器
    /// </summary>
    public class MaterialBrowserForm : WinFormsDialog
    {
        /// <summary>
        /// 選取的素材
        /// </summary>
        public Fs3pData SelectedMaterial { get; private set; }

        /// <summary>
        /// 選取的素材檔案路徑
        /// </summary>
        public string SelectedFilePath { get; private set; }

        private MaterialLibrary _library;
        private ListView lvMaterials;
        private PictureBox pbPreview;
        private Label lblInfo;
        private TextBox txtSearch;
        private Button btnSearch;
        private Button btnOpen;
        private Button btnDelete;
        private Button btnSetPath;
        private Button btnOK;
        private Button btnCancel;
        private ImageList imageList;
        private Label lblPath;

        // Context menu items for localization
        private ToolStripMenuItem menuUse;
        private ToolStripMenuItem menuDelete;
        private ToolStripMenuItem menuOpenFolder;
        private ToolStripMenuItem menuRefresh;

        public MaterialBrowserForm()
        {
            _library = new MaterialLibrary();
            InitializeComponents();
            LoadMaterials();
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

        private void InitializeComponents()
        {
            Text = "素材庫瀏覽器";
            Size = new Size(800, 600);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(600, 400);

            // 搜尋區
            var searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                Padding = new Padding(5)
            };
            Controls.Add(searchPanel);

            txtSearch = new TextBox
            {
                Location = new Point(5, 5),
                Size = new Size(200, 23)
            };
            txtSearch.KeyDown += (s, e) => { if (e.GetKeyCode() == Keys.Enter) SearchMaterials(); };
            searchPanel.Controls.Add(txtSearch);

            btnSearch = new Button
            {
                Text = "搜尋",
                Location = new Point(210, 4),
                Size = new Size(60, 25)
            };
            btnSearch.Click += (s, e) => SearchMaterials();
            searchPanel.Controls.Add(btnSearch);

            lblPath = new Label
            {
                Location = new Point(290, 8),
                Size = new Size(400, 20),
                Text = $"素材庫: {_library.LibraryPath}"
            };
            searchPanel.Controls.Add(lblPath);

            btnSetPath = new Button
            {
                Text = "變更路徑",
                Location = new Point(700, 4),
                Size = new Size(75, 25),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnSetPath.Click += BtnSetPath_Click;
            searchPanel.Controls.Add(btnSetPath);

            // 主要內容區
            var contentPanel = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 500
            };
            Controls.Add(contentPanel);

            // 左側：素材列表
            lvMaterials = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.LargeIcon,
                MultiSelect = false
            };
            lvMaterials.SelectedIndexChanged += LvMaterials_SelectedIndexChanged;
            lvMaterials.DoubleClick += LvMaterials_DoubleClick;

            // 右鍵選單
            var contextMenu = new ContextMenuStrip();
            menuUse = new ToolStripMenuItem("使用此素材", null, (s, e) => BtnOK_Click(s, e));
            menuDelete = new ToolStripMenuItem("刪除", null, (s, e) => BtnDelete_Click(s, e));
            menuOpenFolder = new ToolStripMenuItem("開啟所在資料夾", null, (s, e) => OpenContainingFolder());
            menuRefresh = new ToolStripMenuItem("重新整理", null, (s, e) => { _library.ClearCache(); LoadMaterials(); });
            contextMenu.Items.Add(menuUse);
            contextMenu.Items.Add(new Eto.Forms.SeparatorMenuItem());
            contextMenu.Items.Add(menuDelete);
            contextMenu.Items.Add(menuOpenFolder);
            contextMenu.Items.Add(new Eto.Forms.SeparatorMenuItem());
            contextMenu.Items.Add(menuRefresh);
            contextMenu.Opening += (s, e) =>
            {
                bool hasSelection = lvMaterials.SelectedItems.Count > 0;
                menuUse.Enabled = hasSelection;
                menuDelete.Enabled = hasSelection;
                menuOpenFolder.Enabled = hasSelection;
            };
            lvMaterials.ContextMenuStrip = contextMenu;

            contentPanel.Panel1 = lvMaterials;

            imageList = new ImageList
            {
                ImageSize = new Size(64, 64),
                ColorDepth = ColorDepth.Depth32Bit
            };
            lvMaterials.LargeImageList = imageList;

            // 右側：預覽和資訊
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };
            contentPanel.Panel2 = rightPanel;

            pbPreview = new PictureBox
            {
                Location = new Point(5, 5),
                Size = new Size(200, 200),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            rightPanel.Controls.Add(pbPreview);

            lblInfo = new Label
            {
                Location = new Point(5, 215),
                Size = new Size(200, 150),
                Text = "選擇素材以查看詳細資訊"
            };
            rightPanel.Controls.Add(lblInfo);

            // 按鈕區
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 45,
                Padding = new Padding(5)
            };
            Controls.Add(buttonPanel);

            btnOpen = new Button
            {
                Text = "開啟檔案...",
                Location = new Point(5, 8),
                Size = new Size(90, 28)
            };
            btnOpen.Click += BtnOpen_Click;
            buttonPanel.Controls.Add(btnOpen);

            btnDelete = new Button
            {
                Text = "刪除",
                Location = new Point(100, 8),
                Size = new Size(70, 28),
                Enabled = false
            };
            btnDelete.Click += BtnDelete_Click;
            buttonPanel.Controls.Add(btnDelete);

            btnOK = new Button
            {
                Text = "使用",
                Location = new Point(600, 8),
                Size = new Size(80, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Enabled = false,
                DialogResult = DialogResult.Ok
            };
            btnOK.Click += BtnOK_Click;
            buttonPanel.Controls.Add(btnOK);

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(690, 8),
                Size = new Size(80, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel
            };
            buttonPanel.Controls.Add(btnCancel);

            AcceptButton = btnOK;
            CancelButton = btnCancel;

            // 調整順序：搜尋在最上，按鈕在最下
            searchPanel.BringToFront();
            contentPanel.BringToFront();
        }

        private void LoadMaterials()
        {
            lvMaterials.Items.Clear();
            imageList.Images.Clear();

            var materials = _library.GetAllMaterials();
            int imageIndex = 0;

            foreach (var info in materials)
            {
                var item = new ListViewItem(info.Name ?? "未命名")
                {
                    Tag = info.FilePath
                };

                // 添加縮圖
                if (info.ThumbnailPng != null && info.ThumbnailPng.Length > 0)
                {
                    try
                    {
                        using (var ms = new MemoryStream(info.ThumbnailPng))
                        {
                            var thumb = new Bitmap(ms);
                            imageList.Images.Add(thumb);
                            item.ImageIndex = imageIndex++;
                        }
                    }
                    catch
                    {
                        item.ImageIndex = -1;
                    }
                }
                else
                {
                    item.ImageIndex = -1;
                }

                lvMaterials.Items.Add(item);
            }

            lblPath.Text = $"素材庫: {_library.LibraryPath} ({materials.Count} 個素材)";
        }

        private void SearchMaterials()
        {
            string keyword = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                LoadMaterials();
                return;
            }

            lvMaterials.Items.Clear();
            imageList.Images.Clear();

            var results = _library.Search(keyword);
            int imageIndex = 0;

            foreach (var info in results)
            {
                var item = new ListViewItem(info.Name ?? "未命名")
                {
                    Tag = info.FilePath
                };

                if (info.ThumbnailPng != null && info.ThumbnailPng.Length > 0)
                {
                    try
                    {
                        using (var ms = new MemoryStream(info.ThumbnailPng))
                        {
                            var thumb = new Bitmap(ms);
                            imageList.Images.Add(thumb);
                            item.ImageIndex = imageIndex++;
                        }
                    }
                    catch
                    {
                        item.ImageIndex = -1;
                    }
                }
                else
                {
                    item.ImageIndex = -1;
                }

                lvMaterials.Items.Add(item);
            }
        }

        private void LvMaterials_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvMaterials.SelectedItems.Count == 0)
            {
                pbPreview.Image = null;
                lblInfo.Text = "選擇素材以查看詳細資訊";
                btnOK.Enabled = false;
                btnDelete.Enabled = false;
                return;
            }

            var item = lvMaterials.SelectedItems[0];
            string filePath = item.Tag as string;
            SelectedFilePath = filePath;

            try
            {
                var info = Fs3pParser.GetInfo(filePath);
                if (info != null)
                {
                    // 顯示縮圖
                    if (info.ThumbnailPng != null && info.ThumbnailPng.Length > 0)
                    {
                        using (var ms = new MemoryStream(info.ThumbnailPng))
                        {
                            pbPreview.Image?.Dispose();
                            pbPreview.Image = new Bitmap(ms);
                        }
                    }
                    else
                    {
                        pbPreview.Image = null;
                    }

                    // 顯示資訊
                    lblInfo.Text = $"名稱: {info.Name}\n" +
                                   $"範圍: {info.Width} x {info.Height}\n" +
                                   $"圖層: {GetLayerDescription(info.LayerFlags)}\n" +
                                   $"檔案大小: {info.FileSize / 1024.0:F1} KB\n" +
                                   $"檔案: {Path.GetFileName(filePath)}";

                    btnOK.Enabled = true;
                    btnDelete.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                lblInfo.Text = $"無法讀取素材:\n{ex.Message}";
                btnOK.Enabled = false;
                btnDelete.Enabled = true; // 可以刪除損壞的檔案
            }
        }

        private string GetLayerDescription(ushort flags)
        {
            var layers = new List<string>();
            if ((flags & 0x01) != 0) layers.Add("L1");
            if ((flags & 0x02) != 0) layers.Add("L2");
            if ((flags & 0x04) != 0) layers.Add("L3");
            if ((flags & 0x08) != 0) layers.Add("L4");
            return layers.Count > 0 ? string.Join(", ", layers) : "無";
        }

        private void LvMaterials_DoubleClick(object sender, EventArgs e)
        {
            if (lvMaterials.SelectedItems.Count > 0)
            {
                BtnOK_Click(sender, e);
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedFilePath))
            {
                WinFormsMessageBox.Show("請選擇素材", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
                return;
            }

            try
            {
                SelectedMaterial = _library.LoadMaterial(SelectedFilePath);
                if (SelectedMaterial == null)
                {
                    WinFormsMessageBox.Show("無法載入素材", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.None;
                    return;
                }
            }
            catch (Exception ex)
            {
                WinFormsMessageBox.Show($"載入素材失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
            }
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "素材檔案|*.fs32p|所有檔案|*.*";
                ofd.Title = "開啟素材檔案";

                if (ofd.ShowDialog(this) == DialogResult.Ok)
                {
                    try
                    {
                        SelectedFilePath = ofd.FileName;
                        SelectedMaterial = Fs3pParser.ParseFile(ofd.FileName);

                        if (SelectedMaterial != null)
                        {
                            DialogResult = DialogResult.Ok;
                            Close();
                        }
                        else
                        {
                            WinFormsMessageBox.Show("無法解析素材檔案", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        WinFormsMessageBox.Show($"開啟檔案失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedFilePath))
                return;

            var result = WinFormsMessageBox.Show(
                $"確定要刪除素材 \"{lvMaterials.SelectedItems[0].Text}\"？",
                "確認刪除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                if (_library.DeleteMaterial(SelectedFilePath))
                {
                    LoadMaterials();
                }
                else
                {
                    WinFormsMessageBox.Show("刪除失敗", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnSetPath_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "選擇素材庫資料夾";
                fbd.SelectedPath = _library.LibraryPath;

                if (fbd.ShowDialog(this) == DialogResult.Ok)
                {
                    _library.LibraryPath = fbd.SelectedPath;
                    _library.ClearCache();
                    LoadMaterials();
                }
            }
        }

        private void OpenContainingFolder()
        {
            if (string.IsNullOrEmpty(SelectedFilePath) || !File.Exists(SelectedFilePath))
                return;

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{SelectedFilePath}\"");
            }
            catch
            {
                // 忽略錯誤
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pbPreview.Image?.Dispose();
                imageList?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void UpdateLocalization()
        {
            // Form title
            Text = LocalizationManager.L("Form_MaterialBrowser_Title");

            // Buttons
            btnSearch.Text = LocalizationManager.L("Button_Search");
            btnSetPath.Text = LocalizationManager.L("Material_ChangePath");
            btnOpen.Text = LocalizationManager.L("Material_OpenFile");
            btnDelete.Text = LocalizationManager.L("Button_Delete");
            btnOK.Text = LocalizationManager.L("Material_Use");
            btnCancel.Text = LocalizationManager.L("Button_Cancel");

            // Context menu
            menuUse.Text = LocalizationManager.L("Material_UseThis");
            menuDelete.Text = LocalizationManager.L("Button_Delete");
            menuOpenFolder.Text = LocalizationManager.L("Material_OpenFolder");
            menuRefresh.Text = LocalizationManager.L("Button_Refresh");

            // Update lblPath with current library info
            var materials = _library.GetAllMaterials();
            lblPath.Text = $"{LocalizationManager.L("Material_Library")}: {_library.LibraryPath} ({materials.Count} {LocalizationManager.L("Material_Items")})";
        }
    }
}
