using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace VibranceZ.Core
{
    // Döngüsel (~1 MB) yerel günlük yöneticisi
    public static class Logger
    {
        private const long MaxFileBytes = 1024 * 1024; // 1 MB
        private static readonly object SyncLock = new object();

        public static string LogDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Z4Vibrance",
                    "logs");
            }
        }

        public static string LogFilePath
        {
            get { return Path.Combine(LogDirectory, "z4vibrance.log"); }
        }

        public static string OldLogFilePath
        {
            get { return Path.Combine(LogDirectory, "z4vibrance.old.log"); }
        }

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Warn(string message)
        {
            Write("WARN", message, null);
        }

        public static void Error(string message, Exception ex = null)
        {
            Write("ERROR", message, ex);
        }

        private static void Write(string level, string message, Exception ex)
        {
            try
            {
                lock (SyncLock)
                {
                    if (!Directory.Exists(LogDirectory))
                        Directory.CreateDirectory(LogDirectory);

                    RotateIfNeeded();

                    var sb = new StringBuilder();
                    sb.Append('[');
                    sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    sb.Append("] [");
                    sb.Append(level);
                    sb.Append("] ");
                    sb.Append(message ?? string.Empty);

                    if (ex != null)
                    {
                        sb.Append(" | ");
                        sb.Append(ex.GetType().Name);
                        sb.Append(": ");
                        sb.Append(ex.Message);
                        if (!string.IsNullOrEmpty(ex.StackTrace))
                        {
                            sb.AppendLine();
                            sb.Append("    ");
                            sb.Append(ex.StackTrace.Replace("\r\n", "\r\n    "));
                        }
                    }

                    sb.AppendLine();
                    File.AppendAllText(LogFilePath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Günlük yazma hatası uygulamayı durdurmamalı
            }
        }

        private static void RotateIfNeeded()
        {
            if (!File.Exists(LogFilePath))
                return;

            var fi = new FileInfo(LogFilePath);
            if (fi.Length < MaxFileBytes)
                return;

            try
            {
                if (File.Exists(OldLogFilePath))
                    File.Delete(OldLogFilePath);
                File.Move(LogFilePath, OldLogFilePath);
            }
            catch
            {
                // Döndürme başarısız olursa mevcut dosyayı sıfırla
                try { File.WriteAllText(LogFilePath, string.Empty, Encoding.UTF8); }
                catch { }
            }
        }

        public static void OpenLogFolder()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                    Directory.CreateDirectory(LogDirectory);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + LogDirectory + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Error("Gunluk klasoru acilamadi", ex);
            }
        }
    }
}
