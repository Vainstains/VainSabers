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
        private Transform m_target = null!;
        

        private CircularBuffer<MovementData> m_movementData = new CircularBuffer<MovementData>(100);

        public void Init(Transform target)
        {
            m_target = target;
        }
        public override Pose GetPoseAgo(float age)
        {
            if (m_movementData.Count == 0)
                return m_target.GetPose();

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
        
        public override void SampleNonAlloc(int samples, float duration, Pose[] result)
        {
            if (samples <= 0)
                return;

            Pose currentPose = m_target.GetPose();

            if (samples == 1 || duration <= 0.0001f || m_movementData.Count == 0)
            {
                for (int i = 0; i < samples; i++)
                    result[i] = currentPose;
                return;
            }

            float interval = duration / (samples - 1);
            float accumulator = 0f;
            int dataIndex = 0;

            // Cache first pose
            result[0] = currentPose;

            for (int sampleIndex = 1; sampleIndex < samples; sampleIndex++)
            {
                float targetTime = sampleIndex * interval;

                while (dataIndex < m_movementData.Count - 1 &&
                       accumulator + m_movementData[dataIndex].DeltaTime < targetTime)
                {
                    accumulator += m_movementData[dataIndex].DeltaTime;
                    dataIndex++;
                }

                if (dataIndex >= m_movementData.Count - 1)
                {
                    result[sampleIndex] = m_movementData[m_movementData.Count - 1].Pose;
                    continue;
                }

                var newer = m_movementData[dataIndex];
                var older = m_movementData[dataIndex + 1];

                float segmentTime = targetTime - accumulator;
                float t = newer.DeltaTime > 0f
                    ? segmentTime / newer.DeltaTime
                    : 0f;

                result[sampleIndex] = newer.Pose.LerpTo(older.Pose, t);
            }
        }
        
        private void Update()
        {
            var currentPose = m_target.GetPose();
            m_movementData.Add(new  MovementData { Pose = currentPose, DeltaTime = Time.deltaTime });
        }
    }
}