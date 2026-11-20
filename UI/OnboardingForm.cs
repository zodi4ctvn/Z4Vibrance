using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VibranceZ.Core;

namespace VibranceZ.UI
{
    public sealed class OnboardingForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private readonly IVibranceBackend _backend;
        private readonly Timer _testTimer;
        private int _step;
        private bool _isTesting;

        private Label _lblStepTitle;
        private Label _lblBody;
        private FlatButton _btnTestGpu;
        private Label _lblTestResult;
        private FlatButton _btnBack;
        private FlatButton _btnNext;

        public OnboardingForm(IVibranceBackend backend)
        {
            _backend = backend;
            _testTimer = new Timer { Interval = 1100 };
            _testTimer.Tick += (s, e) =>
            {
                _testTimer.Stop();
                _isTesting = false;
                try { _backend.RestoreInitial(); }
                catch { }
                _btnTestGpu.Enabled = true;
                _lblTestResult.ForeColor = Theme.ActiveGreen;
                _lblTestResult.Text = L.Get("WizardStep2Success");
            };

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(500, 340);
            BackColor = Theme.PanelBg;
            ShowInTaskbar = false;
            DoubleBuffered = true;

            BuildUI();
            RenderStep();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyWindowCorners(Handle, this);
        }

        private void BuildUI()
        {
            var titleBar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.RailBg };
            titleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); }
            };

            var lblTitle = new Label { Text = L.Get("WizardTitle"), Font = Theme.FontBodyBold, ForeColor = Theme.TextPrimary, AutoSize = true, Location = new Point(16, 11) };
            titleBar.Controls.Add(lblTitle);

            _lblStepTitle = new Label { Font = Theme.FontHero, ForeColor = Theme.TextPrimary, AutoSize = false, Size = new Size(452, 34), Location = new Point(24, 58) };
            _lblBody = new Label { Font = Theme.FontBody, ForeColor = Theme.TextSecondary, AutoSize = false, Size = new Size(452, 130), Location = new Point(24, 102) };

            _btnTestGpu = new FlatButton
            {
                Text = L.Get("WizardStep2TestBtn"),
                ButtonStyle = FlatButtonStyle.Secondary,
                Size = new Size(200, 34),
                Location = new Point(24, 200),
                Visible = false
            };
            _btnTestGpu.Click += (s, e) =>
            {
                _btnTestGpu.Enabled = false;
                _isTesting = true;
                _lblTestResult.ForeColor = Theme.Accent;
                _lblTestResult.Text = L.Get("WizardStep2Testing");
                int boost = Math.Max(15, (_backend.MaxLevel - _backend.DefaultLevel) / 3);
                int testSat = Math.Min(_backend.MaxLevel, _backend.DefaultLevel + boost);
                _backend.SetDisplayColor(testSat, _backend.DefaultContrast, _backend.DefaultBrightness, 108, _backend.DefaultColorTemp, false, Screen.PrimaryScreen);
                _testTimer.Start();
            };

            _lblTestResult = new Label { Font = Theme.FontSmall, ForeColor = Theme.TextSecondary, AutoSize = false, Size = new Size(452, 24), Location = new Point(24, 244), Visible = false };

            _btnBack = new FlatButton { Text = L.Get("WizardBack"), ButtonStyle = FlatButtonStyle.Secondary, Size = new Size(92, 32), Location = new Point(276, 288) };
            _btnBack.Click += (s, e) => { if (_step > 0) { _step--; RenderStep(); } };

            _btnNext = new FlatButton { Text = L.Get("WizardNext"), ButtonStyle = FlatButtonStyle.Primary, Size = new Size(96, 32), Location = new Point(378, 288) };
            _btnNext.Click += (s, e) =>
            {
                if (_step == 0)
                {
                    _step = 1;
                    RenderStep();
                }
                else
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            Controls.AddRange(new Control[] { titleBar, _lblStepTitle, _lblBody, _btnTestGpu, _lblTestResult, _btnBack, _btnNext });
        }

        private void RenderStep()
        {
            if (_step == 0)
            {
                _lblStepTitle.Text = L.Get("WizardStep1Title");
                _lblBody.Text = L.Get("WizardStep1Desc");
                _btnTestGpu.Visible = false;
                _lblTestResult.Visible = false;
                _btnBack.Enabled = false;
                _btnNext.Text = L.Get("WizardNext");
            }
            else
            {
                _lblStepTitle.Text = L.Get("WizardStep2Title");
                string gpuStatus = _backend.IsAvailable ? L.Get("DriverConnected") : L.Get("DriverDisconnected");
                _lblBody.Text = string.Format("{0}: {1} ({2})\nAPI: {3}\n{4}",
                    L.Get("AboutGpu"), _backend.VendorName, _backend.DeviceName, _backend.ApiName, gpuStatus);
                _btnTestGpu.Visible = true;
                _lblTestResult.Visible = true;
                _btnBack.Enabled = true;
                _btnNext.Text = L.Get("WizardFinish");
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Border, 1f))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isTesting)
            {
                _testTimer.Stop();
                _isTesting = false;
                try { _backend.RestoreInitial(); }
                catch { }
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_isTesting)
                {
                    _isTesting = false;
                    try { _backend.RestoreInitial(); }
                    catch { }
                }
                _testTimer.Stop();
                _testTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
