using System;
using Eto.Forms;
using Eto.Drawing;

namespace flyworld.eto.component
{
    #region Delegate Types

    /// <summary>
    /// MethodInvoker delegate for WinForms compatibility
    /// </summary>
    public delegate void MethodInvoker();

    // Event handler delegates
    public delegate void MouseEventHandler(object sender, Eto.Forms.MouseEventArgs e);
    public delegate void KeyEventHandler(object sender, Eto.Forms.KeyEventArgs e);
    public delegate void PaintEventHandler(object sender, Eto.Forms.PaintEventArgs e);
    public delegate void DragEventHandler(object sender, Eto.Forms.DragEventArgs e);
    public delegate void ScrollEventHandler(object sender, ScrollEventArgs e);
    public delegate void PreviewKeyDownEventHandler(object sender, PreviewKeyDownEventArgs e);
    public delegate void DrawItemEventHandler(object sender, DrawItemEventArgs e);
    public delegate void ItemCheckEventHandler(object sender, ItemCheckEventArgs e);
    public delegate void FormClosedEventHandler(object sender, FormClosedEventArgs e);
    public delegate void FormClosingEventHandler(object sender, FormClosingEventArgs e);

    #endregion

    #region EventArgs Classes

    /// <summary>
    /// FormClosedEventArgs for form close handling
    /// </summary>
    public class FormClosedEventArgs : EventArgs
    {
        public CloseReason CloseReason { get; }
        public FormClosedEventArgs(CloseReason reason) => CloseReason = reason;
    }

    /// <summary>
    /// FormClosingEventArgs for form closing handling
    /// </summary>
    public class FormClosingEventArgs : EventArgs
    {
        public bool Cancel { get; set; }
        public CloseReason CloseReason { get; }
        public FormClosingEventArgs(CloseReason reason) => CloseReason = reason;
    }

    /// <summary>
    /// HandledMouseEventArgs with Handled property
    /// </summary>
    public class HandledMouseEventArgs : Eto.Forms.MouseEventArgs
    {
        public HandledMouseEventArgs(Eto.Forms.MouseButtons buttons, Keys modifiers, PointF location, int delta = 0)
            : base(buttons, modifiers, location)
        {
            Delta = delta;
        }

        public new int Delta { get; }
        public new bool Handled { get; set; }
    }

    /// <summary>
    /// ScrollEventArgs for scroll events
    /// </summary>
    public class ScrollEventArgs : EventArgs
    {
        public int NewValue { get; set; }
        public int OldValue { get; set; }
        public ScrollEventType Type { get; set; }
        public ScrollOrientation ScrollOrientation { get; set; }

        public ScrollEventArgs(ScrollEventType type, int newValue)
        {
            Type = type;
            NewValue = newValue;
        }

        public ScrollEventArgs(ScrollEventType type, int oldValue, int newValue)
        {
            Type = type;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public ScrollEventArgs(ScrollEventType type, int newValue, ScrollOrientation orientation)
        {
            Type = type;
            NewValue = newValue;
            ScrollOrientation = orientation;
        }

        public ScrollEventArgs(ScrollEventType type, int oldValue, int newValue, ScrollOrientation orientation)
        {
            Type = type;
            OldValue = oldValue;
            NewValue = newValue;
            ScrollOrientation = orientation;
        }
    }

    /// <summary>
    /// PreviewKeyDownEventArgs for key preview
    /// </summary>
    public class PreviewKeyDownEventArgs : EventArgs
    {
        public Eto.Forms.Keys KeyCode { get; }
        public bool IsInputKey { get; set; }

        public PreviewKeyDownEventArgs(Eto.Forms.Keys keyCode)
        {
            KeyCode = keyCode;
        }
    }

    /// <summary>
    /// DrawItemEventArgs for custom drawing
    /// </summary>
    public class DrawItemEventArgs : EventArgs
    {
        public Eto.Drawing.Graphics Graphics { get; }
        public int Index { get; }
        public Eto.Drawing.Rectangle Bounds { get; }
        public DrawItemState State { get; }
        public Eto.Drawing.Font Font { get; }
        public Eto.Drawing.Color ForeColor { get; }
        public Eto.Drawing.Color BackColor { get; }

        public DrawItemEventArgs(Eto.Drawing.Graphics graphics, Eto.Drawing.Font font, Eto.Drawing.Rectangle rect, int index, DrawItemState state)
        {
            Graphics = graphics;
            Font = font;
            Bounds = rect;
            Index = index;
            State = state;
            ForeColor = Colors.Black;
            BackColor = Colors.White;
        }

        public DrawItemEventArgs(Eto.Drawing.Graphics graphics, Eto.Drawing.Font font, Eto.Drawing.Rectangle rect, int index, DrawItemState state, Eto.Drawing.Color foreColor, Eto.Drawing.Color backColor)
        {
            Graphics = graphics;
            Font = font;
            Bounds = rect;
            Index = index;
            State = state;
            ForeColor = foreColor;
            BackColor = backColor;
        }

        public virtual void DrawBackground()
        {
            Graphics?.FillRectangle(BackColor, Bounds);
        }

        public virtual void DrawFocusRectangle()
        {
            if ((State & DrawItemState.Focus) != 0)
            {
                Graphics?.DrawRectangle(Colors.Black, Bounds);
            }
        }
    }

    /// <summary>
    /// ItemCheckEventArgs for checkbox item check events
    /// </summary>
    public class ItemCheckEventArgs : EventArgs
    {
        public int Index { get; }
        public CheckState NewValue { get; set; }
        public CheckState CurrentValue { get; }

        public ItemCheckEventArgs(int index, CheckState newValue, CheckState currentValue)
        {
            Index = index;
            NewValue = newValue;
            CurrentValue = currentValue;
        }
    }

    /// <summary>
    /// ItemCheckedEventArgs for ListView item check
    /// </summary>
    public class ItemCheckedEventArgs : EventArgs
    {
        public object Item { get; }
        public ItemCheckedEventArgs(object item) => Item = item;
    }

    #endregion

    #region Enums

    public enum CloseReason
    {
        None,
        WindowsShutDown,
        MdiFormClosing,
        UserClosing,
        TaskManagerClosing,
        FormOwnerClosing,
        ApplicationExitCall
    }

    public enum CheckState
    {
        Unchecked,
        Checked,
        Indeterminate
    }

    [Flags]
    public enum DrawItemState
    {
        None = 0,
        Selected = 1,
        Grayed = 2,
        Disabled = 4,
        Checked = 8,
        Focus = 16,
        Default = 32,
        HotLight = 64,
        Inactive = 128,
        NoAccelerator = 256,
        NoFocusRect = 512,
        ComboBoxEdit = 4096
    }

    public enum ScrollEventType
    {
        SmallDecrement,
        SmallIncrement,
        LargeDecrement,
        LargeIncrement,
        ThumbPosition,
        ThumbTrack,
        First,
        Last,
        EndScroll
    }

    public enum ScrollOrientation
    {
        HorizontalScroll,
        VerticalScroll
    }

    #endregion
}
