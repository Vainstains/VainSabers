using System;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Menu;
using VainSabers.Sabers;

namespace VainSabers;

internal class MenuStateHandler : MonoBehaviour
{
    public struct ModPanelState
    {
        public bool EditorOpen = false;
        public bool ConfigOpen = false;
        public bool SettingsOpen = false;
        public string EditingPreset = "";

        public ModPanelState(bool configOpen, bool editorOpen, string preset)
        {
            ConfigOpen = configOpen;
            EditorOpen = editorOpen;
            EditingPreset = preset;
        }
    }
    
    private PluginConfig m_config = null!;

    public void Init(PluginConfig config)
    {
        m_config = config;
    }
    
    public static event Action<ModPanelState> ModPanelStateChanged = null!;
    public static event Action? PresetListChanged = null!;
    
    private static ModPanelState s_modPanelState = new ModPanelState(false, false, "");
    
    public static (BlurSaber left, BlurSaber right) Sabers { get; set; }
    private void OnEnable() {
        s_modPanelState.ConfigOpen = true;
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }

    private void OnDisable() {
        s_modPanelState.ConfigOpen = false;
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }

    public static void ToggleEditorOpen()
    {
        s_modPanelState.EditorOpen = !s_modPanelState.EditorOpen;
        Plugin.Log.Info($"Toggling saber editor state: {s_modPanelState.EditorOpen}");
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }
    
    public static void SetEditorOpen(bool open)
    {
        if (s_modPanelState.EditorOpen == open)
            return;
        s_modPanelState.EditorOpen = open;
        Plugin.Log.Info($"Setting saber editor state: {s_modPanelState.EditorOpen}");
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }

    public static void SetEditingPreset(string preset)
    {
        s_modPanelState.EditingPreset = preset;
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }

    public static void ToggleSettingsOpen()
    {
        s_modPanelState.SettingsOpen = !s_modPanelState.SettingsOpen;
        Plugin.Log.Info($"Toggling settings panel state: {s_modPanelState.SettingsOpen}");
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }

    public static void SetSettingsOpen(bool open)
    {
        if (s_modPanelState.SettingsOpen == open)
            return;
        s_modPanelState.SettingsOpen = open;
        Plugin.Log.Info($"Setting settings panel state: {open}");
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }

    public static void NotifyPresetListChanged()
    {
        PresetListChanged?.Invoke();
    }
}