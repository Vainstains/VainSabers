using System;
using System.IO;
using IPA.Utilities;
using UnityEngine;
using VainSabers.Legacy;

namespace VainSabers.Config;

public static class ConfigUtil
{
    public static readonly string ConfigDir = Path.Combine(UnityGame.UserDataPath, "VainSabers");
    public static readonly string LegacyConfigDir = Path.Combine(Path.Combine(UnityGame.UserDataPath, "Legacy"), "VainSabers");
    
    public static void EnsureDefaultExists()
    {
        Directory.CreateDirectory(LegacyConfigDir);
        string defaultPath = Path.Combine(LegacyConfigDir, "default.json");

        if (!File.Exists(defaultPath))
        {
            var saberProfile = new LegacySaberProfile(new[]
                {
                    // pommel
                    new LegacySaberProfile.ProfileVertex(-0.155f, 0.0000f, 1.0f, 0.0f, 0.2f, 0.2f),
                    new LegacySaberProfile.ProfileVertex(-0.153f, 0.0071f, 1.0f, 0.0f, 0.2f, 0.2f),
                    new LegacySaberProfile.ProfileVertex(-0.1347f, 0.0110f, 1.0f, 0.0f, 0.2f, 0.2f),

                    // handle
                    new LegacySaberProfile.ProfileVertex(-0.1237f, 0.00825f, 0.0f, 0.0f, 0.0f, 0.1f),
                    new LegacySaberProfile.ProfileVertex(-0.0266f, 0.0103f, 0.0f, 0.0f, 0.0f, 0.1f),
                    new LegacySaberProfile.ProfileVertex(-0.0211f, 0.0126f, 0.0f, 0.0f, 0.0f, 0.1f),
                    new LegacySaberProfile.ProfileVertex(-0.0040f, 0.0126f, 0.0f, 0.0f, 0.0f, 0.1f),

                    // blade
                    new LegacySaberProfile.ProfileVertex(0.0050f, 0.0110f, 0.9f, 0.2f, 0.2f, 0.2f),
                    new LegacySaberProfile.ProfileVertex(0.0567f, 0.0110f, 0.9f, 0.1f, 0.3f, 0.3f),
                    new LegacySaberProfile.ProfileVertex(0.1746f, 0.0110f, 0.9f, 0.0f, 0.5f, 0.4f),

                    new LegacySaberProfile.ProfileVertex(0.8488f, 0.0080f, 1.1f, 0.0f, 0.9f, 0.8f),
                    new LegacySaberProfile.ProfileVertex(0.9513f, 0.0060f, 1.0f, 0.3f, 0.2f, 0.6f),
                    new LegacySaberProfile.ProfileVertex(1.0000f, 0.0030f, 1.0f, 0.3f, 0.2f, 0.2f),
                },
                0.16f, 0.5f);

            saberProfile.Save(defaultPath);
            Plugin.Log.Info($"Default saber profile created at {defaultPath}");
        }
    }

    internal static LegacySaberProfile GetLegacySaberProfile(string name)
    {
        string path = Path.Combine(LegacyConfigDir, $"{name}.json");
        if (!File.Exists(path))
        {
            Plugin.Log.Warn($"Saber profile '{name}' not found. Falling back to default.");
            path = Path.Combine(LegacyConfigDir, "default.json");
        }

        try
        {
            return LegacySaberProfile.Load(path);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to load saber profile '{name}' from {path}: {ex.Message}");
            // Fallback: return default profile
            string defaultPath = Path.Combine(LegacyConfigDir, "default.json");
            return File.Exists(defaultPath) ? LegacySaberProfile.Load(defaultPath) : null!;
        }
    }
    
    internal static string GetSaberProfile(string name)
    {
        string path = Path.Combine(ConfigDir, $"{name}.txt");
        if (!File.Exists(path))
        {
            Plugin.Log.Warn($"Saber profile '{name}' not found.");
        }

        return path;
    }
}