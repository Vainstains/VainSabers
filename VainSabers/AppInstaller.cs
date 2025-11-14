using VainSabers.Config;
using Zenject;

namespace VainSabers;

internal class AppInstaller : Installer
{
    private readonly PluginConfig m_pluginConfig;

    public AppInstaller(PluginConfig pluginConfig)
    {
        this.m_pluginConfig = pluginConfig;
    }

    public override void InstallBindings()
    {
        Container.BindInstance(m_pluginConfig).AsSingle();
    }
}