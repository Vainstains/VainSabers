using System;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Menu;
using VainSabers.Sabers;

namespace VainSabers;

public enum EditorReturnDestination
{
    Menu,
    Settings
}

internal class MenuStateHandler : MonoBehaviour
{
    public struct ModPanelState
    {
        public EditorReturnDestination Destination = EditorReturnDestination.Menu;
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

    public static ModPanelState CurrentState => s_modPanelState;
    public static string CurrentEditingPreset => s_modPanelState.EditingPreset;
    public static bool IsEditorOpen => s_modPanelState.EditorOpen;
    public static bool IsConfigOpen => s_modPanelState.ConfigOpen;

    public static (BlurSaber left, BlurSaber right) Sabers { get; set; }
    private void OnEnable()
    {
        s_modPanelState.ConfigOpen = true;
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }

    private void OnDisable()
    {
        s_modPanelState.ConfigOpen = false;
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }

    // split this into two methods why the hell was it ever one in the first place
    public static void OpenEditor(EditorReturnDestination? destination)
    {
        if (destination.HasValue)
            s_modPanelState.Destination = destination.Value;
        if (s_modPanelState.EditorOpen || s_modPanelState.SettingsOpen)
        {
            SetSettingsOpen(false);
        }
        if (s_modPanelState.EditorOpen)
            return;
        s_modPanelState.EditorOpen = true;
        Plugin.Log.Info($"Opening saber editor");
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }

    public static void CloseEditor()
    {
        s_modPanelState.EditorOpen = false;
        Plugin.Log.Info($"Closing saber editor");
        ModPanelStateChanged?.Invoke(s_modPanelState);

        if (s_modPanelState.Destination == EditorReturnDestination.Settings)
        {
            Plugin.Log.Info("Returning to settings");
            SetSettingsOpen(true);
        }
    }

    public static void SetEditingPreset(string preset)
    {
        s_modPanelState.EditingPreset = preset;
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }

    public static void ToggleSettingsOpen()
    {
        if (!s_modPanelState.SettingsOpen &&
            s_modPanelState.EditorOpen)
        {
            Plugin.Log.Info("no n togglke settingso");
            return;
        }

        s_modPanelState.SettingsOpen = !s_modPanelState.SettingsOpen;
        Plugin.Log.Info($"Toggling settings panel state: {s_modPanelState.SettingsOpen}");
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }

    public static void SetSettingsOpen(bool open)
    {
        if (open && s_modPanelState.EditorOpen)
        {
            Plugin.Log.Info("no settings");
            return;
        }

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