using UnityEngine;
using VainSabers.Config;
using VainSabers.Helpers;

namespace VainSabers.Sabers;

internal class FrameAverager : MonoBehaviour
{
    private struct MovementData
    {
        public Vector3 Position;
        public Vector3 Up;
        public Vector3 Fwd;

        public MovementData(Vector3 position, Vector3 up, Vector3 fwd)
        {
            Position = position;
            Up = up;
            Fwd = fwd;
        }
    }

    private CircularBuffer<MovementData>? m_movementData;
    private Transform? m_target;
    private PluginConfig m_config = null!;
    public void Init(PluginConfig conf, Transform target)
    {
        m_config = conf;
        m_target = target;
        m_movementData = new CircularBuffer<MovementData>(100);
    }

    private void LateUpdate()
    {
        if (m_target == null || m_movementData == null)
            return;
        
        m_movementData.Add(new MovementData(
            m_target.position,
            m_target.up,
            m_target.forward
        ));
        
        Vector3 avgPos = Vector3.zero;
        Vector3 avgUp = Vector3.zero;
        Vector3 avgFwd = Vector3.zero;

        for (int i = 0; i < m_config.SaberSmoothing + 1; i++)
        {
            var data = m_movementData[i];
            avgPos += data.Position;
            avgUp  += data.Up;
            avgFwd += data.Fwd;
        }

        int count = Mathf.Min(m_movementData.Count, m_config.SaberSmoothing + 1);
        if (count > 0)
        {
            avgPos /= count;
            avgUp.Normalize();
            avgFwd.Normalize();

            avgUp  = (avgUp / count).normalized;
            avgFwd = (avgFwd / count).normalized;
            
            transform.position = avgPos;
            transform.rotation = Quaternion.LookRotation(avgFwd, avgUp);
        }
    }
}