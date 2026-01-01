using UnityEngine;

namespace VainSabers
{
    [ExecuteInEditMode]
    public class MovementTracker : MovementHistoryProvider
    {
        struct MovementData
        {
            public Pose Pose;
            public float DeltaTime;
        }
        public Transform Target;

        private CircularBuffer<MovementData> m_movementData = new CircularBuffer<MovementData>(100);
        public override Pose GetPoseAgo(float age)
        {
            if (m_movementData.Count == 0)
                return Target.GetPose(); // fallback if no data

            float accumulated = 0f;

            // Start at the newest entry
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

            // If we didn’t find a segment (asked for too old), return oldest
            return m_movementData[m_movementData.Count - 1].Pose;
        }

        private void Update()
        {
            m_movementData.Add(new  MovementData { Pose = Target.GetPose(), DeltaTime = Time.deltaTime });
        }
    }
}