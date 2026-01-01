using SiraUtil.Sabers;
using VainSabers.Config;
using VainSabers.Sabers;
using Zenject;

namespace VainSabers;

public class PlayerInstaller : Installer
{
    private readonly PluginConfig m_config;
    
    private PlayerInstaller(PluginConfig config)
    {
        this.m_config = config;
    }
    
    public override void InstallBindings()
    {
        if (!m_config.Enabled)
        {
            Plugin.Print("VainSabers is disabled, not installing...");
            return;
        }
        Plugin.Print("VainSabers is enabled, installing...");
        
        const int priority = 69; // hehe
        Container.BindInstance(SaberModelRegistration.Create<BlurSaberModelController>(priority)).AsSingle();
    }
}