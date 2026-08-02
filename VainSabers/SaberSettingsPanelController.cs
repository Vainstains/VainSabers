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

        m_panel = SimpleFloatingPanel.Create(new Vector2(110, 55), new Vector3(0, 1.2f, 2.0f));
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
        var content = m_panel.Content;

        var blurMs = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Blur MS")
            .SetComponent<NumberInputComponent>()
            .WithMinMaxStep(0f, 50f, 1f).WithSensitivityCoef(20)
            .WithValue(config.BlurMS);
        blurMs.OnValueChanged += v => m_config.BlurMS = Mathf.RoundToInt(v);

        var softness = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Softness")
            .SetComponent<NumberInputComponent>()
            .WithMinMaxStep(0f, 1f, 0.01f)
            .WithValue(config.BlurSoftness);
        softness.OnValueChanged += v => m_config.BlurSoftness = v;

        var bladeMs = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Blade Trail MS")
            .SetComponent<NumberInputComponent>()
            .WithMinMaxStep(0f, 200f, 1f).WithSensitivityCoef(100)
            .WithValue(config.BladeTrailMS);
        bladeMs.OnValueChanged += v => m_config.BladeTrailMS = Mathf.RoundToInt(v);

        var tipMs = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Tip Trail MS")
            .SetComponent<NumberInputComponent>()
            .WithMinMaxStep(0f, 200f, 1f).WithSensitivityCoef(100)
            .WithValue(config.TipTrailMS);
        tipMs.OnValueChanged += v => m_config.TipTrailMS = Mathf.RoundToInt(v);

        var quality = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Quality")
            .SetComponent<NumberInputComponent>()
            .WithMinMaxStep(0.01f, 1.5f, 0.01f)
            .WithValue(config.SaberQuality);
        quality.OnValueChanged += v => m_config.SaberQuality = v;

        var zRot = content.AddChild<FieldComponent>()
            .WithPreferredHeight(4).WithLabel("Z Rotation")
            .SetComponent<NumberInputComponent>()
            .WithMinMaxStep(-180f, 180f, 1f).WithSensitivityCoef(90)
            .WithValue(config.ZRotationOffset);
        zRot.OnValueChanged += v =>
        {
            m_config.ZRotationOffset = v;
            ApplyZRotationOffset();
        };

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
}
