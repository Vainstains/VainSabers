using UnityEngine;

namespace VainSabers
{
    public class MockMovementHistoryProvider : MovementHistoryProvider
    {
        public Transform Present;
        public Transform Past;
        public float PastAge;

        public override Pose GetPoseAgo(float age)
        {
            age = Mathf.Clamp(age, 0, PastAge);
            var present = Present.GetPose();
            var past = Past.GetPose();

            return present.LerpTo(past, age / PastAge);
        }
    }
}