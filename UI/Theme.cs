using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VibranceZ.UI
{
    // Merkezi renk, boşluk ve yazı tipi sabitleri
    public static class Theme
    {
        public static bool IsOled { get; private set; }

        // Arka plan katmanları (Discord koyu palet)
        public static Color RailBg { get; private set; }
        public static Color PanelBg { get; private set; }
        public static Color ContentBg { get; private set; }
        public static Color CardBg { get; private set; }
        public static Color CardHoverBg { get; private set; }
        public static Color ButtonSecondaryBg { get; private set; }
        public static Color ButtonSecondaryHoverBg { get; private set; }
        public static Color SelectedBg { get; private set; }
        public static Color InputBg { get; private set; }
        public static Color Border { get; private set; }
        public static Color BorderSubtle { get; private set; }

        // Eski adlarla uyumluluk özellikleri
        public static Color WindowBg { get { return RailBg; } }
        public static Color SurfaceBg { get { return PanelBg; } }
        public static Color HoverBg { get { return CardHoverBg; } }

        // Vurgu renkleri (#5865F2 sabit)
        public static readonly Color Accent = Color.FromArgb(88, 101, 242);        // #5865F2
        public static readonly Color AccentHover = Color.FromArgb(110, 121, 245);  // Açık ton
        public static readonly Color AccentPressed = Color.FromArgb(71, 82, 196);  // Koyu ton
        public static readonly Color AccentMuted = Color.FromArgb(40, 88, 101, 242);

        // Metin renkleri
        public static readonly Color TextPrimary = Color.FromArgb(242, 243, 245);   // #F2F3F5
        public static readonly Color TextSecondary = Color.FromArgb(181, 186, 193); // #B5BAC1
        public static readonly Color TextMuted = Color.FromArgb(128, 132, 142);     // #80848E

        // Durum renkleri
        public static readonly Color ActiveGreen = Color.FromArgb(35, 165, 89);     // #23A559
        public static readonly Color WarningAmber = Color.FromArgb(240, 178, 50);   // #F0B232
        public static readonly Color DangerRed = Color.FromArgb(242, 63, 66);       // #F23F42

        // Boşluk ve yarıçap sabitleri
        public const int RailWidth = 64;
        public const int SidebarWidth = 240;
        public const int TitleBarHeight = 40;
        public const int StatusBarHeight = 52;
        public const float CardRadius = 10f;
        public const float ButtonRadius = 7f;
        public const int CardPadding = 16;

        // Yazı tipleri (WinForms GDI/ClearType için en net modern Windows ailesi: Segoe UI + Segoe UI Semibold)
        private static readonly string UiFontFamily = "Segoe UI";
        private static readonly string UiSemiboldFamily = ResolveSemiboldFamily();

        public static readonly Font FontHero = new Font(UiSemiboldFamily, 18f, FontStyle.Regular);
        public static readonly Font FontTitle = new Font(UiSemiboldFamily, 11.5f, FontStyle.Regular);
        public static readonly Font FontCardTitle = new Font(UiSemiboldFamily, 10f, FontStyle.Regular);
        public static readonly Font FontSection = new Font(UiSemiboldFamily, 8.5f, FontStyle.Regular);
        public static readonly Font FontBody = new Font(UiFontFamily, 9.5f, FontStyle.Regular);
        public static readonly Font FontBodyBold = new Font(UiSemiboldFamily, 9.5f, FontStyle.Regular);
        public static readonly Font FontSmall = new Font(UiFontFamily, 8.5f, FontStyle.Regular);
        public static readonly Font FontMono = new Font("Consolas", 9.5f, FontStyle.Bold);

        static Theme()
        {
            ApplyPalette(false);
        }

        public static void ApplyPalette(bool oledBlack)
        {
            IsOled = oledBlack;
            if (oledBlack)
            {
                RailBg = Color.FromArgb(0, 0, 0);          // #000000
                PanelBg = Color.FromArgb(12, 13, 16);      // #0C0D10
                ContentBg = Color.FromArgb(18, 19, 24);    // #121318
                CardBg = Color.FromArgb(24, 26, 32);       // #181A20
                CardHoverBg = Color.FromArgb(34, 37, 45);  // #22252D
                ButtonSecondaryBg = Color.FromArgb(36, 39, 48);
                ButtonSecondaryHoverBg = Color.FromArgb(48, 52, 64);
                SelectedBg = Color.FromArgb(42, 46, 58);
                InputBg = Color.FromArgb(10, 11, 14);
                Border = Color.FromArgb(38, 41, 50);
                BorderSubtle = Color.FromArgb(26, 28, 35);
            }
            else
            {
                RailBg = Color.FromArgb(30, 31, 34);       // #1E1F22
                PanelBg = Color.FromArgb(43, 45, 49);      // #2B2D31
                ContentBg = Color.FromArgb(49, 51, 56);    // #313338
                CardBg = Color.FromArgb(43, 45, 49);       // #2B2D31
                CardHoverBg = Color.FromArgb(56, 58, 64);  // #383A40
                ButtonSecondaryBg = Color.FromArgb(58, 61, 69);      // #3A3D45 (karttan belirgin)
                ButtonSecondaryHoverBg = Color.FromArgb(72, 76, 86); // #484C56
                SelectedBg = Color.FromArgb(64, 66, 73);   // #404249
                InputBg = Color.FromArgb(30, 31, 34);      // #1E1F22
                Border = Color.FromArgb(63, 66, 74);       // #3F424A
                BorderSubtle = Color.FromArgb(40, 42, 46); // #282A2E
            }
        }

        private static string ResolveSemiboldFamily()
        {
            try
            {
                using (var installed = new InstalledFontCollection())
                {
                    foreach (var fam in installed.Families)
                    {
                        if (string.Equals(fam.Name, "Segoe UI Semibold", StringComparison.OrdinalIgnoreCase))
                            return "Segoe UI Semibold";
                    }
                }
            }
            catch { }
            return "Segoe UI";
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // Win11'de DWM, Win10'da GraphicsPath Region ile köşe yuvarlatma
        public static void ApplyWindowCorners(IntPtr hwnd, Control form = null)
        {
            bool dwmOk = false;
            try
            {
                // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2
                int preference = 2;
                int hr = DwmSetWindowAttribute(hwnd, 33, ref preference, sizeof(int));
                if (hr == 0)
                {
                    dwmOk = true;
                    // DWMWA_BORDER_COLOR = 34 (0x00BBGGRR)
                    int borderColor = (Border.B << 16) | (Border.G << 8) | Border.R;
                    DwmSetWindowAttribute(hwnd, 34, ref borderColor, sizeof(int));
                }
            }
            catch
            {
                dwmOk = false;
            }

            // Windows 10 geri dönüşü: GraphicsPath ile köşe yuvarlatma (tam ekranda temizle)
            if (form is Form && ((Form)form).WindowState == FormWindowState.Maximized)
            {
                if (form.Region != null) { form.Region.Dispose(); form.Region = null; }
                return;
            }

            if (!dwmOk && form != null && form.Width > 20 && form.Height > 20)
            {
                try
                {
                    using (var path = RoundedRect(new RectangleF(0, 0, form.Width, form.Height), 10f))
                    {
                        if (form.Region != null) form.Region.Dispose();
                        form.Region = new Region(path);
                    }
                }
                catch { }
            }
        }

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0)
            {
                path.AddRectangle(r);
                return path;
            }

            float d = radius * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d < 1f) d = 1f;

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color LerpColor(Color a, Color b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }
}
