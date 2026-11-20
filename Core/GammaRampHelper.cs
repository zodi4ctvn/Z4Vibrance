using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VibranceZ.Core
{
    public static class GammaRampHelper
    {
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref Ramp lpRamp);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct Ramp
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Red;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Green;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Blue;
        }

        private static Ramp BuildRamp(int gammaPercent)
        {
            // gammaPercent: 100 = 1.00 (nötr), >100 gölgeleri açar, <100 koyulaştırır
            double gamma = Math.Max(0.50, Math.Min(2.50, gammaPercent / 100.0));
            double exponent = 1.0 / gamma;

            var ramp = new Ramp
            {
                Red = new ushort[256],
                Green = new ushort[256],
                Blue = new ushort[256]
            };

            for (int i = 0; i < 256; i++)
            {
                double normalized = i / 255.0;
                double adjusted = Math.Pow(normalized, exponent);
                int val = (int)Math.Round(adjusted * 65535.0);
                if (val < 0) val = 0;
                if (val > 65535) val = 65535;
                ushort u = (ushort)val;
                ramp.Red[i] = u;
                ramp.Green[i] = u;
                ramp.Blue[i] = u;
            }

            return ramp;
        }

        public static void ApplyGamma(int gammaPercent, bool primaryOnly, Screen primaryScreen)
        {
            var ramp = BuildRamp(gammaPercent);

            if (primaryOnly && primaryScreen != null)
            {
                ApplyToDevice(primaryScreen.DeviceName, ref ramp);
            }
            else
            {
                foreach (var screen in Screen.AllScreens)
                {
                    ApplyToDevice(screen.DeviceName, ref ramp);
                }
            }
        }

        public static void ApplyGammaForDevice(string deviceName, int gammaPercent)
        {
            if (string.IsNullOrEmpty(deviceName))
                return;

            var ramp = BuildRamp(gammaPercent);
            ApplyToDevice(deviceName, ref ramp);
        }

        private static void ApplyToDevice(string deviceName, ref Ramp ramp)
        {
            if (string.IsNullOrEmpty(deviceName))
                return;

            IntPtr hdc = IntPtr.Zero;
            try
            {
                hdc = CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
                if (hdc != IntPtr.Zero)
                {
                    SetDeviceGammaRamp(hdc, ref ramp);
                }
            }
            catch (DllNotFoundException ex)
            {
                Logger.Error("gdi32.dll bulunamadi", ex);
            }
            catch (EntryPointNotFoundException ex)
            {
                Logger.Error("gdi32.dll giris noktasi bulunamadi", ex);
            }
            catch (Exception ex)
            {
                Logger.Warn("Gamma ramp uygulanamadi (" + deviceName + "): " + ex.Message);
            }
            finally
            {
                if (hdc != IntPtr.Zero)
                {
                    try { DeleteDC(hdc); }
                    catch { }
                }
            }
        }
    }
}
