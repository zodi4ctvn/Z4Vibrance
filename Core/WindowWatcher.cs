using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace VibranceZ.Core
{
    public class RunningWindowInfo
    {
        public IntPtr Hwnd { get; set; }
        public string Title { get; set; }
        public string ProcessName { get; set; }
        public string ExecutablePath { get; set; }
        public Bitmap IconBitmap { get; set; }
    }

    public class ForegroundChangedEventArgs : EventArgs
    {
        public IntPtr Hwnd { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public string ExecutablePath { get; set; }
        public bool IsFullscreen { get; set; }
    }

    public sealed class WindowWatcher : IDisposable
    {
        private const uint EventSystemForeground = 0x0003;
        private const uint EventSystemMinimizeEnd = 0x0017;
        private const uint WineventOutofcontext = 0x0000;
        private const uint WineventSkipownprocess = 0x0002;
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const int DwmwaCloaked = 14;

        private delegate void WinEventDelegate(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        private readonly WinEventDelegate _winEventDelegate;
        private IntPtr _hookForeground;
        private IntPtr _hookMinimizeEnd;
        private readonly Dictionary<string, Bitmap> _iconCache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        public event EventHandler<ForegroundChangedEventArgs> ForegroundChanged;

        public WindowWatcher()
        {
            _winEventDelegate = OnWinEvent;
        }

        public void Start()
        {
            if (_hookForeground == IntPtr.Zero)
            {
                _hookForeground = SetWinEventHook(
                    EventSystemForeground,
                    EventSystemForeground,
                    IntPtr.Zero,
                    _winEventDelegate,
                    0,
                    0,
                    WineventOutofcontext | WineventSkipownprocess);
            }

            if (_hookMinimizeEnd == IntPtr.Zero)
            {
                _hookMinimizeEnd = SetWinEventHook(
                    EventSystemMinimizeEnd,
                    EventSystemMinimizeEnd,
                    IntPtr.Zero,
                    _winEventDelegate,
                    0,
                    0,
                    WineventOutofcontext | WineventSkipownprocess);
            }

            IntPtr current = GetForegroundWindow();
            if (current != IntPtr.Zero)
                EmitForeground(current);
        }

        public void Stop()
        {
            if (_hookForeground != IntPtr.Zero)
            {
                try { UnhookWinEvent(_hookForeground); }
                catch { }
                _hookForeground = IntPtr.Zero;
            }
            if (_hookMinimizeEnd != IntPtr.Zero)
            {
                try { UnhookWinEvent(_hookMinimizeEnd); }
                catch { }
                _hookMinimizeEnd = IntPtr.Zero;
            }
        }

        private void OnWinEvent(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            IntPtr target = hwnd != IntPtr.Zero ? hwnd : GetForegroundWindow();
            if (target != IntPtr.Zero)
                EmitForeground(target);
        }

        private void EmitForeground(IntPtr hwnd)
        {
            var handler = ForegroundChanged;
            if (handler == null)
                return;

            string title = GetWindowTitle(hwnd);
            string exePath = GetProcessExecutablePath(hwnd);
            string procName = string.IsNullOrEmpty(exePath) ? string.Empty : Path.GetFileNameWithoutExtension(exePath);

            handler(this, new ForegroundChangedEventArgs
            {
                Hwnd = hwnd,
                WindowTitle = title,
                ExecutablePath = exePath,
                ProcessName = procName,
                IsFullscreen = IsWindowFullscreen(hwnd)
            });
        }

        public ForegroundChangedEventArgs GetCurrentForegroundInfo()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return null;

            string exePath = GetProcessExecutablePath(hwnd);
            return new ForegroundChangedEventArgs
            {
                Hwnd = hwnd,
                WindowTitle = GetWindowTitle(hwnd),
                ExecutablePath = exePath,
                ProcessName = string.IsNullOrEmpty(exePath) ? string.Empty : Path.GetFileNameWithoutExtension(exePath),
                IsFullscreen = IsWindowFullscreen(hwnd)
            };
        }

        // Pencerenin tam ekran olup olmadığını kontrol eder
        public static bool IsWindowFullscreen(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return false;

            try
            {
                RECT rect;
                if (!GetWindowRect(hwnd, out rect))
                    return false;

                var scr = Screen.FromHandle(hwnd);
                if (scr == null)
                    return false;

                var bounds = scr.Bounds;
                // Kenarlıksız veya tam ekran pencereler için 2px tolerans
                return rect.Left <= bounds.Left + 2 &&
                       rect.Top <= bounds.Top + 2 &&
                       rect.Right >= bounds.Right - 2 &&
                       rect.Bottom >= bounds.Bottom - 2;
            }
            catch
            {
                return false;
            }
        }

        public List<RunningWindowInfo> GetOpenWindows()
        {
            var result = new List<RunningWindowInfo>();
            int selfPid = Process.GetCurrentProcess().Id;
            var seenExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            EnumWindows((hwnd, lParam) =>
            {
                if (!IsWindowVisible(hwnd))
                    return true;

                int cloaked = 0;
                if (DwmGetWindowAttribute(hwnd, DwmwaCloaked, out cloaked, sizeof(int)) == 0 && cloaked != 0)
                    return true;

                string title = GetWindowTitle(hwnd);
                if (string.IsNullOrWhiteSpace(title))
                    return true;

                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                if (pid == 0 || pid == (uint)selfPid)
                    return true;

                string exePath = GetProcessExecutablePath(hwnd);
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return true;

                string procName = Path.GetFileNameWithoutExtension(exePath);
                if (string.Equals(procName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(procName, "TextInputHost", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(procName, "ShellExperienceHost", StringComparison.OrdinalIgnoreCase))
                    return true;

                string dedupeKey = exePath + "|" + title;
                if (seenExes.Contains(dedupeKey))
                    return true;
                seenExes.Add(dedupeKey);

                result.Add(new RunningWindowInfo
                {
                    Hwnd = hwnd,
                    Title = title,
                    ProcessName = procName,
                    ExecutablePath = exePath,
                    IconBitmap = GetIconForExecutable(exePath)
                });

                return true;
            }, IntPtr.Zero);

            return result;
        }

        public Bitmap GetIconForExecutable(string exePath)
        {
            if (string.IsNullOrEmpty(exePath))
                return null;

            Bitmap cached;
            if (_iconCache.TryGetValue(exePath, out cached))
                return cached;

            try
            {
                if (File.Exists(exePath))
                {
                    using (Icon ico = Icon.ExtractAssociatedIcon(exePath))
                    {
                        if (ico != null)
                        {
                            using (Bitmap raw = ico.ToBitmap())
                            {
                                Bitmap bmp = new Bitmap(raw, new Size(24, 24));
                                _iconCache[exePath] = bmp;
                                return bmp;
                            }
                        }
                    }
                }
            }
            catch
            {
                // İkon çıkarma hatalarını yoksay
            }
            return null;
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            try
            {
                int len = GetWindowTextLength(hwnd);
                if (len <= 0)
                    return string.Empty;

                var sb = new StringBuilder(len + 1);
                GetWindowText(hwnd, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetProcessExecutablePath(IntPtr hwnd)
        {
            try
            {
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                if (pid == 0)
                    return string.Empty;

                IntPtr hProcess = OpenProcess(ProcessQueryLimitedInformation, false, pid);
                if (hProcess == IntPtr.Zero)
                    return string.Empty;

                try
                {
                    var sb = new StringBuilder(1024);
                    int size = sb.Capacity;
                    if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                        return sb.ToString();
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }
            catch
            {
                // Erişim engellenen sistem süreçlerini yoksay
            }

            return string.Empty;
        }

        public void Dispose()
        {
            Stop();
            foreach (var bmp in _iconCache.Values)
            {
                if (bmp != null)
                    bmp.Dispose();
            }
            _iconCache.Clear();
        }
    }
}
