using UnityEngine;

namespace VainSabers.Sabers
{
    public interface IMovementHistoryProvider
    {
        Pose GetPoseAgo(float age);
        Pose[] Sample(uint samples, float duration);
    }

    public abstract class MovementHistoryProvider : MonoBehaviour, IMovementHistoryProvider
    {
        public abstract Pose GetPoseAgo(float age);
        public abstract Pose[] Sample(uint samples, float duration);
    }
}