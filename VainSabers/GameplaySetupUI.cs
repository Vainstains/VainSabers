using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.GameplaySetup;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Helpers;
using VainSabers.Menu;
using Zenject;

namespace VainSabers;

public class GameplaySetupUI : IInitializable, IDisposable, INotifyPropertyChanged
{
    private readonly PluginConfig m_config;
    private readonly MenuSaberManager m_menuSaberManager;
    
    private const string TabName = "VainSabers";

    public GameplaySetupUI(PluginConfig config, MenuSaberManager menuSaberManager)
    {
        m_config = config;
        m_menuSaberManager = menuSaberManager;
    }
    
    public void Initialize()
    {
        GameplaySetup.Instance.AddTab(TabName, "VainSabers.settings.bsml", this);
        
        MenuStateHandler.PresetListChanged += OnPresetListChanged;
        UpdatePresetDropdown();
        m_menuSaberManager.Update(m_config.CurrentSaber);
        
        if (m_config.MenuMode == MenuPointerDisplayMode.Pointer)
            m_menuSaberManager.UpdateMenuPreset(m_config.MenuSaberPreset);
    }
    
    public void Dispose()
    {
        MenuStateHandler.PresetListChanged -= OnPresetListChanged;
        if (GameplaySetup.Instance != null)
            GameplaySetup.Instance.RemoveTab(TabName);
    }

    private void OnPresetListChanged()
    {
        UpdatePresetDropdown();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedMenuPreset)));
        m_menuSaberManager.Update(m_config.CurrentSaber);
        UpdateEditorButtons();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    
    [UIValue("modEnabled")]
    private bool ModEnabled
    {
        get => m_config.Enabled;
        set
        {
            m_config.Enabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModEnabled)));
        }
    }
    
    [UIComponent("SaberPresetDropdown")]
#pragma warning disable CS0649
    public DropDownListSetting SaberPresetDropdown = null!;
#pragma warning restore CS0649

    [UIComponent("MenuPresetDropdown")]
#pragma warning disable CS0649
    public DropDownListSetting MenuPresetDropdown = null!;
#pragma warning restore CS0649

    [UIComponent("EditSaberButton")]
#pragma warning disable CS0649
    private HMUI.NoTransitionsButton? EditSaberButton = null;
#pragma warning restore CS0649

    [UIComponent("EditMenuButton")]
#pragma warning disable CS0649
    private HMUI.NoTransitionsButton? EditMenuButton = null;
#pragma warning restore CS0649

    [UIComponent("MenuPresetContainer")]
#pragma warning disable CS0649
    private UnityEngine.Transform? MenuPresetContainer = null;
#pragma warning restore CS0649

    // Keep legacy field for any existing BSML parse that still expects PresetDropdown/EditorButton (not used now)
    [UIComponent("PresetDropdown")]
#pragma warning disable CS0649
    public DropDownListSetting PresetDropDown = null!;
#pragma warning restore CS0649

    [UIComponent("EditorButton")]
#pragma warning disable CS0649
    private HMUI.NoTransitionsButton? EditorButton = null;
#pragma warning restore CS0649
    
    internal void UpdatePresetDropdown()
    {
        var names = GetPresetNames();
        PresetNames = names;
        MenuPresetNames = new List<object>(names);

        if (SaberPresetDropdown != null)
        {
            SaberPresetDropdown.Values = PresetNames;
            // Fix: find active preset and switch dropdown to right index after sorted insert
            int idx = PresetNames.FindIndex(o => o.ToString() == m_config.CurrentSaber);
            if (idx >= 0)
            {
                try { SaberPresetDropdown.Value = PresetNames[idx]; } catch {}
            }
            SaberPresetDropdown.UpdateChoices();
            // Ensure UI reflects correct value even if UpdateChoices resets it
            if (idx >= 0)
            {
                try { SaberPresetDropdown.Value = PresetNames[idx]; } catch {}
            }
        }
        
        if (PresetDropDown != null)
        {
            PresetDropDown.Values = PresetNames;
            int idx = PresetNames.FindIndex(o => o.ToString() == m_config.CurrentSaber);
            if (idx >= 0)
            {
                try { PresetDropDown.Value = PresetNames[idx]; } catch {}
            }
            PresetDropDown.UpdateChoices();
            if (idx >= 0)
            {
                try { PresetDropDown.Value = PresetNames[idx]; } catch {}
            }
        }
        if (MenuPresetDropdown != null)
        {
            MenuPresetDropdown.Values = MenuPresetNames;
            int idx = MenuPresetNames.FindIndex(o => o.ToString() == m_config.MenuSaberPreset);
            if (idx >= 0)
            {
                try { MenuPresetDropdown.Value = MenuPresetNames[idx]; } catch {}
            }
            MenuPresetDropdown.UpdateChoices();
            if (idx >= 0)
            {
                try { MenuPresetDropdown.Value = MenuPresetNames[idx]; } catch {}
            }
        }

        // Also notify BSML binding for SelectedPreset/MenuPreset so text matches
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedMenuPreset)));

        UpdateEditorButtons();
        UpdateMenuPresetVisibility();
    }

    private static List<object> GetPresetNames()
    {
        if (!Directory.Exists(Config.ConfigUtil.ConfigDir))
            return new List<object>();

        var jsonFiles = Directory.GetFiles(Config.ConfigUtil.ConfigDir, "*.json");
        var vainsaberFiles = Directory.GetFiles(Config.ConfigUtil.ConfigDir, "*.vainsaber");
        var names = jsonFiles.Select(Path.GetFileNameWithoutExtension)
            .Concat(vainsaberFiles.Select(Path.GetFileNameWithoutExtension))
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .Cast<object>()
            .ToList();
        Plugin.Log.Info($"Found {names.Count} Presets");

        return names;
    }
    
    [UIValue("PresetNames")]
    private List<object> PresetNames = GetPresetNames();

    [UIValue("MenuPresetNames")]
    private List<object> MenuPresetNames = GetPresetNames();

    [UIValue("SelectedPreset")]
    private string SelectedPreset
    {
        get => m_config.CurrentSaber;
        set
        {
            m_config.CurrentSaber = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
            m_menuSaberManager.Update(value);
            MenuStateHandler.SetEditingPreset(value);
            UpdateEditorButtons();
        }
    }

    [UIValue("SelectedMenuPreset")]
    private string SelectedMenuPreset
    {
        get => m_config.MenuSaberPreset;
        set
        {
            m_config.MenuSaberPreset = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedMenuPreset)));
            m_menuSaberManager.UpdateMenuPreset(value);
            // If we are editing menu preset, keep editing target in sync?
            // Do not change EditingPreset here unless user explicitly edits menu preset; keep separate
            UpdateEditorButtons();
        }
    }

    [UIValue("menuModeChoices")]
    private List<object> menuModeChoices = Enum.GetValues(typeof(MenuPointerDisplayMode)).Cast<object>().ToList();

    [UIValue("menuMode")]
    private MenuPointerDisplayMode menuMode
    {
        get => m_config.MenuMode;
        set
        {
            m_config.MenuMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(menuMode)));
            UpdateMenuPresetVisibility();
            // Ensure visibility and preset update - use full SetActive with current panel state
            m_menuSaberManager.RefreshVisibility();
            if (value == MenuPointerDisplayMode.Pointer)
                m_menuSaberManager.UpdateMenuPreset(m_config.MenuSaberPreset);
            else if (value == MenuPointerDisplayMode.Saber)
                m_menuSaberManager.Update(m_config.CurrentSaber);
        }
    }

    private void UpdateMenuPresetVisibility()
    {
        if (MenuPresetContainer != null)
        {
            bool show = m_config.MenuMode == MenuPointerDisplayMode.Pointer;
            MenuPresetContainer.gameObject.SetActive(show);
        }
    }

    [UIAction("EditSaberPreset")]
    private void EditSaberPreset()
    {
        if (string.IsNullOrEmpty(m_config.CurrentSaber))
            return;
        if (IsPresetReadOnly(m_config.CurrentSaber))
            return;
        MenuStateHandler.SetEditingPreset(m_config.CurrentSaber);
        MenuStateHandler.SetEditorOpen(true);
    }

    [UIAction("EditMenuPreset")]
    private void EditMenuPreset()
    {
        if (string.IsNullOrEmpty(m_config.MenuSaberPreset))
            return;
        if (IsPresetReadOnly(m_config.MenuSaberPreset))
            return;
        MenuStateHandler.SetEditingPreset(m_config.MenuSaberPreset);
        MenuStateHandler.SetEditorOpen(true);
    }

    public void ToggleEditor() => MenuStateHandler.SetEditorOpen(true);

    public void ToggleSettingsPanel() => MenuStateHandler.ToggleSettingsOpen();

    private bool IsPresetReadOnly(string preset)
    {
        if (string.IsNullOrEmpty(preset))
            return false;
        var profile = Config.ConfigUtil.GetSaberProfile(preset);
        return profile.EndsWith(".vainsaber", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateEditorButtons()
    {
        if (EditSaberButton != null)
            EditSaberButton.interactable = !IsPresetReadOnly(m_config.CurrentSaber);
        if (EditMenuButton != null)
            EditMenuButton.interactable = !IsPresetReadOnly(m_config.MenuSaberPreset);
        // Legacy
        if (EditorButton != null)
            EditorButton.interactable = !IsPresetReadOnly(m_config.CurrentSaber);
    }
    
    [UIAction("CreateNewPreset")]
    private void CreateNewPreset()
    {
        if (!Directory.Exists(Config.ConfigUtil.ConfigDir))
            Directory.CreateDirectory(Config.ConfigUtil.ConfigDir);
        
        string baseName = "NewPreset";
        string presetName = baseName;
        int counter = 0;
        
        while (File.Exists(Path.Combine(Config.ConfigUtil.ConfigDir, $"{presetName}.json")))
        {
            counter++;
            presetName = $"{baseName}{counter}";
        }
        
        string presetPath = Path.Combine(Config.ConfigUtil.ConfigDir, $"{presetName}.json");
        File.WriteAllText(presetPath, "{\"version\":2,\"parts\":[]}");
        
        Plugin.Log.Info($"Created new empty preset: {presetName} at {presetPath}");
        
        UpdatePresetDropdown();
        
        SelectedPreset = presetName;
    }
    
    [UIObject("root")]
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    private GameObject? _root;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value

    [UIAction("#post-parse")]
    private void OnAfterParse() {
        _root?.AddInitComponent<MenuStateHandler>(m_config);
        _root?.AddInitComponent<SaberEditorController>(m_config);
        _root?.AddInitComponent<SaberSettingsPanelController>(m_config);
        UpdateEditorButtons();
        UpdateMenuPresetVisibility();
        // Ensure dropdown choices are populated after parse (BSML creates components after #post-parse? Do again)
        UpdatePresetDropdown();
    }
}
