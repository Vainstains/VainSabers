using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace VainSabers.Config;

public class PluginConfig
{
    public virtual bool Enabled { get; set; } = true;
    public virtual string CurrentLegacySaber { get; set; } = "default";
    public virtual bool UseLegacy { get; set; } = false;
    public virtual string CurrentSaber { get; set; } = "segmented";

    public virtual int BlurMS { get; set; } = 16;
    public virtual float BlurSoftness { get; set; } = 0.75f;
    public virtual bool ActiveInMenu { get; set; } = true;
    
    public virtual int TipTrailMS { get; set; } = 140;
    public virtual int BladeTrailMS { get; set; } = 60;
    
    public virtual float SaberQuality { get; set; } = 1;
}