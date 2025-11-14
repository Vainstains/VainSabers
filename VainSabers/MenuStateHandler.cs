using System;
using UnityEngine;
using VainSabers.Menu;
using VainSabers.Sabers;

namespace VainSabers;

internal class MenuStateHandler : MonoBehaviour
{
    public struct ModPanelState
    {
        public bool EditorOpen = false;
        public bool ConfigOpen = false;
        public string EditingPreset = "";

        public ModPanelState(bool configOpen, bool editorOpen, string preset)
        {
            ConfigOpen = configOpen;
            EditorOpen = editorOpen;
            EditingPreset = preset;
        }
    }
    
    public static event Action<ModPanelState> ModPanelStateChanged = null!;
    
    private static ModPanelState s_modPanelState = new ModPanelState(false, false, "");
    
    public static (BlurSaber left, BlurSaber right) Sabers { get; set; }
    private void OnEnable() {
        s_modPanelState.ConfigOpen = true;
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }

    private void OnDisable() {
        s_modPanelState.ConfigOpen = true;
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }

    public static void ToggleEditorOpen()
    {
        s_modPanelState.EditorOpen = !s_modPanelState.EditorOpen;
        Plugin.Log.Info($"Toggling saber editor state: {s_modPanelState.EditorOpen}");
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }

    public static void SetEditingPreset(string preset)
    {
        s_modPanelState.EditingPreset = preset;
        ModPanelStateChanged?.Invoke(s_modPanelState);
    }
}