using UnityEngine;
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

    public void Init(Transform target, int framesToConsider)
    {
        m_target = target;
        m_movementData = new CircularBuffer<MovementData>(framesToConsider);
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

        for (int i = 0; i < m_movementData.Count; i++)
        {
            var data = m_movementData[i];
            avgPos += data.Position;
            avgUp  += data.Up;
            avgFwd += data.Fwd;
        }

        int count = m_movementData.Count;
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