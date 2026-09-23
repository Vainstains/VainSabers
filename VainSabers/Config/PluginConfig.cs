using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using UnityEngine;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace VainSabers.Config;

public enum PointerMode
{
    Vanilla = 0,
    VainSabers = 1,
    None = 2
}

public enum LaserMode
{
    Vanilla = 0,
    VainSabers = 1,
    None = 2
}

public class PluginConfig
{
    public virtual bool Enabled { get; set; } = true;
    public virtual string CurrentSaber { get; set; } = "default";
    private int _blurMS = 16;
    public virtual int BlurMS
    {
        get => _blurMS;
        set => _blurMS = Mathf.Clamp(value, 0, 25);
    }
    public virtual float BlurSoftness { get; set; } = 0.8f;
    public virtual bool ActiveInMenu { get; set; } = true;
    
    public virtual int TipTrailMS { get; set; } = 140;
    public virtual int BladeTrailMS { get; set; } = 60;
    
    public virtual float SaberQuality { get; set; } = 1;
    
    public virtual float ZRotationOffset { get; set; } = 0f;

    public virtual bool PositionSmoothingEnabled { get; set; } = false;
    public virtual float PositionSmoothingStrength { get; set; } = 0.5f;
    public virtual bool RotationSmoothingEnabled { get; set; } = false;
    public virtual float RotationSmoothingStrength { get; set; } = 0.5f;

    public virtual PointerMode PointerMode { get; set; } = PointerMode.Vanilla;
    public virtual LaserMode LaserMode { get; set; } = LaserMode.Vanilla;
    public virtual float MenuPointerLaserBlurFactor { get; set; } = 1f;
    public virtual string MenuPointerDotPreset { get; set; } = "menupointer-dot";
}