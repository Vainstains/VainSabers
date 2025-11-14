using UnityEngine;

namespace VainSabers.Sabers
{
    public class MockMovementHistoryProvider : MovementHistoryProvider
    {
        public Transform Present = null!;
        public Transform Past = null!;
        public float PastAge;

        public override Pose GetPoseAgo(float age)
        {
            age = Mathf.Clamp(age, 0, PastAge);
            var present = Present.GetPose();
            var past = Past.GetPose();

            return present.LerpTo(past, age / PastAge);
        }
        
        public override Pose[] Sample(uint samples, float duration)
        {
            if (samples == 0) return new Pose[0];
            
            Pose[] result = new Pose[samples];
            var present = Present.GetPose();
            var past = Past.GetPose();
            
            for (int i = 0; i < samples; i++)
            {
                float sampleTime = i * (duration / (samples - 1));
                float t = Mathf.Clamp01(sampleTime / PastAge);
                result[i] = present.LerpTo(past, t);
            }
            
            return result;
        }
    }
}