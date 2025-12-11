using UnityEngine;

namespace VainSabers
{
    public static class PoseHelpers
    {
        public static Pose LerpTo(this Pose pose, Pose other, float t)
        {
            return new Pose(
                Vector3.Lerp(pose.position, other.position, t),
                Quaternion.Slerp(pose.rotation, other.rotation, t));
        }

        public static Pose GetPose(this Transform transform)
        {
            return new Pose(transform.position, transform.rotation);
        }

        public static Pose TransformPose(this Pose pose, Matrix4x4 mat)
        {
            var position = mat.MultiplyPoint(pose.position);
            var basisForward = mat.MultiplyVector(pose.forward);
            var basisUp = mat.MultiplyVector(pose.up);
            
            return new Pose(position, Quaternion.LookRotation(basisForward, basisUp));
        }
        
        public static Matrix4x4 AsMatrix(this Pose pose)
        {
            return Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one);
        }
    }
}