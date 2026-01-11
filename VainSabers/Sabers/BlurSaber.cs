using UnityEngine;
using VainSabers.Config;
using VainSabers.Helpers;

namespace VainSabers.Sabers;

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
        
        var historyProvider = gameObject.AddInitComponent<MovementTracker>(target);
        m_blurSaberData = gameObject.AddInitComponent<BlurSaberData>(m_config);
        
        m_tipTrail = gameObject.AddInitComponent<SaberTipTrail>(m_config, target, historyProvider);
        m_ribbonTrail = gameObject.AddInitComponent<SaberRibbonTrail>(m_config, target, historyProvider);
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