using UnityEngine;

namespace VainSabers.Legacy;

internal class SmoothFollower : MonoBehaviour
{
    private Transform? m_target;

    private Vector3 m_dampedPosA;
    private Vector3 m_dampedUpA;
    private Vector3 m_dampedFwdA;

    private Vector3 m_dampedPosB;
    private Vector3 m_dampedUpB;
    private Vector3 m_dampedFwdB;
    
    private float m_tightness;

    public void Init(Transform target, float tightness)
    {
        m_target = target;
        m_tightness = tightness;

        // Initialize both stages at the target so there’s no pop on start
        m_dampedPosA = target.position;
        m_dampedUpA  = target.up;
        m_dampedFwdA = target.forward;

        m_dampedPosB = m_dampedPosA;
        m_dampedUpB  = m_dampedUpA;
        m_dampedFwdB = m_dampedFwdA;
    }

    private void Damp(ref Vector3 damped, Vector3 target)
    {
        // exponential decay form → independent of framerate
        float t = 1.0f - Mathf.Exp(-m_tightness * Time.deltaTime);
        damped = Vector3.Lerp(damped, target, t);
    }

    private void Update()
    {
        if (m_target == null)
            return;

        Vector3 targetPos = m_target.position;
        Vector3 targetUp  = m_target.up;
        Vector3 targetFwd = m_target.forward;

        // First stage: damp towards the actual target
        Damp(ref m_dampedPosA, targetPos);
        Damp(ref m_dampedUpA,  targetUp);
        Damp(ref m_dampedFwdA, targetFwd);

        // Lead target = 2A - B (previous B values are stored in m_dampedXxB)
        Vector3 leadPos = 3f * m_dampedPosA - 2f * m_dampedPosB;
        Vector3 leadUp  = 3f * m_dampedUpA  - 2f * m_dampedUpB;
        Vector3 leadFwd = 3f * m_dampedFwdA - 2f * m_dampedFwdB;

        // Second stage: damp towards the lead target
        Damp(ref m_dampedPosB, leadPos);
        Damp(ref m_dampedUpB,  leadUp);
        Damp(ref m_dampedFwdB, leadFwd);

        // Finally: apply result to this transform
        transform.SetPositionAndRotation(m_dampedPosB, Quaternion.LookRotation(m_dampedFwdB, m_dampedUpB));
    }
}