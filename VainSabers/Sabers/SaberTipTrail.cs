using System.Collections.Generic;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Data;

namespace VainSabers.Sabers;

internal class SaberTipTrail : MonoBehaviour
{
    private LineRenderer _lineRenderer = null!;
    private Transform _saber = null!;
    private MovementHistoryProvider _sweepData = null!;
    
    private int _coarseSampleCount = 24;
    private int _refinedSampleCount = 47;
    private int _refinedSampleCount2 = 93;
    
    private Pose[] _poseBuffer = new Pose[24];
    private Vector3[] _coarsePositions = new Vector3[24];
    private Vector3[] _refinedPositions = new Vector3[47];
    private Vector3[] _refinedPositions2 = new Vector3[93];

    private void UpdateSampleCounts(int lengthMs)
    {
        int coarse = Mathf.Clamp(lengthMs / 4, 4, 256);
        if (coarse == _coarseSampleCount) return;
        _coarseSampleCount = coarse;
        _refinedSampleCount = _coarseSampleCount * 2 - 1;
        _refinedSampleCount2 = _refinedSampleCount * 2 - 1;
        _poseBuffer = new Pose[_coarseSampleCount];
        _coarsePositions = new Vector3[_coarseSampleCount];
        _refinedPositions = new Vector3[_refinedSampleCount];
        _refinedPositions2 = new Vector3[_refinedSampleCount2];
        if (_lineRenderer != null)
            _lineRenderer.positionCount = _refinedSampleCount2;
    }

    private float m_opacity = 0.0f;
    private Color m_trailColor = Color.white;
    private Color m_baseColor = Color.white;
    private Color m_gameColor = Color.white;
    private SaberTrailData m_trailData;

    public void Init(MovementHistoryProvider sweepData, SaberTrailData trailData, Transform saberTransform)
    {
        _sweepData = sweepData;
        _saber = saberTransform;
        m_trailData = trailData;

        UpdateSampleCounts(trailData.Length);
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.material = new Material(VainSabersAssets.VertexGlowShaderUntextured);
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = _refinedSampleCount2;

        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 0.0f);
        curve.AddKey(0.3f, 1.0f);
        curve.AddKey(1.0f, 0.0f);
        _lineRenderer.widthCurve = curve;

        ApplyConfig(trailData);
    }

    public void ApplyConfig(SaberTrailData trailData)
    {
        m_trailData = trailData;
        UpdateSampleCounts(trailData.Length);
        if (_lineRenderer == null) return;
        _lineRenderer.positionCount = _refinedSampleCount2;
        _lineRenderer.widthMultiplier = trailData.Width;
        _lineRenderer.sortingOrder = 100;
        _lineRenderer.material.renderQueue = 3600 + trailData.QueueOffset;
        _lineRenderer.material.SetFloat("_GlowBoost", trailData.Glow);
        _lineRenderer.material.SetFloat("_DepthOffset", trailData.DepthOffset);
        // Gradient base color – keep legacy Color for fallback but gradient is primary
        m_baseColor = new Color(trailData.Color[0], trailData.Color[1], trailData.Color[2], 1f);
        UpdateFinalColor();
    }

    public void SetGameColor(Color color)
    {
        m_gameColor = color;
        UpdateFinalColor();
    }

    private void UpdateFinalColor()
    {
        Color tonemappedGame = SquarePreserveLuminance(m_gameColor * 0.8f);
        tonemappedGame.a = m_gameColor.a;
        if (_lineRenderer != null && _lineRenderer.material != null)
        {
            _lineRenderer.material.SetColor("_CustomColor", tonemappedGame);
        }
        // Keep m_trailColor as fallback solid for when gradient missing
        m_trailColor = m_baseColor;
        // Store tonemapped for per-vertex evaluation
        m_tonemappedGame = tonemappedGame;
    }

    private Color m_tonemappedGame = Color.white;

    private float EvaluateCustomBlend(float t)
    {
        var keys = m_trailData.CustomBlendGradientKeys;
        if (keys == null || keys.Count == 0)
            return Mathf.Clamp01(m_trailData.CustomBlend);
        if (keys.Count == 1)
            return Mathf.Clamp01(keys[0].Value);
        keys.Sort((a, b) => a.Time.CompareTo(b.Time));
        t = Mathf.Clamp01(t);
        if (t <= keys[0].Time) return Mathf.Clamp01(keys[0].Value);
        if (t >= keys[keys.Count - 1].Time) return Mathf.Clamp01(keys[keys.Count - 1].Value);
        for (int i = 1; i < keys.Count; i++)
        {
            var prev = keys[i - 1];
            var next = keys[i];
            if (t < next.Time)
            {
                float t0 = t - prev.Time;
                float interval = next.Time - prev.Time;
                float t1 = interval > 0.0001f ? t0 / interval : 0f;
                return Mathf.Clamp01(Mathf.Lerp(prev.Value, next.Value, prev.Easing.Evaluate(t1)));
            }
        }
        return Mathf.Clamp01(keys[keys.Count - 1].Value);
    }

    private Color EvaluateTrailGradient(float t)
    {
        var keys = m_trailData.ColorGradientKeys;
        if (keys == null || keys.Count == 0)
            return m_baseColor;
        if (keys.Count == 1)
            return keys[0].Color;
        keys.Sort((a, b) => a.Time.CompareTo(b.Time));
        t = Mathf.Clamp01(t);
        if (t <= keys[0].Time) return keys[0].Color;
        if (t >= keys[keys.Count - 1].Time) return keys[keys.Count - 1].Color;
        for (int i = 1; i < keys.Count; i++)
        {
            var prev = keys[i - 1];
            var next = keys[i];
            if (t < next.Time)
            {
                float t0 = t - prev.Time;
                float interval = next.Time - prev.Time;
                float t1 = interval > 0.0001f ? t0 / interval : 0f;
                return Color.Lerp(prev.Color, next.Color, prev.Easing.Evaluate(t1));
            }
        }
        return keys[keys.Count - 1].Color;
    }

    private static Color SquarePreserveLuminance(Color c)
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

    private void LateUpdate()
    {
        if (_sweepData == null || !_lineRenderer)
            return;

        float tipSpeed = EstimateTipSpeed();
        Vector3 localOffset = new Vector3(m_trailData.Position[0], m_trailData.Position[1], m_trailData.Position[2]);

        _sweepData.SampleNonAlloc(_coarseSampleCount, m_trailData.Length * 0.001f, _poseBuffer);
        for (var i = 0; i < _coarseSampleCount; i++)
            _coarsePositions[i] = _poseBuffer[i].position + _poseBuffer[i].rotation * localOffset;

        _lineRenderer.enabled = m_trailData.Length > 0;

        RefinePositions(_coarsePositions, _refinedPositions);
        RefinePositions(_refinedPositions, _refinedPositions2);

        _lineRenderer.SetPositions(_refinedPositions2);

        float activation = Mathf.Clamp01(m_trailData.MotionActivation);
        if (activation <= 0.001f)
        {
            m_opacity = 1f;
        }
        else
        {
            tipSpeed *= 0.8f;
            float expFactor = Mathf.Exp((1f - activation) * 2.2f);
            float threshold = 0.8f * activation;
            float gated = Mathf.Clamp01((tipSpeed - threshold) * expFactor);
            float decay = 3.0f * expFactor;
            m_opacity = Mathf.Max(gated, Mathf.MoveTowards(m_opacity, 0.0f, Time.deltaTime * decay));
        }

        UpdateGradient(m_opacity * m_trailData.Opacity);
    }

    private float EstimateTipSpeed()
    {
        Pose now = _sweepData.GetPoseAgo(0.0f);
        Pose prev = _sweepData.GetPoseAgo(0.02f);
        return (now.position - prev.position).magnitude / 0.02f;
    }

    private void RefinePositions(Vector3[] coarse, Vector3[] refined)
    {
        int newLength = refined.Length;

        for (int i = 0; i < coarse.Length - 1; i++)
        {
            refined[2 * i] = coarse[i];
            refined[2 * i + 1] = (coarse[i] + coarse[i + 1]) * 0.5f;
        }
        refined[newLength - 1] = coarse[coarse.Length - 1];

        for (int i = 1; i < coarse.Length - 1; i++)
        {
            int index = 2 * i;
            Vector3 midpointAverage = (refined[index - 1] + refined[index + 1]) * 0.5f;
            refined[index] = (refined[index] + midpointAverage) * 0.5f;
        }
    }
    
    private readonly Gradient _cachedGradient = new Gradient();
    private readonly GradientAlphaKey[] _alphaKeys = new GradientAlphaKey[2];

    private void UpdateGradient(float opacity)
    {
        // Build color keys from trail gradient (rgb over length) – sample 8 points to preserve easing, and blend with custom color per t
        var trailKeys = m_trailData.ColorGradientKeys;
        GradientColorKey[] colorKeys;
        if (trailKeys != null && trailKeys.Count > 0)
        {
            const int sampleCount = 8;
            colorKeys = new GradientColorKey[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)(sampleCount - 1);
                Color baseCol = EvaluateTrailGradient(t);
                float blend = EvaluateCustomBlend(t);
                Color blended = Color.Lerp(baseCol, m_tonemappedGame, blend);
                colorKeys[i] = new GradientColorKey(blended, t);
            }
        }
        else
        {
            colorKeys = new GradientColorKey[2];
            colorKeys[0] = new GradientColorKey(m_trailColor, 0f);
            colorKeys[1] = new GradientColorKey(m_trailColor, 1f);
        }
        _alphaKeys[0] = new GradientAlphaKey(0.9f * opacity, 0f);
        _alphaKeys[1] = new GradientAlphaKey(0.9f * opacity * (1f - m_trailData.Fade), 1f);
        _cachedGradient.SetKeys(colorKeys, _alphaKeys);
        _lineRenderer.colorGradient = _cachedGradient;
    }
}
