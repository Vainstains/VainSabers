using UnityEngine;

namespace VainSabers.Sabers;

internal static class BlurMotionHistory
{
    private const float BladeProbeDistance = 1f;
    private const float MaxSweepTravel = 0.72f;
    private const float MovementNoise = 0.0002f;

    public static void LimitSweep(Pose[] poses)
    {
        Vector3 firstBaseStep = Vector3.zero;
        Vector3 firstTipStep = Vector3.zero;
        Vector3 previousBase = poses[0].position;
        Vector3 previousTip = previousBase + poses[0].forward * BladeProbeDistance;
        float travel = 0f;

        for (int i = 1; i < poses.Length; i++)
        {
            Vector3 bladeBase = poses[i].position;
            Vector3 bladeTip = bladeBase + poses[i].forward * BladeProbeDistance;
            Vector3 baseStep = bladeBase - previousBase;
            Vector3 tipStep = bladeTip - previousTip;
            previousBase = bladeBase;
            previousTip = bladeTip;

            float distance = Mathf.Sqrt(Mathf.Max(baseStep.sqrMagnitude, tipStep.sqrMagnitude));
            if (distance < MovementNoise)
                continue;

            // Exposure follows the most recent swing. Older poses past a turn
            // would sweep the same area a second time and fold the mesh.
            float alignment = Vector3.Dot(baseStep, firstBaseStep) + Vector3.Dot(tipStep, firstTipStep);
            if (alignment < 0f)
            {
                RetainPrefix(poses, i - 1);
                return;
            }

            if (travel + distance > MaxSweepTravel)
            {
                RetainPrefix(poses, i - 1 + (MaxSweepTravel - travel) / distance);
                return;
            }

            if (travel == 0f)
            {
                firstBaseStep = baseStep;
                firstTipStep = tipStep;
            }
            travel += distance;
        }
    }

    private static void RetainPrefix(Pose[] poses, float lastSample)
    {
        float step = lastSample / (poses.Length - 1);
        // Read from the still-intact prefix while resampling in place.
        for (int i = poses.Length - 1; i > 0; i--)
        {
            float index = i * step;
            int newer = Mathf.Min((int)index, poses.Length - 2);
            poses[i] = poses[newer].LerpTo(poses[newer + 1], index - newer);
        }
    }
}
