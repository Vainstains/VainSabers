using UnityEngine;

internal class SaberProfile
{
    public struct ProfileVertex
    {
        public Vector2 Position;
        public float BladeGlowFactor;
        public float BladeWhiteFactor;
        public float BladeFadeFactor;
        public float EdgeSoftness;
        public ProfileVertex(Vector2 position, float bladeGlowFactor, float bladeWhiteFactor, float bladeFadeFactor, float edgeSoftness)
        {
            Position = position;
            BladeGlowFactor = bladeGlowFactor;
            BladeWhiteFactor = bladeWhiteFactor;
            BladeFadeFactor = bladeFadeFactor;
            EdgeSoftness = edgeSoftness;
        }
    }
    
    public ProfileVertex[] Vertices { get; private set; }
    

    public SaberProfile(ProfileVertex[] vertices)
    {
        Vertices = vertices;
    }
}