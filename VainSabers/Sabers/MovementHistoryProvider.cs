using UnityEngine;

namespace VainSabers.Sabers
{
    public interface IMovementHistoryProvider
    {
        Pose GetPoseAgo(float age);
        void SampleNonAlloc(int samples, float duration, Pose[] buffer);
    }
    public abstract class MovementHistoryProvider : MonoBehaviour, IMovementHistoryProvider
    {
        public abstract Pose GetPoseAgo(float age);
        public abstract void SampleNonAlloc(int samples, float duration, Pose[] buffer);
    }
}