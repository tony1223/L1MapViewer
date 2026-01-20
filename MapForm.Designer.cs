using System.ComponentModel;
// using System.Drawing; // Replaced with Eto.Drawing
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.Controls;
using L1MapViewer.Localization;
using L1MapViewer.Other;
using static L1MapViewer.Compatibility.KeysCompat;

namespace L1FlyMapViewer
{
    partial class MapForm
    {
        private IContainer components;
        private MenuStrip menuStrip1;

        // 頂級選單
        private ToolStripMenuItem menuFile;
        private ToolStripMenuItem menuEdit;
        private ToolStripMenuItem menuView;
        private ToolStripMenuItem menuTools;
        private ToolStripMenuItem menuHelp;

        // 檔案選單項目
        private ToolStripMenuItem openToolStripMenuItem;
        private ToolStripMenuItem importMaterialToolStripMenuItem;
        private ToolStripMenuItem importFs32ToNewMapToolStripMenuItem;
        private ToolStripMenuItem menuSaveS32;
        private ToolStripMenuItem menuExportFs32;
        private ToolStripMenuItem exportToolStripMenuItem;
        private ToolStripMenuItem exportL1JToolStripMenuItem;
        private ToolStripMenuItem exportDIRToolStripMenuItem;
        private ToolStripMenuItem exportAllL1JToolStripMenuItem;
        private ToolStripMenuItem exportAllDIRToolStripMenuItem;
        private ToolStripMenuItem menuExit;

        // 編輯選單項目
        private ToolStripMenuItem menuUndo;
        private ToolStripMenuItem menuRedo;
        private ToolStripMenuItem menuCopy;
        private ToolStripMenuItem menuPaste;
        private ToolStripMenuItem menuDelete;
        private ToolStripMenuItem batchDeleteTileToolStripMenuItem;
        private ToolStripMenuItem menuBatchReplaceTile;

        // 檢視選單項目
        private ToolStripMenuItem menuReloadMap;
        private ToolStripMenuItem menuLayers;
        private ToolStripMenuItem menuLayerL1;
        private ToolStripMenuItem menuLayerL2;
        private ToolStripMenuItem menuLayerL4;
        private ToolStripMenuItem menuLayerL5;
        private ToolStripMenuItem menuLayerL8;
        private ToolStripMenuItem menuLayerPassable;
        private ToolStripMenuItem menuLayerSafe;
        private ToolStripMenuItem menuLayerCombat;
        private ToolStripMenuItem menuLayerGrid;
        private ToolStripMenuItem menuLayerS32Bound;
        private ToolStripMenuItem menuZoom;
        private ToolStripMenuItem menuZoomIn;
        private ToolStripMenuItem menuZoomOut;
        private ToolStripMenuItem menuZoom100;

        // 工具選單項目
        private ToolStripMenuItem menuPassableEdit;
        private ToolStripMenuItem menuRegionEdit;
        private ToolStripMenuItem menuLayer5Edit;
        private ToolStripMenuItem menuValidateMap;
        private ToolStripMenuItem menuCleanupTiles;

        // 說明選單項目
        private ToolStripMenuItem discordToolStripMenuItem;
        private ToolStripMenuItem languageToolStripMenuItem;
        private ToolStripMenuItem langZhTWToolStripMenuItem;
        private ToolStripMenuItem langJaJPToolStripMenuItem;
        private ToolStripMenuItem langEnUSToolStripMenuItem;
        private ToolStripMenuItem menuAbout;
        private StatusStrip statusStrip1;
        public ToolStripStatusLabel toolStripStatusLabel1;
        public ToolStripProgressBar toolStripProgressBar1;
        public ToolStripStatusLabel toolStripStatusLabel2;
        public ToolStripStatusLabel toolStripStatusLabel3;
        private ToolStripStatusLabel toolStripJumpLabel;
        private ToolStripTextBox toolStripJumpTextBox;
        private ToolStripButton toolStripJumpButton;
        private ToolStripButton toolStripCopyMoveCmd;
        private ToolStripButton toolStripShowAllL8;

        // 左側面板
        private Panel leftPanel;
        public ComboBox comboBox1;  // 保留給介面相容性，但隱藏
        private PictureBox miniMapPictureBox;

        // 左下角 TabControl 內容（TabControl 本身在 MapForm.cs 中以 Eto 原生方式建立）
        private TextBox txtMapSearch;
        private ListBox lstMaps;

        // S32 檔案清單 Tab 內容
        private Label lblS32Files;
        private Button btnS32SelectAll;
        private Button btnS32SelectNone;
        private CheckedListBox lstS32Files;

        // 右側面板（Tile 清單）
        private Panel rightPanel;
        private Label lblTileList;
        private TextBox txtTileSearch;
        private L1MapViewer.Controls.IconTextListControl lvTiles;
        private Label lblMaterials;
        private L1MapViewer.Controls.IconTextListControl lvMaterials;
        private Button btnMoreMaterials;
        private Label lblGroupThumbnails;
        private TextBox txtGroupSearch;
        private ComboBox cmbGroupMode;
        private L1MapViewer.Controls.IconTextListControl lvGroupThumbnails;

        // 工具列面板（右側功能區）
        private Panel toolbarContainer;
        private Panel toolbarPanel;
        private Panel toolbarPanel2;
        private Button btnToolCopy;
        private Button btnToolPaste;
        private Button btnToolDelete;
        private Button btnToolUndo;
        private Button btnToolRedo;
        private Button btnToolSave;
        private Button btnToolCellInfo;
        private Button btnToolReplaceTile;
        private Button btnToolAddS32;
        private Button btnToolClearLayer7;
        private Button btnToolClearCell;
        private Button btnMapValidate;
        private Button btnToolCheckL1;
        private Button btnToolCheckL2;
        private Button btnToolCheckL3;
        private Button btnToolCheckL4;
        private Button btnToolCheckL5;
        private Button btnToolCheckL6;
        private Button btnToolCheckL7;
        private Button btnToolCheckL8;
        private Button btnEnableVisibleL8;
        private Button btnViewClipboard;
        private Button btnToolTestTil;
        private Button btnToolClearTestTil;
        private ToolTip toolTip1;

        // 中間 TabControl
        private TabControl tabControl1;
        private TabPage tabMapPreview;
        private TabPage tabS32Editor;

        // 地圖預覽頁籤控制項
        public ZoomablePanel panel1;
        public PictureBox pictureBox4;
        public PictureBox pictureBox3;
        public PictureBox pictureBox2;
        public PictureBox pictureBox1;
        public VScrollBar vScrollBar1;
        public HScrollBar hScrollBar1;

        // S32 編輯器頁籤控制項
        private Panel s32EditorPanel;
        private Panel s32LayerControlPanel;
        private CheckBox chkLayer1;
        private CheckBox chkLayer2;
        private CheckBox chkLayer3;
        private CheckBox chkLayer4;

        // 浮動圖層控制面板（右上角）
        private Panel layerFloatPanel;
        private Label lblLayerIcon;
        private Panel layerPopupPanel;
        private CheckBox chkFloatLayer1;
        private CheckBox chkFloatLayer2;
        private CheckBox chkFloatLayer4;
        private CheckBox chkFloatPassable;
        private CheckBox chkFloatGrid;
        private CheckBox chkFloatS32Boundary;
        private CheckBox chkFloatLayer5;
        private CheckBox chkFloatSafeZones;
        private CheckBox chkFloatCombatZones;
        private CheckBox chkFloatLayer8Spr;
        private CheckBox chkFloatLayer8Marker;
        private CheckBox chkShowPassable;
        private CheckBox chkShowLayer5;
        private CheckBox chkShowGrid;
        private CheckBox chkShowS32Boundary;
        private CheckBox chkShowSafeZones;
        private CheckBox chkShowCombatZones;
        private Button btnCopySettings;
        private Button btnRegionEdit;
        private Button btnCopyMapCoords;
        private Button btnImportFs32;
        private Button btnEditPassable;
        private Button btnEditLayer5;
        private Button btnMergeL2ToL1;
        private Button btnSaveS32;
        private Button btnReloadMap;
        private Button btnAnalyzeAttr;
        private Panel s32MapPanel;
        private MapViewerControl _mapViewerControl;
        private Label lblS32Info;

        protected override void Dispose(bool disposing)
        {
            if (disposing && this.components != null)
                this.components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            // MenuStrip
            this.menuStrip1 = new MenuStrip();

            // 頂級選單
            this.menuFile = new ToolStripMenuItem();
            this.menuEdit = new ToolStripMenuItem();
            this.menuView = new ToolStripMenuItem();
            this.menuTools = new ToolStripMenuItem();
            this.menuHelp = new ToolStripMenuItem();

            // 檔案選單項目
            this.openToolStripMenuItem = new ToolStripMenuItem();
            this.importMaterialToolStripMenuItem = new ToolStripMenuItem();
            this.importFs32ToNewMapToolStripMenuItem = new ToolStripMenuItem();
            this.menuSaveS32 = new ToolStripMenuItem();
            this.menuExportFs32 = new ToolStripMenuItem();
            this.exportToolStripMenuItem = new ToolStripMenuItem();
            this.exportL1JToolStripMenuItem = new ToolStripMenuItem();
            this.exportDIRToolStripMenuItem = new ToolStripMenuItem();
            this.exportAllL1JToolStripMenuItem = new ToolStripMenuItem();
            this.exportAllDIRToolStripMenuItem = new ToolStripMenuItem();
            this.menuExit = new ToolStripMenuItem();

            // 編輯選單項目
            this.menuUndo = new ToolStripMenuItem();
            this.menuRedo = new ToolStripMenuItem();
            this.menuCopy = new ToolStripMenuItem();
            this.menuPaste = new ToolStripMenuItem();
            this.menuDelete = new ToolStripMenuItem();
            this.batchDeleteTileToolStripMenuItem = new ToolStripMenuItem();
            this.menuBatchReplaceTile = new ToolStripMenuItem();

            // 檢視選單項目
            this.menuReloadMap = new ToolStripMenuItem();
            this.menuLayers = new ToolStripMenuItem();
            this.menuLayerL1 = new ToolStripMenuItem();
            this.menuLayerL2 = new ToolStripMenuItem();
            this.menuLayerL4 = new ToolStripMenuItem();
            this.menuLayerL5 = new ToolStripMenuItem();
            this.menuLayerL8 = new ToolStripMenuItem();
            this.menuLayerPassable = new ToolStripMenuItem();
            this.menuLayerSafe = new ToolStripMenuItem();
            this.menuLayerCombat = new ToolStripMenuItem();
            this.menuLayerGrid = new ToolStripMenuItem();
            this.menuLayerS32Bound = new ToolStripMenuItem();
            this.menuZoom = new ToolStripMenuItem();
            this.menuZoomIn = new ToolStripMenuItem();
            this.menuZoomOut = new ToolStripMenuItem();
            this.menuZoom100 = new ToolStripMenuItem();

            // 工具選單項目
            this.menuPassableEdit = new ToolStripMenuItem();
            this.menuRegionEdit = new ToolStripMenuItem();
            this.menuLayer5Edit = new ToolStripMenuItem();
            this.menuValidateMap = new ToolStripMenuItem();
            this.menuCleanupTiles = new ToolStripMenuItem();

            // 說明選單項目
            this.discordToolStripMenuItem = new ToolStripMenuItem();
            this.languageToolStripMenuItem = new ToolStripMenuItem();
            this.langZhTWToolStripMenuItem = new ToolStripMenuItem();
            this.langJaJPToolStripMenuItem = new ToolStripMenuItem();
            this.langEnUSToolStripMenuItem = new ToolStripMenuItem();
            this.menuAbout = new ToolStripMenuItem();

            // StatusStrip
            this.statusStrip1 = new StatusStrip();
            this.toolStripStatusLabel1 = new ToolStripStatusLabel();
            this.toolStripStatusLabel2 = new ToolStripStatusLabel();
            this.toolStripStatusLabel3 = new ToolStripStatusLabel();
            this.toolStripProgressBar1 = new ToolStripProgressBar();
            this.toolStripJumpLabel = new ToolStripStatusLabel();
            this.toolStripJumpTextBox = new ToolStripTextBox();
            this.toolStripJumpButton = new ToolStripButton();
            this.toolStripShowAllL8 = new ToolStripButton();
            this.toolStripCopyMoveCmd = new ToolStripButton();

            // 左側面板
            this.leftPanel = new Panel();
            this.comboBox1 = new ComboBox();
            this.miniMapPictureBox = new PictureBox();

            // 左下角 TabControl 內容（TabControl 本身在 MapForm.cs 中建立）
            this.txtMapSearch = new TextBox();
            this.lstMaps = new ListBox();

            // S32 檔案清單
            this.lblS32Files = new Label();
            this.btnS32SelectAll = new Button();
            this.btnS32SelectNone = new Button();
            this.lstS32Files = new CheckedListBox();

            // 右側面板
            this.rightPanel = new Panel();
            this.lblTileList = new Label();
            this.txtTileSearch = new TextBox();
            this.lvTiles = new L1MapViewer.Controls.IconTextListControl();
            this.lblMaterials = new Label();
            this.lvMaterials = new L1MapViewer.Controls.IconTextListControl();
            this.btnMoreMaterials = new Button();
            this.lblGroupThumbnails = new Label();
            this.txtGroupSearch = new TextBox();
            this.cmbGroupMode = new ComboBox();
            this.lvGroupThumbnails = new L1MapViewer.Controls.IconTextListControl();

            // 工具列面板
            this.toolbarContainer = new Panel();
            this.toolbarPanel = new Panel();
            this.toolbarPanel2 = new Panel();
            this.btnToolCopy = new Button();
            this.btnToolPaste = new Button();
            this.btnToolDelete = new Button();
            this.btnToolUndo = new Button();
            this.btnToolRedo = new Button();
            this.btnToolSave = new Button();
            this.btnToolCellInfo = new Button();
            this.btnToolReplaceTile = new Button();
            this.btnToolAddS32 = new Button();
            this.btnToolClearLayer7 = new Button();
            this.btnToolClearCell = new Button();
            this.btnMapValidate = new Button();
            this.btnToolCheckL1 = new Button();
            this.btnToolCheckL2 = new Button();
            this.btnToolCheckL3 = new Button();
            this.btnToolCheckL4 = new Button();
            this.btnToolCheckL5 = new Button();
            this.btnToolCheckL6 = new Button();
            this.btnToolCheckL7 = new Button();
            this.btnToolCheckL8 = new Button();
            this.btnEnableVisibleL8 = new Button();
            this.btnViewClipboard = new Button();
            this.btnToolTestTil = new Button();
            this.btnToolClearTestTil = new Button();
            this.toolTip1 = new ToolTip();

            // 中間 TabControl
            this.tabControl1 = new TabControl();
            this.tabMapPreview = new TabPage();
            this.tabS32Editor = new TabPage();

            // 地圖預覽頁籤
            this.panel1 = new ZoomablePanel();
            this.pictureBox4 = new PictureBox();
            this.pictureBox3 = new PictureBox();
            this.pictureBox2 = new PictureBox();
            this.pictureBox1 = new PictureBox();
            this.vScrollBar1 = new VScrollBar();
            this.hScrollBar1 = new HScrollBar();

            // S32 編輯器頁籤
            this.s32EditorPanel = new Panel();
            this.s32LayerControlPanel = new Panel();
            this.chkLayer1 = new CheckBox();
            this.chkLayer2 = new CheckBox();
            this.chkLayer3 = new CheckBox();
            this.chkLayer4 = new CheckBox();
            this.chkShowPassable = new CheckBox();
            this.chkShowGrid = new CheckBox();
            this.chkShowS32Boundary = new CheckBox();
            this.chkShowSafeZones = new CheckBox();
            this.chkShowCombatZones = new CheckBox();
            this.btnCopySettings = new Button();
            this.btnCopyMapCoords = new Button();
            this.btnImportFs32 = new Button();
            this.btnRegionEdit = new Button();
            this.btnEditPassable = new Button();
            this.btnEditLayer5 = new Button();
            this.btnMergeL2ToL1 = new Button();
            this.btnSaveS32 = new Button();
            this.btnReloadMap = new Button();
            this.btnAnalyzeAttr = new Button();
            this.s32MapPanel = new Panel();
            this._mapViewerControl = new MapViewerControl();
            this.lblS32Info = new Label();

            // 浮動圖層控制面板
            this.layerFloatPanel = new Panel();
            this.lblLayerIcon = new Label();
            this.layerPopupPanel = new Panel();
            this.chkFloatLayer1 = new CheckBox();
            this.chkFloatLayer2 = new CheckBox();
            this.chkFloatLayer4 = new CheckBox();
            this.chkFloatPassable = new CheckBox();
            this.chkFloatGrid = new CheckBox();
            this.chkFloatS32Boundary = new CheckBox();
            this.chkFloatLayer5 = new CheckBox();
            this.chkFloatSafeZones = new CheckBox();
            this.chkFloatCombatZones = new CheckBox();
            this.chkFloatLayer8Spr = new CheckBox();
            this.chkFloatLayer8Marker = new CheckBox();
            this.chkShowLayer5 = new CheckBox();

            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.leftPanel.SuspendLayout();
            ((ISupportInitialize)this.miniMapPictureBox).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabS32Editor.SuspendLayout();
            this.s32EditorPanel.SuspendLayout();
            this.s32LayerControlPanel.SuspendLayout();
            this.s32MapPanel.SuspendLayout();
            ((ISupportInitialize)this.pictureBox4).BeginInit();
            ((ISupportInitialize)this.pictureBox3).BeginInit();
            ((ISupportInitialize)this.pictureBox2).BeginInit();
            ((ISupportInitialize)this.pictureBox1).BeginInit();
            this.SuspendLayout();

            //
            // menuStrip1 - 主選單列
            //
            this.menuStrip1.Items.AddRange(new ToolStripItem[] {
                this.menuFile,
                this.menuEdit,
                this.menuView,
                this.menuTools,
                this.menuHelp
            });
            this.menuStrip1.SetLocation(new Point(0, 0));
            this.menuStrip1.SetName("menuStrip1");
            this.menuStrip1.Size = new Size(1200, 24);
            this.menuStrip1.TabIndex = 0;

            // ========== 檔案選單 ==========
            this.menuFile.SetName("menuFile");
            this.menuFile.Text = "檔案(&F)";
            this.menuFile.DropDownItems.AddRange(new ToolStripItem[] {
                this.openToolStripMenuItem,
                new ToolStripSeparator(),
                this.importMaterialToolStripMenuItem,
                this.importFs32ToNewMapToolStripMenuItem,
                new ToolStripSeparator(),
                this.menuSaveS32,
                this.menuExportFs32,
                this.exportToolStripMenuItem,
                new ToolStripSeparator(),
                this.menuExit
            });

            this.openToolStripMenuItem.SetName("openToolStripMenuItem");
            this.openToolStripMenuItem.Text = "開啟天堂客戶端...";
            this.openToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);

            this.importMaterialToolStripMenuItem.SetName("importMaterialToolStripMenuItem");
            this.importMaterialToolStripMenuItem.Text = "匯入素材...";
            this.importMaterialToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.I;
            this.importMaterialToolStripMenuItem.Click += new System.EventHandler(this.importMaterialToolStripMenuItem_Click);

            this.importFs32ToNewMapToolStripMenuItem.SetName("importFs32ToNewMapToolStripMenuItem");
            this.importFs32ToNewMapToolStripMenuItem.Text = "匯入地圖包到新地圖...";
            this.importFs32ToNewMapToolStripMenuItem.Click += new System.EventHandler(this.importFs32ToNewMapToolStripMenuItem_Click);

            this.menuSaveS32.SetName("menuSaveS32");
            this.menuSaveS32.Text = "儲存 S32";
            this.menuSaveS32.ShortcutKeys = Keys.Control | Keys.S;
            this.menuSaveS32.Click += new System.EventHandler(this.btnSaveS32_Click);

            this.menuExportFs32.SetName("menuExportFs32");
            this.menuExportFs32.Text = "匯出 FS32 地圖包...";
            this.menuExportFs32.Click += new System.EventHandler(this.ExportFs32MenuItem_Click);

            this.exportToolStripMenuItem.SetName("exportToolStripMenuItem");
            this.exportToolStripMenuItem.Text = "匯出通行資料";
            this.exportToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
                this.exportL1JToolStripMenuItem,
                this.exportDIRToolStripMenuItem,
                new ToolStripSeparator(),
                this.exportAllL1JToolStripMenuItem,
                this.exportAllDIRToolStripMenuItem
            });

            this.exportL1JToolStripMenuItem.SetName("exportL1JToolStripMenuItem");
            this.exportL1JToolStripMenuItem.Text = "L1J 格式";
            this.exportL1JToolStripMenuItem.Click += new System.EventHandler(this.exportL1JToolStripMenuItem_Click);

            this.exportDIRToolStripMenuItem.SetName("exportDIRToolStripMenuItem");
            this.exportDIRToolStripMenuItem.Text = "DIR 格式";
            this.exportDIRToolStripMenuItem.Click += new System.EventHandler(this.exportDIRToolStripMenuItem_Click);

            this.exportAllL1JToolStripMenuItem.SetName("exportAllL1JToolStripMenuItem");
            this.exportAllL1JToolStripMenuItem.Text = "輸出所有地圖 (L1J)";
            this.exportAllL1JToolStripMenuItem.Click += new System.EventHandler(this.exportAllL1JToolStripMenuItem_Click);

            this.exportAllDIRToolStripMenuItem.SetName("exportAllDIRToolStripMenuItem");
            this.exportAllDIRToolStripMenuItem.Text = "輸出所有地圖 (DIR)";
            this.exportAllDIRToolStripMenuItem.Click += new System.EventHandler(this.exportAllDIRToolStripMenuItem_Click);

            this.menuExit.SetName("menuExit");
            this.menuExit.Text = "結束";
            this.menuExit.Click += (s, e) => this.Close();

            // ========== 編輯選單 ==========
            this.menuEdit.SetName("menuEdit");
            this.menuEdit.Text = "編輯(&E)";
            this.menuEdit.DropDownItems.AddRange(new ToolStripItem[] {
                this.menuUndo,
                this.menuRedo,
                new ToolStripSeparator(),
                this.menuCopy,
                this.menuPaste,
                this.menuDelete,
                new ToolStripSeparator(),
                this.batchDeleteTileToolStripMenuItem,
                this.menuBatchReplaceTile
            });

            this.menuUndo.SetName("menuUndo");
            this.menuUndo.Text = "復原";
            this.menuUndo.ShortcutKeys = Keys.Control | Keys.Z;
            this.menuUndo.Click += new System.EventHandler(this.btnToolUndo_Click);

            this.menuRedo.SetName("menuRedo");
            this.menuRedo.Text = "重做";
            this.menuRedo.ShortcutKeys = Keys.Control | Keys.Y;
            this.menuRedo.Click += new System.EventHandler(this.btnToolRedo_Click);

            this.menuCopy.SetName("menuCopy");
            this.menuCopy.Text = "複製";
            this.menuCopy.ShortcutKeys = Keys.Control | Keys.C;
            this.menuCopy.Click += new System.EventHandler(this.btnToolCopy_Click);

            this.menuPaste.SetName("menuPaste");
            this.menuPaste.Text = "貼上";
            this.menuPaste.ShortcutKeys = Keys.Control | Keys.V;
            this.menuPaste.Click += new System.EventHandler(this.btnToolPaste_Click);

            this.menuDelete.SetName("menuDelete");
            this.menuDelete.Text = "刪除";
            this.menuDelete.ShortcutKeys = Keys.Delete;
            this.menuDelete.Click += new System.EventHandler(this.btnToolDelete_Click);

            this.batchDeleteTileToolStripMenuItem.SetName("batchDeleteTileToolStripMenuItem");
            this.batchDeleteTileToolStripMenuItem.Text = "批次刪除 Tile...";
            this.batchDeleteTileToolStripMenuItem.Click += new System.EventHandler(this.batchDeleteTileToolStripMenuItem_Click);

            this.menuBatchReplaceTile.SetName("menuBatchReplaceTile");
            this.menuBatchReplaceTile.Text = "批次取代 Tile...";
            this.menuBatchReplaceTile.Click += new System.EventHandler(this.btnToolReplaceTile_Click);

            // ========== 檢視選單 ==========
            this.menuView.SetName("menuView");
            this.menuView.Text = "檢視(&V)";
            this.menuView.DropDownItems.AddRange(new ToolStripItem[] {
                this.menuReloadMap,
                new ToolStripSeparator(),
                this.menuLayers,
                this.menuZoom
            });

            this.menuReloadMap.SetName("menuReloadMap");
            this.menuReloadMap.Text = "重新載入地圖";
            this.menuReloadMap.ShortcutKeys = Keys.F5;
            this.menuReloadMap.Click += new System.EventHandler(this.btnReloadMap_Click);

            // 圖層子選單
            this.menuLayers.SetName("menuLayers");
            this.menuLayers.Text = "圖層";
            this.menuLayers.DropDownItems.AddRange(new ToolStripItem[] {
                this.menuLayerL1,
                this.menuLayerL2,
                this.menuLayerL4,
                this.menuLayerL5,
                this.menuLayerL8,
                new ToolStripSeparator(),
                this.menuLayerPassable,
                this.menuLayerSafe,
                this.menuLayerCombat,
                new ToolStripSeparator(),
                this.menuLayerGrid,
                this.menuLayerS32Bound
            });

            this.menuLayerL1.SetName("menuLayerL1");
            this.menuLayerL1.Text = "Layer 1 地板";
            this.menuLayerL1.Checked = true;
            this.menuLayerL1.Click += (s, e) => { menuLayerL1.Checked = !menuLayerL1.Checked; chkLayer1.Checked = menuLayerL1.Checked; };

            this.menuLayerL2.SetName("menuLayerL2");
            this.menuLayerL2.Text = "Layer 2 裝飾";
            this.menuLayerL2.Checked = true;
            this.menuLayerL2.Click += (s, e) => { menuLayerL2.Checked = !menuLayerL2.Checked; chkLayer2.Checked = menuLayerL2.Checked; };

            this.menuLayerL4.SetName("menuLayerL4");
            this.menuLayerL4.Text = "Layer 4 物件";
            this.menuLayerL4.Checked = true;
            this.menuLayerL4.Click += (s, e) => { menuLayerL4.Checked = !menuLayerL4.Checked; chkLayer4.Checked = menuLayerL4.Checked; };

            this.menuLayerL5.SetName("menuLayerL5");
            this.menuLayerL5.Text = "Layer 5 事件";
            this.menuLayerL5.Click += (s, e) => { menuLayerL5.Checked = !menuLayerL5.Checked; chkShowLayer5.Checked = menuLayerL5.Checked; };

            this.menuLayerL8.SetName("menuLayerL8");
            this.menuLayerL8.Text = "Layer 8 SPR";
            this.menuLayerL8.Click += (s, e) => { menuLayerL8.Checked = !menuLayerL8.Checked; chkFloatLayer8Spr.Checked = menuLayerL8.Checked; };

            this.menuLayerPassable.SetName("menuLayerPassable");
            this.menuLayerPassable.Text = "通行性";
            this.menuLayerPassable.Click += (s, e) => { menuLayerPassable.Checked = !menuLayerPassable.Checked; chkShowPassable.Checked = menuLayerPassable.Checked; };

            this.menuLayerSafe.SetName("menuLayerSafe");
            this.menuLayerSafe.Text = "安全區域";
            this.menuLayerSafe.Click += (s, e) => { menuLayerSafe.Checked = !menuLayerSafe.Checked; chkShowSafeZones.Checked = menuLayerSafe.Checked; };

            this.menuLayerCombat.SetName("menuLayerCombat");
            this.menuLayerCombat.Text = "戰鬥區域";
            this.menuLayerCombat.Click += (s, e) => { menuLayerCombat.Checked = !menuLayerCombat.Checked; chkShowCombatZones.Checked = menuLayerCombat.Checked; };

            this.menuLayerGrid.SetName("menuLayerGrid");
            this.menuLayerGrid.Text = "格線";
            this.menuLayerGrid.Click += (s, e) => { menuLayerGrid.Checked = !menuLayerGrid.Checked; chkShowGrid.Checked = menuLayerGrid.Checked; };

            this.menuLayerS32Bound.SetName("menuLayerS32Bound");
            this.menuLayerS32Bound.Text = "S32 邊界";
            this.menuLayerS32Bound.Click += (s, e) => { menuLayerS32Bound.Checked = !menuLayerS32Bound.Checked; chkShowS32Boundary.Checked = menuLayerS32Bound.Checked; };

            // 縮放子選單
            this.menuZoom.SetName("menuZoom");
            this.menuZoom.Text = "縮放";
            this.menuZoom.DropDownItems.AddRange(new ToolStripItem[] {
                this.menuZoomIn,
                this.menuZoomOut,
                this.menuZoom100
            });

            this.menuZoomIn.SetName("menuZoomIn");
            this.menuZoomIn.Text = "放大";
            this.menuZoomIn.ShortcutKeys = Keys.Control | Oemplus;
            this.menuZoomIn.Click += (s, e) => ZoomIn();

            this.menuZoomOut.SetName("menuZoomOut");
            this.menuZoomOut.Text = "縮小";
            this.menuZoomOut.ShortcutKeys = Keys.Control | OemMinus;
            this.menuZoomOut.Click += (s, e) => ZoomOut();

            this.menuZoom100.SetName("menuZoom100");
            this.menuZoom100.Text = "100%";
            this.menuZoom100.ShortcutKeys = Keys.Control | Keys.D0;
            this.menuZoom100.Click += (s, e) => ResetZoom();

            // ========== 工具選單 ==========
            this.menuTools.SetName("menuTools");
            this.menuTools.Text = "工具(&T)";
            this.menuTools.DropDownItems.AddRange(new ToolStripItem[] {
                this.menuPassableEdit,
                this.menuRegionEdit,
                this.menuLayer5Edit,
                new ToolStripSeparator(),
                this.menuValidateMap,
                new ToolStripSeparator(),
                this.menuCleanupTiles
            });

            this.menuPassableEdit.SetName("menuPassableEdit");
            this.menuPassableEdit.Text = "通行編輯模式";
            this.menuPassableEdit.Click += new System.EventHandler(this.btnEditPassable_Click);

            this.menuRegionEdit.SetName("menuRegionEdit");
            this.menuRegionEdit.Text = "區域編輯模式";
            this.menuRegionEdit.Click += new System.EventHandler(this.btnRegionEdit_Click);

            this.menuLayer5Edit.SetName("menuLayer5Edit");
            this.menuLayer5Edit.Text = "透明編輯模式";
            this.menuLayer5Edit.Click += new System.EventHandler(this.btnEditLayer5_Click);

            this.menuValidateMap.SetName("menuValidateMap");
            this.menuValidateMap.Text = "驗證地圖正確性";
            this.menuValidateMap.Click += new System.EventHandler(this.btnMapValidate_Click);

            this.menuCleanupTiles.SetName("menuCleanupTiles");
            this.menuCleanupTiles.Text = "清理未使用的 Tiles...";
            this.menuCleanupTiles.ForeColor = Colors.Red;
            this.menuCleanupTiles.Click += new System.EventHandler(this.menuCleanupTiles_Click);

            // ========== 說明選單 ==========
            this.menuHelp.SetName("menuHelp");
            this.menuHelp.Text = "說明(&H)";
            this.menuHelp.DropDownItems.AddRange(new ToolStripItem[] {
                this.discordToolStripMenuItem,
                new ToolStripSeparator(),
                this.languageToolStripMenuItem,
                new ToolStripSeparator(),
                this.menuAbout
            });

            this.discordToolStripMenuItem.SetName("discordToolStripMenuItem");
            this.discordToolStripMenuItem.Text = "到 Discord 討論";
            this.discordToolStripMenuItem.Click += new System.EventHandler(this.discordToolStripMenuItem_Click);

            this.languageToolStripMenuItem.SetName("languageToolStripMenuItem");
            this.languageToolStripMenuItem.Text = "Language";
            this.languageToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
                this.langZhTWToolStripMenuItem,
                this.langJaJPToolStripMenuItem,
                this.langEnUSToolStripMenuItem
            });

            this.langZhTWToolStripMenuItem.SetName("langZhTWToolStripMenuItem");
            this.langZhTWToolStripMenuItem.Text = "繁體中文";
            this.langZhTWToolStripMenuItem.Tag = "zh-TW";
            this.langZhTWToolStripMenuItem.Click += new System.EventHandler(this.LanguageMenuItem_Click);

            this.langJaJPToolStripMenuItem.SetName("langJaJPToolStripMenuItem");
            this.langJaJPToolStripMenuItem.Text = "日本語";
            this.langJaJPToolStripMenuItem.Tag = "ja-JP";
            this.langJaJPToolStripMenuItem.Click += new System.EventHandler(this.LanguageMenuItem_Click);

            this.langEnUSToolStripMenuItem.SetName("langEnUSToolStripMenuItem");
            this.langEnUSToolStripMenuItem.Text = "English";
            this.langEnUSToolStripMenuItem.Tag = "en-US";
            this.langEnUSToolStripMenuItem.Click += new System.EventHandler(this.LanguageMenuItem_Click);

            this.menuAbout.SetName("menuAbout");
            this.menuAbout.Text = "關於...";
            this.menuAbout.Click += (s, e) => MessageBox.Show("L1MapViewer\n天堂地圖編輯器\n\n作者: Flyworld (Tony1223)", "關於");

            //
            // statusStrip1
            //
            this.statusStrip1.Items.AddRange(new ToolStripItem[] {
                this.toolStripStatusLabel1,
                this.toolStripShowAllL8,
                new ToolStripSeparator(),
                this.toolStripStatusLabel2,
                this.toolStripStatusLabel3,
                this.toolStripProgressBar1,
                new ToolStripSeparator(),
                this.toolStripCopyMoveCmd,
                new ToolStripSeparator(),
                this.toolStripJumpLabel,
                this.toolStripJumpTextBox,
                this.toolStripJumpButton
            });
            this.statusStrip1.SetLocation(new Point(0, 678));
            this.statusStrip1.SetName("statusStrip1");
            this.statusStrip1.Size = new Size(1200, 22);
            this.statusStrip1.TabIndex = 1;

            //
            // toolStripStatusLabel1
            //
            this.toolStripStatusLabel1.SetName("toolStripStatusLabel1");
            this.toolStripStatusLabel1.Size = new Size(300, 17);
            this.toolStripStatusLabel1.Text = "點擊獲取座標 | Ctrl+拖拽選擇範圍";

            //
            // toolStripStatusLabel2
            //
            this.toolStripStatusLabel2.SetName("toolStripStatusLabel2");
            this.toolStripStatusLabel2.Size = new Size(100, 17);

            //
            // toolStripStatusLabel3
            //
            this.toolStripStatusLabel3.SetName("toolStripStatusLabel3");
            this.toolStripStatusLabel3.Size = new Size(885, 17);
            this.toolStripStatusLabel3.Spring = true;

            //
            // toolStripProgressBar1
            //
            this.toolStripProgressBar1.SetName("toolStripProgressBar1");
            this.toolStripProgressBar1.Size = new Size(100, 16);
            this.toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
            this.toolStripProgressBar1.Visible = false;

            //
            // toolStripJumpLabel
            //
            this.toolStripJumpLabel.SetName("toolStripJumpLabel");
            this.toolStripJumpLabel.Size = new Size(60, 17);
            this.toolStripJumpLabel.Text = "跳轉座標:";

            //
            // toolStripJumpTextBox
            //
            this.toolStripJumpTextBox.SetName("toolStripJumpTextBox");
            this.toolStripJumpTextBox.Size = new Size(100, 22);
            this.toolStripJumpTextBox.ToolTipText = "輸入座標 (X,Y) 然後按 Enter 或點擊跳轉";
            this.toolStripJumpTextBox.KeyDown += new KeyEventHandler(this.toolStripJumpTextBox_KeyDown);

            //
            // toolStripJumpButton
            //
            this.toolStripJumpButton.SetName("toolStripJumpButton");
            this.toolStripJumpButton.Size = new Size(35, 20);
            this.toolStripJumpButton.Text = "Go";
            this.toolStripJumpButton.Click += new System.EventHandler(this.toolStripJumpButton_Click);

            //
            // toolStripShowAllL8
            //
            this.toolStripShowAllL8.SetName("toolStripShowAllL8");
            this.toolStripShowAllL8.Size = new Size(80, 20);
            this.toolStripShowAllL8.Text = LocalizationManager.L("L8_ShowAllL8");
            this.toolStripShowAllL8.ToolTipText = LocalizationManager.L("L8_ShowAllL8");
            this.toolStripShowAllL8.Click += new System.EventHandler(this.toolStripShowAllL8_Click);

            //
            // toolStripCopyMoveCmd
            //
            this.toolStripCopyMoveCmd.SetName("toolStripCopyMoveCmd");
            this.toolStripCopyMoveCmd.Size = new Size(120, 20);
            this.toolStripCopyMoveCmd.Text = "複製移動指令";
            this.toolStripCopyMoveCmd.ToolTipText = "複製 移動 x y 地圖id 指令到剪貼簿";
            this.toolStripCopyMoveCmd.Enabled = false;
            this.toolStripCopyMoveCmd.Click += new System.EventHandler(this.toolStripCopyMoveCmd_Click);

            //
            // leftPanel
            //
            this.leftPanel.BorderStyle = BorderStyle.FixedSingle;
            this.leftPanel.GetControls().Add(this.comboBox1);
            this.leftPanel.GetControls().Add(this.miniMapPictureBox);
            // leftTabControl 在 MapForm.cs 中以 Eto 原生方式建立
            this.leftPanel.SetDock(DockStyle.Left);
            this.leftPanel.SetLocation(new Point(0, 24));
            this.leftPanel.SetName("leftPanel");
            this.leftPanel.Size = new Size(280, 654);
            this.leftPanel.TabIndex = 2;

            //
            // comboBox1 (隱藏，保留給介面相容性)
            //
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.SetLocation(new Point(10, 10));
            this.comboBox1.SetName("comboBox1");
            this.comboBox1.Size = new Size(260, 23);
            this.comboBox1.TabIndex = 0;
            this.comboBox1.DropDownStyle = ComboBoxStyle.DropDown;
            this.comboBox1.MaxDropDownItems = 20;
            this.comboBox1.Visible = false;  // 隱藏
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            this.comboBox1.TextChanged += new System.EventHandler(this.comboBox1_TextChanged);

            //
            // miniMapPictureBox
            //
            this.miniMapPictureBox.BackgroundColor = Colors.Black;
            this.miniMapPictureBox.BorderStyle = BorderStyle.FixedSingle;
            this.miniMapPictureBox.SetLocation(new Point(5, 5));
            this.miniMapPictureBox.SetName("miniMapPictureBox");
            this.miniMapPictureBox.Size = new Size(268, 268);
            this.miniMapPictureBox.SetSizeMode(PictureBoxSizeMode.Normal);
            this.miniMapPictureBox.TabIndex = 1;
            this.miniMapPictureBox.SetTabStop(true);
            this.miniMapPictureBox.Cursor = Cursors.Hand;
            this.miniMapPictureBox.MouseDown += new MouseEventHandler(this.miniMapPictureBox_MouseDown);
            this.miniMapPictureBox.MouseMove += new MouseEventHandler(this.miniMapPictureBox_MouseMove);
            this.miniMapPictureBox.MouseUp += new MouseEventHandler(this.miniMapPictureBox_MouseUp);
            this.miniMapPictureBox.MouseClick += new MouseEventHandler(this.miniMapPictureBox_MouseClick);
            this.miniMapPictureBox.PreviewKeyDown += new PreviewKeyDownEventHandler(this.miniMapPictureBox_PreviewKeyDown);
            this.miniMapPictureBox.KeyDown += new KeyEventHandler(this.miniMapPictureBox_KeyDown);

            // leftTabControl, tabMapList, tabS32Files 在 MapForm.cs 中以 Eto 原生方式建立

            //
            // txtMapSearch
            //
            this.txtMapSearch.SetLocation(new Point(3, 3));
            this.txtMapSearch.SetName("txtMapSearch");
            this.txtMapSearch.Size = new Size(254, 23);
            this.txtMapSearch.TabIndex = 0;
            this.txtMapSearch.PlaceholderText = "搜尋地圖 (ID 或名稱)...";
            this.txtMapSearch.TextChanged += new System.EventHandler(this.txtMapSearch_TextChanged);

            //
            // lstMaps (地圖列表 ListBox)
            //
            this.lstMaps.SetLocation(new Point(3, 30));
            this.lstMaps.SetName("lstMaps");
            this.lstMaps.Size = new Size(254, 304);
            this.lstMaps.TabIndex = 1;
            this.lstMaps.SelectedIndexChanged += new System.EventHandler(this.lstMaps_SelectedIndexChanged);
            this.lstMaps.MouseUp += new MouseEventHandler(this.lstMaps_MouseUp);

            //
            // lblS32Files
            //
            this.lblS32Files.SetLocation(new Point(3, 3));
            this.lblS32Files.SetName("lblS32Files");
            this.lblS32Files.Size = new Size(100, 20);
            this.lblS32Files.TabIndex = 2;
            this.lblS32Files.Text = "S32 檔案清單";
            this.lblS32Files.SetTextAlign(ContentAlignment.MiddleLeft);

            //
            // btnS32SelectAll
            //
            this.btnS32SelectAll.SetLocation(new Point(150, 3));
            this.btnS32SelectAll.SetName("btnS32SelectAll");
            this.btnS32SelectAll.Size = new Size(50, 20);
            this.btnS32SelectAll.TabIndex = 20;
            this.btnS32SelectAll.Text = "全選";
            this.btnS32SelectAll.Click += new System.EventHandler(this.btnS32SelectAll_Click);

            //
            // btnS32SelectNone
            //
            this.btnS32SelectNone.SetLocation(new Point(205, 3));
            this.btnS32SelectNone.SetName("btnS32SelectNone");
            this.btnS32SelectNone.Size = new Size(50, 20);
            this.btnS32SelectNone.TabIndex = 21;
            this.btnS32SelectNone.Text = "全不選";
            this.btnS32SelectNone.Click += new System.EventHandler(this.btnS32SelectNone_Click);

            //
            // lstS32Files
            //
            this.lstS32Files.SetLocation(new Point(3, 26));
            this.lstS32Files.SetName("lstS32Files");
            this.lstS32Files.Size = new Size(254, 305);
            this.lstS32Files.TabIndex = 3;
            this.lstS32Files.CheckOnClick = true;
            this.lstS32Files.DrawMode = DrawMode.OwnerDrawFixed;
            this.lstS32Files.SelectedIndexChanged += new System.EventHandler(this.lstS32Files_SelectedIndexChanged);
            this.lstS32Files.ItemCheck += new ItemCheckEventHandler(this.lstS32Files_ItemCheck);
            this.lstS32Files.MouseUp += new MouseEventHandler(this.lstS32Files_MouseUp);
            this.lstS32Files.DrawItem += new DrawItemEventHandler(this.lstS32Files_DrawItem);

            //
            // tabControl1
            //
            this.tabControl1.GetControls().Add(this.tabS32Editor);
            this.tabControl1.SetLocation(new Point(290, 34));
            this.tabControl1.SetName("tabControl1");
            this.tabControl1.SelectedIndex = 0;  // S32 編輯器
            this.tabControl1.Size = new Size(710, 640);
            this.tabControl1.TabIndex = 3;

            //
            // tabMapPreview
            //
            this.tabMapPreview.BackgroundColor = Colors.Black;
            this.tabMapPreview.GetControls().Add(this.panel1);
            this.tabMapPreview.GetControls().Add(this.vScrollBar1);
            this.tabMapPreview.GetControls().Add(this.hScrollBar1);
            this.tabMapPreview.SetLocation(new Point(4, 22));
            this.tabMapPreview.SetName("tabMapPreview");
            this.tabMapPreview.Padding = new Padding(3);
            this.tabMapPreview.Size = new Size(702, 614);
            this.tabMapPreview.TabIndex = 0;
            this.tabMapPreview.Text = "地圖預覽";

            //
            // tabS32Editor
            //
            this.tabS32Editor.BackgroundColor = Colors.DarkGrey;
            this.tabS32Editor.GetControls().Add(this.s32EditorPanel);
            this.tabS32Editor.SetLocation(new Point(4, 22));
            this.tabS32Editor.SetName("tabS32Editor");
            this.tabS32Editor.Padding = new Padding(3);
            this.tabS32Editor.Size = new Size(702, 614);
            this.tabS32Editor.TabIndex = 1;
            this.tabS32Editor.Text = "S32 編輯器";

            //
            // panel1 (地圖顯示區域)
            //
            this.panel1.BackgroundColor = Colors.Black;
            this.panel1.BorderStyle = BorderStyle.None;
            this.panel1.GetControls().Add(this.pictureBox4);
            this.panel1.GetControls().Add(this.pictureBox3);
            this.panel1.GetControls().Add(this.pictureBox2);
            this.panel1.GetControls().Add(this.pictureBox1);
            this.panel1.SetLocation(new Point(3, 3));
            this.panel1.SetName("panel1");
            this.panel1.Size = new Size(679, 591);
            this.panel1.TabIndex = 0;

            //
            // pictureBox4
            //
            this.pictureBox4.BackgroundColor = Colors.Transparent;
            this.pictureBox4.SetDock(DockStyle.Fill);
            this.pictureBox4.SetLocation(new Point(0, 0));
            this.pictureBox4.SetName("pictureBox4");
            this.pictureBox4.Size = new Size(panel1.Width, panel1.Height);
            this.pictureBox4.TabIndex = 3;
            this.pictureBox4.SetTabStop(false);

            //
            // pictureBox3
            //
            this.pictureBox3.BackgroundColor = Colors.Transparent;
            this.pictureBox3.SetDock(DockStyle.Fill);
            this.pictureBox3.SetLocation(new Point(0, 0));
            this.pictureBox3.SetName("pictureBox3");
            this.pictureBox3.Size = new Size(panel1.Width, panel1.Height);
            this.pictureBox3.TabIndex = 2;
            this.pictureBox3.SetTabStop(false);

            //
            // pictureBox2
            //
            this.pictureBox2.BackgroundColor = Colors.Transparent;
            this.pictureBox2.SetDock(DockStyle.Fill);
            this.pictureBox2.SetLocation(new Point(0, 0));
            this.pictureBox2.SetName("pictureBox2");
            this.pictureBox2.Size = new Size(panel1.Width, panel1.Height);
            this.pictureBox2.TabIndex = 1;
            this.pictureBox2.SetTabStop(false);
            this.pictureBox2.Paint += new PaintEventHandler(this.pictureBox2_Paint);
            this.pictureBox2.MouseDown += new MouseEventHandler(this.pictureBox2_MouseDown);
            this.pictureBox2.MouseMove += new MouseEventHandler(this.pictureBox2_MouseMove);
            this.pictureBox2.MouseUp += new MouseEventHandler(this.pictureBox2_MouseUp);

            //
            // pictureBox1
            //
            this.pictureBox1.SetLocation(new Point(3, 3));
            this.pictureBox1.SetName("pictureBox1");
            this.pictureBox1.Size = new Size(100, 50);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.SetTabStop(false);

            //
            // vScrollBar1
            //
            this.vScrollBar1.SetLocation(new Point(682, 3));
            this.vScrollBar1.SetName("vScrollBar1");
            this.vScrollBar1.Size = new Size(17, 591);
            this.vScrollBar1.TabIndex = 1;
            this.vScrollBar1.Scroll += new ScrollEventHandler(this.vScrollBar1_Scroll);

            //
            // hScrollBar1
            //
            this.hScrollBar1.SetLocation(new Point(3, 594));
            this.hScrollBar1.SetName("hScrollBar1");
            this.hScrollBar1.Size = new Size(679, 17);
            this.hScrollBar1.TabIndex = 2;
            this.hScrollBar1.Scroll += new ScrollEventHandler(this.hScrollBar1_Scroll);

            //
            // s32EditorPanel
            //
            this.s32EditorPanel.BackgroundColor = Colors.White;
            this.s32EditorPanel.GetControls().Add(this.layerFloatPanel);
            this.s32EditorPanel.GetControls().Add(this.s32MapPanel);
            this.s32EditorPanel.GetControls().Add(this.s32LayerControlPanel);
            this.s32EditorPanel.GetControls().Add(this.lblS32Info);
            this.s32EditorPanel.SetDock(DockStyle.Fill);
            this.s32EditorPanel.SetLocation(new Point(3, 3));
            this.s32EditorPanel.SetName("s32EditorPanel");
            this.s32EditorPanel.Size = new Size(696, 608);
            this.s32EditorPanel.TabIndex = 0;
            this.s32EditorPanel.Resize += new System.EventHandler(this.s32EditorPanel_Resize);

            //
            // s32LayerControlPanel
            //
            this.s32LayerControlPanel.BackgroundColor = Colors.LightGrey;
            this.s32LayerControlPanel.BorderStyle = BorderStyle.FixedSingle;
            this.s32LayerControlPanel.GetControls().Add(this.btnCopySettings);
            this.s32LayerControlPanel.GetControls().Add(this.btnCopyMapCoords);
            this.s32LayerControlPanel.GetControls().Add(this.btnImportFs32);
            this.s32LayerControlPanel.GetControls().Add(this.btnEditPassable);
            this.s32LayerControlPanel.GetControls().Add(this.btnEditLayer5);
            this.s32LayerControlPanel.GetControls().Add(this.btnRegionEdit);
            this.s32LayerControlPanel.GetControls().Add(this.btnSaveS32);
            this.s32LayerControlPanel.GetControls().Add(this.btnReloadMap);
            this.s32LayerControlPanel.GetControls().Add(this.btnAnalyzeAttr);
            this.s32LayerControlPanel.SetDock(DockStyle.Top);
            this.s32LayerControlPanel.SetLocation(new Point(0, 0));
            this.s32LayerControlPanel.SetName("s32LayerControlPanel");
            this.s32LayerControlPanel.Size = new Size(696, 65);
            this.s32LayerControlPanel.TabIndex = 0;

            //
            // chkLayer1
            //
            this.chkLayer1.SetAutoSize(true);
            this.chkLayer1.Checked = true;
            this.chkLayer1.CheckState = CheckState.Checked;
            this.chkLayer1.SetLocation(new Point(10, 10));
            this.chkLayer1.SetName("chkLayer1");
            this.chkLayer1.Size = new Size(90, 17);
            this.chkLayer1.TabIndex = 0;
            this.chkLayer1.Text = "第1層 (地板)";
            this.chkLayer1.SetUseVisualStyleBackColor(true);
            this.chkLayer1.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkLayer2
            //
            this.chkLayer2.SetAutoSize(true);
            this.chkLayer2.Checked = true;
            this.chkLayer2.SetLocation(new Point(110, 10));
            this.chkLayer2.SetName("chkLayer2");
            this.chkLayer2.Size = new Size(60, 17);
            this.chkLayer2.TabIndex = 1;
            this.chkLayer2.Text = "第2層";
            this.chkLayer2.SetUseVisualStyleBackColor(true);
            this.chkLayer2.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkLayer3
            //
            this.chkLayer3.SetAutoSize(true);
            this.chkLayer3.SetLocation(new Point(180, 10));
            this.chkLayer3.SetName("chkLayer3");
            this.chkLayer3.Size = new Size(90, 17);
            this.chkLayer3.TabIndex = 2;
            this.chkLayer3.Text = "第3層 (多色屬性)";
            this.chkLayer3.SetUseVisualStyleBackColor(true);
            this.chkLayer3.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkLayer4
            //
            this.chkLayer4.SetAutoSize(true);
            this.chkLayer4.Checked = true;
            this.chkLayer4.CheckState = CheckState.Checked;
            this.chkLayer4.SetLocation(new Point(280, 10));
            this.chkLayer4.SetName("chkLayer4");
            this.chkLayer4.Size = new Size(90, 17);
            this.chkLayer4.TabIndex = 3;
            this.chkLayer4.Text = "第4層 (物件)";
            this.chkLayer4.SetUseVisualStyleBackColor(true);
            this.chkLayer4.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkShowPassable
            //
            this.chkShowPassable.SetAutoSize(true);
            this.chkShowPassable.SetLocation(new Point(380, 10));
            this.chkShowPassable.SetName("chkShowPassable");
            this.chkShowPassable.Size = new Size(100, 17);
            this.chkShowPassable.TabIndex = 4;
            this.chkShowPassable.Text = "通行性 (紅藍)";
            this.chkShowPassable.SetUseVisualStyleBackColor(true);
            this.chkShowPassable.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkShowGrid
            //
            this.chkShowGrid.SetAutoSize(true);
            this.chkShowGrid.Checked = true;
            this.chkShowGrid.CheckState = CheckState.Checked;
            this.chkShowGrid.SetLocation(new Point(490, 10));
            this.chkShowGrid.SetName("chkShowGrid");
            this.chkShowGrid.Size = new Size(90, 17);
            this.chkShowGrid.TabIndex = 5;
            this.chkShowGrid.Text = "顯示格線";
            this.chkShowGrid.SetUseVisualStyleBackColor(true);
            this.chkShowGrid.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkShowS32Boundary
            //
            this.chkShowS32Boundary.SetAutoSize(true);
            this.chkShowS32Boundary.SetLocation(new Point(570, 10));
            this.chkShowS32Boundary.SetName("chkShowS32Boundary");
            this.chkShowS32Boundary.Size = new Size(90, 17);
            this.chkShowS32Boundary.TabIndex = 7;
            this.chkShowS32Boundary.Text = "S32邊界";
            this.chkShowS32Boundary.SetUseVisualStyleBackColor(true);
            this.chkShowS32Boundary.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkShowLayer5
            //
            this.chkShowLayer5.SetName("chkShowLayer5");
            this.chkShowLayer5.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkShowSafeZones
            //
            this.chkShowSafeZones.SetAutoSize(true);
            this.chkShowSafeZones.SetLocation(new Point(650, 10));
            this.chkShowSafeZones.SetName("chkShowSafeZones");
            this.chkShowSafeZones.Size = new Size(70, 17);
            this.chkShowSafeZones.TabIndex = 16;
            this.chkShowSafeZones.Text = "安全區域";
            this.chkShowSafeZones.SetUseVisualStyleBackColor(true);
            this.chkShowSafeZones.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkShowCombatZones
            //
            this.chkShowCombatZones.SetAutoSize(true);
            this.chkShowCombatZones.SetLocation(new Point(730, 10));
            this.chkShowCombatZones.SetName("chkShowCombatZones");
            this.chkShowCombatZones.Size = new Size(70, 17);
            this.chkShowCombatZones.TabIndex = 17;
            this.chkShowCombatZones.Text = "戰鬥區域";
            this.chkShowCombatZones.SetUseVisualStyleBackColor(true);
            this.chkShowCombatZones.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // btnCopySettings
            //
            this.btnCopySettings.SetLocation(new Point(210, 5));
            this.btnCopySettings.SetName("btnCopySettings");
            this.btnCopySettings.Size = new Size(90, 25);
            this.btnCopySettings.TabIndex = 8;
            this.btnCopySettings.Text = "複製設定...";
            this.btnCopySettings.SetUseVisualStyleBackColor(true);
            this.btnCopySettings.Click += new System.EventHandler(this.btnCopySettings_Click);

            //
            // btnCopyMapCoords
            //
            this.btnCopyMapCoords.SetLocation(new Point(310, 5));
            this.btnCopyMapCoords.SetName("btnCopyMapCoords");
            this.btnCopyMapCoords.Size = new Size(75, 25);
            this.btnCopyMapCoords.TabIndex = 13;
            this.btnCopyMapCoords.Text = "複製座標";
            this.btnCopyMapCoords.SetUseVisualStyleBackColor(true);
            this.btnCopyMapCoords.Click += new System.EventHandler(this.btnCopyMapCoords_Click);

            //
            // btnImportFs32
            //
            this.btnImportFs32.SetLocation(new Point(395, 5));
            this.btnImportFs32.SetName("btnImportFs32");
            this.btnImportFs32.Size = new Size(90, 25);
            this.btnImportFs32.TabIndex = 16;
            this.btnImportFs32.Text = "匯入地圖包";
            this.btnImportFs32.SetUseVisualStyleBackColor(true);
            this.btnImportFs32.Click += new System.EventHandler(this.btnImportFs32_Click);

            //
            // btnEditPassable
            //
            this.btnEditPassable.SetLocation(new Point(10, 35));
            this.btnEditPassable.SetName("btnEditPassable");
            this.btnEditPassable.Size = new Size(80, 25);
            this.btnEditPassable.TabIndex = 9;
            this.btnEditPassable.Text = "通行編輯";
            this.btnEditPassable.SetUseVisualStyleBackColor(true);
            this.btnEditPassable.Click += new System.EventHandler(this.btnEditPassable_Click);

            //
            // btnEditLayer5
            //
            this.btnEditLayer5.SetLocation(new Point(100, 35));
            this.btnEditLayer5.SetName("btnEditLayer5");
            this.btnEditLayer5.Size = new Size(80, 25);
            this.btnEditLayer5.TabIndex = 14;
            this.btnEditLayer5.Text = "透明編輯";
            this.btnEditLayer5.SetUseVisualStyleBackColor(true);
            this.btnEditLayer5.Click += new System.EventHandler(this.btnEditLayer5_Click);

            //
            // btnMergeL2ToL1
            //
            this.btnMergeL2ToL1.SetLocation(new Point(190, 35));
            this.btnMergeL2ToL1.SetName("btnMergeL2ToL1");
            this.btnMergeL2ToL1.Size = new Size(80, 25);
            this.btnMergeL2ToL1.TabIndex = 16;
            this.btnMergeL2ToL1.Text = "L2合併L1";
            this.btnMergeL2ToL1.SetUseVisualStyleBackColor(true);
            this.btnMergeL2ToL1.Click += new System.EventHandler(this.btnMergeL2ToL1_Click);

            //
            // btnRegionEdit
            //
            this.btnRegionEdit.SetLocation(new Point(280, 35));
            this.btnRegionEdit.SetName("btnRegionEdit");
            this.btnRegionEdit.Size = new Size(80, 25);
            this.btnRegionEdit.TabIndex = 15;
            this.btnRegionEdit.Text = "戰鬥區域";
            this.btnRegionEdit.SetUseVisualStyleBackColor(true);
            this.btnRegionEdit.Click += new System.EventHandler(this.btnRegionEdit_Click);

            //
            // btnSaveS32
            //
            this.btnSaveS32.SetLocation(new Point(120, 5));
            this.btnSaveS32.SetName("btnSaveS32");
            this.btnSaveS32.Size = new Size(80, 25);
            this.btnSaveS32.TabIndex = 11;
            this.btnSaveS32.Text = "保存 S32";
            this.btnSaveS32.SetUseVisualStyleBackColor(true);
            this.btnSaveS32.Click += new System.EventHandler(this.btnSaveS32_Click);

            //
            // btnReloadMap
            //
            this.btnReloadMap.SetLocation(new Point(10, 5));
            this.btnReloadMap.SetName("btnReloadMap");
            this.btnReloadMap.Size = new Size(100, 25);
            this.btnReloadMap.TabIndex = 12;
            this.btnReloadMap.Text = "重新載入 (F5)";
            this.btnReloadMap.SetUseVisualStyleBackColor(true);
            this.btnReloadMap.Click += new System.EventHandler(this.btnReloadMap_Click);

            //
            // btnAnalyzeAttr
            //
            this.btnAnalyzeAttr.SetLocation(new Point(315, 35));
            this.btnAnalyzeAttr.SetName("btnAnalyzeAttr");
            this.btnAnalyzeAttr.Size = new Size(80, 25);
            this.btnAnalyzeAttr.TabIndex = 17;
            this.btnAnalyzeAttr.Text = "分析屬性";
            this.btnAnalyzeAttr.SetUseVisualStyleBackColor(true);
            this.btnAnalyzeAttr.Visible = false;

            //
            // s32MapPanel
            //
            this.s32MapPanel.AutoScroll = false;
            this.s32MapPanel.BackgroundColor = Colors.Black;
            this.s32MapPanel.BorderStyle = BorderStyle.FixedSingle;
            this.s32MapPanel.GetControls().Add(this._mapViewerControl);
            this.s32MapPanel.SetDock(DockStyle.Fill);
            this.s32MapPanel.SetLocation(new Point(0, 65));
            this.s32MapPanel.SetName("s32MapPanel");
            this.s32MapPanel.Size = new Size(696, 523);
            this.s32MapPanel.TabIndex = 1;

            //
            // _mapViewerControl
            //
            this._mapViewerControl.BackgroundColor = Colors.Black;
            this._mapViewerControl.SetDock(DockStyle.Fill);
            this._mapViewerControl.SetName("_mapViewerControl");
            this._mapViewerControl.TabIndex = 0;
            this._mapViewerControl.SetTabStop(false);

            //
            // lblS32Info
            //
            this.lblS32Info.BackgroundColor = Colors.DarkGrey;
            this.lblS32Info.SetDock(DockStyle.Bottom);
            this.lblS32Info.SetLocation(new Point(0, 588));
            this.lblS32Info.SetName("lblS32Info");
            this.lblS32Info.Size = new Size(696, 20);
            this.lblS32Info.TabIndex = 2;
            this.lblS32Info.Text = "請選擇一個 S32 檔案";
            this.lblS32Info.SetTextAlign(ContentAlignment.MiddleLeft);

            //
            // layerFloatPanel (浮動圖層控制面板)
            //
            this.layerFloatPanel.BackgroundColor = Color.FromArgb(200, 50, 50, 50);
            this.layerFloatPanel.GetControls().Add(this.lblLayerIcon);
            this.layerFloatPanel.GetControls().Add(this.layerPopupPanel);
            this.layerFloatPanel.SetLocation(new Point(10, 10));
            this.layerFloatPanel.SetName("layerFloatPanel");
            this.layerFloatPanel.Size = new Size(110, 295);
            this.layerFloatPanel.TabIndex = 10;
            this.layerFloatPanel.SetAnchor(AnchorStyles.Top | AnchorStyles.Right);

            //
            // lblLayerIcon (圖層圖示)
            //
            this.lblLayerIcon.BackgroundColor = Colors.Transparent;
            this.lblLayerIcon.Font = FontHelper.CreateUIFont(10F, FontStyle.Bold);
            this.lblLayerIcon.TextColor = Colors.White;
            this.lblLayerIcon.SetLocation(new Point(0, 0));
            this.lblLayerIcon.SetName("lblLayerIcon");
            this.lblLayerIcon.Size = new Size(90, 24);
            this.lblLayerIcon.TabIndex = 0;
            this.lblLayerIcon.Text = "▣ 圖層";
            this.lblLayerIcon.SetTextAlign(ContentAlignment.MiddleCenter);

            //
            // layerPopupPanel (展開的選項面板)
            //
            this.layerPopupPanel.BackgroundColor = Color.FromArgb(230, 40, 40, 40);
            this.layerPopupPanel.GetControls().Add(this.chkFloatLayer1);
            this.layerPopupPanel.GetControls().Add(this.chkFloatLayer2);
            this.layerPopupPanel.GetControls().Add(this.chkFloatLayer4);
            this.layerPopupPanel.GetControls().Add(this.chkFloatLayer5);
            this.layerPopupPanel.GetControls().Add(this.chkFloatPassable);
            this.layerPopupPanel.GetControls().Add(this.chkFloatGrid);
            this.layerPopupPanel.GetControls().Add(this.chkFloatS32Boundary);
            this.layerPopupPanel.GetControls().Add(this.chkFloatSafeZones);
            this.layerPopupPanel.GetControls().Add(this.chkFloatCombatZones);
            this.layerPopupPanel.GetControls().Add(this.chkFloatLayer8Spr);
            this.layerPopupPanel.GetControls().Add(this.chkFloatLayer8Marker);
            this.layerPopupPanel.SetLocation(new Point(0, 24));
            this.layerPopupPanel.SetName("layerPopupPanel");
            this.layerPopupPanel.Padding = new Padding(5);
            this.layerPopupPanel.Size = new Size(110, 290);
            this.layerPopupPanel.TabIndex = 1;
            this.layerPopupPanel.Visible = true;

            //
            // chkFloatLayer1
            //
            this.chkFloatLayer1.SetAutoSize(true);
            this.chkFloatLayer1.Checked = true;
            this.chkFloatLayer1.CheckState = CheckState.Checked;
            this.chkFloatLayer1.TextColor = Colors.White;
            this.chkFloatLayer1.SetLocation(new Point(8, 5));
            this.chkFloatLayer1.SetName("chkFloatLayer1");
            this.chkFloatLayer1.Size = new Size(80, 19);
            this.chkFloatLayer1.TabIndex = 0;
            this.chkFloatLayer1.Text = "L1 地板";
            this.chkFloatLayer1.SetUseVisualStyleBackColor(true);
            this.chkFloatLayer1.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatLayer2
            //
            this.chkFloatLayer2.SetAutoSize(true);
            this.chkFloatLayer2.Checked = true;
            this.chkFloatLayer2.TextColor = Colors.LightGrey;
            this.chkFloatLayer2.SetLocation(new Point(8, 27));
            this.chkFloatLayer2.SetName("chkFloatLayer2");
            this.chkFloatLayer2.Size = new Size(80, 19);
            this.chkFloatLayer2.TabIndex = 1;
            this.chkFloatLayer2.Text = "L2";
            this.chkFloatLayer2.SetUseVisualStyleBackColor(true);
            this.chkFloatLayer2.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatLayer4
            //
            this.chkFloatLayer4.SetAutoSize(true);
            this.chkFloatLayer4.Checked = true;
            this.chkFloatLayer4.CheckState = CheckState.Checked;
            this.chkFloatLayer4.TextColor = Colors.White;
            this.chkFloatLayer4.SetLocation(new Point(8, 49));
            this.chkFloatLayer4.SetName("chkFloatLayer4");
            this.chkFloatLayer4.Size = new Size(80, 19);
            this.chkFloatLayer4.TabIndex = 2;
            this.chkFloatLayer4.Text = "L4 物件";
            this.chkFloatLayer4.SetUseVisualStyleBackColor(true);
            this.chkFloatLayer4.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatLayer5 (放在 L4 下面)
            //
            this.chkFloatLayer5.SetAutoSize(true);
            this.chkFloatLayer5.TextColor = Color.FromArgb(100, 180, 255);
            this.chkFloatLayer5.SetLocation(new Point(8, 71));
            this.chkFloatLayer5.SetName("chkFloatLayer5");
            this.chkFloatLayer5.Size = new Size(80, 19);
            this.chkFloatLayer5.TabIndex = 3;
            this.chkFloatLayer5.Text = "L5 透明";
            this.chkFloatLayer5.SetUseVisualStyleBackColor(true);
            this.chkFloatLayer5.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatPassable
            //
            this.chkFloatPassable.SetAutoSize(true);
            this.chkFloatPassable.TextColor = Colors.LightGreen;
            this.chkFloatPassable.SetLocation(new Point(8, 93));
            this.chkFloatPassable.SetName("chkFloatPassable");
            this.chkFloatPassable.Size = new Size(80, 19);
            this.chkFloatPassable.TabIndex = 4;
            this.chkFloatPassable.Text = "通行";
            this.chkFloatPassable.SetUseVisualStyleBackColor(true);
            this.chkFloatPassable.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatGrid
            //
            this.chkFloatGrid.SetAutoSize(true);
            this.chkFloatGrid.TextColor = Colors.LightBlue;
            this.chkFloatGrid.SetLocation(new Point(8, 115));
            this.chkFloatGrid.SetName("chkFloatGrid");
            this.chkFloatGrid.Size = new Size(80, 19);
            this.chkFloatGrid.TabIndex = 5;
            this.chkFloatGrid.Text = "格線";
            this.chkFloatGrid.SetUseVisualStyleBackColor(true);
            this.chkFloatGrid.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatS32Boundary
            //
            this.chkFloatS32Boundary.SetAutoSize(true);
            this.chkFloatS32Boundary.TextColor = Colors.Orange;
            this.chkFloatS32Boundary.SetLocation(new Point(8, 137));
            this.chkFloatS32Boundary.SetName("chkFloatS32Boundary");
            this.chkFloatS32Boundary.Size = new Size(80, 19);
            this.chkFloatS32Boundary.TabIndex = 6;
            this.chkFloatS32Boundary.Text = "S32邊界";
            this.chkFloatS32Boundary.SetUseVisualStyleBackColor(true);
            this.chkFloatS32Boundary.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatSafeZones
            //
            this.chkFloatSafeZones.SetAutoSize(true);
            this.chkFloatSafeZones.TextColor = Color.FromArgb(100, 180, 255);
            this.chkFloatSafeZones.SetLocation(new Point(8, 159));
            this.chkFloatSafeZones.SetName("chkFloatSafeZones");
            this.chkFloatSafeZones.Size = new Size(80, 19);
            this.chkFloatSafeZones.TabIndex = 7;
            this.chkFloatSafeZones.Text = "安全區域";
            this.chkFloatSafeZones.SetUseVisualStyleBackColor(true);
            this.chkFloatSafeZones.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatCombatZones
            //
            this.chkFloatCombatZones.SetAutoSize(true);
            this.chkFloatCombatZones.TextColor = Color.FromArgb(255, 100, 100);
            this.chkFloatCombatZones.SetLocation(new Point(8, 181));
            this.chkFloatCombatZones.SetName("chkFloatCombatZones");
            this.chkFloatCombatZones.Size = new Size(80, 19);
            this.chkFloatCombatZones.TabIndex = 8;
            this.chkFloatCombatZones.Text = "戰鬥區域";
            this.chkFloatCombatZones.SetUseVisualStyleBackColor(true);
            this.chkFloatCombatZones.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatLayer8Spr
            //
            this.chkFloatLayer8Spr.SetAutoSize(true);
            this.chkFloatLayer8Spr.TextColor = Color.FromArgb(255, 180, 100);
            this.chkFloatLayer8Spr.SetLocation(new Point(8, 203));
            this.chkFloatLayer8Spr.SetName("chkFloatLayer8Spr");
            this.chkFloatLayer8Spr.Size = new Size(80, 19);
            this.chkFloatLayer8Spr.TabIndex = 9;
            this.chkFloatLayer8Spr.Text = LocalizationManager.L("Layer_L8Spr");
            this.chkFloatLayer8Spr.SetUseVisualStyleBackColor(true);
            this.chkFloatLayer8Spr.Checked = true;
            this.chkFloatLayer8Spr.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatLayer8Marker
            //
            this.chkFloatLayer8Marker.SetAutoSize(true);
            this.chkFloatLayer8Marker.TextColor = Color.FromArgb(255, 180, 100);
            this.chkFloatLayer8Marker.SetLocation(new Point(8, 223));
            this.chkFloatLayer8Marker.SetName("chkFloatLayer8Marker");
            this.chkFloatLayer8Marker.Size = new Size(80, 19);
            this.chkFloatLayer8Marker.TabIndex = 10;
            this.chkFloatLayer8Marker.Text = LocalizationManager.L("Layer_L8Marker");
            this.chkFloatLayer8Marker.SetUseVisualStyleBackColor(true);
            this.chkFloatLayer8Marker.Checked = true;
            this.chkFloatLayer8Marker.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // rightPanel
            //
            this.rightPanel.BorderStyle = BorderStyle.FixedSingle;
            this.rightPanel.GetControls().Add(this.lblTileList);
            this.rightPanel.GetControls().Add(this.txtTileSearch);
            this.rightPanel.GetControls().Add(this.lvTiles);
            this.rightPanel.GetControls().Add(this.lblMaterials);
            this.rightPanel.GetControls().Add(this.lvMaterials);
            this.rightPanel.GetControls().Add(this.btnMoreMaterials);
            this.rightPanel.GetControls().Add(this.lblGroupThumbnails);
            this.rightPanel.GetControls().Add(this.txtGroupSearch);
            this.rightPanel.GetControls().Add(this.cmbGroupMode);
            this.rightPanel.GetControls().Add(this.lvGroupThumbnails);
            this.rightPanel.SetDock(DockStyle.Right);
            this.rightPanel.SetLocation(new Point(1010, 24));
            this.rightPanel.SetName("rightPanel");
            this.rightPanel.Size = new Size(220, 654);
            this.rightPanel.TabIndex = 6;
            this.rightPanel.AllowDrop = true;
            this.rightPanel.DragEnter += new DragEventHandler(this.lvMaterials_DragEnter);
            this.rightPanel.DragOver += new DragEventHandler(this.lvMaterials_DragOver);
            this.rightPanel.DragDrop += new DragEventHandler(this.lvMaterials_DragDrop);

            //
            // lblTileList
            //
            this.lblTileList.SetLocation(new Point(5, 5));
            this.lblTileList.SetName("lblTileList");
            this.lblTileList.Size = new Size(210, 20);
            this.lblTileList.TabIndex = 0;
            this.lblTileList.Text = "使用的 Tile";
            this.lblTileList.SetTextAlign(ContentAlignment.MiddleLeft);

            //
            // txtTileSearch
            //
            this.txtTileSearch.SetLocation(new Point(5, 28));
            this.txtTileSearch.SetName("txtTileSearch");
            this.txtTileSearch.Size = new Size(210, 23);
            this.txtTileSearch.TabIndex = 7;
            this.txtTileSearch.PlaceholderText = "搜尋 TileId...";
            this.txtTileSearch.TextChanged += new System.EventHandler(this.txtTileSearch_TextChanged);

            //
            // lvTiles
            //
            this.lvTiles.Size = new Size(210, 125);
            this.lvTiles.ImageSize = 40;
            this.lvTiles.TileWidth = 60;
            this.lvTiles.TileHeight = 75;
            this.lvTiles.ItemDoubleClick += new System.EventHandler(this.lvTiles_DoubleClick);
            this.lvTiles.MouseUp += new System.EventHandler<Eto.Forms.MouseEventArgs>(this.lvTiles_MouseUp_Eto);

            //
            // lblMaterials
            //
            this.lblMaterials.SetLocation(new Point(5, 185));
            this.lblMaterials.SetName("lblMaterials");
            this.lblMaterials.Size = new Size(210, 20);
            this.lblMaterials.TabIndex = 2;
            this.lblMaterials.Text = "最近的素材";
            this.lblMaterials.SetTextAlign(ContentAlignment.MiddleLeft);

            //
            // lvMaterials
            //
            this.lvMaterials.Size = new Size(210, 95);
            this.lvMaterials.ImageSize = 40;
            this.lvMaterials.TileWidth = 60;
            this.lvMaterials.TileHeight = 75;
            this.lvMaterials.MultiSelect = false;
            this.lvMaterials.AllowDrop = true;
            this.lvMaterials.ItemDoubleClick += new System.EventHandler(this.lvMaterials_DoubleClick_Eto);
            this.lvMaterials.MouseUp += new System.EventHandler<Eto.Forms.MouseEventArgs>(this.lvMaterials_MouseUp_Eto);

            //
            // btnMoreMaterials
            //
            this.btnMoreMaterials.SetLocation(new Point(5, 308));
            this.btnMoreMaterials.SetName("btnMoreMaterials");
            this.btnMoreMaterials.Size = new Size(210, 23);
            this.btnMoreMaterials.TabIndex = 8;
            this.btnMoreMaterials.Text = "更多...";
            this.btnMoreMaterials.SetUseVisualStyleBackColor(true);
            this.btnMoreMaterials.Click += new System.EventHandler(this.btnMoreMaterials_Click);

            //
            // lblGroupThumbnails
            //
            this.lblGroupThumbnails.SetLocation(new Point(5, 335));
            this.lblGroupThumbnails.SetName("lblGroupThumbnails");
            this.lblGroupThumbnails.Size = new Size(140, 20);
            this.lblGroupThumbnails.TabIndex = 4;
            this.lblGroupThumbnails.Text = "群組縮圖列表";
            this.lblGroupThumbnails.SetTextAlign(ContentAlignment.MiddleLeft);

            //
            // cmbGroupMode
            //
            this.cmbGroupMode.SetLocation(new Point(5, 358));
            this.cmbGroupMode.SetName("cmbGroupMode");
            this.cmbGroupMode.Size = new Size(100, 23);
            this.cmbGroupMode.TabIndex = 8;
            this.cmbGroupMode.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbGroupMode.Items.AddRange(new object[] {
                LocalizationManager.L("GroupMode_SelectedArea"),
                LocalizationManager.L("GroupMode_SelectedAreaAll"),
                LocalizationManager.L("GroupMode_All")
            });
            this.cmbGroupMode.SelectedIndex = 0;
            this.cmbGroupMode.SelectedIndexChanged += new System.EventHandler(this.cmbGroupMode_SelectedIndexChanged);

            //
            // txtGroupSearch
            //
            this.txtGroupSearch.SetLocation(new Point(110, 358));
            this.txtGroupSearch.SetName("txtGroupSearch");
            this.txtGroupSearch.Size = new Size(100, 23);
            this.txtGroupSearch.TabIndex = 9;
            this.txtGroupSearch.PlaceholderText = "搜尋...";
            this.txtGroupSearch.TextChanged += new System.EventHandler(this.txtGroupSearch_TextChanged);

            //
            // lvGroupThumbnails
            //
            this.lvGroupThumbnails.Size = new Size(210, 260);
            this.lvGroupThumbnails.MultiSelect = true;
            this.lvGroupThumbnails.ImageSize = 40;
            this.lvGroupThumbnails.TileWidth = 60;
            this.lvGroupThumbnails.TileHeight = 75;
            this.lvGroupThumbnails.MouseDown += new System.EventHandler<Eto.Forms.MouseEventArgs>(this.lvGroupThumbnails_MouseClick_Eto);
            this.lvGroupThumbnails.ItemDoubleClick += new System.EventHandler(this.lvGroupThumbnails_DoubleClick);
            this.lvGroupThumbnails.MouseUp += new System.EventHandler<Eto.Forms.MouseEventArgs>(this.lvGroupThumbnails_MouseUp_Eto);
            this.lvGroupThumbnails.SelectionChanged += new System.EventHandler(this.lvGroupThumbnails_SelectionChanged);

            //
            // toolbarPanel (第一排)
            //
            this.toolbarPanel.BorderStyle = BorderStyle.FixedSingle;
            this.toolbarPanel.GetControls().Add(this.btnToolCopy);
            this.toolbarPanel.GetControls().Add(this.btnToolPaste);
            this.toolbarPanel.GetControls().Add(this.btnToolDelete);
            this.toolbarPanel.GetControls().Add(this.btnToolUndo);
            this.toolbarPanel.GetControls().Add(this.btnToolRedo);
            this.toolbarPanel.GetControls().Add(this.btnToolSave);
            this.toolbarPanel.GetControls().Add(this.btnToolCellInfo);
            this.toolbarPanel.GetControls().Add(this.btnToolReplaceTile);
            this.toolbarPanel.GetControls().Add(this.btnToolAddS32);
            this.toolbarPanel.GetControls().Add(this.btnToolClearLayer7);
            this.toolbarPanel.GetControls().Add(this.btnToolClearCell);
            this.toolbarPanel.GetControls().Add(this.btnMapValidate);
            this.toolbarPanel.SetDock(DockStyle.Left);
            this.toolbarPanel.SetLocation(new Point(0, 0));
            this.toolbarPanel.SetName("toolbarPanel");
            this.toolbarPanel.Size = new Size(40, 654);
            this.toolbarPanel.TabIndex = 7;

            //
            // toolbarPanel2 (第二排 - 查看各層)
            //
            this.toolbarPanel2.BorderStyle = BorderStyle.FixedSingle;
            this.toolbarPanel2.GetControls().Add(this.btnToolCheckL1);
            this.toolbarPanel2.GetControls().Add(this.btnToolCheckL2);
            this.toolbarPanel2.GetControls().Add(this.btnToolCheckL3);
            this.toolbarPanel2.GetControls().Add(this.btnToolCheckL4);
            this.toolbarPanel2.GetControls().Add(this.btnToolCheckL5);
            this.toolbarPanel2.GetControls().Add(this.btnToolCheckL6);
            this.toolbarPanel2.GetControls().Add(this.btnToolCheckL7);
            this.toolbarPanel2.GetControls().Add(this.btnToolCheckL8);
            this.toolbarPanel2.GetControls().Add(this.btnEnableVisibleL8);
            this.toolbarPanel2.GetControls().Add(this.btnViewClipboard);
            this.toolbarPanel2.GetControls().Add(this.btnToolTestTil);
            this.toolbarPanel2.GetControls().Add(this.btnToolClearTestTil);
            this.toolbarPanel2.SetDock(DockStyle.Left);
            this.toolbarPanel2.SetLocation(new Point(40, 0));
            this.toolbarPanel2.SetName("toolbarPanel2");
            this.toolbarPanel2.Size = new Size(40, 654);
            this.toolbarPanel2.TabIndex = 8;

            //
            // toolbarContainer (容器)
            //
            this.toolbarContainer.GetControls().Add(this.toolbarPanel2);
            this.toolbarContainer.GetControls().Add(this.toolbarPanel);
            this.toolbarContainer.SetDock(DockStyle.Right);
            this.toolbarContainer.SetName("toolbarContainer");
            this.toolbarContainer.Size = new Size(80, 654);
            this.toolbarContainer.TabIndex = 9;

            //
            // btnToolCopy
            //
            this.btnToolCopy.SetLocation(new Point(2, 5));
            this.btnToolCopy.SetName("btnToolCopy");
            this.btnToolCopy.Size = new Size(34, 34);
            this.btnToolCopy.TabIndex = 0;
            this.btnToolCopy.Text = "複製";
            this.btnToolCopy.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolCopy, "複製 (Ctrl+C)");
            this.btnToolCopy.Click += new System.EventHandler(this.btnToolCopy_Click);

            //
            // btnToolPaste
            //
            this.btnToolPaste.SetLocation(new Point(2, 44));
            this.btnToolPaste.SetName("btnToolPaste");
            this.btnToolPaste.Size = new Size(34, 34);
            this.btnToolPaste.TabIndex = 1;
            this.btnToolPaste.Text = "貼上";
            this.btnToolPaste.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolPaste, "貼上 (Ctrl+V)");
            this.btnToolPaste.Click += new System.EventHandler(this.btnToolPaste_Click);

            //
            // btnToolDelete
            //
            this.btnToolDelete.SetLocation(new Point(2, 83));
            this.btnToolDelete.SetName("btnToolDelete");
            this.btnToolDelete.Size = new Size(34, 34);
            this.btnToolDelete.TabIndex = 2;
            this.btnToolDelete.Text = "刪除";
            this.btnToolDelete.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolDelete, "刪除 (Del)");
            this.btnToolDelete.Click += new System.EventHandler(this.btnToolDelete_Click);

            //
            // btnToolUndo
            //
            this.btnToolUndo.SetLocation(new Point(2, 132));
            this.btnToolUndo.SetName("btnToolUndo");
            this.btnToolUndo.Size = new Size(34, 34);
            this.btnToolUndo.TabIndex = 3;
            this.btnToolUndo.Text = "復原";
            this.btnToolUndo.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolUndo, "復原 (Ctrl+Z)");
            this.btnToolUndo.Click += new System.EventHandler(this.btnToolUndo_Click);

            //
            // btnToolRedo
            //
            this.btnToolRedo.SetLocation(new Point(2, 171));
            this.btnToolRedo.SetName("btnToolRedo");
            this.btnToolRedo.Size = new Size(34, 34);
            this.btnToolRedo.TabIndex = 4;
            this.btnToolRedo.Text = "重做";
            this.btnToolRedo.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolRedo, "重做 (Ctrl+Y)");
            this.btnToolRedo.Click += new System.EventHandler(this.btnToolRedo_Click);

            //
            // btnToolSave
            //
            this.btnToolSave.SetLocation(new Point(2, 220));
            this.btnToolSave.SetName("btnToolSave");
            this.btnToolSave.Size = new Size(34, 34);
            this.btnToolSave.TabIndex = 5;
            this.btnToolSave.Text = "儲存";
            this.btnToolSave.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolSave, "儲存 (Ctrl+S)");
            this.btnToolSave.Click += new System.EventHandler(this.btnToolSave_Click);

            //
            // btnToolCellInfo
            //
            this.btnToolCellInfo.SetLocation(new Point(2, 269));
            this.btnToolCellInfo.SetName("btnToolCellInfo");
            this.btnToolCellInfo.Size = new Size(34, 34);
            this.btnToolCellInfo.TabIndex = 6;
            this.btnToolCellInfo.Text = "詳細";
            this.btnToolCellInfo.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolCellInfo, "格子詳細資訊");
            this.btnToolCellInfo.Click += new System.EventHandler(this.btnToolCellInfo_Click);

            //
            // btnToolReplaceTile
            //
            this.btnToolReplaceTile.SetLocation(new Point(2, 318));
            this.btnToolReplaceTile.SetName("btnToolReplaceTile");
            this.btnToolReplaceTile.Size = new Size(34, 34);
            this.btnToolReplaceTile.TabIndex = 7;
            this.btnToolReplaceTile.Text = "替換";
            this.btnToolReplaceTile.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolReplaceTile, "批次替換地板");
            this.btnToolReplaceTile.Click += new System.EventHandler(this.btnToolReplaceTile_Click);

            //
            // btnToolAddS32
            //
            this.btnToolAddS32.SetLocation(new Point(2, 367));
            this.btnToolAddS32.SetName("btnToolAddS32");
            this.btnToolAddS32.Size = new Size(34, 34);
            this.btnToolAddS32.TabIndex = 8;
            this.btnToolAddS32.Text = "新增";
            this.btnToolAddS32.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolAddS32, "新增 S32 區塊");
            this.btnToolAddS32.Click += new System.EventHandler(this.btnToolAddS32_Click);

            //
            // btnToolClearLayer7
            //
            this.btnToolClearLayer7.SetLocation(new Point(2, 405));
            this.btnToolClearLayer7.SetName("btnToolClearLayer7");
            this.btnToolClearLayer7.Size = new Size(34, 34);
            this.btnToolClearLayer7.TabIndex = 9;
            this.btnToolClearLayer7.Text = "清L7";
            this.btnToolClearLayer7.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolClearLayer7, "清除所有第七層（傳送點）資料");
            this.btnToolClearLayer7.Click += new System.EventHandler(this.btnToolClearLayer7_Click);

            //
            // btnToolClearCell
            //
            this.btnToolClearCell.SetLocation(new Point(2, 443));
            this.btnToolClearCell.SetName("btnToolClearCell");
            this.btnToolClearCell.Size = new Size(34, 34);
            this.btnToolClearCell.TabIndex = 10;
            this.btnToolClearCell.Text = "清格";
            this.btnToolClearCell.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolClearCell, "清除指定格子的各層資料");
            this.btnToolClearCell.Click += new System.EventHandler(this.btnToolClearCell_Click);

            //
            // btnMapValidate
            //
            this.btnMapValidate.SetLocation(new Point(2, 481));
            this.btnMapValidate.SetName("btnMapValidate");
            this.btnMapValidate.Size = new Size(34, 34);
            this.btnMapValidate.TabIndex = 13;
            this.btnMapValidate.Text = "⚠";
            this.btnMapValidate.TextColor = Colors.Red;
            this.btnMapValidate.Font = new Font(this.btnMapValidate.Font.FamilyName, 14, FontStyle.Bold);
            this.btnMapValidate.SetUseVisualStyleBackColor(true);
            this.btnMapValidate.Visible = false;  // 預設隱藏，有異常時才顯示
            this.toolTip1.SetToolTip(this.btnMapValidate, "地圖驗證 (L5/Tile/L8 異常檢查)");
            this.btnMapValidate.Click += new System.EventHandler(this.btnMapValidate_Click);

            //
            // btnToolCheckL1
            //
            this.btnToolCheckL1.SetLocation(new Point(2, 2));
            this.btnToolCheckL1.SetName("btnToolCheckL1");
            this.btnToolCheckL1.Size = new Size(34, 34);
            this.btnToolCheckL1.TabIndex = 11;
            this.btnToolCheckL1.Text = "查L1";
            this.btnToolCheckL1.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolCheckL1, "查看與編輯第一層（地板圖塊）資料");
            this.btnToolCheckL1.Click += new System.EventHandler(this.btnToolCheckL1_Click);

            //
            // btnToolCheckL2
            //
            this.btnToolCheckL2.SetLocation(new Point(2, 40));
            this.btnToolCheckL2.SetName("btnToolCheckL2");
            this.btnToolCheckL2.Size = new Size(34, 34);
            this.btnToolCheckL2.TabIndex = 11;
            this.btnToolCheckL2.Text = "查L2";
            this.btnToolCheckL2.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolCheckL2, "查看與清除第二層資料");
            this.btnToolCheckL2.Click += new System.EventHandler(this.btnToolCheckL2_Click);

            //
            // btnToolCheckL3
            //
            this.btnToolCheckL3.SetLocation(new Point(2, 78));
            this.btnToolCheckL3.SetName("btnToolCheckL3");
            this.btnToolCheckL3.Size = new Size(34, 34);
            this.btnToolCheckL3.TabIndex = 19;
            this.btnToolCheckL3.Text = "查L3";
            this.btnToolCheckL3.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolCheckL3, "查看第三層（屬性）資料");
            this.btnToolCheckL3.Click += new System.EventHandler(this.btnToolCheckL3_Click);

            //
            // btnToolCheckL4
            //
            this.btnToolCheckL4.SetLocation(new Point(2, 116));
            this.btnToolCheckL4.SetName("btnToolCheckL4");
            this.btnToolCheckL4.Size = new Size(34, 34);
            this.btnToolCheckL4.TabIndex = 11;
            this.btnToolCheckL4.Text = "查L4";
            this.btnToolCheckL4.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolCheckL4, "查看與編輯第四層（物件）資料");
            this.btnToolCheckL4.Click += new System.EventHandler(this.btnToolCheckL4_Click);

            //
            // btnToolCheckL5
            //
            this.btnToolCheckL5.SetLocation(new Point(2, 154));
            this.btnToolCheckL5.SetName("btnToolCheckL5");
            this.btnToolCheckL5.Size = new Size(34, 34);
            this.btnToolCheckL5.TabIndex = 11;
            this.btnToolCheckL5.Text = "查L5";
            this.btnToolCheckL5.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolCheckL5, "查看與管理第五層（透明圖塊）資料");
            this.btnToolCheckL5.Click += new System.EventHandler(this.btnToolCheckL5_Click);

            //
            // btnToolCheckL6
            //
            this.btnToolCheckL6.SetLocation(new Point(2, 192));
            this.btnToolCheckL6.SetName("btnToolCheckL6");
            this.btnToolCheckL6.Size = new Size(34, 34);
            this.btnToolCheckL6.TabIndex = 12;
            this.btnToolCheckL6.Text = "查L6";
            this.btnToolCheckL6.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolCheckL6, "查看與管理第六層（使用的TileId）資料");
            this.btnToolCheckL6.Click += new System.EventHandler(this.btnToolCheckL6_Click);

            //
            // btnToolCheckL7
            //
            this.btnToolCheckL7.SetLocation(new Point(2, 230));
            this.btnToolCheckL7.SetName("btnToolCheckL7");
            this.btnToolCheckL7.Size = new Size(34, 34);
            this.btnToolCheckL7.TabIndex = 13;
            this.btnToolCheckL7.Text = "查L7";
            this.btnToolCheckL7.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolCheckL7, "查看與編輯第七層（傳送點）資料");
            this.btnToolCheckL7.Click += new System.EventHandler(this.btnToolCheckL7_Click);

            //
            // btnToolCheckL8
            //
            this.btnToolCheckL8.SetLocation(new Point(2, 268));
            this.btnToolCheckL8.SetName("btnToolCheckL8");
            this.btnToolCheckL8.Size = new Size(34, 34);
            this.btnToolCheckL8.TabIndex = 14;
            this.btnToolCheckL8.Text = "查L8";
            this.btnToolCheckL8.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolCheckL8, "查看哪些S32有第八層（特效）資料");
            this.btnToolCheckL8.Click += new System.EventHandler(this.btnToolCheckL8_Click);

            //
            // btnEnableVisibleL8
            //
            this.btnEnableVisibleL8.SetLocation(new Point(2, 304));
            this.btnEnableVisibleL8.SetName("btnEnableVisibleL8");
            this.btnEnableVisibleL8.Size = new Size(34, 34);
            this.btnEnableVisibleL8.TabIndex = 15;
            this.btnEnableVisibleL8.Text = "啟L8";
            this.btnEnableVisibleL8.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnEnableVisibleL8, "啟用畫面中所有可見的 L8 特效");
            this.btnEnableVisibleL8.Click += new System.EventHandler(this.btnEnableVisibleL8_Click);

            //
            // btnViewClipboard
            //
            this.btnViewClipboard.SetLocation(new Point(2, 340));
            this.btnViewClipboard.SetName("btnViewClipboard");
            this.btnViewClipboard.Size = new Size(34, 34);
            this.btnViewClipboard.TabIndex = 16;
            this.btnViewClipboard.Text = "剪貼";
            this.btnViewClipboard.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnViewClipboard, "查看剪貼簿內容 (Ctrl+Shift+C)");
            this.btnViewClipboard.Click += new System.EventHandler(this.btnViewClipboard_Click);

            //
            // btnToolTestTil
            //
            this.btnToolTestTil.SetLocation(new Point(2, 378));
            this.btnToolTestTil.SetName("btnToolTestTil");
            this.btnToolTestTil.Size = new Size(34, 34);
            this.btnToolTestTil.TabIndex = 17;
            this.btnToolTestTil.Text = "測til";
            this.btnToolTestTil.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolTestTil, "測試外部 til 檔案（暫時替換顯示）");
            this.btnToolTestTil.Click += new System.EventHandler(this.btnToolTestTil_Click);

            //
            // btnToolClearTestTil
            //
            this.btnToolClearTestTil.SetLocation(new Point(2, 416));
            this.btnToolClearTestTil.SetName("btnToolClearTestTil");
            this.btnToolClearTestTil.Size = new Size(34, 34);
            this.btnToolClearTestTil.TabIndex = 18;
            this.btnToolClearTestTil.Text = "清til";
            this.btnToolClearTestTil.SetUseVisualStyleBackColor(true);
            this.toolTip1.SetToolTip(this.btnToolClearTestTil, "清除測 til（恢復正常顯示）");
            this.btnToolClearTestTil.Click += new System.EventHandler(this.btnToolClearTestTil_Click);

            //
            // MapForm
            //
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1400, 700);
            this.GetControls().Add(this.toolbarContainer);
            this.GetControls().Add(this.rightPanel);
            this.GetControls().Add(this.tabControl1);
            this.GetControls().Add(this.leftPanel);
            this.GetControls().Add(this.statusStrip1);
            // Note: menuStrip1 is set via MainMenuStrip property, not added to Controls
            this.MainMenuStrip = this.menuStrip1;
            this.SetName("MapForm");
            this.Text = "地圖編輯器";
            this.Load += new System.EventHandler(this.MapForm_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.leftPanel.ResumeLayout(false);
            this.leftPanel.PerformLayout();
            ((ISupportInitialize)this.miniMapPictureBox).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabS32Editor.ResumeLayout(false);
            this.s32EditorPanel.ResumeLayout(false);
            this.s32LayerControlPanel.ResumeLayout(false);
            this.s32LayerControlPanel.PerformLayout();
            this.s32MapPanel.ResumeLayout(false);
            ((ISupportInitialize)this.pictureBox4).EndInit();
            ((ISupportInitialize)this.pictureBox3).EndInit();
            ((ISupportInitialize)this.pictureBox2).EndInit();
            ((ISupportInitialize)this.pictureBox1).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
