using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace VainSabers.Config;

public class PluginConfig
{
    public virtual bool Enabled { get; set; } = true;
    public virtual string CurrentSaber { get; set; } = "default";
    public virtual int BlurMS { get; set; } = 16;
    public virtual float BlurSoftness { get; set; } = 0.8f;
    public virtual bool ActiveInMenu { get; set; } = true;
    
    public virtual int TipTrailMS { get; set; } = 140;
    public virtual int BladeTrailMS { get; set; } = 60;
    
    public virtual float SaberQuality { get; set; } = 1;
    
    public virtual float ZRotationOffset { get; set; } = 0f;

    public virtual bool MotionSmoothingEnabled { get; set; } = false;
    public virtual float MotionSmoothingStrength { get; set; } = 0.5f;
}