using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using VibranceZ.Core;

namespace VibranceZ.UI
{
    // Paylaşımlı animasyon zamanlayıcısı (aktif animasyon yoksa durur)
    public static class AnimationManager
    {
        private static readonly Timer SharedTimer = new Timer { Interval = 16 };
        private static readonly List<Func<bool>> Callbacks = new List<Func<bool>>();

        static AnimationManager()
        {
            SharedTimer.Tick += OnTick;
        }

        public static void Register(Func<bool> tickCallback)
        {
            if (tickCallback == null) return;
            if (!Callbacks.Contains(tickCallback))
                Callbacks.Add(tickCallback);
            if (!SharedTimer.Enabled)
                SharedTimer.Start();
        }

        public static void Unregister(Func<bool> tickCallback)
        {
            if (tickCallback == null) return;
            Callbacks.Remove(tickCallback);
            if (Callbacks.Count == 0 && SharedTimer.Enabled)
                SharedTimer.Stop();
        }

        private static void OnTick(object sender, EventArgs e)
        {
            for (int i = Callbacks.Count - 1; i >= 0; i--)
            {
                bool keepAlive = false;
                try { keepAlive = Callbacks[i](); }
                catch { keepAlive = false; }

                if (!keepAlive)
                    Callbacks.RemoveAt(i);
            }

            if (Callbacks.Count == 0)
                SharedTimer.Stop();
        }
    }

    public enum FlatButtonStyle
    {
        Primary,
        Secondary,
        DangerGhost
    }

    // Açık kalan metin kutularını dışarı tıklanınca kapatmak için geçici mesaj filtresi
    internal sealed class OutsideClickFilter : IMessageFilter
    {
        private readonly Control _target;
        private readonly Action _onOutsideClick;

        public OutsideClickFilter(Control target, Action onOutsideClick)
        {
            _target = target;
            _onOutsideClick = onOutsideClick;
        }

        public bool PreFilterMessage(ref Message m)
        {
            // WM_LBUTTONDOWN (0x0201), WM_RBUTTONDOWN (0x0204), WM_NCLBUTTONDOWN (0x00A1)
            if (m.Msg == 0x0201 || m.Msg == 0x0204 || m.Msg == 0x00A1)
            {
                if (_target != null && !_target.IsDisposed && _target.IsHandleCreated && m.HWnd != _target.Handle)
                {
                    if (_onOutsideClick != null) _onOutsideClick();
                }
            }
            return false;
        }
    }

    // Koyu temalı özel açılır liste (ComboBox yerine)
    public class DarkDropdown : Control
    {
        private readonly List<string> _items = new List<string>();
        private int _selectedIndex = -1;
        private bool _hover;
        private ContextMenuStrip _menu;

        public event EventHandler SelectedIndexChanged;

        public List<string> Items { get { return _items; } }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                int next = (value >= 0 && value < _items.Count) ? value : -1;
                if (_selectedIndex != next)
                {
                    _selectedIndex = next;
                    Invalidate();
                    var h = SelectedIndexChanged;
                    if (h != null) h(this, EventArgs.Empty);
                }
                else
                {
                    Invalidate();
                }
            }
        }

        public string SelectedItem
        {
            get { return (_selectedIndex >= 0 && _selectedIndex < _items.Count) ? _items[_selectedIndex] : null; }
        }

        public DarkDropdown()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(220, 28);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            if (!Enabled || _items.Count == 0) return;

            if (_menu != null) { _menu.Dispose(); _menu = null; }
            _menu = new ContextMenuStrip { Renderer = new DarkToolStripRenderer(), ShowImageMargin = false, MinimumSize = new Size(Width, 0) };
            for (int i = 0; i < _items.Count; i++)
            {
                int idx = i;
                var item = new ToolStripMenuItem(_items[i]) { Checked = (i == _selectedIndex) };
                item.Click += (s, ev) => { SelectedIndex = idx; };
                _menu.Items.Add(item);
            }
            _menu.Show(this, new Point(0, Height + 2));
            base.OnClick(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _menu != null) { _menu.Dispose(); _menu = null; }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : Theme.CardBg);

            Color bg = Enabled ? Theme.InputBg : Theme.CardBg;
            Color border = !Enabled ? Theme.BorderSubtle : (_hover ? Theme.Accent : Theme.Border);
            Color fg = Enabled ? Theme.TextPrimary : Theme.TextMuted;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.RoundedRect(rect, 6f))
            {
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
                using (var p = new Pen(border, 1f)) g.DrawPath(p, path);
            }

            string text = SelectedItem ?? string.Empty;
            TextRenderer.DrawText(g, text, Theme.FontBody,
                new Rectangle(10, 0, Width - 34, Height), fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // Aşağı ok ikonu
            int ax = Width - 18;
            int ay = Height / 2 - 2;
            using (var p = new Pen(Enabled ? Theme.TextSecondary : Theme.TextMuted, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(p, ax, ay, ax + 4, ay + 4);
                g.DrawLine(p, ax + 4, ay + 4, ax + 8, ay);
            }
        }
    }

    // Ayar açıklaması için küçük (i) bilgi ikonu
    public class InfoBadge : Control
    {
        private bool _hover;

        public InfoBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(16, 16);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.CardBg);

            Color circleCol = _hover ? Theme.Accent : Theme.TextMuted;
            using (var p = new Pen(circleCol, 1.2f))
                g.DrawEllipse(p, 1.5f, 1.5f, Width - 3f, Height - 3f);

            using (var b = new SolidBrush(_hover ? Color.White : Theme.TextMuted))
            {
                // 'i' noktası ve gövdesi
                g.FillEllipse(b, 7.2f, 4.2f, 1.8f, 1.8f);
                g.FillRectangle(b, 7.3f, 7.2f, 1.6f, 4.6f);
            }
        }
    }

    public class DarkStepperBox : Control
    {
        private int _value;
        private int _minimum;
        private int _maximum = 59;
        private bool _hoverMinus;
        private bool _hoverPlus;
        private bool _hoverBody;
        private TextBox _inlineEdit;
        private OutsideClickFilter _clickFilter;

        public event EventHandler ValueChanged;

        public int Minimum
        {
            get { return _minimum; }
            set { _minimum = value; if (_value < _minimum) Value = _minimum; }
        }

        public int Maximum
        {
            get { return _maximum; }
            set { _maximum = value; if (_value > _maximum) Value = _maximum; }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int clamped = Math.Max(_minimum, Math.Min(_maximum, value));
                if (_value != clamped)
                {
                    _value = clamped;
                    Invalidate();
                    var h = ValueChanged;
                    if (h != null) h(this, EventArgs.Empty);
                }
                else
                {
                    Invalidate();
                }
            }
        }

        public DarkStepperBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(74, 28);
            Cursor = Cursors.Hand;
        }

        private Rectangle MinusRect { get { return new Rectangle(0, 0, 20, Height); } }
        private Rectangle PlusRect { get { return new Rectangle(Width - 20, 0, 20, Height); } }
        private Rectangle CenterRect { get { return new Rectangle(20, 0, Width - 40, Height); } }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hm = MinusRect.Contains(e.Location);
            bool hp = PlusRect.Contains(e.Location);
            bool hb = ClientRectangle.Contains(e.Location);
            if (hm != _hoverMinus || hp != _hoverPlus || hb != _hoverBody)
            {
                _hoverMinus = hm;
                _hoverPlus = hp;
                _hoverBody = hb;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverMinus = false;
            _hoverPlus = false;
            _hoverBody = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Enabled)
            {
                if (MinusRect.Contains(e.Location))
                    Value = _value - 1;
                else if (PlusRect.Contains(e.Location))
                    Value = _value + 1;
                else if (CenterRect.Contains(e.Location))
                    OpenInlineEdit();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (Enabled)
                Value = _value + (e.Delta > 0 ? 1 : -1);
            var hme = e as HandledMouseEventArgs;
            if (hme != null) hme.Handled = true;
            base.OnMouseWheel(e);
        }

        private void OpenInlineEdit()
        {
            if (_inlineEdit != null) return;
            _inlineEdit = new TextBox
            {
                Bounds = new Rectangle(21, 5, Width - 42, Height - 10),
                BorderStyle = BorderStyle.None,
                BackColor = Theme.InputBg,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontMono,
                TextAlign = HorizontalAlignment.Center,
                Text = _value.ToString("00")
            };
            _inlineEdit.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { CommitEdit(true); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Escape) { CommitEdit(false); e.SuppressKeyPress = true; }
            };
            _inlineEdit.LostFocus += (s, e) => CommitEdit(true);
            Controls.Add(_inlineEdit);
            _clickFilter = new OutsideClickFilter(_inlineEdit, () => CommitEdit(true));
            Application.AddMessageFilter(_clickFilter);
            _inlineEdit.Focus();
            _inlineEdit.SelectAll();
        }

        private void CommitEdit(bool apply)
        {
            if (_clickFilter != null)
            {
                Application.RemoveMessageFilter(_clickFilter);
                _clickFilter = null;
            }
            if (_inlineEdit == null) return;
            var tb = _inlineEdit;
            _inlineEdit = null;
            if (apply)
            {
                int parsed;
                if (int.TryParse(tb.Text.Trim(), out parsed))
                    Value = parsed;
            }
            Controls.Remove(tb);
            tb.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : Theme.CardBg);

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.RoundedRect(rect, 6f))
            {
                using (var b = new SolidBrush(Theme.InputBg)) g.FillPath(b, path);
                using (var p = new Pen(_hoverBody ? Theme.Accent : Theme.Border, 1f)) g.DrawPath(p, path);
            }

            // İnce ayraç çizgileri
            using (var sep = new Pen(Theme.BorderSubtle, 1f))
            {
                g.DrawLine(sep, 20, 5, 20, Height - 5);
                g.DrawLine(sep, Width - 20, 5, Width - 20, Height - 5);
            }

            TextRenderer.DrawText(g, "-", Theme.FontBodyBold, MinusRect,
                _hoverMinus ? Color.White : Theme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, _value.ToString("00"), Theme.FontMono, CenterRect,
                Theme.TextPrimary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, "+", Theme.FontBodyBold, PlusRect,
                _hoverPlus ? Color.White : Theme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    public class WheelScrollPanel : Panel
    {
        private int _scrollOffset;

        public WheelScrollPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoScroll = false;
        }

        public void ResetScroll()
        {
            if (_scrollOffset == 0) return;
            ApplyDelta(-_scrollOffset);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int maxScroll = GetMaxScroll();
            if (maxScroll > 0)
            {
                int target = Math.Max(0, Math.Min(maxScroll, _scrollOffset - (e.Delta / 3)));
                int delta = target - _scrollOffset;
                if (delta != 0)
                    ApplyDelta(delta);
            }
            var hme = e as HandledMouseEventArgs;
            if (hme != null) hme.Handled = true;
            base.OnMouseWheel(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            int maxScroll = GetMaxScroll();
            if (_scrollOffset > maxScroll)
                ApplyDelta(maxScroll - _scrollOffset);
        }

        private int GetMaxScroll()
        {
            int maxBottom = 0;
            foreach (Control c in Controls)
            {
                if (!c.Visible) continue;
                int originalBottom = c.Bottom + _scrollOffset;
                if (originalBottom > maxBottom)
                    maxBottom = originalBottom;
            }
            return Math.Max(0, maxBottom + 24 - ClientSize.Height);
        }

        private void ApplyDelta(int delta)
        {
            SuspendLayout();
            _scrollOffset += delta;
            foreach (Control c in Controls)
                c.Top -= delta;
            ResumeLayout(true);
        }
    }

    public class RoundedCardPanel : Panel
    {
        public float Radius { get; set; }
        public bool HoverHighlight { get; set; }
        private bool _hover;

        public RoundedCardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Radius = Theme.CardRadius;
            BackColor = Theme.CardBg;
            Padding = new Padding(Theme.CardPadding);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            if (HoverHighlight) { _hover = true; Invalidate(); }
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hover) { _hover = false; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.ContentBg);

            Color fill = (_hover && HoverHighlight) ? Theme.CardHoverBg : BackColor;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.RoundedRect(rect, Radius))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                using (var p = new Pen(Theme.Border, 1f)) g.DrawPath(p, path);
            }
        }
    }

    public class FlatButton : Control
    {
        private bool _hover;
        private bool _pressed;
        private float _hoverProgress;
        private readonly Func<bool> _animTick;

        public FlatButtonStyle ButtonStyle { get; set; }

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            ButtonStyle = FlatButtonStyle.Secondary;
            Font = Theme.FontBodyBold;
            Cursor = Cursors.Hand;
            Size = new Size(100, 32);
            _animTick = StepAnimation;
        }

        private bool StepAnimation()
        {
            float target = _hover ? 1f : 0f;
            float step = 0.14f; // ~150ms geçiş
            if (Math.Abs(_hoverProgress - target) <= step)
            {
                _hoverProgress = target;
                Invalidate();
                return false;
            }
            _hoverProgress += (_hoverProgress < target) ? step : -step;
            Invalidate();
            return true;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; AnimationManager.Register(_animTick); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; AnimationManager.Register(_animTick); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) AnimationManager.Unregister(_animTick);
            base.Dispose(disposing);
        }

        private Color ResolveOpaqueParentBg()
        {
            Control p = Parent;
            while (p != null)
            {
                if (p.BackColor.A == 255)
                    return p.BackColor;
                p = p.Parent;
            }
            return Theme.CardBg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(ResolveOpaqueParentBg());

            Color bg, border, fg;

            if (!Enabled)
            {
                bg = Theme.CardBg;
                border = Theme.BorderSubtle;
                fg = Theme.TextMuted;
            }
            else if (ButtonStyle == FlatButtonStyle.Primary)
            {
                bg = _pressed ? Theme.AccentPressed : Theme.LerpColor(Theme.Accent, Theme.AccentHover, _hoverProgress);
                border = bg;
                fg = Color.White;
            }
            else if (ButtonStyle == FlatButtonStyle.DangerGhost)
            {
                Color baseBg = Theme.ButtonSecondaryBg;
                Color hovBg = Color.FromArgb(48, Theme.DangerRed.R, Theme.DangerRed.G, Theme.DangerRed.B);
                bg = _pressed ? Color.FromArgb(80, Theme.DangerRed.R, Theme.DangerRed.G, Theme.DangerRed.B) : Theme.LerpColor(baseBg, hovBg, _hoverProgress);
                border = Theme.LerpColor(Theme.Border, Theme.DangerRed, _hoverProgress);
                fg = Theme.LerpColor(Theme.TextSecondary, Theme.DangerRed, _hoverProgress);
            }
            else
            {
                bg = _pressed ? Theme.SelectedBg : Theme.LerpColor(Theme.ButtonSecondaryBg, Theme.ButtonSecondaryHoverBg, _hoverProgress);
                border = Theme.LerpColor(Theme.Border, Color.FromArgb(105, 112, 128), _hoverProgress);
                fg = Theme.TextPrimary;
            }

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.RoundedRect(rect, Theme.ButtonRadius))
            {
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
                using (var p = new Pen(border, 1f)) g.DrawPath(p, path);
            }

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    public class FlatToggle : Control
    {
        private bool _checked;
        private bool _hover;
        private float _knobPos;
        private readonly Func<bool> _animTick;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    if (IsHandleCreated)
                        AnimationManager.Register(_animTick);
                    else
                        _knobPos = _checked ? 1f : 0f;
                    Invalidate();
                    var h = CheckedChanged;
                    if (h != null) h(this, EventArgs.Empty);
                }
            }
        }

        public FlatToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(42, 22);
            Cursor = Cursors.Hand;
            _animTick = StepAnimation;
        }

        private bool StepAnimation()
        {
            float target = _checked ? 1f : 0f;
            float step = 0.16f;
            if (Math.Abs(_knobPos - target) <= step)
            {
                _knobPos = target;
                Invalidate();
                return false;
            }
            _knobPos += (_knobPos < target) ? step : -step;
            Invalidate();
            return true;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { if (Enabled) Checked = !Checked; base.OnClick(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) AnimationManager.Unregister(_animTick);
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.CardBg);

            Color offBg = _hover ? Color.FromArgb(138, 142, 152) : Color.FromArgb(114, 118, 125);
            Color onBg = _hover ? Theme.AccentHover : Theme.ActiveGreen;
            Color trackColor = Enabled ? Theme.LerpColor(offBg, onBg, _knobPos) : Theme.BorderSubtle;

            var rect = new RectangleF(1f, 1f, Width - 2f, Height - 2f);
            using (var path = Theme.RoundedRect(rect, rect.Height / 2f))
            using (var b = new SolidBrush(trackColor))
            {
                g.FillPath(b, path);
            }

            float knobSize = Height - 6f;
            float minX = 3f;
            float maxX = Width - knobSize - 3f;
            float knobX = minX + (maxX - minX) * _knobPos;

            using (var b = new SolidBrush(Enabled ? Color.White : Theme.TextMuted))
                g.FillEllipse(b, knobX, 3f, knobSize, knobSize);
        }
    }

    public class SettingSliderRow : Control
    {
        private int _minimum = 0;
        private int _maximum = 400;
        private int _value = 100;
        private int _defaultValue = 100;
        private string _title = string.Empty;
        private string _unitSuffix = string.Empty;
        private string _unsupportedReason = string.Empty;
        private bool _emptyDisplay;
        private bool _dragging;
        private bool _hoverTrack;
        private bool _hoverReset;
        private bool _hoverBadge;
        private TextBox _inlineEdit;
        private OutsideClickFilter _clickFilter;

        public event EventHandler ValueChanged;

        public string Title
        {
            get { return _title; }
            set { _title = value ?? string.Empty; Invalidate(); }
        }

        public string UnitSuffix
        {
            get { return _unitSuffix; }
            set { _unitSuffix = value ?? string.Empty; Invalidate(); }
        }

        public string UnsupportedReason
        {
            get { return _unsupportedReason; }
            set { _unsupportedReason = value ?? string.Empty; Invalidate(); }
        }

        public int Minimum
        {
            get { return _minimum; }
            set { _minimum = value; if (_value < _minimum) Value = _minimum; Invalidate(); }
        }

        public int Maximum
        {
            get { return _maximum; }
            set { _maximum = value; if (_value > _maximum) Value = _maximum; Invalidate(); }
        }

        public int DefaultValue
        {
            get { return _defaultValue; }
            set { _defaultValue = Math.Max(_minimum, Math.Min(_maximum, value)); Invalidate(); }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                _emptyDisplay = false;
                int clamped = Math.Max(_minimum, Math.Min(_maximum, value));
                if (_value != clamped)
                {
                    _value = clamped;
                    Invalidate();
                    var h = ValueChanged;
                    if (h != null) h(this, EventArgs.Empty);
                }
                else
                {
                    Invalidate();
                }
            }
        }

        public SettingSliderRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(420, 34);
            Cursor = Cursors.Hand;
        }

        public void SetEmptyDisplay()
        {
            _emptyDisplay = true;
            Invalidate();
        }

        private Rectangle GetTrackRect()
        {
            int labelW = 128;
            int rightArea = 96; // Değer kutusu + sıfırlama ikonu
            int trackW = Math.Max(60, Width - labelW - rightArea - 8);
            return new Rectangle(labelW, (Height - 6) / 2, trackW, 6);
        }

        private Rectangle GetBadgeRect()
        {
            return new Rectangle(Width - 88, (Height - 24) / 2, 58, 24);
        }

        private Rectangle GetResetRect()
        {
            return new Rectangle(Width - 24, (Height - 24) / 2, 22, 24);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!Enabled || !string.IsNullOrEmpty(_unsupportedReason))
                return;

            if (e.Button == MouseButtons.Left)
            {
                if (GetResetRect().Contains(e.Location))
                {
                    Value = _defaultValue;
                    return;
                }

                if (GetBadgeRect().Contains(e.Location))
                {
                    OpenInlineEditor();
                    return;
                }

                var tr = GetTrackRect();
                if (e.X >= tr.Left - 8 && e.X <= tr.Right + 8)
                {
                    _dragging = true;
                    UpdateValueFromMouse(e.X);
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            if (!Enabled || !string.IsNullOrEmpty(_unsupportedReason))
                return;

            Point pt = PointToClient(Cursor.Position);
            var tr = GetTrackRect();
            if (pt.X >= tr.Left - 8 && pt.X <= tr.Right + 8)
            {
                Value = _defaultValue;
            }
            base.OnDoubleClick(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hTrack = GetTrackRect().Contains(e.X, Height / 2) || _dragging;
            bool hBadge = GetBadgeRect().Contains(e.Location);
            bool hReset = GetResetRect().Contains(e.Location);

            if (hTrack != _hoverTrack || hBadge != _hoverBadge || hReset != _hoverReset)
            {
                _hoverTrack = hTrack;
                _hoverBadge = hBadge;
                _hoverReset = hReset;
                Invalidate();
            }

            if (_dragging && Enabled)
                UpdateValueFromMouse(e.X);

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e) { _dragging = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnMouseLeave(EventArgs e) { _hoverTrack = false; _hoverBadge = false; _hoverReset = false; Invalidate(); base.OnMouseLeave(e); }

        private void UpdateValueFromMouse(int mouseX)
        {
            var tr = GetTrackRect();
            float ratio = (float)(mouseX - tr.Left) / Math.Max(1, tr.Width);
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;
            Value = _minimum + (int)Math.Round(ratio * (_maximum - _minimum));
        }

        private void OpenInlineEditor()
        {
            if (_inlineEdit != null) return;
            var br = GetBadgeRect();
            _inlineEdit = new TextBox
            {
                Bounds = new Rectangle(br.X + 2, br.Y + 3, br.Width - 4, br.Height - 6),
                BorderStyle = BorderStyle.None,
                BackColor = Theme.InputBg,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontMono,
                TextAlign = HorizontalAlignment.Center,
                Text = _value.ToString()
            };
            _inlineEdit.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    CommitInlineEditor(true);
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    CommitInlineEditor(false);
                    e.SuppressKeyPress = true;
                }
            };
            _inlineEdit.LostFocus += (s, e) => CommitInlineEditor(true);
            Controls.Add(_inlineEdit);
            _clickFilter = new OutsideClickFilter(_inlineEdit, () => CommitInlineEditor(true));
            Application.AddMessageFilter(_clickFilter);
            _inlineEdit.Focus();
            _inlineEdit.SelectAll();
        }

        private void CommitInlineEditor(bool apply)
        {
            if (_clickFilter != null)
            {
                Application.RemoveMessageFilter(_clickFilter);
                _clickFilter = null;
            }
            if (_inlineEdit == null) return;
            var tb = _inlineEdit;
            _inlineEdit = null;
            if (apply)
            {
                int parsed;
                if (int.TryParse(tb.Text.Trim(), out parsed))
                    Value = parsed;
            }
            Controls.Remove(tb);
            tb.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : Theme.CardBg);

            bool hasUnsupported = !string.IsNullOrEmpty(_unsupportedReason);
            Color titleColor = (Enabled && !hasUnsupported) ? Theme.TextSecondary : Theme.TextMuted;
            TextRenderer.DrawText(g, _title, Theme.FontBody,
                new Rectangle(0, 0, 124, Height), titleColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            var tr = GetTrackRect();
            if (hasUnsupported)
            {
                TextRenderer.DrawText(g, _unsupportedReason, Theme.FontSmall,
                    new Rectangle(tr.X, 0, Width - tr.X, Height), Theme.TextMuted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }

            int cy = Height / 2;
            using (var bgPath = Theme.RoundedRect(new RectangleF(tr.X, cy - 2.5f, tr.Width, 5f), 2.5f))
            using (var bgBrush = new SolidBrush(Theme.InputBg))
            {
                g.FillPath(bgBrush, bgPath);
            }

            float ratio = (_maximum > _minimum) ? (float)(_value - _minimum) / (_maximum - _minimum) : 0f;
            float fillW = Math.Max(0f, tr.Width * ratio);

            if (Enabled && fillW > 1f)
            {
                using (var fillPath = Theme.RoundedRect(new RectangleF(tr.X, cy - 2.5f, fillW, 5f), 2.5f))
                using (var fillBrush = new SolidBrush(Theme.Accent))
                {
                    g.FillPath(fillBrush, fillPath);
                }
            }

            // Hover'da büyüyen topuz
            float knobR = (_hoverTrack || _dragging) ? 7.5f : 5.5f;
            float knobX = tr.X + fillW - knobR;
            using (var kb = new SolidBrush(Enabled ? Color.White : Theme.TextMuted))
                g.FillEllipse(kb, knobX, cy - knobR, knobR * 2f, knobR * 2f);

            // Değer kutusu (tıklanabilir)
            var br = GetBadgeRect();
            using (var badgePath = Theme.RoundedRect(new RectangleF(br.X, br.Y, br.Width, br.Height), 5f))
            {
                using (var bb = new SolidBrush(Theme.InputBg)) g.FillPath(bb, badgePath);
                using (var bp = new Pen(_hoverBadge ? Theme.Accent : Theme.Border, 1f)) g.DrawPath(bp, badgePath);
            }

            string valStr = _emptyDisplay ? "--" : (_value.ToString() + _unitSuffix);
            TextRenderer.DrawText(g, valStr, Theme.FontMono, br,
                Enabled ? Theme.TextPrimary : Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // Sıfırla ikonu (↺)
            var rr = GetResetRect();
            Color resetColor = !Enabled ? Theme.TextMuted : (_hoverReset ? Theme.TextPrimary : Theme.TextMuted);
            using (var p = new Pen(resetColor, 1.5f))
            {
                float rcx = rr.X + rr.Width / 2f;
                float rcy = rr.Y + rr.Height / 2f;
                g.DrawArc(p, rcx - 5f, rcy - 5f, 10f, 10f, 35, 275);
                g.DrawLine(p, rcx - 5f, rcy - 2f, rcx - 5f, rcy - 6f);
                g.DrawLine(p, rcx - 5f, rcy - 2f, rcx - 1.5f, rcy - 2f);
            }
        }
    }

    public enum NavIconKind
    {
        General,
        Profiles,
        Preview,
        Settings,
        About
    }

    // Sol navigasyon rayı ikonu
    public class NavRailButton : Control
    {
        private bool _active;
        private bool _hover;
        private float _anim;
        private readonly Func<bool> _animTick;

        public NavIconKind IconKind { get; set; }

        public bool Active
        {
            get { return _active; }
            set
            {
                if (_active != value)
                {
                    _active = value;
                    if (IsHandleCreated) AnimationManager.Register(_animTick);
                    else _anim = _active ? 1f : 0f;
                    Invalidate();
                }
            }
        }

        public NavRailButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(Theme.RailWidth, 52);
            Cursor = Cursors.Hand;
            _animTick = StepAnimation;
        }

        private bool StepAnimation()
        {
            float target = (_active || _hover) ? 1f : 0f;
            float step = 0.16f;
            if (Math.Abs(_anim - target) <= step)
            {
                _anim = target;
                Invalidate();
                return false;
            }
            _anim += (_anim < target) ? step : -step;
            Invalidate();
            return true;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; AnimationManager.Register(_animTick); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; AnimationManager.Register(_animTick); base.OnMouseLeave(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) AnimationManager.Unregister(_animTick);
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.RailBg);

            // Solda beyaz hap göstergesi
            if (_active || _hover)
            {
                float pillH = _active ? 32f : 14f;
                float pillY = (Height - pillH) / 2f;
                using (var pillPath = Theme.RoundedRect(new RectangleF(0f, pillY, 4f, pillH), 2f))
                using (var pb = new SolidBrush(Color.White))
                {
                    g.FillPath(pb, pillPath);
                }
            }

            float boxSize = 44f;
            float bx = (Width - boxSize) / 2f;
            float by = (Height - boxSize) / 2f;
            float radius = 22f - (_anim * 10f); // 22 (daire) -> 12 (yuvarlak kare)
            Color bg = _active ? Theme.Accent : Theme.LerpColor(Theme.ContentBg, Theme.Accent, _anim * 0.85f);

            using (var path = Theme.RoundedRect(new RectangleF(bx, by, boxSize, boxSize), radius))
            using (var b = new SolidBrush(bg))
            {
                g.FillPath(b, path);
            }

            Color iconColor = (_active || _hover) ? Color.White : Theme.TextSecondary;
            DrawVectorIcon(g, IconKind, Width / 2f, Height / 2f, iconColor);
        }

        private static void DrawVectorIcon(Graphics g, NavIconKind kind, float cx, float cy, Color color)
        {
            using (var p = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                switch (kind)
                {
                    case NavIconKind.General:
                        g.DrawLine(p, cx - 8, cy - 1, cx, cy - 8);
                        g.DrawLine(p, cx, cy - 8, cx + 8, cy - 1);
                        g.DrawRectangle(p, cx - 5.5f, cy - 1, 11, 8);
                        break;
                    case NavIconKind.Profiles:
                        g.DrawRectangle(p, cx - 8, cy - 7, 7, 6);
                        g.DrawRectangle(p, cx + 1, cy - 7, 7, 6);
                        g.DrawRectangle(p, cx - 8, cy + 1, 7, 6);
                        g.DrawRectangle(p, cx + 1, cy + 1, 7, 6);
                        break;
                    case NavIconKind.Preview:
                        g.DrawEllipse(p, cx - 7, cy - 7, 14, 14);
                        g.DrawEllipse(p, cx - 2.5f, cy - 2.5f, 5, 5);
                        break;
                    case NavIconKind.Settings:
                        g.DrawLine(p, cx - 8, cy - 5, cx + 8, cy - 5);
                        g.DrawLine(p, cx - 8, cy, cx + 8, cy);
                        g.DrawLine(p, cx - 8, cy + 5, cx + 8, cy + 5);
                        using (var b = new SolidBrush(color))
                        {
                            g.FillEllipse(b, cx - 4, cy - 7.5f, 5, 5);
                            g.FillEllipse(b, cx + 1, cy - 2.5f, 5, 5);
                            g.FillEllipse(b, cx - 5, cy + 2.5f, 5, 5);
                        }
                        break;
                    case NavIconKind.About:
                        g.DrawEllipse(p, cx - 8, cy - 8, 16, 16);
                        g.DrawLine(p, cx, cy - 1, cx, cy + 4.5f);
                        using (var b = new SolidBrush(color))
                            g.FillEllipse(b, cx - 1.2f, cy - 5f, 2.4f, 2.4f);
                        break;
                }
            }
        }
    }

    // Canlı renk önizleme şeridi (spektrum, gri tonlama, ten rengi)
    public class ColorPreviewStrip : Control
    {
        private int _saturation = 100;
        private int _filterSaturation = 100;
        private int _contrast = 100;
        private int _brightness = 0;
        private int _gamma = 100;
        private int _colorTemp = 6500;

        public ColorPreviewStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(440, 62);
        }

        public void UpdateValues(int saturation, int contrast, int brightness, int gamma, int colorTemp, int filterSaturation = 100)
        {
            _saturation = saturation;
            _filterSaturation = filterSaturation;
            _contrast = contrast;
            _brightness = brightness;
            _gamma = gamma;
            _colorTemp = colorTemp;
            Invalidate();
        }

        private Color TransformColor(Color baseCol)
        {
            double satFactor = Math.Max(0.0, (_saturation / 100.0) * (_filterSaturation / 100.0));
            double conFactor = Math.Max(0.2, _contrast / 100.0);
            double briOffset = _brightness / 200.0;
            double gamExp = 1.0 / Math.Max(0.50, Math.Min(2.50, _gamma / 100.0));

            double tempShift = (_colorTemp - 6500.0) / 3500.0;
            double rMult = 1.0 - tempShift * 0.14;
            double bMult = 1.0 + tempShift * 0.16;

            double r = baseCol.R / 255.0;
            double g = baseCol.G / 255.0;
            double b = baseCol.B / 255.0;

            double lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            r = lum + (r - lum) * satFactor;
            g = lum + (g - lum) * satFactor;
            b = lum + (b - lum) * satFactor;

            r = ((r - 0.5) * conFactor + 0.5 + briOffset) * rMult;
            g = ((g - 0.5) * conFactor + 0.5 + briOffset);
            b = ((b - 0.5) * conFactor + 0.5 + briOffset) * bMult;

            r = Math.Pow(Math.Max(0.0, Math.Min(1.0, r)), gamExp);
            g = Math.Pow(Math.Max(0.0, Math.Min(1.0, g)), gamExp);
            b = Math.Pow(Math.Max(0.0, Math.Min(1.0, b)), gamExp);

            return Color.FromArgb(
                (int)Math.Round(r * 255.0),
                (int)Math.Round(g * 255.0),
                (int)Math.Round(b * 255.0));
        }

        private static Color HslToColor(double h, double s, double l)
        {
            double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
            double x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
            double m = l - c / 2.0;
            double r = 0, g = 0, b = 0;

            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }

            return Color.FromArgb(
                (int)Math.Round((r + m) * 255.0),
                (int)Math.Round((g + m) * 255.0),
                (int)Math.Round((b + m) * 255.0));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.CardBg);

            int w = Math.Max(40, Width - 2);
            int halfH = Math.Max(14, (Height - 6) / 2);

            // Üst şerit: Tam spektrum
            int steps = Math.Min(w, 120);
            float segW = (float)w / steps;
            for (int i = 0; i < steps; i++)
            {
                double hue = (i / (double)steps) * 360.0;
                Color col = TransformColor(HslToColor(hue, 0.78, 0.52));
                using (var b = new SolidBrush(col))
                    g.FillRectangle(b, 1 + i * segW, 1, segW + 1f, halfH);
            }

            // Alt sol: Gri tonlama (8 basamak) | Alt sağ: Ten renkleri (6 ton)
            int grayW = w / 2 - 3;
            int skinX = w / 2 + 3;
            int skinW = w - skinX + 1;

            int graySteps = 8;
            float gSegW = (float)grayW / graySteps;
            for (int i = 0; i < graySteps; i++)
            {
                int v = (int)Math.Round((i / (double)(graySteps - 1)) * 255.0);
                Color col = TransformColor(Color.FromArgb(v, v, v));
                using (var b = new SolidBrush(col))
                    g.FillRectangle(b, 1 + i * gSegW, halfH + 4, gSegW + 1f, halfH);
            }

            Color[] skinTones =
            {
                Color.FromArgb(245, 214, 193),
                Color.FromArgb(232, 186, 155),
                Color.FromArgb(207, 150, 114),
                Color.FromArgb(174, 118, 84),
                Color.FromArgb(132, 84, 58),
                Color.FromArgb(88, 54, 38)
            };
            float sSegW = (float)skinW / skinTones.Length;
            for (int i = 0; i < skinTones.Length; i++)
            {
                Color col = TransformColor(skinTones[i]);
                using (var b = new SolidBrush(col))
                    g.FillRectangle(b, skinX + i * sSegW, halfH + 4, sSegW + 1f, halfH);
            }

            using (var p = new Pen(Theme.Border, 1f))
            {
                g.DrawRectangle(p, 0, 0, w + 1, halfH + 1);
                g.DrawRectangle(p, 0, halfH + 3, grayW + 1, halfH + 1);
                g.DrawRectangle(p, skinX - 1, halfH + 3, skinW + 1, halfH + 1);
            }
        }
    }

    // Sağ alt köşe bildirim kutusu
    public sealed class ToastNotification : Control
    {
        private string _message = string.Empty;
        private Color _accentColor = Theme.ActiveGreen;
        private readonly Timer _hideTimer;

        public ToastNotification()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(260, 38);
            Visible = false;
            _hideTimer = new Timer { Interval = 2400 };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                Visible = false;
            };
        }

        public void ShowMessage(string message, Color accentColor)
        {
            _message = message ?? string.Empty;
            _accentColor = accentColor;
            _hideTimer.Stop();
            Visible = true;
            BringToFront();
            Invalidate();
            _hideTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hideTimer.Stop();
                _hideTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : Theme.ContentBg);

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.RoundedRect(rect, 8f))
            {
                using (var b = new SolidBrush(Theme.RailBg)) g.FillPath(b, path);
                using (var p = new Pen(_accentColor, 1.2f)) g.DrawPath(p, path);
            }

            using (var dotB = new SolidBrush(_accentColor))
                g.FillEllipse(dotB, 12, (Height - 8) / 2, 8, 8);

            TextRenderer.DrawText(g, _message, Theme.FontBody,
                new Rectangle(28, 0, Width - 36, Height), Theme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // Tepsi ve sağ tık menüleri için koyu tema çizici
    public sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var b = new SolidBrush(Theme.PanelBg))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using (var b = new SolidBrush(Theme.PanelBg))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var p = new Pen(Theme.Border, 1f))
                e.Graphics.DrawRectangle(p, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = (e.Item != null && !e.Item.Enabled)
                ? Theme.TextMuted
                : ((e.Item != null && e.Item.Selected) ? Color.White : Theme.TextSecondary);
            base.OnRenderArrow(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var rect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
            if (e.Item.Selected && e.Item.Enabled)
            {
                using (var path = Theme.RoundedRect(rect, 5f))
                using (var b = new SolidBrush(Theme.Accent))
                    e.Graphics.FillPath(b, path);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.TextColor = !e.Item.Enabled ? Theme.TextMuted : (e.Item.Selected ? Color.White : Theme.TextPrimary);
            e.TextFont = Theme.FontBody;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var p = new Pen(Theme.BorderSubtle, 1f))
                e.Graphics.DrawLine(p, 10, y, e.Item.Width - 10, y);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var r = e.ImageRectangle;
            using (var b = new SolidBrush(Theme.ActiveGreen))
                e.Graphics.FillEllipse(b, r.X + 4, r.Y + 4, 8, 8);
        }
    }

    // Ortadan bölünmüş, sürüklenebilir Önce / Sonra canlı renk karşılaştırma kontrolü
    public class BeforeAfterCompareControl : Control
    {
        private Bitmap _sourceImage;
        private Bitmap _beforeCache;
        private Bitmap _afterCache;
        private bool _beforeDirty = true;
        private bool _afterDirty = true;

        private float _splitRatio = 0.5f;
        private bool _dragging;

        private int _satPercent = 100;
        private int _filterSatPercent = 100;
        private int _screenSatPercent = 100;
        private int _screenFilterSatPercent = 100;
        private int _contrast = 100;
        private int _brightness = 0;
        private int _gamma = 100;
        private int _colorTemp = 6500;

        public string BeforeText { get; set; }
        public string AfterText { get; set; }

        public BeforeAfterCompareControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(608, 310);
            Cursor = Cursors.SizeWE;
            BeforeText = "BEFORE";
            AfterText = "AFTER";
        }

        public void SetImage(Image image)
        {
            if (_sourceImage != null)
            {
                _sourceImage.Dispose();
                _sourceImage = null;
            }
            if (image != null)
                _sourceImage = new Bitmap(image);

            _baseDirty = true;
            _beforeDirty = true;
            _afterDirty = true;
            Invalidate();
        }

        public void UpdateValues(int satPercent, int contrast, int brightness, int gamma, int colorTemp,
            int filterSatPercent = 100, int screenSatPercent = 100, int screenFilterSatPercent = 100)
        {
            bool beforeChanged = _screenSatPercent != screenSatPercent || _screenFilterSatPercent != screenFilterSatPercent;
            bool afterChanged = beforeChanged ||
                _satPercent != satPercent ||
                _filterSatPercent != filterSatPercent ||
                _contrast != contrast ||
                _brightness != brightness ||
                _gamma != gamma ||
                _colorTemp != colorTemp;

            if (!afterChanged)
                return;

            _satPercent = satPercent;
            _filterSatPercent = filterSatPercent;
            _screenSatPercent = screenSatPercent;
            _screenFilterSatPercent = screenFilterSatPercent;
            _contrast = contrast;
            _brightness = brightness;
            _gamma = gamma;
            _colorTemp = colorTemp;

            if (beforeChanged)
                _baseDirty = true;
            _beforeDirty = true;
            _afterDirty = true;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _baseDirty = true;
            _beforeDirty = true;
            _afterDirty = true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                Capture = true;
                UpdateSplitFromX(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
                UpdateSplitFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragging)
            {
                _dragging = false;
                Capture = false;
            }
        }

        private void UpdateSplitFromX(int x)
        {
            if (Width <= 0) return;
            float next = Math.Max(0.03f, Math.Min(0.97f, x / (float)Width));
            if (Math.Abs(next - _splitRatio) > 0.001f)
            {
                _splitRatio = next;
                Invalidate();
            }
        }

        private Bitmap _baseScaledCache;
        private bool _baseDirty = true;

        private void EnsureCaches()
        {
            int w = Width;
            int h = Height;
            if (w <= 4 || h <= 4 || _sourceImage == null)
                return;

            if (_baseDirty || _baseScaledCache == null || _baseScaledCache.Width != w || _baseScaledCache.Height != h)
            {
                if (_baseScaledCache != null) _baseScaledCache.Dispose();
                _baseScaledCache = new Bitmap(w, h);

                using (var g = Graphics.FromImage(_baseScaledCache))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.Clear(Theme.InputBg);

                    float scale = Math.Max(w / (float)_sourceImage.Width, h / (float)_sourceImage.Height);
                    float drawW = _sourceImage.Width * scale;
                    float drawH = _sourceImage.Height * scale;
                    float dx = (w - drawW) * 0.5f;
                    float dy = (h - drawH) * 0.5f;
                    g.DrawImage(_sourceImage, dx, dy, drawW, drawH);
                }
                _baseDirty = false;
                _beforeDirty = true;
                _afterDirty = true;
            }

            float screenLinear = Math.Max(0f, (_screenSatPercent / 100.0f) * (_screenFilterSatPercent / 100.0f));

            if (_beforeDirty || _beforeCache == null || _beforeCache.Width != w || _beforeCache.Height != h)
            {
                if (_beforeCache != null) _beforeCache.Dispose();
                _beforeCache = new Bitmap(w, h);

                // Monitör tüm ekrana screenLinear uyguladığı için ÖNCE tarafını 1/screenLinear ile geri al
                float invSat = screenLinear > 1.01f ? Math.Max(0.05f, 1.0f / screenLinear) : 1.0f;

                using (var g = Graphics.FromImage(_beforeCache))
                using (var ia = new System.Drawing.Imaging.ImageAttributes())
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;

                    ia.SetWrapMode(WrapMode.TileFlipXY);
                    ia.SetColorMatrix(BuildMatrixFromSat(invSat, 1.0f, 0f, 6500),
                        System.Drawing.Imaging.ColorMatrixFlag.Default,
                        System.Drawing.Imaging.ColorAdjustType.Bitmap);

                    g.DrawImage(_baseScaledCache, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, ia);
                }
                _beforeDirty = false;
            }

            if (_afterDirty || _afterCache == null || _afterCache.Width != w || _afterCache.Height != h)
            {
                if (_afterCache != null) _afterCache.Dispose();
                _afterCache = new Bitmap(w, h);

                float relativeSat;
                if (_satPercent == _screenSatPercent && _filterSatPercent == _screenFilterSatPercent)
                {
                    // Ekrana zaten bu ayar uygulanıyor: ekstra doygunluk basma (çift doygunluk olmasın)
                    relativeSat = 1.0f;
                }
                else
                {
                    float targetSat = ComputeEffectiveSaturation(_satPercent, _filterSatPercent);
                    relativeSat = screenLinear > 0.05f ? Math.Min(2.45f, targetSat / screenLinear) : targetSat;
                }
                float con = Math.Max(0.2f, _contrast / 100.0f);
                float bri = _brightness / 200.0f;

                using (var g = Graphics.FromImage(_afterCache))
                using (var ia = new System.Drawing.Imaging.ImageAttributes())
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;

                    ia.SetWrapMode(WrapMode.TileFlipXY);
                    ia.SetColorMatrix(BuildMatrixFromSat(relativeSat, con, bri, _colorTemp),
                        System.Drawing.Imaging.ColorMatrixFlag.Default,
                        System.Drawing.Imaging.ColorAdjustType.Bitmap);

                    float gamVal = Math.Max(0.4f, Math.Min(2.5f, 100.0f / Math.Max(50, Math.Min(250, _gamma))));
                    ia.SetGamma(gamVal);

                    g.DrawImage(_baseScaledCache, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, ia);
                }
                _afterDirty = false;
            }
        }

        private static float MapSliderToScale(int percent, float maxBoost)
        {
            if (percent <= 0) return 0f;
            if (percent <= 100) return percent / 100.0f;
            float t = Math.Min(1.0f, (percent - 100) / 300.0f);
            return 1.0f + t * maxBoost;
        }

        private static float ComputeEffectiveSaturation(int satPercent, int filterSatPercent)
        {
            float gpu = MapSliderToScale(satPercent, 1.15f);
            float flt = MapSliderToScale(filterSatPercent, 1.25f);
            if (gpu < 1.0f || flt < 1.0f)
                return gpu * flt;
            return Math.Min(2.45f, 1.0f + (gpu - 1.0f) + (flt - 1.0f) * 0.85f);
        }

        private static System.Drawing.Imaging.ColorMatrix BuildMatrixFromSat(float sat, float con, float bri, int colorTemp)
        {
            float tempShift = (colorTemp - 6500.0f) / 3500.0f;
            float rMult = 1.0f - tempShift * 0.14f;
            float bMult = 1.0f + tempShift * 0.16f;

            const float rw = 0.2126f;
            const float gw = 0.7152f;
            const float bw = 0.0722f;

            float inv = 1.0f - sat;
            float r = rw * inv;
            float g = gw * inv;
            float b = bw * inv;

            float t = (1.0f - con) * 0.5f + bri;

            float[][] m =
            {
                new[] { (r + sat) * con * rMult, r * con,                 r * con * bMult,         0f, 0f },
                new[] { g * con * rMult,         (g + sat) * con,         g * con * bMult,         0f, 0f },
                new[] { b * con * rMult,         b * con,                 (b + sat) * con * bMult, 0f, 0f },
                new[] { 0f,                      0f,                      0f,                      1f, 0f },
                new[] { t * rMult,               t,                       t * bMult,               0f, 1f }
            };
            return new System.Drawing.Imaging.ColorMatrix(m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var bounds = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var clipPath = Theme.RoundedRect(bounds, 8f))
            {
                g.SetClip(clipPath);

                EnsureCaches();
                int splitX = (int)Math.Round(Width * _splitRatio);

                if (_beforeCache != null && _afterCache != null)
                {
                    g.DrawImageUnscaled(_afterCache, 0, 0);

                    var prevClip = g.Clip;
                    g.SetClip(new Rectangle(0, 0, splitX, Height), CombineMode.Intersect);
                    g.DrawImageUnscaled(_beforeCache, 0, 0);
                    g.Clip = prevClip;
                }
                else
                {
                    g.Clear(Theme.InputBg);
                }

                // Ayırıcı dikey çizgi
                using (var shadowPen = new Pen(Color.FromArgb(120, 0, 0, 0), 4f))
                    g.DrawLine(shadowPen, splitX, 0, splitX, Height);
                using (var linePen = new Pen(Color.FromArgb(235, 255, 255, 255), 2f))
                    g.DrawLine(linePen, splitX, 0, splitX, Height);

                // Ortadaki sürükleme tutamacı
                int cy = Height / 2;
                var knob = new RectangleF(splitX - 15, cy - 15, 30, 30);
                using (var kb = new SolidBrush(Color.FromArgb(220, 20, 22, 28)))
                    g.FillEllipse(kb, knob);
                using (var kp = new Pen(Color.White, 2f))
                    g.DrawEllipse(kp, knob);
                using (var arrowPen = new Pen(Color.White, 1.8f))
                {
                    arrowPen.StartCap = LineCap.Round;
                    arrowPen.EndCap = LineCap.Round;
                    g.DrawLine(arrowPen, splitX - 4, cy - 4, splitX - 8, cy);
                    g.DrawLine(arrowPen, splitX - 8, cy, splitX - 4, cy + 4);
                    g.DrawLine(arrowPen, splitX + 4, cy - 4, splitX + 8, cy);
                    g.DrawLine(arrowPen, splitX + 8, cy, splitX + 4, cy + 4);
                }

                // Köşe etiketleri
                if (splitX > 76)
                    DrawBadge(g, BeforeText, 12, 12, false);
                if (Width - splitX > 76)
                    DrawBadge(g, AfterText, Width - 12, 12, true);

                g.ResetClip();
            }

            using (var borderPen = new Pen(Theme.BorderSubtle, 1f))
            using (var borderPath = Theme.RoundedRect(bounds, 8f))
                g.DrawPath(borderPen, borderPath);
        }

        private static void DrawBadge(Graphics g, string text, int x, int y, bool alignRight)
        {
            if (string.IsNullOrEmpty(text)) return;
            var sz = TextRenderer.MeasureText(text, Theme.FontSmall);
            int bw = sz.Width + 14;
            int bh = 22;
            int rx = alignRight ? (x - bw) : x;
            var rect = new RectangleF(rx, y, bw, bh);

            using (var path = Theme.RoundedRect(rect, 6f))
            using (var bg = new SolidBrush(Color.FromArgb(180, 14, 16, 22)))
                g.FillPath(bg, path);

            TextRenderer.DrawText(g, text, Theme.FontSmall,
                new Rectangle(rx, y, bw, bh), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_sourceImage != null) { _sourceImage.Dispose(); _sourceImage = null; }
                if (_baseScaledCache != null) { _baseScaledCache.Dispose(); _baseScaledCache = null; }
                if (_beforeCache != null) { _beforeCache.Dispose(); _beforeCache = null; }
                if (_afterCache != null) { _afterCache.Dispose(); _afterCache = null; }
            }
            base.Dispose(disposing);
        }
    }
}
