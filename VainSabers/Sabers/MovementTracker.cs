using UnityEngine;
using VainSabers.Helpers;

namespace VainSabers.Sabers
{
    [ExecuteInEditMode]
    public class MovementTracker : MovementHistoryProvider
    {
        struct MovementData
        {
            public Pose Pose;
            public float DeltaTime;
        }
        public Transform Target = null!;

        private CircularBuffer<MovementData> m_movementData = new CircularBuffer<MovementData>(100);
        public override Pose GetPoseAgo(float age)
        {
            if (m_movementData.Count == 0)
                return Target.GetPose();

            float accumulated = 0f;
            
            for (int i = 0; i < m_movementData.Count - 1; i++)
            {
                var newer = m_movementData[i];
                var older = m_movementData[i + 1];

                accumulated += newer.DeltaTime;

                if (accumulated >= age)
                {
                    float overshoot = accumulated - age;
                    float segmentDuration = newer.DeltaTime;
                    float t = 1f - (overshoot / segmentDuration);

                    return newer.Pose.LerpTo(older.Pose, t);
                }
            }
            
            return m_movementData[m_movementData.Count - 1].Pose;
        }
        
        public override Pose[] Sample(uint samples, float duration)
        {
            if (samples == 0) return new Pose[0];
            if (samples == 1) return new Pose[] { Target.GetPose() };
            
            Pose[] result = new Pose[samples];
            
            if (duration <= 0.001f)
            {
                var currentPose = Target.GetPose();
                for (int i = 0; i < samples; i++)
                {
                    result[i] = currentPose;
                }
                return result;
            }
            
            float interval = duration / (samples - 1);
            
            if (m_movementData.Count == 0)
            {
                var currentPose = Target.GetPose();
                for (int i = 0; i < samples; i++)
                {
                    result[i] = currentPose;
                }
                return result;
            }

            float accumulator = 0f;
            int sampleIndex = 0;
            int dataIndex = 0;
            
            while (sampleIndex < samples && dataIndex < m_movementData.Count - 1)
            {
                float targetTime = sampleIndex * interval;
                
                while (dataIndex < m_movementData.Count - 1 && accumulator < targetTime)
                {
                    accumulator += m_movementData[dataIndex].DeltaTime;
                    dataIndex++;
                }
                
                if (dataIndex >= m_movementData.Count)
                {
                    break;
                }
                
                int newerIndex = Mathf.Max(0, dataIndex - 1);
                int olderIndex = dataIndex;
                
                float segmentStart = accumulator - m_movementData[newerIndex].DeltaTime;
                float segmentEnd = accumulator;
                float segmentDuration = segmentEnd - segmentStart;
                
                if (segmentDuration > 0)
                {
                    float t = (targetTime - segmentStart) / segmentDuration;
                    result[sampleIndex] = m_movementData[newerIndex].Pose.LerpTo(m_movementData[olderIndex].Pose, t);
                }
                else
                {
                    result[sampleIndex] = m_movementData[newerIndex].Pose;
                }
                
                sampleIndex++;
            }
            
            var oldestPose = m_movementData[m_movementData.Count - 1].Pose;
            for (int i = sampleIndex; i < samples; i++)
            {
                result[i] = oldestPose;
            }
            
            return result;
        }

        private Pose m_lastPose;
        private void Update()
        {
            var currentPose = Target.GetPose();
            m_movementData.Add(new  MovementData { Pose = currentPose.LerpTo(m_lastPose, 0.5f), DeltaTime = Time.deltaTime });
            m_lastPose = currentPose;
        }
    }
}