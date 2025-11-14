using Zenject;
using VainSabers.Menu;

namespace VainSabers;

public class MenuInstaller : Installer
{
    public override void InstallBindings()
    {
        Container.BindInterfacesAndSelfTo<ColorOverrideSettingsHook>().AsSingle();
        Container.BindInterfacesAndSelfTo<MenuPointers>().AsSingle();
        Container.Bind<MenuSaberManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<GameplaySetupUI>().AsSingle();
    }
}