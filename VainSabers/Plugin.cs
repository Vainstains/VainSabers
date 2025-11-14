using System.Reflection;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.GameplaySetup;
using BeatSaberMarkupLanguage.Util;
using BeatSaberMarkupLanguage.ViewControllers;
using IPA;
using IPA.Loader;
using IpaLogger = IPA.Logging.Logger;
using Config = IPA.Config.Config;
using HarmonyLib;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using TMPro;
using UnityEngine;
using VainSabers.Config;
using Zenject;

namespace VainSabers;

[Plugin(RuntimeOptions.DynamicInit)]
internal class Plugin
{
    public static void Print(string msg)
    {
        Log.Info(msg);
    }
    internal static IpaLogger Log { get; private set; } = null!;
    
    private Harmony m_harmony;
    private Assembly m_executingAssembly = Assembly.GetExecutingAssembly();
    
    // Methods with [Init] are called when the plugin is first loaded by IPA.
    // All the parameters are provided by IPA and are optional.
    // The constructor is called before any method with [Init]. Only use [Init] with one constructor.
    [Init]
    public Plugin(IpaLogger ipaLogger, PluginMetadata pluginMetadata, IPA.Config.Config config, Zenjector zenjector)
    {
        Log = ipaLogger;
        Log.Info($"{pluginMetadata.Name} {pluginMetadata.HVersion} initialized.");
        
        Config.ConfigUtil.EnsureDefaultExists();
        
        m_harmony = new Harmony(pluginMetadata.Id);
        
        zenjector.UseLogger(ipaLogger);
        
        VainSabersAssets.LoadAssets();
        zenjector.Install<AppInstaller>(Location.App, config.Generated<PluginConfig>());
        zenjector.Install<MenuInstaller>(Location.Menu);
        zenjector.Install<PlayerInstaller>(Location.StandardPlayer);
    }


    [OnStart]
    public void OnApplicationStart()
    {
        Print("OnApplicationStart");
        
        m_harmony.PatchAll(m_executingAssembly);
    }

    [OnExit]
    public void OnApplicationQuit()
    {
        Print("OnApplicationQuit");
        m_harmony.UnpatchSelf();
    }
}

public static class VainSabersAssets
{
    public static Shader? TestShader { get; private set; }
    public static Shader? SaberShader { get; private set; }
    public static Shader? VertexGlowShader { get; private set; }
    
    public static Shader? VertexGlowShader2Side { get; private set; }
    
    public static Material? NormalSaberMaterial { get; private set; }
    public static Material? InvertedSaberMaterial { get; private set; }
    public static Material? NormalLitSaberMaterial { get; private set; }
    public static Material? InvertedLitSaberMaterial { get; private set; }
    public static void LoadAssets()
    {   
        Plugin.Print("Loading vs_assets from resource");
        var assets = AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("VainSabers.vs_assets"));
        if (assets)
        {
            foreach (var name in assets.GetAllAssetNames())
            {
                Plugin.Print(name);
            }
            TestShader = assets.LoadAsset<Shader>("vs_test");
            SaberShader = assets.LoadAsset<Shader>("vs_saber");
            VertexGlowShader = assets.LoadAsset<Shader>("vs_flatglow");
            VertexGlowShader2Side = assets.LoadAsset<Shader>("vs_flatglow_2side");
            
            NormalSaberMaterial = assets.LoadAsset<Material>("saber");
            InvertedSaberMaterial = assets.LoadAsset<Material>("saberinverted");
            NormalLitSaberMaterial = assets.LoadAsset<Material>("saberlit");
            InvertedLitSaberMaterial = assets.LoadAsset<Material>("saberlitinverted");
            
            Plugin.Print("Loaded vs_assets ok");
        }
        else
        {
            Plugin.Print("Failed to load vs_assets");
        }
    }
}