using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using VibranceZ.Core;
using VibranceZ.Models;
using VibranceZ.UI;

namespace VibranceZ
{
    internal static class Program
    {
        public static readonly int WmShowMe = RegisterWindowMessage("Z4Vibrance_ShowMe_Msg");
        private static readonly IntPtr HwndBroadcast = new IntPtr(0xFFFF);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static MainForm _mainForm;
        private static int _cleanedUp;

        [STAThread]
        private static void Main()
        {
            IntegrityGuard.Verify();

            bool createdNew;
            using (var mutex = new Mutex(true, "Z4Vibrance_SingleInstance_Mutex", out createdNew))
            {
                if (!createdNew)
                {
                    // İkinci açılışta mevcut pencereyi öne getir
                    if (WmShowMe != 0)
                        PostMessage(HwndBroadcast, WmShowMe, IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                bool dirtyShutdown = AppConfig.WasPreviousSessionDirty();
                if (dirtyShutdown)
                {
                    Logger.Warn("Onceki oturumun duzgun kapanmadigi tespit edildi; gamma sifirlaniyor.");
                    try { GammaRampHelper.ApplyGamma(100, false, Screen.PrimaryScreen); }
                    catch { }
                }

                AppConfig.MarkSessionStarted();
                Logger.Info("Z4Vibrance baslatiliyor.");

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) =>
                {
                    Logger.Error("Yakalanmayan UI is parcacigi hatasi", e.Exception);
                    SafeCleanup();
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    Logger.Error("Yakalanmayan AppDomain hatasi", e.ExceptionObject as Exception);
                    SafeCleanup();
                };
                AppDomain.CurrentDomain.ProcessExit += (s, e) => SafeCleanup();

                _mainForm = new MainForm(dirtyShutdown);
                Application.Run(_mainForm);
                SafeCleanup();
            }
        }

        private static void SafeCleanup()
        {
            if (Interlocked.Exchange(ref _cleanedUp, 1) != 0)
                return;

            if (_mainForm != null)
            {
                try { _mainForm.CleanupSafety(); }
                catch { }
            }

            AppConfig.MarkSessionCleanExit();
            Logger.Info("Z4Vibrance guvenli sekilde kapatildi.");
        }
    }
}
