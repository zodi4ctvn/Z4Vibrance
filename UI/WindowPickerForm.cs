using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VibranceZ.Core;

namespace VibranceZ.UI
{
    public sealed class WindowPickerForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private readonly WindowWatcher _watcher;
        private readonly List<RunningWindowInfo> _allWindows = new List<RunningWindowInfo>();
        private readonly List<RunningWindowInfo> _filtered = new List<RunningWindowInfo>();

        private TextBox _searchBox;
        private ListBox _listBox;
        private FlatButton _btnRefresh;
        private FlatButton _btnAdd;
        private FlatButton _btnCancel;

        public RunningWindowInfo SelectedWindow { get; private set; }

        public WindowPickerForm(WindowWatcher watcher)
        {
            _watcher = watcher;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(480, 440);
            BackColor = Theme.WindowBg;
            DoubleBuffered = true;
            ShowInTaskbar = false;

            BuildUI();
            LoadWindows();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyWindowCorners(Handle, this);
        }

        private void BuildUI()
        {
            // Titlebar
            var titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Theme.SurfaceBg
            };
            titleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };

            var lblTitle = new Label
            {
                Text = L.Get("PickerTitle"),
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(14, 11)
            };
            lblTitle.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };

            var btnClose = new Panel
            {
                Size = new Size(36, 40),
                Location = new Point(Width - 36, 0),
                BackColor = Theme.SurfaceBg,
                Cursor = Cursors.Hand
            };
            bool closeHover = false;
            btnClose.MouseEnter += (s, e) => { closeHover = true; btnClose.Invalidate(); };
            btnClose.MouseLeave += (s, e) => { closeHover = false; btnClose.Invalidate(); };
            btnClose.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            btnClose.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(closeHover ? Theme.DangerRed : Theme.SurfaceBg);
                using (var p = new Pen(closeHover ? Color.White : Theme.TextSecondary, 1.5f))
                {
                    int cx = btnClose.Width / 2;
                    int cy = btnClose.Height / 2;
                    g.DrawLine(p, cx - 4, cy - 4, cx + 4, cy + 4);
                    g.DrawLine(p, cx + 4, cy - 4, cx - 4, cy + 4);
                }
            };

            titleBar.Controls.Add(lblTitle);
            titleBar.Controls.Add(btnClose);

            // Search bar container
            var searchPanel = new Panel
            {
                Location = new Point(14, 52),
                Size = new Size(Width - 118, 30),
                BackColor = Theme.WindowBg
            };
            searchPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Theme.WindowBg);
                using (var path = Theme.RoundedRect(new RectangleF(0.5f, 0.5f, searchPanel.Width - 1.5f, searchPanel.Height - 1.5f), 6f))
                {
                    using (var b = new SolidBrush(Theme.InputBg)) g.FillPath(b, path);
                    using (var p = new Pen(Theme.Border, 1f)) g.DrawPath(p, path);
                }
            };

            _searchBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.InputBg,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                Location = new Point(10, 7),
                Width = searchPanel.Width - 20
            };
            _searchBox.TextChanged += (s, e) => ApplyFilter();
            searchPanel.Controls.Add(_searchBox);

            _btnRefresh = new FlatButton
            {
                Text = L.Get("Refresh"),
                Location = new Point(Width - 96, 52),
                Size = new Size(82, 30),
                ButtonStyle = FlatButtonStyle.Secondary
            };
            _btnRefresh.Click += (s, e) => LoadWindows();

            // Owner-drawn list of open windows with real .exe icons
            _listBox = new ListBox
            {
                Location = new Point(14, 92),
                Size = new Size(Width - 28, Height - 148),
                BackColor = Theme.SurfaceBg,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 46,
                IntegralHeight = false
            };
            _listBox.DrawItem += OnDrawWindowItem;
            _listBox.SelectedIndexChanged += (s, e) =>
            {
                _btnAdd.Enabled = _listBox.SelectedIndex >= 0;
            };
            _listBox.DoubleClick += (s, e) => ConfirmSelection();

            _btnCancel = new FlatButton
            {
                Text = L.Get("Cancel"),
                Location = new Point(Width - 204, Height - 44),
                Size = new Size(86, 30),
                ButtonStyle = FlatButtonStyle.Secondary
            };
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            _btnAdd = new FlatButton
            {
                Text = L.Get("AddWindow"),
                Location = new Point(Width - 110, Height - 44),
                Size = new Size(96, 30),
                ButtonStyle = FlatButtonStyle.Primary,
                Enabled = false
            };
            _btnAdd.Click += (s, e) => ConfirmSelection();

            Controls.Add(titleBar);
            Controls.Add(searchPanel);
            Controls.Add(_btnRefresh);
            Controls.Add(_listBox);
            Controls.Add(_btnCancel);
            Controls.Add(_btnAdd);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Border, 1f))
            {
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            }
        }

        private void LoadWindows()
        {
            _allWindows.Clear();
            _allWindows.AddRange(_watcher.GetOpenWindows());
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string query = (_searchBox.Text ?? string.Empty).Trim();
            _filtered.Clear();

            foreach (var w in _allWindows)
            {
                if (string.IsNullOrEmpty(query) ||
                    w.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    w.ProcessName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _filtered.Add(w);
                }
            }

            _listBox.BeginUpdate();
            _listBox.Items.Clear();
            foreach (var w in _filtered)
                _listBox.Items.Add(w);
            _listBox.EndUpdate();

            _btnAdd.Enabled = _listBox.SelectedIndex >= 0;
        }

        private void OnDrawWindowItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _filtered.Count)
                return;

            var item = _filtered[e.Index];
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color bg = selected ? Theme.SelectedBg : Theme.SurfaceBg;

            using (var b = new SolidBrush(bg))
                g.FillRectangle(b, e.Bounds);

            if (selected)
            {
                using (var accentBar = new SolidBrush(Theme.Accent))
                    g.FillRectangle(accentBar, e.Bounds.X, e.Bounds.Y + 6, 3, e.Bounds.Height - 12);
            }

            // Icon
            int iconX = e.Bounds.X + 12;
            int iconY = e.Bounds.Y + (e.Bounds.Height - 24) / 2;
            if (item.IconBitmap != null)
            {
                g.DrawImage(item.IconBitmap, iconX, iconY, 24, 24);
            }
            else
            {
                using (var placeholder = new SolidBrush(Theme.CardBg))
                    g.FillRectangle(placeholder, iconX, iconY, 24, 24);
            }

            // Text
            int textX = iconX + 34;
            int textW = e.Bounds.Width - textX - 10;

            var titleRect = new Rectangle(textX, e.Bounds.Y + 6, textW, 18);
            var procRect = new Rectangle(textX, e.Bounds.Y + 24, textW, 16);

            TextRenderer.DrawText(g, item.Title, Theme.FontBodyBold, titleRect, Theme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(g, item.ProcessName + ".exe", Theme.FontSmall, procRect, Theme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // Subtle separator
            using (var sep = new Pen(Theme.BorderSubtle, 1f))
                g.DrawLine(sep, e.Bounds.X + 10, e.Bounds.Bottom - 1, e.Bounds.Right - 10, e.Bounds.Bottom - 1);
        }

        private void ConfirmSelection()
        {
            if (_listBox.SelectedIndex < 0 || _listBox.SelectedIndex >= _filtered.Count)
                return;

            SelectedWindow = _filtered[_listBox.SelectedIndex];
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
