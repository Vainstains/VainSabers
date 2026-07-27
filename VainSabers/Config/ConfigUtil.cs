using System;
using System.IO;
using System.Linq;
using System.Reflection;
using IPA.Utilities;
using UnityEngine;
using VainSabers.Sabers;

namespace VainSabers.Config;

public static class ConfigUtil
{
    public static readonly string ConfigDir = Path.Combine(UnityGame.UserDataPath, "VainSabers");

    public static void EnsureDefaultExists()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            
            MigrateLegacyTxtFiles();

            bool hasJsonConfigs = Directory
                .EnumerateFiles(ConfigDir, "*.json", SearchOption.TopDirectoryOnly)
                .Any();

            if (hasJsonConfigs)
                return;

            Plugin.Log.Info("No JSON configs found. Extracting default configs...");

            Assembly asm = Assembly.GetExecutingAssembly();
            
            const string resourcePrefix = "VainSabers.Config.Defaults.";

            foreach (string resourceName in asm.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(resourcePrefix) ||
                    !resourceName.EndsWith(".json"))
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

    private static void MigrateLegacyTxtFiles()
    {
        try
        {
            var txtFiles = Directory.GetFiles(ConfigDir, "*.txt", SearchOption.TopDirectoryOnly);
            foreach (var txtPath in txtFiles)
            {
                var jsonPath = Path.ChangeExtension(txtPath, ".json");
                if (File.Exists(jsonPath))
                {
                    Plugin.Log.Debug($"Skipping migration of {Path.GetFileName(txtPath)} (JSON already exists)");
                    continue;
                }

                Plugin.Log.Info($"Migrating legacy preset: {Path.GetFileName(txtPath)}");
                BlurSaberData.ConvertLegacyFile(txtPath);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to migrate legacy txt files: {ex}");
        }
    }

    internal static string GetSaberProfile(string name)
    {
        string jsonPath = Path.Combine(ConfigDir, $"{name}.json");
        if (File.Exists(jsonPath))
            return jsonPath;

        string txtPath = Path.Combine(ConfigDir, $"{name}.txt");
        if (File.Exists(txtPath))
            return txtPath;

        Plugin.Log.Warn($"Saber profile '{name}' not found.");
        return jsonPath;
    }
}
