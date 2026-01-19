// Global using directives for cross-platform compatibility
// Maps WinForms/System.Drawing types to Eto.Forms/SkiaSharp equivalents

// Import the compatibility namespace for extension methods and helpers
global using L1MapViewer.Compatibility;
global using static L1MapViewer.Compatibility.ClipboardHelper;
global using static L1MapViewer.Compatibility.ApplicationHelper;

global using Point = Eto.Drawing.Point;
global using PointF = Eto.Drawing.PointF;
global using Size = Eto.Drawing.Size;
global using SizeF = Eto.Drawing.SizeF;
global using Rectangle = Eto.Drawing.Rectangle;
global using RectangleF = Eto.Drawing.RectangleF;
global using Color = Eto.Drawing.Color;
global using Font = Eto.Drawing.Font;
global using Pen = Eto.Drawing.Pen;
global using Brush = Eto.Drawing.Brush;
global using SolidBrush = Eto.Drawing.SolidBrush;
global using Image = Eto.Drawing.Image;
global using Bitmap = L1MapViewer.Compatibility.WinFormsBitmap;
global using Graphics = Eto.Drawing.Graphics;

// SkiaSharp for advanced graphics operations
global using SkiaSharp;

// Eto.Forms control aliases using WinForms-compatible wrappers
global using PictureBox = L1MapViewer.Compatibility.WinFormsPictureBox;
global using Panel = L1MapViewer.Compatibility.WinFormsPanel;
global using UserControl = L1MapViewer.Compatibility.WinFormsPanel;
global using GroupBox = L1MapViewer.Compatibility.WinFormsGroupBox;
global using Form = L1MapViewer.Compatibility.WinFormsForm;
global using Button = L1MapViewer.Compatibility.WinFormsButton;
global using Label = L1MapViewer.Compatibility.WinFormsLabel;
global using TextBox = L1MapViewer.Compatibility.WinFormsTextBox;
global using CheckBox = L1MapViewer.Compatibility.WinFormsCheckBox;
global using RadioButton = L1MapViewer.Compatibility.WinFormsRadioButton;
global using ComboBox = L1MapViewer.Compatibility.WinFormsComboBox;
global using ListBox = L1MapViewer.Compatibility.WinFormsListBox;
global using NumericUpDown = L1MapViewer.Compatibility.WinFormsNumericUpDown;
global using ProgressBar = L1MapViewer.Compatibility.WinFormsProgressBar;
global using MenuStrip = L1MapViewer.Compatibility.WinFormsMenuStrip;
global using ToolStrip = Eto.Forms.ToolBar;
global using StatusStrip = L1MapViewer.Compatibility.WinFormsStatusStrip;
global using ToolStripMenuItem = L1MapViewer.Compatibility.WinFormsToolStripMenuItem;
global using ToolStripButton = L1MapViewer.Compatibility.WinFormsToolStripButton;
global using ToolStripSeparator = L1MapViewer.Compatibility.WinFormsToolStripSeparator;
global using SplitContainer = L1MapViewer.Compatibility.WinFormsSplitContainer;
global using TabControl = L1MapViewer.Compatibility.WinFormsTabControl;
global using TabPage = L1MapViewer.Compatibility.WinFormsTabPage;
global using TreeView = L1MapViewer.Compatibility.WinFormsTreeView;
global using ListView = L1MapViewer.Compatibility.WinFormsListView;
global using OpenFileDialog = L1MapViewer.Compatibility.WinFormsOpenFileDialog;
global using SaveFileDialog = L1MapViewer.Compatibility.WinFormsSaveFileDialog;
global using FolderBrowserDialog = L1MapViewer.Compatibility.WinFormsFolderBrowserDialog;
global using MessageBox = L1MapViewer.Compatibility.WinFormsMessageBox;
global using DialogResult = L1MapViewer.Compatibility.DialogResultCompat;
global using Keys = Eto.Forms.Keys;
global using Cursor = Eto.Forms.Cursor;
global using Cursors = L1MapViewer.Compatibility.CursorsCompat;
global using static L1MapViewer.Compatibility.ControlCompat;
global using Control = Eto.Forms.Control;
global using MouseEventArgs = Eto.Forms.MouseEventArgs;
global using KeyEventArgs = Eto.Forms.KeyEventArgs;
global using PaintEventArgs = Eto.Forms.PaintEventArgs;
global using DragEventArgs = Eto.Forms.DragEventArgs;

// Event handler delegates - 從 UI/Component/EventTypes.cs 匯入
global using MouseEventHandler = flyworld.eto.component.MouseEventHandler;
global using KeyEventHandler = flyworld.eto.component.KeyEventHandler;
global using PaintEventHandler = flyworld.eto.component.PaintEventHandler;
global using DragEventHandler = flyworld.eto.component.DragEventHandler;
global using ScrollEventHandler = flyworld.eto.component.ScrollEventHandler;
global using PreviewKeyDownEventHandler = flyworld.eto.component.PreviewKeyDownEventHandler;
global using DrawItemEventHandler = flyworld.eto.component.DrawItemEventHandler;
global using ItemCheckEventHandler = flyworld.eto.component.ItemCheckEventHandler;
global using FormClosedEventHandler = flyworld.eto.component.FormClosedEventHandler;
global using FormClosingEventHandler = flyworld.eto.component.FormClosingEventHandler;
global using FormClosedEventArgs = flyworld.eto.component.FormClosedEventArgs;
global using FormClosingEventArgs = flyworld.eto.component.FormClosingEventArgs;
global using CloseReason = flyworld.eto.component.CloseReason;

// Timer - Use WinForms-compatible wrapper
global using Timer = L1MapViewer.Compatibility.WinFormsTimer;

// Scrollbars and other WinForms-specific types - use compatibility layer
global using DataGridView = L1MapViewer.Compatibility.WinFormsListView;
global using CharacterCasing = L1MapViewer.Compatibility.CharacterCasing;
global using SelectionMode = L1MapViewer.Compatibility.SelectionMode;
global using ColumnHeader = L1MapViewer.Compatibility.ColumnHeader;
global using HorizontalAlignment = L1MapViewer.Compatibility.HorizontalAlignment;
global using VScrollBar = L1MapViewer.Compatibility.VScrollBar;
global using HScrollBar = L1MapViewer.Compatibility.HScrollBar;
global using ToolStripStatusLabel = L1MapViewer.Compatibility.ToolStripStatusLabel;
global using ToolStripProgressBar = L1MapViewer.Compatibility.ToolStripProgressBar;
global using ImageList = L1MapViewer.Compatibility.ImageList;
global using ListViewItem = L1MapViewer.Compatibility.ListViewItem;
global using ImageFormat = L1MapViewer.Compatibility.ImageFormat;
global using Region = L1MapViewer.Compatibility.Region;
global using IWin32Window = L1MapViewer.Compatibility.IWin32Window;
global using UITypeEditor = L1MapViewer.Compatibility.UITypeEditor;
global using UITypeEditorEditStyle = L1MapViewer.Compatibility.UITypeEditorEditStyle;
global using DataGridViewCellFormattingEventArgs = L1MapViewer.Compatibility.DataGridViewCellFormattingEventArgs;
global using ColumnClickEventArgs = L1MapViewer.Compatibility.ColumnClickEventArgs;
global using BorderStyle = L1MapViewer.Compatibility.BorderStyle;
global using FormStartPosition = L1MapViewer.Compatibility.FormStartPosition;
global using PictureBoxSizeMode = L1MapViewer.Compatibility.PictureBoxSizeMode;
global using ComboBoxStyle = L1MapViewer.Compatibility.ComboBoxStyle;
global using View = L1MapViewer.Compatibility.View;
global using SortOrder = L1MapViewer.Compatibility.SortOrder;
global using AnchorStyles = L1MapViewer.Compatibility.AnchorStyles;
global using DockStyle = L1MapViewer.Compatibility.DockStyle;
global using MessageBoxButtons = L1MapViewer.Compatibility.MessageBoxButtons;
global using MessageBoxIcon = L1MapViewer.Compatibility.MessageBoxIcon;
global using ColorDepth = L1MapViewer.Compatibility.ColorDepth;
global using ToolStripItemDisplayStyle = L1MapViewer.Compatibility.ToolStripItemDisplayStyle;
global using FlowLayoutPanel = L1MapViewer.Compatibility.FlowLayoutPanel;
global using FlowDirection = L1MapViewer.Compatibility.FlowDirection;
global using CheckedListBox = L1MapViewer.Compatibility.CheckedListBox;
global using ItemCheckEventArgs = flyworld.eto.component.ItemCheckEventArgs;
global using CheckState = flyworld.eto.component.CheckState;
global using ToolStripTextBox = L1MapViewer.Compatibility.ToolStripTextBox;
global using ToolTip = L1MapViewer.Compatibility.ToolTip;
global using DrawItemEventArgs = flyworld.eto.component.DrawItemEventArgs;
global using PreviewKeyDownEventArgs = flyworld.eto.component.PreviewKeyDownEventArgs;
global using IWindowsFormsEditorService = L1MapViewer.Compatibility.IWindowsFormsEditorService;
global using ToolboxBitmapAttribute = L1MapViewer.Compatibility.ToolboxBitmapAttribute;
global using ControlStyles = L1MapViewer.Compatibility.ControlStyles;
global using MouseButtons = L1MapViewer.Compatibility.MouseButtons;
global using Pens = L1MapViewer.Compatibility.Pens;
global using ContentAlignment = L1MapViewer.Compatibility.ContentAlignment;
global using AutoScaleMode = L1MapViewer.Compatibility.AutoScaleMode;
global using PixelFormat = L1MapViewer.Compatibility.PixelFormat;
global using ImageLockMode = L1MapViewer.Compatibility.ImageLockMode;
global using BitmapData = L1MapViewer.Compatibility.BitmapData;
global using Padding = L1MapViewer.Compatibility.Padding;
global using WinFormsMessageBox = L1MapViewer.Compatibility.WinFormsMessageBox;
global using FlatStyle = L1MapViewer.Compatibility.FlatStyle;
global using ScrollBars = L1MapViewer.Compatibility.ScrollBars;
global using FontStyleCompat = L1MapViewer.Compatibility.FontStyleCompat;

// FontStyle alias - use Eto.Drawing.FontStyle but add static access to Regular etc.
global using FontStyle = Eto.Drawing.FontStyle;
global using FlatButtonAppearance = L1MapViewer.Compatibility.FlatButtonAppearance;
global using ColumnHeaderStyle = L1MapViewer.Compatibility.ColumnHeaderStyle;
global using FixedPanel = L1MapViewer.Compatibility.FixedPanel;
global using ProgressBarStyle = L1MapViewer.Compatibility.ProgressBarStyle;
global using AutoSizeMode = L1MapViewer.Compatibility.AutoSizeMode;
global using FormBorderStyle = L1MapViewer.Compatibility.FormBorderStyle;
global using ScrollEventArgs = flyworld.eto.component.ScrollEventArgs;
global using ScrollEventType = flyworld.eto.component.ScrollEventType;
global using ScrollOrientation = flyworld.eto.component.ScrollOrientation;
global using MethodInvoker = flyworld.eto.component.MethodInvoker;
global using HandledMouseEventArgs = flyworld.eto.component.HandledMouseEventArgs;
global using StringFormat = L1MapViewer.Compatibility.StringFormat;
global using StringAlignment = L1MapViewer.Compatibility.StringAlignment;
global using StringTrimming = L1MapViewer.Compatibility.StringTrimming;
global using StringFormatFlags = L1MapViewer.Compatibility.StringFormatFlags;
global using ImageAttributes = L1MapViewer.Compatibility.ImageAttributes;
global using ColorMatrix = L1MapViewer.Compatibility.ColorMatrix;
global using ColorMatrixFlag = L1MapViewer.Compatibility.ColorMatrixFlag;
global using ColorAdjustType = L1MapViewer.Compatibility.ColorAdjustType;
global using WrapMode = L1MapViewer.Compatibility.WrapMode;
global using TextRenderingHint = L1MapViewer.Compatibility.TextRenderingHint;
global using ContextMenuStrip = Eto.Forms.ContextMenu;
global using ToolStripItem = System.Object;
global using DrawMode = L1MapViewer.Compatibility.DrawMode;
global using SmoothingMode = L1MapViewer.Compatibility.SmoothingMode;
global using InterpolationMode = L1MapViewer.Compatibility.InterpolationMode;
global using CompositingQuality = L1MapViewer.Compatibility.CompositingQuality;

// Ambiguous types - prefer compatibility layer
global using Colors = L1MapViewer.Compatibility.ColorsCompat;
global using SystemColors = L1MapViewer.Compatibility.SystemColors;
global using MessageBoxDefaultButton = L1MapViewer.Compatibility.MessageBoxDefaultButton;
global using GraphicsPath = L1MapViewer.Compatibility.GraphicsPath;
global using DashStyle = L1MapViewer.Compatibility.DashStyle;
global using DataFormats = L1MapViewer.Compatibility.DataFormats;
global using DrawItemState = flyworld.eto.component.DrawItemState;
global using Brushes = L1MapViewer.Compatibility.Brushes;
global using ControlPaint = L1MapViewer.Compatibility.ControlPaint;
global using ButtonState = L1MapViewer.Compatibility.ButtonState;
global using GraphicsUnit = L1MapViewer.Compatibility.GraphicsUnit;
global using Border3DStyle = L1MapViewer.Compatibility.Border3DStyle;
global using ColorHelper = L1MapViewer.Compatibility.ColorHelper;
global using DragDropEffects = L1MapViewer.Compatibility.DragDropEffects;
global using PixelOffsetMode = L1MapViewer.Compatibility.PixelOffsetMode;

namespace L1MapViewer.Compatibility
{
    /// <summary>
    /// Compatibility helpers for WinForms to Eto.Forms migration
    /// </summary>
    public static class CompatibilityExtensions
    {
        // Helper methods can be added here as needed
    }
}
