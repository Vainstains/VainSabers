using System;
using SiraUtil.Interfaces;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Helpers;
using VainSabers.Legacy;
using Zenject;

namespace VainSabers.Sabers;

internal class BlurSaberModelController : SaberModelController, IPreSaberModelInit, IColorable
{
    [Inject] private readonly ColorManager m_colorManager = null!;
    [Inject] private readonly PluginConfig m_config = null!;
    
    private Transform m_saberTransform = null!;

    private Color m_color;
    public Color Color
    {
        get => m_color;
        set
        {
            m_color = value;
            SetColor(m_color);
        }
    }


    public bool PreInit(Transform parent, Saber saber)
    {
        m_saberTransform = saber.transform;
        transform.SetParent(null);
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        
        SetupSaber(m_config.CurrentSaber);
        
        Color = m_colorManager.ColorForSaberType(saber.saberType);
        return false;
    }
    
    private BlurSaber? m_blurSaber;
    private void SetupSaber(string preset)
    {
        m_blurSaber = gameObject.AddInitComponent<BlurSaber>(m_saberTransform, m_config);
        m_blurSaber.SetPreset(preset);
    }
    private void SetColor(Color color)
    {
        m_blurSaber?.SetColor(color);
    }
}

internal class BlurSaber : MonoBehaviour
{
    private PluginConfig m_config = null!;
    private BlurSaberData m_blurSaberData = null!;
    private SaberTipTrail m_tipTrail = null!;
    private SaberRibbonTrail m_ribbonTrail = null!;
    
    public BlurSaberData Data => m_blurSaberData!;

	private string m_currentPreset = "";
    
    public void Init(Transform target, PluginConfig config)
    {
        m_config = config;
        var historyProvider = gameObject.AddComponent<MovementTracker>();
        historyProvider.Target = target;
        m_blurSaberData = gameObject.AddInitComponent<BlurSaberData>(m_config);
        
        m_tipTrail = gameObject.AddInitComponent<SaberTipTrail>(target, historyProvider);
        m_ribbonTrail = gameObject.AddInitComponent<SaberRibbonTrail>(target, historyProvider);
    }

    public void SetPreset(string preset)
    {
		if (preset != m_currentPreset)
        	m_blurSaberData?.ImportFromFile(Config.ConfigUtil.GetSaberProfile(preset));
		m_currentPreset = preset;
    }

    public void SetColor(Color color)
    {
        if (m_blurSaberData != null)
            m_blurSaberData.CustomColor = SquarePreserveLuminance(color * 0.8f);
        if (m_tipTrail != null)
            m_tipTrail.SetColor(color);
        if (m_ribbonTrail != null)
            m_ribbonTrail.SetColor(color);
    }
    
    Color SquarePreserveLuminance(Color c)
    {
        // 1. Original luminance (Rec. 601 weights for perception)
        float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        // 2. Square channels
        float r2 = c.r * c.r;
        float g2 = c.g * c.g;
        float b2 = c.b * c.b;

        // 3. New luminance
        float lum2 = 0.299f * r2 + 0.587f * g2 + 0.114f * b2;

        // 4. Scale to preserve original luminance
        float scale = (lum2 > 0.00001f) ? (lum / lum2) : 0f;

        return new Color(
            Mathf.Clamp01(r2 * scale),
            Mathf.Clamp01(g2 * scale),
            Mathf.Clamp01(b2 * scale),
            c.a
        );
    }

}