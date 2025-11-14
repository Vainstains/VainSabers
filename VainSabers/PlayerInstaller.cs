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
        if (m_config.UseLegacy)
            Container.BindInstance(SaberModelRegistration.Create<LegacySaberModelController>(priority)).AsSingle();
        else
            Container.BindInstance(SaberModelRegistration.Create<BlurSaberModelController>(priority)).AsSingle();
    }
}