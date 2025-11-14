using System.Runtime.CompilerServices;
using UnityEngine;

namespace VainSabers.Sabers
{
    public static class PoseHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Pose LerpTo(this in Pose pose, in Pose other, float t)
        {
            return new Pose(
                Vector3.LerpUnclamped(pose.position, other.position, t),
                Quaternion.SlerpUnclamped(pose.rotation, other.rotation, t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Pose GetPose(this Transform transform)
        {
            return new Pose(transform.position, transform.rotation);
        }
        
        // matrix math taken from chromapper. idk might be faster.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Pose TransformPose(this in Pose pose, in Matrix4x4 mat)
        {
            Vector3 pos;
            pos.x = (mat.m00 * pose.position.x) + (mat.m01 * pose.position.y) + (mat.m02 * pose.position.z) + mat.m03;
            pos.y = (mat.m10 * pose.position.x) + (mat.m11 * pose.position.y) + (mat.m12 * pose.position.z) + mat.m13;
            pos.z = (mat.m20 * pose.position.x) + (mat.m21 * pose.position.y) + (mat.m22 * pose.position.z) + mat.m23;
            Vector3 fwd, up;
            fwd.x = (mat.m00 * pose.forward.x) + (mat.m01 * pose.forward.y) + (mat.m02 * pose.forward.z);
            fwd.y = (mat.m10 * pose.forward.x) + (mat.m11 * pose.forward.y) + (mat.m12 * pose.forward.z);
            fwd.z = (mat.m20 * pose.forward.x) + (mat.m21 * pose.forward.y) + (mat.m22 * pose.forward.z);

            up.x = (mat.m00 * pose.up.x) + (mat.m01 * pose.up.y) + (mat.m02 * pose.up.z);
            up.y = (mat.m10 * pose.up.x) + (mat.m11 * pose.up.y) + (mat.m12 * pose.up.z);
            up.z = (mat.m20 * pose.up.x) + (mat.m21 * pose.up.y) + (mat.m22 * pose.up.z);
            return new Pose(pos, Quaternion.LookRotation(fwd, up));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 AsMatrix(this in Pose pose)
        {
            return Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one);
        }
    }
}