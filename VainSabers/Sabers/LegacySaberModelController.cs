using SiraUtil.Interfaces;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Helpers;
using VainSabers.Legacy;
using Zenject;

namespace VainSabers.Sabers;

internal class LegacySaberModelController : SaberModelController, IPreSaberModelInit, IColorable
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
        
        var profile = Config.ConfigUtil.GetLegacySaberProfile(m_config.CurrentLegacySaber);
        SetupSaber(profile);
        
        Color = m_colorManager.ColorForSaberType(saber.saberType);
        return false;
    }
    
    private SaberSweepGenerator m_sweepGenerator = null!;
    private LegacySaberTipTrail m_tipTrail = null!;
    private SaberSweepData  m_sweepData = new SaberSweepData(100);
    private void SetupSaber(LegacySaberProfile profile)
    {
        var follower = gameObject.AddInitChild<FrameAverager>(m_saberTransform, 2).transform;
        
        m_sweepGenerator = gameObject.AddInitComponent<SaberSweepGenerator>(follower, m_sweepData, 10, 1.0f / 55.0f, profile);
        m_tipTrail = gameObject.AddInitChild<LegacySaberTipTrail>(follower, m_sweepData);
    }
    private void SetColor(Color color)
    {
        m_sweepGenerator.SetColor(color);
        m_tipTrail.SetColor(color);
    }
}