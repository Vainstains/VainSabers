using UnityEngine;

internal class SaberTipTrail : MonoBehaviour
{
    private TrailRenderer m_trailRenderer = null!;
    private Transform m_saber = null!;
    
    private SaberSweepData m_sweepData = null!;
    
    public void Init(Transform saberTransform, SaberSweepData sweepData)
    {
        m_saber = saberTransform;
        m_sweepData = sweepData;
        m_trailRenderer = gameObject.AddComponent<TrailRenderer>();
        m_trailRenderer.material = new Material(Shader.Find("Unlit/vs_flatglow"));
        m_trailRenderer.widthMultiplier = 0.03f;

        // Define width curve: time (0→1) over lifetime, value = relative width (0→1)
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 0.0f);   // start thin
        curve.AddKey(0.2f, 1.0f);   // quickly reach full width
        curve.AddKey(1.0f, 0.0f);   // taper off at the end

        m_trailRenderer.widthCurve = curve;
        //m_trailRenderer.emitting = false;
        m_trailRenderer.time = 0.15f;
    }

    private void LateUpdate()
    {
        transform.position = m_saber.position + m_saber.forward;
    }

    public void SetColor(Color color)
    {
        if (!m_trailRenderer)
            return;
        
        m_trailRenderer.startColor = color;
        color.a = 0;
        m_trailRenderer.endColor = color;
    }
}