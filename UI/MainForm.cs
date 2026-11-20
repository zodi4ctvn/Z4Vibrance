using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using VibranceZ.Core;
using VibranceZ.Models;

namespace VibranceZ.UI
{
    public sealed class MainForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const string RunRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "Z4Vibrance";

        private readonly AppConfig _config;
        private readonly IVibranceBackend _backend;
        private readonly WindowWatcher _watcher;
        private readonly TransitionEngine _transitionEngine;
        private readonly UndoRedoManager _undoRedo = new UndoRedoManager();
        private readonly Timer _scheduleTimer;
        private readonly Timer _saveDebounceTimer;
        private readonly Timer _hwThrottleTimer;
        private Action _pendingHwAction;
        private bool _pendingConfigSave;
        private DateTime _lastUndoPushUtc = DateTime.MinValue;
        private readonly Icon _appIcon;
        private readonly Bitmap _appTitleBitmap;
        private readonly bool _dirtyShutdown;
        private readonly ToolTip _toolTip = new ToolTip { InitialDelay = 120, ReshowDelay = 80, AutoPopDelay = 8000 };

        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _trayOpenItem;
        private ToolStripMenuItem _trayProfilesItem;
        private ToolStripMenuItem _trayToggleItem;
        private ToolStripMenuItem _trayResetItem;
        private ToolStripMenuItem _trayExitItem;

        private ContextMenuStrip _profileCtxMenu;
        private ToolStripMenuItem _ctxEdit;
        private ToolStripMenuItem _ctxRename;
        private ToolStripMenuItem _ctxDuplicate;
        private ToolStripMenuItem _ctxDelete;

        private bool _forceExit;
        private bool _suppressEvents;
        private bool _isPreviewing;
        private bool _isHoldingBeforeAfter;
        private bool _cleanedUp;

        private string _activeProcessPath = string.Empty;
        private string _activeWindowTitle = "Masaustu";
        private bool _activeWindowFullscreen;
        private int _appliedSaturation;
        private AppProfile _lastMatchedProfile;
        private DateTime _activeProfileStartUtc = DateTime.UtcNow;

        // Çerçeve & Düzen
        private Panel _titleBar;
        private Panel _railPanel;
        private Panel _sidebarPanel;
        private Panel _statusBar;
        private Panel _contentHost;
        private ToastNotification _toast;

        private NavRailButton _navGeneral;
        private NavRailButton _navProfiles;
        private NavRailButton _navPreview;
        private NavRailButton _navSettings;
        private NavRailButton _navAbout;
        private NavIconKind _activeTab = NavIconKind.General;

        private Panel _pageGeneral;
        private Panel _pageProfiles;
        private Panel _pagePreview;
        private Panel _pageSettings;
        private Panel _pageAbout;

        // Sol Profil Paneli
        private Label _lblSidebarHeader;
        private TextBox _txtSearch;
        private ListBox _profileList;
        private readonly List<AppProfile> _filteredProfiles = new List<AppProfile>();
        private int _hoveredListIndex = -1;
        private FlatButton _btnPickWindow;
        private FlatButton _btnPickFile;

        // Alt Durum Çubuğu
        private Panel _statusDot;
        private Label _lblStatusGpu;
        private Label _lblStatusActive;
        private FlatButton _btnUndo;
        private FlatButton _btnRedo;
        private FlatButton _btnStatusReset;
        private FlatButton _btnStatusPause;

        // Genel Sekmesi
        private Label _lblGeneralHero;
        private PictureBox _pbActiveIcon;
        private Label _lblActiveCap;
        private Label _lblActiveTitle;
        private Label _lblActiveState;
        private Label _lblAppliedValue;
        private ColorPreviewStrip _miniPreviewStrip;
        private Label _lblShowcaseHeader;
        private BeforeAfterCompareControl _liveShowcaseView;
        private Label _lblDesktopCardTitle;
        private SettingSliderRow _rowDesktopSat;
        private SettingSliderRow _rowDesktopFilterSat;
        private SettingSliderRow _rowDesktopCon;
        private SettingSliderRow _rowDesktopBri;
        private SettingSliderRow _rowDesktopGam;
        private SettingSliderRow _rowDesktopTemp;
        private Label _lblPresetsHeader;
        private FlowLayoutPanel _presetsFlow;
        private FlatButton _btnSaveDesktopPreset;

        // Profiller Sekmesi
        private Label _lblProfilesHero;
        private FlatButton _btnImportProfile;
        private FlatButton _btnExportProfile;
        private Panel _emptyStatePanel;
        private Label _lblEmptyTitle;
        private Label _lblEmptyDesc;
        private FlatButton _btnEmptyAdd;
        private RoundedCardPanel _profileDetailCard;
        private PictureBox _pbProfileIcon;
        private Label _lblProfileName;
        private Label _lblProfilePath;
        private Label _lblProfileUsage;
        private FlatButton _btnPreview;
        private FlatButton _btnDeleteProfile;
        private SettingSliderRow _rowProfSat;
        private SettingSliderRow _rowProfFilterSat;
        private SettingSliderRow _rowProfCon;
        private SettingSliderRow _rowProfBri;
        private SettingSliderRow _rowProfGam;
        private SettingSliderRow _rowProfTemp;
        private Label _lblPrimary;
        private FlatToggle _tglPrimaryOnly;
        private Label _lblMatchFullPath;
        private FlatToggle _tglMatchFullPath;
        private Label _lblFullscreenOnly;
        private FlatToggle _tglFullscreenOnly;
        private Label _lblPerMonitor;
        private FlatToggle _tglPerMonitor;
        private Label _lblSelectMonitor;
        private DarkDropdown _cmbMonitors;

        private InfoBadge _infoPerMonitor;
        private InfoBadge _infoPrimary;
        private InfoBadge _infoMatchFullPath;
        private InfoBadge _infoFullscreenOnly;
        private InfoBadge _infoAutoTrack;
        private InfoBadge _infoStartup;
        private InfoBadge _infoTrayClose;
        private InfoBadge _infoOled;
        private InfoBadge _infoSchedule;

        // Önizleme Sekmesi
        private Label _lblPreviewHero;
        private BeforeAfterCompareControl _compareView;
        private Label _lblCompareHint;
        private FlatButton _btnLoadPreviewImg;
        private FlatButton _btnResetPreviewImg;
        private FlatButton _btnPrevLive;
        private FlatButton _btnPrevFps;
        private FlatButton _btnPrevOverdrive;
        private string _previewSampleMode = "auto";
        private ColorPreviewStrip _largePreviewStrip;
        private FlatButton _btnBeforeAfter;
        private ColorState _lastColorState;

        // Ayarlar Sekmesi
        private Label _lblSettingsHero;
        private Label _lblAppearanceCard;
        private Label _lblAutoTrack;
        private FlatToggle _tglEnabled;
        private Label _lblStartup;
        private FlatToggle _tglStartup;
        private Label _lblTrayClose;
        private FlatToggle _tglTrayOnClose;
        private Label _lblOled;
        private FlatToggle _tglOled;
        private Label _lblLangRow;
        private FlatButton _btnLangSwitch;
        private Label _lblDiagCard;
        private Label _lblDiagInfo;
        private FlatButton _btnOpenLogs;
        private Label _lblScheduleCard;
        private Label _lblEnableSchedule;
        private FlatToggle _tglSchedule;
        private Label _lblSchedStart;
        private DarkStepperBox _numStartHour;
        private DarkStepperBox _numStartMin;
        private Label _lblSchedEnd;
        private DarkStepperBox _numEndHour;
        private DarkStepperBox _numEndMin;
        private SettingSliderRow _rowSchedTemp;
        private SettingSliderRow _rowSchedSat;

        // Hakkında Sekmesi
        private Label _lblAboutHero;
        private Label _lblAboutVersion;
        private Label _lblAboutGpu;
        private Label _lblAboutApi;
        private Label _lblAboutPrivacy;
        private FlatButton _btnOpenWizard;

        public MainForm(bool dirtyShutdown = false)
        {
            _dirtyShutdown = dirtyShutdown;
            _config = AppConfig.Load();
            L.CurrentLanguage = _config.Language == "en" ? "en" : "tr";
            Theme.ApplyPalette(_config.OledBlackTheme);

            _backend = BackendFactory.CreateAndInit();
            _watcher = new WindowWatcher();
            _transitionEngine = new TransitionEngine();
            _appIcon = LoadEmbeddedAppIcon() ?? IconGenerator.CreateAppIcon(64);
            _appTitleBitmap = LoadEmbeddedAppBitmap(20) ?? IconGenerator.CreateAppBitmap(20);

            if (_dirtyShutdown)
            {
                try
                {
                    _backend.RestoreInitial();
                    ColorFilterHelper.Reset();
                }
                catch { }
            }

            SanitizeConfigValues();

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(880, 620);
            Size = new Size(1020, 720);
            BackColor = Theme.RailBg;
            Icon = _appIcon;
            Text = "Z4Vibrance";

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            SuspendLayout();
            BuildUI();
            BuildTrayIcon();
            BuildProfileContextMenu();
            ResumeLayout(true);

            ApplyLanguageTexts();
            RefreshProfileList();
            SyncGeneralControls();
            SyncSettingsControls();
            SyncSelectedProfileControls();
            SwitchTab(NavIconKind.General);

            _watcher.ForegroundChanged += OnForegroundChanged;
            _watcher.Start();
            ApplyCurrentForegroundState(true);

            _scheduleTimer = new Timer { Interval = 30000 };
            _scheduleTimer.Tick += (s, e) => ApplyCurrentForegroundState();
            if (_config.ScheduleEnabled && _config.Schedules.Count > 0)
                _scheduleTimer.Start();

            _saveDebounceTimer = new Timer { Interval = 350 };
            _saveDebounceTimer.Tick += (s, e) =>
            {
                _saveDebounceTimer.Stop();
                if (_pendingConfigSave)
                {
                    _pendingConfigSave = false;
                    _config.Save();
                }
            };

            _hwThrottleTimer = new Timer { Interval = 30 };
            _hwThrottleTimer.Tick += (s, e) =>
            {
                _hwThrottleTimer.Stop();
                var act = _pendingHwAction;
                _pendingHwAction = null;
                if (act != null) act();
            };

            SystemEvents.SessionEnding += OnSessionEnding;
            SystemEvents.DisplaySettingsChanged += OnSystemDisplayOrPowerChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;

            Shown += (s, e) =>
            {
                LayoutStatusBar();
                LayoutAllPages();
                PositionToast();
                if (_dirtyShutdown)
                    ShowToast(L.Get("DirtyShutdownWarning"), Theme.WarningAmber);

                if (!_config.OnboardingCompleted)
                {
                    using (var wiz = new OnboardingForm(_backend))
                    {
                        wiz.ShowDialog(this);
                    }
                    _config.OnboardingCompleted = true;
                    _config.Save();
                    ApplyCurrentForegroundState(true);
                }
            };
        }

        private void SanitizeConfigValues()
        {
            if (_config.IsFreshConfig)
            {
                _config.DesktopSaturation = _backend.DefaultLevel;
                _config.DesktopFilterSaturation = ColorFilterHelper.DefaultPercent;
                _config.DesktopContrast = _backend.DefaultContrast;
                _config.DesktopBrightness = _backend.DefaultBrightness;
                _config.DesktopGamma = 100;
                _config.DesktopColorTemp = _backend.DefaultColorTemp;
                _config.IsFreshConfig = false;
            }
            else
            {
                _config.DesktopSaturation = ClampSat(_config.DesktopSaturation);
                _config.DesktopFilterSaturation = ClampFilterSat(_config.DesktopFilterSaturation);
                _config.DesktopContrast = ClampCon(_config.DesktopContrast > 0 ? _config.DesktopContrast : _backend.DefaultContrast);
                _config.DesktopBrightness = ClampBri(_config.DesktopBrightness);
                _config.DesktopGamma = ClampGam(_config.DesktopGamma > 0 ? _config.DesktopGamma : 100);
                _config.DesktopColorTemp = ClampTemp(_config.DesktopColorTemp > 0 ? _config.DesktopColorTemp : _backend.DefaultColorTemp);
            }

            foreach (var p in _config.Profiles)
            {
                ClampProfileToBackend(p);
            }

            foreach (var s in _config.Schedules)
            {
                s.Saturation = ClampSat(s.Saturation);
                s.FilterSaturation = ClampFilterSat(s.FilterSaturation);
                s.Contrast = ClampCon(s.Contrast > 0 ? s.Contrast : _backend.DefaultContrast);
                s.Brightness = ClampBri(s.Brightness);
                s.Gamma = ClampGam(s.Gamma > 0 ? s.Gamma : 100);
                s.ColorTemp = ClampTemp(s.ColorTemp > 0 ? s.ColorTemp : _backend.DefaultColorTemp);
            }
        }

        private void ClampProfileToBackend(AppProfile p)
        {
            if (p == null) return;
            p.TargetSaturation = ClampSat(p.TargetSaturation);
            p.TargetFilterSaturation = ClampFilterSat(p.TargetFilterSaturation);
            p.TargetContrast = ClampCon(p.TargetContrast > 0 ? p.TargetContrast : _backend.DefaultContrast);
            p.TargetBrightness = ClampBri(p.TargetBrightness);
            p.TargetGamma = ClampGam(p.TargetGamma > 0 ? p.TargetGamma : 100);
            p.TargetColorTemp = ClampTemp(p.TargetColorTemp > 0 ? p.TargetColorTemp : _backend.DefaultColorTemp);
            if (p.MonitorSettings != null)
            {
                foreach (var ms in p.MonitorSettings)
                {
                    if (ms == null) continue;
                    ms.Saturation = ClampSat(ms.Saturation);
                    ms.Contrast = ClampCon(ms.Contrast > 0 ? ms.Contrast : _backend.DefaultContrast);
                    ms.Brightness = ClampBri(ms.Brightness);
                    ms.Gamma = ClampGam(ms.Gamma > 0 ? ms.Gamma : 100);
                    ms.ColorTemp = ClampTemp(ms.ColorTemp > 0 ? ms.ColorTemp : _backend.DefaultColorTemp);
                }
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyWindowCorners(Handle, this);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (IsHandleCreated)
                Theme.ApplyWindowCorners(Handle, this);
            PositionToast();
        }

        // Çerçevesiz pencerede Aero Snap, tam ekran sınırları ve kenardan boyutlandırma
        protected override CreateParams CreateParams
        {
            get
            {
                const int wsThickFrame = 0x00040000;
                const int wsMinimizeBox = 0x00020000;
                const int wsMaximizeBox = 0x00010000;
                CreateParams cp = base.CreateParams;
                cp.Style |= wsThickFrame | wsMinimizeBox | wsMaximizeBox;
                return cp;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Program.WmShowMe && Program.WmShowMe != 0)
            {
                RestoreFromTray();
                return;
            }

            // Standart Windows çerçevesini çizme (Aero Snap aktif kalırken özel çerçeve korunur)
            if (m.Msg == 0x0083 && m.WParam.ToInt32() == 1)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            // Modal pencere (Dosya / Pencere Seç) açılınca klasik Windows başlık çubuğunun çizilmesini engelle
            if (m.Msg == 0x0085) // WM_NCPAINT
            {
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == 0x0086) // WM_NCACTIVATE
            {
                m.Result = new IntPtr(1);
                return;
            }

            // Tam ekranda görev çubuğunu kapatmaması için WorkingArea sınırlarını bildir
            if (m.Msg == 0x0024)
            {
                base.WndProc(ref m);
                var mmi = (MINMAXINFO)Marshal.PtrToStructure(m.LParam, typeof(MINMAXINFO));
                Screen scr = Screen.FromHandle(Handle);
                Rectangle wa = scr.WorkingArea;
                Rectangle mb = scr.Bounds;
                mmi.ptMaxPosition.X = wa.Left - mb.Left;
                mmi.ptMaxPosition.Y = wa.Top - mb.Top;
                mmi.ptMaxSize.X = wa.Width;
                mmi.ptMaxSize.Y = wa.Height;
                mmi.ptMinTrackSize.X = MinimumSize.Width;
                mmi.ptMinTrackSize.Y = MinimumSize.Height;
                Marshal.StructureToPtr(mmi, m.LParam, true);
                m.Result = IntPtr.Zero;
                return;
            }

            const int wmNcHitTest = 0x0084;
            if (m.Msg == wmNcHitTest && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                Point pt = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16));
                const int grip = 6;
                bool left = pt.X <= grip;
                bool right = pt.X >= ClientSize.Width - grip;
                bool top = pt.Y <= grip;
                bool bottom = pt.Y >= ClientSize.Height - grip;

                if (top && left) { m.Result = (IntPtr)13; return; }
                if (top && right) { m.Result = (IntPtr)14; return; }
                if (bottom && left) { m.Result = (IntPtr)16; return; }
                if (bottom && right) { m.Result = (IntPtr)17; return; }
                if (left) { m.Result = (IntPtr)10; return; }
                if (right) { m.Result = (IntPtr)11; return; }
                if (top) { m.Result = (IntPtr)12; return; }
                if (bottom) { m.Result = (IntPtr)15; return; }
                return;
            }

            base.WndProc(ref m);
        }

        private static Icon LoadEmbeddedAppIcon()
        {
            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return null;
            }
        }

        private Bitmap LoadEmbeddedAppBitmap(int size)
        {
            try
            {
                if (_appIcon != null)
                {
                    using (var sized = new Icon(_appIcon, size, size))
                        return sized.ToBitmap();
                }
            }
            catch { }
            return null;
        }

        private int ClampSat(int val) { return Math.Max(_backend.MinLevel, Math.Min(_backend.MaxLevel, val)); }
        private static int ClampFilterSat(int val) { return ColorFilterHelper.Clamp(val); }
        private int ClampCon(int val) { return Math.Max(_backend.MinContrast, Math.Min(_backend.MaxContrast, val)); }
        private int ClampBri(int val) { return Math.Max(_backend.MinBrightness, Math.Min(_backend.MaxBrightness, val)); }
        private static int ClampGam(int val) { return Math.Max(50, Math.Min(250, val)); }
        private int ClampTemp(int val) { return Math.Max(_backend.MinColorTemp, Math.Min(_backend.MaxColorTemp, val)); }

        private void BuildUI()
        {
            // 1. Üst Başlık Çubuğu
            _titleBar = new Panel { Dock = DockStyle.Top, Width = ClientSize.Width, Height = Theme.TitleBarHeight, BackColor = Theme.RailBg };
            AttachDrag(_titleBar);

            var pbLogo = new PictureBox
            {
                Image = _appTitleBitmap,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(20, 20),
                Location = new Point(22, 10)
            };
            AttachDrag(pbLogo);

            var lblAppTitle = new Label
            {
                Text = "Z4Vibrance",
                Font = Theme.FontTitle,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(50, 10)
            };
            AttachDrag(lblAppTitle);

            var btnClose = CreateCaptionButton(CaptionKind.Close);
            btnClose.Click += (s, e) => HandleCloseAction();

            var btnMax = CreateCaptionButton(CaptionKind.Maximize);
            btnMax.Click += (s, e) =>
            {
                WindowState = (WindowState == FormWindowState.Maximized)
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            };

            var btnMin = CreateCaptionButton(CaptionKind.Minimize);
            btnMin.Click += (s, e) => { WindowState = FormWindowState.Minimized; };

            _titleBar.Controls.Add(pbLogo);
            _titleBar.Controls.Add(lblAppTitle);
            _titleBar.Controls.Add(btnMin);
            _titleBar.Controls.Add(btnMax);
            _titleBar.Controls.Add(btnClose);

            // 2. Sol İkon Rayı (~64px)
            _railPanel = new Panel { Dock = DockStyle.Left, Width = Theme.RailWidth, BackColor = Theme.RailBg };
            _navGeneral = new NavRailButton { IconKind = NavIconKind.General, Location = new Point(0, 8) };
            _navProfiles = new NavRailButton { IconKind = NavIconKind.Profiles, Location = new Point(0, 64) };
            _navPreview = new NavRailButton { IconKind = NavIconKind.Preview, Location = new Point(0, 120) };
            _navSettings = new NavRailButton { IconKind = NavIconKind.Settings, Location = new Point(0, 176) };
            _navAbout = new NavRailButton { IconKind = NavIconKind.About, Location = new Point(0, 232) };

            _navGeneral.Click += (s, e) => SwitchTab(NavIconKind.General);
            _navProfiles.Click += (s, e) => SwitchTab(NavIconKind.Profiles);
            _navPreview.Click += (s, e) => SwitchTab(NavIconKind.Preview);
            _navSettings.Click += (s, e) => SwitchTab(NavIconKind.Settings);
            _navAbout.Click += (s, e) => SwitchTab(NavIconKind.About);

            _railPanel.Controls.AddRange(new Control[] { _navGeneral, _navProfiles, _navPreview, _navSettings, _navAbout });

            // 3. Alt Sabit Mini Durum Çubuğu
            _statusBar = new Panel { Dock = DockStyle.Bottom, Width = ClientSize.Width, Height = Theme.StatusBarHeight, BackColor = Theme.RailBg };
            _statusBar.Paint += (s, e) =>
            {
                using (var p = new Pen(Theme.BorderSubtle, 1f))
                    e.Graphics.DrawLine(p, 0, 0, _statusBar.Width, 0);
            };

            _statusDot = new Panel { Size = new Size(10, 10), Location = new Point(18, 21), BackColor = Theme.RailBg };
            _statusDot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color dotCol = _backend.IsAvailable ? Theme.ActiveGreen : Theme.WarningAmber;
                using (var b = new SolidBrush(dotCol))
                    e.Graphics.FillEllipse(b, 0, 0, 9, 9);
            };

            _lblStatusGpu = new Label
            {
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Size = new Size(340, 18),
                Location = new Point(36, 9)
            };

            _lblStatusActive = new Label
            {
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(420, 16),
                Location = new Point(36, 27)
            };

            _btnUndo = new FlatButton
            {
                Size = new Size(76, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(_statusBar.Width - 410, 11),
                ButtonStyle = FlatButtonStyle.Secondary,
                Enabled = false
            };
            _btnUndo.Click += (s, e) => PerformUndo();

            _btnRedo = new FlatButton
            {
                Size = new Size(76, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(_statusBar.Width - 328, 11),
                ButtonStyle = FlatButtonStyle.Secondary,
                Enabled = false
            };
            _btnRedo.Click += (s, e) => PerformRedo();

            _btnStatusPause = new FlatButton
            {
                Size = new Size(110, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(_statusBar.Width - 246, 11),
                ButtonStyle = FlatButtonStyle.Secondary
            };
            _btnStatusPause.Click += (s, e) =>
            {
                _config.Enabled = !_config.Enabled;
                _config.Save();
                SyncSettingsControls();
                UpdateTrayMenuState();
                UpdateStatusBar();
                ApplyCurrentForegroundState();
            };

            _btnStatusReset = new FlatButton
            {
                Size = new Size(116, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(_statusBar.Width - 128, 11),
                ButtonStyle = FlatButtonStyle.DangerGhost
            };
            _btnStatusReset.Click += (s, e) => ResetAllColorsToDefault();

            _statusBar.Controls.AddRange(new Control[] { _statusDot, _lblStatusGpu, _lblStatusActive, _btnUndo, _btnRedo, _btnStatusPause, _btnStatusReset });

            // 4. Profil Kenar Çubuğu (~240px)
            _sidebarPanel = new Panel { Dock = DockStyle.Left, Width = Theme.SidebarWidth, Height = ClientSize.Height - Theme.TitleBarHeight - Theme.StatusBarHeight, BackColor = Theme.PanelBg };
            _sidebarPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(Theme.BorderSubtle, 1f))
                    e.Graphics.DrawLine(p, _sidebarPanel.Width - 1, 0, _sidebarPanel.Width - 1, _sidebarPanel.Height);
            };

            _lblSidebarHeader = new Label
            {
                Font = Theme.FontSection,
                ForeColor = Theme.TextMuted,
                AutoSize = true,
                Location = new Point(14, 14)
            };

            var searchWrap = new Panel
            {
                Location = new Point(12, 36),
                Size = new Size(Theme.SidebarWidth - 24, 30),
                BackColor = Theme.PanelBg
            };
            searchWrap.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // Köşeler panel rengiyle temizlenir, içi yuvarlatılmış giriş kutusu olarak doldurulur
                g.Clear(_sidebarPanel != null ? _sidebarPanel.BackColor : Theme.PanelBg);
                using (var path = Theme.RoundedRect(new RectangleF(0.5f, 0.5f, searchWrap.Width - 1.5f, searchWrap.Height - 1.5f), 6f))
                {
                    using (var b = new SolidBrush(Theme.InputBg)) g.FillPath(b, path);
                    using (var p = new Pen(Theme.Border, 1f)) g.DrawPath(p, path);
                }
            };

            _txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.InputBg,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                Location = new Point(10, 7),
                Width = searchWrap.Width - 20
            };
            _txtSearch.TextChanged += (s, e) => RefreshProfileList();
            searchWrap.Controls.Add(_txtSearch);

            _profileList = new ListBox
            {
                Location = new Point(8, 74),
                Size = new Size(Theme.SidebarWidth - 16, 480),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Theme.PanelBg,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 46,
                IntegralHeight = false
            };
            _profileList.DrawItem += OnDrawProfileItem;
            _profileList.MouseMove += (s, e) =>
            {
                int idx = _profileList.IndexFromPoint(e.Location);
                if (idx != _hoveredListIndex)
                {
                    _hoveredListIndex = idx;
                    _profileList.Invalidate();
                }
            };
            _profileList.MouseLeave += (s, e) => { _hoveredListIndex = -1; _profileList.Invalidate(); };
            _profileList.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    int idx = _profileList.IndexFromPoint(e.Location);
                    if (idx >= 0) _profileList.SelectedIndex = idx;
                }
            };
            _profileList.SelectedIndexChanged += (s, e) =>
            {
                _isPreviewing = false;
                SyncSelectedProfileControls();
                if (_activeTab != NavIconKind.Profiles && GetSelectedProfile() != null)
                    SwitchTab(NavIconKind.Profiles);
                ApplyCurrentForegroundState();
            };

            _btnPickWindow = new FlatButton
            {
                ButtonStyle = FlatButtonStyle.Primary,
                Size = new Size(134, 32),
                Location = new Point(12, _sidebarPanel.Height - 44),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            _btnPickWindow.Click += OnPickWindowClick;

            _btnPickFile = new FlatButton
            {
                ButtonStyle = FlatButtonStyle.Secondary,
                Size = new Size(76, 32),
                Location = new Point(152, _sidebarPanel.Height - 44),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            _btnPickFile.Click += OnPickFileClick;

            _sidebarPanel.Controls.AddRange(new Control[] { _lblSidebarHeader, searchWrap, _profileList, _btnPickWindow, _btnPickFile });

            // 5. Ana İçerik Alanı
            _contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                Size = new Size(ClientSize.Width - Theme.RailWidth - Theme.SidebarWidth, ClientSize.Height - Theme.TitleBarHeight - Theme.StatusBarHeight),
                BackColor = Theme.ContentBg,
                AutoScroll = false
            };

            BuildGeneralPage();
            BuildProfilesPage();
            BuildPreviewPage();
            BuildSettingsPage();
            BuildAboutPage();

            _toast = new ToastNotification();
            _contentHost.Controls.Add(_toast);

            // Dock sırası WinForms'ta önemlidir (Fill en üstte, sonra Left/Bottom/Top)
            Controls.Add(_contentHost);
            Controls.Add(_sidebarPanel);
            Controls.Add(_railPanel);
            Controls.Add(_statusBar);
            Controls.Add(_titleBar);

            _contentHost.SizeChanged += (s, e) => LayoutAllPages();
            _statusBar.SizeChanged += (s, e) => LayoutStatusBar();
            LayoutStatusBar();
            LayoutAllPages();
            PositionToast();
        }

        private void LayoutStatusBar()
        {
            if (_statusBar == null || _btnStatusReset == null) return;
            int w = _statusBar.ClientSize.Width;
            if (w < 300) return;

            int right = w - 12;
            _btnStatusReset.Location = new Point(right - _btnStatusReset.Width, 11);
            right = _btnStatusReset.Left - 8;
            _btnStatusPause.Location = new Point(right - _btnStatusPause.Width, 11);
            right = _btnStatusPause.Left - 8;
            _btnRedo.Location = new Point(right - _btnRedo.Width, 11);
            right = _btnRedo.Left - 6;
            _btnUndo.Location = new Point(right - _btnUndo.Width, 11);

            int maxLabelW = Math.Max(120, _btnUndo.Left - 46);
            _lblStatusGpu.Width = maxLabelW;
            _lblStatusActive.Width = maxLabelW;
        }

        private void LayoutAllPages()
        {
            if (_contentHost == null) return;
            int hostW = _contentHost.ClientSize.Width;
            if (hostW < 200) return;

            // Dikey kaydırma çubuğu için 20px pay bırak
            int cardW = Math.Max(420, hostW - 64);
            int innerW = cardW - 32;

            Panel[] pages = { _pageGeneral, _pageProfiles, _pagePreview, _pageSettings, _pageAbout };
            foreach (var page in pages)
            {
                if (page == null) continue;
                foreach (Control c in page.Controls)
                {
                    var card = c as RoundedCardPanel;
                    if (card == null) continue;

                    card.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                    card.Left = 24;
                    card.Width = cardW;

                    foreach (Control child in card.Controls)
                    {
                        if (child is SettingSliderRow || child is ColorPreviewStrip || child is BeforeAfterCompareControl)
                        {
                            child.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                            child.Left = 16;
                            child.Width = innerW;
                        }
                        else if (child is FlatToggle)
                        {
                            child.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                            child.Left = cardW - 16 - child.Width;
                        }
                        else if (child is FlowLayoutPanel)
                        {
                            child.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                            child.Left = 16;
                            child.Width = innerW;
                        }
                    }
                }
            }

            // Özel konumlandırılan kontroller
            if (_lblAppliedValue != null)
            {
                _lblAppliedValue.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _lblAppliedValue.Left = cardW - 16 - _lblAppliedValue.Width;
                _lblActiveTitle.Width = Math.Max(160, _lblAppliedValue.Left - 70);
                _lblActiveState.Width = _lblActiveTitle.Width;
            }
            if (_btnSaveDesktopPreset != null)
            {
                _btnSaveDesktopPreset.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _btnSaveDesktopPreset.Left = cardW - 16 - _btnSaveDesktopPreset.Width;
                LayoutPresetButtons(innerW);
            }
            if (_btnExportProfile != null && _btnImportProfile != null)
            {
                _btnExportProfile.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _btnImportProfile.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _btnExportProfile.Left = 24 + cardW - _btnExportProfile.Width;
                _btnImportProfile.Left = _btnExportProfile.Left - 8 - _btnImportProfile.Width;
            }
            if (_btnDeleteProfile != null && _btnPreview != null)
            {
                _btnDeleteProfile.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _btnPreview.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _btnDeleteProfile.Left = cardW - 16 - _btnDeleteProfile.Width;
                _btnPreview.Left = _btnDeleteProfile.Left - 8 - _btnPreview.Width;
                _lblProfileName.Width = Math.Max(140, _btnPreview.Left - 70);
                _lblProfilePath.Width = _lblProfileName.Width;
                _lblProfileUsage.Width = _lblProfileName.Width;
            }
            if (_cmbMonitors != null)
            {
                _cmbMonitors.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _cmbMonitors.Left = cardW - 16 - _cmbMonitors.Width;
            }
            if (_emptyStatePanel != null && _btnEmptyAdd != null)
            {
                _lblEmptyTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _lblEmptyDesc.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _lblEmptyTitle.Left = 24;
                _lblEmptyTitle.Width = cardW - 48;
                _lblEmptyDesc.Left = 24;
                _lblEmptyDesc.Width = cardW - 48;
                _btnEmptyAdd.Left = (cardW - _btnEmptyAdd.Width) / 2;
            }
            if (_btnLangSwitch != null)
            {
                _btnLangSwitch.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _btnLangSwitch.Left = cardW - 16 - _btnLangSwitch.Width;
            }
            if (_btnOpenLogs != null && _lblDiagInfo != null)
            {
                _btnOpenLogs.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _btnOpenLogs.Left = cardW - 16 - _btnOpenLogs.Width;
                _lblDiagInfo.Width = Math.Max(180, _btnOpenLogs.Left - 28);
            }
            if (_lblAboutPrivacy != null)
            {
                _lblAboutPrivacy.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _lblAboutPrivacy.Width = cardW - 40;
            }
        }

        private void BuildGeneralPage()
        {
            _pageGeneral = CreatePagePanel();
            _lblGeneralHero = CreateHeroLabel();

            var statusCard = new RoundedCardPanel { Location = new Point(24, 68), Size = new Size(640, 128), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _pbActiveIcon = new PictureBox { Size = new Size(32, 32), Location = new Point(16, 18), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.InputBg };
            _lblActiveCap = new Label { Font = Theme.FontSection, ForeColor = Theme.TextMuted, AutoSize = true, Location = new Point(58, 12) };
            _lblActiveTitle = new Label { Font = Theme.FontTitle, ForeColor = Theme.TextPrimary, AutoSize = false, Size = new Size(420, 22), Location = new Point(58, 28) };
            _lblActiveState = new Label { Font = Theme.FontSmall, ForeColor = Theme.TextSecondary, AutoSize = false, Size = new Size(420, 16), Location = new Point(58, 50) };

            _lblAppliedValue = new Label
            {
                Text = "100",
                Font = new Font("Consolas", 16f, FontStyle.Bold),
                ForeColor = Theme.Accent,
                BackColor = Theme.InputBg,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(76, 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(statusCard.Width - 92, 16)
            };

            _miniPreviewStrip = new ColorPreviewStrip
            {
                Location = new Point(16, 74),
                Size = new Size(statusCard.Width - 32, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            statusCard.Controls.AddRange(new Control[] { _pbActiveIcon, _lblActiveCap, _lblActiveTitle, _lblActiveState, _lblAppliedValue, _miniPreviewStrip });

            var showcaseCard = new RoundedCardPanel { Location = new Point(24, 210), Size = new Size(640, 206), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _lblShowcaseHeader = new Label { Font = Theme.FontSection, ForeColor = Theme.TextMuted, AutoSize = true, Location = new Point(16, 12) };
            _liveShowcaseView = new BeforeAfterCompareControl
            {
                Location = new Point(16, 36),
                Size = new Size(showcaseCard.Width - 32, 156),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            showcaseCard.Controls.AddRange(new Control[] { _lblShowcaseHeader, _liveShowcaseView });

            var desktopCard = new RoundedCardPanel { Location = new Point(24, 430), Size = new Size(640, 268), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _lblDesktopCardTitle = new Label { Font = Theme.FontSection, ForeColor = Theme.TextMuted, AutoSize = true, Location = new Point(16, 14) };

            int rowW = desktopCard.Width - 32;
            _rowDesktopSat = CreateSliderRow(16, 42, rowW, _backend.MinLevel, _backend.MaxLevel, _backend.DefaultLevel);
            _rowDesktopSat.ValueChanged += (s, e) => { if (_suppressEvents) return; PushDesktopUndo(); _config.DesktopSaturation = _rowDesktopSat.Value; ScheduleConfigSave(); ApplyCurrentForegroundState(true, true); };

            _rowDesktopFilterSat = CreateSliderRow(16, 78, rowW, ColorFilterHelper.MinPercent, ColorFilterHelper.MaxPercent, ColorFilterHelper.DefaultPercent);
            _rowDesktopFilterSat.UnitSuffix = "%";
            _rowDesktopFilterSat.ValueChanged += (s, e) => { if (_suppressEvents) return; PushDesktopUndo(); _config.DesktopFilterSaturation = _rowDesktopFilterSat.Value; ScheduleConfigSave(); ApplyCurrentForegroundState(true, true); };

            _rowDesktopCon = CreateSliderRow(16, 114, rowW, _backend.MinContrast, _backend.MaxContrast, _backend.DefaultContrast);
            _rowDesktopCon.UnsupportedReason = _backend.SupportsContrastBrightness ? string.Empty : L.Get("UnsupportedOnNvidia");
            _rowDesktopCon.ValueChanged += (s, e) => { if (_suppressEvents) return; PushDesktopUndo(); _config.DesktopContrast = _rowDesktopCon.Value; ScheduleConfigSave(); ApplyCurrentForegroundState(true, true); };

            _rowDesktopBri = CreateSliderRow(16, 150, rowW, _backend.MinBrightness, _backend.MaxBrightness, _backend.DefaultBrightness);
            _rowDesktopBri.UnsupportedReason = _backend.SupportsContrastBrightness ? string.Empty : L.Get("UnsupportedOnNvidia");
            _rowDesktopBri.ValueChanged += (s, e) => { if (_suppressEvents) return; PushDesktopUndo(); _config.DesktopBrightness = _rowDesktopBri.Value; ScheduleConfigSave(); ApplyCurrentForegroundState(true, true); };

            _rowDesktopGam = CreateSliderRow(16, 186, rowW, 50, 250, 100);
            _rowDesktopGam.ValueChanged += (s, e) => { if (_suppressEvents) return; PushDesktopUndo(); _config.DesktopGamma = _rowDesktopGam.Value; ScheduleConfigSave(); ApplyCurrentForegroundState(true, true); };

            _rowDesktopTemp = CreateSliderRow(16, 222, rowW, _backend.MinColorTemp, _backend.MaxColorTemp, _backend.DefaultColorTemp);
            _rowDesktopTemp.UnitSuffix = "K";
            _rowDesktopTemp.UnsupportedReason = _backend.SupportsColorTemp ? string.Empty : (_backend.VendorName == "NVIDIA" ? L.Get("UnsupportedOnNvidia") : string.Empty);
            _rowDesktopTemp.ValueChanged += (s, e) => { if (_suppressEvents) return; PushDesktopUndo(); _config.DesktopColorTemp = _rowDesktopTemp.Value; ScheduleConfigSave(); ApplyCurrentForegroundState(true, true); };

            desktopCard.Controls.AddRange(new Control[] { _lblDesktopCardTitle, _rowDesktopSat, _rowDesktopFilterSat, _rowDesktopCon, _rowDesktopBri, _rowDesktopGam, _rowDesktopTemp });

            var presetsCard = new RoundedCardPanel { Location = new Point(24, 712), Size = new Size(640, 96), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _lblPresetsHeader = new Label { Font = Theme.FontSection, ForeColor = Theme.TextMuted, AutoSize = true, Location = new Point(16, 15) };
            _btnSaveDesktopPreset = new FlatButton { ButtonStyle = FlatButtonStyle.Secondary, Size = new Size(134, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(presetsCard.Width - 150, 10) };
            _btnSaveDesktopPreset.Click += (s, e) => SaveCurrentAsPreset(false);

            _presetsFlow = new FlowLayoutPanel
            {
                Location = new Point(16, 46),
                Size = new Size(presetsCard.Width - 32, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Theme.CardBg,
                AutoScroll = false
            };
            presetsCard.Controls.AddRange(new Control[] { _lblPresetsHeader, _btnSaveDesktopPreset, _presetsFlow });

            _pageGeneral.Controls.AddRange(new Control[] { _lblGeneralHero, statusCard, showcaseCard, desktopCard, presetsCard });
            _contentHost.Controls.Add(_pageGeneral);
        }

        private void BuildProfilesPage()
        {
            _pageProfiles = CreatePagePanel();
            _lblProfilesHero = CreateHeroLabel();

            _btnImportProfile = new FlatButton { ButtonStyle = FlatButtonStyle.Secondary, Size = new Size(106, 30), Location = new Point(444, 22), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnImportProfile.Click += OnImportProfileClick;

            _btnExportProfile = new FlatButton { ButtonStyle = FlatButtonStyle.Secondary, Size = new Size(106, 30), Location = new Point(558, 22), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnExportProfile.Click += OnExportProfileClick;

            _emptyStatePanel = new RoundedCardPanel { Location = new Point(24, 68), Size = new Size(640, 220), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _lblEmptyTitle = new Label { Font = Theme.FontTitle, ForeColor = Theme.TextPrimary, AutoSize = false, Size = new Size(580, 28), Location = new Point(24, 56), TextAlign = ContentAlignment.MiddleCenter, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _lblEmptyDesc = new Label { Font = Theme.FontBody, ForeColor = Theme.TextSecondary, AutoSize = false, Size = new Size(580, 36), Location = new Point(24, 88), TextAlign = ContentAlignment.MiddleCenter, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _btnEmptyAdd = new FlatButton { ButtonStyle = FlatButtonStyle.Primary, Size = new Size(160, 34), Location = new Point(240, 140) };
            _btnEmptyAdd.Click += OnPickWindowClick;
            _emptyStatePanel.Controls.AddRange(new Control[] { _lblEmptyTitle, _lblEmptyDesc, _btnEmptyAdd });

            _profileDetailCard = new RoundedCardPanel { Location = new Point(24, 68), Size = new Size(640, 470), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _pbProfileIcon = new PictureBox { Size = new Size(32, 32), Location = new Point(16, 16), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.InputBg };
            _lblProfileName = new Label { Font = Theme.FontTitle, ForeColor = Theme.TextPrimary, AutoSize = false, Size = new Size(320, 22), Location = new Point(58, 12) };
            _lblProfilePath = new Label { Font = Theme.FontSmall, ForeColor = Theme.TextMuted, AutoSize = false, Size = new Size(320, 16), Location = new Point(58, 34) };
            _lblProfileUsage = new Label { Font = Theme.FontSmall, ForeColor = Theme.ActiveGreen, AutoSize = false, Size = new Size(320, 16), Location = new Point(58, 49) };

            _btnPreview = new FlatButton { ButtonStyle = FlatButtonStyle.Secondary, Size = new Size(96, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(_profileDetailCard.Width - 208, 16) };
            _btnPreview.Click += (s, e) =>
            {
                if (GetSelectedProfile() == null) return;
                _isPreviewing = !_isPreviewing;
                _btnPreview.Text = _isPreviewing ? L.Get("StopPreview") : L.Get("Preview");
                _btnPreview.ButtonStyle = _isPreviewing ? FlatButtonStyle.Primary : FlatButtonStyle.Secondary;
                ApplyCurrentForegroundState(true);
            };

            _btnDeleteProfile = new FlatButton { ButtonStyle = FlatButtonStyle.DangerGhost, Size = new Size(88, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(_profileDetailCard.Width - 104, 16) };
            _btnDeleteProfile.Click += OnRemoveProfileClick;

            int rowW = _profileDetailCard.Width - 32;
            _lblPerMonitor = new Label { Font = Theme.FontBody, ForeColor = Theme.TextSecondary, AutoSize = true, Location = new Point(16, 66) };
            _tglPerMonitor = new FlatToggle { Location = new Point(_profileDetailCard.Width - 58, 64), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _tglPerMonitor.CheckedChanged += (s, e) =>
            {
                if (_suppressEvents) return;
                var pr = GetSelectedProfile();
                if (pr == null) return;
                pr.PerMonitorEnabled = _tglPerMonitor.Checked;
                EnsureMonitorSettingsInitialized(pr);
                _config.Save();
                SyncSelectedProfileControls();
                ApplyCurrentForegroundState(true);
            };

            _lblSelectMonitor = new Label { Font = Theme.FontBody, ForeColor = Theme.TextSecondary, AutoSize = true, Location = new Point(16, 98) };
            _cmbMonitors = new DarkDropdown
            {
                Location = new Point(_profileDetailCard.Width - 236, 94),
                Size = new Size(220, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _cmbMonitors.SelectedIndexChanged += (s, e) => { if (!_suppressEvents) SyncSelectedProfileSlidersOnly(); };

            _rowProfSat = CreateSliderRow(16, 130, rowW, _backend.MinLevel, _backend.MaxLevel, _backend.DefaultLevel);
            _rowProfSat.ValueChanged += (s, e) => UpdateProfileOrMonitorSetting((p, m) => { if (m != null) m.Saturation = _rowProfSat.Value; else p.TargetSaturation = _rowProfSat.Value; });

            _rowProfFilterSat = CreateSliderRow(16, 166, rowW, ColorFilterHelper.MinPercent, ColorFilterHelper.MaxPercent, ColorFilterHelper.DefaultPercent);
            _rowProfFilterSat.UnitSuffix = "%";
            _rowProfFilterSat.ValueChanged += (s, e) => SaveSelectedProfileChange(p => p.TargetFilterSaturation = _rowProfFilterSat.Value);

            _rowProfCon = CreateSliderRow(16, 202, rowW, _backend.MinContrast, _backend.MaxContrast, _backend.DefaultContrast);
            _rowProfCon.UnsupportedReason = _backend.SupportsContrastBrightness ? string.Empty : L.Get("UnsupportedOnNvidia");
            _rowProfCon.ValueChanged += (s, e) => UpdateProfileOrMonitorSetting((p, m) => { if (m != null) m.Contrast = _rowProfCon.Value; else p.TargetContrast = _rowProfCon.Value; });

            _rowProfBri = CreateSliderRow(16, 238, rowW, _backend.MinBrightness, _backend.MaxBrightness, _backend.DefaultBrightness);
            _rowProfBri.UnsupportedReason = _backend.SupportsContrastBrightness ? string.Empty : L.Get("UnsupportedOnNvidia");
            _rowProfBri.ValueChanged += (s, e) => UpdateProfileOrMonitorSetting((p, m) => { if (m != null) m.Brightness = _rowProfBri.Value; else p.TargetBrightness = _rowProfBri.Value; });

            _rowProfGam = CreateSliderRow(16, 274, rowW, 50, 250, 100);
            _rowProfGam.ValueChanged += (s, e) => UpdateProfileOrMonitorSetting((p, m) => { if (m != null) m.Gamma = _rowProfGam.Value; else p.TargetGamma = _rowProfGam.Value; });

            _rowProfTemp = CreateSliderRow(16, 310, rowW, _backend.MinColorTemp, _backend.MaxColorTemp, _backend.DefaultColorTemp);
            _rowProfTemp.UnitSuffix = "K";
            _rowProfTemp.UnsupportedReason = _backend.SupportsColorTemp ? string.Empty : (_backend.VendorName == "NVIDIA" ? L.Get("UnsupportedOnNvidia") : string.Empty);
            _rowProfTemp.ValueChanged += (s, e) => UpdateProfileOrMonitorSetting((p, m) => { if (m != null) m.ColorTemp = _rowProfTemp.Value; else p.TargetColorTemp = _rowProfTemp.Value; });

            _lblPrimary = new Label { Font = Theme.FontBody, ForeColor = Theme.TextSecondary, AutoSize = true, Location = new Point(16, 358) };
            _tglPrimaryOnly = new FlatToggle { Location = new Point(_profileDetailCard.Width - 58, 356), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _tglPrimaryOnly.CheckedChanged += (s, e) => SaveSelectedProfileChange(p => p.PrimaryMonitorOnly = _tglPrimaryOnly.Checked);

            _lblMatchFullPath = new Label { Font = Theme.FontBody, ForeColor = Theme.TextSecondary, AutoSize = true, Location = new Point(16, 392) };
            _tglMatchFullPath = new FlatToggle { Location = new Point(_profileDetailCard.Width - 58, 390), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _tglMatchFullPath.CheckedChanged += (s, e) => SaveSelectedProfileChange(p => p.MatchFullPath = _tglMatchFullPath.Checked);

            _lblFullscreenOnly = new Label { Font = Theme.FontBody, ForeColor = Theme.TextSecondary, AutoSize = true, Location = new Point(16, 426) };
            _tglFullscreenOnly = new FlatToggle { Location = new Point(_profileDetailCard.Width - 58, 424), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _tglFullscreenOnly.CheckedChanged += (s, e) => SaveSelectedProfileChange(p => p.FullscreenOnly = _tglFullscreenOnly.Checked);

            _infoPerMonitor = new InfoBadge();
            _infoPrimary = new InfoBadge();
            _infoMatchFullPath = new InfoBadge();
            _infoFullscreenOnly = new InfoBadge();

            _profileDetailCard.Controls.AddRange(new Control[]
            {
                _pbProfileIcon, _lblProfileName, _lblProfilePath, _lblProfileUsage, _btnPreview, _btnDeleteProfile,
                _lblPerMonitor, _infoPerMonitor, _tglPerMonitor, _lblSelectMonitor, _cmbMonitors,
                _rowProfSat, _rowProfFilterSat, _rowProfCon, _rowProfBri, _rowProfGam, _rowProfTemp,
                _lblPrimary, _infoPrimary, _tglPrimaryOnly, _lblMatchFullPath, _infoMatchFullPath, _tglMatchFullPath,
                _lblFullscreenOnly, _infoFullscreenOnly, _tglFullscreenOnly
            });

            _pageProfiles.Controls.AddRange(new Control[] { _lblProfilesHero, _btnImportProfile, _btnExportProfile, _emptyStatePanel, _profileDetailCard });
            _contentHost.Controls.Add(_pageProfiles);
        }

        private void BuildPreviewPage()
        {
            _pagePreview = CreatePagePanel();
            _lblPreviewHero = CreateHeroLabel();

            var compareCard = new RoundedCardPanel { Location = new Point(24, 68), Size = new Size(640, 434), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _compareView = new BeforeAfterCompareControl
            {
                Location = new Point(16, 16),
                Size = new Size(compareCard.Width - 32, 310),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            LoadDefaultPreviewImage();

            _lblCompareHint = new Label
            {
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                AutoSize = true,
                Location = new Point(16, 333)
            };

            _btnPrevLive = new FlatButton { ButtonStyle = FlatButtonStyle.Secondary, Size = new Size(116, 28), Location = new Point(16, 354), Font = Theme.FontSmall };
            _btnPrevLive.Click += (s, e) => { _previewSampleMode = "live"; UpdatePreviewVisuals(_lastColorState); };

            _btnPrevFps = new FlatButton { ButtonStyle = FlatButtonStyle.Secondary, Size = new Size(122, 28), Location = new Point(138, 354), Font = Theme.FontSmall };
            _btnPrevFps.Click += (s, e) => { _previewSampleMode = "fps"; UpdatePreviewVisuals(_lastColorState); };

            _btnPrevOverdrive = new FlatButton { ButtonStyle = FlatButtonStyle.Primary, Size = new Size(106, 28), Location = new Point(266, 354), Font = Theme.FontSmall };
            _btnPrevOverdrive.Click += (s, e) => { _previewSampleMode = "overdrive"; UpdatePreviewVisuals(_lastColorState); };

            _btnBeforeAfter = new FlatButton { ButtonStyle = FlatButtonStyle.Secondary, Size = new Size(194, 30), Location = new Point(16, 390) };
            _btnBeforeAfter.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _isHoldingBeforeAfter = true;
                    _backend.SetDisplayColor(_backend.DefaultLevel, _backend.DefaultContrast, _backend.DefaultBrightness, 100, _backend.DefaultColorTemp, false, Screen.PrimaryScreen);
                    ColorFilterHelper.Apply(ColorFilterHelper.DefaultPercent);
                    _largePreviewStrip.UpdateValues(100, _backend.DefaultContrast, _backend.DefaultBrightness, 100, _backend.DefaultColorTemp, ColorFilterHelper.DefaultPercent);
                    _compareView.UpdateValues(100, _backend.DefaultContrast, _backend.DefaultBrightness, 100, _backend.DefaultColorTemp, ColorFilterHelper.DefaultPercent);
                }
            };
            _btnBeforeAfter.MouseUp += (s, e) =>
            {
                if (_isHoldingBeforeAfter)
                {
                    _isHoldingBeforeAfter = false;
                    ApplyCurrentForegroundState(true);
                }
            };
            _btnBeforeAfter.MouseLeave += (s, e) =>
            {
                if (_isHoldingBeforeAfter)
                {
                    _isHoldingBeforeAfter = false;
                    ApplyCurrentForegroundState(true);
                }
            };

            _btnLoadPreviewImg = new FlatButton { ButtonStyle = FlatButtonStyle.Secondary, Size = new Size(120, 30), Location = new Point(218, 390) };
            _btnLoadPreviewImg.Click += OnLoadPreviewImageClick;

            _btnResetPreviewImg = new FlatButton { ButtonStyle = FlatButtonStyle.Secondary, Size = new Size(102, 30), Location = new Point(346, 390) };
            _btnResetPreviewImg.Click += (s, e) => LoadDefaultPreviewImage();

            compareCard.Controls.AddRange(new Control[]
            {
                _compareView, _lblCompareHint,
                _btnPrevLive, _btnPrevFps, _btnPrevOverdrive,
                _btnBeforeAfter, _btnLoadPreviewImg, _btnResetPreviewImg
            });

            var stripCard = new RoundedCardPanel { Location = new Point(24, 516), Size = new Size(640, 140), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _largePreviewStrip = new ColorPreviewStrip { Location = new Point(16, 16), Size = new Size(stripCard.Width - 32, 108), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            stripCard.Controls.Add(_largePreviewStrip);

            _pagePreview.Controls.AddRange(new Control[] { _lblPreviewHero, compareCard, stripCard });
            _contentHost.Controls.Add(_pagePreview);
        }

        private void LoadDefaultPreviewImage()
        {
            if (_compareView == null && _liveShowcaseView == null) return;
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Z4Vibrance.preview_sample.jpg"))
                {
                    if (stream != null)
                    {
                        using (var img = Image.FromStream(stream))
                        {
                            if (_compareView != null) _compareView.SetImage(img);
                            if (_liveShowcaseView != null) _liveShowcaseView.SetImage(img);
                        }
                        return;
                    }
                }
            }
            catch { }

            try
            {
                string fallback = Path.Combine(Application.StartupPath, "assets", "preview_sample.jpg");
                if (File.Exists(fallback))
                {
                    using (var img = Image.FromFile(fallback))
                    {
                        if (_compareView != null) _compareView.SetImage(img);
                        if (_liveShowcaseView != null) _liveShowcaseView.SetImage(img);
                    }
                }
            }
            catch { }
        }

        private void OnLoadPreviewImageClick(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog { Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp" })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        using (var img = Image.FromFile(dlg.FileName))
                        {
                            if (_compareView != null) _compareView.SetImage(img);
                            if (_liveShowcaseView != null) _liveShowcaseView.SetImage(img);
                        }
                    }
                    catch { }
                }
            }
        }

        private int GetNormalizedSatPercent(int sat)
        {
            if (_backend.MaxLevel >= 200 && _backend.DefaultLevel >= 50)
                return sat;

            int range = Math.Max(1, _backend.MaxLevel - _backend.DefaultLevel);
            if (sat >= _backend.DefaultLevel)
                return 100 + (int)Math.Round((sat - _backend.DefaultLevel) / (double)range * 100.0);

            int negRange = Math.Max(1, _backend.DefaultLevel - _backend.MinLevel);
            return Math.Max(0, (int)Math.Round((sat - _backend.MinLevel) / (double)negRange * 100.0));
        }

        private void UpdatePreviewVisuals(ColorState st)
        {
            int normSat = GetNormalizedSatPercent(st.Saturation);
            _miniPreviewStrip.UpdateValues(normSat, st.Contrast, st.Brightness, st.Gamma, st.ColorTemp, st.FilterSaturation);
            if (_liveShowcaseView != null && _pageGeneral != null && _pageGeneral.Visible)
                _liveShowcaseView.UpdateValues(normSat, st.Contrast, st.Brightness, st.Gamma, st.ColorTemp, st.FilterSaturation, normSat, st.FilterSaturation);
            if (_pagePreview != null && _pagePreview.Visible)
            {
                _largePreviewStrip.UpdateValues(normSat, st.Contrast, st.Brightness, st.Gamma, st.ColorTemp, st.FilterSaturation);

                bool isNeutral = normSat == 100 && st.FilterSaturation == 100 &&
                                 st.Contrast == 100 && st.Brightness == 0 &&
                                 st.Gamma == 100 && st.ColorTemp == 6500;

                string effectiveMode = _previewSampleMode == "auto"
                    ? (isNeutral ? "overdrive" : "live")
                    : _previewSampleMode;

                if (_btnPrevLive != null) _btnPrevLive.ButtonStyle = effectiveMode == "live" ? FlatButtonStyle.Primary : FlatButtonStyle.Secondary;
                if (_btnPrevFps != null) _btnPrevFps.ButtonStyle = effectiveMode == "fps" ? FlatButtonStyle.Primary : FlatButtonStyle.Secondary;
                if (_btnPrevOverdrive != null) _btnPrevOverdrive.ButtonStyle = effectiveMode == "overdrive" ? FlatButtonStyle.Primary : FlatButtonStyle.Secondary;

                if (_compareView != null)
                {
                    if (effectiveMode == "fps")
                        _compareView.UpdateValues(180, 110, 5, 110, 6500, 115, normSat, st.FilterSaturation);
                    else if (effectiveMode == "overdrive")
                        _compareView.UpdateValues(220, 108, 2, 106, 6500, 150, normSat, st.FilterSaturation);
                    else
                        _compareView.UpdateValues(normSat, st.Contrast, st.Brightness, st.Gamma, st.ColorTemp, st.FilterSaturation, normSat, st.FilterSaturation);
                }
            }
        }

        private void BuildSettingsPage()
        {
            _pageSettings = CreatePagePanel();
            _lblSettingsHero = CreateHeroLabel();

            var appCard = new RoundedCardPanel { Location = new Point(24, 68), Size = new Size(640, 224), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _lblAppearanceCard = new Label { Font = Theme.FontSection, ForeColor = Theme.TextMuted, AutoSize = true, Location = new Point(16, 14) };

            _lblAutoTrack = new Label { Font = Theme.FontBody, ForeColor = Theme.TextPrimary, AutoSize = true, Location = new Point(16, 44) };
            _tglEnabled = new FlatToggle { Location = new Point(appCard.Width - 58, 42), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _tglEnabled.CheckedChanged += (s, e) => { if (_suppressEvents) return; _config.Enabled = _tglEnabled.Checked; _config.Save(); UpdateTrayMenuState(); UpdateStatusBar(); ApplyCurrentForegroundState(); ShowToast(L.Get("ToastSettingsSaved"), Theme.ActiveGreen); };

            _lblStartup = new Label { Font = Theme.FontBody, ForeColor = Theme.TextPrimary, AutoSize = true, Location = new Point(16, 80) };
            _tglStartup = new FlatToggle { Location = new Point(appCard.Width - 58, 78), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _tglStartup.CheckedChanged += (s, e) => { if (_suppressEvents) return; _config.StartWithWindows = _tglStartup.Checked; _config.Save(); SetStartupRegistry(_config.StartWithWindows); ShowToast(L.Get("ToastSettingsSaved"), Theme.ActiveGreen); };

            _lblTrayClose = new Label { Font = Theme.FontBody, ForeColor = Theme.TextPrimary, AutoSize = true, Location = new Point(16, 116) };
            _tglTrayOnClose = new FlatToggle { Location = new Point(appCard.Width - 58, 114), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _tglTrayOnClose.CheckedChanged += (s, e) => { if (_suppressEvents) return; _config.MinimizeToTrayOnClose = _tglTrayOnClose.Checked; _config.Save(); ShowToast(L.Get("ToastSettingsSaved"), Theme.ActiveGreen); };

            _lblOled = new Label { Font = Theme.FontBody, ForeColor = Theme.TextPrimary, AutoSize = true, Location = new Point(16, 152) };
            _tglOled = new FlatToggle { Location = new Point(appCard.Width - 58, 150), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _tglOled.CheckedChanged += (s, e) =>
            {
                if (_suppressEvents) return;
                _config.OledBlackTheme = _tglOled.Checked;
                _config.Save();
                Theme.ApplyPalette(_config.OledBlackTheme);
                RefreshThemeColors();
                ShowToast(L.Get("ToastSettingsSaved"), Theme.ActiveGreen);
            };

            _lblLangRow = new Label { Font = Theme.FontBody, ForeColor = Theme.TextPrimary, AutoSize = true, Location = new Point(16, 188) };
            _btnLangSwitch = new FlatButton { Size = new Size(92, 28), Location = new Point(appCard.Width - 108, 183), Anchor = AnchorStyles.Top | AnchorStyles.Right, ButtonStyle = FlatButtonStyle.Secondary };
            _btnLangSwitch.Click += (s, e) =>
            {
                _config.Language = L.CurrentLanguage == "tr" ? "en" : "tr";
                L.CurrentLanguage = _config.Language;
                _config.Save();
                ApplyLanguageTexts();
                SyncSelectedProfileControls();
                ApplyCurrentForegroundState(true);
            };

            _infoAutoTrack = new InfoBadge();
            _infoStartup = new InfoBadge();
            _infoTrayClose = new InfoBadge();
            _infoOled = new InfoBadge();

            appCard.Controls.AddRange(new Control[]
            {
                _lblAppearanceCard, _lblAutoTrack, _infoAutoTrack, _tglEnabled, _lblStartup, _infoStartup, _tglStartup,
                _lblTrayClose, _infoTrayClose, _tglTrayOnClose, _lblOled, _infoOled, _tglOled,
                _lblLangRow, _btnLangSwitch
            });

            var schedCard = new RoundedCardPanel { Location = new Point(24, 306), Size = new Size(640, 200), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _lblScheduleCard = new Label { Font = Theme.FontSection, ForeColor = Theme.TextMuted, AutoSize = true, Location = new Point(16, 14) };
            _lblEnableSchedule = new Label { Font = Theme.FontBody, ForeColor = Theme.TextPrimary, AutoSize = true, Location = new Point(16, 44) };
            _tglSchedule = new FlatToggle { Location = new Point(schedCard.Width - 58, 42), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _tglSchedule.CheckedChanged += (s, e) =>
            {
                if (_suppressEvents) return;
                EnsureScheduleRuleExists();
                _config.ScheduleEnabled = _tglSchedule.Checked;
                _config.Save();
                if (_config.ScheduleEnabled) _scheduleTimer.Start(); else _scheduleTimer.Stop();
                ApplyCurrentForegroundState();
                ShowToast(L.Get("ToastSettingsSaved"), Theme.ActiveGreen);
            };

            _lblSchedStart = new Label { Font = Theme.FontBody, ForeColor = Theme.TextSecondary, AutoSize = true, Location = new Point(16, 80) };
            _numStartHour = CreateTimeBox(148, 76, 23);
            _numStartMin = CreateTimeBox(228, 76, 59);
            _lblSchedEnd = new Label { Font = Theme.FontBody, ForeColor = Theme.TextSecondary, AutoSize = true, Location = new Point(322, 80) };
            _numEndHour = CreateTimeBox(446, 76, 23);
            _numEndMin = CreateTimeBox(526, 76, 59);

            EventHandler onSchedTimeChanged = (s, e) =>
            {
                if (_suppressEvents) return;
                EnsureScheduleRuleExists();
                var r = _config.Schedules[0];
                r.StartHour = (int)_numStartHour.Value;
                r.StartMinute = (int)_numStartMin.Value;
                r.EndHour = (int)_numEndHour.Value;
                r.EndMinute = (int)_numEndMin.Value;
                _config.Save();
                ApplyCurrentForegroundState();
            };
            _numStartHour.ValueChanged += onSchedTimeChanged;
            _numStartMin.ValueChanged += onSchedTimeChanged;
            _numEndHour.ValueChanged += onSchedTimeChanged;
            _numEndMin.ValueChanged += onSchedTimeChanged;

            _rowSchedTemp = CreateSliderRow(16, 114, schedCard.Width - 32, _backend.MinColorTemp, _backend.MaxColorTemp, 5500);
            _rowSchedTemp.UnitSuffix = "K";
            _rowSchedTemp.UnsupportedReason = _backend.SupportsColorTemp ? string.Empty : (_backend.VendorName == "NVIDIA" ? L.Get("UnsupportedOnNvidia") : string.Empty);
            _rowSchedTemp.ValueChanged += (s, e) => { if (_suppressEvents) return; EnsureScheduleRuleExists(); _config.Schedules[0].ColorTemp = _rowSchedTemp.Value; _config.Save(); ApplyCurrentForegroundState(); };

            _rowSchedSat = CreateSliderRow(16, 152, schedCard.Width - 32, _backend.MinLevel, _backend.MaxLevel, _backend.DefaultLevel);
            _rowSchedSat.ValueChanged += (s, e) => { if (_suppressEvents) return; EnsureScheduleRuleExists(); _config.Schedules[0].Saturation = _rowSchedSat.Value; _config.Save(); ApplyCurrentForegroundState(); };

            _infoSchedule = new InfoBadge();
            schedCard.Controls.AddRange(new Control[]
            {
                _lblScheduleCard, _lblEnableSchedule, _infoSchedule, _tglSchedule,
                _lblSchedStart, _numStartHour, _numStartMin, _lblSchedEnd, _numEndHour, _numEndMin,
                _rowSchedTemp, _rowSchedSat
            });

            var diagCard = new RoundedCardPanel { Location = new Point(24, 520), Size = new Size(640, 106), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _lblDiagCard = new Label { Font = Theme.FontSection, ForeColor = Theme.TextMuted, AutoSize = true, Location = new Point(16, 14) };
            _lblDiagInfo = new Label { Font = Theme.FontSmall, ForeColor = Theme.TextSecondary, AutoSize = false, Size = new Size(460, 48), Location = new Point(16, 40) };
            _btnOpenLogs = new FlatButton { ButtonStyle = FlatButtonStyle.Secondary, Size = new Size(130, 32), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(diagCard.Width - 146, 44) };
            _btnOpenLogs.Click += (s, e) => Logger.OpenLogFolder();
            diagCard.Controls.AddRange(new Control[] { _lblDiagCard, _lblDiagInfo, _btnOpenLogs });

            _pageSettings.Controls.AddRange(new Control[] { _lblSettingsHero, appCard, schedCard, diagCard });
            _contentHost.Controls.Add(_pageSettings);
        }

        private static DarkStepperBox CreateTimeBox(int x, int y, int maxVal)
        {
            return new DarkStepperBox
            {
                Location = new Point(x, y),
                Size = new Size(74, 28),
                Minimum = 0,
                Maximum = maxVal
            };
        }

        private void BuildAboutPage()
        {
            _pageAbout = CreatePagePanel();
            _lblAboutHero = CreateHeroLabel();

            var card = new RoundedCardPanel { Location = new Point(24, 68), Size = new Size(640, 240), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _lblAboutVersion = new Label { Font = Theme.FontTitle, ForeColor = Theme.TextPrimary, AutoSize = true, Location = new Point(20, 22) };
            _lblAboutGpu = new Label { Font = Theme.FontBody, ForeColor = Theme.TextSecondary, AutoSize = true, Location = new Point(20, 58) };
            _lblAboutApi = new Label { Font = Theme.FontBody, ForeColor = Theme.TextSecondary, AutoSize = true, Location = new Point(20, 86) };
            _lblAboutPrivacy = new Label { Font = Theme.FontBody, ForeColor = Theme.ActiveGreen, AutoSize = false, Size = new Size(590, 42), Location = new Point(20, 122), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _btnOpenWizard = new FlatButton { ButtonStyle = FlatButtonStyle.Secondary, Size = new Size(190, 32), Location = new Point(20, 178) };
            _btnOpenWizard.Click += (s, e) =>
            {
                using (var wiz = new OnboardingForm(_backend))
                {
                    wiz.ShowDialog(this);
                }
                ApplyCurrentForegroundState(true);
            };

            card.Controls.AddRange(new Control[] { _lblAboutVersion, _lblAboutGpu, _lblAboutApi, _lblAboutPrivacy, _btnOpenWizard });
            _pageAbout.Controls.AddRange(new Control[] { _lblAboutHero, card });
            _contentHost.Controls.Add(_pageAbout);
        }

        private Panel CreatePagePanel()
        {
            return new WheelScrollPanel
            {
                Dock = DockStyle.Fill,
                Size = new Size(688, 628),
                BackColor = Theme.ContentBg,
                Visible = false
            };
        }

        private static Label CreateHeroLabel()
        {
            return new Label
            {
                Font = Theme.FontHero,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(24, 18)
            };
        }

        private static SettingSliderRow CreateSliderRow(int x, int y, int width, int min, int max, int defVal)
        {
            return new SettingSliderRow
            {
                Location = new Point(x, y),
                Size = new Size(width, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Minimum = min,
                Maximum = max,
                DefaultValue = defVal
            };
        }

        private void SwitchTab(NavIconKind tab)
        {
            _activeTab = tab;
            _navGeneral.Active = tab == NavIconKind.General;
            _navProfiles.Active = tab == NavIconKind.Profiles;
            _navPreview.Active = tab == NavIconKind.Preview;
            _navSettings.Active = tab == NavIconKind.Settings;
            _navAbout.Active = tab == NavIconKind.About;

            _pageGeneral.Visible = tab == NavIconKind.General;
            _pageProfiles.Visible = tab == NavIconKind.Profiles;
            _pagePreview.Visible = tab == NavIconKind.Preview;
            _pageSettings.Visible = tab == NavIconKind.Settings;
            _pageAbout.Visible = tab == NavIconKind.About;
            var activePage = tab == NavIconKind.General ? _pageGeneral
                : tab == NavIconKind.Profiles ? _pageProfiles
                : tab == NavIconKind.Preview ? _pagePreview
                : tab == NavIconKind.Settings ? _pageSettings
                : _pageAbout;
            var wsp = activePage as WheelScrollPanel;
            if (wsp != null) wsp.ResetScroll();
            LayoutAllPages();
            if (tab == NavIconKind.Preview || tab == NavIconKind.General)
                UpdatePreviewVisuals(_lastColorState);
        }

        private void RefreshThemeColors()
        {
            BackColor = Theme.RailBg;
            _titleBar.BackColor = Theme.RailBg;
            _railPanel.BackColor = Theme.RailBg;
            _statusBar.BackColor = Theme.RailBg;
            _statusDot.BackColor = Theme.RailBg;
            _sidebarPanel.BackColor = Theme.PanelBg;
            _profileList.BackColor = Theme.PanelBg;
            _contentHost.BackColor = Theme.ContentBg;
            _pageGeneral.BackColor = Theme.ContentBg;
            _pageProfiles.BackColor = Theme.ContentBg;
            _pagePreview.BackColor = Theme.ContentBg;
            _pageSettings.BackColor = Theme.ContentBg;
            _pageAbout.BackColor = Theme.ContentBg;
            if (_presetsFlow != null) _presetsFlow.BackColor = Theme.CardBg;
            Invalidate(true);
        }

        private void ScheduleConfigSave()
        {
            _pendingConfigSave = true;
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();
        }

        private void ScheduleHardwareApply(Action hwAction)
        {
            _pendingHwAction = hwAction;
            if (!_hwThrottleTimer.Enabled)
                _hwThrottleTimer.Start();
        }

        private void SaveSelectedProfileChange(Action<AppProfile> mutator)
        {
            if (_suppressEvents) return;
            var pr = GetSelectedProfile();
            if (pr == null) return;
            PushProfileUndo(pr);
            mutator(pr);
            ScheduleConfigSave();
            ApplyCurrentForegroundState(true, true);
        }

        private void PushDesktopUndo()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastUndoPushUtc).TotalMilliseconds < 450)
                return;
            _lastUndoPushUtc = now;

            _undoRedo.PushUndo(new ColorUndoSnapshot
            {
                TargetKey = "DESKTOP",
                Saturation = _config.DesktopSaturation,
                FilterSaturation = _config.DesktopFilterSaturation,
                Contrast = _config.DesktopContrast,
                Brightness = _config.DesktopBrightness,
                Gamma = _config.DesktopGamma,
                ColorTemp = _config.DesktopColorTemp
            });
            UpdateUndoRedoButtons();
        }

        private void PushProfileUndo(AppProfile pr)
        {
            if (pr == null) return;
            var now = DateTime.UtcNow;
            if ((now - _lastUndoPushUtc).TotalMilliseconds < 450)
                return;
            _lastUndoPushUtc = now;

            _undoRedo.PushUndo(new ColorUndoSnapshot
            {
                TargetKey = pr.Id,
                Saturation = pr.TargetSaturation,
                FilterSaturation = pr.TargetFilterSaturation,
                Contrast = pr.TargetContrast,
                Brightness = pr.TargetBrightness,
                Gamma = pr.TargetGamma,
                ColorTemp = pr.TargetColorTemp
            });
            UpdateUndoRedoButtons();
        }

        private ColorUndoSnapshot CaptureSnapshotForKey(string targetKey)
        {
            if (targetKey == "DESKTOP")
            {
                return new ColorUndoSnapshot
                {
                    TargetKey = "DESKTOP",
                    Saturation = _config.DesktopSaturation,
                    FilterSaturation = _config.DesktopFilterSaturation,
                    Contrast = _config.DesktopContrast,
                    Brightness = _config.DesktopBrightness,
                    Gamma = _config.DesktopGamma,
                    ColorTemp = _config.DesktopColorTemp
                };
            }
            foreach (var p in _config.Profiles)
            {
                if (p.Id == targetKey)
                {
                    return new ColorUndoSnapshot
                    {
                        TargetKey = p.Id,
                        Saturation = p.TargetSaturation,
                        FilterSaturation = p.TargetFilterSaturation,
                        Contrast = p.TargetContrast,
                        Brightness = p.TargetBrightness,
                        Gamma = p.TargetGamma,
                        ColorTemp = p.TargetColorTemp
                    };
                }
            }
            return null;
        }

        private void ApplyUndoSnapshot(ColorUndoSnapshot snap)
        {
            if (snap == null) return;
            if (snap.TargetKey == "DESKTOP")
            {
                _config.DesktopSaturation = ClampSat(snap.Saturation);
                _config.DesktopFilterSaturation = ClampFilterSat(snap.FilterSaturation);
                _config.DesktopContrast = ClampCon(snap.Contrast);
                _config.DesktopBrightness = ClampBri(snap.Brightness);
                _config.DesktopGamma = ClampGam(snap.Gamma);
                _config.DesktopColorTemp = ClampTemp(snap.ColorTemp);
                _config.Save();
                SyncGeneralControls();
                ApplyCurrentForegroundState(true);
                return;
            }
            foreach (var p in _config.Profiles)
            {
                if (p.Id == snap.TargetKey)
                {
                    p.TargetSaturation = ClampSat(snap.Saturation);
                    p.TargetFilterSaturation = ClampFilterSat(snap.FilterSaturation);
                    p.TargetContrast = ClampCon(snap.Contrast);
                    p.TargetBrightness = ClampBri(snap.Brightness);
                    p.TargetGamma = ClampGam(snap.Gamma);
                    p.TargetColorTemp = ClampTemp(snap.ColorTemp);
                    _config.Save();
                    SyncSelectedProfileControls();
                    ApplyCurrentForegroundState(true);
                    return;
                }
            }
        }

        private void PerformUndo()
        {
            if (!_undoRedo.CanUndo) return;
            // Mevcut durumu redo için al
            string key = (_activeTab == NavIconKind.Profiles && GetSelectedProfile() != null)
                ? GetSelectedProfile().Id
                : "DESKTOP";
            var snap = _undoRedo.PopUndo(CaptureSnapshotForKey(key));
            ApplyUndoSnapshot(snap);
            UpdateUndoRedoButtons();
        }

        private void PerformRedo()
        {
            if (!_undoRedo.CanRedo) return;
            string key = (_activeTab == NavIconKind.Profiles && GetSelectedProfile() != null)
                ? GetSelectedProfile().Id
                : "DESKTOP";
            var snap = _undoRedo.PopRedo(CaptureSnapshotForKey(key));
            ApplyUndoSnapshot(snap);
            UpdateUndoRedoButtons();
        }

        private void UpdateUndoRedoButtons()
        {
            if (_btnUndo != null) _btnUndo.Enabled = _undoRedo.CanUndo;
            if (_btnRedo != null) _btnRedo.Enabled = _undoRedo.CanRedo;
        }

        private void RefreshPresetsFlow()
        {
            if (_presetsFlow == null) return;
            _presetsFlow.Controls.Clear();
            var all = PresetManager.GetBuiltInPresets(_backend);
            if (_config.UserPresets != null)
                all.AddRange(_config.UserPresets);

            foreach (var preset in all)
            {
                var captured = preset;
                var btn = new FlatButton
                {
                    Text = captured.Name,
                    ButtonStyle = FlatButtonStyle.Secondary,
                    Size = new Size(114, 30),
                    Font = Theme.FontSmall,
                    Margin = new Padding(0, 0, 8, 8)
                };
                btn.Click += (s, e) => ApplyPresetToDesktop(captured);
                _presetsFlow.Controls.Add(btn);
            }
            if (_presetsFlow.Parent != null)
                LayoutPresetButtons(_presetsFlow.Width);
        }

        private void LayoutPresetButtons(int innerW)
        {
            if (_presetsFlow == null || _presetsFlow.Controls.Count == 0 || innerW < 120)
                return;

            int count = _presetsFlow.Controls.Count;
            int gap = 8;
            int cols = Math.Min(count, Math.Max(1, (innerW + gap) / 110));
            int baseW = (innerW - (cols - 1) * gap) / cols;
            int extra = (innerW - (cols - 1) * gap) % cols;

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                bool isLastInRow = (col == cols - 1) || (i == count - 1 && count < cols);
                int w = baseW + (col < extra ? 1 : 0);
                var btn = _presetsFlow.Controls[i];
                btn.Size = new Size(w, 30);
                btn.Margin = new Padding(0, 0, isLastInRow ? 0 : gap, 8);
            }

            int rows = (int)Math.Ceiling(count / (double)cols);
            _presetsFlow.Height = rows * 38;
            if (_presetsFlow.Parent != null)
                _presetsFlow.Parent.Height = 46 + rows * 38 + 10;
        }

        private void ApplyPresetToDesktop(ColorPreset preset)
        {
            if (preset == null) return;
            PushDesktopUndo();
            _config.DesktopSaturation = ClampSat(preset.Saturation);
            _config.DesktopFilterSaturation = ClampFilterSat(preset.FilterSaturation);
            _config.DesktopContrast = ClampCon(preset.Contrast);
            _config.DesktopBrightness = ClampBri(preset.Brightness);
            _config.DesktopGamma = ClampGam(preset.Gamma);
            _config.DesktopColorTemp = ClampTemp(preset.ColorTemp);
            _config.Save();
            SyncGeneralControls();
            ApplyCurrentForegroundState(true);
            ShowToast(preset.Name, Theme.Accent);
        }

        private void SaveCurrentAsPreset(bool fromProfile)
        {
            string name = PromptTextDialog(L.Get("SavePreset"), L.Get("EnterPresetName"), L.Get("DefaultPresetName"));
            if (string.IsNullOrWhiteSpace(name)) return;

            var preset = new ColorPreset
            {
                Name = name.Trim(),
                IsBuiltIn = false,
                Saturation = _config.DesktopSaturation,
                FilterSaturation = _config.DesktopFilterSaturation,
                Contrast = _config.DesktopContrast,
                Brightness = _config.DesktopBrightness,
                Gamma = _config.DesktopGamma,
                ColorTemp = _config.DesktopColorTemp
            };
            _config.UserPresets.Add(preset);
            _config.Save();
            RefreshPresetsFlow();
            ShowToast(L.Get("PresetSavedToast"), Theme.ActiveGreen);
        }

        private string PromptTextDialog(string title, string prompt, string initialValue)
        {
            using (var dlg = new Form())
            {
                dlg.FormBorderStyle = FormBorderStyle.None;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.Size = new Size(360, 150);
                dlg.BackColor = Theme.PanelBg;
                dlg.ShowInTaskbar = false;
                Theme.ApplyWindowCorners(dlg.Handle, dlg);

                var lbl = new Label { Text = prompt, Font = Theme.FontBodyBold, ForeColor = Theme.TextPrimary, Location = new Point(18, 18), AutoSize = true };
                var tb = new TextBox { Text = initialValue ?? string.Empty, Font = Theme.FontBody, BackColor = Theme.InputBg, ForeColor = Theme.TextPrimary, BorderStyle = BorderStyle.FixedSingle, Location = new Point(18, 48), Size = new Size(324, 26) };
                var btnOk = new FlatButton { Text = "OK", ButtonStyle = FlatButtonStyle.Primary, Size = new Size(88, 30), Location = new Point(254, 100) };
                var btnCancel = new FlatButton { Text = L.Get("Cancel"), ButtonStyle = FlatButtonStyle.Secondary, Size = new Size(88, 30), Location = new Point(158, 100) };

                btnOk.Click += (s, e) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };
                btnCancel.Click += (s, e) => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); };
                tb.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter) { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                    else if (e.KeyCode == Keys.Escape) { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); }
                };

                dlg.Controls.AddRange(new Control[] { lbl, tb, btnCancel, btnOk });
                return dlg.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
            }
        }

        private void EnsureScheduleRuleExists()
        {
            if (_config.Schedules == null)
                _config.Schedules = new List<ScheduleRule>();
            if (_config.Schedules.Count == 0)
                _config.Schedules.Add(new ScheduleRule { Saturation = _backend.DefaultLevel });
        }

        private void EnsureMonitorSettingsInitialized(AppProfile pr)
        {
            if (pr == null) return;
            if (pr.MonitorSettings == null)
                pr.MonitorSettings = new List<MonitorColorSetting>();

            foreach (var scr in Screen.AllScreens)
            {
                bool found = false;
                foreach (var ms in pr.MonitorSettings)
                {
                    if (string.Equals(ms.DeviceName, scr.DeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    pr.MonitorSettings.Add(new MonitorColorSetting
                    {
                        DeviceName = scr.DeviceName,
                        Enabled = true,
                        Saturation = pr.TargetSaturation,
                        Contrast = pr.TargetContrast,
                        Brightness = pr.TargetBrightness,
                        Gamma = pr.TargetGamma,
                        ColorTemp = pr.TargetColorTemp
                    });
                }
            }
        }

        private MonitorColorSetting GetSelectedMonitorSetting(AppProfile pr)
        {
            if (pr == null || !pr.PerMonitorEnabled || _cmbMonitors == null || _cmbMonitors.SelectedItem == null)
                return null;
            string dev = _cmbMonitors.SelectedItem.ToString();
            foreach (var ms in pr.MonitorSettings)
            {
                if (string.Equals(ms.DeviceName, dev, StringComparison.OrdinalIgnoreCase))
                    return ms;
            }
            return null;
        }

        private void UpdateProfileOrMonitorSetting(Action<AppProfile, MonitorColorSetting> mutator)
        {
            if (_suppressEvents) return;
            var pr = GetSelectedProfile();
            if (pr == null) return;
            PushProfileUndo(pr);
            var ms = GetSelectedMonitorSetting(pr);
            mutator(pr, ms);
            ScheduleConfigSave();
            ApplyCurrentForegroundState(true, true);
        }

        private enum CaptionKind { Minimize, Maximize, Close }

        private Panel CreateCaptionButton(CaptionKind kind)
        {
            int rightOffset = kind == CaptionKind.Close ? 44 : (kind == CaptionKind.Maximize ? 86 : 128);
            var p = new Panel
            {
                Size = new Size(42, Theme.TitleBarHeight),
                Location = new Point(_titleBar.Width - rightOffset, 0),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Theme.RailBg,
                Cursor = Cursors.Hand
            };
            bool hover = false;
            p.MouseEnter += (s, e) => { hover = true; p.Invalidate(); };
            p.MouseLeave += (s, e) => { hover = false; p.Invalidate(); };
            p.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Color bg = hover ? (kind == CaptionKind.Close ? Theme.DangerRed : Theme.CardHoverBg) : Theme.RailBg;
                g.Clear(bg);
                Color fg = (hover && kind == CaptionKind.Close) ? Color.White : Theme.TextSecondary;
                using (var pen = new Pen(fg, 1.5f))
                {
                    int cx = p.Width / 2;
                    int cy = p.Height / 2;
                    if (kind == CaptionKind.Close)
                    {
                        g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                        g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
                    }
                    else if (kind == CaptionKind.Maximize)
                    {
                        g.DrawRectangle(pen, cx - 5, cy - 5, 10, 10);
                    }
                    else
                    {
                        g.DrawLine(pen, cx - 5, cy + 2, cx + 5, cy + 2);
                    }
                }
            };
            return p;
        }

        private void AttachDrag(Control c)
        {
            c.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (e.Clicks == 2)
                    {
                        WindowState = (WindowState == FormWindowState.Maximized)
                            ? FormWindowState.Normal
                            : FormWindowState.Maximized;
                        return;
                    }
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
        }

        private void BuildTrayIcon()
        {
            _trayMenu = new ContextMenuStrip { Renderer = new DarkToolStripRenderer() };
            _trayOpenItem = new ToolStripMenuItem();
            _trayOpenItem.Click += (s, e) => RestoreFromTray();

            _trayProfilesItem = new ToolStripMenuItem();

            _trayToggleItem = new ToolStripMenuItem { CheckOnClick = false };
            _trayToggleItem.Click += (s, e) =>
            {
                _config.Enabled = !_config.Enabled;
                _config.Save();
                SyncSettingsControls();
                UpdateTrayMenuState();
                UpdateStatusBar();
                ApplyCurrentForegroundState();
            };

            _trayResetItem = new ToolStripMenuItem();
            _trayResetItem.Click += (s, e) => ResetAllColorsToDefault();

            _trayExitItem = new ToolStripMenuItem();
            _trayExitItem.Click += (s, e) =>
            {
                _forceExit = true;
                Close();
            };

            _trayMenu.Items.Add(_trayOpenItem);
            _trayMenu.Items.Add(_trayProfilesItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_trayToggleItem);
            _trayMenu.Items.Add(_trayResetItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_trayExitItem);

            _trayIcon = new NotifyIcon
            {
                Icon = _appIcon,
                Text = "Z4Vibrance",
                ContextMenuStrip = _trayMenu,
                Visible = true
            };
            _trayIcon.DoubleClick += (s, e) => RestoreFromTray();
            UpdateTrayMenuState();
        }

        private void BuildProfileContextMenu()
        {
            _profileCtxMenu = new ContextMenuStrip { Renderer = new DarkToolStripRenderer() };
            _ctxEdit = new ToolStripMenuItem();
            _ctxEdit.Click += (s, e) => { if (GetSelectedProfile() != null) SwitchTab(NavIconKind.Profiles); };

            _ctxRename = new ToolStripMenuItem();
            _ctxRename.Click += (s, e) => RenameSelectedProfile();

            _ctxDuplicate = new ToolStripMenuItem();
            _ctxDuplicate.Click += (s, e) => DuplicateSelectedProfile();

            _ctxDelete = new ToolStripMenuItem();
            _ctxDelete.Click += OnRemoveProfileClick;

            _profileCtxMenu.Items.AddRange(new ToolStripItem[] { _ctxEdit, _ctxRename, _ctxDuplicate, new ToolStripSeparator(), _ctxDelete });
            _profileList.ContextMenuStrip = _profileCtxMenu;
        }

        private void RenameSelectedProfile()
        {
            var pr = GetSelectedProfile();
            if (pr == null) return;
            string newName = PromptTextDialog(L.Get("CtxRename"), L.Get("RenamePrompt"), pr.Name);
            if (string.IsNullOrWhiteSpace(newName)) return;
            pr.Name = newName.Trim();
            _config.Save();
            RefreshProfileList(pr.Id);
            SyncSelectedProfileControls();
            ShowToast(L.Get("ToastSettingsSaved"), Theme.ActiveGreen);
        }

        private void OnExportProfileClick(object sender, EventArgs e)
        {
            var pr = GetSelectedProfile();
            if (pr == null) return;
            using (var dlg = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = pr.Name + ".json" })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (PresetManager.ExportProfile(pr, dlg.FileName))
                        ShowToast(L.Get("ToastExported"), Theme.ActiveGreen);
                }
            }
        }

        private void OnImportProfileClick(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog { Filter = "JSON (*.json)|*.json" })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var imported = PresetManager.ImportProfile(dlg.FileName);
                    if (imported == null)
                    {
                        ShowToast(L.Get("ImportFailed"), Theme.DangerRed);
                        return;
                    }
                    ClampProfileToBackend(imported);
                    _config.Profiles.Add(imported);
                    _config.Save();
                    RefreshProfileList(imported.Id);
                    SyncSelectedProfileControls();
                    SwitchTab(NavIconKind.Profiles);
                    ShowToast(L.Get("ToastImported"), Theme.ActiveGreen);
                }
            }
        }

        private void DuplicateSelectedProfile()
        {
            var src = GetSelectedProfile();
            if (src == null) return;
            var copy = new AppProfile
            {
                Name = src.Name + " " + L.Get("CopySuffix"),
                ExecutablePath = src.ExecutablePath,
                ProcessName = src.ProcessName,
                MatchFullPath = src.MatchFullPath,
                TargetSaturation = src.TargetSaturation,
                TargetFilterSaturation = src.TargetFilterSaturation,
                TargetContrast = src.TargetContrast,
                TargetBrightness = src.TargetBrightness,
                TargetGamma = src.TargetGamma,
                TargetColorTemp = src.TargetColorTemp,
                PrimaryMonitorOnly = src.PrimaryMonitorOnly,
                FullscreenOnly = src.FullscreenOnly,
                ApplyDelayMs = src.ApplyDelayMs,
                RevertDelayMs = src.RevertDelayMs
            };
            _config.Profiles.Add(copy);
            _config.Save();
            RefreshProfileList(copy.Id);
            SyncSelectedProfileControls();
            ShowToast(L.Get("ToastProfileAdded"), Theme.ActiveGreen);
        }

        private void UpdateTrayMenuState()
        {
            if (_trayToggleItem != null)
            {
                _trayToggleItem.Checked = _config.Enabled;
                _trayToggleItem.Text = _config.Enabled ? L.Get("TrayPause") : L.Get("TrayResume");
            }
            if (_trayProfilesItem != null)
            {
                _trayProfilesItem.DropDownItems.Clear();
                foreach (var p in _config.Profiles)
                {
                    var item = new ToolStripMenuItem(p.Name);
                    var captured = p;
                    item.Click += (s, e) =>
                    {
                        RestoreFromTray();
                        RefreshProfileList(captured.Id);
                        SwitchTab(NavIconKind.Profiles);
                    };
                    _trayProfilesItem.DropDownItems.Add(item);
                }
                _trayProfilesItem.Enabled = _config.Profiles.Count > 0;
            }
        }

        private void PlaceInfoBadge(InfoBadge badge, Label anchorLabel, string tipKey)
        {
            if (badge == null || anchorLabel == null) return;
            badge.Location = new Point(anchorLabel.Right + 6, anchorLabel.Top + 1);
            _toolTip.SetToolTip(badge, L.Get(tipKey));
        }

        private void UpdateStatusBar()
        {
            string gpuText = _backend.IsAvailable
                ? (_backend.VendorName + " — " + _backend.DeviceName)
                : L.Get("DriverDisconnected");
            _lblStatusGpu.Text = gpuText;
            _statusDot.Invalidate();

            _btnStatusPause.Text = _config.Enabled ? L.Get("Pause") : L.Get("Resume");
        }

        private void ResetAllColorsToDefault()
        {
            _suppressEvents = true;
            _config.DesktopSaturation = _backend.DefaultLevel;
            _config.DesktopFilterSaturation = ColorFilterHelper.DefaultPercent;
            _config.DesktopContrast = _backend.DefaultContrast;
            _config.DesktopBrightness = _backend.DefaultBrightness;
            _config.DesktopGamma = 100;
            _config.DesktopColorTemp = _backend.DefaultColorTemp;
            _config.Save();
            SyncGeneralControls();
            _suppressEvents = false;

            _isPreviewing = false;
            _backend.RestoreInitial();
            ColorFilterHelper.Reset();
            ApplyCurrentForegroundState(true);
            ShowToast(L.Get("ToastResetDone"), Theme.Accent);
        }

        private void ShowToast(string message, Color accent)
        {
            if (_toast == null) return;
            PositionToast();
            _toast.ShowMessage(message, accent);
        }

        private void PositionToast()
        {
            if (_toast == null || _contentHost == null) return;
            _toast.Location = new Point(_contentHost.Width - _toast.Width - 20, _contentHost.Height - _toast.Height - 16);
        }

        private void RestoreFromTray()
        {
            Show();
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();
        }

        private void HandleCloseAction()
        {
            if (_config.MinimizeToTrayOnClose && !_forceExit)
            {
                Hide();
                return;
            }
            _forceExit = true;
            Close();
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            CleanupSafety();
        }

        private void OnSystemDisplayOrPowerChanged(object sender, EventArgs e)
        {
            BeginInvoke(new Action(() => ApplyCurrentForegroundState(true)));
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                Logger.Info("Sistem uykudan uyandi, renk ayarlari yeniden uygulaniyor.");
                BeginInvoke(new Action(() => ApplyCurrentForegroundState(true)));
            }
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                Logger.Info("Oturum kilidi acildi, renk ayarlari yeniden uygulaniyor.");
                BeginInvoke(new Action(() => ApplyCurrentForegroundState(true)));
            }
        }

        private void FlushActiveProfileUsage()
        {
            if (_lastMatchedProfile != null)
            {
                long delta = (long)(DateTime.UtcNow - _activeProfileStartUtc).TotalSeconds;
                if (delta > 0)
                {
                    _lastMatchedProfile.UsageSeconds += delta;
                    _config.Save();
                }
                _activeProfileStartUtc = DateTime.UtcNow;
            }
        }

        public void CleanupSafety()
        {
            if (_cleanedUp) return;
            _cleanedUp = true;

            try
            {
                if (_saveDebounceTimer != null) _saveDebounceTimer.Stop();
                if (_hwThrottleTimer != null) _hwThrottleTimer.Stop();
                if (_pendingConfigSave)
                {
                    _pendingConfigSave = false;
                    _config.Save();
                }
                FlushActiveProfileUsage();
                if (_scheduleTimer != null) _scheduleTimer.Stop();
                if (_transitionEngine != null) _transitionEngine.Dispose();
                _isPreviewing = false;
                _backend.RestoreInitial();
                ColorFilterHelper.Shutdown();
                _watcher.Dispose();
                _backend.Dispose();
            }
            catch { }

            AppConfig.MarkSessionCleanExit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_forceExit && e.CloseReason == CloseReason.UserClosing && _config.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            SystemEvents.SessionEnding -= OnSessionEnding;
            SystemEvents.DisplaySettingsChanged -= OnSystemDisplayOrPowerChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            CleanupSafety();

            if (_scheduleTimer != null) _scheduleTimer.Dispose();
            if (_saveDebounceTimer != null) _saveDebounceTimer.Dispose();
            if (_hwThrottleTimer != null) _hwThrottleTimer.Dispose();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            _toolTip.Dispose();
            _appIcon.Dispose();
            _appTitleBitmap.Dispose();

            base.OnFormClosing(e);
        }

        private void SyncGeneralControls()
        {
            _suppressEvents = true;
            _rowDesktopSat.Value = ClampSat(_config.DesktopSaturation);
            _rowDesktopFilterSat.Value = ClampFilterSat(_config.DesktopFilterSaturation);
            _rowDesktopCon.Enabled = _backend.SupportsContrastBrightness;
            _rowDesktopCon.Value = ClampCon(_config.DesktopContrast);
            _rowDesktopBri.Enabled = _backend.SupportsContrastBrightness;
            _rowDesktopBri.Value = ClampBri(_config.DesktopBrightness);
            _rowDesktopGam.Value = ClampGam(_config.DesktopGamma);
            _rowDesktopTemp.Enabled = _backend.SupportsColorTemp;
            _rowDesktopTemp.Value = ClampTemp(_config.DesktopColorTemp);
            _suppressEvents = false;
        }

        private void SyncSettingsControls()
        {
            _suppressEvents = true;
            _tglEnabled.Checked = _config.Enabled;
            _tglStartup.Checked = _config.StartWithWindows;
            _tglTrayOnClose.Checked = _config.MinimizeToTrayOnClose;
            _tglOled.Checked = _config.OledBlackTheme;

            EnsureScheduleRuleExists();
            var rule = _config.Schedules[0];
            _tglSchedule.Checked = _config.ScheduleEnabled;
            _numStartHour.Value = rule.StartHour;
            _numStartMin.Value = rule.StartMinute;
            _numEndHour.Value = rule.EndHour;
            _numEndMin.Value = rule.EndMinute;
            _rowSchedTemp.Enabled = _backend.SupportsColorTemp;
            _rowSchedTemp.Value = ClampTemp(rule.ColorTemp);
            _rowSchedSat.Value = ClampSat(rule.Saturation);
            _suppressEvents = false;
        }

        private AppProfile GetSelectedProfile()
        {
            int idx = _profileList.SelectedIndex;
            if (idx < 0 || idx >= _filteredProfiles.Count)
                return null;
            return _filteredProfiles[idx];
        }

        private void SyncSelectedProfileControls()
        {
            var profile = GetSelectedProfile();
            bool hasProfile = profile != null;
            _emptyStatePanel.Visible = !hasProfile;
            _profileDetailCard.Visible = hasProfile;
            if (_btnExportProfile != null) _btnExportProfile.Enabled = hasProfile;

            if (!hasProfile)
                return;

            _suppressEvents = true;
            _pbProfileIcon.Image = _watcher.GetIconForExecutable(profile.ExecutablePath);
            _lblProfileName.Text = profile.Name;
            _lblProfilePath.Text = string.IsNullOrEmpty(profile.ExecutablePath) ? profile.ProcessName : profile.ExecutablePath;
            _lblProfileUsage.Text = L.Get("ProfileUsage") + FormatUsageDuration(profile.UsageSeconds);

            _tglPerMonitor.Checked = profile.PerMonitorEnabled;
            _cmbMonitors.Enabled = profile.PerMonitorEnabled;
            _cmbMonitors.Items.Clear();
            foreach (var scr in Screen.AllScreens)
                _cmbMonitors.Items.Add(scr.DeviceName);
            if (_cmbMonitors.Items.Count > 0)
                _cmbMonitors.SelectedIndex = 0;

            EnsureMonitorSettingsInitialized(profile);
            _tglPrimaryOnly.Checked = profile.PrimaryMonitorOnly;
            _tglMatchFullPath.Checked = profile.MatchFullPath;
            _tglFullscreenOnly.Checked = profile.FullscreenOnly;

            _btnPreview.Text = _isPreviewing ? L.Get("StopPreview") : L.Get("Preview");
            _btnPreview.ButtonStyle = _isPreviewing ? FlatButtonStyle.Primary : FlatButtonStyle.Secondary;
            _suppressEvents = false;

            SyncSelectedProfileSlidersOnly();
        }

        private static string FormatUsageDuration(long totalSeconds)
        {
            if (totalSeconds < 60) return totalSeconds + "s";
            long mins = totalSeconds / 60;
            if (mins < 60) return mins + "m";
            long hrs = mins / 60;
            long remM = mins % 60;
            return hrs + "h " + remM + "m";
        }

        private void SyncSelectedProfileSlidersOnly()
        {
            var profile = GetSelectedProfile();
            if (profile == null) return;

            _suppressEvents = true;
            var ms = GetSelectedMonitorSetting(profile);
            int sat = ms != null ? ms.Saturation : profile.TargetSaturation;
            int con = ms != null ? ms.Contrast : profile.TargetContrast;
            int bri = ms != null ? ms.Brightness : profile.TargetBrightness;
            int gam = ms != null ? ms.Gamma : profile.TargetGamma;
            int temp = ms != null ? ms.ColorTemp : profile.TargetColorTemp;

            _rowProfSat.Value = ClampSat(sat);
            _rowProfFilterSat.Value = ClampFilterSat(profile.TargetFilterSaturation);
            _rowProfCon.Enabled = _backend.SupportsContrastBrightness;
            _rowProfCon.Value = ClampCon(con);
            _rowProfBri.Enabled = _backend.SupportsContrastBrightness;
            _rowProfBri.Value = ClampBri(bri);
            _rowProfGam.Value = ClampGam(gam);
            _rowProfTemp.Enabled = _backend.SupportsColorTemp;
            _rowProfTemp.Value = ClampTemp(temp);
            _suppressEvents = false;
        }

        private void ApplyLanguageTexts()
        {
            _toolTip.SetToolTip(_navGeneral, L.Get("NavGeneral"));
            _toolTip.SetToolTip(_navProfiles, L.Get("NavProfiles"));
            _toolTip.SetToolTip(_navPreview, L.Get("NavPreview"));
            _toolTip.SetToolTip(_navSettings, L.Get("NavSettings"));
            _toolTip.SetToolTip(_navAbout, L.Get("NavAbout"));

            _lblSidebarHeader.Text = L.Get("TrackedWindows");
            _btnPickWindow.Text = "+ " + L.Get("PickWindow");
            _btnPickFile.Text = L.Get("PickFile");

            _lblGeneralHero.Text = L.Get("GeneralTitle");
            _lblActiveCap.Text = L.Get("ActiveWindow");
            _lblShowcaseHeader.Text = L.Get("LiveShowcaseCard");
            _lblDesktopCardTitle.Text = L.Get("GeneralSettings");
            _lblPresetsHeader.Text = L.Get("PresetsLabel");
            _btnSaveDesktopPreset.Text = L.Get("SavePreset");
            RefreshPresetsFlow();
            _rowDesktopSat.Title = L.Get("Saturation");
            _rowDesktopFilterSat.Title = L.Get("FilterSaturation");
            _toolTip.SetToolTip(_rowDesktopFilterSat, L.Get("InfoFilterSaturation"));
            _rowDesktopCon.Title = L.Get("Contrast");
            _rowDesktopBri.Title = L.Get("Brightness");
            _rowDesktopGam.Title = L.Get("Gamma");
            _rowDesktopTemp.Title = L.Get("ColorTemp");

            _lblProfilesHero.Text = L.Get("ProfilesTitle");
            _btnImportProfile.Text = L.Get("ImportProfile");
            _btnExportProfile.Text = L.Get("ExportProfile");
            _lblEmptyTitle.Text = L.Get("NoProfilesTitle");
            _lblEmptyDesc.Text = L.Get("NoProfilesDesc");
            _btnEmptyAdd.Text = L.Get("AddFirstProfile");
            _btnDeleteProfile.Text = L.Get("Remove");
            _btnPreview.Text = _isPreviewing ? L.Get("StopPreview") : L.Get("Preview");
            _lblPerMonitor.Text = L.Get("PerMonitorMode");
            _lblSelectMonitor.Text = L.Get("SelectMonitor");
            _rowProfSat.Title = L.Get("Saturation");
            _rowProfFilterSat.Title = L.Get("FilterSaturation");
            _toolTip.SetToolTip(_rowProfFilterSat, L.Get("InfoFilterSaturation"));
            _rowProfCon.Title = L.Get("Contrast");
            _rowProfBri.Title = L.Get("Brightness");
            _rowProfGam.Title = L.Get("Gamma");
            _rowProfTemp.Title = L.Get("ColorTemp");
            _lblPrimary.Text = L.Get("PrimaryOnly");
            _lblMatchFullPath.Text = L.Get("MatchFullPath");
            _lblFullscreenOnly.Text = L.Get("FullscreenOnly");

            _lblPreviewHero.Text = L.Get("PreviewTitle");
            _btnBeforeAfter.Text = L.Get("HoldBeforeAfter");
            _lblCompareHint.Text = L.Get("PreviewDragHint");
            _btnPrevLive.Text = L.Get("PreviewModeLive");
            _btnPrevFps.Text = L.Get("PresetFps");
            _btnPrevOverdrive.Text = L.Get("PresetOverdrive");
            _btnLoadPreviewImg.Text = L.Get("LoadPreviewImage");
            _btnResetPreviewImg.Text = L.Get("ResetPreviewImage");
            if (_compareView != null)
            {
                _compareView.BeforeText = L.Get("BeforeLabel");
                _compareView.AfterText = L.Get("AfterLabel");
                _compareView.Invalidate();
            }
            if (_liveShowcaseView != null)
            {
                _liveShowcaseView.BeforeText = L.Get("BeforeLabel");
                _liveShowcaseView.AfterText = L.Get("AfterLabel");
                _liveShowcaseView.Invalidate();
            }

            _lblSettingsHero.Text = L.Get("SettingsTitle");
            _lblAppearanceCard.Text = L.Get("AppearanceCard");
            _lblAutoTrack.Text = L.Get("AutoTrack");
            _lblStartup.Text = L.Get("StartWithWin");
            _lblTrayClose.Text = L.Get("MinimizeToTray");
            _lblOled.Text = L.Get("OledTheme");
            _lblLangRow.Text = L.Get("LanguageLabel");
            _btnLangSwitch.Text = L.CurrentLanguage == "tr" ? "Türkçe" : "English";
            _lblDiagCard.Text = L.Get("DiagnosticsCard");
            _btnOpenLogs.Text = L.Get("OpenLogs");

            string statusMsg = _backend.IsAvailable ? L.Get("DriverConnected") : (_backend.LastError ?? L.Get("DriverDisconnected"));
            _lblDiagInfo.Text = string.Format("{0}: {1}\nAPI: {2}", _backend.VendorName, statusMsg, _backend.ApiName);

            _lblAboutHero.Text = L.Get("AboutTitle");
            _lblAboutVersion.Text = "Z4Vibrance";
            _lblAboutGpu.Text = L.Get("AboutGpu") + ": " + _backend.VendorName + " (" + _backend.DeviceName + ")";
            _lblAboutApi.Text = L.Get("AboutApi") + ": " + _backend.ApiName;
            _lblAboutPrivacy.Text = L.Get("AboutPrivacy");
            _btnOpenWizard.Text = L.Get("OpenWizard");

            _lblScheduleCard.Text = L.Get("ScheduleCard");
            _lblEnableSchedule.Text = L.Get("EnableSchedule");
            _lblSchedStart.Text = L.Get("ScheduleStart");
            _lblSchedEnd.Text = L.Get("ScheduleEnd");
            _rowSchedTemp.Title = L.Get("ScheduleTemp");
            _rowSchedSat.Title = L.Get("ScheduleSat");

            _btnUndo.Text = L.Get("Undo");
            _btnRedo.Text = L.Get("Redo");
            _btnStatusReset.Text = L.Get("ResetAll");
            UpdateStatusBar();

            PlaceInfoBadge(_infoAutoTrack, _lblAutoTrack, "InfoAutoTrack");
            PlaceInfoBadge(_infoStartup, _lblStartup, "InfoStartWithWin");
            PlaceInfoBadge(_infoTrayClose, _lblTrayClose, "InfoMinimizeToTray");
            PlaceInfoBadge(_infoOled, _lblOled, "InfoOled");
            PlaceInfoBadge(_infoSchedule, _lblEnableSchedule, "InfoSchedule");
            PlaceInfoBadge(_infoPerMonitor, _lblPerMonitor, "InfoPerMonitor");
            PlaceInfoBadge(_infoPrimary, _lblPrimary, "InfoPrimaryOnly");
            PlaceInfoBadge(_infoMatchFullPath, _lblMatchFullPath, "InfoMatchFullPath");
            PlaceInfoBadge(_infoFullscreenOnly, _lblFullscreenOnly, "InfoFullscreenOnly");

            if (_ctxEdit != null)
            {
                _ctxEdit.Text = L.Get("CtxEdit");
                _ctxRename.Text = L.Get("CtxRename");
                _ctxDuplicate.Text = L.Get("CtxDuplicate");
                _ctxDelete.Text = L.Get("CtxDelete");
            }
            if (_trayOpenItem != null)
            {
                _trayOpenItem.Text = L.Get("TrayOpen");
                _trayProfilesItem.Text = L.Get("TrayProfiles");
                _trayResetItem.Text = L.Get("TrayReset");
                _trayExitItem.Text = L.Get("TrayExit");
                UpdateTrayMenuState();
            }
        }

        private void RefreshProfileList(string selectId = null)
        {
            string prevId = selectId;
            if (prevId == null && GetSelectedProfile() != null)
                prevId = GetSelectedProfile().Id;

            string q = (_txtSearch != null ? _txtSearch.Text : string.Empty).Trim();
            _filteredProfiles.Clear();
            foreach (var p in _config.Profiles)
            {
                if (string.IsNullOrEmpty(q) ||
                    (p.Name != null && p.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (p.ProcessName != null && p.ProcessName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    _filteredProfiles.Add(p);
                }
            }

            _profileList.BeginUpdate();
            _profileList.Items.Clear();
            int selectIdx = -1;
            for (int i = 0; i < _filteredProfiles.Count; i++)
            {
                _profileList.Items.Add(_filteredProfiles[i].Name);
                if (prevId != null && _filteredProfiles[i].Id == prevId)
                    selectIdx = i;
            }
            if (selectIdx < 0 && _filteredProfiles.Count > 0)
                selectIdx = 0;
            if (selectIdx >= 0)
                _profileList.SelectedIndex = selectIdx;
            _profileList.EndUpdate();

            UpdateTrayMenuState();
        }

        private void OnDrawProfileItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _filteredProfiles.Count)
                return;

            var profile = _filteredProfiles[e.Index];
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool hovered = e.Index == _hoveredListIndex;
            bool isMatch = _config.Enabled && IsProfileMatch(profile, _activeProcessPath);

            using (var clr = new SolidBrush(Theme.PanelBg))
                g.FillRectangle(clr, e.Bounds);

            var pillRect = new RectangleF(e.Bounds.X + 4, e.Bounds.Y + 2, e.Bounds.Width - 8, e.Bounds.Height - 4);
            if (selected || hovered)
            {
                Color bg = selected ? Theme.SelectedBg : Theme.CardHoverBg;
                using (var path = Theme.RoundedRect(pillRect, 6f))
                using (var b = new SolidBrush(bg))
                    g.FillPath(b, path);
            }

            if (selected)
            {
                using (var bar = new SolidBrush(Theme.Accent))
                    g.FillRectangle(bar, e.Bounds.X + 4, e.Bounds.Y + 10, 3, e.Bounds.Height - 20);
            }

            int iconX = e.Bounds.X + 14;
            int iconY = e.Bounds.Y + (e.Bounds.Height - 24) / 2;
            Bitmap ico = _watcher.GetIconForExecutable(profile.ExecutablePath);
            if (ico != null)
                g.DrawImage(ico, iconX, iconY, 24, 24);
            else
            {
                using (var ph = new SolidBrush(Theme.InputBg))
                    g.FillEllipse(ph, iconX, iconY, 24, 24);
            }

            Color dotColor = isMatch ? Theme.ActiveGreen : Theme.TextMuted;
            using (var dotB = new SolidBrush(dotColor))
                g.FillEllipse(dotB, e.Bounds.Right - 20, e.Bounds.Y + (e.Bounds.Height - 8) / 2, 8, 8);

            int textX = iconX + 32;
            int textW = e.Bounds.Width - textX - 26;

            TextRenderer.DrawText(g, profile.Name, Theme.FontBodyBold,
                new Rectangle(textX, e.Bounds.Y + 6, textW, 18), Theme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(g, profile.ProcessName + ".exe", Theme.FontSmall,
                new Rectangle(textX, e.Bounds.Y + 24, textW, 16), Theme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void OnPickWindowClick(object sender, EventArgs e)
        {
            using (var picker = new WindowPickerForm(_watcher))
            {
                if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedWindow != null)
                {
                    AddOrSelectProfile(
                        picker.SelectedWindow.ProcessName,
                        picker.SelectedWindow.ExecutablePath,
                        picker.SelectedWindow.ProcessName);
                }
            }
        }

        private void OnPickFileClick(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = L.Get("ExeFilter");
                dlg.Title = L.Get("ExeDialogTitle");
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string procName = Path.GetFileNameWithoutExtension(dlg.FileName);
                    AddOrSelectProfile(procName, dlg.FileName, procName);
                }
            }
        }

        private void AddOrSelectProfile(string name, string exePath, string procName)
        {
            foreach (var existing in _config.Profiles)
            {
                if (string.Equals(existing.ExecutablePath, exePath, StringComparison.OrdinalIgnoreCase))
                {
                    RefreshProfileList(existing.Id);
                    SwitchTab(NavIconKind.Profiles);
                    return;
                }
            }

            var profile = new AppProfile
            {
                Name = name,
                ExecutablePath = exePath,
                ProcessName = procName,
                TargetSaturation = PresetManager.GetDefaultProfileSaturation(_backend),
                TargetFilterSaturation = ColorFilterHelper.DefaultPercent,
                TargetContrast = _backend.DefaultContrast,
                TargetBrightness = _backend.DefaultBrightness,
                TargetGamma = 100,
                TargetColorTemp = _backend.DefaultColorTemp,
                PrimaryMonitorOnly = false
            };
            _config.Profiles.Add(profile);
            _config.Save();
            RefreshProfileList(profile.Id);
            SyncSelectedProfileControls();
            SwitchTab(NavIconKind.Profiles);
            ShowToast(L.Get("ToastProfileAdded"), Theme.ActiveGreen);
        }

        private void OnRemoveProfileClick(object sender, EventArgs e)
        {
            var selected = GetSelectedProfile();
            if (selected == null) return;

            _config.Profiles.Remove(selected);
            _config.Save();
            _isPreviewing = false;
            RefreshProfileList();
            SyncSelectedProfileControls();
            ApplyCurrentForegroundState();
            ShowToast(L.Get("ToastProfileRemoved"), Theme.WarningAmber);
        }

        private AppProfile FindMatchingProfile(string exePath, bool isFullscreen)
        {
            if (!_config.Enabled) return null;
            foreach (var p in _config.Profiles)
            {
                if (IsProfileMatch(p, exePath) && (!p.FullscreenOnly || isFullscreen))
                    return p;
            }
            return null;
        }

        private void OnForegroundChanged(object sender, ForegroundChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, ForegroundChangedEventArgs>(OnForegroundChanged), sender, e);
                return;
            }

            _activeProcessPath = e.ExecutablePath ?? string.Empty;
            _activeWindowTitle = string.IsNullOrWhiteSpace(e.WindowTitle)
                ? (string.IsNullOrEmpty(e.ProcessName) ? L.Get("Desktop") : e.ProcessName)
                : e.WindowTitle;
            _activeWindowFullscreen = e.IsFullscreen;

            var nextMatched = FindMatchingProfile(_activeProcessPath, _activeWindowFullscreen);
            int delay = _config.WindowDebounceMs;
            if (nextMatched != null && nextMatched.ApplyDelayMs > 0)
                delay = nextMatched.ApplyDelayMs;
            else if (nextMatched == null && _lastMatchedProfile != null && _lastMatchedProfile.RevertDelayMs > 0)
                delay = _lastMatchedProfile.RevertDelayMs;

            _transitionEngine.ScheduleDebounced(delay, () => ApplyCurrentForegroundState(false));
        }

        private static bool IsProfileMatch(AppProfile profile, string activeExePath)
        {
            if (profile == null || string.IsNullOrEmpty(activeExePath))
                return false;

            if (profile.MatchFullPath)
                return string.Equals(profile.ExecutablePath, activeExePath, StringComparison.OrdinalIgnoreCase);

            if (string.Equals(profile.ExecutablePath, activeExePath, StringComparison.OrdinalIgnoreCase))
                return true;

            string activeProc = Path.GetFileNameWithoutExtension(activeExePath);
            return string.Equals(profile.ProcessName, activeProc, StringComparison.OrdinalIgnoreCase);
        }

        private ScheduleRule GetActiveScheduleRule()
        {
            if (!_config.ScheduleEnabled || _config.Schedules == null || _config.Schedules.Count == 0)
                return null;

            var now = DateTime.Now;
            int nowMin = now.Hour * 60 + now.Minute;

            foreach (var rule in _config.Schedules)
            {
                if (rule == null || !rule.Enabled) continue;
                int startMin = rule.StartHour * 60 + rule.StartMinute;
                int endMin = rule.EndHour * 60 + rule.EndMinute;
                bool inRange = startMin <= endMin ? (nowMin >= startMin && nowMin < endMin) : (nowMin >= startMin || nowMin < endMin);
                if (inRange) return rule;
            }
            return null;
        }

        private void ApplyCurrentForegroundState(bool immediate = false, bool throttleHardware = false)
        {
            if (_isHoldingBeforeAfter) return;

            AppProfile matched = FindMatchingProfile(_activeProcessPath, _activeWindowFullscreen);
            if (_lastMatchedProfile != matched)
            {
                FlushActiveProfileUsage();
                _lastMatchedProfile = matched;
                _activeProfileStartUtc = DateTime.UtcNow;
            }

            int targetSat, targetFilterSat, targetCon, targetBri, targetGam, targetTemp;
            bool primaryOnly = false;
            string statusText;
            ScheduleRule activeSchedule = null;

            if (_isPreviewing && GetSelectedProfile() != null)
            {
                var pr = GetSelectedProfile();
                targetSat = pr.TargetSaturation;
                targetFilterSat = pr.TargetFilterSaturation;
                targetCon = pr.TargetContrast;
                targetBri = pr.TargetBrightness;
                targetGam = pr.TargetGamma;
                targetTemp = pr.TargetColorTemp;
                primaryOnly = pr.PrimaryMonitorOnly;
                statusText = L.Get("PreviewStatus") + pr.Name;
            }
            else if (!_config.Enabled)
            {
                targetSat = _config.DesktopSaturation;
                targetFilterSat = _config.DesktopFilterSaturation;
                targetCon = _config.DesktopContrast;
                targetBri = _config.DesktopBrightness;
                targetGam = _config.DesktopGamma;
                targetTemp = _config.DesktopColorTemp;
                statusText = L.Get("PausedStatus");
            }
            else if (matched != null)
            {
                targetSat = matched.TargetSaturation;
                targetFilterSat = matched.TargetFilterSaturation;
                targetCon = matched.TargetContrast;
                targetBri = matched.TargetBrightness;
                targetGam = matched.TargetGamma;
                targetTemp = matched.TargetColorTemp;
                primaryOnly = matched.PrimaryMonitorOnly;
                statusText = L.Get("ActiveProfileStatus") + matched.Name;
            }
            else if ((activeSchedule = GetActiveScheduleRule()) != null)
            {
                targetSat = activeSchedule.Saturation;
                targetFilterSat = activeSchedule.FilterSaturation;
                targetCon = activeSchedule.Contrast;
                targetBri = activeSchedule.Brightness;
                targetGam = activeSchedule.Gamma;
                targetTemp = activeSchedule.ColorTemp;
                statusText = L.Get("ScheduleActiveStatus") + activeSchedule.Name;
            }
            else
            {
                targetSat = _config.DesktopSaturation;
                targetFilterSat = _config.DesktopFilterSaturation;
                targetCon = _config.DesktopContrast;
                targetBri = _config.DesktopBrightness;
                targetGam = _config.DesktopGamma;
                targetTemp = _config.DesktopColorTemp;
                statusText = L.Get("DesktopProfileApplied");
            }

            var targetState = new ColorState
            {
                Saturation = ClampSat(targetSat),
                FilterSaturation = ClampFilterSat(targetFilterSat),
                Contrast = ClampCon(targetCon),
                Brightness = ClampBri(targetBri),
                Gamma = ClampGam(targetGam),
                ColorTemp = ClampTemp(targetTemp)
            };

            _lastColorState = targetState;
            _appliedSaturation = targetState.Saturation;
            bool usePrimaryOnly = primaryOnly;
            AppProfile activePerMonitorProfile = (_isPreviewing && GetSelectedProfile() != null && GetSelectedProfile().PerMonitorEnabled)
                ? GetSelectedProfile()
                : ((matched != null && matched.PerMonitorEnabled && _config.Enabled) ? matched : null);

            if (activePerMonitorProfile != null && activePerMonitorProfile.MonitorSettings != null && activePerMonitorProfile.MonitorSettings.Count > 0)
            {
                _transitionEngine.ApplyImmediate(targetState, st =>
                {
                    foreach (var ms in activePerMonitorProfile.MonitorSettings)
                    {
                        if (!ms.Enabled) continue;
                        _backend.SetDisplayColorForMonitor(ms.DeviceName,
                            ClampSat(ms.Saturation), ClampCon(ms.Contrast), ClampBri(ms.Brightness),
                            ClampGam(ms.Gamma), ClampTemp(ms.ColorTemp));
                    }
                    ColorFilterHelper.Apply(st.FilterSaturation);
                    UpdatePreviewVisuals(st);
                });
            }
            else
            {
                if (throttleHardware)
                {
                    UpdatePreviewVisuals(targetState);
                    var captured = targetState;
                    ScheduleHardwareApply(() =>
                    {
                        _backend.SetDisplayColor(captured.Saturation, captured.Contrast, captured.Brightness, captured.Gamma, captured.ColorTemp, usePrimaryOnly, Screen.PrimaryScreen);
                        ColorFilterHelper.Apply(captured.FilterSaturation);
                    });
                }
                else
                {
                    Action<ColorState> applyAction = st =>
                    {
                        _backend.SetDisplayColor(st.Saturation, st.Contrast, st.Brightness, st.Gamma, st.ColorTemp, usePrimaryOnly, Screen.PrimaryScreen);
                        ColorFilterHelper.Apply(st.FilterSaturation);
                        UpdatePreviewVisuals(st);
                    };

                    if (immediate || _isPreviewing)
                        _transitionEngine.ApplyImmediate(targetState, applyAction);
                    else
                        _transitionEngine.TransitionTo(targetState, _config.TransitionDurationMs, applyAction);
                }
            }

            _pbActiveIcon.Image = _watcher.GetIconForExecutable(_activeProcessPath);
            _lblActiveTitle.Text = string.IsNullOrEmpty(_activeWindowTitle) ? L.Get("Desktop") : _activeWindowTitle;
            _lblActiveState.Text = statusText;
            _lblStatusActive.Text = statusText + " — " + _lblActiveTitle.Text;
            _lblAppliedValue.Text = _appliedSaturation.ToString();
            _lblAppliedValue.ForeColor = (matched != null || _isPreviewing || activeSchedule != null) ? Theme.ActiveGreen : Theme.Accent;

            _profileList.Invalidate();
        }

        private static void SetStartupRegistry(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (key == null) return;
                    if (enable)
                        key.SetValue(AppRegistryName, "\"" + Application.ExecutablePath + "\"");
                    else
                        key.DeleteValue(AppRegistryName, false);
                }
            }
            catch { }
        }
    }
}
