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
    
    private GameObject? m_leftStaticAnchor;
    private GameObject? m_rightStaticAnchor;
    private bool m_staticDisplayActive;
    
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
        bool shouldShow = ShouldShowMenuSabers;
        blurSaber.gameObject.SetActive(shouldShow);
        return blurSaber;
    }

    private bool ShouldShowMenuSabers => m_config.Enabled && (m_config.MenuMode == MenuPointerDisplayMode.Saber || m_config.MenuMode == MenuPointerDisplayMode.Pointer);

    private string ActivePreset => m_config.MenuMode == MenuPointerDisplayMode.Pointer ? m_config.MenuSaberPreset : m_config.CurrentSaber;

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

    public void SetActive(bool configOpen, bool editorOpen = false)
    {
        bool panelOpen = configOpen;
        bool shouldShow = ShouldShowMenuSabers;

        bool active;
        string presetToUse;

        if (editorOpen)
        {
            // During editing, always show the saber being edited in hand, regardless of MenuMode
            string editingPreset = MenuStateHandler.CurrentEditingPreset;
            if (string.IsNullOrEmpty(editingPreset))
                editingPreset = ActivePreset;
            if (string.IsNullOrEmpty(editingPreset))
                editingPreset = m_config.CurrentSaber;
            active = true;
            presetToUse = editingPreset;
            // Editor controls its own preview anchors for FPFC, so ensure static display is exited
            ExitStaticDisplay();
        }
        else
        {
            active = shouldShow;
            presetToUse = ActivePreset;

            if (Helpers.Helpers.GetIsFpfc())
            {
                if (shouldShow && panelOpen && !editorOpen)
                    EnterStaticDisplay();
                else
                    ExitStaticDisplay();
            }
        }

        m_menuPointers.SetPointerVisibility(!active);
        m_leftSaber.gameObject.SetActive(active);
        m_rightSaber.gameObject.SetActive(active);
        
        var selectedColorScheme = m_colorSchemesSettings.GetOverrideColorScheme();
        var (colorLeft, colorRight) = selectedColorScheme is null ? (defaultColorLeft, defaultColorRight)
            : (selectedColorScheme.saberAColor, selectedColorScheme.saberBColor);
        
        SetColor(colorLeft, colorRight);
        
        if (active)
        {
            if (!string.IsNullOrEmpty(presetToUse))
            {
                m_leftSaber.SetPreset(presetToUse);
                m_rightSaber.SetPreset(presetToUse);
            }
        }
    }

    private void EnterStaticDisplay()
    {
        if (m_staticDisplayActive) return;

        m_leftStaticAnchor = new GameObject("LeftSaberStaticAnchor");
        m_leftStaticAnchor.transform.position = new Vector3(-3.6f, 0.5f, 1.7f);
        m_leftStaticAnchor.transform.rotation = Quaternion.LookRotation(Vector3.up, -m_leftStaticAnchor.transform.position);

        m_rightStaticAnchor = new GameObject("RightSaberStaticAnchor");
        m_rightStaticAnchor.transform.position = new Vector3(-3.0f, 0.5f, 2.8f);
        m_rightStaticAnchor.transform.rotation = Quaternion.LookRotation(Vector3.up, -m_rightStaticAnchor.transform.position);

        m_leftSaber.SetStaticTarget(m_leftStaticAnchor.transform);
        m_rightSaber.SetStaticTarget(m_rightStaticAnchor.transform);

        m_staticDisplayActive = true;
    }

    private void ExitStaticDisplay()
    {
        if (!m_staticDisplayActive) return;

        m_leftSaber.SetStaticTarget(null);
        m_rightSaber.SetStaticTarget(null);

        if (m_leftStaticAnchor != null)
            UnityEngine.Object.Destroy(m_leftStaticAnchor);
        if (m_rightStaticAnchor != null)
            UnityEngine.Object.Destroy(m_rightStaticAnchor);

        m_leftStaticAnchor = null;
        m_rightStaticAnchor = null;
        m_staticDisplayActive = false;
    }

    public void Update(string presetName)
    {
        var selectedColorScheme = m_colorSchemesSettings.GetOverrideColorScheme();
        var (colorLeft, colorRight) = selectedColorScheme is null ? (defaultColorLeft, defaultColorRight)
            : (selectedColorScheme.saberAColor, selectedColorScheme.saberBColor);
        
        SetColor(colorLeft, colorRight);
        // Only push gameplay preset to menu sabers if menu is in Saber mode
        if (m_config.MenuMode == MenuPointerDisplayMode.Saber)
        {
            m_leftSaber.SetPreset(presetName);
            m_rightSaber.SetPreset(presetName);
        }
        else if (m_config.MenuMode == MenuPointerDisplayMode.Pointer)
        {
            // Ensure menu preset is still applied (gameplay change should not affect menu)
            string menuPreset = m_config.MenuSaberPreset;
            m_leftSaber.SetPreset(menuPreset);
            m_rightSaber.SetPreset(menuPreset);
        }
    }

    public void UpdateMenuPreset(string presetName)
    {
        var selectedColorScheme = m_colorSchemesSettings.GetOverrideColorScheme();
        var (colorLeft, colorRight) = selectedColorScheme is null ? (defaultColorLeft, defaultColorRight)
            : (selectedColorScheme.saberAColor, selectedColorScheme.saberBColor);
        
        SetColor(colorLeft, colorRight);
        if (m_config.MenuMode == MenuPointerDisplayMode.Pointer)
        {
            m_leftSaber.SetPreset(presetName);
            m_rightSaber.SetPreset(presetName);
        }
    }

    public void Refresh()
    {
        SetActive(MenuStateHandler.IsConfigOpen, MenuStateHandler.IsEditorOpen);
    }

    public void RefreshVisibility()
    {
        SetActive(MenuStateHandler.IsConfigOpen, MenuStateHandler.IsEditorOpen);
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
