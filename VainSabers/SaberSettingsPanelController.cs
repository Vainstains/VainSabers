using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VainSabers.Config;
using VainSabers.UI;

namespace VainSabers;

internal class SaberSettingsPanelController : MonoBehaviour
{
    private SimpleFloatingPanel? m_panel;
    private PluginConfig m_config = null!;

    public void Init(PluginConfig config)
    {
        m_config = config;
    }

    private void Awake()
    {
        MenuStateHandler.ModPanelStateChanged += StateChanged;
    }

    private void OnDestroy()
    {
        MenuStateHandler.ModPanelStateChanged -= StateChanged;
    }

    private void StateChanged(MenuStateHandler.ModPanelState state)
    {
        if (m_panel != null)
        {
            m_panel.Destroy();
            m_panel = null;
        }

        if (!state.SettingsOpen)
            return;

        m_panel = SimpleFloatingPanel.Create(new Vector2(110, 94), new Vector3(0, 1.2f, 2.0f));
        m_panel.Show();
        var settings = m_panel.AddChild<SaberSettingsPanelComponent>().ToFill();
        settings.Build(m_config);
    }
}

internal class SaberSettingsPanelComponent : UIComponent
{
    private PluginConfig m_config = null!;
    private SubPanelComponent m_panel = null!;

    protected override void Init()
    {
        base.Init();
        m_panel = AddChild<SubPanelComponent>().WithLabel("Saber Settings").ToFill();
        m_panel.LayoutElement.flexibleHeight = 1;
    }

    public void Build(PluginConfig config)
    {
        m_config = config;
        Rebuild();
    }

    private void Rebuild()
    {
        var content = m_panel.Content;
        content.ClearChildren();

        content.AddSubHeader("Blur");
        var blurMs = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Blur MS")
            .SetComponent<NumberInputComponent>()
            .WithMinMaxStep(0f, 25f, 1f).WithSensitivityCoef(20)
            .WithValue(Mathf.Min(m_config.BlurMS, 25));
        blurMs.OnValueChanged += v => m_config.BlurMS = Mathf.Clamp(Mathf.RoundToInt(v), 0, 25);

        var softness = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Softness")
            .SetComponent<NumberInputComponent>()
            .WithMinMaxStep(0f, 1f, 0.01f)
            .WithValue(m_config.BlurSoftness);
        softness.OnValueChanged += v => m_config.BlurSoftness = v;

        content.AddSubHeader("Default Trails");
        var bladeMs = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Blade Trail MS")
            .SetComponent<NumberInputComponent>()
            .WithMinMaxStep(0f, 200f, 1f).WithSensitivityCoef(100)
            .WithValue(m_config.BladeTrailMS);
        bladeMs.OnValueChanged += v => m_config.BladeTrailMS = Mathf.RoundToInt(v);

        var tipMs = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Tip Trail MS")
            .SetComponent<NumberInputComponent>()
            .WithMinMaxStep(0f, 200f, 1f).WithSensitivityCoef(100)
            .WithValue(m_config.TipTrailMS);
        tipMs.OnValueChanged += v => m_config.TipTrailMS = Mathf.RoundToInt(v);

        content.AddSubHeader("Quality & Position");
        var quality = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Quality")
            .SetComponent<NumberInputComponent>()
            .WithMinMaxStep(0.01f, 1.5f, 0.01f)
            .WithValue(m_config.SaberQuality);
        quality.OnValueChanged += v => m_config.SaberQuality = v;

        var zRot = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Z Rotation")
            .SetComponent<NumberInputComponent>()
            .WithMinMaxStep(-180f, 180f, 1f).WithSensitivityCoef(90)
            .WithValue(m_config.ZRotationOffset);
        zRot.OnValueChanged += v =>
        {
            m_config.ZRotationOffset = v;
            ApplyZRotationOffset();
        };

        content.AddSubHeader("Smoothing");

        var posSmooth = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Pos Smoothing")
            .SetComponent<ToggleComponent>()
            .WithValue(m_config.PositionSmoothingEnabled);
        posSmooth.OnValueChanged += v =>
        {
            m_config.PositionSmoothingEnabled = v;
            Rebuild();
        };

        if (m_config.PositionSmoothingEnabled)
        {
            var posSmoothness = content.AddChild<FieldComponent>()
                .WithPreferredHeight(4).WithLabel("Pos Smoothness")
                .SetComponent<NumberInputComponent>()
                .WithMinMaxStep(0f, 1f, 0.01f)
                .WithValue(m_config.PositionSmoothingStrength);
            posSmoothness.OnValueChanged += v => m_config.PositionSmoothingStrength = v;
        }

        var rotSmooth = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Rot Smoothing")
            .SetComponent<ToggleComponent>()
            .WithValue(m_config.RotationSmoothingEnabled);
        rotSmooth.OnValueChanged += v =>
        {
            m_config.RotationSmoothingEnabled = v;
            Rebuild();
        };

        if (m_config.RotationSmoothingEnabled)
        {
            var rotSmoothness = content.AddChild<FieldComponent>()
                .WithPreferredHeight(4).WithLabel("Rot Smoothness")
                .SetComponent<NumberInputComponent>()
                .WithMinMaxStep(0f, 1f, 0.01f)
                .WithValue(m_config.RotationSmoothingStrength);
            rotSmoothness.OnValueChanged += v => m_config.RotationSmoothingStrength = v;
        }
        
        content.AddSubHeader("Menu Pointers");

        var pointerMode = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Pointer")
            .SetComponent<DropdownComponent>();
        pointerMode.SetEnumOptions(m_config.PointerMode);
        pointerMode.OnSelectionChanged += _ =>
        {
            m_config.PointerMode = pointerMode.SelectedEnumValue<PointerMode>();
            Rebuild();
        };

        var laserMode = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Laser")
            .SetComponent<DropdownComponent>();
        laserMode.SetEnumOptions(m_config.LaserMode);
        laserMode.OnSelectionChanged += _ =>
        {
            m_config.LaserMode = laserMode.SelectedEnumValue<LaserMode>();
            Rebuild();
        };

        if (m_config.LaserMode == LaserMode.VainSabers)
        {
            var laserBlur = content.AddChild<FieldComponent>()
                .WithPreferredHeight(4).WithLabel("Laser Blur")
                .SetComponent<NumberInputComponent>()
                .WithMinMaxStep(0f, 1f, 0.01f)
                .WithValue(m_config.MenuPointerLaserBlurFactor);
            laserBlur.OnValueChanged += v => m_config.MenuPointerLaserBlurFactor = Mathf.Clamp01(v);
        }

        if (m_config.PointerMode == PointerMode.VainSabers)
        {
            var dotPresetNames = GetDotPresetNames();
            if (!dotPresetNames.Contains(m_config.MenuPointerDotPreset))
                dotPresetNames = dotPresetNames.Concat(new[] { m_config.MenuPointerDotPreset }).Distinct().OrderBy(x => x).ToList();
            int dotPresetIdx = dotPresetNames.IndexOf(m_config.MenuPointerDotPreset);
            if (dotPresetIdx < 0) dotPresetIdx = dotPresetNames.IndexOf("menupointer-dot");
            if (dotPresetIdx < 0) dotPresetIdx = 0;
            var dotPresetRow = content.AddChild<FieldComponent>()
                .WithPreferredHeight(4).WithLabel("Dot Preset")
                .SetComponent<DropdownWithEditComponent>();
            dotPresetRow.SetOptions(dotPresetNames, dotPresetIdx);
            dotPresetRow.OnSelectionChanged += idx =>
            {
                if (idx >= 0 && idx < dotPresetNames.Count)
                    m_config.MenuPointerDotPreset = dotPresetNames[idx];
            };
            dotPresetRow.OnEditClicked += () =>
            {
                string preset = dotPresetRow.SelectedValue ?? m_config.MenuPointerDotPreset;
                if (string.IsNullOrEmpty(preset)) return;
                // Check read-only (.vainsaber)
                var profile = ConfigUtil.GetSaberProfile(preset);
                if (profile.EndsWith(".vainsaber", StringComparison.OrdinalIgnoreCase)) return;
                MenuStateHandler.SetEditingPreset(preset);
                MenuStateHandler.SetEditorOpen(true);
            };
        }

        var closeRow = content.AddChild<UIComponent>().WithPreferredHeight(4);
        var closeButton = closeRow.AddChild<TextButtonComponent>().ToFill().WithText("Close");
        closeButton.OnClick += () => MenuStateHandler.SetSettingsOpen(false);
        closeButton.Color = new Color(0.55f, 0.35f, 0.2f, 1.0f);
    }

    private void ApplyZRotationOffset()
    {
        var (left, right) = MenuStateHandler.Sabers;
        left?.ApplyZRotationOffset();
        right?.ApplyZRotationOffset();
    }

    private static List<string> GetDotPresetNames()
    {
        try
        {
            var dir = ConfigUtil.ConfigDir;
            if (!Directory.Exists(dir)) return new List<string> { "menupointer-dot" };
            var json = Directory.GetFiles(dir, "*.json").Select(Path.GetFileNameWithoutExtension);
            var vainsaber = Directory.GetFiles(dir, "*.vainsaber").Select(Path.GetFileNameWithoutExtension);
            var names = json.Concat(vainsaber).Distinct().OrderBy(x => x).ToList();
            if (names.Count == 0) names.Add("menupointer-dot");
            if (!names.Contains("menupointer-dot")) names.Insert(0, "menupointer-dot");
            return names;
        }
        catch
        {
            return new List<string> { "menupointer-dot" };
        }
    }
}
