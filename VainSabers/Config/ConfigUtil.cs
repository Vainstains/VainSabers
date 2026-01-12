using System;
using System.IO;
using System.Linq;
using System.Reflection;
using IPA.Utilities;
using UnityEngine;

namespace VainSabers.Config;

public static class ConfigUtil
{
    public static readonly string ConfigDir = Path.Combine(UnityGame.UserDataPath, "VainSabers");

    public static void EnsureDefaultExists()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            bool hasConfigs = Directory
                .EnumerateFiles(ConfigDir, "*.txt", SearchOption.TopDirectoryOnly)
                .Any();

            if (hasConfigs)
                return;

            Plugin.Log.Info("No configs found. Extracting default configs...");

            Assembly asm = Assembly.GetExecutingAssembly();
            
            const string resourcePrefix = "VainSabers.Config.Defaults.";

            foreach (string resourceName in asm.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(resourcePrefix) ||
                    !resourceName.EndsWith(".txt"))
                    continue;

                string fileName = resourceName.Substring(resourcePrefix.Length);
                string outputPath = Path.Combine(ConfigDir, fileName);

                using Stream resourceStream = asm.GetManifestResourceStream(resourceName);
                if (resourceStream == null)
                    continue;

                using FileStream fileStream = File.Create(outputPath);
                resourceStream.CopyTo(fileStream);

                Plugin.Log.Debug($"Extracted default config: {fileName}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to extract default configs: {ex}");
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