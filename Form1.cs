// Decompiled with JetBrains decompiler
// Type: L1MapViewer.Form1
// Assembly: L1MapViewer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 947D87B5-34AD-4966-80C3-B36E112901DC
// Assembly location: C:\workspaces\lineage\L1MapViewer\建置好的檔案\L1MapViewer.exe

using L1MapViewer.Helper;
using L1MapViewer.Other;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using L1MapViewer;

namespace L1FlyMapViewer
{
    public partial class Form1 : Form, IMapViewer
    {
        // IMapViewer 介面實作 - 明確公開控制項屬性
        ComboBox IMapViewer.comboBox1 => this.comboBox1;
        PictureBox IMapViewer.pictureBox1 => this.pictureBox1;
        PictureBox IMapViewer.pictureBox2 => this.pictureBox2;
        PictureBox IMapViewer.pictureBox3 => this.pictureBox3;
        PictureBox IMapViewer.pictureBox4 => this.pictureBox4;
        VScrollBar IMapViewer.vScrollBar1 => this.vScrollBar1;
        HScrollBar IMapViewer.hScrollBar1 => this.hScrollBar1;
        ToolStripProgressBar IMapViewer.toolStripProgressBar1 => this.toolStripProgressBar1;
        ToolStripStatusLabel IMapViewer.toolStripStatusLabel1 => this.toolStripStatusLabel1;
        ToolStripStatusLabel IMapViewer.toolStripStatusLabel2 => this.toolStripStatusLabel2;
        ToolStripStatusLabel IMapViewer.toolStripStatusLabel3 => this.toolStripStatusLabel3;
        Panel IMapViewer.panel1 => this.panel1;

        private static Form1 instance;
        private Point mouseDownPoint;
        private Point mouseDownMapPoint;  // 地图上的点击起始位置
        private bool isMouseDrag;
        private bool isSelecting;  // 是否正在选择范围
        private Point selectionStart;  // 选择起点（地图坐标）
        private Point selectionEnd;    // 选择终点（地图坐标）
        private Rectangle selectionRect;  // 选择矩形（屏幕坐标）
        private const int DRAG_THRESHOLD = 5;  // 拖拽阈值（像素）

        // 編輯模式
        private int? editingSpawnId = null;  // 正在編輯的 spawn ID
        private bool isLoadingData = false;  // 是否正在載入資料
        private DateTime lastPreviewUpdate = DateTime.MinValue;  // 上次預覽更新時間
        private const int PREVIEW_UPDATE_INTERVAL_MS = 200;  // 預覽更新間隔（毫秒）

        // 怪物顏色緩存
        private Dictionary<int, Color> monsterColors = new Dictionary<int, Color>();

        // 當前地圖的所有 spawn 資料
        private class SpawnData
        {
            public int Id { get; set; }
            public int NpcTemplateId { get; set; }
            public string Name { get; set; }
            public int Count { get; set; }
            public int LocX { get; set; }
            public int LocY { get; set; }
            public int RandomX { get; set; }
            public int RandomY { get; set; }
            public int LocX1 { get; set; }
            public int LocY1 { get; set; }
            public int LocX2 { get; set; }
            public int LocY2 { get; set; }
        }
        private List<SpawnData> currentSpawnList = new List<SpawnData>();

        // 縮放相關
        public double zoomLevel { get; set; } = 1.0;  // 當前縮放級別
        private const double ZOOM_MIN = 0.1;  // 最小縮放
        private const double ZOOM_MAX = 5.0;  // 最大縮放
        private const double ZOOM_STEP = 0.2;  // 縮放步進（增大以提升性能）
        private Image originalMapImage;  // 原始地圖圖片

        public static Form1 Get() => Form1.instance;

        public Form1()
        {
            this.InitializeComponent();
            Form1.instance = this;

            // 启动时自动加载上次的资料夹
            this.Load += Form1_Load;

            // 註冊滑鼠滾輪事件用於縮放
            this.panel1.MouseWheel += Panel1_MouseWheel;

            // 確保 panel1 可以接收焦點
            this.panel1.TabStop = true;

            // 當滑鼠進入 panel1 時自動取得焦點
            this.panel1.MouseEnter += (s, e) => this.panel1.Focus();

            // 設置 PictureBox 的 SizeMode 為 StretchImage，讓圖片拉伸填滿容器
            this.pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            this.pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            this.pictureBox3.SizeMode = PictureBoxSizeMode.StretchImage;
            this.pictureBox4.SizeMode = PictureBoxSizeMode.StretchImage;

            // 註冊 DataGridView 的 CellFormatting 事件，確保顏色格保持原色
            this.dataGridViewMonsters.CellFormatting += DataGridViewMonsters_CellFormatting;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string iniPath = Path.GetTempPath() + "mapviewer.ini";
            if (File.Exists(iniPath))
            {
                string savedPath = Utils.GetINI("Path", "LineagePath", "", iniPath);
                if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
                {
                    this.toolStripStatusLabel3.Text = savedPath;
                    Share.LineagePath = savedPath;
                    try
                    {
                        this.LoadMap(savedPath);
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }

            // 自動連線到最後使用的資料庫
            try
            {
                if (L1MapViewer.Helper.DatabaseHelper.AutoConnectToLastUsed())
                {
                    // 如果有選擇地圖，載入該地圖的 spawnlist
                    if (this.comboBox1.SelectedItem != null)
                    {
                        LoadSpawnListForCurrentMap();
                    }
                }
            }
            catch (Exception ex)
            {
                // 自動連線失敗，忽略
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "請選擇天堂資料夾";
                folderDialog.ShowNewFolderButton = false;

                string str = Path.GetTempPath() + "mapviewer.ini";
                if (File.Exists(str))
                {
                    string savedPath = Utils.GetINI("Path", "LineagePath", "", str);
                    if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
                        folderDialog.SelectedPath = savedPath;
                }
                else
                {
                    folderDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                }

                if (folderDialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(folderDialog.SelectedPath))
                    return;

                this.toolStripStatusLabel3.Text = folderDialog.SelectedPath;
                Share.LineagePath = folderDialog.SelectedPath;
                // 保存选择的路径（保存完整路径，而不是父目录）
                Utils.WriteINI("Path", "LineagePath", folderDialog.SelectedPath, str);
                this.LoadMap(folderDialog.SelectedPath);
            }
        }

        public void LoadMap(string selectedPath)
        {
            Utils.ShowProgressBar(true, this);
            Dictionary<string, Struct.L1Map> dictionary = L1MapHelper.Read(selectedPath);
            this.comboBox1.Items.Clear();
            this.comboBox1.BeginUpdate();
            foreach (string key in Utils.SortAsc((ICollection)dictionary.Keys))
            {
                Struct.L1Map l1Map = dictionary[key];
                this.comboBox1.Items.Add((object)string.Format("{0}-{1}", (object)key, (object)l1Map.szName));
            }
            this.comboBox1.EndUpdate();
            this.comboBox1.SelectedIndex = 5;
            this.toolStripStatusLabel2.Text = "MapCount=" + (object)dictionary.Count;
            Utils.ShowProgressBar(false, this);
        }

        // 更新小地图
        private void UpdateMiniMap()
        {
            try
            {
                if (this.pictureBox1.Image == null)
                    return;

                // 创建小地图的缩略图
                int miniWidth = 260;
                int miniHeight = 260;

                Bitmap miniMap = new Bitmap(miniWidth, miniHeight);
                using (Graphics g = Graphics.FromImage(miniMap))
                {
                    // 设置高质量缩放
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                    // 计算缩放比例，保持宽高比
                    float scaleX = (float)miniWidth / this.pictureBox1.Image.Width;
                    float scaleY = (float)miniHeight / this.pictureBox1.Image.Height;
                    float scale = Math.Min(scaleX, scaleY);

                    int scaledWidth = (int)(this.pictureBox1.Image.Width * scale);
                    int scaledHeight = (int)(this.pictureBox1.Image.Height * scale);
                    int offsetX = (miniWidth - scaledWidth) / 2;
                    int offsetY = (miniHeight - scaledHeight) / 2;

                    // 绘制背景
                    g.FillRectangle(Brushes.Black, 0, 0, miniWidth, miniHeight);

                    // 绘制缩小的地图
                    g.DrawImage(this.pictureBox1.Image,
                        offsetX, offsetY, scaledWidth, scaledHeight);

                    // 绘制当前视口位置（红色矩形）
                    if (this.panel1.Width > 0 && this.panel1.Height > 0 && this.pictureBox1.Width > 0 && this.pictureBox1.Height > 0)
                    {
                        // 使用縮放後的 pictureBox 大小來計算視口比例
                        float viewPortScaleX = (float)scaledWidth / this.pictureBox1.Width;
                        float viewPortScaleY = (float)scaledHeight / this.pictureBox1.Height;

                        int viewX = (int)(this.hScrollBar1.Value * viewPortScaleX) + offsetX;
                        int viewY = (int)(this.vScrollBar1.Value * viewPortScaleY) + offsetY;
                        int viewWidth = (int)(this.panel1.Width * viewPortScaleX);
                        int viewHeight = (int)(this.panel1.Height * viewPortScaleY);

                        using (Pen viewPortPen = new Pen(Color.Red, 2))
                        {
                            g.DrawRectangle(viewPortPen, viewX, viewY, viewWidth, viewHeight);
                        }
                    }
                }

                // 更新小地图显示
                if (this.miniMapPictureBox.Image != null)
                    this.miniMapPictureBox.Image.Dispose();
                this.miniMapPictureBox.Image = miniMap;
            }
            catch
            {
                // 忽略错误
            }
        }

        public void vScrollBar1_Scroll(object sender, ScrollEventArgs e)
        {
            this.pictureBox1.Top = -this.vScrollBar1.Value;
            // 拖拽时跳过更新小地图，提升性能
            if (!this.isMouseDrag)
                UpdateMiniMap();
        }

        public void hScrollBar1_Scroll(object sender, ScrollEventArgs e)
        {
            this.pictureBox1.Left = -this.hScrollBar1.Value;
            // 拖拽时跳过更新小地图，提升性能
            if (!this.isMouseDrag)
                UpdateMiniMap();
        }

        private void pictureBox2_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.mouseDownPoint = Cursor.Position;
                this.mouseDownMapPoint = e.Location;

                // 按住 Ctrl 键进行范围选择
                if (ModifierKeys == Keys.Control)
                {
                    this.isSelecting = true;
                    // 調整座標以考慮縮放
                    int adjustedX = (int)(e.X / this.zoomLevel);
                    int adjustedY = (int)(e.Y / this.zoomLevel);
                    var linLoc = L1MapHelper.GetLinLocation(adjustedX, adjustedY);
                    if (linLoc != null)
                    {
                        this.selectionStart = new Point(linLoc.x, linLoc.y);
                        this.selectionRect = new Rectangle(e.X, e.Y, 0, 0);
                    }
                    // 重置預覽時間戳
                    lastPreviewUpdate = DateTime.MinValue;
                    this.Cursor = Cursors.Hand;
                }
                else
                {
                    // 开始拖拽
                    this.isMouseDrag = true;
                    this.Cursor = Cursors.Hand;
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                L1MapHelper.doLocTagEvent(e, this);
            }
        }

        private void pictureBox2_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            this.Cursor = Cursors.Default;

            // 处理范围选择结束
            if (this.isSelecting)
            {
                this.isSelecting = false;
                // 調整座標以考慮縮放
                int adjustedX = (int)(e.X / this.zoomLevel);
                int adjustedY = (int)(e.Y / this.zoomLevel);
                var linLoc = L1MapHelper.GetLinLocation(adjustedX, adjustedY);
                if (linLoc != null)
                {
                    this.selectionEnd = new Point(linLoc.x, linLoc.y);

                    // 如果在編輯模式，填充到 x1,y1,x2,y2 欄位
                    if (editingSpawnId.HasValue)
                    {
                        int minX = Math.Min(this.selectionStart.X, this.selectionEnd.X);
                        int maxX = Math.Max(this.selectionStart.X, this.selectionEnd.X);
                        int minY = Math.Min(this.selectionStart.Y, this.selectionEnd.Y);
                        int maxY = Math.Max(this.selectionStart.Y, this.selectionEnd.Y);

                        lblX1Value.Text = minX.ToString();
                        lblY1Value.Text = minY.ToString();
                        lblX2Value.Text = maxX.ToString();
                        lblY2Value.Text = maxY.ToString();

                        // 重新繪製最終標記（不透明）
                        int count = int.Parse(txtCustomCount.Text);
                        string name = txtMonsterName.Text;
                        int npcTemplateId = int.Parse(txtMonsterId.Text);
                        DrawSpawnMarker(0, 0, minX, minY, maxX, maxY, 0, 0, count, name, npcTemplateId);

                        this.toolStripStatusLabel1.Text = $"已設定範圍: [{minX},{minY}]-[{maxX},{maxY}]";
                    }
                    else
                    {
                        ShowSelectionRange();
                    }
                }
                // 清除选择框
                this.pictureBox2.Invalidate();
            }
            // 处理点击（没有拖拽）
            else if (this.isMouseDrag)
            {
                int dragDistance = Math.Abs(Cursor.Position.X - this.mouseDownPoint.X) +
                                  Math.Abs(Cursor.Position.Y - this.mouseDownPoint.Y);

                // 如果移动距离小于阈值，视为点击
                if (dragDistance < DRAG_THRESHOLD)
                {
                    // 調整座標以考慮縮放
                    int adjustedX = (int)(e.X / this.zoomLevel);
                    int adjustedY = (int)(e.Y / this.zoomLevel);
                    var linLoc = L1MapHelper.GetLinLocation(adjustedX, adjustedY);
                    if (linLoc != null)
                    {
                        ShowSinglePoint(linLoc.x, linLoc.y);
                    }
                }

                // 拖拽结束后更新小地图（不管距离多少都要更新）
                UpdateMiniMap();
                this.isMouseDrag = false;
            }
        }

        // 滑鼠滾輪縮放地圖（需按住 Ctrl 鍵）
        private void Panel1_MouseWheel(object sender, MouseEventArgs e)
        {
            // 檢查是否按住 Ctrl 鍵
            if (Control.ModifierKeys != Keys.Control)
                return;

            if (this.pictureBox1.Image == null)
                return;

            // 計算新的縮放級別
            double oldZoom = zoomLevel;
            if (e.Delta > 0)
            {
                // 向上滾動，放大
                zoomLevel = Math.Min(ZOOM_MAX, zoomLevel + ZOOM_STEP);
            }
            else
            {
                // 向下滾動，縮小
                zoomLevel = Math.Max(ZOOM_MIN, zoomLevel - ZOOM_STEP);
            }

            // 如果縮放級別沒有變化，直接返回
            if (Math.Abs(oldZoom - zoomLevel) < 0.001)
                return;

            // 保存原始圖片（第一次縮放時）
            if (originalMapImage == null)
            {
                originalMapImage = (Image)this.pictureBox1.Image.Clone();
            }

            // 暫停繪圖以提升性能
            this.panel1.SuspendLayout();

            try
            {
                // 取得滑鼠在 panel1 中的位置
                Point mousePos = this.panel1.PointToClient(Cursor.Position);

                // 計算滑鼠在圖片中的比例位置
                double xRatio = (double)(mousePos.X + this.hScrollBar1.Value) / this.pictureBox1.Width;
                double yRatio = (double)(mousePos.Y + this.vScrollBar1.Value) / this.pictureBox1.Height;

                // 計算新的圖片大小
                int newWidth = (int)(originalMapImage.Width * zoomLevel);
                int newHeight = (int)(originalMapImage.Height * zoomLevel);

                // 批量調整所有 PictureBox 的大小
                this.pictureBox1.Size = new Size(newWidth, newHeight);
                this.pictureBox2.Size = new Size(newWidth, newHeight);
                this.pictureBox3.Size = new Size(newWidth, newHeight);
                this.pictureBox4.Size = new Size(newWidth, newHeight);

                // 更新滾動條
                this.hScrollBar1.Maximum = Math.Max(0, newWidth);
                this.vScrollBar1.Maximum = Math.Max(0, newHeight);

                // 根據滑鼠位置調整滾動條，使縮放中心在滑鼠位置
                int newScrollX = (int)(newWidth * xRatio - mousePos.X);
                int newScrollY = (int)(newHeight * yRatio - mousePos.Y);

                this.hScrollBar1.Value = Math.Max(0, Math.Min(this.hScrollBar1.Maximum - this.panel1.Width, newScrollX));
                this.vScrollBar1.Value = Math.Max(0, Math.Min(this.vScrollBar1.Maximum - this.panel1.Height, newScrollY));

                // 觸發滾動條事件來更新顯示
                this.vScrollBar1_Scroll(null, null);
                this.hScrollBar1_Scroll(null, null);
            }
            finally
            {
                // 恢復繪圖
                this.panel1.ResumeLayout();
            }

            // 只刷新一次 panel
            this.panel1.Invalidate();
        }

        private void pictureBox2_MouseMove(object sender, MouseEventArgs e)
        {
            // 范围选择模式
            if (this.isSelecting)
            {
                // 更新选择矩形
                int x = Math.Min(this.mouseDownMapPoint.X, e.X);
                int y = Math.Min(this.mouseDownMapPoint.Y, e.Y);
                int width = Math.Abs(e.X - this.mouseDownMapPoint.X);
                int height = Math.Abs(e.Y - this.mouseDownMapPoint.Y);
                this.selectionRect = new Rectangle(x, y, width, height);

                // 重绘选择框
                this.pictureBox2.Invalidate();

                // 显示当前范围
                // 調整座標以考慮縮放
                int adjustedX = (int)(e.X / this.zoomLevel);
                int adjustedY = (int)(e.Y / this.zoomLevel);
                var linLoc = L1MapHelper.GetLinLocation(adjustedX, adjustedY);
                if (linLoc != null)
                {
                    this.toolStripStatusLabel2.Text = string.Format("[{0},{1}]-[{2},{3}]",
                        this.selectionStart.X, this.selectionStart.Y, linLoc.x, linLoc.y);

                    // 如果在編輯模式，顯示預覽（限制更新頻率）
                    if (editingSpawnId.HasValue)
                    {
                        TimeSpan timeSinceLastUpdate = DateTime.Now - lastPreviewUpdate;
                        if (timeSinceLastUpdate.TotalMilliseconds >= PREVIEW_UPDATE_INTERVAL_MS)
                        {
                            lastPreviewUpdate = DateTime.Now;
                            PreviewSelectionArea(this.selectionStart, new Point(linLoc.x, linLoc.y));
                        }
                    }
                }
            }
            // 拖拽模式
            else if (this.isMouseDrag)
            {
                try
                {
                    // 计算新的滚动位置
                    int deltaX = Cursor.Position.X - this.mouseDownPoint.X;
                    int deltaY = Cursor.Position.Y - this.mouseDownPoint.Y;

                    int newScrollX = this.hScrollBar1.Value - deltaX;
                    int newScrollY = this.vScrollBar1.Value - deltaY;

                    // 限制在有效范围内
                    if (this.hScrollBar1.Maximum > 0)
                    {
                        newScrollX = Math.Max(this.hScrollBar1.Minimum, Math.Min(newScrollX, this.hScrollBar1.Maximum));
                        this.hScrollBar1.Value = newScrollX;
                    }

                    if (this.vScrollBar1.Maximum > 0)
                    {
                        newScrollY = Math.Max(this.vScrollBar1.Minimum, Math.Min(newScrollY, this.vScrollBar1.Maximum));
                        this.vScrollBar1.Value = newScrollY;
                    }

                    // 更新显示
                    this.vScrollBar1_Scroll(null, null);
                    this.hScrollBar1_Scroll(null, null);

                    // 更新鼠标位置
                    this.mouseDownPoint = Cursor.Position;
                }
                catch
                {
                    // 忽略错误
                }
            }
            // 普通移动，显示坐标
            else
            {
                L1MapHelper.doMouseMoveEvent(e, this);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.comboBox1.SelectedItem == null)
                return;

            // 重置縮放級別
            zoomLevel = 1.0;
            if (originalMapImage != null)
            {
                originalMapImage.Dispose();
                originalMapImage = null;
            }

            // 清除編輯狀態
            ClearEditMode();

            string szSelectName = this.comboBox1.SelectedItem.ToString();
            if (szSelectName.Contains("-"))
                szSelectName = szSelectName.Split('-')[0].Trim();
            L1MapHelper.doPaintEvent(szSelectName, this);

            // 等待地图绘制完成后更新小地图
            Application.DoEvents();
            UpdateMiniMap();

            // 如果已連線到資料庫，載入該地圖的 spawnlist 資料
            if (L1MapViewer.Helper.DatabaseHelper.IsConnected)
            {
                LoadSpawnListForCurrentMap();
            }
        }

        // 清除編輯模式
        private void ClearEditMode()
        {
            editingSpawnId = null;
            btnAddToDatabase.Text = "加入资料库";

            // 清除地圖標記（如果有勾選顯示所有怪物，則重新繪製所有怪物）
            if (this.pictureBox4.Image != null)
            {
                if (chkShowAllSpawns.Checked)
                {
                    DrawAllSpawnsOnMap();
                }
                else
                {
                    using (Graphics g = Graphics.FromImage(this.pictureBox4.Image))
                    {
                        g.Clear(Color.Transparent);
                    }
                    this.pictureBox4.Refresh();
                }
            }
        }

        // 根據怪物ID生成一致的柔和顏色
        private Color GetMonsterColor(int npcTemplateId)
        {
            if (monsterColors.ContainsKey(npcTemplateId))
                return monsterColors[npcTemplateId];

            // 使用黃金角度分佈色相，確保相鄰ID顏色差異明顯
            // 黃金角度約 137.5 度，可以讓顏色均勻分散
            const double goldenAngle = 137.5077640500378;
            double hue = (npcTemplateId * goldenAngle) % 360;

            // 使用怪物ID作為種子來生成飽和度和亮度
            Random rand = new Random(npcTemplateId);

            // 使用更高的飽和度和合適的亮度，確保顏色鮮明
            double saturation = 0.65 + rand.NextDouble() * 0.35; // 0.65-1.0 (高飽和度)
            double lightness = 0.40 + rand.NextDouble() * 0.25; // 0.40-0.65 (中等亮度)

            // HSL 轉 RGB
            Color color = HslToRgb(hue, saturation, lightness);
            monsterColors[npcTemplateId] = color;
            return color;
        }

        // HSL 轉 RGB
        private Color HslToRgb(double h, double s, double l)
        {
            h = h / 360.0;
            double r, g, b;

            if (s == 0)
            {
                r = g = b = l;
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = HueToRgb(p, q, h + 1.0 / 3.0);
                g = HueToRgb(p, q, h);
                b = HueToRgb(p, q, h - 1.0 / 3.0);
            }

            return Color.FromArgb(255, (int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }

        // 預覽選擇區域
        private void PreviewSelectionArea(Point start, Point end)
        {
            try
            {
                if (string.IsNullOrEmpty(txtMonsterId.Text) || string.IsNullOrEmpty(txtCustomCount.Text))
                    return;

                int minX = Math.Min(start.X, end.X);
                int maxX = Math.Max(start.X, end.X);
                int minY = Math.Min(start.Y, end.Y);
                int maxY = Math.Max(start.Y, end.Y);

                int count = int.Parse(txtCustomCount.Text);
                string name = txtMonsterName.Text;
                int npcTemplateId = int.Parse(txtMonsterId.Text);

                // 取得怪物顏色
                Color color = GetMonsterColor(npcTemplateId);

                // 繪製預覽標記（使用半透明顏色）
                DrawPreviewMarker(minX, minY, maxX, maxY, count, name, color);
            }
            catch
            {
                // 忽略錯誤
            }
        }

        // 繪製預覽標記（半透明）
        private void DrawPreviewMarker(int minX, int minY, int maxX, int maxY, int count, string name, Color color)
        {
            try
            {
                if (this.pictureBox4.Image == null)
                    return;

                using (Graphics g = Graphics.FromImage(this.pictureBox4.Image))
                {
                    g.Clear(Color.Transparent);

                    // 四個角點的遊戲座標
                    var corner1 = L1MapHelper.GetLinLocationByCoords(minX, minY);
                    var corner2 = L1MapHelper.GetLinLocationByCoords(maxX, minY);
                    var corner3 = L1MapHelper.GetLinLocationByCoords(maxX, maxY);
                    var corner4 = L1MapHelper.GetLinLocationByCoords(minX, maxY);

                    if (corner1 != null && corner2 != null && corner3 != null && corner4 != null)
                    {
                        var bounds1 = corner1.region.GetBounds(g);
                        var bounds2 = corner2.region.GetBounds(g);
                        var bounds3 = corner3.region.GetBounds(g);
                        var bounds4 = corner4.region.GetBounds(g);

                        Point p1 = new Point((int)(bounds1.X + bounds1.Width / 2), (int)(bounds1.Y + bounds1.Height / 2));
                        Point p2 = new Point((int)(bounds2.X + bounds2.Width / 2), (int)(bounds2.Y + bounds2.Height / 2));
                        Point p3 = new Point((int)(bounds3.X + bounds3.Width / 2), (int)(bounds3.Y + bounds3.Height / 2));
                        Point p4 = new Point((int)(bounds4.X + bounds4.Width / 2), (int)(bounds4.Y + bounds4.Height / 2));

                        // 繪製半透明菱形（使用怪物的顏色）
                        using (Pen pen = new Pen(Color.FromArgb(180, color), 3))
                        {
                            Point[] points = new Point[] { p1, p2, p3, p4 };
                            g.DrawPolygon(pen, points);
                        }

                        // 繪製文字
                        int centerX = (p1.X + p2.X + p3.X + p4.X) / 4;
                        int centerY = (p1.Y + p2.Y + p3.Y + p4.Y) / 4;
                        DrawMarkerText(g, centerX, centerY, count, name);
                    }
                }

                this.pictureBox4.Refresh();
            }
            catch
            {
                // 忽略錯誤
            }
        }

        // 显示单点坐标
        private void ShowSinglePoint(int x, int y)
        {
            string coords = string.Format("{0},{1}", x, y);
            this.toolStripStatusLabel2.Text = coords;

            // 复制到剪贴板
            try
            {
                Clipboard.SetText(coords);
                this.toolStripStatusLabel1.Text = "已复制: " + coords;
            }
            catch
            {
                this.toolStripStatusLabel1.Text = coords;
            }
        }

        // 显示范围坐标
        private void ShowSelectionRange()
        {
            int x1 = Math.Min(this.selectionStart.X, this.selectionEnd.X);
            int y1 = Math.Min(this.selectionStart.Y, this.selectionEnd.Y);
            int x2 = Math.Max(this.selectionStart.X, this.selectionEnd.X);
            int y2 = Math.Max(this.selectionStart.Y, this.selectionEnd.Y);

            string rangeText = string.Format("[{0},{1}]-[{2},{3}]", x1, y1, x2, y2);
            this.toolStripStatusLabel2.Text = rangeText;

            // 复制到剪贴板
            try
            {
                Clipboard.SetText(rangeText);
                this.toolStripStatusLabel1.Text = "已复制范围: " + rangeText;
            }
            catch
            {
                this.toolStripStatusLabel1.Text = rangeText;
            }
        }

        // 绘制选择框（45度投影的菱形）
        private void pictureBox2_Paint(object sender, PaintEventArgs e)
        {
            if (this.isSelecting && this.selectionStart != Point.Empty)
            {
                try
                {
                    // 取得當前滑鼠位置的遊戲座標
                    Point currentScreenPos = this.pictureBox2.PointToClient(Cursor.Position);
                    int adjustedX = (int)(currentScreenPos.X / this.zoomLevel);
                    int adjustedY = (int)(currentScreenPos.Y / this.zoomLevel);
                    var currentLinLoc = L1MapHelper.GetLinLocation(adjustedX, adjustedY);

                    if (currentLinLoc != null)
                    {
                        // 計算矩形的四個角點
                        int minX = Math.Min(this.selectionStart.X, currentLinLoc.x);
                        int maxX = Math.Max(this.selectionStart.X, currentLinLoc.x);
                        int minY = Math.Min(this.selectionStart.Y, currentLinLoc.y);
                        int maxY = Math.Max(this.selectionStart.Y, currentLinLoc.y);

                        // 四個角點的遊戲座標
                        var corner1 = L1MapHelper.GetLinLocationByCoords(minX, minY);
                        var corner2 = L1MapHelper.GetLinLocationByCoords(maxX, minY);
                        var corner3 = L1MapHelper.GetLinLocationByCoords(maxX, maxY);
                        var corner4 = L1MapHelper.GetLinLocationByCoords(minX, maxY);

                        if (corner1 != null && corner2 != null && corner3 != null && corner4 != null)
                        {
                            var bounds1 = corner1.region.GetBounds(e.Graphics);
                            var bounds2 = corner2.region.GetBounds(e.Graphics);
                            var bounds3 = corner3.region.GetBounds(e.Graphics);
                            var bounds4 = corner4.region.GetBounds(e.Graphics);

                            // 取每個格子的中心點
                            Point p1 = new Point((int)(bounds1.X + bounds1.Width / 2), (int)(bounds1.Y + bounds1.Height / 2));
                            Point p2 = new Point((int)(bounds2.X + bounds2.Width / 2), (int)(bounds2.Y + bounds2.Height / 2));
                            Point p3 = new Point((int)(bounds3.X + bounds3.Width / 2), (int)(bounds3.Y + bounds3.Height / 2));
                            Point p4 = new Point((int)(bounds4.X + bounds4.Width / 2), (int)(bounds4.Y + bounds4.Height / 2));

                            // 繪製菱形選擇框
                            // 如果在編輯模式，使用怪物的顏色；否則使用黃色
                            Color penColor = Color.Yellow;
                            if (editingSpawnId.HasValue && !string.IsNullOrEmpty(txtMonsterId.Text))
                            {
                                try
                                {
                                    int npcTemplateId = int.Parse(txtMonsterId.Text);
                                    penColor = GetMonsterColor(npcTemplateId);
                                }
                                catch
                                {
                                    // 如果無法解析 ID，使用黃色
                                }
                            }

                            using (Pen pen = new Pen(penColor, 2))
                            {
                                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                                Point[] points = new Point[] { p1, p2, p3, p4 };
                                e.Graphics.DrawPolygon(pen, points);
                            }
                        }
                    }
                }
                catch
                {
                    // 忽略錯誤
                }
            }
        }

        // 資料庫設定
        private void databaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (DatabaseConnectionForm form = new DatabaseConnectionForm())
            {
                form.ShowDialog(this);

                // 如果連線成功，載入當前地圖的 spawnlist 資料
                if (L1MapViewer.Helper.DatabaseHelper.IsConnected)
                {
                    LoadSpawnListForCurrentMap();
                }
            }
        }

        // 地圖編輯器
        private void mapEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MapForm mapForm = new MapForm();
            mapForm.Show();
        }

        // 從資料庫載入 spawnlist 資料
        public void LoadSpawnListFromDatabase(int? mapId = null)
        {
            try
            {
                if (!L1MapViewer.Helper.DatabaseHelper.IsConnected)
                {
                    MessageBox.Show("請先連線到資料庫", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 設定載入中標記，避免觸發 SelectionChanged
                isLoadingData = true;

                // 清除編輯狀態
                ClearEditMode();

                // 清空現有資料
                dataGridViewMonsters.Rows.Clear();
                currentSpawnList.Clear();

                // 建立 SQL 查詢 - JOIN npc 資料表取得名字，並取得所有座標欄位
                string query = @"SELECT s.id, s.npc_templateid, n.name, s.count, s.mapid,
                                       s.locx, s.locy, s.randomx, s.randomy,
                                       s.locx1, s.locy1, s.locx2, s.locy2
                               FROM spawnlist s
                               LEFT JOIN npc n ON s.npc_templateid = n.npcid";

                // 如果指定地圖 ID，加上篩選條件
                if (mapId.HasValue)
                {
                    query += $" WHERE s.mapid = {mapId.Value}";
                }

                query += " ORDER BY s.mapid, s.id";

                // 執行查詢
                using (var reader = L1MapViewer.Helper.DatabaseHelper.ExecuteQuery(query))
                {
                    int rowCount = 0;
                    while (reader.Read())
                    {
                        // 取得資料
                        int spawnId = reader["id"] != DBNull.Value ? Convert.ToInt32(reader["id"]) : 0;
                        int npcTemplateId = reader["npc_templateid"] != DBNull.Value ? Convert.ToInt32(reader["npc_templateid"]) : 0;
                        string name = reader["name"] != DBNull.Value && !string.IsNullOrEmpty(reader["name"].ToString())
                            ? reader["name"].ToString()
                            : $"ID:{npcTemplateId}";
                        int count = reader["count"] != DBNull.Value ? Convert.ToInt32(reader["count"]) : 0;

                        // 取得座標資料
                        int locx = reader["locx"] != DBNull.Value ? Convert.ToInt32(reader["locx"]) : 0;
                        int locy = reader["locy"] != DBNull.Value ? Convert.ToInt32(reader["locy"]) : 0;
                        int randomx = reader["randomx"] != DBNull.Value ? Convert.ToInt32(reader["randomx"]) : 0;
                        int randomy = reader["randomy"] != DBNull.Value ? Convert.ToInt32(reader["randomy"]) : 0;
                        int locx1 = reader["locx1"] != DBNull.Value ? Convert.ToInt32(reader["locx1"]) : 0;
                        int locy1 = reader["locy1"] != DBNull.Value ? Convert.ToInt32(reader["locy1"]) : 0;
                        int locx2 = reader["locx2"] != DBNull.Value ? Convert.ToInt32(reader["locx2"]) : 0;
                        int locy2 = reader["locy2"] != DBNull.Value ? Convert.ToInt32(reader["locy2"]) : 0;

                        // 儲存完整的 spawn 資料
                        SpawnData spawnData = new SpawnData
                        {
                            Id = spawnId,
                            NpcTemplateId = npcTemplateId,
                            Name = name,
                            Count = count,
                            LocX = locx,
                            LocY = locy,
                            RandomX = randomx,
                            RandomY = randomy,
                            LocX1 = locx1,
                            LocY1 = locy1,
                            LocX2 = locx2,
                            LocY2 = locy2
                        };
                        currentSpawnList.Add(spawnData);

                        // 取得怪物顏色
                        Color monsterColor = GetMonsterColor(npcTemplateId);

                        // 加入到 DataGridView，並設定顏色欄位的背景色
                        int rowIndex = dataGridViewMonsters.Rows.Add("", spawnId, name, count);
                        dataGridViewMonsters.Rows[rowIndex].Cells["color"].Style.BackColor = monsterColor;

                        rowCount++;
                    }

                    string message = mapId.HasValue
                        ? $"已載入 {rowCount} 筆資料 (地圖 {mapId.Value})"
                        : $"已載入 {rowCount} 筆資料";

                    this.toolStripStatusLabel1.Text = message;
                }

                // 載入完成後，如果勾選了顯示選項，在地圖上繪製所有怪物
                if (chkShowAllSpawns.Checked)
                {
                    DrawAllSpawnsOnMap();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入資料失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // 清除載入中標記
                isLoadingData = false;
            }
        }

        // 根據當前選擇的地圖載入 spawnlist
        public void LoadSpawnListForCurrentMap()
        {
            if (this.comboBox1.SelectedItem == null)
            {
                LoadSpawnListFromDatabase();
                return;
            }

            // 取得當前選擇的地圖 ID
            string selectedMap = this.comboBox1.SelectedItem.ToString();
            string mapId = selectedMap.Split('-')[0].Trim();

            if (int.TryParse(mapId, out int mapIdInt))
            {
                LoadSpawnListFromDatabase(mapIdInt);
            }
            else
            {
                LoadSpawnListFromDatabase();
            }
        }

        // 重新載入當前地圖按鈕點擊事件
        private void btnReloadSpawnList_Click(object sender, EventArgs e)
        {
            if (!L1MapViewer.Helper.DatabaseHelper.IsConnected)
            {
                MessageBox.Show("請先連線到資料庫", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            LoadSpawnListForCurrentMap();
        }

        // 顯示所有怪物位置勾選變更事件
        private void chkShowAllSpawns_CheckedChanged(object sender, EventArgs e)
        {
            if (chkShowAllSpawns.Checked)
            {
                // 勾選時顯示所有怪物
                DrawAllSpawnsOnMap();
            }
            else
            {
                // 取消勾選時清除標記
                ClearAllSpawnMarkers();
            }
        }

        // 清除所有怪物標記
        private void ClearAllSpawnMarkers()
        {
            try
            {
                if (this.pictureBox4.Image != null)
                {
                    using (Graphics g = Graphics.FromImage(this.pictureBox4.Image))
                    {
                        g.Clear(Color.Transparent);
                    }
                    this.pictureBox4.Refresh();
                }
            }
            catch (Exception ex)
            {
                this.toolStripStatusLabel1.Text = "清除標記失敗: " + ex.Message;
            }
        }

        // 新增按鈕點擊事件
        private void btnNewSpawn_Click(object sender, EventArgs e)
        {
            // 清除編輯狀態，進入新增模式
            editingSpawnId = null;

            // 清除所有欄位
            txtMonsterId.Text = "";
            txtMonsterName.Text = "";
            txtMonsterNote.Text = "";
            txtCustomCount.Text = "1";

            lblX1Value.Text = "";
            lblY1Value.Text = "";
            lblX2Value.Text = "";
            lblY2Value.Text = "";

            lblSpawnTimeMinValue.Text = "60";
            lblSpawnTimeMaxValue.Text = "120";
            lblOnScreenValue.Text = "0";
            lblTeleportBackValue.Text = "0";
            lblGroupValue.Text = "0";

            // 改變按鈕文字為「存檔」
            btnAddToDatabase.Text = "存檔";

            // 清除地圖標記（如果有勾選顯示所有怪物，則重新繪製所有怪物）
            if (this.pictureBox4.Image != null)
            {
                if (chkShowAllSpawns.Checked)
                {
                    DrawAllSpawnsOnMap();
                }
                else
                {
                    using (Graphics g = Graphics.FromImage(this.pictureBox4.Image))
                    {
                        g.Clear(Color.Transparent);
                    }
                    this.pictureBox4.Refresh();
                }
            }

            this.toolStripStatusLabel1.Text = "新增模式";
        }

        // 複製按鈕點擊事件
        private void btnCopySpawn_Click(object sender, EventArgs e)
        {
            if (dataGridViewMonsters.SelectedRows.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "請先選擇要複製的怪物";
                return;
            }

            if (!L1MapViewer.Helper.DatabaseHelper.IsConnected)
            {
                MessageBox.Show("請先連線到資料庫", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // 取得選中的 spawn ID
                int sourceSpawnId = Convert.ToInt32(dataGridViewMonsters.SelectedRows[0].Cells["id"].Value);

                // 宣告變數儲存資料
                int npcTemplateId = 0;
                int count = 0;
                int locx = 0;
                int locy = 0;
                int randomx = 0;
                int randomy = 0;
                int locx1 = 0;
                int locy1 = 0;
                int locx2 = 0;
                int locy2 = 0;
                int minRespawn = 60;
                int maxRespawn = 120;
                int respawnScreen = 0;
                int movementDistance = 0;
                int groupId = 0;
                string location = "";
                int mapId = 0;

                // 從資料庫取得完整資料
                string querySource = $@"SELECT * FROM spawnlist WHERE id = {sourceSpawnId}";

                using (var reader = L1MapViewer.Helper.DatabaseHelper.ExecuteQuery(querySource))
                {
                    if (reader.Read())
                    {
                        // 取得所有欄位資料
                        npcTemplateId = reader["npc_templateid"] != DBNull.Value ? Convert.ToInt32(reader["npc_templateid"]) : 0;
                        count = reader["count"] != DBNull.Value ? Convert.ToInt32(reader["count"]) : 0;
                        locx = reader["locx"] != DBNull.Value ? Convert.ToInt32(reader["locx"]) : 0;
                        locy = reader["locy"] != DBNull.Value ? Convert.ToInt32(reader["locy"]) : 0;
                        randomx = reader["randomx"] != DBNull.Value ? Convert.ToInt32(reader["randomx"]) : 0;
                        randomy = reader["randomy"] != DBNull.Value ? Convert.ToInt32(reader["randomy"]) : 0;
                        locx1 = reader["locx1"] != DBNull.Value ? Convert.ToInt32(reader["locx1"]) : 0;
                        locy1 = reader["locy1"] != DBNull.Value ? Convert.ToInt32(reader["locy1"]) : 0;
                        locx2 = reader["locx2"] != DBNull.Value ? Convert.ToInt32(reader["locx2"]) : 0;
                        locy2 = reader["locy2"] != DBNull.Value ? Convert.ToInt32(reader["locy2"]) : 0;
                        minRespawn = reader["min_respawn_delay"] != DBNull.Value ? Convert.ToInt32(reader["min_respawn_delay"]) : 60;
                        maxRespawn = reader["max_respawn_delay"] != DBNull.Value ? Convert.ToInt32(reader["max_respawn_delay"]) : 120;
                        respawnScreen = reader["respawn_screen"] != DBNull.Value ? Convert.ToInt32(reader["respawn_screen"]) : 0;
                        movementDistance = reader["movement_distance"] != DBNull.Value ? Convert.ToInt32(reader["movement_distance"]) : 0;
                        groupId = reader["group_id"] != DBNull.Value ? Convert.ToInt32(reader["group_id"]) : 0;
                        location = reader["location"] != DBNull.Value ? reader["location"].ToString() : "";
                        mapId = reader["mapid"] != DBNull.Value ? Convert.ToInt32(reader["mapid"]) : 0;
                    }
                } // reader 在這裡關閉

                // 插入新的複製資料（座標保持一樣）
                string insertQuery = $@"INSERT INTO spawnlist
                    (npc_templateid, count, locx, locy, randomx, randomy,
                     locx1, locy1, locx2, locy2,
                     min_respawn_delay, max_respawn_delay, respawn_screen,
                     movement_distance, group_id, location, mapid)
                    VALUES
                    ({npcTemplateId}, {count}, {locx}, {locy}, {randomx}, {randomy},
                     {locx1}, {locy1}, {locx2}, {locy2},
                     {minRespawn}, {maxRespawn}, {respawnScreen},
                     {movementDistance}, {groupId}, '{location.Replace("'", "''")}', {mapId})";

                L1MapViewer.Helper.DatabaseHelper.ExecuteNonQuery(insertQuery);
                this.toolStripStatusLabel1.Text = "複製成功";

                // 取得最後插入的 ID
                int newId = 0;
                string lastIdQuery = "SELECT LAST_INSERT_ID() as newid";
                using (var idReader = L1MapViewer.Helper.DatabaseHelper.ExecuteQuery(lastIdQuery))
                {
                    if (idReader.Read())
                    {
                        newId = Convert.ToInt32(idReader["newid"]);
                    }
                }

                // 重新載入當前地圖的 spawnlist
                LoadSpawnListForCurrentMap();

                // 自動選中新增的資料
                if (newId > 0 && dataGridViewMonsters.Rows.Count > 0)
                {
                    // 在 DataGridView 中找到並選中這筆新資料
                    foreach (DataGridViewRow row in dataGridViewMonsters.Rows)
                    {
                        if (Convert.ToInt32(row.Cells["id"].Value) == newId)
                        {
                            row.Selected = true;
                            dataGridViewMonsters.FirstDisplayedScrollingRowIndex = row.Index;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"複製失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 刪除按鈕點擊事件
        private void btnDeleteSpawn_Click(object sender, EventArgs e)
        {
            if (dataGridViewMonsters.SelectedRows.Count == 0)
            {
                this.toolStripStatusLabel1.Text = "請先選擇要刪除的怪物";
                return;
            }

            if (!L1MapViewer.Helper.DatabaseHelper.IsConnected)
            {
                MessageBox.Show("請先連線到資料庫", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // 取得選中的 ID 和名稱
                int spawnId = Convert.ToInt32(dataGridViewMonsters.SelectedRows[0].Cells["id"].Value);
                string name = dataGridViewMonsters.SelectedRows[0].Cells["name"].Value?.ToString() ?? "未知怪物";

                // 顯示確認對話框
                DialogResult result = MessageBox.Show(
                    $"確定要刪除 [{name}] (ID: {spawnId}) 嗎？\n\n此操作無法復原。",
                    "確認刪除",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    // 執行刪除
                    string deleteQuery = $"DELETE FROM spawnlist WHERE id = {spawnId}";
                    L1MapViewer.Helper.DatabaseHelper.ExecuteNonQuery(deleteQuery);

                    this.toolStripStatusLabel1.Text = $"已刪除 [{name}]";

                    // 重新載入當前地圖的 spawnlist
                    LoadSpawnListForCurrentMap();

                    // 清除編輯狀態
                    ClearEditMode();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刪除失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 搜尋怪物按鈕點擊事件
        private void btnSearchMonster_Click(object sender, EventArgs e)
        {
            if (!L1MapViewer.Helper.DatabaseHelper.IsConnected)
            {
                MessageBox.Show("請先連線到資料庫", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new MonsterSearchDialog())
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    txtMonsterId.Text = dialog.SelectedMonsterId.ToString();
                    txtMonsterName.Text = dialog.SelectedMonsterName;
                }
            }
        }

        // txtMonsterId 文字變更事件
        private void txtMonsterId_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(txtMonsterId.Text))
                {
                    int npcId = int.Parse(txtMonsterId.Text);

                    // 從資料庫查詢
                    if (L1MapViewer.Helper.DatabaseHelper.IsConnected)
                    {
                        string query = $"SELECT name FROM npc WHERE npcid = {npcId}";
                        using (var reader = L1MapViewer.Helper.DatabaseHelper.ExecuteQuery(query))
                        {
                            if (reader.Read())
                            {
                                string name = reader["name"] != DBNull.Value ? reader["name"].ToString() : $"ID:{npcId}";
                                txtMonsterName.Text = name;
                            }
                            else
                            {
                                txtMonsterName.Text = $"ID:{npcId}";
                            }
                        }
                    }
                    else
                    {
                        txtMonsterName.Text = $"ID:{npcId}";
                    }
                }
                else
                {
                    txtMonsterName.Text = "";
                }
            }
            catch
            {
                txtMonsterName.Text = "";
            }
        }

        // 加入/更新資料庫按鈕點擊事件
        private void btnAddToDatabase_Click(object sender, EventArgs e)
        {
            try
            {
                // 檢查資料庫連線
                if (!L1MapViewer.Helper.DatabaseHelper.IsConnected)
                {
                    MessageBox.Show("請先連線到資料庫", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 驗證必要欄位
                if (string.IsNullOrWhiteSpace(txtMonsterId.Text))
                {
                    MessageBox.Show("請輸入怪物 ID", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtCustomCount.Text))
                {
                    MessageBox.Show("請輸入怪物數量", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 取得當前地圖 ID
                if (this.comboBox1.SelectedItem == null)
                {
                    MessageBox.Show("請先選擇地圖", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string selectedMap = this.comboBox1.SelectedItem.ToString();
                int mapId = int.Parse(selectedMap.Split('-')[0].Trim());

                // 取得欄位資料
                int npcTemplateId = int.Parse(txtMonsterId.Text);
                int count = int.Parse(txtCustomCount.Text);
                int minRespawn = int.Parse(lblSpawnTimeMinValue.Text);
                int maxRespawn = int.Parse(lblSpawnTimeMaxValue.Text);
                int respawnScreen = int.Parse(lblOnScreenValue.Text);
                int movementDistance = int.Parse(lblTeleportBackValue.Text);
                int groupId = int.Parse(lblGroupValue.Text);

                // 取得座標資訊（方形配置）
                int locx = 0, locy = 0, randomx = 0, randomy = 0;
                int locx1 = 0, locy1 = 0, locx2 = 0, locy2 = 0;

                // 方形配置 - 使用 x1, y1, x2, y2
                if (!string.IsNullOrWhiteSpace(lblX1Value.Text))
                    locx1 = int.Parse(lblX1Value.Text);
                if (!string.IsNullOrWhiteSpace(lblY1Value.Text))
                    locy1 = int.Parse(lblY1Value.Text);
                if (!string.IsNullOrWhiteSpace(lblX2Value.Text))
                    locx2 = int.Parse(lblX2Value.Text);
                if (!string.IsNullOrWhiteSpace(lblY2Value.Text))
                    locy2 = int.Parse(lblY2Value.Text);

                // 計算中心點作為 locx, locy
                locx = (locx1 + locx2) / 2;
                locy = (locy1 + locy2) / 2;

                // 判斷是更新還是新增
                // 取得備註
                string location = txtMonsterNote.Text.Trim();

                if (editingSpawnId.HasValue)
                {
                    // 更新模式
                    string updateQuery = $@"UPDATE spawnlist SET
                        npc_templateid = {npcTemplateId},
                        count = {count},
                        locx = {locx},
                        locy = {locy},
                        randomx = {randomx},
                        randomy = {randomy},
                        locx1 = {locx1},
                        locy1 = {locy1},
                        locx2 = {locx2},
                        locy2 = {locy2},
                        min_respawn_delay = {minRespawn},
                        max_respawn_delay = {maxRespawn},
                        respawn_screen = {respawnScreen},
                        movement_distance = {movementDistance},
                        group_id = {groupId},
                        location = '{location.Replace("'", "''")}',
                        mapid = {mapId}
                        WHERE id = {editingSpawnId.Value}";

                    L1MapViewer.Helper.DatabaseHelper.ExecuteNonQuery(updateQuery);
                    this.toolStripStatusLabel1.Text = "更新成功";
                }
                else
                {
                    // 新增模式
                    string insertQuery = $@"INSERT INTO spawnlist
                        (npc_templateid, count, locx, locy, randomx, randomy,
                         locx1, locy1, locx2, locy2,
                         min_respawn_delay, max_respawn_delay, respawn_screen,
                         movement_distance, group_id, location, mapid)
                        VALUES
                        ({npcTemplateId}, {count}, {locx}, {locy}, {randomx}, {randomy},
                         {locx1}, {locy1}, {locx2}, {locy2},
                         {minRespawn}, {maxRespawn}, {respawnScreen},
                         {movementDistance}, {groupId}, '{location.Replace("'", "''")}', {mapId})";

                    L1MapViewer.Helper.DatabaseHelper.ExecuteNonQuery(insertQuery);
                    this.toolStripStatusLabel1.Text = "新增成功";
                }

                // 重新載入當前地圖的 spawnlist
                LoadSpawnListForCurrentMap();

                // 清除編輯狀態
                ClearEditMode();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // DataGridView 選擇變更事件
        private void dataGridViewMonsters_SelectionChanged(object sender, EventArgs e)
        {
            // 如果正在載入資料，不處理 SelectionChanged 事件
            if (isLoadingData)
                return;

            if (dataGridViewMonsters.SelectedRows.Count == 0)
                return;

            if (!L1MapViewer.Helper.DatabaseHelper.IsConnected)
                return;

            try
            {
                // 取得選中的 ID
                int spawnId = Convert.ToInt32(dataGridViewMonsters.SelectedRows[0].Cells["id"].Value);

                // 從資料庫載入完整資料
                LoadSpawnDataForEdit(spawnId);
            }
            catch (Exception ex)
            {
                this.toolStripStatusLabel1.Text = "載入資料失敗: " + ex.Message;
            }
        }

        // DataGridView 格式化事件 - 確保顏色格保持原色
        private void DataGridViewMonsters_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            // 只處理顏色列
            if (dataGridViewMonsters.Columns[e.ColumnIndex].Name == "color")
            {
                // 強制使用儲存格的背景色，忽略選中狀態
                e.CellStyle.SelectionBackColor = e.CellStyle.BackColor;
            }
        }

        // 載入 Spawn 資料到編輯欄位
        private void LoadSpawnDataForEdit(int spawnId)
        {
            try
            {
                string query = $@"SELECT s.*, n.name
                                FROM spawnlist s
                                LEFT JOIN npc n ON s.npc_templateid = n.npcid
                                WHERE s.id = {spawnId}";

                using (var reader = L1MapViewer.Helper.DatabaseHelper.ExecuteQuery(query))
                {
                    if (reader.Read())
                    {
                        // 設定編輯模式
                        editingSpawnId = spawnId;

                        // 取得資料
                        int npcId = reader["npc_templateid"] != DBNull.Value ? Convert.ToInt32(reader["npc_templateid"]) : 0;
                        string name = reader["name"] != DBNull.Value ? (reader["name"]?.ToString() ?? $"ID:{npcId}") : $"ID:{npcId}";
                        int count = reader["count"] != DBNull.Value ? Convert.ToInt32(reader["count"]) : 0;
                        int locx = reader["locx"] != DBNull.Value ? Convert.ToInt32(reader["locx"]) : 0;
                        int locy = reader["locy"] != DBNull.Value ? Convert.ToInt32(reader["locy"]) : 0;
                        int randomx = reader["randomx"] != DBNull.Value ? Convert.ToInt32(reader["randomx"]) : 0;
                        int randomy = reader["randomy"] != DBNull.Value ? Convert.ToInt32(reader["randomy"]) : 0;
                        int locx1 = reader["locx1"] != DBNull.Value ? Convert.ToInt32(reader["locx1"]) : 0;
                        int locy1 = reader["locy1"] != DBNull.Value ? Convert.ToInt32(reader["locy1"]) : 0;
                        int locx2 = reader["locx2"] != DBNull.Value ? Convert.ToInt32(reader["locx2"]) : 0;
                        int locy2 = reader["locy2"] != DBNull.Value ? Convert.ToInt32(reader["locy2"]) : 0;
                        int minRespawn = reader["min_respawn_delay"] != DBNull.Value ? Convert.ToInt32(reader["min_respawn_delay"]) : 60;
                        int maxRespawn = reader["max_respawn_delay"] != DBNull.Value ? Convert.ToInt32(reader["max_respawn_delay"]) : 120;
                        int respawnScreen = reader["respawn_screen"] != DBNull.Value ? Convert.ToInt32(reader["respawn_screen"]) : 0;
                        int movementDistance = reader["movement_distance"] != DBNull.Value ? Convert.ToInt32(reader["movement_distance"]) : 0;
                        int groupId = reader["group_id"] != DBNull.Value ? Convert.ToInt32(reader["group_id"]) : 0;
                        string location = reader["location"] != DBNull.Value ? reader["location"].ToString() : "";

                        // 填充到右側欄位
                        txtMonsterId.Text = npcId.ToString();
                        txtMonsterName.Text = name;
                        txtMonsterNote.Text = location;
                        txtCustomCount.Text = count.ToString();

                        // 座標資訊
                        lblX1Value.Text = locx1.ToString();
                        lblY1Value.Text = locy1.ToString();
                        lblX2Value.Text = locx2.ToString();
                        lblY2Value.Text = locy2.ToString();

                        // 重生時間
                        lblSpawnTimeMinValue.Text = minRespawn.ToString();
                        lblSpawnTimeMaxValue.Text = maxRespawn.ToString();

                        // 其他設定
                        lblOnScreenValue.Text = respawnScreen.ToString();
                        lblTeleportBackValue.Text = movementDistance.ToString();
                        lblGroupValue.Text = groupId.ToString();

                        // 修改按鈕文字為「更新」
                        btnAddToDatabase.Text = "更新";

                        // 在地圖上繪製標記
                        DrawSpawnMarker(locx, locy, locx1, locy1, locx2, locy2, randomx, randomy, count, name, npcId);

                        this.toolStripStatusLabel1.Text = $"已載入 Spawn ID: {spawnId}";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入 Spawn 資料失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 在地圖上繪製 Spawn 標記
        private void DrawSpawnMarker(int locx, int locy, int locx1, int locy1, int locx2, int locy2,
                                     int randomx, int randomy, int count, string name, int npcTemplateId)
        {
            try
            {
                if (this.pictureBox4.Image == null)
                    return;

                // 如果勾選了顯示所有怪物，就重新繪製所有怪物
                if (chkShowAllSpawns.Checked)
                {
                    DrawAllSpawnsOnMap();
                }
                else
                {
                    // 取得怪物顏色
                    Color color = GetMonsterColor(npcTemplateId);

                    // 清除之前的標記，只顯示當前選中的怪物
                    using (Graphics g = Graphics.FromImage(this.pictureBox4.Image))
                    {
                        g.Clear(Color.Transparent);

                        // 根據座標類型繪製不同的標記
                        if (randomx > 0 || randomy > 0)
                        {
                            // 圓形範圍
                            DrawCircleMarkerWithColor(g, locx, locy, Math.Max(randomx, randomy), count, name, color);
                        }
                        else if (locx1 != 0 && locy1 != 0 && locx2 != 0 && locy2 != 0)
                        {
                            // 矩形範圍 - 兩個對角點
                            DrawRectangleMarkerWithColor(g, locx1, locy1, locx2, locy2, count, name, color);
                        }
                        else
                        {
                            // 單點
                            DrawPointMarkerWithColor(g, locx, locy, count, name, color);
                        }
                    }

                    this.pictureBox4.Refresh();
                }
            }
            catch (Exception ex)
            {
                this.toolStripStatusLabel1.Text = "繪製標記失敗: " + ex.Message;
            }
        }

        // 繪製標記文字
        private void DrawMarkerText(Graphics g, int x, int y, int count, string name)
        {
            string text = $"{name} x{count}";
            using (Font font = new Font("微軟正黑體", 10, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
            {
                SizeF textSize = g.MeasureString(text, font);
                g.FillRectangle(bgBrush, x + 15, y - 10, textSize.Width + 4, textSize.Height);
                g.DrawString(text, font, textBrush, x + 17, y - 10);
            }
        }

        // 在地圖上繪製所有 Spawn
        private void DrawAllSpawnsOnMap()
        {
            try
            {
                if (this.pictureBox4.Image == null)
                    return;

                // 清除之前的標記
                using (Graphics g = Graphics.FromImage(this.pictureBox4.Image))
                {
                    g.Clear(Color.Transparent);

                    // 繪製每個 spawn
                    foreach (var spawn in currentSpawnList)
                    {
                        // 取得怪物顏色
                        Color color = GetMonsterColor(spawn.NpcTemplateId);

                        // 根據座標類型繪製不同的標記
                        if (spawn.RandomX > 0 || spawn.RandomY > 0)
                        {
                            // 圓形範圍
                            DrawCircleMarkerWithColor(g, spawn.LocX, spawn.LocY, Math.Max(spawn.RandomX, spawn.RandomY), spawn.Count, spawn.Name, color);
                        }
                        else if (spawn.LocX1 != 0 && spawn.LocY1 != 0 && spawn.LocX2 != 0 && spawn.LocY2 != 0)
                        {
                            // 矩形範圍 - 兩個對角點
                            DrawRectangleMarkerWithColor(g, spawn.LocX1, spawn.LocY1, spawn.LocX2, spawn.LocY2, spawn.Count, spawn.Name, color);
                        }
                        else
                        {
                            // 單點
                            DrawPointMarkerWithColor(g, spawn.LocX, spawn.LocY, spawn.Count, spawn.Name, color);
                        }
                    }
                }

                this.pictureBox4.Refresh();
            }
            catch (Exception ex)
            {
                this.toolStripStatusLabel1.Text = "繪製怪物標記失敗: " + ex.Message;
            }
        }

        // 繪製圓形標記（帶顏色）
        private void DrawCircleMarkerWithColor(Graphics g, int centerX, int centerY, int radius, int count, string name, Color color)
        {
            var loc = L1MapHelper.GetLinLocationByCoords(centerX, centerY);
            if (loc != null)
            {
                var bounds = loc.region.GetBounds(g);
                int x = (int)(bounds.X + bounds.Width / 2);
                int y = (int)(bounds.Y + bounds.Height / 2);

                // 繪製圓形
                using (Pen pen = new Pen(color, 2))
                {
                    int radiusPx = (int)(radius * 24); // 轉換為像素
                    g.DrawEllipse(pen, x - radiusPx, y - radiusPx, radiusPx * 2, radiusPx * 2);
                }

                // 繪製文字
                DrawMarkerText(g, x, y, count, name);
            }
        }

        // 繪製矩形標記（帶顏色）
        private void DrawRectangleMarkerWithColor(Graphics g, int x1, int y1, int x2, int y2, int count, string name, Color color)
        {
            // 計算矩形的四個角點（在遊戲座標系統中）
            int minX = Math.Min(x1, x2);
            int maxX = Math.Max(x1, x2);
            int minY = Math.Min(y1, y2);
            int maxY = Math.Max(y1, y2);

            // 四個角點的遊戲座標
            var corner1 = L1MapHelper.GetLinLocationByCoords(minX, minY); // 左上
            var corner2 = L1MapHelper.GetLinLocationByCoords(maxX, minY); // 右上
            var corner3 = L1MapHelper.GetLinLocationByCoords(maxX, maxY); // 右下
            var corner4 = L1MapHelper.GetLinLocationByCoords(minX, maxY); // 左下

            if (corner1 != null && corner2 != null && corner3 != null && corner4 != null)
            {
                var bounds1 = corner1.region.GetBounds(g);
                var bounds2 = corner2.region.GetBounds(g);
                var bounds3 = corner3.region.GetBounds(g);
                var bounds4 = corner4.region.GetBounds(g);

                // 取每個格子的中心點
                Point p1 = new Point((int)(bounds1.X + bounds1.Width / 2), (int)(bounds1.Y + bounds1.Height / 2));
                Point p2 = new Point((int)(bounds2.X + bounds2.Width / 2), (int)(bounds2.Y + bounds2.Height / 2));
                Point p3 = new Point((int)(bounds3.X + bounds3.Width / 2), (int)(bounds3.Y + bounds3.Height / 2));
                Point p4 = new Point((int)(bounds4.X + bounds4.Width / 2), (int)(bounds4.Y + bounds4.Height / 2));

                // 繪製菱形（四邊形）
                using (Pen pen = new Pen(color, 2))
                {
                    Point[] points = new Point[] { p1, p2, p3, p4 };
                    g.DrawPolygon(pen, points);
                }

                // 繪製文字在中心點
                int centerX = (p1.X + p2.X + p3.X + p4.X) / 4;
                int centerY = (p1.Y + p2.Y + p3.Y + p4.Y) / 4;
                DrawMarkerText(g, centerX, centerY, count, name);
            }
        }

        // 繪製單點標記（帶顏色）
        private void DrawPointMarkerWithColor(Graphics g, int x, int y, int count, string name, Color color)
        {
            var loc = L1MapHelper.GetLinLocationByCoords(x, y);
            if (loc != null)
            {
                var bounds = loc.region.GetBounds(g);
                int px = (int)(bounds.X + bounds.Width / 2);
                int py = (int)(bounds.Y + bounds.Height / 2);

                // 繪製十字標記
                using (Pen pen = new Pen(color, 3))
                {
                    g.DrawLine(pen, px - 10, py, px + 10, py);
                    g.DrawLine(pen, px, py - 10, px, py + 10);
                }

                DrawMarkerText(g, px, py, count, name);
            }
        }

        // 小地图点击跳转
        private void miniMapPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            try
            {
                if (this.pictureBox1.Image == null)
                    return;

                int miniWidth = 260;
                int miniHeight = 260;

                // 计算缩放比例
                float scaleX = (float)miniWidth / this.pictureBox1.Image.Width;
                float scaleY = (float)miniHeight / this.pictureBox1.Image.Height;
                float scale = Math.Min(scaleX, scaleY);

                int scaledWidth = (int)(this.pictureBox1.Image.Width * scale);
                int scaledHeight = (int)(this.pictureBox1.Image.Height * scale);
                int offsetX = (miniWidth - scaledWidth) / 2;
                int offsetY = (miniHeight - scaledHeight) / 2;

                // 转换点击位置到原始地图坐标
                int clickX = e.X - offsetX;
                int clickY = e.Y - offsetY;

                if (clickX < 0 || clickY < 0 || clickX > scaledWidth || clickY > scaledHeight)
                    return;

                float clickRatioX = (float)clickX / scaledWidth;
                float clickRatioY = (float)clickY / scaledHeight;

                // 计算滚动条位置（使点击位置居中）
                int newScrollX = (int)(clickRatioX * this.pictureBox1.Image.Width) - this.panel1.Width / 2;
                int newScrollY = (int)(clickRatioY * this.pictureBox1.Image.Height) - this.panel1.Height / 2;

                // 限制在有效范围内
                newScrollX = Math.Max(this.hScrollBar1.Minimum, Math.Min(newScrollX, this.hScrollBar1.Maximum));
                newScrollY = Math.Max(this.vScrollBar1.Minimum, Math.Min(newScrollY, this.vScrollBar1.Maximum));

                // 更新滚动条
                this.hScrollBar1.Value = newScrollX;
                this.vScrollBar1.Value = newScrollY;
                this.hScrollBar1_Scroll(null, null);
                this.vScrollBar1_Scroll(null, null);
            }
            catch
            {
                // 忽略错误
            }
        }
    }
}
