using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VibranceZ.Core
{
    public sealed class AmdVibranceBackend : IVibranceBackend
    {
        private const string AdlDll = "atiadlxx.dll";
        private const int AdlSuccess = 0;
        private const int AdlDisplayColorBrightness = (1 << 0);
        private const int AdlDisplayColorContrast = (1 << 1);
        private const int AdlDisplayColorSaturation = (1 << 2);
        private const int AdlDisplayColorTemperature = (1 << 4);
        private const int AdlMaxPath = 256;
        private const int AdlMaxAdapters = 40;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr AdlMainMemoryAllocDelegate(int size);

        [DllImport(AdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL_Main_Control_Create(AdlMainMemoryAllocDelegate callback, int enumConnectedAdapters);

        [DllImport(AdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL_Main_Control_Destroy();

        [DllImport(AdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL_Adapter_NumberOfAdapters_Get(ref int numAdapters);

        [DllImport(AdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL_Adapter_AdapterInfo_Get(IntPtr info, int inputSize);

        [DllImport(AdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL_Adapter_Active_Get(int adapterIndex, ref int status);

        [DllImport(AdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL_Display_DisplayInfo_Get(int adapterIndex, ref int numDisplays, out IntPtr displayInfoArray, int forceDetect);

        [DllImport(AdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL_Display_Color_Set(int adapterIndex, int displayIndex, int colorType, int value);

        [DllImport(AdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL_Display_Color_Get(
            int adapterIndex,
            int displayIndex,
            int colorType,
            ref int current,
            ref int defVal,
            ref int minVal,
            ref int maxVal,
            ref int stepVal);

        [StructLayout(LayoutKind.Sequential)]
        private struct AdlAdapterInfo
        {
            public int Size;
            public int AdapterIndex;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)]
            public string UDID;
            public int BusNumber;
            public int DeviceNumber;
            public int FunctionNumber;
            public int VendorID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)]
            public string AdapterName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)]
            public string DisplayName;
            public int Present;
            public int Exist;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)]
            public string DriverPath;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)]
            public string DriverPathExt;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)]
            public string PNPString;
            public int OSDisplayIndex;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AdlAdapterInfoArray
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = AdlMaxAdapters)]
            public AdlAdapterInfo[] ADLAdapterInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AdlDisplayId
        {
            public int DisplayLogicalIndex;
            public int DisplayPhysicalIndex;
            public int DisplayLogicalAdapterIndex;
            public int DisplayPhysicalAdapterIndex;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AdlDisplayInfo
        {
            public AdlDisplayId DisplayID;
            public int DisplayControllerIndex;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)]
            public string DisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)]
            public string DisplayManufacturerName;
            public int DisplayType;
            public int DisplayOutputType;
            public int DisplayConnector;
            public int DisplayInfoMask;
            public int DisplayInfoValue;
        }

        private sealed class DisplayTarget
        {
            public int AdapterIndex;
            public int DisplayLogicalIndex;
            public string OsDisplayName;
        }

        private static readonly AdlMainMemoryAllocDelegate MemoryAlloc = size => Marshal.AllocCoTaskMem(size);
        private readonly List<DisplayTarget> _targets = new List<DisplayTarget>();
        private readonly List<IntPtr> _buffersToFree = new List<IntPtr>();
        private bool _initialized;

        private int _initialSat = 100;
        private int _initialCon = 100;
        private int _initialBri = 0;
        private int _initialTemp = 6500;
        private int _tempStep = 100;

        public string VendorName { get { return "AMD"; } }
        public string DeviceName { get; private set; }
        public string ApiName { get { return "AMD ADL (atiadlxx.dll)"; } }
        public string LastError { get; private set; }

        public int MinLevel { get; private set; }
        public int MaxLevel { get; private set; }
        public int DefaultLevel { get; private set; }

        public bool SupportsContrastBrightness { get { return true; } }
        public int MinContrast { get; private set; }
        public int MaxContrast { get; private set; }
        public int DefaultContrast { get; private set; }

        public int MinBrightness { get; private set; }
        public int MaxBrightness { get; private set; }
        public int DefaultBrightness { get; private set; }

        public bool SupportsColorTemp { get; private set; }
        public int MinColorTemp { get; private set; }
        public int MaxColorTemp { get; private set; }
        public int DefaultColorTemp { get; private set; }

        public bool IsAvailable { get; private set; }

        public AmdVibranceBackend()
        {
            DeviceName = "AMD Radeon";
            LastError = string.Empty;
            MinLevel = 0;
            MaxLevel = 400;
            DefaultLevel = 100;
            MinContrast = 0;
            MaxContrast = 200;
            DefaultContrast = 100;
            MinBrightness = -100;
            MaxBrightness = 100;
            DefaultBrightness = 0;
            SupportsColorTemp = false;
            MinColorTemp = 4000;
            MaxColorTemp = 10000;
            DefaultColorTemp = 6500;
        }

        public bool Init()
        {
            try
            {
                if (ADL_Main_Control_Create(MemoryAlloc, 1) != AdlSuccess)
                {
                    LastError = "ADL_Main_Control_Create basarisiz";
                    return false;
                }

                _initialized = true;

                int numAdapters = 0;
                if (ADL_Adapter_NumberOfAdapters_Get(ref numAdapters) != AdlSuccess || numAdapters <= 0)
                {
                    LastError = "AMD bagdastirici bulunamadi";
                    return false;
                }

                var infoArray = new AdlAdapterInfoArray();
                int size = Marshal.SizeOf(infoArray);
                IntPtr adapterBuffer = Marshal.AllocCoTaskMem(size);
                _buffersToFree.Add(adapterBuffer);
                Marshal.StructureToPtr(infoArray, adapterBuffer, false);

                if (ADL_Adapter_AdapterInfo_Get(adapterBuffer, size) != AdlSuccess)
                {
                    LastError = "ADL_Adapter_AdapterInfo_Get basarisiz";
                    return false;
                }

                infoArray = (AdlAdapterInfoArray)Marshal.PtrToStructure(adapterBuffer, typeof(AdlAdapterInfoArray));

                for (int i = 0; i < numAdapters && i < AdlMaxAdapters; i++)
                {
                    var adapter = infoArray.ADLAdapterInfo[i];
                    if (!string.IsNullOrEmpty(adapter.AdapterName) && DeviceName == "AMD Radeon")
                    {
                        DeviceName = adapter.AdapterName.Trim();
                    }

                    int isActive = 0;
                    if (ADL_Adapter_Active_Get(adapter.AdapterIndex, ref isActive) != AdlSuccess)
                        continue;

                    IntPtr displayBuffer = IntPtr.Zero;
                    int numDisplays = 0;
                    if (ADL_Display_DisplayInfo_Get(adapter.AdapterIndex, ref numDisplays, out displayBuffer, 1) == AdlSuccess &&
                        displayBuffer != IntPtr.Zero)
                    {
                        _buffersToFree.Add(displayBuffer);

                        int structSize = Marshal.SizeOf(typeof(AdlDisplayInfo));
                        for (int j = 0; j < numDisplays; j++)
                        {
                            IntPtr itemPtr = new IntPtr(displayBuffer.ToInt64() + j * structSize);
                            var dispInfo = (AdlDisplayInfo)Marshal.PtrToStructure(itemPtr, typeof(AdlDisplayInfo));

                            if (dispInfo.DisplayID.DisplayLogicalAdapterIndex == -1)
                                continue;

                            if (dispInfo.DisplayID.DisplayLogicalAdapterIndex == adapter.AdapterIndex)
                            {
                                _targets.Add(new DisplayTarget
                                {
                                    AdapterIndex = adapter.AdapterIndex,
                                    DisplayLogicalIndex = dispInfo.DisplayID.DisplayLogicalIndex,
                                    OsDisplayName = adapter.DisplayName ?? string.Empty
                                });
                            }
                        }
                    }
                }

                if (_targets.Count > 0)
                {
                    int cur = 100, def = 100, min = 0, max = 200, step = 1;
                    var first = _targets[0];
                    if (ADL_Display_Color_Get(first.AdapterIndex, first.DisplayLogicalIndex, AdlDisplayColorSaturation,
                        ref cur, ref def, ref min, ref max, ref step) == AdlSuccess && max > min)
                    {
                        MinLevel = min;
                        MaxLevel = Math.Max(400, max);
                        DefaultLevel = def;
                        _initialSat = Math.Max(MinLevel, Math.Min(MaxLevel, cur));
                    }

                    if (ADL_Display_Color_Get(first.AdapterIndex, first.DisplayLogicalIndex, AdlDisplayColorContrast,
                        ref cur, ref def, ref min, ref max, ref step) == AdlSuccess && max > min)
                    {
                        MinContrast = min;
                        MaxContrast = max;
                        DefaultContrast = def;
                        _initialCon = Math.Max(min, Math.Min(max, cur));
                    }

                    if (ADL_Display_Color_Get(first.AdapterIndex, first.DisplayLogicalIndex, AdlDisplayColorBrightness,
                        ref cur, ref def, ref min, ref max, ref step) == AdlSuccess && max > min)
                    {
                        MinBrightness = min;
                        MaxBrightness = max;
                        DefaultBrightness = def;
                        _initialBri = Math.Max(min, Math.Min(max, cur));
                    }

                    if (ADL_Display_Color_Get(first.AdapterIndex, first.DisplayLogicalIndex, AdlDisplayColorTemperature,
                        ref cur, ref def, ref min, ref max, ref step) == AdlSuccess && max > min)
                    {
                        SupportsColorTemp = true;
                        MinColorTemp = min;
                        MaxColorTemp = max;
                        DefaultColorTemp = def;
                        _tempStep = Math.Max(1, step);
                        _initialTemp = Math.Max(min, Math.Min(max, cur));
                        Logger.Info(string.Format("AMD Renk Sicakligi araligi: {0}-{1}K, adim={2}", min, max, _tempStep));
                    }
                }

                IsAvailable = _targets.Count > 0;
                if (!IsAvailable)
                    LastError = "Aktif AMD ekrani bulunamadi";
                return IsAvailable;
            }
            catch (DllNotFoundException ex)
            {
                LastError = "atiadlxx.dll bulunamadi";
                Logger.Info("AMD ADL DLL bulunamadi: " + ex.Message);
                IsAvailable = false;
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                LastError = "atiadlxx.dll giris noktasi uyumsuz";
                Logger.Warn("AMD ADL giris noktasi bulunamadi: " + ex.Message);
                IsAvailable = false;
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Logger.Error("AMD ADL baslatma hatasi", ex);
                IsAvailable = false;
                return false;
            }
            finally
            {
                FreeTrackedBuffers();
            }
        }

        private void FreeTrackedBuffers()
        {
            foreach (var ptr in _buffersToFree)
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(ptr);
            }
            _buffersToFree.Clear();
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

            if (!IsAvailable)
                return;

            int clampedSat = Math.Max(MinLevel, Math.Min(MaxLevel, saturation));
            int clampedCon = Math.Max(MinContrast, Math.Min(MaxContrast, contrast));
            int clampedBri = Math.Max(MinBrightness, Math.Min(MaxBrightness, brightness));
            int clampedTemp = Math.Max(MinColorTemp, Math.Min(MaxColorTemp, colorTemp));
            // AMD ADL sürücüsü yalnızca stepVal katlarındaki sıcaklık değerlerini kabul eder
            if (_tempStep > 1)
            {
                clampedTemp = MinColorTemp + ((int)Math.Round((clampedTemp - MinColorTemp) / (double)_tempStep)) * _tempStep;
                clampedTemp = Math.Max(MinColorTemp, Math.Min(MaxColorTemp, clampedTemp));
            }
            string primaryName = primaryScreen != null ? primaryScreen.DeviceName : null;

            foreach (var t in _targets)
            {
                if (primaryOnly && !string.IsNullOrEmpty(primaryName))
                {
                    if (!string.Equals(t.OsDisplayName, primaryName, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                ApplyTarget(t, clampedSat, clampedCon, clampedBri, clampedTemp);
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

            if (!IsAvailable)
                return;

            int clampedSat = Math.Max(MinLevel, Math.Min(MaxLevel, saturation));
            int clampedCon = Math.Max(MinContrast, Math.Min(MaxContrast, contrast));
            int clampedBri = Math.Max(MinBrightness, Math.Min(MaxBrightness, brightness));
            int clampedTemp = Math.Max(MinColorTemp, Math.Min(MaxColorTemp, colorTemp));
            if (_tempStep > 1)
            {
                clampedTemp = MinColorTemp + ((int)Math.Round((clampedTemp - MinColorTemp) / (double)_tempStep)) * _tempStep;
                clampedTemp = Math.Max(MinColorTemp, Math.Min(MaxColorTemp, clampedTemp));
            }

            foreach (var t in _targets)
            {
                if (string.Equals(t.OsDisplayName, deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyTarget(t, clampedSat, clampedCon, clampedBri, clampedTemp);
                }
            }
        }

        private void ApplyTarget(DisplayTarget t, int sat, int con, int bri, int temp)
        {
            try
            {
                ADL_Display_Color_Set(t.AdapterIndex, t.DisplayLogicalIndex, AdlDisplayColorSaturation, sat);
                ADL_Display_Color_Set(t.AdapterIndex, t.DisplayLogicalIndex, AdlDisplayColorContrast, con);
                ADL_Display_Color_Set(t.AdapterIndex, t.DisplayLogicalIndex, AdlDisplayColorBrightness, bri);
                if (SupportsColorTemp)
                    ADL_Display_Color_Set(t.AdapterIndex, t.DisplayLogicalIndex, AdlDisplayColorTemperature, temp);
            }
            catch (DllNotFoundException ex)
            {
                LastError = "atiadlxx.dll bulunamadi";
                Logger.Error("AMD ADL Set sirasinda DLL kayboldu", ex);
            }
            catch (EntryPointNotFoundException ex)
            {
                LastError = "atiadlxx.dll giris noktasi hatasi";
                Logger.Error("AMD ADL Set giris noktasi hatasi", ex);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Logger.Warn("AMD ekran rengi ayarlanamadi: " + ex.Message);
            }
        }

        public void RestoreInitial()
        {
            SetDisplayColor(_initialSat, _initialCon, _initialBri, 100, _initialTemp, false, Screen.PrimaryScreen);
        }

        public void Dispose()
        {
            FreeTrackedBuffers();

            if (_initialized)
            {
                try { ADL_Main_Control_Destroy(); }
                catch (Exception ex) { Logger.Warn("ADL_Main_Control_Destroy hatasi: " + ex.Message); }
                _initialized = false;
            }
        }
    }
}
