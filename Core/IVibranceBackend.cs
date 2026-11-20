using System;
using System.Windows.Forms;

namespace VibranceZ.Core
{
    public interface IVibranceBackend : IDisposable
    {
        string VendorName { get; }
        string DeviceName { get; }
        string ApiName { get; }
        string LastError { get; }

        int MinLevel { get; }
        int MaxLevel { get; }
        int DefaultLevel { get; }

        bool SupportsContrastBrightness { get; }
        int MinContrast { get; }
        int MaxContrast { get; }
        int DefaultContrast { get; }

        int MinBrightness { get; }
        int MaxBrightness { get; }
        int DefaultBrightness { get; }

        bool SupportsColorTemp { get; }
        int MinColorTemp { get; }
        int MaxColorTemp { get; }
        int DefaultColorTemp { get; }

        bool IsAvailable { get; }

        bool Init();
        void SetDisplayColor(int saturation, int contrast, int brightness, int gamma, int colorTemp, bool primaryOnly, Screen primaryScreen);
        void SetDisplayColorForMonitor(string deviceName, int saturation, int contrast, int brightness, int gamma, int colorTemp);
        void RestoreInitial();
    }
}
