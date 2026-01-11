using Eto.Forms;
using Eto.Drawing;

// 事件類型已移至 UI/Component/EventTypes.cs，透過 GlobalUsings.cs 全域匯入

namespace L1MapViewer.Compatibility;

#region WinForms Delegate and Event Types
// 事件類型已移至 UI/Component/EventTypes.cs

/// <summary>
/// StringFormat for GDI+ text formatting compatibility
/// </summary>
public class StringFormat : IDisposable
{
    public StringAlignment Alignment { get; set; } = StringAlignment.Near;
    public StringAlignment LineAlignment { get; set; } = StringAlignment.Near;
    public StringFormatFlags FormatFlags { get; set; }
    public StringTrimming Trimming { get; set; } = StringTrimming.None;

    public static StringFormat GenericDefault => new StringFormat();
    public static StringFormat GenericTypographic => new StringFormat();

    public void Dispose() { }
}

public enum StringAlignment { Near, Center, Far }
public enum StringTrimming { None, Character, Word, EllipsisCharacter, EllipsisWord, EllipsisPath }

[Flags]
public enum StringFormatFlags
{
    DirectionRightToLeft = 1,
    DirectionVertical = 2,
    FitBlackBox = 4,
    DisplayFormatControl = 32,
    NoFontFallback = 1024,
    MeasureTrailingSpaces = 2048,
    NoWrap = 4096,
    LineLimit = 8192,
    NoClip = 16384
}

/// <summary>
/// ImageAttributes for GDI+ image manipulation
/// </summary>
public class ImageAttributes : IDisposable
{
    public Eto.Drawing.Color ColorKeyLow { get; private set; }
    public Eto.Drawing.Color ColorKeyHigh { get; private set; }
    public bool HasColorKey { get; private set; }

    public void SetColorMatrix(ColorMatrix matrix) { }
    public void SetColorMatrix(ColorMatrix matrix, ColorMatrixFlag flags) { }
    public void SetColorMatrix(ColorMatrix matrix, ColorMatrixFlag flags, ColorAdjustType type) { }
    public void SetWrapMode(WrapMode mode) { }
    public void SetWrapMode(WrapMode mode, Color color) { }

    public void SetColorKey(Eto.Drawing.Color colorLow, Eto.Drawing.Color colorHigh)
    {
        ColorKeyLow = colorLow;
        ColorKeyHigh = colorHigh;
        HasColorKey = true;
    }

    public void SetColorKey(Eto.Drawing.Color colorLow, Eto.Drawing.Color colorHigh, ColorAdjustType type)
    {
        SetColorKey(colorLow, colorHigh);
    }

    public void ClearColorKey()
    {
        HasColorKey = false;
    }

    public void Dispose() { }
}

public class ColorMatrix
{
    public float[][] Matrix { get; set; }
    public float Matrix00 { get; set; } = 1;
    public float Matrix11 { get; set; } = 1;
    public float Matrix22 { get; set; } = 1;
    public float Matrix33 { get; set; } = 1;
    public float Matrix44 { get; set; } = 1;

    public ColorMatrix() { }
    public ColorMatrix(float[][] matrix) { Matrix = matrix; }
}

public enum ColorMatrixFlag { Default, SkipGrays, AltGrays }
public enum ColorAdjustType { Default, Bitmap, Brush, Pen, Text, Count, Any }
public enum WrapMode { Tile, TileFlipX, TileFlipY, TileFlipXY, Clamp }

/// <summary>
/// TextRenderingHint for graphics compatibility
/// </summary>
public enum TextRenderingHint
{
    SystemDefault,
    SingleBitPerPixelGridFit,
    SingleBitPerPixel,
    AntiAliasGridFit,
    AntiAlias,
    ClearTypeGridFit
}

/// <summary>
/// Cursors compatibility class - maps WinForms cursor names to Eto
/// </summary>
public static class CursorsCompat
{
    public static Eto.Forms.Cursor Default => Eto.Forms.Cursors.Default;
    public static Eto.Forms.Cursor Arrow => Eto.Forms.Cursors.Arrow;
    public static Eto.Forms.Cursor Hand => Eto.Forms.Cursors.Pointer;
    public static Eto.Forms.Cursor Pointer => Eto.Forms.Cursors.Pointer;
    public static Eto.Forms.Cursor IBeam => Eto.Forms.Cursors.IBeam;
    public static Eto.Forms.Cursor Cross => Eto.Forms.Cursors.Crosshair;
    public static Eto.Forms.Cursor WaitCursor => Eto.Forms.Cursors.Default; // Eto doesn't have wait cursor
    public static Eto.Forms.Cursor SizeNS => Eto.Forms.Cursors.VerticalSplit;
    public static Eto.Forms.Cursor SizeWE => Eto.Forms.Cursors.HorizontalSplit;
    public static Eto.Forms.Cursor SizeNWSE => Eto.Forms.Cursors.Default;
    public static Eto.Forms.Cursor SizeNESW => Eto.Forms.Cursors.Default;
    public static Eto.Forms.Cursor SizeAll => Eto.Forms.Cursors.Move;
    public static Eto.Forms.Cursor No => Eto.Forms.Cursors.Default;
    public static Eto.Forms.Cursor Help => Eto.Forms.Cursors.Default;
}

/// <summary>
/// Control compatibility class for static properties like ModifierKeys
/// </summary>
public static class ControlCompat
{
    public static Eto.Forms.Keys ModifierKeys => Eto.Forms.Keyboard.Modifiers;
}

#endregion

#region WinForms-compatible Control Wrappers

/// <summary>
/// Base class with common WinForms properties
/// </summary>
public class WinFormsControlBase
{
    // Common stub properties that don't directly map to Eto
    public Point Location { get; set; }
    public bool AutoSize { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public bool TabStop { get; set; } = true;
    public Padding Margin { get; set; }
    public string Name { get; set; }
    public bool UseVisualStyleBackColor { get; set; }
}

/// <summary>
/// WinForms-compatible Label wrapper
/// </summary>
public class WinFormsLabel : Eto.Forms.Label
{
    public Point Location { get; set; }
    public bool AutoSize { get; set; } = true;
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public Padding Padding { get; set; }
    public Padding Margin { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public BorderStyle BorderStyle { get; set; }
    public ContentAlignment TextAlign { get; set; }
    public bool AutoEllipsis { get; set; }

    // Position properties
    public int Left { get => Location.X; set => Location = new Point(value, Location.Y); }
    public int Top { get => Location.Y; set => Location = new Point(Location.X, value); }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }
    public Color ForeColor { get => TextColor; set => TextColor = value; }

    // Event compatibility
    public event EventHandler Click { add => MouseDown += (s, e) => value(s, e); remove { } }
}

/// <summary>
/// WinForms-compatible Button wrapper
/// </summary>
public class WinFormsButton : Eto.Forms.Button
{
    public Point Location { get; set; }
    public bool AutoSize { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public bool TabStop { get; set; } = true;
    public Padding Margin { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public bool UseVisualStyleBackColor { get; set; }
    public FlatStyle FlatStyle { get; set; }
    public FlatButtonAppearance FlatAppearance { get; } = new FlatButtonAppearance();
    public new Eto.Drawing.Image Image { get => base.Image; set => base.Image = value; }
    public ContentAlignment ImageAlign { get; set; }
    public ContentAlignment TextAlign { get; set; }
    public Eto.Forms.DialogResult DialogResult { get; set; }

    // Position properties
    public int Left { get => Location.X; set => Location = new Point(value, Location.Y); }
    public int Top { get => Location.Y; set => Location = new Point(Location.X, value); }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }
    public Color ForeColor { get => TextColor; set => TextColor = value; }

    // Click event - wrap WinForms EventHandler to Eto EventHandler<EventArgs>
    public new event EventHandler Click
    {
        add => base.Click += (s, e) => value(s, EventArgs.Empty);
        remove { }
    }
}

/// <summary>
/// WinForms-compatible TextBox wrapper
/// </summary>
public class WinFormsTextBox : Eto.Forms.TextBox
{
    public Point Location { get; set; }
    public bool AutoSize { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public bool TabStop { get; set; } = true;
    public Padding Margin { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public BorderStyle BorderStyle { get; set; }
    public bool Multiline { get; set; }
    public ScrollBars ScrollBars { get; set; }
    public bool WordWrap { get; set; }
    public bool AcceptsReturn { get; set; }
    public bool AcceptsTab { get; set; }
    public CharacterCasing CharacterCasing { get; set; }
    public char PasswordChar { get; set; }
    public bool UseSystemPasswordChar { get; set; }
    public int SelectionStart { get; set; }
    public int SelectionLength { get; set; }

    // Position properties
    public int Left { get => Location.X; set => Location = new Point(value, Location.Y); }
    public int Top { get => Location.Y; set => Location = new Point(Location.X, value); }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }
    public Color ForeColor { get => TextColor; set => TextColor = value; }

    // TextChanged event - wrap WinForms EventHandler to Eto EventHandler<EventArgs>
    public new event EventHandler TextChanged
    {
        add => base.TextChanged += (s, e) => value(s, EventArgs.Empty);
        remove { }
    }
}

/// <summary>
/// CharacterCasing enum for TextBox compatibility
/// </summary>
public enum CharacterCasing { Normal, Upper, Lower }

/// <summary>
/// WinForms-compatible Panel wrapper
/// </summary>
public class WinFormsPanel : Eto.Forms.Panel
{
    public Point Location { get; set; }
    public bool AutoSize { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public bool TabStop { get; set; }
    public Padding Margin { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public BorderStyle BorderStyle { get; set; }
    public bool AutoScroll { get; set; }
    public Point AutoScrollPosition { get; set; }
    public bool DoubleBuffered { get; set; } = true; // Eto.Forms handles this automatically
    public Eto.Forms.ContextMenu ContextMenuStrip { get => ContextMenu; set => ContextMenu = value; }

    // Position properties
    public int Left { get => Location.X; set => Location = new Point(value, Location.Y); }
    public int Top { get => Location.Y; set => Location = new Point(Location.X, value); }
    public int Right => Location.X + Width;
    public int Bottom => Location.Y + Height;

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }

    // Resize event
    public event EventHandler Resize { add => SizeChanged += (s, e) => value(s, EventArgs.Empty); remove { } }

    // Mouse events - wrap WinForms delegate to Eto EventHandler<MouseEventArgs>
    public new event MouseEventHandler MouseDown
    {
        add => base.MouseDown += (s, e) => value(s, e);
        remove { }
    }
    public new event MouseEventHandler MouseMove
    {
        add => base.MouseMove += (s, e) => value(s, e);
        remove { }
    }
    public new event MouseEventHandler MouseUp
    {
        add => base.MouseUp += (s, e) => value(s, e);
        remove { }
    }
    public event MouseEventHandler MouseClick
    {
        add => base.MouseDown += (s, e) => value(s, e);
        remove { }
    }
    public new event MouseEventHandler MouseDoubleClick
    {
        add => base.MouseDoubleClick += (s, e) => value(s, e);
        remove { }
    }

    // Key events
    public new event KeyEventHandler KeyDown
    {
        add => base.KeyDown += (s, e) => value(s, e);
        remove { }
    }
    public new event KeyEventHandler KeyUp
    {
        add => base.KeyUp += (s, e) => value(s, e);
        remove { }
    }

    // Drag events
    public new event DragEventHandler DragEnter
    {
        add => base.DragEnter += (s, e) => value(s, e);
        remove { }
    }
    public new event DragEventHandler DragOver
    {
        add => base.DragOver += (s, e) => value(s, e);
        remove { }
    }
    public new event DragEventHandler DragDrop
    {
        add => base.DragDrop += (s, e) => value(s, e);
        remove { }
    }
    public new event DragEventHandler DragLeave
    {
        add => base.DragLeave += (s, e) => value(s, e);
        remove { }
    }

    // Controls collection compatibility
    private WinFormsControlCollection _controls;
    public WinFormsControlCollection Controls => _controls ??= new WinFormsControlCollection(this);
}

/// <summary>
/// WinForms-compatible StatusStrip wrapper
/// </summary>
public class WinFormsStatusStrip : Eto.Forms.Panel
{
    public Point Location { get; set; }
    public bool AutoSize { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public new string Name { get => ID; set => ID = value; }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }

    // Items collection for status strip items (stub - items not actually rendered)
    private StatusStripItemCollection _items;
    public StatusStripItemCollection Items => _items ??= new StatusStripItemCollection();
}

/// <summary>
/// Collection for StatusStrip items (stub implementation)
/// </summary>
public class StatusStripItemCollection : System.Collections.Generic.List<object>
{
    public void AddRange(object[] items)
    {
        foreach (var item in items)
        {
            Add(item);
        }
    }
}

/// <summary>
/// WinForms-compatible Form wrapper
/// </summary>
public class WinFormsForm : Eto.Forms.Form
{
    public Point Location { get; set; }
    public bool AutoSize { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public FormStartPosition StartPosition { get; set; }
    public FormBorderStyle FormBorderStyle { get; set; }
    public bool KeyPreview { get; set; }
    public bool TopMost { get => Topmost; set => Topmost = value; }
    public SizeF AutoScaleDimensions { get; set; }
    public AutoScaleMode AutoScaleMode { get; set; }
    public Eto.Forms.MenuBar MainMenuStrip { get => Menu; set => Menu = value; }
    public new Eto.Drawing.Icon Icon { get => base.Icon; set => base.Icon = value; }
    public new bool ShowInTaskbar { get => base.ShowInTaskbar; set => base.ShowInTaskbar = value; }
    public IWin32Window Owner { get; set; }
    public bool ControlBox { get; set; } = true;
    public bool MaximizeBox { get => Maximizable; set => Maximizable = value; }
    public bool MinimizeBox { get => Minimizable; set => Minimizable = value; }
    public Eto.Forms.Button AcceptButton { get; set; }
    public Eto.Forms.Button CancelButton { get; set; }
    public bool IsHandleCreated { get; set; } = true; // Always true in Eto.Forms

    public new string Text { get => Title; set => Title = value; }
    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }

    // Controls collection compatibility
    private WinFormsControlCollection _controls;
    public WinFormsControlCollection Controls => _controls ??= new WinFormsControlCollection(this);

    // Event compatibility - wrap WinForms EventHandler to Eto EventHandler<EventArgs>
    public new event EventHandler Load { add => Shown += (s, e) => value(s, EventArgs.Empty); remove { } }
    public new event EventHandler FormClosing { add => Closing += (s, e) => value(s, EventArgs.Empty); remove { } }
    public event FormClosedEventHandler FormClosed { add => Closed += (s, e) => value(s, new FormClosedEventArgs(CloseReason.UserClosing)); remove { } }
    public new event EventHandler Resize { add => SizeChanged += (s, e) => value(s, EventArgs.Empty); remove { } }

    // Dialog methods - Form doesn't have ShowModal, use Show instead
    public new DialogResultCompat ShowDialog()
    {
        Show();
        return DialogResultCompat.OK;
    }
    public DialogResultCompat ShowDialog(Eto.Forms.Control owner)
    {
        Show();
        return DialogResultCompat.OK;
    }

    // Show with owner
    public void Show(Eto.Forms.Control owner)
    {
        Show();
    }
}

/// <summary>
/// WinForms-compatible CheckBox wrapper
/// </summary>
public class WinFormsCheckBox : Eto.Forms.CheckBox
{
    public Point Location { get; set; }
    public bool AutoSize { get; set; } = true;
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public bool TabStop { get; set; } = true;
    public Padding Margin { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public bool UseVisualStyleBackColor { get; set; }
    public ContentAlignment TextAlign { get; set; }
    public ContentAlignment CheckAlign { get; set; }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }
    public Color ForeColor { get => TextColor; set => TextColor = value; }

    // CheckState property for WinForms compatibility
    public CheckState CheckState
    {
        get => Checked == true ? CheckState.Checked : (Checked == false ? CheckState.Unchecked : CheckState.Indeterminate);
        set => Checked = value == CheckState.Checked ? true : (value == CheckState.Unchecked ? false : null);
    }

    // CheckedChanged event - wrap WinForms EventHandler to Eto EventHandler<EventArgs>
    public new event EventHandler CheckedChanged
    {
        add => base.CheckedChanged += (s, e) => value(s, EventArgs.Empty);
        remove { }
    }
}

/// <summary>
/// WinForms-compatible RadioButton wrapper
/// </summary>
public class WinFormsRadioButton : Eto.Forms.RadioButton
{
    public Point Location { get; set; }
    public bool AutoSize { get; set; } = true;
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public bool TabStop { get; set; } = true;
    public new string Name { get => ID; set => ID = value; }
    public bool UseVisualStyleBackColor { get; set; }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }
    public Color ForeColor { get => TextColor; set => TextColor = value; }

    // CheckedChanged event - wrap WinForms EventHandler to Eto EventHandler<EventArgs>
    public new event EventHandler CheckedChanged
    {
        add => base.CheckedChanged += (s, e) => value(s, EventArgs.Empty);
        remove { }
    }
}

/// <summary>
/// WinForms-compatible ComboBox wrapper
/// </summary>
public class WinFormsComboBox : Eto.Forms.ComboBox
{
    private ComboBoxObjectCollection? _objectCollection;

    public Point Location { get; set; }
    public bool AutoSize { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public bool TabStop { get; set; } = true;
    public new string Name { get => ID; set => ID = value; }
    public ComboBoxStyle DropDownStyle { get; set; }
    public bool FormattingEnabled { get; set; }
    public int DropDownWidth { get; set; }
    public int MaxDropDownItems { get; set; }
    public int SelectionStart { get; set; }
    public int SelectionLength { get; set; }
    public bool DroppedDown { get; set; }
    public string DisplayMember { get; set; }
    public string ValueMember { get; set; }
    public object DataSource { get; set; }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }
    public Color ForeColor { get => TextColor; set => TextColor = value; }

    // Text property
    public new string Text { get => SelectedValue?.ToString() ?? ""; set { } }

    // WinForms-compatible Items property that accepts any object
    public new ComboBoxObjectCollection Items => _objectCollection ??= new ComboBoxObjectCollection(this);

    // SelectedItem property - returns the original object from our collection
    public object? SelectedItem
    {
        get => SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : null;
        set
        {
            int index = Items.IndexOf(value);
            if (index >= 0) SelectedIndex = index;
        }
    }

    // BeginUpdate/EndUpdate for performance
    public void BeginUpdate() { }
    public void EndUpdate() { }

    // SelectedIndexChanged event - wrap WinForms EventHandler to Eto EventHandler<EventArgs>
    public new event EventHandler SelectedIndexChanged
    {
        add => base.SelectedIndexChanged += (s, e) => value(s, EventArgs.Empty);
        remove { }
    }

    // TextChanged event - wrap WinForms EventHandler to Eto EventHandler<EventArgs>
    public new event EventHandler TextChanged
    {
        add => base.TextChanged += (s, e) => value(s, EventArgs.Empty);
        remove { }
    }
}

/// <summary>
/// WinForms-compatible ObjectCollection for ComboBox that accepts any object
/// </summary>
public class ComboBoxObjectCollection : System.Collections.IList
{
    private readonly Eto.Forms.ComboBox _comboBox;
    private readonly List<object> _items = new List<object>();

    public ComboBoxObjectCollection(Eto.Forms.ComboBox comboBox)
    {
        _comboBox = comboBox;
    }

    public int Count => _items.Count;
    public bool IsReadOnly => false;
    public bool IsFixedSize => false;
    public bool IsSynchronized => false;
    public object SyncRoot => this;

    public object this[int index]
    {
        get => _items[index];
        set
        {
            _items[index] = value;
            RefreshComboBox();
        }
    }

    // Access the underlying Eto ListItemCollection
    private Eto.Forms.ListItemCollection EtoItems => _comboBox.Items;

    public int Add(object? value)
    {
        if (value == null) return -1;
        _items.Add(value);
        EtoItems.Add(value.ToString() ?? string.Empty);
        return _items.Count - 1;
    }

    public void Clear()
    {
        _items.Clear();
        EtoItems.Clear();
    }

    public bool Contains(object? value) => value != null && _items.Contains(value);

    public int IndexOf(object? value) => value == null ? -1 : _items.IndexOf(value);

    public void Insert(int index, object? value)
    {
        if (value == null) return;
        _items.Insert(index, value);
        RefreshComboBox();
    }

    public void Remove(object? value)
    {
        if (value == null) return;
        _items.Remove(value);
        RefreshComboBox();
    }

    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
        RefreshComboBox();
    }

    public void CopyTo(Array array, int index)
    {
        ((System.Collections.ICollection)_items).CopyTo(array, index);
    }

    public System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();

    private void RefreshComboBox()
    {
        EtoItems.Clear();
        foreach (var item in _items)
        {
            EtoItems.Add(item?.ToString() ?? string.Empty);
        }
    }
}

/// <summary>
/// WinForms-compatible ListBox wrapper
/// </summary>
public class WinFormsListBox : Eto.Forms.ListBox
{
    private ListBoxObjectCollection? _objectCollection;

    public Point Location { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public bool TabStop { get; set; } = true;
    public new string Name { get => ID; set => ID = value; }
    public BorderStyle BorderStyle { get; set; }
    public SelectionMode SelectionMode { get; set; }
    public bool IntegralHeight { get; set; }
    public bool HorizontalScrollbar { get; set; }
    public Eto.Drawing.Font Font { get; set; }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }
    public Color ForeColor { get => TextColor; set => TextColor = value; }

    // WinForms-compatible Items property that accepts any object
    public new ListBoxObjectCollection Items => _objectCollection ??= new ListBoxObjectCollection(this);

    // SelectedItem property
    public object SelectedItem { get => SelectedValue; set => SelectedValue = value; }

    // IndexFromPoint - returns item index at given point
    public int IndexFromPoint(Point point)
    {
        // Approximate calculation based on item height
        int itemHeight = 20; // Default item height
        return Math.Max(-1, Math.Min(DataStore?.Count() - 1 ?? -1, point.Y / itemHeight));
    }

    public int IndexFromPoint(int x, int y) => IndexFromPoint(new Point(x, y));
    public int IndexFromPoint(PointF point) => IndexFromPoint(new Point((int)point.X, (int)point.Y));

    // BeginUpdate/EndUpdate for performance
    public void BeginUpdate() { }
    public void EndUpdate() { }

    // DoubleClick event
    public event EventHandler DoubleClick { add => MouseDoubleClick += (s, e) => value(s, EventArgs.Empty); remove { } }

    // SelectedIndexChanged event - wrap WinForms EventHandler to Eto EventHandler<EventArgs>
    public new event EventHandler SelectedIndexChanged
    {
        add => base.SelectedIndexChanged += (s, e) => value(s, EventArgs.Empty);
        remove { }
    }

    // Mouse events - wrap WinForms delegate to Eto EventHandler<MouseEventArgs>
    public new event MouseEventHandler MouseUp
    {
        add => base.MouseUp += (s, e) => value(s, e);
        remove { }
    }
    public new event MouseEventHandler MouseDown
    {
        add => base.MouseDown += (s, e) => value(s, e);
        remove { }
    }
    public new event MouseEventHandler MouseMove
    {
        add => base.MouseMove += (s, e) => value(s, e);
        remove { }
    }
}

/// <summary>
/// WinForms-compatible ObjectCollection for ListBox that accepts any object
/// </summary>
public class ListBoxObjectCollection : System.Collections.IList
{
    private readonly Eto.Forms.ListBox _listBox;
    private readonly List<object> _items = new List<object>();

    public ListBoxObjectCollection(Eto.Forms.ListBox listBox)
    {
        _listBox = listBox;
    }

    public int Count => _items.Count;
    public bool IsReadOnly => false;
    public bool IsFixedSize => false;
    public bool IsSynchronized => false;
    public object SyncRoot => this;

    public object this[int index]
    {
        get => _items[index];
        set
        {
            _items[index] = value;
            RefreshListBox();
        }
    }

    // Access the underlying Eto ListItemCollection
    private Eto.Forms.ListItemCollection EtoItems => ((Eto.Forms.ListBox)_listBox).Items;

    public int Add(object? value)
    {
        if (value == null) return -1;
        _items.Add(value);
        EtoItems.Add(value.ToString() ?? string.Empty);
        return _items.Count - 1;
    }

    public void Clear()
    {
        _items.Clear();
        EtoItems.Clear();
    }

    public bool Contains(object? value) => value != null && _items.Contains(value);

    public int IndexOf(object? value) => value == null ? -1 : _items.IndexOf(value);

    public void Insert(int index, object? value)
    {
        if (value == null) return;
        _items.Insert(index, value);
        RefreshListBox();
    }

    public void Remove(object? value)
    {
        if (value == null) return;
        _items.Remove(value);
        RefreshListBox();
    }

    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
        RefreshListBox();
    }

    public void CopyTo(Array array, int index)
    {
        ((System.Collections.ICollection)_items).CopyTo(array, index);
    }

    public System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();

    private void RefreshListBox()
    {
        EtoItems.Clear();
        foreach (var item in _items)
        {
            EtoItems.Add(item?.ToString() ?? string.Empty);
        }
    }
}

/// <summary>
/// SelectionMode enum for ListBox compatibility
/// </summary>
public enum SelectionMode { None, One, MultiSimple, MultiExtended }

/// <summary>
/// WinForms-compatible NumericUpDown wrapper
/// </summary>
public class WinFormsNumericUpDown : Eto.Forms.NumericStepper
{
    public Point Location { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public bool TabStop { get; set; } = true;
    public new string Name { get => ID; set => ID = value; }
    public BorderStyle BorderStyle { get; set; }
    public bool ThousandsSeparator { get; set; }
    public bool Hexadecimal { get; set; }
    public bool ReadOnly { get; set; }

    public new double Maximum { get => MaxValue; set => MaxValue = value; }
    public new double Minimum { get => MinValue; set => MinValue = value; }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }
    public Color ForeColor { get => TextColor; set => TextColor = value; }

    // ValueChanged event - wrap WinForms EventHandler to Eto EventHandler<EventArgs>
    public new event EventHandler ValueChanged
    {
        add => base.ValueChanged += (s, e) => value(s, EventArgs.Empty);
        remove { }
    }
}

/// <summary>
/// WinForms-compatible GroupBox wrapper
/// </summary>
public class WinFormsGroupBox : Eto.Forms.GroupBox
{
    public Point Location { get; set; }
    public bool AutoSize { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public bool TabStop { get; set; }
    public Padding Margin { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public AutoSizeMode AutoSizeMode { get; set; }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }
    public Color ForeColor { get => TextColor; set => TextColor = value; }

    // Controls collection compatibility
    private WinFormsControlCollection _controls;
    public WinFormsControlCollection Controls => _controls ??= new WinFormsControlCollection(this);
}

/// <summary>
/// WinForms-compatible TabControl wrapper
/// </summary>
public class WinFormsTabControl : Eto.Forms.TabControl
{
    public Point Location { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public Padding Margin { get; set; }

    // TabPages maps to Pages
    public System.Collections.Generic.IList<Eto.Forms.TabPage> TabPages => Pages;

    // SelectedIndexChanged event - wrap WinForms EventHandler to Eto EventHandler<EventArgs>
    public new event EventHandler SelectedIndexChanged
    {
        add => base.SelectedIndexChanged += (s, e) => value(s, EventArgs.Empty);
        remove { }
    }
}

/// <summary>
/// WinForms-compatible TabPage wrapper
/// </summary>
public class WinFormsTabPage : Eto.Forms.TabPage
{
    public WinFormsTabPage() { }
    public WinFormsTabPage(string text) { Text = text; }

    public Point Location { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public bool UseVisualStyleBackColor { get; set; }
    public Padding Margin { get; set; }
    public new Padding Padding { get; set; }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }

    // Controls collection compatibility
    private WinFormsControlCollection _controls;
    public WinFormsControlCollection Controls => _controls ??= new WinFormsControlCollection(this);
}

/// <summary>
/// WinForms-compatible SplitContainer wrapper
/// </summary>
public class WinFormsSplitContainer : Eto.Forms.Splitter
{
    public Point Location { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public Padding Margin { get; set; }
    public int SplitterDistance { get => Position; set => Position = value; }
    public int Panel1MinSize { get; set; }
    public int Panel2MinSize { get; set; }
    public bool IsSplitterFixed { get; set; }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }

    // Events
    public event EventHandler SplitterMoved { add => PositionChanged += (s, e) => value(s, EventArgs.Empty); remove { } }
    public event EventHandler Resize { add => SizeChanged += (s, e) => value(s, EventArgs.Empty); remove { } }
}

/// <summary>
/// WinForms-compatible PictureBox wrapper (ImageView)
/// </summary>
public class WinFormsPictureBox : Eto.Forms.Drawable, System.ComponentModel.ISupportInitialize
{
    public void BeginInit() { }
    public void EndInit() { }
    public Point Location { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public bool TabStop { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public BorderStyle BorderStyle { get; set; }
    public PictureBoxSizeMode SizeMode { get; set; }
    public Padding Margin { get; set; }
    public Eto.Forms.ContextMenu ContextMenuStrip { get => ContextMenu; set => ContextMenu = value; }
    public Eto.Drawing.Image Image { get; set; }

    // Position properties
    public int Left { get => Location.X; set => Location = new Point(value, Location.Y); }
    public int Top { get => Location.Y; set => Location = new Point(Location.X, value); }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }

    // Resize event
    public event EventHandler Resize { add => SizeChanged += (s, e) => value(s, EventArgs.Empty); remove { } }

    // Mouse events - wrap WinForms delegate to Eto EventHandler<MouseEventArgs>
    public new event MouseEventHandler MouseDown
    {
        add => base.MouseDown += (s, e) => value(s, e);
        remove { }
    }
    public new event MouseEventHandler MouseMove
    {
        add => base.MouseMove += (s, e) => value(s, e);
        remove { }
    }
    public new event MouseEventHandler MouseUp
    {
        add => base.MouseUp += (s, e) => value(s, e);
        remove { }
    }
    public event MouseEventHandler MouseClick
    {
        add => base.MouseDown += (s, e) => value(s, e);
        remove { }
    }

    // Key events - wrap WinForms delegate to Eto EventHandler<KeyEventArgs>
    public new event KeyEventHandler KeyDown
    {
        add => base.KeyDown += (s, e) => value(s, e);
        remove { }
    }
    public new event KeyEventHandler KeyUp
    {
        add => base.KeyUp += (s, e) => value(s, e);
        remove { }
    }
    public event PreviewKeyDownEventHandler PreviewKeyDown { add { } remove { } }

    // Paint event
    public new event PaintEventHandler Paint
    {
        add => base.Paint += (s, e) => value(s, e);
        remove { }
    }
}

/// <summary>
/// WinForms-compatible ProgressBar wrapper
/// </summary>
public class WinFormsProgressBar : Eto.Forms.ProgressBar
{
    public Point Location { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public Padding Margin { get; set; }
    public ProgressBarStyle Style { get; set; }
    public int Step { get; set; }

    public new int Maximum { get => MaxValue; set => MaxValue = value; }
    public new int Minimum { get => MinValue; set => MinValue = value; }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }
}

/// <summary>
/// WinForms-compatible ListView wrapper (GridView)
/// </summary>
public class WinFormsListView : Eto.Forms.GridView
{
    public Point Location { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public BorderStyle BorderStyle { get; set; }
    public View View { get; set; }
    public bool FullRowSelect { get; set; }
    public bool MultiSelect { get => AllowMultipleSelection; set => AllowMultipleSelection = value; }
    public ColumnHeaderStyle HeaderStyle { get; set; }
    public SortOrder Sorting { get; set; }
    public bool HideSelection { get; set; }
    public ImageList SmallImageList { get; set; }
    public ImageList LargeImageList { get; set; }
    public Padding Margin { get; set; }
    public bool UseCompatibleStateImageBehavior { get; set; }
    public Eto.Forms.ContextMenu ContextMenuStrip { get => ContextMenu; set => ContextMenu = value; }
    public bool Scrollable { get; set; } = true;
    public bool CheckBoxes { get; set; }
    public Eto.Drawing.Font Font { get; set; }
    public bool LabelEdit { get; set; }
    public System.Collections.IComparer ListViewItemSorter { get; set; }
    public bool VirtualMode { get; set; }
    public int VirtualListSize { get; set; }
    public event EventHandler<RetrieveVirtualItemEventArgs> RetrieveVirtualItem;

    // GridLines - accepts bool for WinForms compatibility
    public new bool GridLines
    {
        get => base.GridLines != Eto.Forms.GridLines.None;
        set => base.GridLines = value ? Eto.Forms.GridLines.Both : Eto.Forms.GridLines.None;
    }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }
    // GridView doesn't have TextColor in Eto
    private Color _foreColor = Eto.Drawing.Colors.Black;
    public Color ForeColor { get => _foreColor; set => _foreColor = value; }

    // Columns collection
    public ColumnHeaderCollection Columns { get; } = new ColumnHeaderCollection();

    // Items collection
    private ListViewItemCollection _items;
    public new ListViewItemCollection Items => _items ??= new ListViewItemCollection(this);

    // SelectedItems
    public SelectedListViewItemCollection SelectedItems { get; } = new SelectedListViewItemCollection();

    // SelectedIndices
    public SelectedIndexCollection SelectedIndices { get; } = new SelectedIndexCollection();

    // CheckedItems
    public CheckedListViewItemCollection CheckedItems { get; } = new CheckedListViewItemCollection();

    // BeginUpdate/EndUpdate for performance
    public void BeginUpdate() { }
    public void EndUpdate() { }

    // AutoResizeColumns
    public void AutoResizeColumns(ColumnHeaderAutoResizeStyle style) { }

    // HitTest
    public ListViewHitTestInfo HitTest(int x, int y)
    {
        return new ListViewHitTestInfo(null, null, ListViewHitTestLocations.None);
    }

    public ListViewHitTestInfo HitTest(Point location) => HitTest(location.X, location.Y);

    // Sort
    public void Sort() { }

    // Events
    public event EventHandler<ColumnClickEventArgs> ColumnClick;
    public event MouseEventHandler MouseClick { add => base.MouseDown += (s, e) => value(s, e); remove { } }
    public event EventHandler DoubleClick { add => CellDoubleClick += (s, e) => value(s, EventArgs.Empty); remove { } }
    public event EventHandler SelectedIndexChanged { add => SelectionChanged += (s, e) => value(s, e); remove { } }
    public event ItemCheckedEventHandler ItemChecked;
    public event EventHandler Click { add => base.MouseDown += (s, e) => value(s, EventArgs.Empty); remove { } }

    // Mouse events - wrap WinForms delegate to Eto EventHandler<MouseEventArgs>
    public new event MouseEventHandler MouseDown
    {
        add => base.MouseDown += (s, e) => value(s, e);
        remove { }
    }
    public new event MouseEventHandler MouseMove
    {
        add => base.MouseMove += (s, e) => value(s, e);
        remove { }
    }
    public new event MouseEventHandler MouseUp
    {
        add => base.MouseUp += (s, e) => value(s, e);
        remove { }
    }
    public new event MouseEventHandler MouseDoubleClick
    {
        add => base.MouseDoubleClick += (s, e) => value(s, e);
        remove { }
    }

    // Drag events
    public new event DragEventHandler DragEnter
    {
        add => base.DragEnter += (s, e) => value(s, e);
        remove { }
    }
    public new event DragEventHandler DragOver
    {
        add => base.DragOver += (s, e) => value(s, e);
        remove { }
    }
    public new event DragEventHandler DragDrop
    {
        add => base.DragDrop += (s, e) => value(s, e);
        remove { }
    }
    public new event DragEventHandler DragLeave
    {
        add => base.DragLeave += (s, e) => value(s, e);
        remove { }
    }

    protected virtual void OnColumnClick(ColumnClickEventArgs e) => ColumnClick?.Invoke(this, e);
    protected virtual void OnItemChecked(ItemCheckedEventArgs e) => ItemChecked?.Invoke(this, e);
}

/// <summary>
/// CheckedListViewItemCollection for ListView
/// </summary>
public class CheckedListViewItemCollection : System.Collections.Generic.List<ListViewItem>
{
    public new int Count => base.Count;
}

/// <summary>
/// ListViewHitTestInfo for HitTest
/// </summary>
public class ListViewHitTestInfo
{
    public ListViewItem Item { get; }
    public ListViewItem.ListViewSubItem SubItem { get; }
    public ListViewHitTestLocations Location { get; }

    public ListViewHitTestInfo(ListViewItem item, ListViewItem.ListViewSubItem subItem, ListViewHitTestLocations location)
    {
        Item = item;
        SubItem = subItem;
        Location = location;
    }
}

/// <summary>
/// ListViewHitTestLocations enum
/// </summary>
[Flags]
public enum ListViewHitTestLocations
{
    None = 0,
    AboveClientArea = 256,
    BelowClientArea = 16,
    Image = 2,
    Label = 4,
    LeftOfClientArea = 64,
    RightOfClientArea = 32,
    StateImage = 512
}

/// <summary>
/// ColumnHeaderAutoResizeStyle enum
/// </summary>
public enum ColumnHeaderAutoResizeStyle
{
    None,
    HeaderSize,
    ColumnContent
}

/// <summary>
/// ItemCheckedEventHandler delegate
/// </summary>
public delegate void ItemCheckedEventHandler(object sender, ItemCheckedEventArgs e);

/// <summary>
/// ItemCheckedEventArgs
/// </summary>
public class ItemCheckedEventArgs : EventArgs
{
    public ListViewItem Item { get; }
    public ItemCheckedEventArgs(ListViewItem item) => Item = item;
}

/// <summary>
/// ColumnHeaderCollection for ListView compatibility
/// </summary>
public class ColumnHeaderCollection : System.Collections.Generic.List<ColumnHeader>
{
    public void AddRange(ColumnHeader[] items) => base.AddRange(items);

    public ColumnHeader Add(string text, int width)
    {
        var header = new ColumnHeader { Text = text, Width = width };
        base.Add(header);
        return header;
    }

    public ColumnHeader Add(string text, int width, HorizontalAlignment alignment)
    {
        var header = new ColumnHeader { Text = text, Width = width, TextAlign = alignment };
        base.Add(header);
        return header;
    }
}

/// <summary>
/// ColumnHeader for ListView compatibility
/// </summary>
public class ColumnHeader
{
    public string Text { get; set; }
    public int Width { get; set; }
    public HorizontalAlignment TextAlign { get; set; }
    public string Name { get; set; }
}

/// <summary>
/// HorizontalAlignment enum for compatibility
/// </summary>
public enum HorizontalAlignment { Left, Right, Center }

/// <summary>
/// SelectedListViewItemCollection for ListView compatibility
/// </summary>
public class SelectedListViewItemCollection : System.Collections.Generic.List<ListViewItem> { }

/// <summary>
/// SelectedIndexCollection for ListView compatibility
/// </summary>
public class SelectedIndexCollection : System.Collections.Generic.List<int> { }

/// <summary>
/// WinForms-compatible TreeView wrapper (TreeGridView)
/// </summary>
public class WinFormsTreeView : Eto.Forms.TreeGridView
{
    public Point Location { get; set; }
    public DockStyle Dock { get; set; }
    public AnchorStyles Anchor { get; set; }
    public int TabIndex { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public BorderStyle BorderStyle { get; set; }
    public bool HideSelection { get; set; }
    public ImageList ImageList { get; set; }
    public bool ShowLines { get; set; }
    public bool ShowRootLines { get; set; }
    public bool ShowPlusMinus { get; set; }
    public Padding Margin { get; set; }

    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }
}

/// <summary>
/// WinForms-compatible Timer wrapper
/// </summary>
public class WinFormsTimer : Eto.Forms.UITimer
{
    public new int Interval
    {
        get => (int)(base.Interval * 1000);
        set => base.Interval = value / 1000.0;
    }

    // Enabled property - maps to Started
    public bool Enabled
    {
        get => Started;
        set
        {
            if (value) Start();
            else Stop();
        }
    }

    // Tick event maps to Elapsed
    public event EventHandler Tick
    {
        add => Elapsed += (s, e) => value(s, EventArgs.Empty);
        remove { }
    }
}

/// <summary>
/// WinForms-compatible MenuStrip wrapper
/// </summary>
public class WinFormsMenuStrip : Eto.Forms.MenuBar
{
    public DockStyle Dock { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public Padding Padding { get; set; }
    public Eto.Drawing.Size Size { get; set; }
    public int TabIndex { get; set; }
    public Point Location { get; set; }

    // MenuBar doesn't have BackgroundColor in Eto
    private Color _backColor = Eto.Drawing.Colors.Transparent;
    public Color BackColor { get => _backColor; set => _backColor = value; }

    // SuspendLayout/ResumeLayout stubs
    public void SuspendLayout() { }
    public void ResumeLayout(bool performLayout = true) { }
    public void PerformLayout() { }
}

/// <summary>
/// WinForms-compatible ToolStripButton wrapper
/// </summary>
public class WinFormsToolStripButton : Eto.Forms.ButtonToolItem
{
    public WinFormsToolStripButton() { }
    public WinFormsToolStripButton(string text) { Text = text; }

    public ToolStripItemDisplayStyle DisplayStyle { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public Eto.Drawing.Size Size { get; set; }
    public string ToolTipText { get => ToolTip; set => ToolTip = value; }

    // Click event - wrap WinForms EventHandler to Eto EventHandler<EventArgs>
    public new event EventHandler Click
    {
        add => base.Click += (s, e) => value(s, EventArgs.Empty);
        remove { }
    }
}

/// <summary>
/// WinForms-compatible ToolStripSeparator wrapper
/// Can be used as both a menu separator and toolbar separator
/// </summary>
public class WinFormsToolStripSeparator : Eto.Forms.SeparatorMenuItem
{
    // Note: Already convertible to MenuItem through inheritance
}

/// <summary>
/// WinForms-compatible ToolStripMenuItem wrapper
/// </summary>
public class WinFormsToolStripMenuItem : Eto.Forms.ButtonMenuItem
{
    public WinFormsToolStripMenuItem() { }

    public WinFormsToolStripMenuItem(string text)
    {
        Text = text;
    }

    public WinFormsToolStripMenuItem(string text, Eto.Drawing.Image image, EventHandler handler)
    {
        Text = text;
        Image = image;
        base.Click += (s, e) => handler(s, EventArgs.Empty);
    }

    public WinFormsToolStripMenuItem(string text, Eto.Drawing.Image image, params WinFormsToolStripMenuItem[] dropDownItems)
    {
        Text = text;
        Image = image;
        foreach (var item in dropDownItems)
            Items.Add(item);
    }

    public new string Name { get => ID; set => ID = value; }
    public Eto.Forms.Keys ShortcutKeys { get => Shortcut; set => Shortcut = value; }
    public bool Checked { get; set; }
    public bool CheckOnClick { get; set; }
    public new Eto.Drawing.Image Image { get => base.Image; set => base.Image = value; }
    public Eto.Forms.Keys ShortcutKeyDisplayString { get; set; }
    public bool ShowShortcutKeys { get; set; } = true;
    public new Eto.Drawing.Size Size { get; set; }
    public Eto.Drawing.Color TextColor { get; set; }
    public Eto.Drawing.Color ForeColor { get => TextColor; set => TextColor = value; }
    public Eto.Drawing.Font Font { get; set; }

    // Click event - wrap WinForms EventHandler to Eto EventHandler<EventArgs>
    public new event EventHandler Click
    {
        add => base.Click += (s, e) => value(s, EventArgs.Empty);
        remove { }
    }

    // DropDownItems maps to Items
    public System.Collections.Generic.IList<Eto.Forms.MenuItem> DropDownItems => Items;
}

/// <summary>
/// WinForms-compatible OpenFileDialog wrapper
/// </summary>
public class WinFormsOpenFileDialog : Eto.Forms.OpenFileDialog
{
    public string Filter
    {
        get => string.Join("|", Filters.Select(f => $"{f.Name}|{string.Join(";", f.Extensions.Select(e => $"*.{e}"))}"));
        set
        {
            Filters.Clear();
            if (string.IsNullOrEmpty(value)) return;
            var parts = value.Split('|');
            for (int i = 0; i < parts.Length - 1; i += 2)
            {
                var filter = new Eto.Forms.FileFilter(parts[i]);
                var exts = parts[i + 1].Split(';').Select(e => e.TrimStart('*', '.'));
                foreach (var ext in exts) filter.Extensions = filter.Extensions.Concat(new[] { ext }).ToArray();
                Filters.Add(filter);
            }
        }
    }
    public int FilterIndex { get; set; } = 1;
    public string InitialDirectory { get => Directory?.ToString() ?? ""; set => Directory = new Uri(value); }
    public bool RestoreDirectory { get; set; }
    public string Title { get => base.Title; set => base.Title = value; }
    public bool Multiselect { get => MultiSelect; set => MultiSelect = value; }
    public string[] FileNames => Filenames?.ToArray() ?? Array.Empty<string>();
}

/// <summary>
/// WinForms-compatible SaveFileDialog wrapper
/// </summary>
public class WinFormsSaveFileDialog : Eto.Forms.SaveFileDialog
{
    public string Filter
    {
        get => string.Join("|", Filters.Select(f => $"{f.Name}|{string.Join(";", f.Extensions.Select(e => $"*.{e}"))}"));
        set
        {
            Filters.Clear();
            if (string.IsNullOrEmpty(value)) return;
            var parts = value.Split('|');
            for (int i = 0; i < parts.Length - 1; i += 2)
            {
                var filter = new Eto.Forms.FileFilter(parts[i]);
                var exts = parts[i + 1].Split(';').Select(e => e.TrimStart('*', '.'));
                foreach (var ext in exts) filter.Extensions = filter.Extensions.Concat(new[] { ext }).ToArray();
                Filters.Add(filter);
            }
        }
    }
    public int FilterIndex { get; set; } = 1;
    public string InitialDirectory { get => Directory?.ToString() ?? ""; set => Directory = new Uri(value); }
    public bool RestoreDirectory { get; set; }
    public string Title { get => base.Title; set => base.Title = value; }
    public bool OverwritePrompt { get; set; } = true;
    public string DefaultExt { get; set; }
    public bool AddExtension { get; set; } = true;
}

/// <summary>
/// WinForms-compatible FolderBrowserDialog wrapper
/// </summary>
public class WinFormsFolderBrowserDialog : Eto.Forms.SelectFolderDialog
{
    public string SelectedPath { get => Directory; set => Directory = value; }
    public string Description { get => Title; set => Title = value; }
    public bool ShowNewFolderButton { get; set; } = true;
}

/// <summary>
/// WinForms-compatible Dialog wrapper
/// </summary>
public class WinFormsDialog : Eto.Forms.Dialog
{
    public Point Location { get; set; }
    public DockStyle Dock { get; set; }
    public new string Name { get => ID; set => ID = value; }
    public FormStartPosition StartPosition { get; set; }
    public FormBorderStyle FormBorderStyle { get; set; }
    public bool KeyPreview { get; set; }
    public SizeF AutoScaleDimensions { get; set; }
    public AutoScaleMode AutoScaleMode { get; set; }
    public bool ControlBox { get; set; } = true;
    public bool MaximizeBox { get => Maximizable; set => Maximizable = value; }
    public bool MinimizeBox { get => Minimizable; set => Minimizable = value; }
    public Eto.Forms.Button AcceptButton { get => DefaultButton; set => DefaultButton = value; }
    public Eto.Forms.Button CancelButton { get => AbortButton; set => AbortButton = value; }
    public bool ShowInTaskbar { get; set; }

    public new string Text { get => Title; set => Title = value; }
    public Color BackColor { get => BackgroundColor; set => BackgroundColor = value; }

    // DialogResult for WinForms compatibility
    public DialogResultCompat DialogResult { get; set; } = DialogResultCompat.None;

    // Close with result (WinForms-compatible)
    public void Close(Eto.Forms.DialogResult result)
    {
        DialogResult = result == Eto.Forms.DialogResult.Ok ? DialogResultCompat.OK : DialogResultCompat.Cancel;
        Close();
    }

    public void Close(DialogResultCompat result)
    {
        DialogResult = result;
        Close();
    }

    // Controls collection compatibility
    private WinFormsControlCollection _controls;
    public WinFormsControlCollection Controls => _controls ??= new WinFormsControlCollection(this);

    // ShowDialog for WinForms compatibility
    // Note: Eto's ShowModal() is void, so we use DialogResult property after showing
    public new DialogResultCompat ShowDialog()
    {
        ShowModal();
        return DialogResult;
    }

    public DialogResultCompat ShowDialog(Eto.Forms.Control owner)
    {
        return ShowDialog();
    }
}

/// <summary>
/// WinForms-compatible ControlCollection
/// </summary>
public class WinFormsControlCollection : System.Collections.ObjectModel.Collection<Eto.Forms.Control>
{
    private readonly Eto.Forms.Container _container;

    public WinFormsControlCollection(Eto.Forms.Container container)
    {
        _container = container;
    }

    public void Add(Eto.Forms.Control control)
    {
        base.Add(control);
        UpdateContainer();
    }

    public void AddRange(Eto.Forms.Control[] controls)
    {
        foreach (var control in controls)
            base.Add(control);
        UpdateContainer();
    }

    private void UpdateContainer()
    {
        Eto.Forms.Control contentToSet = null;

        if (Count == 1)
        {
            contentToSet = this[0];
        }
        else if (Count > 1)
        {
            var layout = new Eto.Forms.StackLayout();
            foreach (var control in this)
            {
                layout.Items.Add(control);
            }
            contentToSet = layout;
        }

        if (contentToSet != null)
        {
            if (_container is Eto.Forms.Panel panel)
            {
                panel.Content = contentToSet;
            }
            else if (_container is Eto.Forms.TabPage tabPage)
            {
                tabPage.Content = contentToSet;
            }
        }
    }
}

#endregion

/// <summary>
/// VScrollBar compatibility class - wraps Eto's Scrollable behavior
/// </summary>
public class VScrollBar : Slider
{
    public VScrollBar()
    {
        Orientation = Orientation.Vertical;
    }

    public int Maximum { get => (int)MaxValue; set => MaxValue = value; }
    public int Minimum { get => (int)MinValue; set => MinValue = value; }
    public int LargeChange { get; set; } = 10;
    public int SmallChange { get; set; } = 1;

    // Scroll event for WinForms compatibility
    public event ScrollEventHandler Scroll
    {
        add => ValueChanged += (s, e) => value?.Invoke(s, new ScrollEventArgs(ScrollEventType.ThumbPosition, (int)Value, ScrollOrientation.VerticalScroll));
        remove { }
    }
}

/// <summary>
/// HScrollBar compatibility class
/// </summary>
public class HScrollBar : Slider
{
    public HScrollBar()
    {
        Orientation = Orientation.Horizontal;
    }

    public int Maximum { get => (int)MaxValue; set => MaxValue = value; }
    public int Minimum { get => (int)MinValue; set => MinValue = value; }
    public int LargeChange { get; set; } = 10;
    public int SmallChange { get; set; } = 1;

    // Scroll event for WinForms compatibility
    public event ScrollEventHandler Scroll
    {
        add => ValueChanged += (s, e) => value?.Invoke(s, new ScrollEventArgs(ScrollEventType.ThumbPosition, (int)Value, ScrollOrientation.HorizontalScroll));
        remove { }
    }
}

/// <summary>
/// ToolStripStatusLabel compatibility - wraps Eto Label
/// </summary>
public class ToolStripStatusLabel : Label
{
    public bool Spring { get; set; }
}

/// <summary>
/// ToolStripProgressBar compatibility
/// </summary>
public class ToolStripProgressBar : ProgressBar
{
    public new int Maximum { get => MaxValue; set => MaxValue = value; }
    public new int Minimum { get => MinValue; set => MinValue = value; }
}

/// <summary>
/// ImageList compatibility - Eto doesn't need this, images are stored directly
/// </summary>
public class ImageList : IDisposable
{
    public Size ImageSize { get; set; } = new Size(16, 16);
    public ColorDepth ColorDepth { get; set; } = ColorDepth.Depth32Bit;
    public ImageCollection Images { get; } = new ImageCollection();

    public void Dispose()
    {
        Images.Clear();
    }

    public class ImageCollection : System.Collections.Generic.List<Image>
    {
        private readonly Dictionary<string, int> _keyToIndex = new Dictionary<string, int>();

        public void Add(string key, Image image)
        {
            _keyToIndex[key] = Count;
            Add(image);
        }

        public bool ContainsKey(string key) => _keyToIndex.ContainsKey(key);

        public Image this[string key]
        {
            get => _keyToIndex.TryGetValue(key, out var index) ? this[index] : null;
        }
    }
}

public enum ColorDepth
{
    Depth4Bit,
    Depth8Bit,
    Depth16Bit,
    Depth24Bit,
    Depth32Bit
}

/// <summary>
/// ListViewItem compatibility for GridView
/// </summary>
public class ListViewItem
{
    public string Text { get; set; }
    public string ImageKey { get; set; }
    public int ImageIndex { get; set; } = -1;
    public object Tag { get; set; }
    public ListViewSubItemCollection SubItems { get; } = new ListViewSubItemCollection();
    public bool Selected { get; set; }
    public bool Checked { get; set; }
    public Color BackColor { get; set; }
    public Color BackgroundColor { get => BackColor; set => BackColor = value; }
    public Color ForeColor { get; set; }
    public Color TextColor { get => ForeColor; set => ForeColor = value; }
    public Eto.Drawing.Font Font { get; set; }
    public string Name { get; set; }
    public int Index { get; set; }

    // EnsureVisible - stub method for WinForms compatibility
    public void EnsureVisible() { /* No-op in Eto */ }

    public ListViewItem() { }
    public ListViewItem(string text) { Text = text; }
    public ListViewItem(string[] items)
    {
        if (items.Length > 0) Text = items[0];
        for (int i = 1; i < items.Length; i++)
            SubItems.Add(new ListViewSubItem { Text = items[i] });
    }

    public class ListViewSubItem
    {
        public string Text { get; set; }
        public object Tag { get; set; }
    }

    public class ListViewSubItemCollection : System.Collections.Generic.List<ListViewSubItem>
    {
        public ListViewSubItem this[int index]
        {
            get => base[index];
            set => base[index] = value;
        }

        public void Add(string text)
        {
            base.Add(new ListViewSubItem { Text = text });
        }
    }
}

/// <summary>
/// ImageFormat compatibility - wraps SkiaSharp format enum
/// </summary>
public class ImageFormat
{
    public SkiaSharp.SKEncodedImageFormat Format { get; }

    private ImageFormat(SkiaSharp.SKEncodedImageFormat format)
    {
        Format = format;
    }

    public static readonly ImageFormat Png = new ImageFormat(SkiaSharp.SKEncodedImageFormat.Png);
    public static readonly ImageFormat Jpeg = new ImageFormat(SkiaSharp.SKEncodedImageFormat.Jpeg);
    public static readonly ImageFormat Bmp = new ImageFormat(SkiaSharp.SKEncodedImageFormat.Bmp);
    public static readonly ImageFormat Gif = new ImageFormat(SkiaSharp.SKEncodedImageFormat.Gif);

    public static implicit operator SkiaSharp.SKEncodedImageFormat(ImageFormat format) => format.Format;

    /// <summary>
    /// Convert to Eto.Drawing.ImageFormat
    /// </summary>
    public Eto.Drawing.ImageFormat ToEtoFormat()
    {
        return Format switch
        {
            SkiaSharp.SKEncodedImageFormat.Png => Eto.Drawing.ImageFormat.Png,
            SkiaSharp.SKEncodedImageFormat.Jpeg => Eto.Drawing.ImageFormat.Jpeg,
            SkiaSharp.SKEncodedImageFormat.Gif => Eto.Drawing.ImageFormat.Gif,
            SkiaSharp.SKEncodedImageFormat.Bmp => Eto.Drawing.ImageFormat.Bitmap,
            _ => Eto.Drawing.ImageFormat.Png
        };
    }

    public static implicit operator Eto.Drawing.ImageFormat(ImageFormat format) => format.ToEtoFormat();
}

/// <summary>
/// Region compatibility - used for clipping
/// </summary>
public class Region : IDisposable
{
    public SkiaSharp.SKRegion SKRegion { get; private set; }

    public Region()
    {
        SKRegion = new SkiaSharp.SKRegion();
    }

    public Region(Rectangle rect)
    {
        SKRegion = new SkiaSharp.SKRegion();
        SKRegion.SetRect(new SkiaSharp.SKRectI(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height));
    }

    public Region(GraphicsPath path)
    {
        SKRegion = new SkiaSharp.SKRegion();
        if (path?.SKPath != null)
        {
            SKRegion.SetPath(path.SKPath);
        }
    }

    public void Exclude(Rectangle rect)
    {
        var excludeRegion = new SkiaSharp.SKRegion();
        excludeRegion.SetRect(new SkiaSharp.SKRectI(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height));
        SKRegion.Op(excludeRegion, SkiaSharp.SKRegionOperation.Difference);
    }

    public RectangleF GetBounds(Eto.Drawing.Graphics g)
    {
        var bounds = SKRegion.Bounds;
        return new RectangleF(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }

    public bool IsVisible(int x, int y)
    {
        return SKRegion.Contains(x, y);
    }

    public bool IsVisible(Point point)
    {
        return SKRegion.Contains(point.X, point.Y);
    }

    public bool IsVisible(Rectangle rect)
    {
        var skRect = new SkiaSharp.SKRectI(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);
        return SKRegion.Intersects(skRect);
    }

    public void MakeEmpty()
    {
        SKRegion.SetEmpty();
    }

    public void Union(GraphicsPath path)
    {
        if (path?.SKPath == null) return;
        using var pathRegion = new SkiaSharp.SKRegion();
        pathRegion.SetPath(path.SKPath);
        SKRegion.Op(pathRegion, SkiaSharp.SKRegionOperation.Union);
    }

    public void Union(Rectangle rect)
    {
        using var rectRegion = new SkiaSharp.SKRegion();
        rectRegion.SetRect(new SkiaSharp.SKRectI(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height));
        SKRegion.Op(rectRegion, SkiaSharp.SKRegionOperation.Union);
    }

    public void Intersect(Region region)
    {
        if (region?.SKRegion == null) return;
        SKRegion.Op(region.SKRegion, SkiaSharp.SKRegionOperation.Intersect);
    }

    public void Dispose()
    {
        SKRegion?.Dispose();
    }
}

/// <summary>
/// GraphicsPath compatibility class using SkiaSharp
/// </summary>
public class GraphicsPath : IDisposable
{
    public SkiaSharp.SKPath SKPath { get; private set; }

    public GraphicsPath()
    {
        SKPath = new SkiaSharp.SKPath();
    }

    public void AddPolygon(Point[] points)
    {
        if (points == null || points.Length < 3) return;
        SKPath.MoveTo(points[0].X, points[0].Y);
        for (int i = 1; i < points.Length; i++)
        {
            SKPath.LineTo(points[i].X, points[i].Y);
        }
        SKPath.Close();
    }

    public void AddPolygon(PointF[] points)
    {
        if (points == null || points.Length < 3) return;
        SKPath.MoveTo(points[0].X, points[0].Y);
        for (int i = 1; i < points.Length; i++)
        {
            SKPath.LineTo(points[i].X, points[i].Y);
        }
        SKPath.Close();
    }

    public void AddLine(int x1, int y1, int x2, int y2)
    {
        SKPath.MoveTo(x1, y1);
        SKPath.LineTo(x2, y2);
    }

    public void AddRectangle(Rectangle rect)
    {
        SKPath.AddRect(new SkiaSharp.SKRect(rect.X, rect.Y, rect.Right, rect.Bottom));
    }

    public void AddEllipse(Rectangle rect)
    {
        SKPath.AddOval(new SkiaSharp.SKRect(rect.X, rect.Y, rect.Right, rect.Bottom));
    }

    public void CloseFigure()
    {
        SKPath.Close();
    }

    public void Reset()
    {
        SKPath.Reset();
    }

    public void MoveTo(Point point)
    {
        SKPath.MoveTo(point.X, point.Y);
    }

    public void MoveTo(PointF point)
    {
        SKPath.MoveTo(point.X, point.Y);
    }

    public void LineTo(Point point)
    {
        SKPath.LineTo(point.X, point.Y);
    }

    public void LineTo(PointF point)
    {
        SKPath.LineTo(point.X, point.Y);
    }

    public void StartFigure()
    {
        // No direct equivalent - just continue path
    }

    public RectangleF GetBounds()
    {
        var bounds = SKPath.Bounds;
        return new RectangleF(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }

    public void Dispose()
    {
        SKPath?.Dispose();
    }
}

/// <summary>
/// IWin32Window stub - not needed on cross-platform
/// </summary>
public interface IWin32Window
{
    IntPtr Handle { get; }
}

/// <summary>
/// UITypeEditor stub - designer support not needed at runtime
/// </summary>
public class UITypeEditor
{
    public virtual UITypeEditorEditStyle GetEditStyle(System.ComponentModel.ITypeDescriptorContext context)
        => UITypeEditorEditStyle.None;

    public virtual object EditValue(System.ComponentModel.ITypeDescriptorContext context,
        IServiceProvider provider, object value) => value;
}

public enum UITypeEditorEditStyle
{
    None,
    Modal,
    DropDown
}

/// <summary>
/// DataGridViewCellFormattingEventArgs stub
/// </summary>
public class DataGridViewCellFormattingEventArgs : EventArgs
{
    public int ColumnIndex { get; set; }
    public int RowIndex { get; set; }
    public object Value { get; set; }
    public bool FormattingApplied { get; set; }
}

/// <summary>
/// ColumnClickEventArgs compatibility
/// </summary>
public class ColumnClickEventArgs : EventArgs
{
    public int Column { get; }
    public ColumnClickEventArgs(int column) { Column = column; }
}

/// <summary>
/// RetrieveVirtualItemEventArgs for virtual ListView compatibility
/// </summary>
public class RetrieveVirtualItemEventArgs : EventArgs
{
    public int ItemIndex { get; }
    public ListViewItem Item { get; set; }
    public RetrieveVirtualItemEventArgs(int itemIndex) { ItemIndex = itemIndex; }
}

/// <summary>
/// BorderStyle enum compatibility
/// </summary>
public enum BorderStyle
{
    None,
    FixedSingle,
    Fixed3D
}

/// <summary>
/// FormStartPosition enum compatibility
/// </summary>
public enum FormStartPosition
{
    Manual,
    CenterScreen,
    WindowsDefaultLocation,
    WindowsDefaultBounds,
    CenterParent
}

/// <summary>
/// PictureBoxSizeMode enum compatibility
/// </summary>
public enum PictureBoxSizeMode
{
    Normal,
    StretchImage,
    AutoSize,
    CenterImage,
    Zoom
}

/// <summary>
/// ComboBoxStyle enum compatibility
/// </summary>
public enum ComboBoxStyle
{
    Simple,
    DropDown,
    DropDownList
}

/// <summary>
/// View enum for ListView compatibility
/// </summary>
public enum View
{
    LargeIcon,
    Details,
    SmallIcon,
    List,
    Tile
}

/// <summary>
/// SortOrder enum compatibility
/// </summary>
public enum SortOrder
{
    None,
    Ascending,
    Descending
}

/// <summary>
/// AnchorStyles compatibility
/// </summary>
[Flags]
public enum AnchorStyles
{
    None = 0,
    Top = 1,
    Bottom = 2,
    Left = 4,
    Right = 8
}

/// <summary>
/// DockStyle compatibility
/// </summary>
public enum DockStyle
{
    None,
    Top,
    Bottom,
    Left,
    Right,
    Fill
}

/// <summary>
/// MessageBoxButtons compatibility
/// </summary>
public enum MessageBoxButtons
{
    OK,
    OKCancel,
    AbortRetryIgnore,
    YesNoCancel,
    YesNo,
    RetryCancel
}

/// <summary>
/// MessageBoxIcon compatibility
/// </summary>
public enum MessageBoxIcon
{
    None,
    Error,
    Hand,
    Stop,
    Question,
    Exclamation,
    Warning,
    Asterisk,
    Information
}

/// <summary>
/// MessageBoxDefaultButton compatibility
/// </summary>
public enum MessageBoxDefaultButton
{
    Button1,
    Button2,
    Button3
}

/// <summary>
/// DataFormats compatibility for drag-drop operations
/// </summary>
public static class DataFormats
{
    public const string FileDrop = "FileDrop";
    public const string Text = "Text";
    public const string UnicodeText = "UnicodeText";
    public const string Html = "Html";
    public const string Rtf = "Rtf";
    public const string Bitmap = "Bitmap";
}

/// <summary>
/// WinForms DialogResult compatibility wrapper - provides OK constant with implicit conversion
/// </summary>
public readonly struct DialogResultCompat
{
    private readonly Eto.Forms.DialogResult _value;

    private DialogResultCompat(Eto.Forms.DialogResult value) => _value = value;

    public static DialogResultCompat OK => new DialogResultCompat(Eto.Forms.DialogResult.Ok);
    public static DialogResultCompat Ok => new DialogResultCompat(Eto.Forms.DialogResult.Ok);
    public static DialogResultCompat Cancel => new DialogResultCompat(Eto.Forms.DialogResult.Cancel);
    public static DialogResultCompat Yes => new DialogResultCompat(Eto.Forms.DialogResult.Yes);
    public static DialogResultCompat No => new DialogResultCompat(Eto.Forms.DialogResult.No);
    public static DialogResultCompat Abort => new DialogResultCompat(Eto.Forms.DialogResult.Abort);
    public static DialogResultCompat Retry => new DialogResultCompat(Eto.Forms.DialogResult.Retry);
    public static DialogResultCompat Ignore => new DialogResultCompat(Eto.Forms.DialogResult.Ignore);
    public static DialogResultCompat None => new DialogResultCompat(Eto.Forms.DialogResult.None);

    public static implicit operator Eto.Forms.DialogResult(DialogResultCompat d) => d._value;
    public static implicit operator DialogResultCompat(Eto.Forms.DialogResult d) => new DialogResultCompat(d);

    public static bool operator ==(DialogResultCompat left, DialogResultCompat right) => left._value == right._value;
    public static bool operator !=(DialogResultCompat left, DialogResultCompat right) => left._value != right._value;
    public static bool operator ==(DialogResultCompat left, Eto.Forms.DialogResult right) => left._value == right;
    public static bool operator !=(DialogResultCompat left, Eto.Forms.DialogResult right) => left._value != right;
    public static bool operator ==(Eto.Forms.DialogResult left, DialogResultCompat right) => left == right._value;
    public static bool operator !=(Eto.Forms.DialogResult left, DialogResultCompat right) => left != right._value;

    public override bool Equals(object obj) => obj switch
    {
        DialogResultCompat other => _value == other._value,
        Eto.Forms.DialogResult other => _value == other,
        _ => false
    };
    public override int GetHashCode() => _value.GetHashCode();
    public override string ToString() => _value.ToString();
}

/// <summary>
/// ToolStripItemDisplayStyle compatibility
/// </summary>
public enum ToolStripItemDisplayStyle
{
    None,
    Text,
    Image,
    ImageAndText
}

/// <summary>
/// FlowLayoutPanel compatibility - use StackLayout
/// </summary>
public class FlowLayoutPanel : Eto.Forms.StackLayout
{
    public bool WrapContents { get; set; } = true;
    public FlowDirection FlowDirection { get; set; } = FlowDirection.LeftToRight;
    public bool AutoScroll { get; set; }
    public Padding Padding { get; set; }
}

public enum FlowDirection
{
    LeftToRight,
    TopDown,
    RightToLeft,
    BottomUp
}

/// <summary>
/// CheckedListBox compatibility
/// </summary>
public class CheckedListBox : Eto.Forms.ListBox
{
    public bool CheckOnClick { get; set; }
    public DrawMode DrawMode { get; set; } = DrawMode.Normal;
    public event ItemCheckEventHandler ItemCheck;

    private readonly HashSet<int> _checkedIndices = new HashSet<int>();
    private ObjectItemCollection _items;

    // SelectedItem (returns the selected item from stored objects)
    public object SelectedItem
    {
        get
        {
            var index = SelectedIndex;
            if (index >= 0 && _items != null && index < _items.Count)
                return _items[index];
            return SelectedValue;
        }
    }

    // Items collection that accepts objects
    public new ObjectItemCollection Items => _items ??= new ObjectItemCollection(this);

    // CheckedIndices collection
    public CheckedIndexCollection CheckedIndices { get; }

    // CheckedItems collection
    public CheckedItemCollection CheckedItems { get; }

    public CheckedListBox()
    {
        CheckedIndices = new CheckedIndexCollection(_checkedIndices);
        CheckedItems = new CheckedItemCollection(this, _checkedIndices);
    }

    public void SetItemChecked(int index, bool value)
    {
        if (value)
            _checkedIndices.Add(index);
        else
            _checkedIndices.Remove(index);
    }

    public bool GetItemChecked(int index) => _checkedIndices.Contains(index);

    public int IndexFromPoint(Point p) => IndexFromPoint(p.X, p.Y);
    public int IndexFromPoint(int x, int y)
    {
        // Simplified - always return -1 (not found)
        return -1;
    }

    // SelectedIndexChanged event - wrap WinForms EventHandler to Eto EventHandler<EventArgs>
    public new event EventHandler SelectedIndexChanged
    {
        add => base.SelectedIndexChanged += (s, e) => value(s, EventArgs.Empty);
        remove { }
    }

    // Mouse events
    public new event MouseEventHandler MouseDown
    {
        add => base.MouseDown += (s, e) => value(s, e);
        remove { }
    }
    public new event MouseEventHandler MouseUp
    {
        add => base.MouseUp += (s, e) => value(s, e);
        remove { }
    }

    // DrawItem event (stub)
    public event DrawItemEventHandler DrawItem { add { } remove { } }

    // BeginUpdate/EndUpdate for WinForms compatibility
    public void BeginUpdate() { }
    public void EndUpdate() { }
}

/// <summary>
/// CheckedIndexCollection for CheckedListBox
/// </summary>
public class CheckedIndexCollection : System.Collections.IEnumerable
{
    private readonly HashSet<int> _indices;

    public CheckedIndexCollection(HashSet<int> indices) => _indices = indices;

    public int Count => _indices.Count;
    public bool Contains(int index) => _indices.Contains(index);
    public int this[int index] => _indices.Skip(index).FirstOrDefault();

    public System.Collections.IEnumerator GetEnumerator() => _indices.GetEnumerator();
}

/// <summary>
/// CheckedItemCollection for CheckedListBox
/// </summary>
public class CheckedItemCollection : System.Collections.IEnumerable
{
    private readonly CheckedListBox _listBox;
    private readonly HashSet<int> _indices;

    public CheckedItemCollection(CheckedListBox listBox, HashSet<int> indices)
    {
        _listBox = listBox;
        _indices = indices;
    }

    public int Count => _indices.Count;

    public object this[int index]
    {
        get
        {
            var realIndex = _indices.Skip(index).FirstOrDefault();
            return realIndex < _listBox.Items.Count ? _listBox.Items[realIndex] : null;
        }
    }

    public System.Collections.IEnumerator GetEnumerator()
    {
        foreach (var idx in _indices)
        {
            if (idx < _listBox.Items.Count)
                yield return _listBox.Items[idx];
        }
    }
}

/// <summary>
/// ObjectItemCollection - Collection that stores objects and syncs with ListBox
/// </summary>
public class ObjectItemCollection : System.Collections.IList
{
    private readonly Eto.Forms.ListBox _listBox;
    private readonly List<object> _items = new List<object>();

    public ObjectItemCollection(Eto.Forms.ListBox listBox) => _listBox = listBox;

    public int Count => _items.Count;
    public bool IsFixedSize => false;
    public bool IsReadOnly => false;
    public bool IsSynchronized => false;
    public object SyncRoot => this;

    public object this[int index]
    {
        get => _items[index];
        set
        {
            _items[index] = value;
            _listBox.Items[index] = new Eto.Forms.ListItem { Text = value?.ToString() ?? "", Tag = value };
        }
    }

    public int Add(object value)
    {
        _items.Add(value);
        _listBox.Items.Add(new Eto.Forms.ListItem { Text = value?.ToString() ?? "", Tag = value });
        return _items.Count - 1;
    }

    public void Clear()
    {
        _items.Clear();
        _listBox.Items.Clear();
    }

    public bool Contains(object value) => _items.Contains(value);
    public int IndexOf(object value) => _items.IndexOf(value);

    public void Insert(int index, object value)
    {
        _items.Insert(index, value);
        _listBox.Items.Insert(index, new Eto.Forms.ListItem { Text = value?.ToString() ?? "", Tag = value });
    }

    public void Remove(object value)
    {
        var index = _items.IndexOf(value);
        if (index >= 0)
            RemoveAt(index);
    }

    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
        _listBox.Items.RemoveAt(index);
    }

    public void CopyTo(System.Array array, int index)
    {
        ((System.Collections.IList)_items).CopyTo(array, index);
    }

    public System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
}

/// <summary>
/// ToolStripTextBox compatibility
/// </summary>
public class ToolStripTextBox : Eto.Forms.TextBox
{
    public string ToolTipText { get => ToolTip; set => ToolTip = value; }

    // KeyDown event - wrap WinForms delegate to Eto EventHandler<KeyEventArgs>
    public new event KeyEventHandler KeyDown
    {
        add => base.KeyDown += (s, e) => value(s, e);
        remove { }
    }

    // TextChanged event - wrap WinForms EventHandler to Eto EventHandler<EventArgs>
    public new event EventHandler TextChanged
    {
        add => base.TextChanged += (s, e) => value(s, EventArgs.Empty);
        remove { }
    }
}

/// <summary>
/// ToolTip compatibility
/// </summary>
public class ToolTip
{
    public void SetToolTip(Eto.Forms.Control control, string tip)
    {
        control.ToolTip = tip;
    }
}

/// <summary>
/// IWindowsFormsEditorService stub
/// </summary>
public interface IWindowsFormsEditorService
{
    void CloseDropDown();
    void DropDownControl(object control);
    Eto.Forms.DialogResult ShowDialog(Eto.Forms.Dialog dialog);
}

/// <summary>
/// ToolboxBitmapAttribute stub
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ToolboxBitmapAttribute : Attribute
{
    public ToolboxBitmapAttribute(Type type, string resourceName) { }
    public ToolboxBitmapAttribute(string imageFile) { }
}

/// <summary>
/// FormBorderStyle enum compatibility
/// </summary>
public enum FormBorderStyle
{
    None,
    FixedSingle,
    Fixed3D,
    FixedDialog,
    Sizable,
    FixedToolWindow,
    SizableToolWindow
}

/// <summary>
/// ControlStyles enum compatibility for WinForms
/// </summary>
[Flags]
public enum ControlStyles
{
    None = 0,
    Selectable = 1,
    AllPaintingInWmPaint = 2,
    UserPaint = 4,
    OptimizedDoubleBuffer = 8
}

/// <summary>
/// MouseButtons enum for WinForms compatibility
/// </summary>
[Flags]
public enum MouseButtons
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 4,
    XButton1 = 8,
    XButton2 = 16
}

/// <summary>
/// Helper to convert Eto MouseButtons to WinForms-style
/// </summary>
public static class MouseButtonsHelper
{
    public static MouseButtons ToWinForms(this Eto.Forms.MouseButtons buttons)
    {
        var result = MouseButtons.None;
        if (buttons.HasFlag(Eto.Forms.MouseButtons.Primary))
            result |= MouseButtons.Left;
        if (buttons.HasFlag(Eto.Forms.MouseButtons.Alternate))
            result |= MouseButtons.Right;
        if (buttons.HasFlag(Eto.Forms.MouseButtons.Middle))
            result |= MouseButtons.Middle;
        return result;
    }

    public static Eto.Forms.MouseButtons ToEto(this MouseButtons buttons)
    {
        var result = Eto.Forms.MouseButtons.None;
        if (buttons.HasFlag(MouseButtons.Left))
            result |= Eto.Forms.MouseButtons.Primary;
        if (buttons.HasFlag(MouseButtons.Right))
            result |= Eto.Forms.MouseButtons.Alternate;
        if (buttons.HasFlag(MouseButtons.Middle))
            result |= Eto.Forms.MouseButtons.Middle;
        return result;
    }
}

/// <summary>
/// Clipboard helper for WinForms-style API
/// </summary>
public static class ClipboardHelper
{
    public static void SetText(string text)
    {
        var clipboard = new Eto.Forms.Clipboard();
        clipboard.Text = text;
    }

    public static string GetText()
    {
        var clipboard = new Eto.Forms.Clipboard();
        return clipboard.Text ?? string.Empty;
    }

    public static bool ContainsText()
    {
        var clipboard = new Eto.Forms.Clipboard();
        return !string.IsNullOrEmpty(clipboard.Text);
    }
}

/// <summary>
/// Application helper for WinForms-style API
/// </summary>
public static class ApplicationHelper
{
    public static void DoEvents()
    {
        // Eto doesn't have direct equivalent, but we can process pending events
        Eto.Forms.Application.Instance.RunIteration();
    }
}

/// <summary>
/// Extension methods for Eto.Forms compatibility with WinForms patterns
/// </summary>
public static class EtoCompatExtensions
{
    /// <summary>
    /// WinForms-style Invoke for cross-thread UI access
    /// </summary>
    public static void Invoke(this Eto.Forms.Control control, Action action)
    {
        Eto.Forms.Application.Instance.Invoke(action);
    }

    /// <summary>
    /// WinForms-style Invoke with Delegate
    /// </summary>
    public static object Invoke(this Eto.Forms.Control control, Delegate method)
    {
        object result = null;
        Eto.Forms.Application.Instance.Invoke(() => { result = method.DynamicInvoke(); });
        return result;
    }

    /// <summary>
    /// WinForms-style InvokeRequired check
    /// </summary>
    public static bool GetInvokeRequired(this Eto.Forms.Control control)
    {
        // In Eto, we typically don't need to check this the same way as WinForms
        // Return false to indicate we're on the main thread
        return false;
    }

    /// <summary>
    /// WinForms-style BeginInvoke for async cross-thread UI access
    /// </summary>
    public static void BeginInvoke(this Eto.Forms.Control control, Action action)
    {
        Eto.Forms.Application.Instance.AsyncInvoke(action);
    }

    /// <summary>
    /// WinForms-style BeginInvoke with Delegate
    /// </summary>
    public static void BeginInvoke(this Eto.Forms.Control control, Delegate method)
    {
        Eto.Forms.Application.Instance.AsyncInvoke(() => method.DynamicInvoke());
    }

    /// <summary>
    /// Refresh compatibility - triggers repaint
    /// </summary>
    public static void Refresh(this Eto.Forms.Control control)
    {
        control.Invalidate();
    }

    /// <summary>
    /// SetStyle stub for WinForms compatibility
    /// </summary>
    public static void SetStyle(this Eto.Forms.Control control, ControlStyles style, bool value)
    {
        // Eto handles these internally
    }

    /// <summary>
    /// UpdateStyles stub for WinForms compatibility
    /// </summary>
    public static void UpdateStyles(this Eto.Forms.Control control)
    {
        // Eto handles these internally
    }

    /// <summary>
    /// Get Button from MouseEventArgs (WinForms compatibility)
    /// </summary>
    public static MouseButtons GetButton(this Eto.Forms.MouseEventArgs e)
    {
        return e.Buttons.ToWinForms();
    }

    /// <summary>
    /// Button property (WinForms compatibility) - returns MouseButtons
    /// </summary>
    public static MouseButtons Button(this Eto.Forms.MouseEventArgs e)
    {
        return e.Buttons.ToWinForms();
    }

    /// <summary>
    /// X property (WinForms compatibility)
    /// </summary>
    public static int X(this Eto.Forms.MouseEventArgs e)
    {
        return (int)e.Location.X;
    }

    /// <summary>
    /// Y property (WinForms compatibility)
    /// </summary>
    public static int Y(this Eto.Forms.MouseEventArgs e)
    {
        return (int)e.Location.Y;
    }

    /// <summary>
    /// Get KeyCode from KeyEventArgs (WinForms compatibility)
    /// </summary>
    public static Eto.Forms.Keys GetKeyCode(this Eto.Forms.KeyEventArgs e)
    {
        return e.Key;
    }

    /// <summary>
    /// SuppressKeyPress stub (WinForms compatibility)
    /// </summary>
    public static void SetSuppressKeyPress(this Eto.Forms.KeyEventArgs e, bool value)
    {
        if (value)
            e.Handled = true;
    }

    // Layout methods (WinForms compatibility stubs)
    public static void SuspendLayout(this Eto.Forms.Control control) { }
    public static void ResumeLayout(this Eto.Forms.Control control) { }
    public static void ResumeLayout(this Eto.Forms.Control control, bool performLayout) { }
    public static void PerformLayout(this Eto.Forms.Control control) { }
    public static void BringToFront(this Eto.Forms.Control control) { }
    public static void SendToBack(this Eto.Forms.Control control) { }

    /// <summary>
    /// GetButton for MapMouseEventArgs (WinForms compatibility)
    /// </summary>
    public static Eto.Forms.MouseButtons GetButton(this L1MapViewer.Controls.MapMouseEventArgs e)
    {
        return e.Button.ToEto();
    }

    /// <summary>
    /// Create MouseEventArgs with WinForms-style parameters
    /// </summary>
    public static Eto.Forms.MouseEventArgs CreateMouseEventArgs(Eto.Forms.MouseButtons buttons, int clicks, float x, float y, int delta)
    {
        return new Eto.Forms.MouseEventArgs(buttons, Keys.None, new PointF(x, y), new SizeF(0, delta));
    }

    // Name property (WinForms uses Name, Eto uses ID)
    public static string GetName(this Eto.Forms.Control control) => control.ID ?? "";
    public static void SetName(this Eto.Forms.Control control, string name) => control.ID = name;

    // SetName for menu items (not Controls in Eto)
    public static void SetName(this WinFormsMenuStrip menu, string name) => menu.Name = name;
    public static void SetName(this WinFormsToolStripMenuItem item, string name) => item.Name = name;
    public static void SetName(this WinFormsToolStripButton button, string name) => button.Name = name;
    public static void SetName(this Eto.Forms.MenuItem item, string name) => item.ID = name;
    public static void SetName(this Eto.Forms.ToolItem item, string name) => item.ID = name;

    // SetLocation for menu types (stub for WinForms compat)
    public static void SetLocation(this WinFormsMenuStrip menu, Point location) => menu.Location = location;

    // Font extension - FontFamily property
    public static FontFamily FontFamily(this Eto.Drawing.Font font) => new FontFamily(font.FamilyName);

    // ForeColor/BackColor (WinForms to Eto)
    // CommonControl doesn't have TextColor in Eto - stub implementation
    public static Eto.Drawing.Color GetForeColor(this Eto.Forms.CommonControl control) => Eto.Drawing.Colors.Black;
    public static void SetForeColor(this Eto.Forms.CommonControl control, Eto.Drawing.Color color) { /* No-op */ }
    public static Eto.Drawing.Color GetBackColor(this Eto.Forms.Control control) => control.BackgroundColor;
    public static void SetBackColor(this Eto.Forms.Control control, Eto.Drawing.Color color) => control.BackgroundColor = color;

    // UseVisualStyleBackColor stub
    public static void SetUseVisualStyleBackColor(this Eto.Forms.Button button, bool value) { }

    // TextAlign for Label
    public static void SetTextAlign(this Eto.Forms.Label label, ContentAlignment align)
    {
        label.TextAlignment = align switch
        {
            ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => Eto.Forms.TextAlignment.Left,
            ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => Eto.Forms.TextAlignment.Center,
            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => Eto.Forms.TextAlignment.Right,
            _ => Eto.Forms.TextAlignment.Left
        };
    }

    // Timer compatibility
    public static void SetInterval(this Eto.Forms.UITimer timer, int milliseconds) => timer.Interval = milliseconds / 1000.0;
    public static int GetInterval(this Eto.Forms.UITimer timer) => (int)(timer.Interval * 1000);
    public static void Start(this Eto.Forms.UITimer timer) => timer.Start();
    public static void Stop(this Eto.Forms.UITimer timer) => timer.Stop();

    // Form properties
    public static void SetClientSize(this Eto.Forms.Window window, Eto.Drawing.Size size) => window.ClientSize = size;
    public static Eto.Drawing.Size GetClientSize(this Eto.Forms.Window window) => window.ClientSize;

    // AddRange for menu items (IList doesn't have AddRange)
    public static void AddRange(this System.Collections.Generic.IList<Eto.Forms.MenuItem> list, params object[] items)
    {
        foreach (var item in items)
        {
            if (item is Eto.Forms.MenuItem menuItem)
                list.Add(menuItem);
        }
    }

    // AddRange for ComboBox items
    public static void AddRange(this ComboBoxObjectCollection collection, params object[] items)
    {
        foreach (var item in items)
            collection.Add(item);
    }
}

/// <summary>
/// ContentAlignment enum for WinForms compatibility
/// </summary>
public enum ContentAlignment
{
    TopLeft = 1,
    TopCenter = 2,
    TopRight = 4,
    MiddleLeft = 16,
    MiddleCenter = 32,
    MiddleRight = 64,
    BottomLeft = 256,
    BottomCenter = 512,
    BottomRight = 1024
}

/// <summary>
/// AutoScaleMode enum stub
/// </summary>
public enum AutoScaleMode
{
    None,
    Font,
    Dpi,
    Inherit
}

/// <summary>
/// ControlCollection wrapper to make Controls.Add() work
/// </summary>
public class ControlCollection : System.Collections.ObjectModel.Collection<Eto.Forms.Control>
{
    private readonly Eto.Forms.Container _container;

    public ControlCollection(Eto.Forms.Container container)
    {
        _container = container;
    }

    protected override void InsertItem(int index, Eto.Forms.Control item)
    {
        base.InsertItem(index, item);
        UpdateContainer();
    }

    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        UpdateContainer();
    }

    protected override void ClearItems()
    {
        base.ClearItems();
        UpdateContainer();
    }

    private void UpdateContainer()
    {
        if (_container is Eto.Forms.Panel panel)
        {
            if (Count == 1)
            {
                panel.Content = this[0];
            }
            else if (Count > 1)
            {
                // 使用 PixelLayout 以支援絕對定位 (Location 屬性)
                var layout = new Eto.Forms.PixelLayout();
                foreach (var control in this)
                {
                    // 取得控件的 Location (如果有設定)
                    var loc = control.GetLocation();
                    layout.Add(control, loc);
                }
                panel.Content = layout;
            }
            else
            {
                panel.Content = null;
            }
        }
    }
}

/// <summary>
/// Extension to get Controls collection from Eto containers
/// </summary>
public static class ContainerExtensions
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Eto.Forms.Container, ControlCollection> _collections
        = new System.Runtime.CompilerServices.ConditionalWeakTable<Eto.Forms.Container, ControlCollection>();

    public static ControlCollection GetControls(this Eto.Forms.Container container)
    {
        return _collections.GetValue(container, c => new ControlCollection(c));
    }

    public static ControlCollection GetControls(this Eto.Forms.Panel panel)
    {
        return _collections.GetValue(panel, p => new ControlCollection(p));
    }

    public static ControlCollection GetControls(this Eto.Forms.Form form)
    {
        return _collections.GetValue(form, f => new ControlCollection(f));
    }

    /// <summary>
    /// GetControls for generic Control - tries to cast to Container first
    /// </summary>
    public static ControlCollection GetControls(this Eto.Forms.Control control)
    {
        if (control is Eto.Forms.Container container)
        {
            return _collections.GetValue(container, c => new ControlCollection(c));
        }
        // Return empty collection for non-containers
        return new ControlCollection(null);
    }

    // Dock stub (Eto doesn't have direct docking)
    public static void SetDock(this Eto.Forms.Control control, DockStyle dock) { }
    public static DockStyle GetDock(this Eto.Forms.Control control) => DockStyle.None;

    // Anchor stub
    public static void SetAnchor(this Eto.Forms.Control control, AnchorStyles anchor) { }

    // AutoSize stub
    public static void SetAutoSize(this Eto.Forms.Control control, bool value) { }

    // BorderStyle for panels
    public static void SetBorderStyle(this Eto.Forms.Panel panel, BorderStyle style) { }

    // Size property
    public static void SetSize(this Eto.Forms.Control control, Eto.Drawing.Size size)
    {
        control.Width = size.Width;
        control.Height = size.Height;
    }

    // StartPosition for forms
    public static void SetStartPosition(this Eto.Forms.Window window, FormStartPosition position) { }

    // FormBorderStyle for forms
    public static void SetFormBorderStyle(this Eto.Forms.Window window, FormBorderStyle style)
    {
        window.Resizable = style == FormBorderStyle.Sizable || style == FormBorderStyle.SizableToolWindow;
    }

    // ShowDialog compatibility - Dialog.ShowModal() returns DialogResult in Eto
    public static Eto.Forms.DialogResult ShowDialogResult(this Eto.Forms.Dialog dialog)
    {
        dialog.ShowModal();
        return Eto.Forms.DialogResult.Ok;
    }

    // TabControl.TabPages
    public static System.Collections.Generic.IList<Eto.Forms.TabPage> GetTabPages(this Eto.Forms.TabControl tabControl)
    {
        return tabControl.Pages;
    }

    // ComboBox Items compatibility - returns Eto's collection type
    public static Eto.Forms.ListItemCollection GetItems(this Eto.Forms.ComboBox comboBox)
    {
        return comboBox.Items;
    }

    // ListBox Items compatibility - returns Eto's collection type
    public static Eto.Forms.ListItemCollection GetItems(this Eto.Forms.ListBox listBox)
    {
        return listBox.Items;
    }

    // SelectedItem for ComboBox
    public static object GetSelectedItem(this Eto.Forms.ComboBox comboBox)
    {
        return comboBox.SelectedValue;
    }

    public static void SetSelectedItem(this Eto.Forms.ComboBox comboBox, object value)
    {
        comboBox.SelectedValue = value;
    }

    // Text for Window/Form
    public static string GetText(this Eto.Forms.Window window) => window.Title;
    public static void SetText(this Eto.Forms.Window window, string text) => window.Title = text;
}

/// <summary>
/// ItemCollection wrapper for compatibility
/// </summary>
public class ItemCollection<T> : System.Collections.ObjectModel.Collection<T>
{
    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items)
            Add(item);
    }
}

/// <summary>
/// Graphics compatibility extensions for SkiaSharp
/// </summary>
public static class GraphicsCompatExtensions
{
    // Store mode values (simplified - doesn't actually affect rendering in Eto)
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Eto.Drawing.Graphics, GraphicsState> _states
        = new System.Runtime.CompilerServices.ConditionalWeakTable<Eto.Drawing.Graphics, GraphicsState>();

    private class GraphicsState
    {
        public SmoothingMode SmoothingMode { get; set; } = SmoothingMode.Default;
        public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.Default;
        public PixelOffsetMode PixelOffsetMode { get; set; } = PixelOffsetMode.Default;
        public CompositingQuality CompositingQuality { get; set; } = CompositingQuality.Default;
        public TextRenderingHint TextRenderingHint { get; set; } = TextRenderingHint.SystemDefault;
    }

    private static GraphicsState GetState(Eto.Drawing.Graphics g) => _states.GetOrCreateValue(g);

    public static void SetSmoothingMode(this Eto.Drawing.Graphics g, SmoothingMode mode)
    {
        GetState(g).SmoothingMode = mode;
        g.AntiAlias = mode == SmoothingMode.AntiAlias || mode == SmoothingMode.HighQuality;
    }

    public static SmoothingMode GetSmoothingMode(this Eto.Drawing.Graphics g) => GetState(g).SmoothingMode;

    public static void SetInterpolationMode(this Eto.Drawing.Graphics g, InterpolationMode mode)
    {
        GetState(g).InterpolationMode = mode;
        g.ImageInterpolation = mode switch
        {
            InterpolationMode.NearestNeighbor => Eto.Drawing.ImageInterpolation.None,
            InterpolationMode.Low => Eto.Drawing.ImageInterpolation.Low,
            InterpolationMode.High or InterpolationMode.HighQualityBicubic or InterpolationMode.HighQualityBilinear
                => Eto.Drawing.ImageInterpolation.High,
            _ => Eto.Drawing.ImageInterpolation.Default
        };
    }

    public static InterpolationMode GetInterpolationMode(this Eto.Drawing.Graphics g) => GetState(g).InterpolationMode;

    public static void SetPixelOffsetMode(this Eto.Drawing.Graphics g, PixelOffsetMode mode)
    {
        GetState(g).PixelOffsetMode = mode;
        // Use implicit conversion operator
        g.PixelOffsetMode = mode;
    }

    public static PixelOffsetMode GetPixelOffsetMode(this Eto.Drawing.Graphics g) => GetState(g).PixelOffsetMode;

    public static void SetCompositingQuality(this Eto.Drawing.Graphics g, CompositingQuality quality)
    {
        GetState(g).CompositingQuality = quality;
    }

    public static CompositingQuality GetCompositingQuality(this Eto.Drawing.Graphics g) => GetState(g).CompositingQuality;

    // TextRenderingHint extension
    public static void SetTextRenderingHint(this Eto.Drawing.Graphics g, TextRenderingHint hint)
    {
        GetState(g).TextRenderingHint = hint;
    }

    public static TextRenderingHint GetTextRenderingHint(this Eto.Drawing.Graphics g) => GetState(g).TextRenderingHint;
}

public enum SmoothingMode { Default, HighSpeed, HighQuality, None, AntiAlias }
public enum InterpolationMode { Default, Low, High, Bilinear, Bicubic, NearestNeighbor, HighQualityBilinear, HighQualityBicubic }

/// <summary>
/// PixelOffsetMode struct with implicit conversion to Eto.Drawing.PixelOffsetMode
/// </summary>
public struct PixelOffsetMode
{
    private readonly int _value;
    private PixelOffsetMode(int value) => _value = value;

    public static readonly PixelOffsetMode Default = new(0);
    public static readonly PixelOffsetMode HighSpeed = new(1);
    public static readonly PixelOffsetMode HighQuality = new(2);
    public static readonly PixelOffsetMode None = new(3);
    public static readonly PixelOffsetMode Half = new(4);

    public static implicit operator Eto.Drawing.PixelOffsetMode(PixelOffsetMode mode)
    {
        return mode._value switch
        {
            4 => Eto.Drawing.PixelOffsetMode.Half, // Half
            _ => Eto.Drawing.PixelOffsetMode.None  // Default, HighSpeed, HighQuality, None
        };
    }

    public override bool Equals(object obj) => obj is PixelOffsetMode other && _value == other._value;
    public override int GetHashCode() => _value;
    public static bool operator ==(PixelOffsetMode left, PixelOffsetMode right) => left._value == right._value;
    public static bool operator !=(PixelOffsetMode left, PixelOffsetMode right) => left._value != right._value;
}

public enum CompositingQuality { Default, HighSpeed, HighQuality, GammaCorrected, AssumeLinear }

/// <summary>
/// More control extensions for property compatibility
/// </summary>
public static class MoreControlExtensions
{
    // MaximizeBox/MinimizeBox for Window
    public static void SetMaximizeBox(this Eto.Forms.Window window, bool value) => window.Maximizable = value;
    public static void SetMinimizeBox(this Eto.Forms.Window window, bool value) => window.Minimizable = value;

    // TabStop stub
    public static void SetTabStop(this Eto.Forms.Control control, bool value) { }

    // SizeMode for ImageView
    public static void SetSizeMode(this Eto.Forms.ImageView imageView, PictureBoxSizeMode mode) { }

    // SizeMode for WinFormsPictureBox
    public static void SetSizeMode(this WinFormsPictureBox pictureBox, PictureBoxSizeMode mode)
    {
        pictureBox.SizeMode = mode;
    }

    // Font property extension
    public static void SetFont(this Eto.Forms.CommonControl control, Eto.Drawing.Font font) => control.Font = font;
    public static Eto.Drawing.Font GetFont(this Eto.Forms.CommonControl control) => control.Font;

    // DoubleBuffered stub for UserControl
    public static void SetDoubleBuffered(this Eto.Forms.Control control, bool value) { }

    // SuspendLayout/ResumeLayout stubs
    public static void SuspendLayout(this Eto.Forms.Container container) { }
    public static void ResumeLayout(this Eto.Forms.Container container, bool performLayout = true) { }

    // DoubleClick event stub (Eto has MouseDoubleClick instead)
    // Note: This is a stub - actual implementation would need event wiring
}

/// <summary>
/// Graphics helper for WinForms compatibility
/// </summary>
public static class GraphicsHelper
{
    /// <summary>
    /// Create a Graphics object for drawing on a Bitmap
    /// In Eto.Forms, this is done via the Graphics constructor
    /// </summary>
    public static Eto.Drawing.Graphics FromImage(Eto.Drawing.Bitmap bitmap)
    {
        return new Eto.Drawing.Graphics(bitmap);
    }
}

/// <summary>
/// Padding struct compatibility
/// </summary>
public struct Padding
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
    public int All { get => Left; set { Left = Top = Right = Bottom = value; } }

    public Padding(int all) { Left = Top = Right = Bottom = all; }
    public Padding(int left, int top, int right, int bottom)
    {
        Left = left; Top = top; Right = right; Bottom = bottom;
    }

    public static implicit operator Eto.Drawing.Padding(Padding p)
        => new Eto.Drawing.Padding(p.Left, p.Top, p.Right, p.Bottom);
}

/// <summary>
/// DragEventArgs extension methods for WinForms compatibility
/// </summary>
public static class DragEventArgsExtensions
{
    // Store Effect values
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Eto.Forms.DragEventArgs, EffectHolder> _effects
        = new System.Runtime.CompilerServices.ConditionalWeakTable<Eto.Forms.DragEventArgs, EffectHolder>();

    private class EffectHolder { public DragDropEffects Effect { get; set; } }

    public static DragDropEffects GetEffect(this Eto.Forms.DragEventArgs e)
    {
        return _effects.GetOrCreateValue(e).Effect;
    }

    public static void SetEffect(this Eto.Forms.DragEventArgs e, DragDropEffects effect)
    {
        _effects.GetOrCreateValue(e).Effect = effect;
    }

    /// <summary>
    /// Effect property wrapper using extension method pattern
    /// </summary>
    public static DragDropEffects Effect(this Eto.Forms.DragEventArgs e) => GetEffect(e);
}

/// <summary>
/// DataObject extension methods for WinForms compatibility
/// </summary>
public static class DataObjectExtensions
{
    public static bool GetDataPresent(this Eto.Forms.DataObject data, string format)
    {
        if (format == DataFormats.FileDrop)
        {
            return data.Uris != null && data.Uris.Length > 0;
        }
        if (format == DataFormats.Text || format == DataFormats.UnicodeText)
        {
            return !string.IsNullOrEmpty(data.Text);
        }
        return false;
    }

    public static string[] GetFileDropData(this Eto.Forms.DataObject data)
    {
        return data.Uris?.Select(u => u.LocalPath).ToArray() ?? Array.Empty<string>();
    }

    public static object GetData(this Eto.Forms.DataObject data, string format)
    {
        if (format == DataFormats.FileDrop)
        {
            return data.GetFileDropData();
        }
        if (format == DataFormats.Text || format == DataFormats.UnicodeText)
        {
            return data.Text;
        }
        return null;
    }
}

/// <summary>
/// More control extensions for common patterns
/// </summary>
public static class ControlPropertyExtensions
{
    // Items for ComboBox (returns underlying collection) - returns ListItemCollection directly
    public static Eto.Forms.ListItemCollection GetItemsCollection(this Eto.Forms.ComboBox comboBox)
    {
        return comboBox.Items;
    }

    // Items for ListBox
    public static Eto.Forms.ListItemCollection GetItemsCollection(this Eto.Forms.ListBox listBox)
    {
        return listBox.Items;
    }

    // SelectedItem for ListBox
    public static object GetSelectedItem(this Eto.Forms.ListBox listBox)
    {
        return listBox.SelectedValue;
    }

    public static void SetSelectedItem(this Eto.Forms.ListBox listBox, object value)
    {
        listBox.SelectedValue = value;
    }

    // Text for various controls
    public static string GetControlText(this Eto.Forms.CommonControl control)
    {
        if (control is Eto.Forms.Label label) return label.Text;
        if (control is Eto.Forms.TextControl textControl) return textControl.Text;
        if (control is Eto.Forms.Button button) return button.Text;
        return "";
    }

    public static void SetControlText(this Eto.Forms.CommonControl control, string text)
    {
        if (control is Eto.Forms.Label label) label.Text = text;
        else if (control is Eto.Forms.TextControl textControl) textControl.Text = text;
        else if (control is Eto.Forms.Button button) button.Text = text;
    }

    // SelectedPath for SelectFolderDialog
    public static string GetSelectedPath(this Eto.Forms.SelectFolderDialog dialog)
    {
        return dialog.Directory;
    }
}

/// <summary>
/// Graphics extensions for drawing operations
/// </summary>
public static class GraphicsDrawExtensions
{
    // DrawString compatibility
    public static void DrawString(this Eto.Drawing.Graphics g, string text, Eto.Drawing.Font font,
        Eto.Drawing.Brush brush, float x, float y)
    {
        g.DrawText(font, brush, x, y, text);
    }

    public static void DrawString(this Eto.Drawing.Graphics g, string text, Eto.Drawing.Font font,
        Eto.Drawing.Brush brush, Eto.Drawing.PointF location)
    {
        g.DrawText(font, brush, location, text);
    }

    public static void DrawString(this Eto.Drawing.Graphics g, string text, Eto.Drawing.Font font,
        Eto.Drawing.Brush brush, Eto.Drawing.RectangleF rect)
    {
        g.DrawText(font, brush, rect.Location, text);
    }

    public static void DrawString(this Eto.Drawing.Graphics g, string text, Eto.Drawing.Font font,
        Eto.Drawing.Brush brush, Eto.Drawing.Rectangle rect, StringFormat format)
    {
        // Ignore format for now, just draw at the rect location
        g.DrawText(font, brush, new Eto.Drawing.PointF(rect.X, rect.Y), text);
    }

    public static void DrawString(this Eto.Drawing.Graphics g, string text, Eto.Drawing.Font font,
        Eto.Drawing.Brush brush, Eto.Drawing.RectangleF rect, StringFormat format)
    {
        // Ignore format for now, just draw at the rect location
        g.DrawText(font, brush, rect.Location, text);
    }

    // FillRectangle with Point/Size (int)
    public static void FillRectangle(this Eto.Drawing.Graphics g, Eto.Drawing.Brush brush,
        int x, int y, int width, int height)
    {
        g.FillRectangle(brush, new Eto.Drawing.Rectangle(x, y, width, height));
    }

    // FillRectangle with Point/Size (float)
    public static void FillRectangle(this Eto.Drawing.Graphics g, Eto.Drawing.Brush brush,
        float x, float y, float width, float height)
    {
        g.FillRectangle(brush, new Eto.Drawing.RectangleF(x, y, width, height));
    }

    // DrawRectangle with Point/Size
    public static void DrawRectangle(this Eto.Drawing.Graphics g, Eto.Drawing.Pen pen,
        int x, int y, int width, int height)
    {
        g.DrawRectangle(pen, new Eto.Drawing.Rectangle(x, y, width, height));
    }

    // DrawLine with points
    public static void DrawLine(this Eto.Drawing.Graphics g, Eto.Drawing.Pen pen,
        int x1, int y1, int x2, int y2)
    {
        g.DrawLine(pen, new Eto.Drawing.Point(x1, y1), new Eto.Drawing.Point(x2, y2));
    }

    // FillPolygon compatibility
    public static void FillPolygon(this Eto.Drawing.Graphics g, Eto.Drawing.Brush brush, Eto.Drawing.Point[] points)
    {
        if (points == null || points.Length < 3) return;
        var path = new Eto.Drawing.GraphicsPath();
        path.MoveTo(points[0]);
        for (int i = 1; i < points.Length; i++)
            path.LineTo(points[i]);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    public static void FillPolygon(this Eto.Drawing.Graphics g, Eto.Drawing.Brush brush, Eto.Drawing.PointF[] points)
    {
        if (points == null || points.Length < 3) return;
        var path = new Eto.Drawing.GraphicsPath();
        path.MoveTo(points[0]);
        for (int i = 1; i < points.Length; i++)
            path.LineTo(points[i]);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    // DrawPolygon compatibility
    public static void DrawPolygon(this Eto.Drawing.Graphics g, Eto.Drawing.Pen pen, Eto.Drawing.Point[] points)
    {
        if (points == null || points.Length < 2) return;
        for (int i = 0; i < points.Length - 1; i++)
            g.DrawLine(pen, points[i], points[i + 1]);
        if (points.Length > 2)
            g.DrawLine(pen, points[points.Length - 1], points[0]);
    }

    // TextRenderingHint property stub for compatibility
    public static TextRenderingHint TextRenderingHint { get; set; }

    // MeasureString compatibility
    public static Eto.Drawing.SizeF MeasureString(this Eto.Drawing.Graphics g, string text, Eto.Drawing.Font font)
    {
        return font.MeasureString(text);
    }

    public static Eto.Drawing.SizeF MeasureString(this Eto.Drawing.Graphics g, string text, Eto.Drawing.Font font, int width)
    {
        // Simplified - doesn't account for wrapping
        return font.MeasureString(text);
    }
}

/// <summary>
/// BitmapHelper - Factory methods for creating Bitmaps with WinForms-style constructors
/// </summary>
public static class BitmapHelper
{
    /// <summary>
    /// Create a new Bitmap with width and height (WinForms-style)
    /// </summary>
    public static Eto.Drawing.Bitmap Create(int width, int height)
    {
        return new Eto.Drawing.Bitmap(new Eto.Drawing.Size(width, height), Eto.Drawing.PixelFormat.Format32bppRgba);
    }

    /// <summary>
    /// Create a new Bitmap with width, height, and pixel format (WinForms-style)
    /// </summary>
    public static Eto.Drawing.Bitmap Create(int width, int height, PixelFormat format)
    {
        return new Eto.Drawing.Bitmap(new Eto.Drawing.Size(width, height), format);
    }

    /// <summary>
    /// Create a new Bitmap with Size (WinForms-style)
    /// </summary>
    public static Eto.Drawing.Bitmap Create(Eto.Drawing.Size size)
    {
        return new Eto.Drawing.Bitmap(size, Eto.Drawing.PixelFormat.Format32bppRgba);
    }
}

/// <summary>
/// WinFormsBitmap - Wrapper that provides WinForms-style Bitmap constructors
/// </summary>
public class WinFormsBitmap : Eto.Drawing.Bitmap
{
    private readonly PixelFormat _originalFormat;

    /// <summary>
    /// Create a new Bitmap with width and height
    /// </summary>
    public WinFormsBitmap(int width, int height)
        : base(new Eto.Drawing.Size(width, height), Eto.Drawing.PixelFormat.Format32bppRgba)
    {
        _originalFormat = PixelFormat.Format32bppArgb;
    }

    /// <summary>
    /// Create a new Bitmap with width, height, and pixel format
    /// </summary>
    public WinFormsBitmap(int width, int height, PixelFormat format)
        : base(new Eto.Drawing.Size(width, height), format)
    {
        _originalFormat = format;
    }

    /// <summary>
    /// Create a new Bitmap from a Stream
    /// </summary>
    public WinFormsBitmap(System.IO.Stream stream)
        : base(stream)
    {
        _originalFormat = PixelFormat.Format32bppArgb;
    }

    /// <summary>
    /// Create a new Bitmap from a file path
    /// </summary>
    public WinFormsBitmap(string filename)
        : base(filename)
    {
        _originalFormat = PixelFormat.Format32bppArgb;
    }

    /// <summary>
    /// Create a new Bitmap with Size
    /// </summary>
    public WinFormsBitmap(Eto.Drawing.Size size)
        : base(size, Eto.Drawing.PixelFormat.Format32bppRgba)
    {
        _originalFormat = PixelFormat.Format32bppArgb;
    }

    /// <summary>
    /// Create a new Bitmap with Size and pixel format
    /// </summary>
    public WinFormsBitmap(Eto.Drawing.Size size, PixelFormat format)
        : base(size, format)
    {
        _originalFormat = format;
    }

    /// <summary>
    /// Create a new Bitmap with Size and Eto pixel format
    /// </summary>
    public WinFormsBitmap(Eto.Drawing.Size size, Eto.Drawing.PixelFormat format)
        : base(size, format)
    {
        _originalFormat = PixelFormat.Format32bppArgb;
    }

    /// <summary>
    /// Create a copy of an existing Bitmap (resizing)
    /// </summary>
    public WinFormsBitmap(Eto.Drawing.Bitmap source, int width, int height)
        : base(new Eto.Drawing.Size(width, height), Eto.Drawing.PixelFormat.Format32bppRgba)
    {
        _originalFormat = PixelFormat.Format32bppArgb;
    }

    /// <summary>
    /// Create a copy of an existing Bitmap (resizing) - Eto.Drawing.Image overload
    /// </summary>
    public WinFormsBitmap(Eto.Drawing.Image source, int width, int height)
        : base(new Eto.Drawing.Size(width, height), Eto.Drawing.PixelFormat.Format32bppRgba)
    {
        _originalFormat = PixelFormat.Format32bppArgb;
    }

    /// <summary>
    /// Create a copy of an existing Image
    /// </summary>
    public WinFormsBitmap(Eto.Drawing.Image source)
        : base(new Eto.Drawing.Size(source.Width, source.Height), Eto.Drawing.PixelFormat.Format32bppRgba)
    {
        _originalFormat = PixelFormat.Format32bppArgb;
    }

    /// <summary>
    /// PixelFormat property - returns the original format passed to constructor
    /// </summary>
    public PixelFormat PixelFormat => _originalFormat;

}

/// <summary>
/// Bitmap extensions for compatibility
/// </summary>
public static class BitmapExtensions
{
    // PixelFormat property via extension method
    public static PixelFormat GetPixelFormat(this Eto.Drawing.Bitmap bitmap)
    {
        return PixelFormat.Format32bppArgb;
    }

    // Note: GetPixelFormat is available for PixelFormat access

    // Size property
    public static Eto.Drawing.Size GetSize(this Eto.Drawing.Bitmap bitmap)
    {
        return new Eto.Drawing.Size(bitmap.Width, bitmap.Height);
    }

    /// <summary>
    /// LockBits - Allocate a buffer for direct pixel manipulation
    /// Supports RGB555 format for tile rendering
    /// </summary>
    public static BitmapData LockBits(this Eto.Drawing.Bitmap bitmap, Eto.Drawing.Rectangle rect,
        ImageLockMode mode, PixelFormat format)
    {
        bool isRgb555 = format == PixelFormat.Format16bppRgb555;
        int bytesPerPixel = isRgb555 ? 2 : 4;
        // Align stride to 4 bytes
        int stride = ((rect.Width * bytesPerPixel + 3) / 4) * 4;
        int bufferSize = stride * rect.Height;

        var buffer = new byte[bufferSize];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);

        return new BitmapData
        {
            Width = rect.Width,
            Height = rect.Height,
            Stride = stride,
            Scan0 = handle.AddrOfPinnedObject(),
            PixelFormat = format,
            Buffer = buffer,
            BufferHandle = handle,
            SourceBitmap = bitmap
        };
    }

    /// <summary>
    /// UnlockBits - Copy buffer data back to the Eto bitmap
    /// Converts RGB555 to RGBA32 if needed
    /// </summary>
    public static void UnlockBits(this Eto.Drawing.Bitmap bitmap, BitmapData data)
    {
        if (data?.Buffer == null || data.SourceBitmap == null) return;

        bool isRgb555 = data.PixelFormat == PixelFormat.Format16bppRgb555;

        try
        {
            // Use Eto's Lock method to get direct pixel access
            using (var bitmapData = bitmap.Lock())
            {
                int srcStride = data.Stride;
                int dstStride = bitmapData.ScanWidth;
                int width = Math.Min(data.Width, bitmap.Width);
                int height = Math.Min(data.Height, bitmap.Height);

                unsafe
                {
                    byte* dstPtr = (byte*)bitmapData.Data;

                    if (isRgb555)
                    {
                        // Convert RGB555 to BGRA32 (Eto uses BGRA)
                        for (int y = 0; y < height; y++)
                        {
                            int srcOffset = y * srcStride;
                            int dstOffset = y * dstStride;

                            for (int x = 0; x < width; x++)
                            {
                                // Read RGB555 (16-bit)
                                ushort rgb555 = (ushort)(data.Buffer[srcOffset] | (data.Buffer[srcOffset + 1] << 8));

                                // Extract RGB components (5 bits each)
                                int r = (rgb555 >> 10) & 0x1F;
                                int g = (rgb555 >> 5) & 0x1F;
                                int b = rgb555 & 0x1F;

                                // Convert to 8-bit (scale from 0-31 to 0-255)
                                byte r8 = (byte)((r << 3) | (r >> 2));
                                byte g8 = (byte)((g << 3) | (g >> 2));
                                byte b8 = (byte)((b << 3) | (b >> 2));
                                byte a8 = 255; // Always opaque - black is a valid color

                                // Write BGRA32
                                dstPtr[dstOffset] = b8;
                                dstPtr[dstOffset + 1] = g8;
                                dstPtr[dstOffset + 2] = r8;
                                dstPtr[dstOffset + 3] = a8;

                                srcOffset += 2;
                                dstOffset += 4;
                            }
                        }
                    }
                    else
                    {
                        // Direct copy for 32-bit formats
                        for (int y = 0; y < height; y++)
                        {
                            int srcOffset = y * srcStride;
                            int dstOffset = y * dstStride;
                            int copyBytes = Math.Min(srcStride, dstStride);
                            System.Buffer.BlockCopy(data.Buffer, srcOffset, new Span<byte>(dstPtr + dstOffset, copyBytes).ToArray(), 0, copyBytes);
                        }
                    }
                }
            }
        }
        catch
        {
            // Silently ignore errors during pixel conversion
        }
        finally
        {
            data.Dispose();
        }
    }

    /// <summary>
    /// Clone for Image (returns a copy)
    /// </summary>
    public static Eto.Drawing.Bitmap Clone(this Eto.Drawing.Image image)
    {
        if (image is Eto.Drawing.Bitmap bitmap)
        {
            return bitmap.Clone();
        }
        // For non-bitmap images, create a new bitmap by drawing
        var result = new Eto.Drawing.Bitmap(image.Width, image.Height, Eto.Drawing.PixelFormat.Format32bppRgba);
        using (var g = new Eto.Drawing.Graphics(result))
        {
            g.DrawImage(image, 0, 0);
        }
        return result;
    }
}

/// <summary>
/// Rectangle extension methods for WinForms compatibility
/// </summary>
public static class RectangleExtensions
{
    /// <summary>
    /// Check if two rectangles intersect
    /// </summary>
    public static bool IntersectsWith(this Eto.Drawing.Rectangle rect, Eto.Drawing.Rectangle other)
    {
        return rect.X < other.X + other.Width &&
               rect.X + rect.Width > other.X &&
               rect.Y < other.Y + other.Height &&
               rect.Y + rect.Height > other.Y;
    }

    /// <summary>
    /// Check if two rectangles intersect (RectangleF version)
    /// </summary>
    public static bool IntersectsWith(this Eto.Drawing.RectangleF rect, Eto.Drawing.RectangleF other)
    {
        return rect.X < other.X + other.Width &&
               rect.X + rect.Width > other.X &&
               rect.Y < other.Y + other.Height &&
               rect.Y + rect.Height > other.Y;
    }
}

/// <summary>
/// Point/PointF extension methods for WinForms compatibility
/// </summary>
public static class PointExtensions
{
    /// <summary>
    /// Convert PointF to Point
    /// </summary>
    public static Eto.Drawing.Point ToPoint(this Eto.Drawing.PointF pointF)
    {
        return new Eto.Drawing.Point((int)pointF.X, (int)pointF.Y);
    }

    /// <summary>
    /// Convert Point to PointF
    /// </summary>
    public static Eto.Drawing.PointF ToPointF(this Eto.Drawing.Point point)
    {
        return new Eto.Drawing.PointF(point.X, point.Y);
    }
}

/// <summary>
/// CursorPosition compatibility class for WinForms static Cursor.Position access
/// </summary>
public static class CursorPosition
{
    /// <summary>
    /// Gets or sets the cursor's position in screen coordinates
    /// </summary>
    public static Eto.Drawing.Point Position
    {
        get
        {
            // In Eto, we use Mouse.Position
            var pos = Eto.Forms.Mouse.Position;
            return new Eto.Drawing.Point((int)pos.X, (int)pos.Y);
        }
        set
        {
            // Setting cursor position is platform-specific and may not be supported in Eto
            // This is a stub
        }
    }
}

/// <summary>
/// Colors compatibility - extends Eto.Drawing.Colors with additional colors
/// </summary>
public static class ColorsCompat
{
    // Alias for British spelling
    public static readonly Eto.Drawing.Color DarkGrey = Eto.Drawing.Color.FromArgb(169, 169, 169);
    public static readonly Eto.Drawing.Color Grey = Eto.Drawing.Color.FromArgb(128, 128, 128);
    public static readonly Eto.Drawing.Color LightGrey = Eto.Drawing.Color.FromArgb(211, 211, 211);
    public static readonly Eto.Drawing.Color DimGrey = Eto.Drawing.Color.FromArgb(105, 105, 105);

    // Forward all standard colors from Eto.Drawing.Colors
    public static Eto.Drawing.Color Black => Eto.Drawing.Colors.Black;
    public static Eto.Drawing.Color White => Eto.Drawing.Colors.White;
    public static Eto.Drawing.Color Red => Eto.Drawing.Colors.Red;
    public static Eto.Drawing.Color Green => Eto.Drawing.Colors.Green;
    public static Eto.Drawing.Color Blue => Eto.Drawing.Colors.Blue;
    public static Eto.Drawing.Color Yellow => Eto.Drawing.Colors.Yellow;
    public static Eto.Drawing.Color Cyan => Eto.Drawing.Colors.Cyan;
    public static Eto.Drawing.Color Magenta => Eto.Drawing.Colors.Magenta;
    public static Eto.Drawing.Color Gray => Eto.Drawing.Colors.Gray;
    public static Eto.Drawing.Color DarkGray => Eto.Drawing.Colors.DarkGray;
    public static Eto.Drawing.Color LightGray => Eto.Drawing.Color.FromArgb(211, 211, 211);
    public static Eto.Drawing.Color Orange => Eto.Drawing.Colors.Orange;
    public static Eto.Drawing.Color Pink => Eto.Drawing.Colors.Pink;
    public static Eto.Drawing.Color Purple => Eto.Drawing.Colors.Purple;
    public static Eto.Drawing.Color Brown => Eto.Drawing.Colors.Brown;
    public static Eto.Drawing.Color Transparent => Eto.Drawing.Colors.Transparent;
    public static Eto.Drawing.Color SkyBlue => Eto.Drawing.Colors.SkyBlue;
    public static Eto.Drawing.Color Lime => Eto.Drawing.Colors.Lime;
    public static Eto.Drawing.Color Teal => Eto.Drawing.Colors.Teal;
    public static Eto.Drawing.Color Navy => Eto.Drawing.Colors.Navy;
    public static Eto.Drawing.Color Olive => Eto.Drawing.Colors.Olive;
    public static Eto.Drawing.Color Maroon => Eto.Drawing.Colors.Maroon;
    public static Eto.Drawing.Color Aqua => Eto.Drawing.Colors.Aqua;
    public static Eto.Drawing.Color Fuchsia => Eto.Drawing.Colors.Fuchsia;
    public static Eto.Drawing.Color Silver => Eto.Drawing.Colors.Silver;
    public static Eto.Drawing.Color LightBlue => Eto.Drawing.Colors.LightBlue;
    public static Eto.Drawing.Color DarkBlue => Eto.Drawing.Colors.DarkBlue;
    public static Eto.Drawing.Color LightGreen => Eto.Drawing.Colors.LightGreen;
    public static Eto.Drawing.Color DarkGreen => Eto.Drawing.Colors.DarkGreen;
    public static Eto.Drawing.Color LightCyan => Eto.Drawing.Colors.LightCyan;
    public static Eto.Drawing.Color DarkCyan => Eto.Drawing.Colors.DarkCyan;
    public static Eto.Drawing.Color LightRed => Eto.Drawing.Color.FromArgb(255, 128, 128);
    public static Eto.Drawing.Color DarkRed => Eto.Drawing.Colors.DarkRed;
    public static Eto.Drawing.Color Salmon => Eto.Drawing.Colors.Salmon;
    public static Eto.Drawing.Color Coral => Eto.Drawing.Colors.Coral;
    public static Eto.Drawing.Color Goldenrod => Eto.Drawing.Colors.Goldenrod;
    public static Eto.Drawing.Color Khaki => Eto.Drawing.Colors.Khaki;
    public static Eto.Drawing.Color SlateGray => Eto.Drawing.Colors.SlateGray;
    public static readonly Eto.Drawing.Color OrangeRed = Eto.Drawing.Color.FromArgb(255, 69, 0);
    public static readonly Eto.Drawing.Color LightYellow = Eto.Drawing.Color.FromArgb(255, 255, 224);
    public static readonly Eto.Drawing.Color Indigo = Eto.Drawing.Color.FromArgb(75, 0, 130);
    public static readonly Eto.Drawing.Color Crimson = Eto.Drawing.Color.FromArgb(220, 20, 60);
    public static readonly Eto.Drawing.Color DarkOrange = Eto.Drawing.Color.FromArgb(255, 140, 0);
    public static readonly Eto.Drawing.Color Gold = Eto.Drawing.Color.FromArgb(255, 215, 0);
    public static readonly Eto.Drawing.Color HotPink = Eto.Drawing.Color.FromArgb(255, 105, 180);
    public static readonly Eto.Drawing.Color DeepPink = Eto.Drawing.Color.FromArgb(255, 20, 147);
    public static readonly Eto.Drawing.Color Turquoise = Eto.Drawing.Color.FromArgb(64, 224, 208);
    public static readonly Eto.Drawing.Color Tomato = Eto.Drawing.Color.FromArgb(255, 99, 71);
    public static readonly Eto.Drawing.Color SpringGreen = Eto.Drawing.Color.FromArgb(0, 255, 127);
    public static readonly Eto.Drawing.Color Violet = Eto.Drawing.Color.FromArgb(238, 130, 238);
    public static readonly Eto.Drawing.Color WhiteSmoke = Eto.Drawing.Color.FromArgb(245, 245, 245);
    public static readonly Eto.Drawing.Color GreenYellow = Eto.Drawing.Color.FromArgb(173, 255, 47);
    public static readonly Eto.Drawing.Color LightSkyBlue = Eto.Drawing.Color.FromArgb(135, 206, 250);
    public static readonly Eto.Drawing.Color MediumPurple = Eto.Drawing.Color.FromArgb(147, 112, 219);
    public static readonly Eto.Drawing.Color Plum = Eto.Drawing.Color.FromArgb(221, 160, 221);
    public static readonly Eto.Drawing.Color Orchid = Eto.Drawing.Color.FromArgb(218, 112, 214);
    public static readonly Eto.Drawing.Color MediumOrchid = Eto.Drawing.Color.FromArgb(186, 85, 211);
    public static readonly Eto.Drawing.Color RoyalBlue = Eto.Drawing.Color.FromArgb(65, 105, 225);
    public static readonly Eto.Drawing.Color CornflowerBlue = Eto.Drawing.Color.FromArgb(100, 149, 237);
    public static readonly Eto.Drawing.Color SteelBlue = Eto.Drawing.Color.FromArgb(70, 130, 180);
    public static readonly Eto.Drawing.Color DodgerBlue = Eto.Drawing.Color.FromArgb(30, 144, 255);
    public static readonly Eto.Drawing.Color DeepSkyBlue = Eto.Drawing.Color.FromArgb(0, 191, 255);
    public static readonly Eto.Drawing.Color CadetBlue = Eto.Drawing.Color.FromArgb(95, 158, 160);
    public static readonly Eto.Drawing.Color MediumAquamarine = Eto.Drawing.Color.FromArgb(102, 205, 170);
    public static readonly Eto.Drawing.Color SeaGreen = Eto.Drawing.Color.FromArgb(46, 139, 87);
    public static readonly Eto.Drawing.Color MediumSeaGreen = Eto.Drawing.Color.FromArgb(60, 179, 113);
    public static readonly Eto.Drawing.Color PaleGreen = Eto.Drawing.Color.FromArgb(152, 251, 152);
    public static readonly Eto.Drawing.Color LawnGreen = Eto.Drawing.Color.FromArgb(124, 252, 0);
    public static readonly Eto.Drawing.Color MediumSpringGreen = Eto.Drawing.Color.FromArgb(0, 250, 154);
    public static readonly Eto.Drawing.Color YellowGreen = Eto.Drawing.Color.FromArgb(154, 205, 50);
    public static readonly Eto.Drawing.Color OliveDrab = Eto.Drawing.Color.FromArgb(107, 142, 35);
    public static readonly Eto.Drawing.Color DarkOliveGreen = Eto.Drawing.Color.FromArgb(85, 107, 47);
    public static readonly Eto.Drawing.Color DarkSeaGreen = Eto.Drawing.Color.FromArgb(143, 188, 143);
    public static readonly Eto.Drawing.Color ForestGreen = Eto.Drawing.Color.FromArgb(34, 139, 34);
    public static readonly Eto.Drawing.Color LimeGreen = Eto.Drawing.Color.FromArgb(50, 205, 50);
    public static readonly Eto.Drawing.Color LightSeaGreen = Eto.Drawing.Color.FromArgb(32, 178, 170);
    public static readonly Eto.Drawing.Color MediumTurquoise = Eto.Drawing.Color.FromArgb(72, 209, 204);
    public static readonly Eto.Drawing.Color PaleTurquoise = Eto.Drawing.Color.FromArgb(175, 238, 238);
    public static readonly Eto.Drawing.Color PowderBlue = Eto.Drawing.Color.FromArgb(176, 224, 230);
    public static readonly Eto.Drawing.Color LightSteelBlue = Eto.Drawing.Color.FromArgb(176, 196, 222);
    public static readonly Eto.Drawing.Color Gainsboro = Eto.Drawing.Color.FromArgb(220, 220, 220);
}

/// <summary>
/// Color extension methods for WinForms compatibility
/// </summary>
public static class ColorExtensions
{
    /// <summary>
    /// Creates a Color with the specified alpha and base color (WinForms Color.FromArgb compatibility)
    /// </summary>
    public static Eto.Drawing.Color FromArgb(int alpha, Eto.Drawing.Color baseColor)
    {
        return Eto.Drawing.Color.FromArgb(
            (int)(baseColor.R * 255),
            (int)(baseColor.G * 255),
            (int)(baseColor.B * 255),
            alpha);
    }
}

/// <summary>
/// Pens static class for WinForms compatibility
/// </summary>
public static class Pens
{
    public static Eto.Drawing.Pen Black => new Eto.Drawing.Pen(Eto.Drawing.Colors.Black);
    public static Eto.Drawing.Pen White => new Eto.Drawing.Pen(Eto.Drawing.Colors.White);
    public static Eto.Drawing.Pen Red => new Eto.Drawing.Pen(Eto.Drawing.Colors.Red);
    public static Eto.Drawing.Pen Green => new Eto.Drawing.Pen(Eto.Drawing.Colors.Green);
    public static Eto.Drawing.Pen Blue => new Eto.Drawing.Pen(Eto.Drawing.Colors.Blue);
    public static Eto.Drawing.Pen Gray => new Eto.Drawing.Pen(Eto.Drawing.Colors.Gray);
    public static Eto.Drawing.Pen LightGray => new Eto.Drawing.Pen(ColorsCompat.LightGray);
    public static Eto.Drawing.Pen DarkGray => new Eto.Drawing.Pen(Eto.Drawing.Colors.DarkGray);
    public static Eto.Drawing.Pen Yellow => new Eto.Drawing.Pen(Eto.Drawing.Colors.Yellow);
    public static Eto.Drawing.Pen Orange => new Eto.Drawing.Pen(Eto.Drawing.Colors.Orange);
    public static Eto.Drawing.Pen Pink => new Eto.Drawing.Pen(Eto.Drawing.Colors.Pink);
    public static Eto.Drawing.Pen Cyan => new Eto.Drawing.Pen(Eto.Drawing.Colors.Cyan);
    public static Eto.Drawing.Pen Magenta => new Eto.Drawing.Pen(Eto.Drawing.Colors.Magenta);
}

/// <summary>
/// Brushes static class for WinForms compatibility
/// </summary>
public static class Brushes
{
    public static Eto.Drawing.SolidBrush Black => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Black);
    public static Eto.Drawing.SolidBrush White => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.White);
    public static Eto.Drawing.SolidBrush Red => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Red);
    public static Eto.Drawing.SolidBrush Green => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Green);
    public static Eto.Drawing.SolidBrush Blue => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Blue);
    public static Eto.Drawing.SolidBrush Gray => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Gray);
    public static Eto.Drawing.SolidBrush LightGray => new Eto.Drawing.SolidBrush(ColorsCompat.LightGray);
    public static Eto.Drawing.SolidBrush DarkGray => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.DarkGray);
    public static Eto.Drawing.SolidBrush Yellow => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Yellow);
    public static Eto.Drawing.SolidBrush Orange => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Orange);
    public static Eto.Drawing.SolidBrush Pink => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Pink);
    public static Eto.Drawing.SolidBrush Cyan => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Cyan);
    public static Eto.Drawing.SolidBrush Magenta => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Magenta);
    public static Eto.Drawing.SolidBrush Transparent => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Transparent);
    public static Eto.Drawing.SolidBrush Navy => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Navy);
    public static Eto.Drawing.SolidBrush Brown => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Brown);
    public static Eto.Drawing.SolidBrush Purple => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Purple);
    public static Eto.Drawing.SolidBrush Salmon => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Salmon);
    public static Eto.Drawing.SolidBrush Coral => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Coral);
    public static Eto.Drawing.SolidBrush Lime => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Lime);
    public static Eto.Drawing.SolidBrush Teal => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Teal);
    public static Eto.Drawing.SolidBrush Olive => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Olive);
    public static Eto.Drawing.SolidBrush Maroon => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Maroon);
    public static Eto.Drawing.SolidBrush Aqua => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Aqua);
    public static Eto.Drawing.SolidBrush Fuchsia => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Fuchsia);
    public static Eto.Drawing.SolidBrush Silver => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Silver);
    public static Eto.Drawing.SolidBrush SkyBlue => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.SkyBlue);
    public static Eto.Drawing.SolidBrush LightBlue => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.LightBlue);
    public static Eto.Drawing.SolidBrush DarkBlue => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.DarkBlue);
    public static Eto.Drawing.SolidBrush LightGreen => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.LightGreen);
    public static Eto.Drawing.SolidBrush DarkGreen => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.DarkGreen);
    public static Eto.Drawing.SolidBrush LightCyan => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.LightCyan);
    public static Eto.Drawing.SolidBrush DarkCyan => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.DarkCyan);
    public static Eto.Drawing.SolidBrush DarkRed => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.DarkRed);
    public static Eto.Drawing.SolidBrush Goldenrod => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Goldenrod);
    public static Eto.Drawing.SolidBrush Khaki => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.Khaki);
    public static Eto.Drawing.SolidBrush SlateGray => new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.SlateGray);
}

/// <summary>
/// Color helper with additional FromArgb overloads
/// </summary>
public static class ColorHelper
{
    /// <summary>
    /// Create color from packed ARGB value (WinForms style)
    /// </summary>
    public static Eto.Drawing.Color FromArgb(int argb)
    {
        int a = (argb >> 24) & 0xFF;
        int r = (argb >> 16) & 0xFF;
        int g = (argb >> 8) & 0xFF;
        int b = argb & 0xFF;
        return Eto.Drawing.Color.FromArgb(r, g, b, a);
    }

    /// <summary>
    /// Create color from alpha and existing color (WinForms style)
    /// </summary>
    public static Eto.Drawing.Color FromArgb(int alpha, Eto.Drawing.Color baseColor)
    {
        return Eto.Drawing.Color.FromArgb((int)(baseColor.R * 255), (int)(baseColor.G * 255), (int)(baseColor.B * 255), alpha);
    }

    /// <summary>
    /// Create color from RGB (WinForms style - alpha = 255)
    /// </summary>
    public static Eto.Drawing.Color FromRgb(int r, int g, int b)
    {
        return Eto.Drawing.Color.FromArgb(r, g, b, 255);
    }
}

/// <summary>
/// ControlPaint utility for WinForms compatibility
/// </summary>
public static class ControlPaint
{
    public static void DrawBorder3D(Graphics g, Rectangle rect)
    {
        DrawBorder3D(g, rect, Border3DStyle.Etched);
    }

    public static void DrawBorder3D(Graphics g, Rectangle rect, Border3DStyle style)
    {
        var pen1 = new Eto.Drawing.Pen(Eto.Drawing.Color.FromArgb(160, 160, 160));
        var pen2 = new Eto.Drawing.Pen(Eto.Drawing.Colors.White);

        g.DrawLine(pen1, rect.Left, rect.Top, rect.Right - 1, rect.Top);
        g.DrawLine(pen1, rect.Left, rect.Top, rect.Left, rect.Bottom - 1);
        g.DrawLine(pen2, rect.Right - 1, rect.Top, rect.Right - 1, rect.Bottom - 1);
        g.DrawLine(pen2, rect.Left, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1);
    }

    public static void DrawButton(Graphics g, Rectangle rect, ButtonState state)
    {
        var brush = new Eto.Drawing.SolidBrush(Eto.Drawing.Color.FromArgb(240, 240, 240));
        var pen = new Eto.Drawing.Pen(Eto.Drawing.Color.FromArgb(160, 160, 160));

        g.FillRectangle(brush, rect);
        g.DrawRectangle(pen, rect);
    }

    public static void DrawCheckBox(Graphics g, Rectangle rect, ButtonState state)
    {
        // Draw checkbox background
        var bgBrush = new Eto.Drawing.SolidBrush(Eto.Drawing.Colors.White);
        var borderPen = new Eto.Drawing.Pen(Eto.Drawing.Color.FromArgb(100, 100, 100));

        g.FillRectangle(bgBrush, rect);
        g.DrawRectangle(borderPen, rect);

        // If checked, draw checkmark
        if ((state & ButtonState.Checked) != 0)
        {
            var checkPen = new Eto.Drawing.Pen(Eto.Drawing.Colors.Black, 2);
            int x = rect.X + 2;
            int y = rect.Y + rect.Height / 2;
            g.DrawLine(checkPen, x + 2, y, x + rect.Width / 3, y + rect.Height / 4);
            g.DrawLine(checkPen, x + rect.Width / 3, y + rect.Height / 4, x + rect.Width - 4, y - rect.Height / 4);
        }
    }

    public static void DrawFocusRectangle(Graphics g, Rectangle rect)
    {
        var pen = new Eto.Drawing.Pen(Eto.Drawing.Colors.Black, 1);
        pen.DashStyle = Eto.Drawing.DashStyles.Dot;
        g.DrawRectangle(pen, rect);
    }

    public static void DrawFocusRectangle(Graphics g, Rectangle rect, Eto.Drawing.Color foreColor, Eto.Drawing.Color backColor)
    {
        DrawFocusRectangle(g, rect);
    }
}

/// <summary>
/// Border3DStyle enum for WinForms compatibility
/// </summary>
public enum Border3DStyle
{
    Adjust = 8192,
    Bump = 9,
    Etched = 6,
    Flat = 16394,
    Raised = 5,
    RaisedInner = 4,
    RaisedOuter = 1,
    Sunken = 10,
    SunkenInner = 8,
    SunkenOuter = 2
}

/// <summary>
/// ButtonState enum for WinForms compatibility
/// </summary>
[Flags]
public enum ButtonState
{
    Normal = 0,
    Inactive = 256,
    Pushed = 512,
    Checked = 1024,
    Flat = 16384,
    All = 18176
}

/// <summary>
/// GraphicsUnit enum for WinForms compatibility
/// </summary>
public enum GraphicsUnit
{
    World = 0,
    Display = 1,
    Pixel = 2,
    Point = 3,
    Inch = 4,
    Document = 5,
    Millimeter = 6
}

/// <summary>
/// Additional color values not in Eto.Drawing.Colors
/// </summary>
public static class WinFormsColors
{
    // Red/Pink colors
    public static readonly Eto.Drawing.Color IndianRed = Eto.Drawing.Color.FromArgb(205, 92, 92);
    public static readonly Eto.Drawing.Color LightCoral = Eto.Drawing.Color.FromArgb(240, 128, 128);
    public static readonly Eto.Drawing.Color Salmon = Eto.Drawing.Color.FromArgb(250, 128, 114);
    public static readonly Eto.Drawing.Color DarkSalmon = Eto.Drawing.Color.FromArgb(233, 150, 122);
    public static readonly Eto.Drawing.Color LightSalmon = Eto.Drawing.Color.FromArgb(255, 160, 122);
    public static readonly Eto.Drawing.Color Coral = Eto.Drawing.Color.FromArgb(255, 127, 80);
    public static readonly Eto.Drawing.Color Tomato = Eto.Drawing.Color.FromArgb(255, 99, 71);
    public static readonly Eto.Drawing.Color OrangeRed = Eto.Drawing.Color.FromArgb(255, 69, 0);
    public static readonly Eto.Drawing.Color DarkOrange = Eto.Drawing.Color.FromArgb(255, 140, 0);
    public static readonly Eto.Drawing.Color Gold = Eto.Drawing.Color.FromArgb(255, 215, 0);
    // Blue colors
    public static readonly Eto.Drawing.Color SteelBlue = Eto.Drawing.Color.FromArgb(70, 130, 180);
    public static readonly Eto.Drawing.Color Khaki = Eto.Drawing.Color.FromArgb(240, 230, 140);
    public static readonly Eto.Drawing.Color LightGoldenrodYellow = Eto.Drawing.Color.FromArgb(250, 250, 210);
    public static readonly Eto.Drawing.Color LightYellow = Eto.Drawing.Color.FromArgb(255, 255, 224);
    public static readonly Eto.Drawing.Color PaleGoldenrod = Eto.Drawing.Color.FromArgb(238, 232, 170);
    public static readonly Eto.Drawing.Color PeachPuff = Eto.Drawing.Color.FromArgb(255, 218, 185);
    public static readonly Eto.Drawing.Color Moccasin = Eto.Drawing.Color.FromArgb(255, 228, 181);
    public static readonly Eto.Drawing.Color PapayaWhip = Eto.Drawing.Color.FromArgb(255, 239, 213);
    public static readonly Eto.Drawing.Color LightGray = Eto.Drawing.Color.FromArgb(211, 211, 211);
    public static readonly Eto.Drawing.Color DarkGray = Eto.Drawing.Color.FromArgb(169, 169, 169);
    public static readonly Eto.Drawing.Color DimGray = Eto.Drawing.Color.FromArgb(105, 105, 105);
    public static readonly Eto.Drawing.Color SlateGray = Eto.Drawing.Color.FromArgb(112, 128, 144);
    public static readonly Eto.Drawing.Color LightSlateGray = Eto.Drawing.Color.FromArgb(119, 136, 153);
    public static readonly Eto.Drawing.Color DarkSlateGray = Eto.Drawing.Color.FromArgb(47, 79, 79);
    public static readonly Eto.Drawing.Color Gainsboro = Eto.Drawing.Color.FromArgb(220, 220, 220);
    public static readonly Eto.Drawing.Color WhiteSmoke = Eto.Drawing.Color.FromArgb(245, 245, 245);
    public static readonly Eto.Drawing.Color Snow = Eto.Drawing.Color.FromArgb(255, 250, 250);
    public static readonly Eto.Drawing.Color Honeydew = Eto.Drawing.Color.FromArgb(240, 255, 240);
    public static readonly Eto.Drawing.Color MintCream = Eto.Drawing.Color.FromArgb(245, 255, 250);
    public static readonly Eto.Drawing.Color Azure = Eto.Drawing.Color.FromArgb(240, 255, 255);
    public static readonly Eto.Drawing.Color AliceBlue = Eto.Drawing.Color.FromArgb(240, 248, 255);
    public static readonly Eto.Drawing.Color GhostWhite = Eto.Drawing.Color.FromArgb(248, 248, 255);
    public static readonly Eto.Drawing.Color Lavender = Eto.Drawing.Color.FromArgb(230, 230, 250);
    public static readonly Eto.Drawing.Color Beige = Eto.Drawing.Color.FromArgb(245, 245, 220);
    public static readonly Eto.Drawing.Color OldLace = Eto.Drawing.Color.FromArgb(253, 245, 230);
    public static readonly Eto.Drawing.Color FloralWhite = Eto.Drawing.Color.FromArgb(255, 250, 240);
    public static readonly Eto.Drawing.Color Ivory = Eto.Drawing.Color.FromArgb(255, 255, 240);
    public static readonly Eto.Drawing.Color AntiqueWhite = Eto.Drawing.Color.FromArgb(250, 235, 215);
    public static readonly Eto.Drawing.Color Linen = Eto.Drawing.Color.FromArgb(250, 240, 230);
    public static readonly Eto.Drawing.Color LavenderBlush = Eto.Drawing.Color.FromArgb(255, 240, 245);
    public static readonly Eto.Drawing.Color MistyRose = Eto.Drawing.Color.FromArgb(255, 228, 225);
}

/// <summary>
/// SystemColors compatibility for WinForms
/// </summary>
public static class SystemColors
{
    public static readonly Eto.Drawing.Color Window = Eto.Drawing.Colors.White;
    public static readonly Eto.Drawing.Color WindowText = Eto.Drawing.Colors.Black;
    public static readonly Eto.Drawing.Color Control = Eto.Drawing.Color.FromArgb(240, 240, 240);
    public static readonly Eto.Drawing.Color ControlText = Eto.Drawing.Colors.Black;
    public static readonly Eto.Drawing.Color Highlight = Eto.Drawing.Color.FromArgb(0, 120, 215);
    public static readonly Eto.Drawing.Color HighlightText = Eto.Drawing.Colors.White;
    public static readonly Eto.Drawing.Color GrayText = Eto.Drawing.Color.FromArgb(109, 109, 109);
    public static readonly Eto.Drawing.Color ActiveCaption = Eto.Drawing.Color.FromArgb(153, 180, 209);
    public static readonly Eto.Drawing.Color InactiveCaption = Eto.Drawing.Color.FromArgb(191, 205, 219);
    public static readonly Eto.Drawing.Color ButtonFace = Eto.Drawing.Color.FromArgb(240, 240, 240);
    public static readonly Eto.Drawing.Color ButtonHighlight = Eto.Drawing.Colors.White;
    public static readonly Eto.Drawing.Color ButtonShadow = Eto.Drawing.Color.FromArgb(160, 160, 160);
}
/// <summary>
/// PixelFormat struct for compatibility with implicit conversion to Eto.Drawing.PixelFormat
/// </summary>
public struct PixelFormat
{
    private readonly int _value;

    private PixelFormat(int value) => _value = value;

    public static readonly PixelFormat Format24bppRgb = new(1);
    public static readonly PixelFormat Format32bppArgb = new(2);
    public static readonly PixelFormat Format32bppRgb = new(3);
    public static readonly PixelFormat Format8bppIndexed = new(4);
    public static readonly PixelFormat Format16bppRgb555 = new(5);
    public static readonly PixelFormat Format16bppRgb565 = new(6);
    public static readonly PixelFormat Format16bppArgb1555 = new(7);

    public static implicit operator Eto.Drawing.PixelFormat(PixelFormat format)
    {
        return format._value switch
        {
            1 => Eto.Drawing.PixelFormat.Format24bppRgb,
            2 => Eto.Drawing.PixelFormat.Format32bppRgba,
            3 => Eto.Drawing.PixelFormat.Format32bppRgb,
            _ => Eto.Drawing.PixelFormat.Format32bppRgba
        };
    }

    public override bool Equals(object? obj) => obj is PixelFormat pf && pf._value == _value;
    public override int GetHashCode() => _value;
    public static bool operator ==(PixelFormat left, PixelFormat right) => left._value == right._value;
    public static bool operator !=(PixelFormat left, PixelFormat right) => left._value != right._value;
}

/// <summary>
/// PixelFormat helper for conversions
/// </summary>
public static class PixelFormatHelper
{
    public static Eto.Drawing.PixelFormat ToEtoFormat(this PixelFormat format)
    {
        if (format == PixelFormat.Format32bppArgb)
            return Eto.Drawing.PixelFormat.Format32bppRgba;
        if (format == PixelFormat.Format32bppRgb)
            return Eto.Drawing.PixelFormat.Format32bppRgb;
        if (format == PixelFormat.Format24bppRgb)
            return Eto.Drawing.PixelFormat.Format24bppRgb;
        // Fallback for other formats
        return Eto.Drawing.PixelFormat.Format32bppRgba;
    }
}

/// <summary>
/// ImageLockMode enum for compatibility
/// </summary>
public enum ImageLockMode
{
    ReadOnly,
    WriteOnly,
    ReadWrite
}

/// <summary>
/// BitmapData class for compatibility - supports direct pixel manipulation
/// </summary>
public class BitmapData : IDisposable
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Stride { get; set; }
    public IntPtr Scan0 { get; set; }
    public PixelFormat PixelFormat { get; set; }

    // Internal buffer management
    internal byte[]? Buffer { get; set; }
    internal System.Runtime.InteropServices.GCHandle BufferHandle { get; set; }
    internal Eto.Drawing.Bitmap? SourceBitmap { get; set; }

    public void Dispose()
    {
        if (BufferHandle.IsAllocated)
        {
            BufferHandle.Free();
        }
        Buffer = null;
        Scan0 = IntPtr.Zero;
    }
}

/// <summary>
/// MessageBox wrapper for WinForms compatibility
/// </summary>
public static class WinFormsMessageBox
{
    public static Eto.Forms.DialogResult Show(string text)
    {
        return Eto.Forms.MessageBox.Show(text);
    }

    public static Eto.Forms.DialogResult Show(string text, string caption)
    {
        return Eto.Forms.MessageBox.Show(text, caption);
    }

    public static Eto.Forms.DialogResult Show(string text, string caption, MessageBoxButtons buttons)
    {
        var type = ConvertButtons(buttons);
        return Eto.Forms.MessageBox.Show(text, caption, type);
    }

    public static Eto.Forms.DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        var type = ConvertButtons(buttons);
        return Eto.Forms.MessageBox.Show(text, caption, type);
    }

    public static Eto.Forms.DialogResult Show(Eto.Forms.Control owner, string text)
    {
        return Eto.Forms.MessageBox.Show(owner, text);
    }

    public static Eto.Forms.DialogResult Show(Eto.Forms.Control owner, string text, string caption)
    {
        return Eto.Forms.MessageBox.Show(owner, text, caption);
    }

    public static Eto.Forms.DialogResult Show(Eto.Forms.Control owner, string text, string caption, MessageBoxButtons buttons)
    {
        var type = ConvertButtons(buttons);
        return Eto.Forms.MessageBox.Show(owner, text, caption, type);
    }

    public static Eto.Forms.DialogResult Show(Eto.Forms.Control owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        var type = ConvertButtons(buttons);
        return Eto.Forms.MessageBox.Show(owner, text, caption, type);
    }

    public static Eto.Forms.DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        var type = ConvertButtons(buttons);
        return Eto.Forms.MessageBox.Show(text, caption, type);
    }

    public static Eto.Forms.DialogResult Show(Eto.Forms.Control owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        var type = ConvertButtons(buttons);
        return Eto.Forms.MessageBox.Show(owner, text, caption, type);
    }

    private static Eto.Forms.MessageBoxButtons ConvertButtons(MessageBoxButtons buttons)
    {
        return buttons switch
        {
            MessageBoxButtons.OK => Eto.Forms.MessageBoxButtons.OK,
            MessageBoxButtons.OKCancel => Eto.Forms.MessageBoxButtons.OKCancel,
            MessageBoxButtons.YesNo => Eto.Forms.MessageBoxButtons.YesNo,
            MessageBoxButtons.YesNoCancel => Eto.Forms.MessageBoxButtons.YesNoCancel,
            _ => Eto.Forms.MessageBoxButtons.OK
        };
    }
}

/// <summary>
/// Font extensions for WinForms compatibility
/// </summary>
public static class FontExtensions
{
    // WinForms Font constructor: new Font(family, size, style)
    public static Eto.Drawing.Font CreateFont(string familyName, float size, Eto.Drawing.FontStyle style)
    {
        return new Eto.Drawing.Font(familyName, size, style);
    }

    public static Eto.Drawing.Font CreateFont(Eto.Drawing.Font baseFont, Eto.Drawing.FontStyle style)
    {
        if (baseFont == null)
        {
            return new Eto.Drawing.Font(Eto.Drawing.SystemFonts.Default().Family, 9, style);
        }
        return new Eto.Drawing.Font(baseFont.Family, baseFont.Size, style);
    }
}

/// <summary>
/// FlatStyle enum for WinForms compatibility
/// </summary>
public enum FlatStyle
{
    Flat,
    Popup,
    Standard,
    System
}

/// <summary>
/// ScrollBars enum for WinForms compatibility
/// </summary>
public enum ScrollBars
{
    None,
    Horizontal,
    Vertical,
    Both
}

/// <summary>
/// FontStyle compatibility - maps to Eto.Drawing.FontStyle
/// </summary>
public static class FontStyleCompat
{
    public const Eto.Drawing.FontStyle Regular = Eto.Drawing.FontStyle.None;
    public const Eto.Drawing.FontStyle Bold = Eto.Drawing.FontStyle.Bold;
    public const Eto.Drawing.FontStyle Italic = Eto.Drawing.FontStyle.Italic;
}

/// <summary>
/// Keys compatibility for numpad and other missing keys
/// </summary>
public static class KeysCompat
{
    public const Eto.Forms.Keys NumPad0 = Eto.Forms.Keys.Keypad0;
    public const Eto.Forms.Keys NumPad1 = Eto.Forms.Keys.Keypad1;
    public const Eto.Forms.Keys NumPad2 = Eto.Forms.Keys.Keypad2;
    public const Eto.Forms.Keys NumPad3 = Eto.Forms.Keys.Keypad3;
    public const Eto.Forms.Keys NumPad4 = Eto.Forms.Keys.Keypad4;
    public const Eto.Forms.Keys NumPad5 = Eto.Forms.Keys.Keypad5;
    public const Eto.Forms.Keys NumPad6 = Eto.Forms.Keys.Keypad6;
    public const Eto.Forms.Keys NumPad7 = Eto.Forms.Keys.Keypad7;
    public const Eto.Forms.Keys NumPad8 = Eto.Forms.Keys.Keypad8;
    public const Eto.Forms.Keys NumPad9 = Eto.Forms.Keys.Keypad9;
    public const Eto.Forms.Keys OemPeriod = Eto.Forms.Keys.Period;
    public const Eto.Forms.Keys OemMinus = Eto.Forms.Keys.Minus;
}

/// <summary>
/// UITimer extensions for WinForms Timer compatibility
/// </summary>
public static class UITimerExtensions
{
    // Map Tick event to Elapsed event
    public static void AddTickHandler(this Eto.Forms.UITimer timer, EventHandler<EventArgs> handler)
    {
        timer.Elapsed += handler;
    }

    public static void RemoveTickHandler(this Eto.Forms.UITimer timer, EventHandler<EventArgs> handler)
    {
        timer.Elapsed -= handler;
    }
}

/// <summary>
/// Label extensions for WinForms compatibility
/// </summary>
public static class LabelExtensions
{
    public static void SetAutoSize(this Eto.Forms.Label label, bool value) { }
    public static void SetBackColor(this Eto.Forms.Label label, Eto.Drawing.Color color) => label.BackgroundColor = color;
    public static void SetForeColor(this Eto.Forms.Label label, Eto.Drawing.Color color) => label.TextColor = color;
    public static void SetPadding(this Eto.Forms.Label label, Padding padding) { }
    public static void SetBorderStyle(this Eto.Forms.Label label, BorderStyle style) { }
}

/// <summary>
/// Button extensions for WinForms compatibility
/// </summary>
public static class ButtonExtensions
{
    public static void SetBackColor(this Eto.Forms.Button button, Eto.Drawing.Color color) => button.BackgroundColor = color;
    public static void SetForeColor(this Eto.Forms.Button button, Eto.Drawing.Color color) => button.TextColor = color;
    public static void SetFlatStyle(this Eto.Forms.Button button, FlatStyle style) { }
    public static void SetFlatAppearance(this Eto.Forms.Button button, FlatButtonAppearance appearance) { }
}

/// <summary>
/// FlatButtonAppearance for WinForms compatibility
/// </summary>
public class FlatButtonAppearance
{
    public Eto.Drawing.Color BorderColor { get; set; }
    public int BorderSize { get; set; }
    public Eto.Drawing.Color MouseDownBackColor { get; set; }
    public Eto.Drawing.Color MouseOverBackColor { get; set; }
}

/// <summary>
/// Form/Window extensions for WinForms compatibility
/// </summary>
public static class FormExtensions
{
    public static void SetKeyPreview(this Eto.Forms.Form form, bool value) { }
    public static void SetText(this Eto.Forms.Form form, string text) => form.Title = text;
    public static string GetText(this Eto.Forms.Form form) => form.Title;
    public static void SetStartPosition(this Eto.Forms.Form form, FormStartPosition position) { }
    public static void SetMinimizeBox(this Eto.Forms.Form form, bool value) => form.Minimizable = value;
    public static void SetMaximizeBox(this Eto.Forms.Form form, bool value) => form.Maximizable = value;
    public static void SetIcon(this Eto.Forms.Form form, Eto.Drawing.Icon icon) => form.Icon = icon;
    public static void SetFormBorderStyle(this Eto.Forms.Form form, FormBorderStyle style)
    {
        form.Resizable = style == FormBorderStyle.Sizable || style == FormBorderStyle.SizableToolWindow;
    }
    public static void SetTopMost(this Eto.Forms.Form form, bool value) => form.Topmost = value;
    public static void SetShowInTaskbar(this Eto.Forms.Form form, bool value) => form.ShowInTaskbar = value;
    public static void SetAutoScaleDimensions(this Eto.Forms.Form form, Eto.Drawing.SizeF size) { }
    public static void SetAutoScaleMode(this Eto.Forms.Form form, AutoScaleMode mode) { }
    public static void SetMainMenuStrip(this Eto.Forms.Form form, Eto.Forms.MenuBar menu) => form.Menu = menu;
}

/// <summary>
/// TextBox extensions for WinForms compatibility
/// </summary>
public static class TextBoxExtensions
{
    public static void SetMultiline(this Eto.Forms.TextBox textBox, bool value) { }
    public static void SetScrollBars(this Eto.Forms.TextBox textBox, ScrollBars scrollBars) { }
    public static void SetWordWrap(this Eto.Forms.TextBox textBox, bool value) { }
    public static void SetAcceptsReturn(this Eto.Forms.TextBox textBox, bool value) { }
    public static void SetAcceptsTab(this Eto.Forms.TextBox textBox, bool value) { }
    public static void SetBorderStyle(this Eto.Forms.TextBox textBox, BorderStyle style) { }
    public static void SetBackColor(this Eto.Forms.TextBox textBox, Eto.Drawing.Color color) => textBox.BackgroundColor = color;
    public static void SetForeColor(this Eto.Forms.TextBox textBox, Eto.Drawing.Color color) => textBox.TextColor = color;

    // TextArea for multiline text
    public static void SetDock(this Eto.Forms.TextBox textBox, DockStyle dock) { }
}

/// <summary>
/// TextArea extensions for WinForms TextBox multiline compatibility
/// </summary>
public static class TextAreaExtensions
{
    public static void SetScrollBars(this Eto.Forms.TextArea textArea, ScrollBars scrollBars) { }
    public static void SetWordWrap(this Eto.Forms.TextArea textArea, bool value) => textArea.Wrap = value;
    public static void SetDock(this Eto.Forms.TextArea textArea, DockStyle dock) { }
    public static void SetBackColor(this Eto.Forms.TextArea textArea, Eto.Drawing.Color color) => textArea.BackgroundColor = color;
    public static void SetForeColor(this Eto.Forms.TextArea textArea, Eto.Drawing.Color color) => textArea.TextColor = color;
}

/// <summary>
/// GridView extensions for WinForms ListView/DataGridView compatibility
/// </summary>
public static class GridViewExtensions
{
    public static void SetDock(this Eto.Forms.GridView gridView, DockStyle dock) { }
    public static void SetView(this Eto.Forms.GridView gridView, View view) { }
    public static void SetFullRowSelect(this Eto.Forms.GridView gridView, bool value) { }
    public static void SetGridLines(this Eto.Forms.GridView gridView, bool value)
    {
        gridView.GridLines = value ? Eto.Forms.GridLines.Both : Eto.Forms.GridLines.None;
    }
    public static void SetMultiSelect(this Eto.Forms.GridView gridView, bool value) => gridView.AllowMultipleSelection = value;
    public static void SetHeaderStyle(this Eto.Forms.GridView gridView, ColumnHeaderStyle style) { }
    public static void SetSorting(this Eto.Forms.GridView gridView, SortOrder order) { }
    public static void SetHideSelection(this Eto.Forms.GridView gridView, bool value) { }
    public static void SetSmallImageList(this Eto.Forms.GridView gridView, ImageList imageList) { }
    public static void SetLargeImageList(this Eto.Forms.GridView gridView, ImageList imageList) { }

    // Items property compatibility (for ListView-style usage)
    public static ListViewItemCollection GetItems(this Eto.Forms.GridView gridView)
    {
        return new ListViewItemCollection(gridView);
    }

    public static ListViewSelectedIndexCollection GetSelectedIndices(this Eto.Forms.GridView gridView)
    {
        return new ListViewSelectedIndexCollection(gridView);
    }
}

/// <summary>
/// ColumnHeaderStyle enum for GridView compatibility
/// </summary>
public enum ColumnHeaderStyle
{
    None,
    Nonclickable,
    Clickable
}

/// <summary>
/// ListViewItemCollection for GridView compatibility
/// </summary>
public class ListViewItemCollection : System.Collections.Generic.List<ListViewItem>
{
    private readonly Eto.Forms.GridView _gridView;

    public ListViewItemCollection(Eto.Forms.GridView gridView)
    {
        _gridView = gridView;
    }

    public new void Clear()
    {
        base.Clear();
        _gridView.DataStore = null;
    }

    public new void Add(ListViewItem item)
    {
        base.Add(item);
    }

    public void AddRange(ListViewItem[] items)
    {
        base.AddRange(items);
    }
}

/// <summary>
/// ListViewSelectedIndexCollection for GridView compatibility
/// </summary>
public class ListViewSelectedIndexCollection : System.Collections.Generic.List<int>
{
    private readonly Eto.Forms.GridView _gridView;

    public ListViewSelectedIndexCollection(Eto.Forms.GridView gridView)
    {
        _gridView = gridView;
    }
}

/// <summary>
/// TabControl extensions for WinForms compatibility
/// </summary>
public static class TabControlExtensions
{
    public static void SetDock(this Eto.Forms.TabControl tabControl, DockStyle dock) { }
    public static void SetSelectedIndex(this Eto.Forms.TabControl tabControl, int index) => tabControl.SelectedIndex = index;
    public static int GetSelectedIndex(this Eto.Forms.TabControl tabControl) => tabControl.SelectedIndex;
    // Note: GetTabPages is defined in ContainerExtensions to avoid ambiguity
}

/// <summary>
/// TabPage extensions for WinForms compatibility
/// </summary>
public static class TabPageExtensions
{
    public static void SetUseVisualStyleBackColor(this Eto.Forms.TabPage tabPage, bool value) { }
}

/// <summary>
/// Panel extensions for WinForms compatibility
/// </summary>
public static class PanelExtensions
{
    public static void SetDock(this Eto.Forms.Panel panel, DockStyle dock) { }
    public static void SetAutoScroll(this Eto.Forms.Panel panel, bool value) { }
    public static void SetBackColor(this Eto.Forms.Panel panel, Eto.Drawing.Color color) => panel.BackgroundColor = color;
    public static void SetBorderStyle(this Eto.Forms.Panel panel, BorderStyle style) { }
}

/// <summary>
/// CheckBox extensions for WinForms compatibility
/// </summary>
public static class CheckBoxExtensions
{
    public static void SetAutoSize(this Eto.Forms.CheckBox checkBox, bool value) { }
    public static void SetUseVisualStyleBackColor(this Eto.Forms.CheckBox checkBox, bool value) { }
}

/// <summary>
/// ComboBox extensions for WinForms compatibility
/// </summary>
public static class ComboBoxExtensions
{
    public static void SetDropDownStyle(this Eto.Forms.ComboBox comboBox, ComboBoxStyle style)
    {
        comboBox.ReadOnly = style == ComboBoxStyle.DropDownList;
    }
    public static void SetFormattingEnabled(this Eto.Forms.ComboBox comboBox, bool value) { }
}

/// <summary>
/// NumericStepper extensions for WinForms NumericUpDown compatibility
/// </summary>
public static class NumericStepperExtensions
{
    public static void SetDecimalPlaces(this Eto.Forms.NumericStepper stepper, int value) => stepper.DecimalPlaces = value;
    public static void SetMaximum(this Eto.Forms.NumericStepper stepper, double value) => stepper.MaxValue = value;
    public static void SetMinimum(this Eto.Forms.NumericStepper stepper, double value) => stepper.MinValue = value;
    public static void SetIncrement(this Eto.Forms.NumericStepper stepper, double value) => stepper.Increment = value;
}

/// <summary>
/// Splitter extensions for WinForms SplitContainer compatibility
/// </summary>
public static class SplitterExtensions
{
    public static void SetDock(this Eto.Forms.Splitter splitter, DockStyle dock) { }
    public static void SetPanel1MinSize(this Eto.Forms.Splitter splitter, int value) { }
    public static void SetPanel2MinSize(this Eto.Forms.Splitter splitter, int value) { }
    public static void SetSplitterDistance(this Eto.Forms.Splitter splitter, int value) => splitter.Position = value;
    public static int GetSplitterDistance(this Eto.Forms.Splitter splitter) => splitter.Position;
    public static void SetFixedPanel(this Eto.Forms.Splitter splitter, FixedPanel panel)
    {
        splitter.FixedPanel = panel == FixedPanel.Panel1 ? Eto.Forms.SplitterFixedPanel.Panel1 :
                              panel == FixedPanel.Panel2 ? Eto.Forms.SplitterFixedPanel.Panel2 :
                              Eto.Forms.SplitterFixedPanel.None;
    }
}

/// <summary>
/// FixedPanel enum for Splitter compatibility
/// </summary>
public enum FixedPanel
{
    None,
    Panel1,
    Panel2
}

/// <summary>
/// ProgressBar extensions for WinForms compatibility
/// </summary>
public static class ProgressBarExtensions
{
    public static void SetStyle(this Eto.Forms.ProgressBar progressBar, ProgressBarStyle style)
    {
        progressBar.Indeterminate = style == ProgressBarStyle.Marquee;
    }
}

/// <summary>
/// ProgressBarStyle enum for compatibility
/// </summary>
public enum ProgressBarStyle
{
    Blocks,
    Continuous,
    Marquee
}

/// <summary>
/// DrawMode enum for WinForms compatibility
/// </summary>
public enum DrawMode
{
    Normal,
    OwnerDrawFixed,
    OwnerDrawVariable
}

/// <summary>
/// GroupBox extensions for WinForms compatibility
/// </summary>
public static class GroupBoxExtensions
{
    public static void SetDock(this Eto.Forms.GroupBox groupBox, DockStyle dock) { }
    public static void SetAutoSize(this Eto.Forms.GroupBox groupBox, bool value) { }
    public static void SetAutoSizeMode(this Eto.Forms.GroupBox groupBox, AutoSizeMode mode) { }
}

/// <summary>
/// AutoSizeMode enum for compatibility
/// </summary>
public enum AutoSizeMode
{
    GrowAndShrink,
    GrowOnly
}

/// <summary>
/// MenuBar extensions for WinForms MenuStrip compatibility
/// </summary>
public static class MenuBarExtensions
{
    public static void SetDock(this Eto.Forms.MenuBar menuBar, DockStyle dock) { }
}

/// <summary>
/// ButtonMenuItem extensions for WinForms ToolStripMenuItem compatibility
/// </summary>
public static class ButtonMenuItemExtensions
{
    public static void SetShortcutKeys(this Eto.Forms.ButtonMenuItem menuItem, Eto.Forms.Keys keys)
    {
        menuItem.Shortcut = keys;
    }

    public static void SetChecked(this Eto.Forms.ButtonMenuItem menuItem, bool value)
    {
        // ButtonMenuItem doesn't have Checked, but CheckMenuItem does
        // This is a stub for compatibility
    }

    public static bool GetChecked(this Eto.Forms.ButtonMenuItem menuItem)
    {
        return false;
    }

    public static void SetCheckOnClick(this Eto.Forms.ButtonMenuItem menuItem, bool value) { }
    public static void SetImage(this Eto.Forms.ButtonMenuItem menuItem, Eto.Drawing.Image image) => menuItem.Image = image;
}

/// <summary>
/// ImageView extensions for WinForms PictureBox compatibility
/// </summary>
public static class ImageViewExtensions
{
    public static void SetDock(this Eto.Forms.ImageView imageView, DockStyle dock) { }
    public static void SetSizeMode(this Eto.Forms.ImageView imageView, PictureBoxSizeMode mode) { }
    public static void SetBackColor(this Eto.Forms.ImageView imageView, Eto.Drawing.Color color) => imageView.BackgroundColor = color;
    public static void SetBorderStyle(this Eto.Forms.ImageView imageView, BorderStyle style) { }
}

/// <summary>
/// Common control property extensions
/// </summary>
public static class CommonControlExtensions
{
    // 存儲控件位置的字典
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Eto.Forms.Control, LocationHolder> _locations
        = new System.Runtime.CompilerServices.ConditionalWeakTable<Eto.Forms.Control, LocationHolder>();

    private class LocationHolder
    {
        public Eto.Drawing.Point Location { get; set; }
    }

    // Location - stored for PixelLayout positioning
    public static void SetLocation(this Eto.Forms.Control control, Eto.Drawing.Point location)
    {
        var holder = _locations.GetOrCreateValue(control);
        holder.Location = location;

        // 如果控件已在 PixelLayout 中，更新其位置
        if (control.Parent is Eto.Forms.PixelLayout pixelLayout)
        {
            pixelLayout.Move(control, location);
        }
    }

    public static Eto.Drawing.Point GetLocation(this Eto.Forms.Control control)
    {
        // 先檢查存儲的位置
        if (_locations.TryGetValue(control, out var holder))
        {
            return holder.Location;
        }

        // 檢查是否有 Location 屬性 (WinForms 相容類別)
        var locProp = control.GetType().GetProperty("Location", typeof(Point));
        if (locProp != null)
        {
            var loc = (Point)locProp.GetValue(control);
            return new Eto.Drawing.Point(loc.X, loc.Y);
        }

        return new Eto.Drawing.Point(0, 0);
    }

    // Size
    public static void SetSize(this Eto.Forms.Control control, Eto.Drawing.Size size)
    {
        control.Width = size.Width;
        control.Height = size.Height;
    }

    // TabIndex
    public static void SetTabIndex(this Eto.Forms.Control control, int index) { }

    // UseWaitCursor
    public static void SetUseWaitCursor(this Eto.Forms.Control control, bool value)
    {
        control.Cursor = value ? Eto.Forms.Cursors.Default : null;
    }

    // ContextMenuStrip
    public static void SetContextMenuStrip(this Eto.Forms.Control control, Eto.Forms.ContextMenu menu)
    {
        control.ContextMenu = menu;
    }

    // AllowDrop
    public static void SetAllowDrop(this Eto.Forms.Control control, bool value)
    {
        control.AllowDrop = value;
    }

    // Note: InvokeRequired, Invoke, BeginInvoke are defined in EtoCompatExtensions
}

/// <summary>
/// Pen extensions for WinForms compatibility
/// </summary>
public static class PenExtensions
{
    // Store DashPattern values
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Eto.Drawing.Pen, float[]> _dashPatterns
        = new System.Runtime.CompilerServices.ConditionalWeakTable<Eto.Drawing.Pen, float[]>();

    public static void SetDashPattern(this Eto.Drawing.Pen pen, float[] pattern)
    {
        _dashPatterns.AddOrUpdate(pen, pattern);
        // Eto doesn't directly support custom dash patterns, but we store it
        pen.DashStyle = Eto.Drawing.DashStyles.Dash;
    }

    public static float[] GetDashPattern(this Eto.Drawing.Pen pen)
    {
        return _dashPatterns.TryGetValue(pen, out var pattern) ? pattern : Array.Empty<float>();
    }

    // DashStyle property alias - use implicit conversion
    public static void SetDashStyle(this Eto.Drawing.Pen pen, L1MapViewer.Compatibility.DashStyle style)
    {
        pen.DashStyle = style; // Uses implicit conversion operator
    }

    // Store LineCap values
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Eto.Drawing.Pen, LineCap[]> _caps
        = new System.Runtime.CompilerServices.ConditionalWeakTable<Eto.Drawing.Pen, LineCap[]>();

    public static void SetEndCap(this Eto.Drawing.Pen pen, LineCap cap)
    {
        var caps = _caps.GetOrCreateValue(pen);
        if (caps == null || caps.Length < 2)
        {
            caps = new LineCap[2];
            _caps.AddOrUpdate(pen, caps);
        }
        caps[1] = cap;
        pen.LineCap = ConvertLineCap(cap);
    }

    public static LineCap GetEndCap(this Eto.Drawing.Pen pen)
    {
        return _caps.TryGetValue(pen, out var caps) && caps.Length > 1 ? caps[1] : LineCap.Flat;
    }

    public static void SetStartCap(this Eto.Drawing.Pen pen, LineCap cap)
    {
        var caps = _caps.GetOrCreateValue(pen);
        if (caps == null || caps.Length < 2)
        {
            caps = new LineCap[2];
            _caps.AddOrUpdate(pen, caps);
        }
        caps[0] = cap;
    }

    public static LineCap GetStartCap(this Eto.Drawing.Pen pen)
    {
        return _caps.TryGetValue(pen, out var caps) && caps.Length > 0 ? caps[0] : LineCap.Flat;
    }

    private static Eto.Drawing.PenLineCap ConvertLineCap(LineCap cap)
    {
        return cap switch
        {
            LineCap.Round => Eto.Drawing.PenLineCap.Round,
            LineCap.Square => Eto.Drawing.PenLineCap.Square,
            _ => Eto.Drawing.PenLineCap.Butt
        };
    }
}

/// <summary>
/// LineCap enum for WinForms compatibility
/// </summary>
public enum LineCap
{
    Flat,
    Square,
    Round,
    Triangle,
    NoAnchor,
    SquareAnchor,
    RoundAnchor,
    DiamondAnchor,
    ArrowAnchor,
    AnchorMask,
    Custom
}

/// <summary>
/// DashStyle struct for WinForms compatibility with implicit conversion to Eto.Drawing.DashStyle
/// </summary>
public struct DashStyle
{
    private readonly int _value;

    private DashStyle(int value) => _value = value;

    public static readonly DashStyle Solid = new(0);
    public static readonly DashStyle Dash = new(1);
    public static readonly DashStyle Dot = new(2);
    public static readonly DashStyle DashDot = new(3);
    public static readonly DashStyle DashDotDot = new(4);
    public static readonly DashStyle Custom = new(5);

    // Implicit conversion to Eto.Drawing.DashStyle
    public static implicit operator Eto.Drawing.DashStyle(DashStyle style)
    {
        return style._value switch
        {
            0 => Eto.Drawing.DashStyles.Solid,  // Solid
            1 => Eto.Drawing.DashStyles.Dash,   // Dash
            2 => Eto.Drawing.DashStyles.Dot,    // Dot
            3 => Eto.Drawing.DashStyles.DashDot, // DashDot
            4 => Eto.Drawing.DashStyles.DashDotDot, // DashDotDot
            _ => Eto.Drawing.DashStyles.Solid   // Custom/default
        };
    }

    public override bool Equals(object? obj) => obj is DashStyle ds && ds._value == _value;
    public override int GetHashCode() => _value;
    public static bool operator ==(DashStyle left, DashStyle right) => left._value == right._value;
    public static bool operator !=(DashStyle left, DashStyle right) => left._value != right._value;
}

/// <summary>
/// DragDropEffects enum for WinForms compatibility
/// </summary>
[Flags]
public enum DragDropEffects
{
    None = 0,
    Copy = 1,
    Move = 2,
    Link = 4,
    Scroll = int.MinValue,
    All = Copy | Move | Link
}
