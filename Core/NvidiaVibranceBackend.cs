using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace VibranceZ.Core
{
    public sealed class NvidiaVibranceBackend : IVibranceBackend
    {
        private const string NvApiDll = "nvapi64.dll";
        private const int NvApiOk = 0;

        private const uint IdInitialize = 0x0150E828;
        private const uint IdUnload = 0xD22BDD7E;
        private const uint IdEnumNvidiaDisplayHandle = 0x9ABDD40D;
        private const uint IdGetAssociatedDisplayName = 0x22A78B05;
        private const uint IdGetDvcInfoEx = 0x0E45002D;
        private const uint IdSetDvcLevelEx = 0x4A82C2B1;

        [DllImport(NvApiDll, EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr NvAPI_QueryInterface(uint id);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvInitializeDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvUnloadDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvEnumDisplayDelegate(int thisEnum, out IntPtr displayHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvGetDisplayNameDelegate(IntPtr displayHandle, [Out] StringBuilder name);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvGetDvcInfoExDelegate(IntPtr displayHandle, uint outputId, ref NvDisplayDvcInfoEx info);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvSetDvcLevelExDelegate(IntPtr displayHandle, uint outputId, ref NvDisplayDvcInfoEx info);

        [StructLayout(LayoutKind.Sequential)]
        private struct NvDisplayDvcInfoEx
        {
            public uint Version;
            public int CurrentLevel;
            public int MinLevel;
            public int MaxLevel;
            public int DefaultLevel;
        }

        private sealed class DisplayHandleInfo
        {
            public IntPtr Handle;
            public string DeviceName;
            public int InitialLevel;
            public bool HasInitialLevel;
        }

        private readonly List<DisplayHandleInfo> _displays = new List<DisplayHandleInfo>();
        private NvUnloadDelegate _unload;
        private NvSetDvcLevelExDelegate _setDvcLevelEx;
        private bool _initialized;

        public string VendorName { get { return "NVIDIA"; } }
        public string DeviceName { get; private set; }
        public string ApiName { get { return "NVIDIA NVAPI (nvapi64.dll)"; } }
        public string LastError { get; private set; }

        public int MinLevel { get; private set; }
        public int MaxLevel { get; private set; }
        public int DefaultLevel { get; private set; }

        public bool SupportsContrastBrightness { get { return false; } }
        public int MinContrast { get { return 0; } }
        public int MaxContrast { get { return 200; } }
        public int DefaultContrast { get { return 100; } }

        public int MinBrightness { get { return -100; } }
        public int MaxBrightness { get { return 100; } }
        public int DefaultBrightness { get { return 0; } }

        public bool SupportsColorTemp { get { return false; } }
        public int MinColorTemp { get { return 4000; } }
        public int MaxColorTemp { get { return 10000; } }
        public int DefaultColorTemp { get { return 6500; } }

        public bool IsAvailable { get; private set; }

        public NvidiaVibranceBackend()
        {
            DeviceName = "NVIDIA GeForce";
            LastError = string.Empty;
            MinLevel = 0;
            MaxLevel = 63;
            DefaultLevel = 0;
        }

        private static T GetDelegate<T>(uint id) where T : class
        {
            IntPtr ptr = NvAPI_QueryInterface(id);
            if (ptr == IntPtr.Zero)
                return null;
            return Marshal.GetDelegateForFunctionPointer(ptr, typeof(T)) as T;
        }

        public bool Init()
        {
            try
            {
                var init = GetDelegate<NvInitializeDelegate>(IdInitialize);
                _unload = GetDelegate<NvUnloadDelegate>(IdUnload);
                var enumDisp = GetDelegate<NvEnumDisplayDelegate>(IdEnumNvidiaDisplayHandle);
                var getName = GetDelegate<NvGetDisplayNameDelegate>(IdGetAssociatedDisplayName);
                var getDvc = GetDelegate<NvGetDvcInfoExDelegate>(IdGetDvcInfoEx);
                _setDvcLevelEx = GetDelegate<NvSetDvcLevelExDelegate>(IdSetDvcLevelEx);

                if (init == null || enumDisp == null || getDvc == null || _setDvcLevelEx == null)
                {
                    LastError = "NVAPI fonksiyon isaretcileri alinamadi";
                    return false;
                }

                if (init() != NvApiOk)
                {
                    LastError = "NvAPI_Initialize basarisiz";
                    return false;
                }

                _initialized = true;

                for (int i = 0; i < 16; i++)
                {
                    IntPtr handle;
                    if (enumDisp(i, out handle) != NvApiOk)
                        break;

                    string name = string.Empty;
                    if (getName != null)
                    {
                        var sb = new StringBuilder(64);
                        if (getName(handle, sb) == NvApiOk)
                            name = sb.ToString();
                    }

                    _displays.Add(new DisplayHandleInfo { Handle = handle, DeviceName = name });
                }

                if (_displays.Count > 0)
                {
                    bool rangeCaptured = false;
                    foreach (var d in _displays)
                    {
                        var info = new NvDisplayDvcInfoEx
                        {
                            Version = (uint)(Marshal.SizeOf(typeof(NvDisplayDvcInfoEx)) | (1 << 16))
                        };
                        if (getDvc(d.Handle, 0, ref info) == NvApiOk && info.MaxLevel > info.MinLevel)
                        {
                            if (!rangeCaptured)
                            {
                                MinLevel = info.MinLevel;
                                MaxLevel = info.MaxLevel;
                                DefaultLevel = info.DefaultLevel;
                                rangeCaptured = true;
                            }
                            d.InitialLevel = Math.Max(info.MinLevel, Math.Min(info.MaxLevel, info.CurrentLevel));
                            d.HasInitialLevel = true;
                        }
                    }
                }

                IsAvailable = _displays.Count > 0;
                if (!IsAvailable)
                    LastError = "Aktif NVIDIA ekrani bulunamadi";
                return IsAvailable;
            }
            catch (DllNotFoundException ex)
            {
                LastError = "nvapi64.dll bulunamadi";
                Logger.Info("NVIDIA NVAPI DLL bulunamadi: " + ex.Message);
                IsAvailable = false;
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                LastError = "nvapi64.dll giris noktasi uyumsuz";
                Logger.Warn("NVIDIA NVAPI giris noktasi bulunamadi: " + ex.Message);
                IsAvailable = false;
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Logger.Error("NVIDIA NVAPI baslatma hatasi", ex);
                IsAvailable = false;
                return false;
            }
        }

        public void SetDisplayColor(int saturation, int contrast, int brightness, int gamma, int colorTemp, bool primaryOnly, Screen primaryScreen)
        {
            try
            {
                GammaRampHelper.ApplyGamma(gamma, primaryOnly, primaryScreen);
            }
            catch (Exception ex)
            {
                Logger.Warn("Gamma uygulanamadi: " + ex.Message);
            }

            if (!IsAvailable || _setDvcLevelEx == null)
                return;

            int clamped = Math.Max(MinLevel, Math.Min(MaxLevel, saturation));
            string primaryName = primaryScreen != null ? primaryScreen.DeviceName : null;

            foreach (var d in _displays)
            {
                if (primaryOnly && !string.IsNullOrEmpty(primaryName))
                {
                    if (!string.Equals(d.DeviceName, primaryName, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                ApplyDisplay(d, clamped);
            }
        }

        public void SetDisplayColorForMonitor(string deviceName, int saturation, int contrast, int brightness, int gamma, int colorTemp)
        {
            if (string.IsNullOrEmpty(deviceName))
                return;

            try
            {
                GammaRampHelper.ApplyGammaForDevice(deviceName, gamma);
            }
            catch { }

            if (!IsAvailable || _setDvcLevelEx == null)
                return;

            int clamped = Math.Max(MinLevel, Math.Min(MaxLevel, saturation));
            foreach (var d in _displays)
            {
                if (string.Equals(d.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyDisplay(d, clamped);
                }
            }
        }

        private void ApplyDisplay(DisplayHandleInfo d, int level)
        {
            try
            {
                var info = new NvDisplayDvcInfoEx
                {
                    Version = (uint)(Marshal.SizeOf(typeof(NvDisplayDvcInfoEx)) | (1 << 16)),
                    CurrentLevel = level
                };
                _setDvcLevelEx(d.Handle, 0, ref info);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Logger.Warn("NVIDIA Digital Vibrance ayarlanamadi: " + ex.Message);
            }
        }

        public void RestoreInitial()
        {
            try
            {
                GammaRampHelper.ApplyGamma(100, false, Screen.PrimaryScreen);
            }
            catch (Exception ex)
            {
                Logger.Warn("Gamma sifirlanamadi: " + ex.Message);
            }

            if (!IsAvailable || _setDvcLevelEx == null)
                return;

            foreach (var d in _displays)
            {
                int restoreLevel = d.HasInitialLevel ? d.InitialLevel : DefaultLevel;
                ApplyDisplay(d, Math.Max(MinLevel, Math.Min(MaxLevel, restoreLevel)));
            }
        }

        public void Dispose()
        {
            if (_initialized && _unload != null)
            {
                try { _unload(); }
                catch (Exception ex) { Logger.Warn("NvAPI_Unload hatasi: " + ex.Message); }
                _initialized = false;
            }
        }
    }
}
