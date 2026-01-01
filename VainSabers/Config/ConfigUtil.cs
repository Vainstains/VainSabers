using System;
using System.IO;
using IPA.Utilities;
using UnityEngine;

namespace VainSabers.Config;

public static class ConfigUtil
{
    public static readonly string ConfigDir = Path.Combine(UnityGame.UserDataPath, "VainSabers");

    public static void EnsureDefaultExists()
    {
        
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