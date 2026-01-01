using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace VainSabers
{
    public interface IMovementHistoryProvider
    {
        Pose GetPoseAgo(float age);
    }

    public abstract class MovementHistoryProvider : MonoBehaviour, IMovementHistoryProvider
    {
        public abstract Pose GetPoseAgo(float age);
    }

    public static class MovementHistoryProviderExtensions
    {
        public static Pose[] Sample(this IMovementHistoryProvider source, uint samples, float duration)
        {
            if (duration <= 0)
                duration = 0;

            if (samples <= 0)
                samples = 1;
            
            var result = new Pose[samples];
            
            if (samples == 1)
            {
                result[0] = source.GetPoseAgo(0f);
                return result;
            }

            var step = duration / (samples - 1);
            for (var i = 0; i < samples; i++)
            {
                var age = i * step;
                result[i] = source.GetPoseAgo(age);
            }

            return result;
        }
    }
}