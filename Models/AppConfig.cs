using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using VibranceZ.Core;

namespace VibranceZ.Models
{
    public class MonitorColorSetting
    {
        public string DeviceName { get; set; }
        public bool Enabled { get; set; }
        public int Saturation { get; set; }
        public int Contrast { get; set; }
        public int Brightness { get; set; }
        public int Gamma { get; set; }
        public int ColorTemp { get; set; }

        public MonitorColorSetting()
        {
            Enabled = true;
            Saturation = 150;
            Contrast = 100;
            Brightness = 0;
            Gamma = 100;
            ColorTemp = 6500;
        }
    }

    public class ColorPreset
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsBuiltIn { get; set; }
        public int Saturation { get; set; }
        public int FilterSaturation { get; set; }
        public int Contrast { get; set; }
        public int Brightness { get; set; }
        public int Gamma { get; set; }
        public int ColorTemp { get; set; }

        public ColorPreset()
        {
            Id = Guid.NewGuid().ToString("N");
            Saturation = 100;
            FilterSaturation = 100;
            Contrast = 100;
            Brightness = 0;
            Gamma = 100;
            ColorTemp = 6500;
        }
    }

    public class ScheduleRule
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public int StartHour { get; set; }
        public int StartMinute { get; set; }
        public int EndHour { get; set; }
        public int EndMinute { get; set; }
        public int Saturation { get; set; }
        public int FilterSaturation { get; set; }
        public int Contrast { get; set; }
        public int Brightness { get; set; }
        public int Gamma { get; set; }
        public int ColorTemp { get; set; }

        public ScheduleRule()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "Gece Modu";
            Enabled = true;
            StartHour = 22;
            StartMinute = 0;
            EndHour = 7;
            EndMinute = 0;
            Saturation = 100;
            FilterSaturation = 100;
            Contrast = 100;
            Brightness = 0;
            Gamma = 100;
            ColorTemp = 5500;
        }
    }

    public class AppProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ExecutablePath { get; set; }
        public string ProcessName { get; set; }
        public bool MatchFullPath { get; set; }
        public int TargetSaturation { get; set; }
        public int TargetFilterSaturation { get; set; }
        public int TargetContrast { get; set; }
        public int TargetBrightness { get; set; }
        public int TargetGamma { get; set; }
        public int TargetColorTemp { get; set; }
        public bool PrimaryMonitorOnly { get; set; }
        public bool FullscreenOnly { get; set; }
        public int ApplyDelayMs { get; set; }
        public int RevertDelayMs { get; set; }
        public bool PerMonitorEnabled { get; set; }
        public List<MonitorColorSetting> MonitorSettings { get; set; }
        public long UsageSeconds { get; set; }

        public AppProfile()
        {
            Id = Guid.NewGuid().ToString("N");
            TargetSaturation = 150;
            TargetFilterSaturation = 100;
            TargetContrast = 100;
            TargetBrightness = 0;
            TargetGamma = 100;
            TargetColorTemp = 6500;
            PrimaryMonitorOnly = false;
            MatchFullPath = false;
            FullscreenOnly = false;
            ApplyDelayMs = 0;
            RevertDelayMs = 0;
            PerMonitorEnabled = false;
            MonitorSettings = new List<MonitorColorSetting>();
            UsageSeconds = 0;
        }
    }

    public class AppConfig
    {
        public const int CurrentConfigVersion = 3;

        public int ConfigVersion { get; set; }
        public string Language { get; set; }
        public bool Enabled { get; set; }
        public int DesktopSaturation { get; set; }
        public int DesktopFilterSaturation { get; set; }
        public int DesktopContrast { get; set; }
        public int DesktopBrightness { get; set; }
        public int DesktopGamma { get; set; }
        public int DesktopColorTemp { get; set; }
        public bool StartWithWindows { get; set; }
        public bool MinimizeToTrayOnClose { get; set; }
        public int TransitionDurationMs { get; set; }
        public int WindowDebounceMs { get; set; }
        public bool ScheduleEnabled { get; set; }
        public bool OledBlackTheme { get; set; }
        public bool OnboardingCompleted { get; set; }
        public List<AppProfile> Profiles { get; set; }
        public List<ScheduleRule> Schedules { get; set; }
        public List<ColorPreset> UserPresets { get; set; }

        [ScriptIgnore]
        public bool IsFreshConfig { get; set; }

        public AppConfig()
        {
            ConfigVersion = CurrentConfigVersion;
            Language = "tr";
            Enabled = true;
            DesktopSaturation = 100;
            DesktopFilterSaturation = 100;
            DesktopContrast = 100;
            DesktopBrightness = 0;
            DesktopGamma = 100;
            DesktopColorTemp = 6500;
            StartWithWindows = false;
            MinimizeToTrayOnClose = true;
            TransitionDurationMs = 180;
            WindowDebounceMs = 80;
            ScheduleEnabled = false;
            OledBlackTheme = false;
            OnboardingCompleted = false;
            Profiles = new List<AppProfile>();
            Schedules = new List<ScheduleRule>();
            UserPresets = new List<ColorPreset>();
        }

        public static string ConfigDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Z4Vibrance");
            }
        }

        public static string ConfigFilePath
        {
            get { return Path.Combine(ConfigDirectory, "config.json"); }
        }

        public static string BackupFilePath
        {
            get { return Path.Combine(ConfigDirectory, "config.json.bak"); }
        }

        public static string SessionLockPath
        {
            get { return Path.Combine(ConfigDirectory, "session.lock"); }
        }

        public static bool WasPreviousSessionDirty()
        {
            try { return File.Exists(SessionLockPath); }
            catch { return false; }
        }

        public static void MarkSessionStarted()
        {
            try
            {
                if (!Directory.Exists(ConfigDirectory))
                    Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(SessionLockPath, DateTime.UtcNow.ToString("o"), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Warn("Oturum kilit dosyasi olusturulamadi: " + ex.Message);
            }
        }

        public static void MarkSessionCleanExit()
        {
            try
            {
                if (File.Exists(SessionLockPath))
                    File.Delete(SessionLockPath);
            }
            catch (Exception ex)
            {
                Logger.Warn("Oturum kilit dosyasi silinemedi: " + ex.Message);
            }
        }

        public static AppConfig Load()
        {
            AppConfig loaded = TryDeserialize(ConfigFilePath);
            if (loaded != null)
            {
                loaded.MigrateAndSanitize();
                return loaded;
            }

            // Ana dosya bozuksa .bak yedeğinden kurtar
            if (File.Exists(BackupFilePath))
            {
                Logger.Warn("config.json okunamadi, config.json.bak yedeginden geri yukleniyor.");
                loaded = TryDeserialize(BackupFilePath);
                if (loaded != null)
                {
                    loaded.MigrateAndSanitize();
                    loaded.Save();
                    return loaded;
                }
            }

            var fresh = new AppConfig { IsFreshConfig = true };
            fresh.MigrateAndSanitize();
            return fresh;
        }

        private static AppConfig TryDeserialize(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                var serializer = new JavaScriptSerializer();
                return serializer.Deserialize<AppConfig>(json);
            }
            catch (Exception ex)
            {
                Logger.Error("Yapilandirma dosyasi okunurken hata olustu: " + path, ex);
                return null;
            }
        }

        private void MigrateAndSanitize()
        {
            if (ConfigVersion < 2)
            {
                if (TransitionDurationMs <= 0) TransitionDurationMs = 180;
                if (WindowDebounceMs <= 0) WindowDebounceMs = 80;
            }
            if (ConfigVersion < 3)
            {
                if (DesktopFilterSaturation == 0) DesktopFilterSaturation = 100;
                if (Profiles != null)
                {
                    foreach (var p in Profiles)
                    {
                        if (p != null && p.TargetFilterSaturation == 0)
                            p.TargetFilterSaturation = 100;
                    }
                }
                if (Schedules != null)
                {
                    foreach (var s in Schedules)
                    {
                        if (s != null && s.FilterSaturation == 0)
                            s.FilterSaturation = 100;
                    }
                }
                if (UserPresets != null)
                {
                    foreach (var pr in UserPresets)
                    {
                        if (pr != null && pr.FilterSaturation == 0)
                            pr.FilterSaturation = 100;
                    }
                }
                ConfigVersion = CurrentConfigVersion;
            }

            if (string.IsNullOrEmpty(Language) || (Language != "tr" && Language != "en"))
                Language = "tr";

            TransitionDurationMs = Clamp(TransitionDurationMs, 0, 1000);
            WindowDebounceMs = Clamp(WindowDebounceMs, 0, 500);

            DesktopSaturation = Clamp(DesktopSaturation, 0, 400);
            DesktopFilterSaturation = Clamp(DesktopFilterSaturation, 0, 400);
            DesktopContrast = Clamp(DesktopContrast, 0, 200);
            DesktopBrightness = Clamp(DesktopBrightness, -100, 100);
            DesktopGamma = Clamp(DesktopGamma == 0 ? 100 : DesktopGamma, 50, 250);
            DesktopColorTemp = Clamp(DesktopColorTemp == 0 ? 6500 : DesktopColorTemp, 4000, 10000);

            if (Profiles == null)
                Profiles = new List<AppProfile>();
            Profiles.RemoveAll(p => p == null);

            foreach (var p in Profiles)
            {
                SanitizeProfile(p);
            }

            if (Schedules == null)
                Schedules = new List<ScheduleRule>();
            Schedules.RemoveAll(s => s == null);

            foreach (var s in Schedules)
            {
                if (string.IsNullOrEmpty(s.Id)) s.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrEmpty(s.Name)) s.Name = "Gece Modu";
                s.StartHour = Clamp(s.StartHour, 0, 23);
                s.StartMinute = Clamp(s.StartMinute, 0, 59);
                s.EndHour = Clamp(s.EndHour, 0, 23);
                s.EndMinute = Clamp(s.EndMinute, 0, 59);
                s.Saturation = Clamp(s.Saturation, 0, 400);
                s.FilterSaturation = Clamp(s.FilterSaturation, 0, 400);
                s.Contrast = Clamp(s.Contrast, 0, 200);
                s.Brightness = Clamp(s.Brightness, -100, 100);
                s.Gamma = Clamp(s.Gamma == 0 ? 100 : s.Gamma, 50, 250);
                s.ColorTemp = Clamp(s.ColorTemp == 0 ? 6500 : s.ColorTemp, 4000, 10000);
            }

            if (UserPresets == null)
                UserPresets = new List<ColorPreset>();
            UserPresets.RemoveAll(pr => pr == null);

            foreach (var pr in UserPresets)
            {
                if (string.IsNullOrEmpty(pr.Id)) pr.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrEmpty(pr.Name)) pr.Name = "Preset";
                pr.Saturation = Clamp(pr.Saturation, 0, 400);
                pr.FilterSaturation = Clamp(pr.FilterSaturation, 0, 400);
                pr.Contrast = Clamp(pr.Contrast, 0, 200);
                pr.Brightness = Clamp(pr.Brightness, -100, 100);
                pr.Gamma = Clamp(pr.Gamma == 0 ? 100 : pr.Gamma, 50, 250);
                pr.ColorTemp = Clamp(pr.ColorTemp == 0 ? 6500 : pr.ColorTemp, 4000, 10000);
            }
        }

        public static void SanitizeProfile(AppProfile p)
        {
            if (p == null)
                return;

            if (string.IsNullOrEmpty(p.Id)) p.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(p.Name)) p.Name = string.IsNullOrEmpty(p.ProcessName) ? "Profil" : p.ProcessName;
            p.TargetSaturation = Clamp(p.TargetSaturation, 0, 400);
            p.TargetFilterSaturation = Clamp(p.TargetFilterSaturation, 0, 400);
            p.TargetContrast = Clamp(p.TargetContrast, 0, 200);
            p.TargetBrightness = Clamp(p.TargetBrightness, -100, 100);
            p.TargetGamma = Clamp(p.TargetGamma == 0 ? 100 : p.TargetGamma, 50, 250);
            p.TargetColorTemp = Clamp(p.TargetColorTemp == 0 ? 6500 : p.TargetColorTemp, 4000, 10000);
            p.ApplyDelayMs = Clamp(p.ApplyDelayMs, 0, 5000);
            p.RevertDelayMs = Clamp(p.RevertDelayMs, 0, 5000);
            if (p.UsageSeconds < 0) p.UsageSeconds = 0;

            if (p.MonitorSettings == null)
            {
                p.MonitorSettings = new List<MonitorColorSetting>();
            }
            else
            {
                p.MonitorSettings.RemoveAll(m => m == null || string.IsNullOrWhiteSpace(m.DeviceName));
                foreach (var m in p.MonitorSettings)
                {
                    m.Saturation = Clamp(m.Saturation, 0, 400);
                    m.Contrast = Clamp(m.Contrast, 0, 200);
                    m.Brightness = Clamp(m.Brightness, -100, 100);
                    m.Gamma = Clamp(m.Gamma == 0 ? 100 : m.Gamma, 50, 250);
                    m.ColorTemp = Clamp(m.ColorTemp == 0 ? 6500 : m.ColorTemp, 4000, 10000);
                }
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(ConfigDirectory))
                    Directory.CreateDirectory(ConfigDirectory);

                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(this);
                string tmpPath = ConfigFilePath + ".tmp";

                File.WriteAllText(tmpPath, json, Encoding.UTF8);

                if (File.Exists(ConfigFilePath))
                {
                    File.Replace(tmpPath, ConfigFilePath, BackupFilePath, true);
                }
                else
                {
                    File.Move(tmpPath, ConfigFilePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Yapilandirma atomik olarak kaydedilemedi", ex);
            }
        }
    }
}
