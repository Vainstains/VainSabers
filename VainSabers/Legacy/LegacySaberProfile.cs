using Newtonsoft.Json;
using UnityEngine;

namespace VainSabers.Legacy;

[System.Serializable]
internal class LegacySaberProfile
{
    [System.Serializable]
    public struct Vert
    {
        public float x, y;

        public Vert(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
    }
    [System.Serializable]
    public struct ProfileVertex
    {
        public Vert Position;
        public float BladeGlowFactor;
        public float BladeWhiteFactor;
        public float BladeFadeFactor;
        public float EdgeSoftness;
        public ProfileVertex(float x, float y, float bladeGlowFactor, float bladeWhiteFactor, float bladeFadeFactor, float edgeSoftness)
        {
            Position = new Vert(x, y);
            BladeGlowFactor = bladeGlowFactor;
            BladeWhiteFactor = bladeWhiteFactor;
            BladeFadeFactor = bladeFadeFactor;
            EdgeSoftness = edgeSoftness;
        }
    }

    public ProfileVertex[] Vertices;
    public float TrailDuration;
    public float TrailOpacity;
    public LegacySaberProfile(ProfileVertex[] vertices, float trailDuration, float trailOpacity)
    {
        Vertices = vertices;
        TrailDuration = trailDuration;
        TrailOpacity = trailOpacity;
    }

    public static LegacySaberProfile Load(string path)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            Debug.LogError($"SaberProfile.Load: File not found at {path}");
            return null!;
        }

        try
        {
            string json = System.IO.File.ReadAllText(path);
            LegacySaberProfile? profile = JsonConvert.DeserializeObject<LegacySaberProfile>(json);

            if (profile == null)
            {
                Debug.LogError($"SaberProfile.Load: Failed to deserialize JSON at {path}");
                return null!;
            }

            return profile;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"SaberProfile.Load: Exception while loading profile from {path}\n{ex}");
            return null!;
        }
    }
    
    public void Save(string path)
    {
        try
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            System.IO.File.WriteAllText(path, json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"SaberProfile.Save: Exception while saving profile to {path}\n{ex}");
        }
    }
}