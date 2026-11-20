using System;
using System.Runtime.InteropServices;

namespace VibranceZ.Core
{
    public static class ColorFilterHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MagColorEffect
        {
            public float M00, M01, M02, M03, M04;
            public float M10, M11, M12, M13, M14;
            public float M20, M21, M22, M23, M24;
            public float M30, M31, M32, M33, M34;
            public float M40, M41, M42, M43, M44;
        }

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MagInitialize();

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MagUninitialize();

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MagSetFullscreenColorEffect(ref MagColorEffect pEffect);

        public const int MinPercent = 0;
        public const int MaxPercent = 400;
        public const int DefaultPercent = 100;

        private static readonly object Sync = new object();
        private static bool _initialized;
        private static bool _unavailable;
        private static int _lastAppliedPercent = DefaultPercent;

        public static bool IsAvailable
        {
            get { lock (Sync) { return !_unavailable; } }
        }

        public static int Clamp(int percent)
        {
            if (percent < MinPercent) return MinPercent;
            if (percent > MaxPercent) return MaxPercent;
            return percent;
        }

        public static void Apply(int percent)
        {
            percent = Clamp(percent);

            lock (Sync)
            {
                if (_unavailable)
                    return;

                if (percent == _lastAppliedPercent)
                    return;

                if (percent == DefaultPercent)
                {
                    if (_initialized)
                    {
                        try
                        {
                            var identity = CreateSaturationMatrix(1.0f);
                            MagSetFullscreenColorEffect(ref identity);
                            MagUninitialize();
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn("Renk filtresi sifirlanamadi: " + ex.Message);
                        }
                        _initialized = false;
                    }
                    _lastAppliedPercent = DefaultPercent;
                    return;
                }

                try
                {
                    if (!_initialized)
                    {
                        if (!MagInitialize())
                        {
                            _unavailable = true;
                            Logger.Warn("Windows Magnification API baslatilamadi.");
                            return;
                        }
                        _initialized = true;
                    }

                    var effect = CreateSaturationMatrix(percent / 100.0f);
                    if (MagSetFullscreenColorEffect(ref effect))
                    {
                        _lastAppliedPercent = percent;
                    }
                }
                catch (DllNotFoundException ex)
                {
                    _unavailable = true;
                    Logger.Warn("Magnification.dll bulunamadi: " + ex.Message);
                }
                catch (EntryPointNotFoundException ex)
                {
                    _unavailable = true;
                    Logger.Warn("Magnification.dll giris noktasi bulunamadi: " + ex.Message);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Renk filtresi uygulanamadi: " + ex.Message);
                }
            }
        }

        public static void Reset()
        {
            Apply(DefaultPercent);
        }

        public static void Shutdown()
        {
            lock (Sync)
            {
                if (!_initialized)
                {
                    _lastAppliedPercent = DefaultPercent;
                    return;
                }

                try
                {
                    var identity = CreateSaturationMatrix(1.0f);
                    MagSetFullscreenColorEffect(ref identity);
                    MagUninitialize();
                }
                catch (Exception ex)
                {
                    Logger.Warn("Renk filtresi kapatilamadi: " + ex.Message);
                }
                finally
                {
                    _initialized = false;
                    _lastAppliedPercent = DefaultPercent;
                }
            }
        }

        private static MagColorEffect CreateSaturationMatrix(float s)
        {
            const float rw = 0.2126f;
            const float gw = 0.7152f;
            const float bw = 0.0722f;

            float inv = 1.0f - s;
            float r = rw * inv;
            float g = gw * inv;
            float b = bw * inv;

            return new MagColorEffect
            {
                M00 = r + s, M01 = r,     M02 = r,     M03 = 0f, M04 = 0f,
                M10 = g,     M11 = g + s, M12 = g,     M13 = 0f, M14 = 0f,
                M20 = b,     M21 = b,     M22 = b + s, M23 = 0f, M24 = 0f,
                M30 = 0f,    M31 = 0f,    M32 = 0f,    M33 = 1f, M34 = 0f,
                M40 = 0f,    M41 = 0f,    M42 = 0f,    M43 = 0f, M44 = 1f
            };
        }
    }
}
