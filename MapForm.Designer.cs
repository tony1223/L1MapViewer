using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using L1MapViewer.Controls;
using L1MapViewer.Other;

namespace L1FlyMapViewer
{
    partial class MapForm
    {
        private IContainer components;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem openToolStripMenuItem;
        private ToolStripMenuItem exportToolStripMenuItem;
        private ToolStripMenuItem exportL1JToolStripMenuItem;
        private ToolStripMenuItem exportDIRToolStripMenuItem;
        private ToolStripMenuItem importMaterialToolStripMenuItem;
        private ToolStripMenuItem importFs32ToNewMapToolStripMenuItem;
        private ToolStripMenuItem discordToolStripMenuItem;
        private ToolStripMenuItem languageToolStripMenuItem;
        private ToolStripMenuItem langZhTWToolStripMenuItem;
        private ToolStripMenuItem langJaJPToolStripMenuItem;
        private ToolStripMenuItem langEnUSToolStripMenuItem;
        private StatusStrip statusStrip1;
        public ToolStripStatusLabel toolStripStatusLabel1;
        public ToolStripProgressBar toolStripProgressBar1;
        public ToolStripStatusLabel toolStripStatusLabel2;
        public ToolStripStatusLabel toolStripStatusLabel3;
        private ToolStripStatusLabel toolStripJumpLabel;
        private ToolStripTextBox toolStripJumpTextBox;
        private ToolStripButton toolStripJumpButton;
        private ToolStripButton toolStripCopyMoveCmd;

        // 左側面板
        private Panel leftPanel;
        public ComboBox comboBox1;  // 保留給介面相容性，但隱藏
        private PictureBox miniMapPictureBox;

        // 左下角 TabControl（地圖列表 / S32 檔案清單）
        private TabControl leftTabControl;
        private TabPage tabMapList;
        private TabPage tabS32Files;
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
        private ListView lvTiles;
        private Label lblMaterials;
        private ListView lvMaterials;
        private Button btnMoreMaterials;
        private Label lblGroupThumbnails;
        private Button btnShowAllGroups;
        private ListView lvGroupThumbnails;

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
        private Button btnToolCheckL5Invalid;
        private Button btnToolCheckL1;
        private Button btnToolCheckL2;
        private Button btnToolCheckL4;
        private Button btnToolCheckL5;
        private Button btnToolCheckL6;
        private Button btnToolCheckL7;
        private Button btnToolCheckL8;
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
        private CheckBox chkFloatLayer8;
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
        private Button btnSetPassable;
        private Button btnSetImpassable;
        private Button btnEditLayer5;
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
            this.components = new Container();

            // MenuStrip
            this.menuStrip1 = new MenuStrip();
            this.openToolStripMenuItem = new ToolStripMenuItem();
            this.exportToolStripMenuItem = new ToolStripMenuItem();
            this.exportL1JToolStripMenuItem = new ToolStripMenuItem();
            this.exportDIRToolStripMenuItem = new ToolStripMenuItem();
            this.importMaterialToolStripMenuItem = new ToolStripMenuItem();
            this.importFs32ToNewMapToolStripMenuItem = new ToolStripMenuItem();
            this.discordToolStripMenuItem = new ToolStripMenuItem();
            this.languageToolStripMenuItem = new ToolStripMenuItem();
            this.langZhTWToolStripMenuItem = new ToolStripMenuItem();
            this.langJaJPToolStripMenuItem = new ToolStripMenuItem();
            this.langEnUSToolStripMenuItem = new ToolStripMenuItem();

            // StatusStrip
            this.statusStrip1 = new StatusStrip();
            this.toolStripStatusLabel1 = new ToolStripStatusLabel();
            this.toolStripStatusLabel2 = new ToolStripStatusLabel();
            this.toolStripStatusLabel3 = new ToolStripStatusLabel();
            this.toolStripProgressBar1 = new ToolStripProgressBar();
            this.toolStripJumpLabel = new ToolStripStatusLabel();
            this.toolStripJumpTextBox = new ToolStripTextBox();
            this.toolStripJumpButton = new ToolStripButton();
            this.toolStripCopyMoveCmd = new ToolStripButton();

            // 左側面板
            this.leftPanel = new Panel();
            this.comboBox1 = new ComboBox();
            this.miniMapPictureBox = new PictureBox();

            // 左下角 TabControl
            this.leftTabControl = new TabControl();
            this.tabMapList = new TabPage();
            this.tabS32Files = new TabPage();
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
            this.lvTiles = new ListView();
            this.lblMaterials = new Label();
            this.lvMaterials = new ListView();
            this.btnMoreMaterials = new Button();
            this.lblGroupThumbnails = new Label();
            this.btnShowAllGroups = new Button();
            this.lvGroupThumbnails = new ListView();

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
            this.btnToolCheckL5Invalid = new Button();
            this.btnToolCheckL1 = new Button();
            this.btnToolCheckL2 = new Button();
            this.btnToolCheckL4 = new Button();
            this.btnToolCheckL5 = new Button();
            this.btnToolCheckL6 = new Button();
            this.btnToolCheckL7 = new Button();
            this.btnToolCheckL8 = new Button();
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
            this.btnSetPassable = new Button();
            this.btnSetImpassable = new Button();
            this.btnEditLayer5 = new Button();
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
            this.chkFloatLayer8 = new CheckBox();
            this.chkShowLayer5 = new CheckBox();

            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.leftPanel.SuspendLayout();
            this.leftTabControl.SuspendLayout();
            this.tabMapList.SuspendLayout();
            this.tabS32Files.SuspendLayout();
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
            // menuStrip1
            //
            this.menuStrip1.Items.AddRange(new ToolStripItem[] {
                this.openToolStripMenuItem,
                this.importMaterialToolStripMenuItem,
                this.importFs32ToNewMapToolStripMenuItem,
                this.exportToolStripMenuItem,
                this.discordToolStripMenuItem,
                this.languageToolStripMenuItem
            });
            this.menuStrip1.Location = new Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new Size(1200, 24);
            this.menuStrip1.TabIndex = 0;

            //
            // openToolStripMenuItem
            //
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new Size(164, 20);
            this.openToolStripMenuItem.Text = "開啟天堂客戶端讀取地圖";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);

            //
            // importMaterialToolStripMenuItem
            //
            this.importMaterialToolStripMenuItem.Name = "importMaterialToolStripMenuItem";
            this.importMaterialToolStripMenuItem.Size = new Size(86, 20);
            this.importMaterialToolStripMenuItem.Text = "匯入素材...";
            this.importMaterialToolStripMenuItem.Click += new System.EventHandler(this.importMaterialToolStripMenuItem_Click);

            //
            // importFs32ToNewMapToolStripMenuItem
            //
            this.importFs32ToNewMapToolStripMenuItem.Name = "importFs32ToNewMapToolStripMenuItem";
            this.importFs32ToNewMapToolStripMenuItem.Size = new Size(140, 20);
            this.importFs32ToNewMapToolStripMenuItem.Text = "匯入地圖包到新地圖...";
            this.importFs32ToNewMapToolStripMenuItem.Click += new System.EventHandler(this.importFs32ToNewMapToolStripMenuItem_Click);

            //
            // exportToolStripMenuItem (下拉選單)
            //
            this.exportToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
                this.exportL1JToolStripMenuItem,
                this.exportDIRToolStripMenuItem
            });
            this.exportToolStripMenuItem.Name = "exportToolStripMenuItem";
            this.exportToolStripMenuItem.Size = new Size(130, 20);
            this.exportToolStripMenuItem.Text = "輸出伺服器通行txt";

            //
            // exportL1JToolStripMenuItem
            //
            this.exportL1JToolStripMenuItem.Name = "exportL1JToolStripMenuItem";
            this.exportL1JToolStripMenuItem.Size = new Size(100, 22);
            this.exportL1JToolStripMenuItem.Text = "L1J 格式";
            this.exportL1JToolStripMenuItem.Click += new System.EventHandler(this.exportL1JToolStripMenuItem_Click);

            //
            // exportDIRToolStripMenuItem
            //
            this.exportDIRToolStripMenuItem.Name = "exportDIRToolStripMenuItem";
            this.exportDIRToolStripMenuItem.Size = new Size(100, 22);
            this.exportDIRToolStripMenuItem.Text = "DIR 格式";
            this.exportDIRToolStripMenuItem.Click += new System.EventHandler(this.exportDIRToolStripMenuItem_Click);

            //
            // discordToolStripMenuItem
            //
            this.discordToolStripMenuItem.Name = "discordToolStripMenuItem";
            this.discordToolStripMenuItem.Size = new Size(100, 20);
            this.discordToolStripMenuItem.Text = "到 Discord 討論";
            this.discordToolStripMenuItem.Click += new System.EventHandler(this.discordToolStripMenuItem_Click);

            //
            // languageToolStripMenuItem
            //
            this.languageToolStripMenuItem.Name = "languageToolStripMenuItem";
            this.languageToolStripMenuItem.Size = new Size(54, 20);
            this.languageToolStripMenuItem.Text = "Language";
            this.languageToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
                this.langZhTWToolStripMenuItem,
                this.langJaJPToolStripMenuItem,
                this.langEnUSToolStripMenuItem
            });

            //
            // langZhTWToolStripMenuItem
            //
            this.langZhTWToolStripMenuItem.Name = "langZhTWToolStripMenuItem";
            this.langZhTWToolStripMenuItem.Size = new Size(120, 22);
            this.langZhTWToolStripMenuItem.Text = "繁體中文";
            this.langZhTWToolStripMenuItem.Tag = "zh-TW";
            this.langZhTWToolStripMenuItem.Click += new System.EventHandler(this.LanguageMenuItem_Click);

            //
            // langJaJPToolStripMenuItem
            //
            this.langJaJPToolStripMenuItem.Name = "langJaJPToolStripMenuItem";
            this.langJaJPToolStripMenuItem.Size = new Size(120, 22);
            this.langJaJPToolStripMenuItem.Text = "日本語";
            this.langJaJPToolStripMenuItem.Tag = "ja-JP";
            this.langJaJPToolStripMenuItem.Click += new System.EventHandler(this.LanguageMenuItem_Click);

            //
            // langEnUSToolStripMenuItem
            //
            this.langEnUSToolStripMenuItem.Name = "langEnUSToolStripMenuItem";
            this.langEnUSToolStripMenuItem.Size = new Size(120, 22);
            this.langEnUSToolStripMenuItem.Text = "English";
            this.langEnUSToolStripMenuItem.Tag = "en-US";
            this.langEnUSToolStripMenuItem.Click += new System.EventHandler(this.LanguageMenuItem_Click);

            //
            // statusStrip1
            //
            this.statusStrip1.Items.AddRange(new ToolStripItem[] {
                this.toolStripStatusLabel1,
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
            this.statusStrip1.Location = new Point(0, 678);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new Size(1200, 22);
            this.statusStrip1.TabIndex = 1;

            //
            // toolStripStatusLabel1
            //
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new Size(400, 17);
            this.toolStripStatusLabel1.Text = "点击获取坐标 | Ctrl+拖拽選择範圍 | Ctrl+滾輪縮放 | S32編輯器:Ctrl+左鍵刪除單格 | Shift+拖拽批量刪除區域";

            //
            // toolStripStatusLabel2
            //
            this.toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            this.toolStripStatusLabel2.Size = new Size(100, 17);

            //
            // toolStripStatusLabel3
            //
            this.toolStripStatusLabel3.Name = "toolStripStatusLabel3";
            this.toolStripStatusLabel3.Size = new Size(885, 17);
            this.toolStripStatusLabel3.Spring = true;

            //
            // toolStripProgressBar1
            //
            this.toolStripProgressBar1.Name = "toolStripProgressBar1";
            this.toolStripProgressBar1.Size = new Size(100, 16);
            this.toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
            this.toolStripProgressBar1.Visible = false;

            //
            // toolStripJumpLabel
            //
            this.toolStripJumpLabel.Name = "toolStripJumpLabel";
            this.toolStripJumpLabel.Size = new Size(60, 17);
            this.toolStripJumpLabel.Text = "跳轉座標:";

            //
            // toolStripJumpTextBox
            //
            this.toolStripJumpTextBox.Name = "toolStripJumpTextBox";
            this.toolStripJumpTextBox.Size = new Size(100, 22);
            this.toolStripJumpTextBox.ToolTipText = "輸入座標 (X,Y) 然後按 Enter 或點擊跳轉";
            this.toolStripJumpTextBox.KeyDown += new KeyEventHandler(this.toolStripJumpTextBox_KeyDown);

            //
            // toolStripJumpButton
            //
            this.toolStripJumpButton.Name = "toolStripJumpButton";
            this.toolStripJumpButton.Size = new Size(35, 20);
            this.toolStripJumpButton.Text = "Go";
            this.toolStripJumpButton.Click += new System.EventHandler(this.toolStripJumpButton_Click);

            //
            // toolStripCopyMoveCmd
            //
            this.toolStripCopyMoveCmd.Name = "toolStripCopyMoveCmd";
            this.toolStripCopyMoveCmd.Size = new Size(120, 20);
            this.toolStripCopyMoveCmd.Text = "複製移動指令";
            this.toolStripCopyMoveCmd.ToolTipText = "複製 移動 x y 地圖id 指令到剪貼簿";
            this.toolStripCopyMoveCmd.Enabled = false;
            this.toolStripCopyMoveCmd.Click += new System.EventHandler(this.toolStripCopyMoveCmd_Click);

            //
            // leftPanel
            //
            this.leftPanel.BorderStyle = BorderStyle.FixedSingle;
            this.leftPanel.Controls.Add(this.comboBox1);
            this.leftPanel.Controls.Add(this.miniMapPictureBox);
            this.leftPanel.Controls.Add(this.leftTabControl);
            this.leftPanel.Dock = DockStyle.Left;
            this.leftPanel.Location = new Point(0, 24);
            this.leftPanel.Name = "leftPanel";
            this.leftPanel.Size = new Size(280, 654);
            this.leftPanel.TabIndex = 2;

            //
            // comboBox1 (隱藏，保留給介面相容性)
            //
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new Point(10, 10);
            this.comboBox1.Name = "comboBox1";
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
            this.miniMapPictureBox.BackColor = Color.Black;
            this.miniMapPictureBox.BorderStyle = BorderStyle.FixedSingle;
            this.miniMapPictureBox.Location = new Point(5, 5);
            this.miniMapPictureBox.Name = "miniMapPictureBox";
            this.miniMapPictureBox.Size = new Size(268, 268);
            this.miniMapPictureBox.SizeMode = PictureBoxSizeMode.Normal;
            this.miniMapPictureBox.TabIndex = 1;
            this.miniMapPictureBox.TabStop = true;
            this.miniMapPictureBox.Cursor = Cursors.Hand;
            this.miniMapPictureBox.MouseDown += new MouseEventHandler(this.miniMapPictureBox_MouseDown);
            this.miniMapPictureBox.MouseMove += new MouseEventHandler(this.miniMapPictureBox_MouseMove);
            this.miniMapPictureBox.MouseUp += new MouseEventHandler(this.miniMapPictureBox_MouseUp);
            this.miniMapPictureBox.MouseClick += new MouseEventHandler(this.miniMapPictureBox_MouseClick);
            this.miniMapPictureBox.PreviewKeyDown += new PreviewKeyDownEventHandler(this.miniMapPictureBox_PreviewKeyDown);
            this.miniMapPictureBox.KeyDown += new KeyEventHandler(this.miniMapPictureBox_KeyDown);

            //
            // leftTabControl
            //
            this.leftTabControl.Controls.Add(this.tabMapList);
            this.leftTabControl.Controls.Add(this.tabS32Files);
            this.leftTabControl.Location = new Point(5, 280);
            this.leftTabControl.Name = "leftTabControl";
            this.leftTabControl.SelectedIndex = 0;
            this.leftTabControl.Size = new Size(268, 365);
            this.leftTabControl.TabIndex = 2;

            //
            // tabMapList (地圖列表)
            //
            this.tabMapList.Controls.Add(this.txtMapSearch);
            this.tabMapList.Controls.Add(this.lstMaps);
            this.tabMapList.Location = new Point(4, 24);
            this.tabMapList.Name = "tabMapList";
            this.tabMapList.Padding = new Padding(3);
            this.tabMapList.Size = new Size(260, 337);
            this.tabMapList.TabIndex = 0;
            this.tabMapList.Text = "地圖列表";
            this.tabMapList.UseVisualStyleBackColor = true;

            //
            // txtMapSearch
            //
            this.txtMapSearch.Location = new Point(3, 3);
            this.txtMapSearch.Name = "txtMapSearch";
            this.txtMapSearch.Size = new Size(254, 23);
            this.txtMapSearch.TabIndex = 0;
            this.txtMapSearch.PlaceholderText = "搜尋地圖 (ID 或名稱)...";
            this.txtMapSearch.TextChanged += new System.EventHandler(this.txtMapSearch_TextChanged);

            //
            // lstMaps (地圖列表 ListBox)
            //
            this.lstMaps.Location = new Point(3, 30);
            this.lstMaps.Name = "lstMaps";
            this.lstMaps.Size = new Size(254, 304);
            this.lstMaps.TabIndex = 1;
            this.lstMaps.SelectedIndexChanged += new System.EventHandler(this.lstMaps_SelectedIndexChanged);
            this.lstMaps.MouseUp += new System.Windows.Forms.MouseEventHandler(this.lstMaps_MouseUp);

            //
            // tabS32Files (S32 檔案清單)
            //
            this.tabS32Files.Controls.Add(this.lblS32Files);
            this.tabS32Files.Controls.Add(this.btnS32SelectAll);
            this.tabS32Files.Controls.Add(this.btnS32SelectNone);
            this.tabS32Files.Controls.Add(this.lstS32Files);
            this.tabS32Files.Location = new Point(4, 24);
            this.tabS32Files.Name = "tabS32Files";
            this.tabS32Files.Padding = new Padding(3);
            this.tabS32Files.Size = new Size(260, 337);
            this.tabS32Files.TabIndex = 1;
            this.tabS32Files.Text = "S32 檔案";
            this.tabS32Files.UseVisualStyleBackColor = true;

            //
            // lblS32Files
            //
            this.lblS32Files.Location = new Point(3, 3);
            this.lblS32Files.Name = "lblS32Files";
            this.lblS32Files.Size = new Size(100, 20);
            this.lblS32Files.TabIndex = 2;
            this.lblS32Files.Text = "S32 檔案清單";
            this.lblS32Files.TextAlign = ContentAlignment.MiddleLeft;

            //
            // btnS32SelectAll
            //
            this.btnS32SelectAll.Location = new Point(150, 3);
            this.btnS32SelectAll.Name = "btnS32SelectAll";
            this.btnS32SelectAll.Size = new Size(50, 20);
            this.btnS32SelectAll.TabIndex = 20;
            this.btnS32SelectAll.Text = "全選";
            this.btnS32SelectAll.Click += new System.EventHandler(this.btnS32SelectAll_Click);

            //
            // btnS32SelectNone
            //
            this.btnS32SelectNone.Location = new Point(205, 3);
            this.btnS32SelectNone.Name = "btnS32SelectNone";
            this.btnS32SelectNone.Size = new Size(50, 20);
            this.btnS32SelectNone.TabIndex = 21;
            this.btnS32SelectNone.Text = "全不選";
            this.btnS32SelectNone.Click += new System.EventHandler(this.btnS32SelectNone_Click);

            //
            // lstS32Files
            //
            this.lstS32Files.Location = new Point(3, 26);
            this.lstS32Files.Name = "lstS32Files";
            this.lstS32Files.Size = new Size(254, 305);
            this.lstS32Files.TabIndex = 3;
            this.lstS32Files.CheckOnClick = false;
            this.lstS32Files.DrawMode = DrawMode.OwnerDrawFixed;
            this.lstS32Files.SelectedIndexChanged += new System.EventHandler(this.lstS32Files_SelectedIndexChanged);
            this.lstS32Files.ItemCheck += new ItemCheckEventHandler(this.lstS32Files_ItemCheck);
            this.lstS32Files.MouseUp += new MouseEventHandler(this.lstS32Files_MouseUp);
            this.lstS32Files.DrawItem += new DrawItemEventHandler(this.lstS32Files_DrawItem);

            //
            // tabControl1
            //
            this.tabControl1.Controls.Add(this.tabS32Editor);
            this.tabControl1.Location = new Point(290, 34);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;  // S32 編輯器
            this.tabControl1.Size = new Size(710, 640);
            this.tabControl1.TabIndex = 3;

            //
            // tabMapPreview
            //
            this.tabMapPreview.BackColor = Color.Black;
            this.tabMapPreview.Controls.Add(this.panel1);
            this.tabMapPreview.Controls.Add(this.vScrollBar1);
            this.tabMapPreview.Controls.Add(this.hScrollBar1);
            this.tabMapPreview.Location = new Point(4, 22);
            this.tabMapPreview.Name = "tabMapPreview";
            this.tabMapPreview.Padding = new Padding(3);
            this.tabMapPreview.Size = new Size(702, 614);
            this.tabMapPreview.TabIndex = 0;
            this.tabMapPreview.Text = "地圖預覽";

            //
            // tabS32Editor
            //
            this.tabS32Editor.BackColor = Color.DarkGray;
            this.tabS32Editor.Controls.Add(this.s32EditorPanel);
            this.tabS32Editor.Location = new Point(4, 22);
            this.tabS32Editor.Name = "tabS32Editor";
            this.tabS32Editor.Padding = new Padding(3);
            this.tabS32Editor.Size = new Size(702, 614);
            this.tabS32Editor.TabIndex = 1;
            this.tabS32Editor.Text = "S32 編輯器";

            //
            // panel1 (地圖顯示區域)
            //
            this.panel1.BackColor = Color.Black;
            this.panel1.BorderStyle = BorderStyle.None;
            this.panel1.Controls.Add(this.pictureBox4);
            this.panel1.Controls.Add(this.pictureBox3);
            this.panel1.Controls.Add(this.pictureBox2);
            this.panel1.Controls.Add(this.pictureBox1);
            this.panel1.Location = new Point(3, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new Size(679, 591);
            this.panel1.TabIndex = 0;

            //
            // pictureBox4
            //
            this.pictureBox4.BackColor = Color.Transparent;
            this.pictureBox4.Dock = DockStyle.Fill;
            this.pictureBox4.Location = new Point(0, 0);
            this.pictureBox4.Name = "pictureBox4";
            this.pictureBox4.Size = new Size(panel1.Width, panel1.Height);
            this.pictureBox4.TabIndex = 3;
            this.pictureBox4.TabStop = false;

            //
            // pictureBox3
            //
            this.pictureBox3.BackColor = Color.Transparent;
            this.pictureBox3.Dock = DockStyle.Fill;
            this.pictureBox3.Location = new Point(0, 0);
            this.pictureBox3.Name = "pictureBox3";
            this.pictureBox3.Size = new Size(panel1.Width, panel1.Height);
            this.pictureBox3.TabIndex = 2;
            this.pictureBox3.TabStop = false;

            //
            // pictureBox2
            //
            this.pictureBox2.BackColor = Color.Transparent;
            this.pictureBox2.Dock = DockStyle.Fill;
            this.pictureBox2.Location = new Point(0, 0);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new Size(panel1.Width, panel1.Height);
            this.pictureBox2.TabIndex = 1;
            this.pictureBox2.TabStop = false;
            this.pictureBox2.Paint += new PaintEventHandler(this.pictureBox2_Paint);
            this.pictureBox2.MouseDown += new MouseEventHandler(this.pictureBox2_MouseDown);
            this.pictureBox2.MouseMove += new MouseEventHandler(this.pictureBox2_MouseMove);
            this.pictureBox2.MouseUp += new MouseEventHandler(this.pictureBox2_MouseUp);

            //
            // pictureBox1
            //
            this.pictureBox1.Location = new Point(3, 3);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new Size(100, 50);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;

            //
            // vScrollBar1
            //
            this.vScrollBar1.Location = new Point(682, 3);
            this.vScrollBar1.Name = "vScrollBar1";
            this.vScrollBar1.Size = new Size(17, 591);
            this.vScrollBar1.TabIndex = 1;
            this.vScrollBar1.Scroll += new ScrollEventHandler(this.vScrollBar1_Scroll);

            //
            // hScrollBar1
            //
            this.hScrollBar1.Location = new Point(3, 594);
            this.hScrollBar1.Name = "hScrollBar1";
            this.hScrollBar1.Size = new Size(679, 17);
            this.hScrollBar1.TabIndex = 2;
            this.hScrollBar1.Scroll += new ScrollEventHandler(this.hScrollBar1_Scroll);

            //
            // s32EditorPanel
            //
            this.s32EditorPanel.BackColor = Color.White;
            this.s32EditorPanel.Controls.Add(this.layerFloatPanel);
            this.s32EditorPanel.Controls.Add(this.s32MapPanel);
            this.s32EditorPanel.Controls.Add(this.s32LayerControlPanel);
            this.s32EditorPanel.Controls.Add(this.lblS32Info);
            this.s32EditorPanel.Dock = DockStyle.Fill;
            this.s32EditorPanel.Location = new Point(3, 3);
            this.s32EditorPanel.Name = "s32EditorPanel";
            this.s32EditorPanel.Size = new Size(696, 608);
            this.s32EditorPanel.TabIndex = 0;
            this.s32EditorPanel.Resize += new System.EventHandler(this.s32EditorPanel_Resize);

            //
            // s32LayerControlPanel
            //
            this.s32LayerControlPanel.BackColor = Color.LightGray;
            this.s32LayerControlPanel.BorderStyle = BorderStyle.FixedSingle;
            this.s32LayerControlPanel.Controls.Add(this.btnCopySettings);
            this.s32LayerControlPanel.Controls.Add(this.btnCopyMapCoords);
            this.s32LayerControlPanel.Controls.Add(this.btnImportFs32);
            this.s32LayerControlPanel.Controls.Add(this.btnSetPassable);
            this.s32LayerControlPanel.Controls.Add(this.btnSetImpassable);
            this.s32LayerControlPanel.Controls.Add(this.btnEditLayer5);
            this.s32LayerControlPanel.Controls.Add(this.btnRegionEdit);
            this.s32LayerControlPanel.Controls.Add(this.btnSaveS32);
            this.s32LayerControlPanel.Controls.Add(this.btnReloadMap);
            this.s32LayerControlPanel.Controls.Add(this.btnAnalyzeAttr);
            this.s32LayerControlPanel.Dock = DockStyle.Top;
            this.s32LayerControlPanel.Location = new Point(0, 0);
            this.s32LayerControlPanel.Name = "s32LayerControlPanel";
            this.s32LayerControlPanel.Size = new Size(696, 65);
            this.s32LayerControlPanel.TabIndex = 0;

            //
            // chkLayer1
            //
            this.chkLayer1.AutoSize = true;
            this.chkLayer1.Checked = true;
            this.chkLayer1.CheckState = CheckState.Checked;
            this.chkLayer1.Location = new Point(10, 10);
            this.chkLayer1.Name = "chkLayer1";
            this.chkLayer1.Size = new Size(90, 17);
            this.chkLayer1.TabIndex = 0;
            this.chkLayer1.Text = "第1層 (地板)";
            this.chkLayer1.UseVisualStyleBackColor = true;
            this.chkLayer1.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkLayer2
            //
            this.chkLayer2.AutoSize = true;
            this.chkLayer2.Checked = true;
            this.chkLayer2.Location = new Point(110, 10);
            this.chkLayer2.Name = "chkLayer2";
            this.chkLayer2.Size = new Size(60, 17);
            this.chkLayer2.TabIndex = 1;
            this.chkLayer2.Text = "第2層";
            this.chkLayer2.UseVisualStyleBackColor = true;
            this.chkLayer2.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkLayer3
            //
            this.chkLayer3.AutoSize = true;
            this.chkLayer3.Location = new Point(180, 10);
            this.chkLayer3.Name = "chkLayer3";
            this.chkLayer3.Size = new Size(90, 17);
            this.chkLayer3.TabIndex = 2;
            this.chkLayer3.Text = "第3層 (多色屬性)";
            this.chkLayer3.UseVisualStyleBackColor = true;
            this.chkLayer3.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkLayer4
            //
            this.chkLayer4.AutoSize = true;
            this.chkLayer4.Checked = true;
            this.chkLayer4.CheckState = CheckState.Checked;
            this.chkLayer4.Location = new Point(280, 10);
            this.chkLayer4.Name = "chkLayer4";
            this.chkLayer4.Size = new Size(90, 17);
            this.chkLayer4.TabIndex = 3;
            this.chkLayer4.Text = "第4層 (物件)";
            this.chkLayer4.UseVisualStyleBackColor = true;
            this.chkLayer4.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkShowPassable
            //
            this.chkShowPassable.AutoSize = true;
            this.chkShowPassable.Location = new Point(380, 10);
            this.chkShowPassable.Name = "chkShowPassable";
            this.chkShowPassable.Size = new Size(100, 17);
            this.chkShowPassable.TabIndex = 4;
            this.chkShowPassable.Text = "通行性 (紅藍)";
            this.chkShowPassable.UseVisualStyleBackColor = true;
            this.chkShowPassable.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkShowGrid
            //
            this.chkShowGrid.AutoSize = true;
            this.chkShowGrid.Checked = true;
            this.chkShowGrid.CheckState = CheckState.Checked;
            this.chkShowGrid.Location = new Point(490, 10);
            this.chkShowGrid.Name = "chkShowGrid";
            this.chkShowGrid.Size = new Size(90, 17);
            this.chkShowGrid.TabIndex = 5;
            this.chkShowGrid.Text = "顯示格線";
            this.chkShowGrid.UseVisualStyleBackColor = true;
            this.chkShowGrid.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkShowS32Boundary
            //
            this.chkShowS32Boundary.AutoSize = true;
            this.chkShowS32Boundary.Location = new Point(570, 10);
            this.chkShowS32Boundary.Name = "chkShowS32Boundary";
            this.chkShowS32Boundary.Size = new Size(90, 17);
            this.chkShowS32Boundary.TabIndex = 7;
            this.chkShowS32Boundary.Text = "S32邊界";
            this.chkShowS32Boundary.UseVisualStyleBackColor = true;
            this.chkShowS32Boundary.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkShowLayer5
            //
            this.chkShowLayer5.Name = "chkShowLayer5";
            this.chkShowLayer5.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkShowSafeZones
            //
            this.chkShowSafeZones.AutoSize = true;
            this.chkShowSafeZones.Location = new Point(650, 10);
            this.chkShowSafeZones.Name = "chkShowSafeZones";
            this.chkShowSafeZones.Size = new Size(70, 17);
            this.chkShowSafeZones.TabIndex = 16;
            this.chkShowSafeZones.Text = "安全區域";
            this.chkShowSafeZones.UseVisualStyleBackColor = true;
            this.chkShowSafeZones.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // chkShowCombatZones
            //
            this.chkShowCombatZones.AutoSize = true;
            this.chkShowCombatZones.Location = new Point(730, 10);
            this.chkShowCombatZones.Name = "chkShowCombatZones";
            this.chkShowCombatZones.Size = new Size(70, 17);
            this.chkShowCombatZones.TabIndex = 17;
            this.chkShowCombatZones.Text = "戰鬥區域";
            this.chkShowCombatZones.UseVisualStyleBackColor = true;
            this.chkShowCombatZones.CheckedChanged += new System.EventHandler(this.S32Layer_CheckedChanged);

            //
            // btnCopySettings
            //
            this.btnCopySettings.Location = new Point(210, 5);
            this.btnCopySettings.Name = "btnCopySettings";
            this.btnCopySettings.Size = new Size(90, 25);
            this.btnCopySettings.TabIndex = 8;
            this.btnCopySettings.Text = "複製設定...";
            this.btnCopySettings.UseVisualStyleBackColor = true;
            this.btnCopySettings.Click += new System.EventHandler(this.btnCopySettings_Click);

            //
            // btnCopyMapCoords
            //
            this.btnCopyMapCoords.Location = new Point(310, 5);
            this.btnCopyMapCoords.Name = "btnCopyMapCoords";
            this.btnCopyMapCoords.Size = new Size(75, 25);
            this.btnCopyMapCoords.TabIndex = 13;
            this.btnCopyMapCoords.Text = "複製座標";
            this.btnCopyMapCoords.UseVisualStyleBackColor = true;
            this.btnCopyMapCoords.Click += new System.EventHandler(this.btnCopyMapCoords_Click);

            //
            // btnImportFs32
            //
            this.btnImportFs32.Location = new Point(395, 5);
            this.btnImportFs32.Name = "btnImportFs32";
            this.btnImportFs32.Size = new Size(90, 25);
            this.btnImportFs32.TabIndex = 16;
            this.btnImportFs32.Text = "匯入地圖包";
            this.btnImportFs32.UseVisualStyleBackColor = true;
            this.btnImportFs32.Click += new System.EventHandler(this.btnImportFs32_Click);

            //
            // btnSetPassable
            //
            this.btnSetPassable.Location = new Point(10, 35);
            this.btnSetPassable.Name = "btnSetPassable";
            this.btnSetPassable.Size = new Size(80, 25);
            this.btnSetPassable.TabIndex = 9;
            this.btnSetPassable.Text = "允許通行";
            this.btnSetPassable.UseVisualStyleBackColor = true;
            this.btnSetPassable.Click += new System.EventHandler(this.btnSetPassable_Click);

            //
            // btnSetImpassable
            //
            this.btnSetImpassable.Location = new Point(100, 35);
            this.btnSetImpassable.Name = "btnSetImpassable";
            this.btnSetImpassable.Size = new Size(80, 25);
            this.btnSetImpassable.TabIndex = 10;
            this.btnSetImpassable.Text = "禁止通行";
            this.btnSetImpassable.UseVisualStyleBackColor = true;
            this.btnSetImpassable.Click += new System.EventHandler(this.btnSetImpassable_Click);

            //
            // btnEditLayer5
            //
            this.btnEditLayer5.Location = new Point(190, 35);
            this.btnEditLayer5.Name = "btnEditLayer5";
            this.btnEditLayer5.Size = new Size(80, 25);
            this.btnEditLayer5.TabIndex = 14;
            this.btnEditLayer5.Text = "透明編輯";
            this.btnEditLayer5.UseVisualStyleBackColor = true;
            this.btnEditLayer5.Click += new System.EventHandler(this.btnEditLayer5_Click);

            //
            // btnRegionEdit
            //
            this.btnRegionEdit.Location = new Point(280, 35);
            this.btnRegionEdit.Name = "btnRegionEdit";
            this.btnRegionEdit.Size = new Size(80, 25);
            this.btnRegionEdit.TabIndex = 15;
            this.btnRegionEdit.Text = "戰鬥區域";
            this.btnRegionEdit.UseVisualStyleBackColor = true;
            this.btnRegionEdit.Click += new System.EventHandler(this.btnRegionEdit_Click);

            //
            // btnSaveS32
            //
            this.btnSaveS32.Location = new Point(120, 5);
            this.btnSaveS32.Name = "btnSaveS32";
            this.btnSaveS32.Size = new Size(80, 25);
            this.btnSaveS32.TabIndex = 11;
            this.btnSaveS32.Text = "保存 S32";
            this.btnSaveS32.UseVisualStyleBackColor = true;
            this.btnSaveS32.Click += new System.EventHandler(this.btnSaveS32_Click);

            //
            // btnReloadMap
            //
            this.btnReloadMap.Location = new Point(10, 5);
            this.btnReloadMap.Name = "btnReloadMap";
            this.btnReloadMap.Size = new Size(100, 25);
            this.btnReloadMap.TabIndex = 12;
            this.btnReloadMap.Text = "重新載入 (F5)";
            this.btnReloadMap.UseVisualStyleBackColor = true;
            this.btnReloadMap.Click += new System.EventHandler(this.btnReloadMap_Click);

            //
            // btnAnalyzeAttr
            //
            this.btnAnalyzeAttr.Location = new Point(315, 35);
            this.btnAnalyzeAttr.Name = "btnAnalyzeAttr";
            this.btnAnalyzeAttr.Size = new Size(80, 25);
            this.btnAnalyzeAttr.TabIndex = 17;
            this.btnAnalyzeAttr.Text = "分析屬性";
            this.btnAnalyzeAttr.UseVisualStyleBackColor = true;
            this.btnAnalyzeAttr.Visible = false;

            //
            // s32MapPanel
            //
            this.s32MapPanel.AutoScroll = false;
            this.s32MapPanel.BackColor = Color.Black;
            this.s32MapPanel.BorderStyle = BorderStyle.FixedSingle;
            this.s32MapPanel.Controls.Add(this._mapViewerControl);
            this.s32MapPanel.Dock = DockStyle.Fill;
            this.s32MapPanel.Location = new Point(0, 65);
            this.s32MapPanel.Name = "s32MapPanel";
            this.s32MapPanel.Size = new Size(696, 523);
            this.s32MapPanel.TabIndex = 1;

            //
            // _mapViewerControl
            //
            this._mapViewerControl.BackColor = Color.Black;
            this._mapViewerControl.Dock = DockStyle.Fill;
            this._mapViewerControl.Name = "_mapViewerControl";
            this._mapViewerControl.TabIndex = 0;
            this._mapViewerControl.TabStop = false;

            //
            // lblS32Info
            //
            this.lblS32Info.BackColor = Color.DarkGray;
            this.lblS32Info.Dock = DockStyle.Bottom;
            this.lblS32Info.Location = new Point(0, 588);
            this.lblS32Info.Name = "lblS32Info";
            this.lblS32Info.Size = new Size(696, 20);
            this.lblS32Info.TabIndex = 2;
            this.lblS32Info.Text = "請選擇一個 S32 檔案";
            this.lblS32Info.TextAlign = ContentAlignment.MiddleLeft;

            //
            // layerFloatPanel (浮動圖層控制面板)
            //
            this.layerFloatPanel.BackColor = Color.FromArgb(200, 50, 50, 50);
            this.layerFloatPanel.Controls.Add(this.lblLayerIcon);
            this.layerFloatPanel.Controls.Add(this.layerPopupPanel);
            this.layerFloatPanel.Location = new Point(10, 10);
            this.layerFloatPanel.Name = "layerFloatPanel";
            this.layerFloatPanel.Size = new Size(90, 295);
            this.layerFloatPanel.TabIndex = 10;
            this.layerFloatPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            //
            // lblLayerIcon (圖層圖示)
            //
            this.lblLayerIcon.BackColor = Color.Transparent;
            this.lblLayerIcon.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            this.lblLayerIcon.ForeColor = Color.White;
            this.lblLayerIcon.Location = new Point(0, 0);
            this.lblLayerIcon.Name = "lblLayerIcon";
            this.lblLayerIcon.Size = new Size(90, 24);
            this.lblLayerIcon.TabIndex = 0;
            this.lblLayerIcon.Text = "▣ 圖層";
            this.lblLayerIcon.TextAlign = ContentAlignment.MiddleCenter;

            //
            // layerPopupPanel (展開的選項面板)
            //
            this.layerPopupPanel.BackColor = Color.FromArgb(230, 40, 40, 40);
            this.layerPopupPanel.Controls.Add(this.chkFloatLayer1);
            this.layerPopupPanel.Controls.Add(this.chkFloatLayer2);
            this.layerPopupPanel.Controls.Add(this.chkFloatLayer4);
            this.layerPopupPanel.Controls.Add(this.chkFloatLayer5);
            this.layerPopupPanel.Controls.Add(this.chkFloatPassable);
            this.layerPopupPanel.Controls.Add(this.chkFloatGrid);
            this.layerPopupPanel.Controls.Add(this.chkFloatS32Boundary);
            this.layerPopupPanel.Controls.Add(this.chkFloatSafeZones);
            this.layerPopupPanel.Controls.Add(this.chkFloatCombatZones);
            this.layerPopupPanel.Controls.Add(this.chkFloatLayer8);
            this.layerPopupPanel.Location = new Point(0, 24);
            this.layerPopupPanel.Name = "layerPopupPanel";
            this.layerPopupPanel.Padding = new Padding(5);
            this.layerPopupPanel.Size = new Size(90, 270);
            this.layerPopupPanel.TabIndex = 1;
            this.layerPopupPanel.Visible = true;

            //
            // chkFloatLayer1
            //
            this.chkFloatLayer1.AutoSize = true;
            this.chkFloatLayer1.Checked = true;
            this.chkFloatLayer1.CheckState = CheckState.Checked;
            this.chkFloatLayer1.ForeColor = Color.White;
            this.chkFloatLayer1.Location = new Point(8, 5);
            this.chkFloatLayer1.Name = "chkFloatLayer1";
            this.chkFloatLayer1.Size = new Size(80, 19);
            this.chkFloatLayer1.TabIndex = 0;
            this.chkFloatLayer1.Text = "L1 地板";
            this.chkFloatLayer1.UseVisualStyleBackColor = true;
            this.chkFloatLayer1.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatLayer2
            //
            this.chkFloatLayer2.AutoSize = true;
            this.chkFloatLayer2.Checked = true;
            this.chkFloatLayer2.ForeColor = Color.LightGray;
            this.chkFloatLayer2.Location = new Point(8, 27);
            this.chkFloatLayer2.Name = "chkFloatLayer2";
            this.chkFloatLayer2.Size = new Size(80, 19);
            this.chkFloatLayer2.TabIndex = 1;
            this.chkFloatLayer2.Text = "L2";
            this.chkFloatLayer2.UseVisualStyleBackColor = true;
            this.chkFloatLayer2.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatLayer4
            //
            this.chkFloatLayer4.AutoSize = true;
            this.chkFloatLayer4.Checked = true;
            this.chkFloatLayer4.CheckState = CheckState.Checked;
            this.chkFloatLayer4.ForeColor = Color.White;
            this.chkFloatLayer4.Location = new Point(8, 49);
            this.chkFloatLayer4.Name = "chkFloatLayer4";
            this.chkFloatLayer4.Size = new Size(80, 19);
            this.chkFloatLayer4.TabIndex = 2;
            this.chkFloatLayer4.Text = "L4 物件";
            this.chkFloatLayer4.UseVisualStyleBackColor = true;
            this.chkFloatLayer4.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatLayer5 (放在 L4 下面)
            //
            this.chkFloatLayer5.AutoSize = true;
            this.chkFloatLayer5.ForeColor = Color.FromArgb(100, 180, 255);
            this.chkFloatLayer5.Location = new Point(8, 71);
            this.chkFloatLayer5.Name = "chkFloatLayer5";
            this.chkFloatLayer5.Size = new Size(80, 19);
            this.chkFloatLayer5.TabIndex = 3;
            this.chkFloatLayer5.Text = "L5 透明";
            this.chkFloatLayer5.UseVisualStyleBackColor = true;
            this.chkFloatLayer5.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatPassable
            //
            this.chkFloatPassable.AutoSize = true;
            this.chkFloatPassable.ForeColor = Color.LightGreen;
            this.chkFloatPassable.Location = new Point(8, 93);
            this.chkFloatPassable.Name = "chkFloatPassable";
            this.chkFloatPassable.Size = new Size(80, 19);
            this.chkFloatPassable.TabIndex = 4;
            this.chkFloatPassable.Text = "通行";
            this.chkFloatPassable.UseVisualStyleBackColor = true;
            this.chkFloatPassable.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatGrid
            //
            this.chkFloatGrid.AutoSize = true;
            this.chkFloatGrid.ForeColor = Color.LightBlue;
            this.chkFloatGrid.Location = new Point(8, 115);
            this.chkFloatGrid.Name = "chkFloatGrid";
            this.chkFloatGrid.Size = new Size(80, 19);
            this.chkFloatGrid.TabIndex = 5;
            this.chkFloatGrid.Text = "格線";
            this.chkFloatGrid.UseVisualStyleBackColor = true;
            this.chkFloatGrid.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatS32Boundary
            //
            this.chkFloatS32Boundary.AutoSize = true;
            this.chkFloatS32Boundary.ForeColor = Color.Orange;
            this.chkFloatS32Boundary.Location = new Point(8, 137);
            this.chkFloatS32Boundary.Name = "chkFloatS32Boundary";
            this.chkFloatS32Boundary.Size = new Size(80, 19);
            this.chkFloatS32Boundary.TabIndex = 6;
            this.chkFloatS32Boundary.Text = "S32邊界";
            this.chkFloatS32Boundary.UseVisualStyleBackColor = true;
            this.chkFloatS32Boundary.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatSafeZones
            //
            this.chkFloatSafeZones.AutoSize = true;
            this.chkFloatSafeZones.ForeColor = Color.FromArgb(100, 180, 255);
            this.chkFloatSafeZones.Location = new Point(8, 159);
            this.chkFloatSafeZones.Name = "chkFloatSafeZones";
            this.chkFloatSafeZones.Size = new Size(80, 19);
            this.chkFloatSafeZones.TabIndex = 7;
            this.chkFloatSafeZones.Text = "安全區域";
            this.chkFloatSafeZones.UseVisualStyleBackColor = true;
            this.chkFloatSafeZones.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatCombatZones
            //
            this.chkFloatCombatZones.AutoSize = true;
            this.chkFloatCombatZones.ForeColor = Color.FromArgb(255, 100, 100);
            this.chkFloatCombatZones.Location = new Point(8, 181);
            this.chkFloatCombatZones.Name = "chkFloatCombatZones";
            this.chkFloatCombatZones.Size = new Size(80, 19);
            this.chkFloatCombatZones.TabIndex = 8;
            this.chkFloatCombatZones.Text = "戰鬥區域";
            this.chkFloatCombatZones.UseVisualStyleBackColor = true;
            this.chkFloatCombatZones.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // chkFloatLayer8
            //
            this.chkFloatLayer8.AutoSize = true;
            this.chkFloatLayer8.ForeColor = Color.FromArgb(255, 180, 100);
            this.chkFloatLayer8.Location = new Point(8, 203);
            this.chkFloatLayer8.Name = "chkFloatLayer8";
            this.chkFloatLayer8.Size = new Size(80, 19);
            this.chkFloatLayer8.TabIndex = 9;
            this.chkFloatLayer8.Text = "L8 特效";
            this.chkFloatLayer8.UseVisualStyleBackColor = true;
            this.chkFloatLayer8.Checked = true;
            this.chkFloatLayer8.CheckedChanged += new System.EventHandler(this.chkFloatLayer_CheckedChanged);

            //
            // rightPanel
            //
            this.rightPanel.BorderStyle = BorderStyle.FixedSingle;
            this.rightPanel.Controls.Add(this.lblTileList);
            this.rightPanel.Controls.Add(this.txtTileSearch);
            this.rightPanel.Controls.Add(this.lvTiles);
            this.rightPanel.Controls.Add(this.lblMaterials);
            this.rightPanel.Controls.Add(this.lvMaterials);
            this.rightPanel.Controls.Add(this.btnMoreMaterials);
            this.rightPanel.Controls.Add(this.lblGroupThumbnails);
            this.rightPanel.Controls.Add(this.btnShowAllGroups);
            this.rightPanel.Controls.Add(this.lvGroupThumbnails);
            this.rightPanel.Dock = DockStyle.Right;
            this.rightPanel.Location = new Point(1010, 24);
            this.rightPanel.Name = "rightPanel";
            this.rightPanel.Size = new Size(220, 654);
            this.rightPanel.TabIndex = 6;
            this.rightPanel.AllowDrop = true;
            this.rightPanel.DragEnter += new DragEventHandler(this.lvMaterials_DragEnter);
            this.rightPanel.DragOver += new DragEventHandler(this.lvMaterials_DragOver);
            this.rightPanel.DragDrop += new DragEventHandler(this.lvMaterials_DragDrop);

            //
            // lblTileList
            //
            this.lblTileList.Location = new Point(5, 5);
            this.lblTileList.Name = "lblTileList";
            this.lblTileList.Size = new Size(210, 20);
            this.lblTileList.TabIndex = 0;
            this.lblTileList.Text = "使用的 Tile";
            this.lblTileList.TextAlign = ContentAlignment.MiddleLeft;

            //
            // txtTileSearch
            //
            this.txtTileSearch.Location = new Point(5, 28);
            this.txtTileSearch.Name = "txtTileSearch";
            this.txtTileSearch.Size = new Size(210, 23);
            this.txtTileSearch.TabIndex = 7;
            this.txtTileSearch.PlaceholderText = "搜尋 TileId...";
            this.txtTileSearch.TextChanged += new System.EventHandler(this.txtTileSearch_TextChanged);

            //
            // lvTiles
            //
            this.lvTiles.Location = new Point(5, 55);
            this.lvTiles.Name = "lvTiles";
            this.lvTiles.Size = new Size(210, 125);
            this.lvTiles.TabIndex = 1;
            this.lvTiles.View = View.LargeIcon;
            this.lvTiles.DoubleClick += new System.EventHandler(this.lvTiles_DoubleClick);
            this.lvTiles.MouseUp += new MouseEventHandler(this.lvTiles_MouseUp);

            //
            // lblMaterials
            //
            this.lblMaterials.Location = new Point(5, 185);
            this.lblMaterials.Name = "lblMaterials";
            this.lblMaterials.Size = new Size(210, 20);
            this.lblMaterials.TabIndex = 2;
            this.lblMaterials.Text = "最近的素材";
            this.lblMaterials.TextAlign = ContentAlignment.MiddleLeft;

            //
            // lvMaterials
            //
            this.lvMaterials.Location = new Point(5, 210);
            this.lvMaterials.Name = "lvMaterials";
            this.lvMaterials.Size = new Size(210, 95);
            this.lvMaterials.TabIndex = 3;
            this.lvMaterials.View = View.LargeIcon;
            this.lvMaterials.MultiSelect = false;
            this.lvMaterials.AllowDrop = true;
            this.lvMaterials.DoubleClick += new System.EventHandler(this.lvMaterials_DoubleClick);
            this.lvMaterials.MouseUp += new MouseEventHandler(this.lvMaterials_MouseUp);
            this.lvMaterials.DragEnter += new DragEventHandler(this.lvMaterials_DragEnter);
            this.lvMaterials.DragOver += new DragEventHandler(this.lvMaterials_DragOver);
            this.lvMaterials.DragDrop += new DragEventHandler(this.lvMaterials_DragDrop);

            //
            // btnMoreMaterials
            //
            this.btnMoreMaterials.Location = new Point(5, 308);
            this.btnMoreMaterials.Name = "btnMoreMaterials";
            this.btnMoreMaterials.Size = new Size(210, 23);
            this.btnMoreMaterials.TabIndex = 8;
            this.btnMoreMaterials.Text = "更多...";
            this.btnMoreMaterials.UseVisualStyleBackColor = true;
            this.btnMoreMaterials.Click += new System.EventHandler(this.btnMoreMaterials_Click);

            //
            // lblGroupThumbnails
            //
            this.lblGroupThumbnails.Location = new Point(5, 335);
            this.lblGroupThumbnails.Name = "lblGroupThumbnails";
            this.lblGroupThumbnails.Size = new Size(140, 20);
            this.lblGroupThumbnails.TabIndex = 4;
            this.lblGroupThumbnails.Text = "群組縮圖列表";
            this.lblGroupThumbnails.TextAlign = ContentAlignment.MiddleLeft;

            //
            // btnShowAllGroups
            //
            this.btnShowAllGroups.Location = new Point(150, 335);
            this.btnShowAllGroups.Name = "btnShowAllGroups";
            this.btnShowAllGroups.Size = new Size(60, 20);
            this.btnShowAllGroups.TabIndex = 6;
            this.btnShowAllGroups.Text = "全部";
            this.btnShowAllGroups.UseVisualStyleBackColor = true;
            this.btnShowAllGroups.Click += new System.EventHandler(this.btnShowAllGroups_Click);

            //
            // lvGroupThumbnails
            //
            this.lvGroupThumbnails.Location = new Point(5, 360);
            this.lvGroupThumbnails.Name = "lvGroupThumbnails";
            this.lvGroupThumbnails.Size = new Size(210, 285);
            this.lvGroupThumbnails.TabIndex = 5;
            this.lvGroupThumbnails.View = View.LargeIcon;
            this.lvGroupThumbnails.MultiSelect = true;
            this.lvGroupThumbnails.MouseClick += new MouseEventHandler(this.lvGroupThumbnails_MouseClick);
            this.lvGroupThumbnails.DoubleClick += new System.EventHandler(this.lvGroupThumbnails_DoubleClick);
            this.lvGroupThumbnails.MouseUp += new MouseEventHandler(this.lvGroupThumbnails_MouseUp);
            this.lvGroupThumbnails.SelectedIndexChanged += new System.EventHandler(this.lvGroupThumbnails_SelectedIndexChanged);

            //
            // toolbarPanel (第一排)
            //
            this.toolbarPanel.BorderStyle = BorderStyle.FixedSingle;
            this.toolbarPanel.Controls.Add(this.btnToolCopy);
            this.toolbarPanel.Controls.Add(this.btnToolPaste);
            this.toolbarPanel.Controls.Add(this.btnToolDelete);
            this.toolbarPanel.Controls.Add(this.btnToolUndo);
            this.toolbarPanel.Controls.Add(this.btnToolRedo);
            this.toolbarPanel.Controls.Add(this.btnToolSave);
            this.toolbarPanel.Controls.Add(this.btnToolCellInfo);
            this.toolbarPanel.Controls.Add(this.btnToolReplaceTile);
            this.toolbarPanel.Controls.Add(this.btnToolAddS32);
            this.toolbarPanel.Controls.Add(this.btnToolClearLayer7);
            this.toolbarPanel.Controls.Add(this.btnToolClearCell);
            this.toolbarPanel.Controls.Add(this.btnToolCheckL5Invalid);
            this.toolbarPanel.Dock = DockStyle.Left;
            this.toolbarPanel.Location = new Point(0, 0);
            this.toolbarPanel.Name = "toolbarPanel";
            this.toolbarPanel.Size = new Size(40, 654);
            this.toolbarPanel.TabIndex = 7;

            //
            // toolbarPanel2 (第二排 - 查看各層)
            //
            this.toolbarPanel2.BorderStyle = BorderStyle.FixedSingle;
            this.toolbarPanel2.Controls.Add(this.btnToolCheckL1);
            this.toolbarPanel2.Controls.Add(this.btnToolCheckL2);
            this.toolbarPanel2.Controls.Add(this.btnToolCheckL4);
            this.toolbarPanel2.Controls.Add(this.btnToolCheckL5);
            this.toolbarPanel2.Controls.Add(this.btnToolCheckL6);
            this.toolbarPanel2.Controls.Add(this.btnToolCheckL7);
            this.toolbarPanel2.Controls.Add(this.btnToolCheckL8);
            this.toolbarPanel2.Dock = DockStyle.Left;
            this.toolbarPanel2.Location = new Point(40, 0);
            this.toolbarPanel2.Name = "toolbarPanel2";
            this.toolbarPanel2.Size = new Size(40, 654);
            this.toolbarPanel2.TabIndex = 8;

            //
            // toolbarContainer (容器)
            //
            this.toolbarContainer.Controls.Add(this.toolbarPanel2);
            this.toolbarContainer.Controls.Add(this.toolbarPanel);
            this.toolbarContainer.Dock = DockStyle.Right;
            this.toolbarContainer.Name = "toolbarContainer";
            this.toolbarContainer.Size = new Size(80, 654);
            this.toolbarContainer.TabIndex = 9;

            //
            // btnToolCopy
            //
            this.btnToolCopy.Location = new Point(2, 5);
            this.btnToolCopy.Name = "btnToolCopy";
            this.btnToolCopy.Size = new Size(34, 34);
            this.btnToolCopy.TabIndex = 0;
            this.btnToolCopy.Text = "複製";
            this.btnToolCopy.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolCopy, "複製 (Ctrl+C)");
            this.btnToolCopy.Click += new System.EventHandler(this.btnToolCopy_Click);

            //
            // btnToolPaste
            //
            this.btnToolPaste.Location = new Point(2, 44);
            this.btnToolPaste.Name = "btnToolPaste";
            this.btnToolPaste.Size = new Size(34, 34);
            this.btnToolPaste.TabIndex = 1;
            this.btnToolPaste.Text = "貼上";
            this.btnToolPaste.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolPaste, "貼上 (Ctrl+V)");
            this.btnToolPaste.Click += new System.EventHandler(this.btnToolPaste_Click);

            //
            // btnToolDelete
            //
            this.btnToolDelete.Location = new Point(2, 83);
            this.btnToolDelete.Name = "btnToolDelete";
            this.btnToolDelete.Size = new Size(34, 34);
            this.btnToolDelete.TabIndex = 2;
            this.btnToolDelete.Text = "刪除";
            this.btnToolDelete.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolDelete, "刪除 (Del)");
            this.btnToolDelete.Click += new System.EventHandler(this.btnToolDelete_Click);

            //
            // btnToolUndo
            //
            this.btnToolUndo.Location = new Point(2, 132);
            this.btnToolUndo.Name = "btnToolUndo";
            this.btnToolUndo.Size = new Size(34, 34);
            this.btnToolUndo.TabIndex = 3;
            this.btnToolUndo.Text = "復原";
            this.btnToolUndo.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolUndo, "復原 (Ctrl+Z)");
            this.btnToolUndo.Click += new System.EventHandler(this.btnToolUndo_Click);

            //
            // btnToolRedo
            //
            this.btnToolRedo.Location = new Point(2, 171);
            this.btnToolRedo.Name = "btnToolRedo";
            this.btnToolRedo.Size = new Size(34, 34);
            this.btnToolRedo.TabIndex = 4;
            this.btnToolRedo.Text = "重做";
            this.btnToolRedo.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolRedo, "重做 (Ctrl+Y)");
            this.btnToolRedo.Click += new System.EventHandler(this.btnToolRedo_Click);

            //
            // btnToolSave
            //
            this.btnToolSave.Location = new Point(2, 220);
            this.btnToolSave.Name = "btnToolSave";
            this.btnToolSave.Size = new Size(34, 34);
            this.btnToolSave.TabIndex = 5;
            this.btnToolSave.Text = "儲存";
            this.btnToolSave.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolSave, "儲存 (Ctrl+S)");
            this.btnToolSave.Click += new System.EventHandler(this.btnToolSave_Click);

            //
            // btnToolCellInfo
            //
            this.btnToolCellInfo.Location = new Point(2, 269);
            this.btnToolCellInfo.Name = "btnToolCellInfo";
            this.btnToolCellInfo.Size = new Size(34, 34);
            this.btnToolCellInfo.TabIndex = 6;
            this.btnToolCellInfo.Text = "詳細";
            this.btnToolCellInfo.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolCellInfo, "格子詳細資訊");
            this.btnToolCellInfo.Click += new System.EventHandler(this.btnToolCellInfo_Click);

            //
            // btnToolReplaceTile
            //
            this.btnToolReplaceTile.Location = new Point(2, 318);
            this.btnToolReplaceTile.Name = "btnToolReplaceTile";
            this.btnToolReplaceTile.Size = new Size(34, 34);
            this.btnToolReplaceTile.TabIndex = 7;
            this.btnToolReplaceTile.Text = "替換";
            this.btnToolReplaceTile.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolReplaceTile, "批次替換地板");
            this.btnToolReplaceTile.Click += new System.EventHandler(this.btnToolReplaceTile_Click);

            //
            // btnToolAddS32
            //
            this.btnToolAddS32.Location = new Point(2, 367);
            this.btnToolAddS32.Name = "btnToolAddS32";
            this.btnToolAddS32.Size = new Size(34, 34);
            this.btnToolAddS32.TabIndex = 8;
            this.btnToolAddS32.Text = "新增";
            this.btnToolAddS32.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolAddS32, "新增 S32 區塊");
            this.btnToolAddS32.Click += new System.EventHandler(this.btnToolAddS32_Click);

            //
            // btnToolClearLayer7
            //
            this.btnToolClearLayer7.Location = new Point(2, 405);
            this.btnToolClearLayer7.Name = "btnToolClearLayer7";
            this.btnToolClearLayer7.Size = new Size(34, 34);
            this.btnToolClearLayer7.TabIndex = 9;
            this.btnToolClearLayer7.Text = "清L7";
            this.btnToolClearLayer7.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolClearLayer7, "清除所有第七層（傳送點）資料");
            this.btnToolClearLayer7.Click += new System.EventHandler(this.btnToolClearLayer7_Click);

            //
            // btnToolClearCell
            //
            this.btnToolClearCell.Location = new Point(2, 443);
            this.btnToolClearCell.Name = "btnToolClearCell";
            this.btnToolClearCell.Size = new Size(34, 34);
            this.btnToolClearCell.TabIndex = 10;
            this.btnToolClearCell.Text = "清格";
            this.btnToolClearCell.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolClearCell, "清除指定格子的各層資料");
            this.btnToolClearCell.Click += new System.EventHandler(this.btnToolClearCell_Click);

            //
            // btnToolCheckL5Invalid
            //
            this.btnToolCheckL5Invalid.Location = new Point(2, 481);
            this.btnToolCheckL5Invalid.Name = "btnToolCheckL5Invalid";
            this.btnToolCheckL5Invalid.Size = new Size(34, 34);
            this.btnToolCheckL5Invalid.TabIndex = 13;
            this.btnToolCheckL5Invalid.Text = "⚠";
            this.btnToolCheckL5Invalid.ForeColor = Color.Red;
            this.btnToolCheckL5Invalid.Font = new Font(this.btnToolCheckL5Invalid.Font.FontFamily, 14, FontStyle.Bold);
            this.btnToolCheckL5Invalid.UseVisualStyleBackColor = true;
            this.btnToolCheckL5Invalid.Visible = false;  // 預設隱藏，有異常時才顯示
            this.toolTip1.SetToolTip(this.btnToolCheckL5Invalid, "檢查 Layer5 無效的 ObjectIndex");
            this.btnToolCheckL5Invalid.Click += new System.EventHandler(this.btnToolCheckL5Invalid_Click);

            //
            // btnToolCheckL1
            //
            this.btnToolCheckL1.Location = new Point(2, 2);
            this.btnToolCheckL1.Name = "btnToolCheckL1";
            this.btnToolCheckL1.Size = new Size(34, 34);
            this.btnToolCheckL1.TabIndex = 11;
            this.btnToolCheckL1.Text = "查L1";
            this.btnToolCheckL1.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolCheckL1, "查看與編輯第一層（地板圖塊）資料");
            this.btnToolCheckL1.Click += new System.EventHandler(this.btnToolCheckL1_Click);

            //
            // btnToolCheckL2
            //
            this.btnToolCheckL2.Location = new Point(2, 40);
            this.btnToolCheckL2.Name = "btnToolCheckL2";
            this.btnToolCheckL2.Size = new Size(34, 34);
            this.btnToolCheckL2.TabIndex = 11;
            this.btnToolCheckL2.Text = "清L2";
            this.btnToolCheckL2.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolCheckL2, "查看與清除第二層資料");
            this.btnToolCheckL2.Click += new System.EventHandler(this.btnToolCheckL2_Click);

            //
            // btnToolCheckL4
            //
            this.btnToolCheckL4.Location = new Point(2, 78);
            this.btnToolCheckL4.Name = "btnToolCheckL4";
            this.btnToolCheckL4.Size = new Size(34, 34);
            this.btnToolCheckL4.TabIndex = 11;
            this.btnToolCheckL4.Text = "查L4";
            this.btnToolCheckL4.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolCheckL4, "查看與編輯第四層（物件）資料");
            this.btnToolCheckL4.Click += new System.EventHandler(this.btnToolCheckL4_Click);

            //
            // btnToolCheckL5
            //
            this.btnToolCheckL5.Location = new Point(2, 116);
            this.btnToolCheckL5.Name = "btnToolCheckL5";
            this.btnToolCheckL5.Size = new Size(34, 34);
            this.btnToolCheckL5.TabIndex = 11;
            this.btnToolCheckL5.Text = "查L5";
            this.btnToolCheckL5.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolCheckL5, "查看與管理第五層（透明圖塊）資料");
            this.btnToolCheckL5.Click += new System.EventHandler(this.btnToolCheckL5_Click);

            //
            // btnToolCheckL6
            //
            this.btnToolCheckL6.Location = new Point(2, 154);
            this.btnToolCheckL6.Name = "btnToolCheckL6";
            this.btnToolCheckL6.Size = new Size(34, 34);
            this.btnToolCheckL6.TabIndex = 12;
            this.btnToolCheckL6.Text = "查L6";
            this.btnToolCheckL6.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolCheckL6, "查看與管理第六層（使用的TileId）資料");
            this.btnToolCheckL6.Click += new System.EventHandler(this.btnToolCheckL6_Click);

            //
            // btnToolCheckL7
            //
            this.btnToolCheckL7.Location = new Point(2, 192);
            this.btnToolCheckL7.Name = "btnToolCheckL7";
            this.btnToolCheckL7.Size = new Size(34, 34);
            this.btnToolCheckL7.TabIndex = 13;
            this.btnToolCheckL7.Text = "查L7";
            this.btnToolCheckL7.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolCheckL7, "查看與編輯第七層（傳送點）資料");
            this.btnToolCheckL7.Click += new System.EventHandler(this.btnToolCheckL7_Click);

            //
            // btnToolCheckL8
            //
            this.btnToolCheckL8.Location = new Point(2, 230);
            this.btnToolCheckL8.Name = "btnToolCheckL8";
            this.btnToolCheckL8.Size = new Size(34, 34);
            this.btnToolCheckL8.TabIndex = 14;
            this.btnToolCheckL8.Text = "查L8";
            this.btnToolCheckL8.UseVisualStyleBackColor = true;
            this.toolTip1.SetToolTip(this.btnToolCheckL8, "查看哪些S32有第八層（特效）資料");
            this.btnToolCheckL8.Click += new System.EventHandler(this.btnToolCheckL8_Click);

            //
            // MapForm
            //
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1400, 700);
            this.Controls.Add(this.toolbarContainer);
            this.Controls.Add(this.rightPanel);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.leftPanel);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "MapForm";
            this.Text = "地圖編輯器";
            this.Load += new System.EventHandler(this.MapForm_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.tabS32Files.ResumeLayout(false);
            this.tabMapList.ResumeLayout(false);
            this.tabMapList.PerformLayout();
            this.leftTabControl.ResumeLayout(false);
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
