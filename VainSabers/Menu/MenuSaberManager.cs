using System;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Helpers;
using VainSabers.Sabers;

namespace VainSabers.Menu;

public class MenuSaberManager : IDisposable
{
    private readonly PluginConfig m_config;
    private readonly MenuPointers m_menuPointers;
    private readonly BlurSaber m_leftSaber;
    private readonly BlurSaber m_rightSaber;
    private readonly ColorSchemesSettings m_colorSchemesSettings;
    
    private readonly Color defaultColorLeft = new Color32(0xC8, 0x14, 0x14, 0xFF);
    private readonly Color defaultColorRight = new Color32(0x28, 0x8E, 0xD2, 0xFF);
    
    private (BlurSaber left, BlurSaber right) Sabers => (m_leftSaber, m_rightSaber);
    
    public MenuSaberManager(MenuPointers menuPointers, ColorSchemesSettings colorSchemesSettings, PluginConfig config)
    {
        m_config = config;
        m_menuPointers = menuPointers;
        var (left, right) = menuPointers.Parents;
        m_leftSaber = SetupSaber(left, true);
        m_rightSaber = SetupSaber(right, false);
        m_colorSchemesSettings = colorSchemesSettings;
        
        MenuStateHandler.ModPanelStateChanged += OnModPanelStateChanged;
        MenuStateHandler.Sabers = Sabers;
        
        SetActive(false);
    }

    private void OnModPanelStateChanged(MenuStateHandler.ModPanelState state)
    {
        SetActive(state.ConfigOpen, state.EditorOpen);
    }

    private BlurSaber SetupSaber(Transform parent, bool isLeft)
    {
        var saberObj = new GameObject("BlurSaber");
        var blurSaber = saberObj.AddInitComponent<BlurSaber>(parent, m_config);
        blurSaber.Data.IsLeftSaber = isLeft;
        blurSaber.ApplyZRotationOffset();
        blurSaber.gameObject.SetActive(m_config.ActiveInMenu);
        return blurSaber;
    }

    public void ApplyZRotationOffset()
    {
        m_leftSaber.ApplyZRotationOffset();
        m_rightSaber.ApplyZRotationOffset();
    }

    public void SetColor(Color left, Color right)
    {
        m_leftSaber.SetColor(left);
        m_rightSaber.SetColor(right);
    }

    public void SetActive(bool active, bool editorOpen = false)
    {
        active = active || m_config.ActiveInMenu;

        if (Helpers.Helpers.GetIsFpfc())
        {
            active = editorOpen;
        }

        m_menuPointers.SetPointerVisibility(!active);
        m_leftSaber.gameObject.SetActive(active);
        m_rightSaber.gameObject.SetActive(active);
        
        var selectedColorScheme = m_colorSchemesSettings.GetOverrideColorScheme();
        var (colorLeft, colorRight) = selectedColorScheme is null ? (defaultColorLeft, defaultColorRight)
            : (selectedColorScheme.saberAColor, selectedColorScheme.saberBColor);
        
        SetColor(colorLeft, colorRight);
        
        m_leftSaber.SetPreset(m_config.CurrentSaber);
        m_rightSaber.SetPreset(m_config.CurrentSaber);
    }

    public void Update(string presetName)
    {
        var selectedColorScheme = m_colorSchemesSettings.GetOverrideColorScheme();
        var (colorLeft, colorRight) = selectedColorScheme is null ? (defaultColorLeft, defaultColorRight)
            : (selectedColorScheme.saberAColor, selectedColorScheme.saberBColor);
        
        SetColor(colorLeft, colorRight);
        m_leftSaber.SetPreset(presetName);
        m_rightSaber.SetPreset(presetName);
    }

    public void NotifyColorSchemeUpdated()
    {
        var selectedColorScheme = m_colorSchemesSettings.GetOverrideColorScheme();
        var (colorLeft, colorRight) = selectedColorScheme is null ? (defaultColorLeft, defaultColorRight)
            : (selectedColorScheme.saberAColor, selectedColorScheme.saberBColor);
        
        SetColor(colorLeft, colorRight);
    }
    
    public void Dispose()
    {
        MenuStateHandler.ModPanelStateChanged -= OnModPanelStateChanged;
    }
}