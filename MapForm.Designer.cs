using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using L1MapViewer.Other;

namespace L1FlyMapViewer
{
    partial class MapForm
    {
        private IContainer components;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem openToolStripMenuItem;
        private ToolStripMenuItem exportToolStripMenuItem;
        private StatusStrip statusStrip1;
        public ToolStripStatusLabel toolStripStatusLabel1;
        public ToolStripProgressBar toolStripProgressBar1;
        public ToolStripStatusLabel toolStripStatusLabel2;
        public ToolStripStatusLabel toolStripStatusLabel3;

        // 左側面板
        private Panel leftPanel;
        public ComboBox comboBox1;
        private PictureBox miniMapPictureBox;
        private Label lblS32Files;
        private CheckedListBox lstS32Files;

        // 右側面板（Tile 清單）
        private Panel rightPanel;
        private Label lblTileList;
        private ListView lvTiles;
        private Label lblLayer4Groups;
        private ListView lvLayer4Groups;

        // 工具列面板（右側功能區）
        private Panel toolbarPanel;
        private Button btnToolCopy;
        private Button btnToolPaste;
        private Button btnToolDelete;
        private Button btnToolUndo;
        private Button btnToolRedo;
        private Button btnToolSave;
        private Button btnToolCellInfo;
        private Button btnToolReplaceTile;
        private Button btnToolAddS32;
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
        private CheckBox chkShowPassable;
        private CheckBox chkShowGrid;
        private CheckBox chkShowS32Boundary;
        private Button btnCopySettings;
        private Button btnSetPassable;
        private Button btnSetImpassable;
        private Button btnSaveS32;
        private Button btnReloadMap;
        private Button btnAnalyzeAttr;
        private Panel s32MapPanel;
        private PictureBox s32PictureBox;
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

            // StatusStrip
            this.statusStrip1 = new StatusStrip();
            this.toolStripStatusLabel1 = new ToolStripStatusLabel();
            this.toolStripStatusLabel2 = new ToolStripStatusLabel();
            this.toolStripStatusLabel3 = new ToolStripStatusLabel();
            this.toolStripProgressBar1 = new ToolStripProgressBar();

            // 左側面板
            this.leftPanel = new Panel();
            this.comboBox1 = new ComboBox();
            this.miniMapPictureBox = new PictureBox();
            this.lblS32Files = new Label();
            this.lstS32Files = new CheckedListBox();

            // 右側面板
            this.rightPanel = new Panel();
            this.lblTileList = new Label();
            this.lvTiles = new ListView();
            this.lblLayer4Groups = new Label();
            this.lvLayer4Groups = new ListView();

            // 工具列面板
            this.toolbarPanel = new Panel();
            this.btnToolCopy = new Button();
            this.btnToolPaste = new Button();
            this.btnToolDelete = new Button();
            this.btnToolUndo = new Button();
            this.btnToolRedo = new Button();
            this.btnToolSave = new Button();
            this.btnToolCellInfo = new Button();
            this.btnToolReplaceTile = new Button();
            this.btnToolAddS32 = new Button();
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
            this.btnCopySettings = new Button();
            this.btnSetPassable = new Button();
            this.btnSetImpassable = new Button();
            this.btnSaveS32 = new Button();
            this.btnReloadMap = new Button();
            this.btnAnalyzeAttr = new Button();
            this.s32MapPanel = new Panel();
            this.s32PictureBox = new PictureBox();
            this.lblS32Info = new Label();

            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.leftPanel.SuspendLayout();
            ((ISupportInitialize)this.miniMapPictureBox).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabMapPreview.SuspendLayout();
            this.tabS32Editor.SuspendLayout();
            this.s32EditorPanel.SuspendLayout();
            this.s32LayerControlPanel.SuspendLayout();
            this.s32MapPanel.SuspendLayout();
            ((ISupportInitialize)this.s32PictureBox).BeginInit();
            this.panel1.SuspendLayout();
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
                this.exportToolStripMenuItem
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
            // exportToolStripMenuItem
            //
            this.exportToolStripMenuItem.Name = "exportToolStripMenuItem";
            this.exportToolStripMenuItem.Size = new Size(122, 20);
            this.exportToolStripMenuItem.Text = "匯出地圖通行資料";
            this.exportToolStripMenuItem.Click += new System.EventHandler(this.exportToolStripMenuItem_Click);

            //
            // statusStrip1
            //
            this.statusStrip1.Items.AddRange(new ToolStripItem[] {
                this.toolStripStatusLabel1,
                this.toolStripStatusLabel2,
                this.toolStripStatusLabel3,
                this.toolStripProgressBar1
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
            // leftPanel
            //
            this.leftPanel.BorderStyle = BorderStyle.FixedSingle;
            this.leftPanel.Controls.Add(this.comboBox1);
            this.leftPanel.Controls.Add(this.miniMapPictureBox);
            this.leftPanel.Controls.Add(this.lblS32Files);
            this.leftPanel.Controls.Add(this.lstS32Files);
            this.leftPanel.Dock = DockStyle.Left;
            this.leftPanel.Location = new Point(0, 24);
            this.leftPanel.Name = "leftPanel";
            this.leftPanel.Size = new Size(280, 654);
            this.leftPanel.TabIndex = 2;

            //
            // comboBox1
            //
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new Point(10, 10);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new Size(260, 21);
            this.comboBox1.TabIndex = 0;
            this.comboBox1.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            this.comboBox1.AutoCompleteSource = AutoCompleteSource.ListItems;
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);

            //
            // miniMapPictureBox
            //
            this.miniMapPictureBox.BackColor = Color.Black;
            this.miniMapPictureBox.BorderStyle = BorderStyle.FixedSingle;
            this.miniMapPictureBox.Location = new Point(10, 40);
            this.miniMapPictureBox.Name = "miniMapPictureBox";
            this.miniMapPictureBox.Size = new Size(260, 260);
            this.miniMapPictureBox.SizeMode = PictureBoxSizeMode.Normal;
            this.miniMapPictureBox.TabIndex = 1;
            this.miniMapPictureBox.TabStop = false;
            this.miniMapPictureBox.Cursor = Cursors.Hand;
            this.miniMapPictureBox.MouseDown += new MouseEventHandler(this.miniMapPictureBox_MouseDown);
            this.miniMapPictureBox.MouseMove += new MouseEventHandler(this.miniMapPictureBox_MouseMove);
            this.miniMapPictureBox.MouseUp += new MouseEventHandler(this.miniMapPictureBox_MouseUp);
            this.miniMapPictureBox.MouseClick += new MouseEventHandler(this.miniMapPictureBox_MouseClick);

            //
            // lblS32Files
            //
            this.lblS32Files.Location = new Point(10, 310);
            this.lblS32Files.Name = "lblS32Files";
            this.lblS32Files.Size = new Size(260, 20);
            this.lblS32Files.TabIndex = 2;
            this.lblS32Files.Text = "S32 檔案清單";
            this.lblS32Files.TextAlign = ContentAlignment.MiddleLeft;

            //
            // lstS32Files
            //
            this.lstS32Files.Location = new Point(10, 335);
            this.lstS32Files.Name = "lstS32Files";
            this.lstS32Files.Size = new Size(260, 300);
            this.lstS32Files.TabIndex = 3;
            this.lstS32Files.CheckOnClick = true;
            this.lstS32Files.SelectedIndexChanged += new System.EventHandler(this.lstS32Files_SelectedIndexChanged);
            this.lstS32Files.ItemCheck += new ItemCheckEventHandler(this.lstS32Files_ItemCheck);

            //
            // tabControl1
            //
            this.tabControl1.Controls.Add(this.tabMapPreview);
            this.tabControl1.Controls.Add(this.tabS32Editor);
            this.tabControl1.Location = new Point(290, 34);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 1;  // 預設開啟 S32 編輯器
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
            this.s32EditorPanel.Controls.Add(this.s32MapPanel);
            this.s32EditorPanel.Controls.Add(this.s32LayerControlPanel);
            this.s32EditorPanel.Controls.Add(this.lblS32Info);
            this.s32EditorPanel.Dock = DockStyle.Fill;
            this.s32EditorPanel.Location = new Point(3, 3);
            this.s32EditorPanel.Name = "s32EditorPanel";
            this.s32EditorPanel.Size = new Size(696, 608);
            this.s32EditorPanel.TabIndex = 0;

            //
            // s32LayerControlPanel
            //
            this.s32LayerControlPanel.BackColor = Color.LightGray;
            this.s32LayerControlPanel.BorderStyle = BorderStyle.FixedSingle;
            this.s32LayerControlPanel.Controls.Add(this.chkLayer1);
            this.s32LayerControlPanel.Controls.Add(this.chkLayer2);
            this.s32LayerControlPanel.Controls.Add(this.chkLayer3);
            this.s32LayerControlPanel.Controls.Add(this.chkLayer4);
            this.s32LayerControlPanel.Controls.Add(this.chkShowPassable);
            this.s32LayerControlPanel.Controls.Add(this.chkShowGrid);
            this.s32LayerControlPanel.Controls.Add(this.chkShowS32Boundary);
            this.s32LayerControlPanel.Controls.Add(this.btnCopySettings);
            this.s32LayerControlPanel.Controls.Add(this.btnSetPassable);
            this.s32LayerControlPanel.Controls.Add(this.btnSetImpassable);
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
            // btnCopySettings
            //
            this.btnCopySettings.Location = new Point(520, 33);
            this.btnCopySettings.Name = "btnCopySettings";
            this.btnCopySettings.Size = new Size(90, 25);
            this.btnCopySettings.TabIndex = 8;
            this.btnCopySettings.Text = "複製設定...";
            this.btnCopySettings.UseVisualStyleBackColor = true;
            this.btnCopySettings.Click += new System.EventHandler(this.btnCopySettings_Click);

            //
            // btnSetPassable
            //
            this.btnSetPassable.Location = new Point(10, 33);
            this.btnSetPassable.Name = "btnSetPassable";
            this.btnSetPassable.Size = new Size(80, 25);
            this.btnSetPassable.TabIndex = 9;
            this.btnSetPassable.Text = "允許通行";
            this.btnSetPassable.UseVisualStyleBackColor = true;
            this.btnSetPassable.Click += new System.EventHandler(this.btnSetPassable_Click);

            //
            // btnSetImpassable
            //
            this.btnSetImpassable.Location = new Point(100, 33);
            this.btnSetImpassable.Name = "btnSetImpassable";
            this.btnSetImpassable.Size = new Size(80, 25);
            this.btnSetImpassable.TabIndex = 10;
            this.btnSetImpassable.Text = "禁止通行";
            this.btnSetImpassable.UseVisualStyleBackColor = true;
            this.btnSetImpassable.Click += new System.EventHandler(this.btnSetImpassable_Click);

            //
            // btnSaveS32
            //
            this.btnSaveS32.Location = new Point(190, 33);
            this.btnSaveS32.Name = "btnSaveS32";
            this.btnSaveS32.Size = new Size(80, 25);
            this.btnSaveS32.TabIndex = 11;
            this.btnSaveS32.Text = "保存 S32";
            this.btnSaveS32.UseVisualStyleBackColor = true;
            this.btnSaveS32.Click += new System.EventHandler(this.btnSaveS32_Click);

            //
            // btnReloadMap
            //
            this.btnReloadMap.Location = new Point(280, 33);
            this.btnReloadMap.Name = "btnReloadMap";
            this.btnReloadMap.Size = new Size(100, 25);
            this.btnReloadMap.TabIndex = 12;
            this.btnReloadMap.Text = "重新載入 (F5)";
            this.btnReloadMap.UseVisualStyleBackColor = true;
            this.btnReloadMap.Click += new System.EventHandler(this.btnReloadMap_Click);

            //
            // s32MapPanel
            //
            this.s32MapPanel.AutoScroll = true;
            this.s32MapPanel.BackColor = Color.Black;
            this.s32MapPanel.BorderStyle = BorderStyle.FixedSingle;
            this.s32MapPanel.Controls.Add(this.s32PictureBox);
            this.s32MapPanel.Dock = DockStyle.Fill;
            this.s32MapPanel.Location = new Point(0, 65);
            this.s32MapPanel.Name = "s32MapPanel";
            this.s32MapPanel.Size = new Size(696, 523);
            this.s32MapPanel.TabIndex = 1;

            //
            // s32PictureBox
            //
            this.s32PictureBox.BackColor = Color.Black;
            this.s32PictureBox.Location = new Point(0, 0);
            this.s32PictureBox.Name = "s32PictureBox";
            this.s32PictureBox.Size = new Size(100, 50);
            this.s32PictureBox.SizeMode = PictureBoxSizeMode.AutoSize;
            this.s32PictureBox.TabIndex = 0;
            this.s32PictureBox.TabStop = false;
            this.s32PictureBox.MouseClick += new MouseEventHandler(this.s32PictureBox_MouseClick);
            this.s32PictureBox.MouseDown += new MouseEventHandler(this.s32PictureBox_MouseDown);
            this.s32PictureBox.MouseMove += new MouseEventHandler(this.s32PictureBox_MouseMove);
            this.s32PictureBox.MouseUp += new MouseEventHandler(this.s32PictureBox_MouseUp);
            this.s32PictureBox.Paint += new PaintEventHandler(this.s32PictureBox_Paint);

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
            // rightPanel
            //
            this.rightPanel.BorderStyle = BorderStyle.FixedSingle;
            this.rightPanel.Controls.Add(this.lblTileList);
            this.rightPanel.Controls.Add(this.lvTiles);
            this.rightPanel.Controls.Add(this.lblLayer4Groups);
            this.rightPanel.Controls.Add(this.lvLayer4Groups);
            this.rightPanel.Dock = DockStyle.Right;
            this.rightPanel.Location = new Point(1010, 24);
            this.rightPanel.Name = "rightPanel";
            this.rightPanel.Size = new Size(190, 654);
            this.rightPanel.TabIndex = 6;

            //
            // lblTileList
            //
            this.lblTileList.Location = new Point(5, 5);
            this.lblTileList.Name = "lblTileList";
            this.lblTileList.Size = new Size(180, 20);
            this.lblTileList.TabIndex = 0;
            this.lblTileList.Text = "使用的 Tile";
            this.lblTileList.TextAlign = ContentAlignment.MiddleLeft;

            //
            // lvTiles
            //
            this.lvTiles.Location = new Point(5, 30);
            this.lvTiles.Name = "lvTiles";
            this.lvTiles.Size = new Size(180, 300);
            this.lvTiles.TabIndex = 1;
            this.lvTiles.View = View.LargeIcon;
            this.lvTiles.DoubleClick += new System.EventHandler(this.lvTiles_DoubleClick);

            //
            // lblLayer4Groups
            //
            this.lblLayer4Groups.Location = new Point(5, 340);
            this.lblLayer4Groups.Name = "lblLayer4Groups";
            this.lblLayer4Groups.Size = new Size(180, 20);
            this.lblLayer4Groups.TabIndex = 2;
            this.lblLayer4Groups.Text = "Layer4 物件群組";
            this.lblLayer4Groups.TextAlign = ContentAlignment.MiddleLeft;

            //
            // lvLayer4Groups
            //
            this.lvLayer4Groups.Location = new Point(5, 365);
            this.lvLayer4Groups.Name = "lvLayer4Groups";
            this.lvLayer4Groups.Size = new Size(180, 280);
            this.lvLayer4Groups.TabIndex = 3;
            this.lvLayer4Groups.View = View.Details;
            this.lvLayer4Groups.FullRowSelect = true;
            this.lvLayer4Groups.CheckBoxes = true;
            this.lvLayer4Groups.Columns.Add("GroupId", 60);
            this.lvLayer4Groups.Columns.Add("數量", 50);
            this.lvLayer4Groups.Columns.Add("位置", 65);
            this.lvLayer4Groups.ItemChecked += new ItemCheckedEventHandler(this.lvLayer4Groups_ItemChecked);

            //
            // toolbarPanel
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
            this.toolbarPanel.Dock = DockStyle.Right;
            this.toolbarPanel.Location = new Point(970, 24);
            this.toolbarPanel.Name = "toolbarPanel";
            this.toolbarPanel.Size = new Size(40, 654);
            this.toolbarPanel.TabIndex = 7;

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
            // MapForm
            //
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1400, 700);
            this.Controls.Add(this.toolbarPanel);
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
            this.leftPanel.ResumeLayout(false);
            this.leftPanel.PerformLayout();
            ((ISupportInitialize)this.miniMapPictureBox).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabMapPreview.ResumeLayout(false);
            this.tabS32Editor.ResumeLayout(false);
            this.s32EditorPanel.ResumeLayout(false);
            this.s32LayerControlPanel.ResumeLayout(false);
            this.s32LayerControlPanel.PerformLayout();
            this.s32MapPanel.ResumeLayout(false);
            this.s32MapPanel.PerformLayout();
            ((ISupportInitialize)this.s32PictureBox).EndInit();
            this.panel1.ResumeLayout(false);
            ((ISupportInitialize)this.pictureBox4).EndInit();
            ((ISupportInitialize)this.pictureBox3).EndInit();
            ((ISupportInitialize)this.pictureBox2).EndInit();
            ((ISupportInitialize)this.pictureBox1).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
