using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using L1MapViewer.Other;

namespace L1FlyMapViewer
{
    partial class Form1
    {
        private IContainer components;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem openToolStripMenuItem;
        private ToolStripMenuItem databaseToolStripMenuItem;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel toolStripStatusLabel1;
        public ToolStripProgressBar toolStripProgressBar1;
        public ToolStripStatusLabel toolStripStatusLabel2;
        public ToolStripStatusLabel toolStripStatusLabel3;

        // 左侧面板
        private Panel leftPanel;
        public ComboBox comboBox1;
        private PictureBox miniMapPictureBox;
        private Label labelUsedTil;
        private Label labelMissingTil;
        private ListBox listBoxUsedTil;
        private ListBox listBoxMissingTil;

        // 中间地图显示区域
        public ZoomablePanel panel1;
        public PictureBox pictureBox4;
        public PictureBox pictureBox3;
        public PictureBox pictureBox2;
        public PictureBox pictureBox1;
        public VScrollBar vScrollBar1;
        public HScrollBar hScrollBar1;

        // 右侧面板容器
        private Panel rightPanel;

        // 右侧上层面板（预览新增区）
        private Panel topRightPanel;
        private Button btnAddNew;
        private Button btnXCenter;
        private Button btnYCenter;
        private Button btnRadius;
        private Label labelX1;
        public Label lblX1Value;
        private Label labelY1;
        public Label lblY1Value;
        private Label labelX2;
        public Label lblX2Value;
        private Label labelY2;
        public Label lblY2Value;
        private Label labelSpawnTimeMin;
        public Label lblSpawnTimeMinValue;
        private Label labelSpawnTimeMax;
        public Label lblSpawnTimeMaxValue;
        private Label labelOnScreen;
        public Label lblOnScreenValue;
        private Label labelTeleportBack;
        public Label lblTeleportBackValue;
        private Label labelGroup;
        public Label lblGroupValue;
        private Button btnAddToDatabase;

        // 右侧下层面板 - 怪物配置
        private Panel bottomRightPanel;
        private Label labelMonsterTitle;
        private Button btnReloadSpawnList;
        private CheckBox chkShowAllSpawns;
        private DataGridView dataGridViewMonsters;
        private Button btnNewSpawn;
        private Button btnCopySpawn;
        private Button btnDeleteSpawn;
        private Label labelMonsterId;
        private TextBox txtMonsterId;
        private Button btnSearchMonster;
        private TextBox txtMonsterName;
        private Label labelMonsterNote;
        private TextBox txtMonsterNote;
        private Label labelCustomCount;
        private TextBox txtCustomCount;

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
            this.databaseToolStripMenuItem = new ToolStripMenuItem();

            // StatusStrip
            this.statusStrip1 = new StatusStrip();
            this.toolStripStatusLabel1 = new ToolStripStatusLabel();
            this.toolStripStatusLabel2 = new ToolStripStatusLabel();
            this.toolStripStatusLabel3 = new ToolStripStatusLabel();
            this.toolStripProgressBar1 = new ToolStripProgressBar();

            // 左侧面板
            this.leftPanel = new Panel();
            this.comboBox1 = new ComboBox();
            this.miniMapPictureBox = new PictureBox();
            this.labelUsedTil = new Label();
            this.labelMissingTil = new Label();
            this.listBoxUsedTil = new ListBox();
            this.listBoxMissingTil = new ListBox();

            // 中间地图面板
            this.panel1 = new ZoomablePanel();
            this.pictureBox4 = new PictureBox();
            this.pictureBox3 = new PictureBox();
            this.pictureBox2 = new PictureBox();
            this.pictureBox1 = new PictureBox();
            this.vScrollBar1 = new VScrollBar();
            this.hScrollBar1 = new HScrollBar();

            // 右侧面板容器和子面板
            this.rightPanel = new Panel();
            this.topRightPanel = new Panel();
            this.bottomRightPanel = new Panel();

            // 上层面板控件
            this.btnAddNew = new Button();
            this.btnXCenter = new Button();
            this.btnYCenter = new Button();
            this.btnRadius = new Button();
            this.labelX1 = new Label();
            this.lblX1Value = new Label();
            this.labelY1 = new Label();
            this.lblY1Value = new Label();
            this.labelX2 = new Label();
            this.lblX2Value = new Label();
            this.labelY2 = new Label();
            this.lblY2Value = new Label();
            this.labelSpawnTimeMin = new Label();
            this.lblSpawnTimeMinValue = new Label();
            this.labelSpawnTimeMax = new Label();
            this.lblSpawnTimeMaxValue = new Label();
            this.labelOnScreen = new Label();
            this.lblOnScreenValue = new Label();
            this.labelTeleportBack = new Label();
            this.lblTeleportBackValue = new Label();
            this.labelGroup = new Label();
            this.lblGroupValue = new Label();
            this.btnAddToDatabase = new Button();

            // 下层面板控件
            this.labelMonsterTitle = new Label();
            this.btnReloadSpawnList = new Button();
            this.chkShowAllSpawns = new CheckBox();
            this.dataGridViewMonsters = new DataGridView();
            this.btnNewSpawn = new Button();
            this.btnCopySpawn = new Button();
            this.btnDeleteSpawn = new Button();
            this.labelMonsterId = new Label();
            this.txtMonsterId = new TextBox();
            this.btnSearchMonster = new Button();
            this.txtMonsterName = new TextBox();
            this.labelMonsterNote = new Label();
            this.txtMonsterNote = new TextBox();
            this.labelCustomCount = new Label();
            this.txtCustomCount = new TextBox();

            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.leftPanel.SuspendLayout();
            ((ISupportInitialize)this.miniMapPictureBox).BeginInit();
            this.panel1.SuspendLayout();
            ((ISupportInitialize)this.pictureBox4).BeginInit();
            ((ISupportInitialize)this.pictureBox3).BeginInit();
            ((ISupportInitialize)this.pictureBox2).BeginInit();
            ((ISupportInitialize)this.pictureBox1).BeginInit();
            this.rightPanel.SuspendLayout();
            this.topRightPanel.SuspendLayout();
            this.bottomRightPanel.SuspendLayout();
            ((ISupportInitialize)this.dataGridViewMonsters).BeginInit();
            this.SuspendLayout();

            //
            // menuStrip1
            //
            this.menuStrip1.Items.AddRange(new ToolStripItem[] {
                this.openToolStripMenuItem,
                this.databaseToolStripMenuItem
            });
            this.menuStrip1.Location = new Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new Size(1400, 24);
            this.menuStrip1.TabIndex = 0;

            //
            // openToolStripMenuItem
            //
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new Size(164, 20);
            this.openToolStripMenuItem.Text = "開啟天堂客戶端讀取地圖";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);

            //
            // databaseToolStripMenuItem
            //
            this.databaseToolStripMenuItem.Name = "databaseToolStripMenuItem";
            this.databaseToolStripMenuItem.Size = new Size(88, 20);
            this.databaseToolStripMenuItem.Text = "資料庫設定";
            this.databaseToolStripMenuItem.Click += new System.EventHandler(this.databaseToolStripMenuItem_Click);

            //
            // statusStrip1
            //
            this.statusStrip1.Items.AddRange(new ToolStripItem[] {
                this.toolStripStatusLabel1,
                this.toolStripStatusLabel2,
                this.toolStripStatusLabel3,
                this.toolStripProgressBar1
            });
            this.statusStrip1.Location = new Point(0, 728);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new Size(1400, 22);
            this.statusStrip1.TabIndex = 1;

            //
            // toolStripStatusLabel1
            //
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new Size(280, 17);
            this.toolStripStatusLabel1.Text = "点击获取坐标 | Ctrl+拖拽选择范围 | Ctrl+滾輪縮放";

            //
            // toolStripStatusLabel2
            //
            this.toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            this.toolStripStatusLabel2.Size = new Size(100, 17);

            //
            // toolStripStatusLabel3
            //
            this.toolStripStatusLabel3.Name = "toolStripStatusLabel3";
            this.toolStripStatusLabel3.Size = new Size(1085, 17);
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
            this.leftPanel.Controls.Add(this.labelUsedTil);
            this.leftPanel.Controls.Add(this.listBoxUsedTil);
            this.leftPanel.Controls.Add(this.labelMissingTil);
            this.leftPanel.Controls.Add(this.listBoxMissingTil);
            this.leftPanel.Dock = DockStyle.Left;
            this.leftPanel.Location = new Point(0, 24);
            this.leftPanel.Name = "leftPanel";
            this.leftPanel.Size = new Size(280, 704);
            this.leftPanel.TabIndex = 2;

            //
            // comboBox1
            //
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new Point(10, 10);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new Size(260, 21);
            this.comboBox1.TabIndex = 0;
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);

            //
            // miniMapPictureBox
            //
            this.miniMapPictureBox.BackColor = Color.Black;
            this.miniMapPictureBox.BorderStyle = BorderStyle.FixedSingle;
            this.miniMapPictureBox.Location = new Point(10, 40);
            this.miniMapPictureBox.Name = "miniMapPictureBox";
            this.miniMapPictureBox.Size = new Size(260, 260);
            this.miniMapPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            this.miniMapPictureBox.TabIndex = 1;
            this.miniMapPictureBox.TabStop = false;
            this.miniMapPictureBox.Cursor = Cursors.Hand;
            this.miniMapPictureBox.MouseClick += new MouseEventHandler(this.miniMapPictureBox_MouseClick);

            //
            // labelUsedTil
            //
            this.labelUsedTil.AutoSize = true;
            this.labelUsedTil.Location = new Point(10, 310);
            this.labelUsedTil.Name = "labelUsedTil";
            this.labelUsedTil.Size = new Size(65, 13);
            this.labelUsedTil.TabIndex = 2;
            this.labelUsedTil.Text = "使用的til";

            //
            // listBoxUsedTil
            //
            this.listBoxUsedTil.FormattingEnabled = true;
            this.listBoxUsedTil.Location = new Point(10, 330);
            this.listBoxUsedTil.Name = "listBoxUsedTil";
            this.listBoxUsedTil.Size = new Size(125, 160);
            this.listBoxUsedTil.TabIndex = 3;

            //
            // labelMissingTil
            //
            this.labelMissingTil.AutoSize = true;
            this.labelMissingTil.Location = new Point(145, 310);
            this.labelMissingTil.Name = "labelMissingTil";
            this.labelMissingTil.Size = new Size(65, 13);
            this.labelMissingTil.TabIndex = 4;
            this.labelMissingTil.Text = "遗失的til";

            //
            // listBoxMissingTil
            //
            this.listBoxMissingTil.FormattingEnabled = true;
            this.listBoxMissingTil.Location = new Point(145, 330);
            this.listBoxMissingTil.Name = "listBoxMissingTil";
            this.listBoxMissingTil.Size = new Size(125, 160);
            this.listBoxMissingTil.TabIndex = 5;

            //
            // panel1 (中间地图显示区域)
            //
            this.panel1.BackColor = Color.Black;
            this.panel1.BorderStyle = BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.pictureBox4);
            this.panel1.Controls.Add(this.pictureBox3);
            this.panel1.Controls.Add(this.pictureBox2);
            this.panel1.Controls.Add(this.pictureBox1);
            this.panel1.Location = new Point(290, 34);
            this.panel1.Name = "panel1";
            this.panel1.Size = new Size(700, 600);
            this.panel1.TabIndex = 3;

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
            this.vScrollBar1.Location = new Point(993, 34);
            this.vScrollBar1.Name = "vScrollBar1";
            this.vScrollBar1.Size = new Size(17, 600);
            this.vScrollBar1.TabIndex = 4;
            this.vScrollBar1.Scroll += new ScrollEventHandler(this.vScrollBar1_Scroll);

            //
            // hScrollBar1
            //
            this.hScrollBar1.Location = new Point(290, 637);
            this.hScrollBar1.Name = "hScrollBar1";
            this.hScrollBar1.Size = new Size(700, 17);
            this.hScrollBar1.TabIndex = 5;
            this.hScrollBar1.Scroll += new ScrollEventHandler(this.hScrollBar1_Scroll);

            //
            // rightPanel (右侧容器面板)
            //
            this.rightPanel.BorderStyle = BorderStyle.FixedSingle;
            this.rightPanel.Controls.Add(this.topRightPanel);
            this.rightPanel.Controls.Add(this.bottomRightPanel);
            this.rightPanel.Dock = DockStyle.Right;
            this.rightPanel.Location = new Point(1020, 24);
            this.rightPanel.Name = "rightPanel";
            this.rightPanel.Size = new Size(380, 704);
            this.rightPanel.TabIndex = 6;

            //
            // topRightPanel (预览新增区 - 右侧)
            //
            this.topRightPanel.Controls.Add(this.btnAddNew);
            this.topRightPanel.Controls.Add(this.btnXCenter);
            this.topRightPanel.Controls.Add(this.btnYCenter);
            this.topRightPanel.Controls.Add(this.btnRadius);
            this.topRightPanel.Controls.Add(this.labelX1);
            this.topRightPanel.Controls.Add(this.lblX1Value);
            this.topRightPanel.Controls.Add(this.labelY1);
            this.topRightPanel.Controls.Add(this.lblY1Value);
            this.topRightPanel.Controls.Add(this.labelX2);
            this.topRightPanel.Controls.Add(this.lblX2Value);
            this.topRightPanel.Controls.Add(this.labelY2);
            this.topRightPanel.Controls.Add(this.lblY2Value);
            this.topRightPanel.Controls.Add(this.labelSpawnTimeMin);
            this.topRightPanel.Controls.Add(this.lblSpawnTimeMinValue);
            this.topRightPanel.Controls.Add(this.labelSpawnTimeMax);
            this.topRightPanel.Controls.Add(this.lblSpawnTimeMaxValue);
            this.topRightPanel.Controls.Add(this.labelOnScreen);
            this.topRightPanel.Controls.Add(this.lblOnScreenValue);
            this.topRightPanel.Controls.Add(this.labelTeleportBack);
            this.topRightPanel.Controls.Add(this.lblTeleportBackValue);
            this.topRightPanel.Controls.Add(this.labelGroup);
            this.topRightPanel.Controls.Add(this.lblGroupValue);
            this.topRightPanel.Controls.Add(this.btnAddToDatabase);
            this.topRightPanel.Dock = DockStyle.Fill;
            this.topRightPanel.Location = new Point(235, 0);
            this.topRightPanel.Name = "topRightPanel";
            this.topRightPanel.Size = new Size(145, 704);
            this.topRightPanel.TabIndex = 0;
            this.topRightPanel.BorderStyle = BorderStyle.FixedSingle;
            this.topRightPanel.AutoScroll = true;

            int y = 5;
            int buttonHeight = 28;
            int labelHeight = 25;
            int padding = 3;

            //
            // btnAddNew (预计新增)
            //
            this.btnAddNew.BackColor = Color.FromArgb(0, 120, 215);
            this.btnAddNew.ForeColor = Color.White;
            this.btnAddNew.Location = new Point(3, y);
            this.btnAddNew.Name = "btnAddNew";
            this.btnAddNew.Size = new Size(135, buttonHeight);
            this.btnAddNew.TabIndex = 0;
            this.btnAddNew.Text = "预计新增";
            this.btnAddNew.UseVisualStyleBackColor = false;
            y += buttonHeight + padding;

            //
            // btnXCenter (X心座标)
            //
            this.btnXCenter.BackColor = Color.FromArgb(0, 120, 215);
            this.btnXCenter.ForeColor = Color.White;
            this.btnXCenter.Location = new Point(3, y);
            this.btnXCenter.Name = "btnXCenter";
            this.btnXCenter.Size = new Size(135, buttonHeight);
            this.btnXCenter.TabIndex = 1;
            this.btnXCenter.Text = "X心座标";
            this.btnXCenter.UseVisualStyleBackColor = false;
            y += buttonHeight + padding;

            //
            // btnYCenter (Y心座标)
            //
            this.btnYCenter.BackColor = Color.FromArgb(0, 120, 215);
            this.btnYCenter.ForeColor = Color.White;
            this.btnYCenter.Location = new Point(3, y);
            this.btnYCenter.Name = "btnYCenter";
            this.btnYCenter.Size = new Size(135, buttonHeight);
            this.btnYCenter.TabIndex = 2;
            this.btnYCenter.Text = "Y心座标";
            this.btnYCenter.UseVisualStyleBackColor = false;
            y += buttonHeight + padding;

            //
            // btnRadius (半径)
            //
            this.btnRadius.BackColor = Color.FromArgb(0, 120, 215);
            this.btnRadius.ForeColor = Color.White;
            this.btnRadius.Location = new Point(3, y);
            this.btnRadius.Name = "btnRadius";
            this.btnRadius.Size = new Size(135, buttonHeight);
            this.btnRadius.TabIndex = 3;
            this.btnRadius.Text = "半径";
            this.btnRadius.UseVisualStyleBackColor = false;
            y += buttonHeight + padding;

            //
            // X1座标
            //
            this.labelX1.BackColor = Color.White;
            this.labelX1.BorderStyle = BorderStyle.FixedSingle;
            this.labelX1.Location = new Point(3, y);
            this.labelX1.Name = "labelX1";
            this.labelX1.Size = new Size(135, 20);
            this.labelX1.TabIndex = 4;
            this.labelX1.Text = "X1座标";
            this.labelX1.TextAlign = ContentAlignment.MiddleCenter;
            y += 20;

            this.lblX1Value.BackColor = Color.FromArgb(255, 255, 225);
            this.lblX1Value.BorderStyle = BorderStyle.FixedSingle;
            this.lblX1Value.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            this.lblX1Value.ForeColor = Color.FromArgb(200, 50, 50);
            this.lblX1Value.Location = new Point(3, y);
            this.lblX1Value.Name = "lblX1Value";
            this.lblX1Value.Size = new Size(135, labelHeight);
            this.lblX1Value.TabIndex = 5;
            this.lblX1Value.Text = "";
            this.lblX1Value.TextAlign = ContentAlignment.MiddleCenter;
            y += labelHeight + padding;

            //
            // Y1座标
            //
            this.labelY1.BackColor = Color.White;
            this.labelY1.BorderStyle = BorderStyle.FixedSingle;
            this.labelY1.Location = new Point(3, y);
            this.labelY1.Name = "labelY1";
            this.labelY1.Size = new Size(135, 20);
            this.labelY1.TabIndex = 6;
            this.labelY1.Text = "Y1座标";
            this.labelY1.TextAlign = ContentAlignment.MiddleCenter;
            y += 20;

            this.lblY1Value.BackColor = Color.FromArgb(255, 255, 225);
            this.lblY1Value.BorderStyle = BorderStyle.FixedSingle;
            this.lblY1Value.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            this.lblY1Value.ForeColor = Color.FromArgb(200, 50, 50);
            this.lblY1Value.Location = new Point(3, y);
            this.lblY1Value.Name = "lblY1Value";
            this.lblY1Value.Size = new Size(135, labelHeight);
            this.lblY1Value.TabIndex = 7;
            this.lblY1Value.Text = "";
            this.lblY1Value.TextAlign = ContentAlignment.MiddleCenter;
            y += labelHeight + padding;

            //
            // X2座标
            //
            this.labelX2.BackColor = Color.White;
            this.labelX2.BorderStyle = BorderStyle.FixedSingle;
            this.labelX2.Location = new Point(3, y);
            this.labelX2.Name = "labelX2";
            this.labelX2.Size = new Size(135, 20);
            this.labelX2.TabIndex = 8;
            this.labelX2.Text = "X2座标";
            this.labelX2.TextAlign = ContentAlignment.MiddleCenter;
            y += 20;

            this.lblX2Value.BackColor = Color.FromArgb(255, 255, 225);
            this.lblX2Value.BorderStyle = BorderStyle.FixedSingle;
            this.lblX2Value.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            this.lblX2Value.ForeColor = Color.FromArgb(200, 50, 50);
            this.lblX2Value.Location = new Point(3, y);
            this.lblX2Value.Name = "lblX2Value";
            this.lblX2Value.Size = new Size(135, labelHeight);
            this.lblX2Value.TabIndex = 9;
            this.lblX2Value.Text = "";
            this.lblX2Value.TextAlign = ContentAlignment.MiddleCenter;
            y += labelHeight + padding;

            //
            // Y2座标
            //
            this.labelY2.BackColor = Color.White;
            this.labelY2.BorderStyle = BorderStyle.FixedSingle;
            this.labelY2.Location = new Point(3, y);
            this.labelY2.Name = "labelY2";
            this.labelY2.Size = new Size(135, 20);
            this.labelY2.TabIndex = 10;
            this.labelY2.Text = "Y2座标";
            this.labelY2.TextAlign = ContentAlignment.MiddleCenter;
            y += 20;

            this.lblY2Value.BackColor = Color.FromArgb(255, 255, 225);
            this.lblY2Value.BorderStyle = BorderStyle.FixedSingle;
            this.lblY2Value.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            this.lblY2Value.ForeColor = Color.FromArgb(200, 50, 50);
            this.lblY2Value.Location = new Point(3, y);
            this.lblY2Value.Name = "lblY2Value";
            this.lblY2Value.Size = new Size(135, labelHeight);
            this.lblY2Value.TabIndex = 11;
            this.lblY2Value.Text = "";
            this.lblY2Value.TextAlign = ContentAlignment.MiddleCenter;
            y += labelHeight + padding;

            //
            // 重生时间(Min)
            //
            this.labelSpawnTimeMin.BackColor = Color.White;
            this.labelSpawnTimeMin.BorderStyle = BorderStyle.FixedSingle;
            this.labelSpawnTimeMin.Location = new Point(3, y);
            this.labelSpawnTimeMin.Name = "labelSpawnTimeMin";
            this.labelSpawnTimeMin.Size = new Size(135, 20);
            this.labelSpawnTimeMin.TabIndex = 12;
            this.labelSpawnTimeMin.Text = "重生时间(Min)";
            this.labelSpawnTimeMin.TextAlign = ContentAlignment.MiddleCenter;
            y += 20;

            this.lblSpawnTimeMinValue.BackColor = Color.FromArgb(255, 255, 225);
            this.lblSpawnTimeMinValue.BorderStyle = BorderStyle.FixedSingle;
            this.lblSpawnTimeMinValue.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            this.lblSpawnTimeMinValue.Location = new Point(3, y);
            this.lblSpawnTimeMinValue.Name = "lblSpawnTimeMinValue";
            this.lblSpawnTimeMinValue.Size = new Size(135, labelHeight);
            this.lblSpawnTimeMinValue.TabIndex = 13;
            this.lblSpawnTimeMinValue.Text = "60";
            this.lblSpawnTimeMinValue.TextAlign = ContentAlignment.MiddleCenter;
            y += labelHeight + padding;

            //
            // 重生时间(Max)
            //
            this.labelSpawnTimeMax.BackColor = Color.White;
            this.labelSpawnTimeMax.BorderStyle = BorderStyle.FixedSingle;
            this.labelSpawnTimeMax.Location = new Point(3, y);
            this.labelSpawnTimeMax.Name = "labelSpawnTimeMax";
            this.labelSpawnTimeMax.Size = new Size(135, 20);
            this.labelSpawnTimeMax.TabIndex = 14;
            this.labelSpawnTimeMax.Text = "重生时间(Max)";
            this.labelSpawnTimeMax.TextAlign = ContentAlignment.MiddleCenter;
            y += 20;

            this.lblSpawnTimeMaxValue.BackColor = Color.FromArgb(255, 255, 225);
            this.lblSpawnTimeMaxValue.BorderStyle = BorderStyle.FixedSingle;
            this.lblSpawnTimeMaxValue.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            this.lblSpawnTimeMaxValue.Location = new Point(3, y);
            this.lblSpawnTimeMaxValue.Name = "lblSpawnTimeMaxValue";
            this.lblSpawnTimeMaxValue.Size = new Size(135, labelHeight);
            this.lblSpawnTimeMaxValue.TabIndex = 15;
            this.lblSpawnTimeMaxValue.Text = "120";
            this.lblSpawnTimeMaxValue.TextAlign = ContentAlignment.MiddleCenter;
            y += labelHeight + padding;

            //
            // 画面内是否生怪
            //
            this.labelOnScreen.BackColor = Color.White;
            this.labelOnScreen.BorderStyle = BorderStyle.FixedSingle;
            this.labelOnScreen.Location = new Point(3, y);
            this.labelOnScreen.Name = "labelOnScreen";
            this.labelOnScreen.Size = new Size(135, 20);
            this.labelOnScreen.TabIndex = 16;
            this.labelOnScreen.Text = "画面内是否生怪";
            this.labelOnScreen.TextAlign = ContentAlignment.MiddleCenter;
            y += 20;

            this.lblOnScreenValue.BackColor = Color.FromArgb(255, 255, 225);
            this.lblOnScreenValue.BorderStyle = BorderStyle.FixedSingle;
            this.lblOnScreenValue.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            this.lblOnScreenValue.Location = new Point(3, y);
            this.lblOnScreenValue.Name = "lblOnScreenValue";
            this.lblOnScreenValue.Size = new Size(135, labelHeight);
            this.lblOnScreenValue.TabIndex = 17;
            this.lblOnScreenValue.Text = "0";
            this.lblOnScreenValue.TextAlign = ContentAlignment.MiddleCenter;
            y += labelHeight + padding;

            //
            // 瞬移回原地(距离)
            //
            this.labelTeleportBack.BackColor = Color.White;
            this.labelTeleportBack.BorderStyle = BorderStyle.FixedSingle;
            this.labelTeleportBack.Location = new Point(3, y);
            this.labelTeleportBack.Name = "labelTeleportBack";
            this.labelTeleportBack.Size = new Size(135, 20);
            this.labelTeleportBack.TabIndex = 18;
            this.labelTeleportBack.Text = "瞬移回原地(距离)";
            this.labelTeleportBack.TextAlign = ContentAlignment.MiddleCenter;
            y += 20;

            this.lblTeleportBackValue.BackColor = Color.FromArgb(255, 255, 225);
            this.lblTeleportBackValue.BorderStyle = BorderStyle.FixedSingle;
            this.lblTeleportBackValue.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            this.lblTeleportBackValue.Location = new Point(3, y);
            this.lblTeleportBackValue.Name = "lblTeleportBackValue";
            this.lblTeleportBackValue.Size = new Size(135, labelHeight);
            this.lblTeleportBackValue.TabIndex = 19;
            this.lblTeleportBackValue.Text = "0";
            this.lblTeleportBackValue.TextAlign = ContentAlignment.MiddleCenter;
            y += labelHeight + padding;

            //
            // Group
            //
            this.labelGroup.BackColor = Color.White;
            this.labelGroup.BorderStyle = BorderStyle.FixedSingle;
            this.labelGroup.Location = new Point(3, y);
            this.labelGroup.Name = "labelGroup";
            this.labelGroup.Size = new Size(135, 20);
            this.labelGroup.TabIndex = 20;
            this.labelGroup.Text = "Group";
            this.labelGroup.TextAlign = ContentAlignment.MiddleCenter;
            y += 20;

            this.lblGroupValue.BackColor = Color.FromArgb(255, 255, 225);
            this.lblGroupValue.BorderStyle = BorderStyle.FixedSingle;
            this.lblGroupValue.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            this.lblGroupValue.Location = new Point(3, y);
            this.lblGroupValue.Name = "lblGroupValue";
            this.lblGroupValue.Size = new Size(135, labelHeight);
            this.lblGroupValue.TabIndex = 21;
            this.lblGroupValue.Text = "0";
            this.lblGroupValue.TextAlign = ContentAlignment.MiddleCenter;
            y += labelHeight + padding * 2;

            //
            // btnAddToDatabase (加入资料库)
            //
            this.btnAddToDatabase.BackColor = Color.White;
            this.btnAddToDatabase.Location = new Point(3, y);
            this.btnAddToDatabase.Name = "btnAddToDatabase";
            this.btnAddToDatabase.Size = new Size(135, 28);
            this.btnAddToDatabase.TabIndex = 22;
            this.btnAddToDatabase.Text = "加入资料库";
            this.btnAddToDatabase.UseVisualStyleBackColor = true;
            this.btnAddToDatabase.Click += new System.EventHandler(this.btnAddToDatabase_Click);

            //
            // bottomRightPanel (配置区 - 左侧)
            //
            this.bottomRightPanel.Controls.Add(this.labelMonsterTitle);
            this.bottomRightPanel.Controls.Add(this.btnReloadSpawnList);
            this.bottomRightPanel.Controls.Add(this.chkShowAllSpawns);
            this.bottomRightPanel.Controls.Add(this.dataGridViewMonsters);
            this.bottomRightPanel.Controls.Add(this.btnNewSpawn);
            this.bottomRightPanel.Controls.Add(this.btnCopySpawn);
            this.bottomRightPanel.Controls.Add(this.btnDeleteSpawn);
            this.bottomRightPanel.Controls.Add(this.labelMonsterId);
            this.bottomRightPanel.Controls.Add(this.txtMonsterId);
            this.bottomRightPanel.Controls.Add(this.btnSearchMonster);
            this.bottomRightPanel.Controls.Add(this.txtMonsterName);
            this.bottomRightPanel.Controls.Add(this.labelMonsterNote);
            this.bottomRightPanel.Controls.Add(this.txtMonsterNote);
            this.bottomRightPanel.Controls.Add(this.labelCustomCount);
            this.bottomRightPanel.Controls.Add(this.txtCustomCount);
            this.bottomRightPanel.Dock = DockStyle.Left;
            this.bottomRightPanel.Location = new Point(0, 0);
            this.bottomRightPanel.Name = "bottomRightPanel";
            this.bottomRightPanel.Size = new Size(235, 704);
            this.bottomRightPanel.TabIndex = 1;
            this.bottomRightPanel.AutoScroll = true;
            this.bottomRightPanel.BorderStyle = BorderStyle.FixedSingle;

            int y2 = 5;

            //
            // labelMonsterTitle
            //
            this.labelMonsterTitle.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold);
            this.labelMonsterTitle.Location = new Point(5, y2);
            this.labelMonsterTitle.Name = "labelMonsterTitle";
            this.labelMonsterTitle.Size = new Size(220, 20);
            this.labelMonsterTitle.TabIndex = 0;
            this.labelMonsterTitle.Text = "自前配置怪物清单";
            this.labelMonsterTitle.TextAlign = ContentAlignment.MiddleLeft;
            y2 += 27;

            //
            // btnReloadSpawnList
            //
            this.btnReloadSpawnList.Location = new Point(5, y2);
            this.btnReloadSpawnList.Name = "btnReloadSpawnList";
            this.btnReloadSpawnList.Size = new Size(220, 22);
            this.btnReloadSpawnList.TabIndex = 1;
            this.btnReloadSpawnList.Text = "重新載入當前地圖";
            this.btnReloadSpawnList.UseVisualStyleBackColor = true;
            this.btnReloadSpawnList.Click += new System.EventHandler(this.btnReloadSpawnList_Click);
            y2 += 27;

            //
            // chkShowAllSpawns
            //
            this.chkShowAllSpawns.AutoSize = true;
            this.chkShowAllSpawns.Checked = true;
            this.chkShowAllSpawns.CheckState = CheckState.Checked;
            this.chkShowAllSpawns.Location = new Point(5, y2);
            this.chkShowAllSpawns.Name = "chkShowAllSpawns";
            this.chkShowAllSpawns.Size = new Size(150, 17);
            this.chkShowAllSpawns.TabIndex = 2;
            this.chkShowAllSpawns.Text = "顯示所有怪物位置";
            this.chkShowAllSpawns.UseVisualStyleBackColor = true;
            this.chkShowAllSpawns.CheckedChanged += new System.EventHandler(this.chkShowAllSpawns_CheckedChanged);
            y2 += 22;

            //
            // dataGridViewMonsters
            //
            this.dataGridViewMonsters.AllowUserToAddRows = false;
            this.dataGridViewMonsters.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewMonsters.Location = new Point(5, y2);
            this.dataGridViewMonsters.Name = "dataGridViewMonsters";
            this.dataGridViewMonsters.RowHeadersVisible = false;
            this.dataGridViewMonsters.Size = new Size(220, 150);
            this.dataGridViewMonsters.TabIndex = 2;
            this.dataGridViewMonsters.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewMonsters.MultiSelect = false;
            this.dataGridViewMonsters.ReadOnly = true;
            this.dataGridViewMonsters.SelectionChanged += new System.EventHandler(this.dataGridViewMonsters_SelectionChanged);

            // 添加列 - 簡化顯示
            DataGridViewTextBoxColumn colorColumn = new DataGridViewTextBoxColumn();
            colorColumn.Name = "color";
            colorColumn.HeaderText = "";
            colorColumn.Width = 20;
            colorColumn.ReadOnly = true;
            colorColumn.DefaultCellStyle.SelectionBackColor = Color.Transparent; // 選中時不改變背景色
            this.dataGridViewMonsters.Columns.Add(colorColumn);

            this.dataGridViewMonsters.Columns.Add("id", "ID");
            this.dataGridViewMonsters.Columns.Add("name", "名字");
            this.dataGridViewMonsters.Columns.Add("count", "數量");

            // 設定欄位寬度
            this.dataGridViewMonsters.Columns["id"].Width = 40;
            this.dataGridViewMonsters.Columns["name"].Width = 90;
            this.dataGridViewMonsters.Columns["count"].Width = 50;

            y2 += 155;

            //
            // btnNewSpawn (新增)
            //
            this.btnNewSpawn.Location = new Point(5, y2);
            this.btnNewSpawn.Name = "btnNewSpawn";
            this.btnNewSpawn.Size = new Size(70, 25);
            this.btnNewSpawn.TabIndex = 3;
            this.btnNewSpawn.Text = "新增";
            this.btnNewSpawn.UseVisualStyleBackColor = true;
            this.btnNewSpawn.Click += new System.EventHandler(this.btnNewSpawn_Click);

            //
            // btnCopySpawn (複製)
            //
            this.btnCopySpawn.Location = new Point(80, y2);
            this.btnCopySpawn.Name = "btnCopySpawn";
            this.btnCopySpawn.Size = new Size(70, 25);
            this.btnCopySpawn.TabIndex = 4;
            this.btnCopySpawn.Text = "複製";
            this.btnCopySpawn.UseVisualStyleBackColor = true;
            this.btnCopySpawn.Click += new System.EventHandler(this.btnCopySpawn_Click);

            //
            // btnDeleteSpawn (刪除)
            //
            this.btnDeleteSpawn.Location = new Point(155, y2);
            this.btnDeleteSpawn.Name = "btnDeleteSpawn";
            this.btnDeleteSpawn.Size = new Size(70, 25);
            this.btnDeleteSpawn.TabIndex = 5;
            this.btnDeleteSpawn.Text = "刪除";
            this.btnDeleteSpawn.UseVisualStyleBackColor = true;
            this.btnDeleteSpawn.Click += new System.EventHandler(this.btnDeleteSpawn_Click);
            y2 += 30;

            //
            // labelMonsterId
            //
            this.labelMonsterId.Location = new Point(5, y2);
            this.labelMonsterId.Name = "labelMonsterId";
            this.labelMonsterId.Size = new Size(220, 20);
            this.labelMonsterId.TabIndex = 3;
            this.labelMonsterId.Text = "輸入怪物ID";
            this.labelMonsterId.TextAlign = ContentAlignment.MiddleLeft;
            y2 += 20;

            //
            // txtMonsterId & btnSearchMonster
            //
            this.txtMonsterId.Location = new Point(5, y2);
            this.txtMonsterId.Name = "txtMonsterId";
            this.txtMonsterId.Size = new Size(70, 20);
            this.txtMonsterId.TabIndex = 4;
            this.txtMonsterId.Text = "45217";
            this.txtMonsterId.TextChanged += new System.EventHandler(this.txtMonsterId_TextChanged);

            this.btnSearchMonster.Location = new Point(80, y2);
            this.btnSearchMonster.Name = "btnSearchMonster";
            this.btnSearchMonster.Size = new Size(60, 23);
            this.btnSearchMonster.TabIndex = 5;
            this.btnSearchMonster.Text = "搜尋...";
            this.btnSearchMonster.UseVisualStyleBackColor = true;
            this.btnSearchMonster.Click += new System.EventHandler(this.btnSearchMonster_Click);
            y2 += 27;

            //
            // txtMonsterName
            //
            this.txtMonsterName.Location = new Point(5, y2);
            this.txtMonsterName.Name = "txtMonsterName";
            this.txtMonsterName.Size = new Size(220, 20);
            this.txtMonsterName.TabIndex = 6;
            this.txtMonsterName.ReadOnly = true;
            this.txtMonsterName.BackColor = Color.WhiteSmoke;
            y2 += 27;

            //
            // labelMonsterNote
            //
            this.labelMonsterNote.Location = new Point(5, y2);
            this.labelMonsterNote.Name = "labelMonsterNote";
            this.labelMonsterNote.Size = new Size(220, 20);
            this.labelMonsterNote.TabIndex = 7;
            this.labelMonsterNote.Text = "怪物備註";
            this.labelMonsterNote.TextAlign = ContentAlignment.MiddleLeft;
            y2 += 20;

            //
            // txtMonsterNote
            //
            this.txtMonsterNote.Location = new Point(5, y2);
            this.txtMonsterNote.Name = "txtMonsterNote";
            this.txtMonsterNote.Size = new Size(220, 20);
            this.txtMonsterNote.TabIndex = 8;
            y2 += 27;

            //
            // labelCustomCount
            //
            this.labelCustomCount.Location = new Point(5, y2);
            this.labelCustomCount.Name = "labelCustomCount";
            this.labelCustomCount.Size = new Size(220, 20);
            this.labelCustomCount.TabIndex = 9;
            this.labelCustomCount.Text = "怪物數量";
            this.labelCustomCount.TextAlign = ContentAlignment.MiddleLeft;
            y2 += 20;

            //
            // txtCustomCount
            //
            this.txtCustomCount.Location = new Point(5, y2);
            this.txtCustomCount.Name = "txtCustomCount";
            this.txtCustomCount.Size = new Size(220, 20);
            this.txtCustomCount.TabIndex = 10;
            this.txtCustomCount.Text = "1";
            y2 += 27;

            //
            // Form1
            //
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1400, 750);
            this.Controls.Add(this.rightPanel);
            this.Controls.Add(this.hScrollBar1);
            this.Controls.Add(this.vScrollBar1);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.leftPanel);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "L1MapMonster - 地圖怪物設定工具";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.leftPanel.ResumeLayout(false);
            this.leftPanel.PerformLayout();
            ((ISupportInitialize)this.miniMapPictureBox).EndInit();
            this.panel1.ResumeLayout(false);
            ((ISupportInitialize)this.pictureBox4).EndInit();
            ((ISupportInitialize)this.pictureBox3).EndInit();
            ((ISupportInitialize)this.pictureBox2).EndInit();
            ((ISupportInitialize)this.pictureBox1).EndInit();
            this.rightPanel.ResumeLayout(false);
            this.topRightPanel.ResumeLayout(false);
            this.bottomRightPanel.ResumeLayout(false);
            this.bottomRightPanel.PerformLayout();
            ((ISupportInitialize)this.dataGridViewMonsters).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
