using System;
using System.Windows.Forms;

namespace VibranceZ.Core
{
    public sealed class DemoVibranceBackend : IVibranceBackend
    {
        public string VendorName { get { return L.Get("DriverNotConnectedShort"); } }
        public string DeviceName { get { return L.Get("DriverNotConnectedDesc"); } }
        public string ApiName { get { return "Windows GDI Gamma (Demo)"; } }
        public string LastError { get; private set; }

        public int MinLevel { get { return 0; } }
        public int MaxLevel { get { return 400; } }
        public int DefaultLevel { get { return 100; } }
        public bool SupportsContrastBrightness { get { return true; } }
        public int MinContrast { get { return 0; } }
        public int MaxContrast { get { return 200; } }
        public int DefaultContrast { get { return 100; } }
        public int MinBrightness { get { return -100; } }
        public int MaxBrightness { get { return 100; } }
        public int DefaultBrightness { get { return 0; } }
        public bool SupportsColorTemp { get { return true; } }
        public int MinColorTemp { get { return 4000; } }
        public int MaxColorTemp { get { return 10000; } }
        public int DefaultColorTemp { get { return 6500; } }
        public bool IsAvailable { get { return false; } }

        public DemoVibranceBackend(string errorDetail = null)
        {
            LastError = errorDetail ?? string.Empty;
        }

        public bool Init() { return false; }

        public void SetDisplayColor(int saturation, int contrast, int brightness, int gamma, int colorTemp, bool primaryOnly, Screen primaryScreen)
        {
            try { GammaRampHelper.ApplyGamma(gamma, primaryOnly, primaryScreen); }
            catch { }
        }

        public void SetDisplayColorForMonitor(string deviceName, int saturation, int contrast, int brightness, int gamma, int colorTemp)
        {
            try { GammaRampHelper.ApplyGammaForDevice(deviceName, gamma); }
            catch { }
        }

        public void RestoreInitial()
        {
            try { GammaRampHelper.ApplyGamma(100, false, Screen.PrimaryScreen); }
            catch { }
        }

        public void Dispose() { }
    }

    public static class BackendFactory
    {
        public static IVibranceBackend CreateAndInit()
        {
            string amdErr = string.Empty;
            var amd = new AmdVibranceBackend();
            if (amd.Init())
            {
                Logger.Info("AMD ADL backend baslatildi: " + amd.DeviceName);
                return amd;
            }
            amdErr = amd.LastError;
            amd.Dispose();

            string nvErr = string.Empty;
            var nvidia = new NvidiaVibranceBackend();
            if (nvidia.Init())
            {
                Logger.Info("NVIDIA NVAPI backend baslatildi: " + nvidia.DeviceName);
                return nvidia;
            }
            nvErr = nvidia.LastError;
            nvidia.Dispose();

            string combined = string.IsNullOrEmpty(amdErr) ? nvErr : (amdErr + (string.IsNullOrEmpty(nvErr) ? "" : " | " + nvErr));
            Logger.Warn("Donanim GPU backend baslatilamadi, Demo moduna geciliyor. Detay: " + combined);
            return new DemoVibranceBackend(combined);
        }
    }
}
