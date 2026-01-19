using System;
using Eto.Forms;
using Eto.Drawing;

namespace flyworld.eto.component
{
    /// <summary>
    /// 自繪 CheckBox 元件，支援自訂文字顏色
    /// </summary>
    public class ColoredCheckBox : Drawable
    {
        private bool _checked;
        private string _text = "";
        private Color _textColor = Colors.White;
        private Font _font;

        public ColoredCheckBox()
        {
            Size = new Size(90, 18);
            _font = new Font(SystemFont.Default, 9);
        }

        public bool? Checked
        {
            get => _checked;
            set
            {
                bool newValue = value == true;
                if (_checked != newValue)
                {
                    _checked = newValue;
                    Invalidate();
                    OnCheckedChanged(EventArgs.Empty);
                }
            }
        }

        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? "";
                Invalidate();
            }
        }

        public new Color TextColor
        {
            get => _textColor;
            set
            {
                _textColor = value;
                Invalidate();
            }
        }

        public event EventHandler<EventArgs> CheckedChanged;

        protected virtual void OnCheckedChanged(EventArgs e)
        {
            CheckedChanged?.Invoke(this, e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            // 繪製勾選框背景
            g.FillRectangle(Colors.White, 0, 2, 14, 14);
            g.DrawRectangle(Colors.Gray, 0, 2, 14, 14);

            // 繪製勾選符號
            if (_checked)
            {
                using (var pen = new Pen(Colors.Black, 2))
                {
                    g.DrawLine(pen, 2, 9, 5, 13);
                    g.DrawLine(pen, 5, 13, 12, 5);
                }
            }

            // 繪製文字
            g.DrawText(_font, _textColor, 18, 1, _text);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Buttons == Eto.Forms.MouseButtons.Primary)
            {
                Checked = !_checked;
            }
        }
    }
}
