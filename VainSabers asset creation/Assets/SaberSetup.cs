using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaberSetup : MonoBehaviour
{
    public Transform saber;

    private readonly SaberProfile m_saberProfile = new SaberProfile(new[]
    {
        // pommel
        new SaberProfile.ProfileVertex(new Vector2(-0.155f, 0.0000f), 1.0f, 0.0f, 0.2f, 0.2f),
        new SaberProfile.ProfileVertex(new Vector2(-0.153f, 0.0071f), 1.0f, 0.0f, 0.2f, 0.2f),
        new SaberProfile.ProfileVertex(new Vector2(-0.1347f, 0.0110f), 1.0f, 0.0f, 0.2f, 0.2f),
        
        // handle
        new SaberProfile.ProfileVertex(new Vector2(-0.1237f, 0.00825f), 0.0f, 0.0f, 0.0f, 0.1f),
        new SaberProfile.ProfileVertex(new Vector2(-0.0266f, 0.0103f), 0.0f, 0.0f, 0.0f, 0.1f),
        new SaberProfile.ProfileVertex(new Vector2(-0.0211f, 0.0126f), 0.0f, 0.0f, 0.0f, 0.1f),
        new SaberProfile.ProfileVertex(new Vector2(-0.0040f, 0.0126f), 0.0f, 0.0f, 0.0f, 0.1f),
        
        // blade
        new SaberProfile.ProfileVertex(new Vector2(0.0050f, 0.0110f), 1.0f, 0.3f, 0.2f, 0.2f),
        new SaberProfile.ProfileVertex(new Vector2(0.0567f, 0.0110f), 1.0f, 0.1f, 0.3f, 0.2f),
        new SaberProfile.ProfileVertex(new Vector2(0.1746f, 0.0110f), 1.0f, 0.0f, 0.5f, 0.3f),
        
        new SaberProfile.ProfileVertex(new Vector2(0.8488f, 0.0080f), 1.0f, 0.0f, 0.7f, 0.5f),
        new SaberProfile.ProfileVertex(new Vector2(0.9513f, 0.0060f), 1.0f, 0.0f, 0.8f, 0.6f),
        new SaberProfile.ProfileVertex(new Vector2(1.0000f, 0.0030f), 1.0f, 0.0f, 1.0f, 0.7f),
    });
    private SaberSweepData  m_sweepData = new SaberSweepData(100);
    void Start()
    {
        var follower = gameObject.AddInitChild<SmoothFollower>(saber, 0.1f, 0.005f);
        
        var c = new Color(0.2f, 0.6f, 1.0f, 1.0f);
        gameObject.AddInitChild<SaberSweepGenerator>(saber, m_sweepData, 24, 1.0f / 60.0f, m_saberProfile).SetColor(c);
        gameObject.AddInitChild<SaberTipTrail>(saber, m_sweepData).SetColor(c);
    }
    
    
}