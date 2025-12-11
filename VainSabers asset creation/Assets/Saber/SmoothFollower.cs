using UnityEngine;

internal class SmoothFollower : MonoBehaviour
{
    private Transform? m_target;
    private Vector3 m_targetPosOld;
    private Vector3 m_targetDirOld;
    private float m_smoothness;
    private float m_extrapolation;

    public void Init(Transform target, float smoothness, float extrapolation)
    {
        m_target = target;
        m_smoothness = smoothness;
        m_extrapolation = extrapolation;
    }
    
    private Vector3 m_velocity;
    private void Update()
    {
        if (!m_target)
            return;
        
        Vector3 targetPosVel = (m_target.position - m_targetPosOld) / Time.deltaTime;
        Vector3 targetDirVel = (m_target.forward - m_targetDirOld) / Time.deltaTime;
        
        m_targetPosOld = m_target.position;
        m_targetDirOld = m_target.forward;

        Vector3 targetPos = m_target.position + targetPosVel * m_extrapolation;
        Vector3 targetDir = (m_target.forward + targetDirVel * m_extrapolation).normalized;
        
        Quaternion targetRot = Quaternion.LookRotation(targetDir, m_target.up);
        
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref m_velocity,
            m_smoothness * 0.1f
        );
        
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            1.0f - Mathf.Exp(-(10 * Time.deltaTime) / m_smoothness)
        );
    }
}