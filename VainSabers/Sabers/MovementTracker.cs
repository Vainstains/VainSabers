using UnityEngine;
using VainSabers.Config;
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
        private PluginConfig m_config = null!;
        private float m_zRotationOffset = 0f;
        private Quaternion m_zRotation = Quaternion.identity;
        
        private bool m_smoothedInitialized;
        private Pose m_smoothedPose;

        public float PositionSmoothingCoefficient = 1f;
        public float RotationSmoothingCoefficient = 1f;
        
        private CircularBuffer<MovementData> m_movementData = new CircularBuffer<MovementData>(100);

    public Transform Target
    {
        get => m_target;
        set => m_target = value;
    }

    public void ClearHistory()
    {
        m_movementData.Clear();
        m_smoothedInitialized = false;
    }

    public void Init(Transform target, PluginConfig config)
    {
        m_target = target;
        m_config = config;
    }

    public void SetZRotationOffset(float degrees)
    {
        m_zRotationOffset = degrees;
        m_zRotation = Quaternion.Euler(0f, 0f, degrees);
    }

    private Pose GetTargetPose()
    {
        var pose = m_target.GetPose();
        if (m_zRotationOffset != 0f)
            pose.rotation = pose.rotation * m_zRotation;
        return pose;
    }

        private bool IsPositionSmoothingEnabled => m_config.PositionSmoothingEnabled;

        private bool IsRotationSmoothingEnabled => m_config.RotationSmoothingEnabled;

        private bool IsAnySmoothingEnabled => IsPositionSmoothingEnabled || IsRotationSmoothingEnabled;

        private float EffectivePositionStrength => m_config.PositionSmoothingStrength;

        private float EffectiveRotationStrength => m_config.RotationSmoothingStrength;

        private Pose GetCurrentPose()
        {
            if (IsAnySmoothingEnabled && m_smoothedInitialized)
                return m_smoothedPose;
            return GetTargetPose();
        }
        public override Pose GetPoseAgo(float age)
        {
            if (m_movementData.Count == 0)
                return GetCurrentPose();

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

            Pose currentPose = GetCurrentPose();

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
            var currentPose = GetTargetPose();

            if (IsAnySmoothingEnabled)
            {
                if (!m_smoothedInitialized)
                {
                    m_smoothedPose = currentPose;
                    m_smoothedInitialized = true;
                }
                else
                {
                    Vector3 smoothedPos = m_smoothedPose.position;
                    Quaternion smoothedRot = m_smoothedPose.rotation;

                    if (IsPositionSmoothingEnabled)
                    {
                        var coeff = PositionSmoothingCoefficient;
                        if (coeff < 0.01f) coeff = 0.01f;
                        float rate = Mathf.Lerp(100f, 10f, Mathf.Clamp01(EffectivePositionStrength));
                        float alpha = 1f - Mathf.Exp(-rate * Time.deltaTime / coeff);
                        smoothedPos = Vector3.Lerp(smoothedPos, currentPose.position, alpha);
                    }
                    else
                    {
                        smoothedPos = currentPose.position;
                    }

                    if (IsRotationSmoothingEnabled)
                    {
                        var coeff = RotationSmoothingCoefficient;
                        if (coeff < 0.01f) coeff = 0.01f;
                        float rate = Mathf.Lerp(100f, 10f, Mathf.Clamp01(EffectiveRotationStrength));
                        float alpha = 1f - Mathf.Exp(-rate * Time.deltaTime / coeff);
                        smoothedRot = Quaternion.Slerp(smoothedRot, currentPose.rotation, alpha);
                    }
                    else
                    {
                        smoothedRot = currentPose.rotation;
                    }

                    m_smoothedPose = new Pose(smoothedPos, smoothedRot);
                }
                m_movementData.Add(new MovementData { Pose = m_smoothedPose, DeltaTime = Time.deltaTime });
            }
            else
            {
                m_smoothedInitialized = false;
                m_movementData.Add(new MovementData { Pose = currentPose, DeltaTime = Time.deltaTime });
            }
        }
    }
}