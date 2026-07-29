using Zenject;
using VainSabers.Menu;

namespace VainSabers;

public class MenuInstaller : Installer
{
    public override void InstallBindings()
    {
        Container.BindInterfacesAndSelfTo<ColorOverrideSettingsHook>().AsSingle();
        Container.BindInterfacesAndSelfTo<MenuPointers>().AsSingle();
        Container.BindInterfacesAndSelfTo<MenuSaberManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<VRPointerManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<GameplaySetupUI>().AsSingle();
    }
}