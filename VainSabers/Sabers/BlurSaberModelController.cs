using System;
using SiraUtil.Interfaces;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Helpers;
using Zenject;

namespace VainSabers.Sabers;

internal class BlurSaberModelController : SaberModelController, IPreSaberModelInit, IColorable
{
    [Inject] private readonly ColorManager m_colorManager = null!;
    [Inject] private readonly PluginConfig m_config = null!;
    
    // private Transform m_saberTransform = null!;

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
        transform.SetParent(parent, false);
        transform.position = parent.position;
        transform.rotation = parent.rotation;
        
        SetupSaber(m_config.CurrentSaber);
        
        Color = m_colorManager.ColorForSaberType(saber.saberType);
        return false;
    }
    
    private BlurSaber? m_blurSaber;
    private void SetupSaber(string preset)
    {
        m_blurSaber = gameObject.AddInitComponent<BlurSaber>(transform, m_config);
        m_blurSaber.SetPreset(preset);
        
        Shader.SetGlobalFloat("_VainSaberBlurSoftness", m_config.BlurSoftness);
    }
    private void SetColor(Color color)
    {
        m_blurSaber?.SetColor(color);
    }
}