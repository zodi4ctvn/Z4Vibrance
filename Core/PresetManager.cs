using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using VibranceZ.Models;

namespace VibranceZ.Core
{
    public static class PresetManager
    {
        public static int ScaleSaturationForBackend(IVibranceBackend backend, int amdScaleSaturation)
        {
            if (backend == null)
                return amdScaleSaturation;

            int min = backend.MinLevel;
            int max = backend.MaxLevel;
            int def = backend.DefaultLevel;

            // AMD / Demo ölçeği (varsayılan ~100, maks >= 200)
            if (max >= 200 && def >= 50)
                return Math.Max(min, Math.Min(max, amdScaleSaturation));

            // NVIDIA NVAPI vb. ölçekler (örn. 0..63, varsayılan 0)
            if (amdScaleSaturation >= 100)
            {
                double ratio = Math.Min(1.0, (amdScaleSaturation - 100) / 100.0);
                int scaled = def + (int)Math.Round(ratio * (max - def));
                return Math.Max(min, Math.Min(max, scaled));
            }
            else
            {
                double ratio = Math.Max(0.0, amdScaleSaturation / 100.0);
                int scaled = min + (int)Math.Round(ratio * (def - min));
                return Math.Max(min, Math.Min(max, scaled));
            }
        }

        public static int GetDefaultProfileSaturation(IVibranceBackend backend)
        {
            if (backend == null)
                return 200;

            if (backend.MaxLevel >= 200 && backend.DefaultLevel >= 50)
                return Math.Min(backend.MaxLevel, 200);

            int boost = (int)Math.Round((backend.MaxLevel - backend.DefaultLevel) * 0.65);
            return Math.Max(backend.MinLevel, Math.Min(backend.MaxLevel, backend.DefaultLevel + boost));
        }

        public static List<ColorPreset> GetBuiltInPresets(IVibranceBackend backend = null)
        {
            return new List<ColorPreset>
            {
                new ColorPreset
                {
                    Id = "builtin_fps",
                    Name = L.Get("PresetFps"),
                    IsBuiltIn = true,
                    Saturation = ScaleSaturationForBackend(backend, 180),
                    FilterSaturation = 115,
                    Contrast = 110,
                    Brightness = 5,
                    Gamma = 110,
                    ColorTemp = 6500
                },
                new ColorPreset
                {
                    Id = "builtin_overdrive",
                    Name = L.Get("PresetOverdrive"),
                    IsBuiltIn = true,
                    Saturation = ScaleSaturationForBackend(backend, 220),
                    FilterSaturation = 220,
                    Contrast = 108,
                    Brightness = 2,
                    Gamma = 106,
                    ColorTemp = 6500
                },
                new ColorPreset
                {
                    Id = "builtin_cinematic",
                    Name = L.Get("PresetCinematic"),
                    IsBuiltIn = true,
                    Saturation = ScaleSaturationForBackend(backend, 135),
                    FilterSaturation = 100,
                    Contrast = 115,
                    Brightness = -4,
                    Gamma = 95,
                    ColorTemp = 6200
                },
                new ColorPreset
                {
                    Id = "builtin_natural",
                    Name = L.Get("PresetNatural"),
                    IsBuiltIn = true,
                    Saturation = ScaleSaturationForBackend(backend, 100),
                    FilterSaturation = 100,
                    Contrast = 100,
                    Brightness = 0,
                    Gamma = 100,
                    ColorTemp = 6500
                },
                new ColorPreset
                {
                    Id = "builtin_eyecare",
                    Name = L.Get("PresetEyeCare"),
                    IsBuiltIn = true,
                    Saturation = ScaleSaturationForBackend(backend, 90),
                    FilterSaturation = 100,
                    Contrast = 95,
                    Brightness = -5,
                    Gamma = 100,
                    ColorTemp = 5000
                },
                new ColorPreset
                {
                    Id = "builtin_reading",
                    Name = L.Get("PresetReading"),
                    IsBuiltIn = true,
                    Saturation = ScaleSaturationForBackend(backend, 75),
                    FilterSaturation = 100,
                    Contrast = 92,
                    Brightness = -8,
                    Gamma = 98,
                    ColorTemp = 4800
                }
            };
        }

        public static bool ExportProfile(AppProfile profile, string filePath)
        {
            if (profile == null || string.IsNullOrEmpty(filePath))
                return false;

            try
            {
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(profile);
                File.WriteAllText(filePath, json, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Profil disa aktarilamadi", ex);
                return false;
            }
        }

        public static AppProfile ImportProfile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                var serializer = new JavaScriptSerializer();
                var imported = serializer.Deserialize<AppProfile>(json);
                if (imported == null)
                    return null;

                imported.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrEmpty(imported.Name))
                    imported.Name = string.IsNullOrEmpty(imported.ProcessName) ? "Imported" : imported.ProcessName;

                AppConfig.SanitizeProfile(imported);
                return imported;
            }
            catch (Exception ex)
            {
                Logger.Error("Profil ice aktarilamadi", ex);
                return null;
            }
        }
    }
}
