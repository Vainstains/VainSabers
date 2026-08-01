using System;
using System.Collections.Generic;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Helpers;

namespace VainSabers.Sabers;

internal class BlurSaber : MonoBehaviour
{
    private PluginConfig m_config = null!;
    private BlurSaberData m_blurSaberData = null!;
    
    private SaberTipTrail m_defaultTipTrail = null!;
    private SaberRibbonTrail m_defaultRibbonTrail = null!;
    
    private readonly List<SaberTipTrail> m_customTipTrails = new();
    private SaberRibbonTrail? m_customBladeTrail;

    private Transform m_saberTransform = null!;
    private MovementHistoryProvider m_historyProvider = null!;
    private MovementTracker m_tracker = null!;
    private Transform m_parkedTarget = null!;
    private Color m_gameColor = Color.white;
    private bool m_inPreviewMode;
    private Transform m_savedTrackerTarget = null!;
    
    public BlurSaberData Data => m_blurSaberData!;

    private string m_currentPreset = "";
    
    public void Init(Transform target, PluginConfig config)
    {
        m_config = config;
        m_saberTransform = target;
        
        m_tracker = gameObject.AddInitComponent<MovementTracker>(target);
        m_historyProvider = m_tracker;
        m_blurSaberData = gameObject.AddInitComponent<BlurSaberData>(m_config);
        m_blurSaberData.IsLeftSaber = target.name.Contains("Left", StringComparison.OrdinalIgnoreCase) || target.parent?.name.Contains("Left", StringComparison.OrdinalIgnoreCase) == true;
        m_blurSaberData.TrailsChanged += OnTrailsChanged;

        var parkedGo = new GameObject("ParkedTarget");
        parkedGo.transform.SetParent(transform, false);
        parkedGo.transform.localPosition = new Vector3(0f, -100f, 0f);
        m_parkedTarget = parkedGo.transform;

        CreateDefaultTrails();
    }

    public void SetPreset(string preset)
    {
        m_blurSaberData?.ImportFromFile(Config.ConfigUtil.GetSaberProfile(preset));
        m_currentPreset = preset;
        RebuildTrails();
    }

    public void SetColor(Color color)
    {
        m_gameColor = color;
        if (m_blurSaberData != null)
            m_blurSaberData.CustomColor = SquarePreserveLuminance(color * 0.8f);
        
        if (m_blurSaberData == null || !m_blurSaberData.UseCustomTrails)
        {
            if (m_defaultTipTrail != null)
                m_defaultTipTrail.SetGameColor(color);
            if (m_defaultRibbonTrail != null)
                m_defaultRibbonTrail.SetGameColor(color);
        }
        else
        {
            foreach (var trail in m_customTipTrails)
                trail?.SetGameColor(color);
            m_customBladeTrail?.SetGameColor(color);
        }
    }
    
    private void OnTrailsChanged()
    {
        RebuildTrails();
    }

    private void RebuildTrails()
    {
        if (m_blurSaberData == null)
            return;

        if (!m_blurSaberData.UseCustomTrails)
        {
            DestroyCustomTrails();
            EnsureDefaultTrails();
            m_defaultTipTrail.SetGameColor(m_gameColor);
            m_defaultRibbonTrail.SetGameColor(m_gameColor);
        }
        else
        {
            DestroyDefaultTrails();
            m_blurSaberData.EnsureDefaultTrails();
            CreateCustomTrails();
        }
    }

    private void CreateDefaultTrails()
    {
        var tipData = new SaberTrailData(
            position: new float[] { 0f, 0f, 1f },
            color: new float[] { 1f, 1f, 1f },
            customBlend: 1f,
            glow: 1f,
            opacity: 1f,
            width: 0.008f,
            length: m_config.TipTrailMS,
            queueOffset: 0
        );
        var tipGo = new GameObject("DefaultTipTrail");
        tipGo.transform.SetParent(transform, false);
        m_defaultTipTrail = tipGo.AddComponent<SaberTipTrail>();
        m_defaultTipTrail.Init(m_historyProvider, tipData, m_saberTransform);

        var bladeData = new SaberTrailData(
            position: new float[] { 0f, 0f, 1f },
            color: new float[] { 1f, 1f, 1f },
            customBlend: 1f,
            glow: 1f,
            opacity: 0.3f,
            width: 0.01f,
            length: m_config.BladeTrailMS,
            queueOffset: 0
        );
        var bladeGo = new GameObject("DefaultRibbonTrail");
        bladeGo.transform.SetParent(transform, false);
        m_defaultRibbonTrail = bladeGo.AddComponent<SaberRibbonTrail>();
        m_defaultRibbonTrail.Init(m_historyProvider, bladeData, m_saberTransform);
    }

    private void EnsureDefaultTrails()
    {
        if (m_defaultTipTrail == null || m_defaultRibbonTrail == null)
        {
            DestroyDefaultTrails();
            CreateDefaultTrails();
        }
        
        m_defaultTipTrail!.ApplyConfig(new SaberTrailData(
            position: new float[] { 0f, 0f, 1f },
            color: new float[] { 1f, 1f, 1f },
            customBlend: 1f,
            glow: 1f,
            opacity: 1f,
            width: 0.008f,
            length: m_config.TipTrailMS,
            queueOffset: 0
        ));
        m_defaultRibbonTrail!.ApplyConfig(new SaberTrailData(
            position: new float[] { 0f, 0f, 1f },
            color: new float[] { 1f, 1f, 1f },
            customBlend: 1f,
            glow: 1f,
            opacity: 0.3f,
            width: 0.01f,
            length: m_config.BladeTrailMS,
            queueOffset: 0
        ));
    }

    private void DestroyDefaultTrails()
    {
        if (m_defaultTipTrail != null)
        {
#if UNITY_EDITOR
            if (Application.isEditor)
                DestroyImmediate(m_defaultTipTrail.gameObject);
            else
                Destroy(m_defaultTipTrail.gameObject);
#else
            Destroy(m_defaultTipTrail.gameObject);
#endif
            m_defaultTipTrail = null!;
        }
        if (m_defaultRibbonTrail != null)
        {
#if UNITY_EDITOR
            if (Application.isEditor)
                DestroyImmediate(m_defaultRibbonTrail.gameObject);
            else
                Destroy(m_defaultRibbonTrail.gameObject);
#else
            Destroy(m_defaultRibbonTrail.gameObject);
#endif
            m_defaultRibbonTrail = null!;
        }
    }

    private void CreateCustomTrails()
    {
        DestroyCustomTrails();

        for (int i = 0; i < m_blurSaberData.TipTrails.Count; i++)
        {
            var data = m_blurSaberData.TipTrails[i];
            var go = new GameObject($"TipTrail_{i}");
            go.transform.SetParent(transform, false);
            var trail = go.AddComponent<SaberTipTrail>();
            trail.Init(m_historyProvider, data, m_saberTransform);
            trail.SetGameColor(m_gameColor);
            m_customTipTrails.Add(trail);
        }

        if (m_blurSaberData.BladeTrail.HasValue)
        {
            var data = m_blurSaberData.BladeTrail.Value;
            var go = new GameObject("CustomBladeTrail");
            go.transform.SetParent(transform, false);
            m_customBladeTrail = go.AddComponent<SaberRibbonTrail>();
            m_customBladeTrail.Init(m_historyProvider, data, m_saberTransform);
            m_customBladeTrail.SetGameColor(m_gameColor);
        }
    }

    private void DestroyCustomTrails()
    {
        foreach (var trail in m_customTipTrails)
        {
            if (trail != null)
            {
#if UNITY_EDITOR
                if (Application.isEditor)
                    DestroyImmediate(trail.gameObject);
                else
                    Destroy(trail.gameObject);
#else
                Destroy(trail.gameObject);
#endif
            }
        }
        m_customTipTrails.Clear();

        if (m_customBladeTrail != null)
        {
#if UNITY_EDITOR
            if (Application.isEditor)
                DestroyImmediate(m_customBladeTrail.gameObject);
            else
                Destroy(m_customBladeTrail.gameObject);
#else
            Destroy(m_customBladeTrail.gameObject);
#endif
            m_customBladeTrail = null;
        }
    }
    
    Color SquarePreserveLuminance(Color c)
    {
        float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        float r2 = c.r * c.r;
        float g2 = c.g * c.g;
        float b2 = c.b * c.b;
        float lum2 = 0.299f * r2 + 0.587f * g2 + 0.114f * b2;
        float scale = (lum2 > 0.00001f) ? (lum / lum2) : 0f;

        return new Color(
            Mathf.Clamp01(r2 * scale),
            Mathf.Clamp01(g2 * scale),
            Mathf.Clamp01(b2 * scale),
            c.a
        );
    }

    public void SetPreviewTransform(Transform? previewTransform)
    {
        if (m_tracker == null) return;

        if (previewTransform != null)
        {
            if (!m_inPreviewMode)
            {
                m_savedTrackerTarget = m_tracker.Target;
                m_inPreviewMode = true;
            }
            m_tracker.Target = previewTransform;
            m_tracker.ClearHistory();
        }
        else if (m_inPreviewMode)
        {
            m_tracker.Target = m_savedTrackerTarget;
            m_tracker.ClearHistory();
            m_savedTrackerTarget = null!;
            m_inPreviewMode = false;
        }
    }

    private void FixedUpdate()
    {
        Shader.SetGlobalFloat("_VainSaberBlurSoftness", m_config.BlurSoftness);

        if (m_inPreviewMode)
            return;
        
        // prolly a better way to do this
        bool isFpfc = Helpers.Helpers.GetIsFpfc();
        if (isFpfc && m_tracker.Target != m_parkedTarget)
        {
            m_tracker.Target = m_parkedTarget;
            m_tracker.ClearHistory();
        }
        else if (!isFpfc && m_tracker.Target == m_parkedTarget)
        {
            m_tracker.Target = m_saberTransform;
            m_tracker.ClearHistory();
        }
    }
}
