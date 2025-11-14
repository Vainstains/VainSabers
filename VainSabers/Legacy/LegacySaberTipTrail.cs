using UnityEngine;

namespace VainSabers.Legacy;

internal class LegacySaberTipTrail : MonoBehaviour
{
    private LineRenderer _lineRenderer = null!;
    private Transform _saber = null!;
    private SaberSweepData _sweepData = null!;

    private const float TrailTime = 0.14f; // seconds of history
    private const int CoarseSampleCount = 20; // reduced coarse samples
    private const int RefinedSampleCount = CoarseSampleCount * 2 - 1; // after refinement: 15*2-1 = 29

    // Pre-allocated arrays to avoid GC allocations
    private Vector3[] _coarsePositions = new Vector3[CoarseSampleCount];
    private Vector3[] _refinedPositions = new Vector3[RefinedSampleCount];

    public void Init(Transform saberTransform, SaberSweepData sweepData)
    {
        _saber = saberTransform;
        _sweepData = sweepData;

        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.material = new Material(VainSabersAssets.VertexGlowShader);
        _lineRenderer.widthMultiplier = 0.018f;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = RefinedSampleCount;

        // Width curve over trail lifetime
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 0.0f);
        curve.AddKey(0.1f, 1.0f);
        curve.AddKey(1.0f, 0.0f);
        _lineRenderer.widthCurve = curve;
    }

    private float m_opacity = 0.0f;
    private Color m_color = Color.clear;

    private void LateUpdate()
    {
        if (_sweepData == null || !_lineRenderer)
            return;

        float tipSpeed = 0;

        // Step 1: Sample coarse positions
        for (int i = 0; i < CoarseSampleCount; i++)
        {
            float t = (i / (float)(CoarseSampleCount - 1)) * TrailTime;
            _sweepData.GetDataPointAtTimeAgo(t, out var pos, out var dir, out var tipVel);
            _coarsePositions[i] = pos + dir;
            
            if (i == CoarseSampleCount - 1) // Only need latest velocity
                tipSpeed = tipVel.magnitude;
        }

        // Step 2: Apply refinement to smooth the trail
        RefinePositions(_coarsePositions, _refinedPositions);

        // Step 3: Set refined positions to line renderer
        _lineRenderer.SetPositions(_refinedPositions);

        tipSpeed *= 0.2f;

        m_opacity = Mathf.Max(
            Mathf.Clamp01(tipSpeed - 0.8f),
            Mathf.MoveTowards(m_opacity, 0.0f, Time.deltaTime * 3.0f));
        
        // Reuse gradient to avoid allocation
        UpdateGradient(m_opacity);
    }

    private void RefinePositions(Vector3[] coarse, Vector3[] refined)
    {
        int newLength = refined.Length;

        // Copy original vertices to even indices and compute midpoints
        for (int i = 0; i < coarse.Length - 1; i++)
        {
            refined[2 * i] = coarse[i];
            refined[2 * i + 1] = (coarse[i] + coarse[i + 1]) * 0.5f;
        }
        refined[newLength - 1] = coarse[coarse.Length - 1];

        // Smooth the original vertices using adjacent midpoints
        for (int i = 1; i < coarse.Length - 1; i++)
        {
            int index = 2 * i;
            Vector3 midpointAverage = (refined[index - 1] + refined[index + 1]) * 0.5f;
            refined[index] = (refined[index] + midpointAverage) * 0.5f;
        }
    }

    private void UpdateGradient(float opacity)
    {
        // Only update gradient if opacity changed significantly
        if (Mathf.Abs(_lineRenderer.colorGradient.alphaKeys[0].alpha - (0.5f * opacity)) > 0.01f)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(m_color, 0.0f), new GradientColorKey(m_color, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.5f * opacity, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            _lineRenderer.colorGradient = gradient;
        }
    }

    public void SetColor(Color color)
    {
        m_color = color;
    }
}

