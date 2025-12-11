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
        
        UpdateLegacyPresetDropdown();
        UpdatePresetDropdown();
        m_menuSaberManager.Update(m_config.CurrentSaber);
    }
    
    public void Dispose()
    {
        if (GameplaySetup.Instance != null)
            GameplaySetup.Instance.RemoveTab(TabName);
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
    
    [UIValue("legacyEnabled")]
    private bool LegacyEnabled
    {
        get => m_config.UseLegacy;
        set
        {
            m_config.UseLegacy = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LegacyEnabled)));
        }
    }
    
    [UIComponent("LegacyPresetDropdown")]
#pragma warning disable CS0649
    public DropDownListSetting LegacyPresetDropDown = null!;
#pragma warning restore CS0649
    
    [UIComponent("PresetDropdown")]
#pragma warning disable CS0649
    public DropDownListSetting PresetDropDown = null!;
#pragma warning restore CS0649
    
    internal void UpdateLegacyPresetDropdown()
    {
        if (LegacyPresetDropDown == null)
        {
            return;
        }

        LegacyPresetNames = GetLegacyPresetNames();
            
        LegacyPresetDropDown.Values = LegacyPresetNames;
        LegacyPresetDropDown.UpdateChoices();
    }
    
    internal void UpdatePresetDropdown()
    {
        if (PresetDropDown == null)
        {
            return;
        }

        PresetNames = GetPresetNames();
            
        PresetDropDown.Values = PresetNames;
        PresetDropDown.UpdateChoices();
    }

    private static List<object> GetLegacyPresetNames()
    {
        List<string> files = Directory.GetFiles(Config.ConfigUtil.LegacyConfigDir, "*.json").ToList();
        files.Sort();
        Plugin.Log.Info($"Found {files.Count} LegacyPresets");

        return files.Select(Path.GetFileNameWithoutExtension).Cast<object>().ToList();
    }
    private static List<object> GetPresetNames()
    {
        List<string> files = Directory.GetFiles(Config.ConfigUtil.ConfigDir, "*.txt").ToList();
        files.Sort();
        Plugin.Log.Info($"Found {files.Count} Presets");

        return files.Select(Path.GetFileNameWithoutExtension).Cast<object>().ToList();
    }

    [UIValue("LegacyPresetNames")]
    private List<object> LegacyPresetNames = GetLegacyPresetNames();

    [UIValue("SelectedLegacyPreset")]
    private string SelectedLegacyPreset
    {
        get => m_config.CurrentLegacySaber;
        set
        {
            m_config.CurrentLegacySaber = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedLegacyPreset)));
        }
    }
    
    [UIValue("PresetNames")]
    private List<object> PresetNames = GetPresetNames();

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
        }
    }
    
    [UIValue("BlurMilliseconds")]
    private int BlurMilliseconds
    {
        get => m_config.BlurMS;
        set
        {
            m_config.BlurMS = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BlurMilliseconds)));
        }
    }
    
    [UIValue("BladeMilliseconds")]
    private int BladeMilliseconds
    {
        get => m_config.BladeTrailMS;
        set
        {
            m_config.BladeTrailMS = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BladeMilliseconds)));
        }
    }
    
    [UIValue("TipMilliseconds")]
    private int TipMilliseconds
    {
        get => m_config.TipTrailMS;
        set
        {
            m_config.TipTrailMS = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TipMilliseconds)));
        }
    }
    
    [UIValue("showInMenu")]
    private bool ShowInMenu
    {
        get => m_config.ActiveInMenu;
        set
        {
            m_config.ActiveInMenu = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowInMenu)));
        }
    }
    
    public void ToggleEditor() => MenuStateHandler.ToggleEditorOpen();
    
    [UIAction("CreateNewPreset")]
    private void CreateNewPreset()
    {
        if (!Directory.Exists(Config.ConfigUtil.ConfigDir))
            Directory.CreateDirectory(Config.ConfigUtil.ConfigDir);
        
        // Generate a unique preset name
        string baseName = "NewPreset";
        string presetName = baseName;
        int counter = 0;
        
        // Find a unique name
        while (File.Exists(Path.Combine(Config.ConfigUtil.ConfigDir, $"{presetName}.txt")))
        {
            counter++;
            presetName = $"{baseName}{counter}";
        }
        
        // Create empty preset file
        string presetPath = Path.Combine(Config.ConfigUtil.ConfigDir, $"{presetName}.txt");
        File.WriteAllText(presetPath, string.Empty);
        
        Plugin.Log.Info($"Created new empty preset: {presetName} at {presetPath}");
        
        // Refresh the dropdown
        UpdatePresetDropdown();
        
        // Select the new preset
        SelectedPreset = presetName;
    }
    
    [UIObject("root")]
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    private GameObject? _root;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value

    [UIAction("#post-parse")]
    private void OnAfterParse() {
        _root?.AddComponent<MenuStateHandler>();
        _root?.AddInitComponent<SaberEditorController>(m_config);
    }
}