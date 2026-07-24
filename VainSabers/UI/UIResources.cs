using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using VRUIControls;

namespace VainSabers.UI;

internal static class UIResources
{
    private static Button? s_soloButton = null;
    private static Button GetSoloButton()
    {
        if (s_soloButton != null)
            return s_soloButton;
        
        s_soloButton = Resources.FindObjectsOfTypeAll<Button>().First(b => b.name == "SoloButton");
        return s_soloButton;
    }

    private static Material? s_noGlowMat;
    public static Material NoGlowMat
    {
        get
        {
            if (s_noGlowMat != null)
                return s_noGlowMat;
            
            var soloButton = GetSoloButton();
            if (soloButton == null)
                throw new Exception("Could not find SoloButton");
            
            var mat = soloButton.transform.Find("Image/Image0").GetComponent<Image>().material;
            s_noGlowMat = new Material(mat);
            return s_noGlowMat;
        }
    }

    private static Material? s_fogMat;
    public static Material FogMat
    {
        get
        {
            if (s_fogMat != null)
                return s_fogMat;
            
            var fogMaterial = Resources.FindObjectsOfTypeAll<Material>().First(m => m.name == "UIFogBG");
            s_fogMat = new Material(fogMaterial);
            return s_fogMat;
        }
    }

    private static PhysicsRaycasterWithCache? s_raycaster;

    public static PhysicsRaycasterWithCache Raycaster
    {
        get
        {
            if (s_raycaster != null)
                return s_raycaster;
            
            var physRaycaster = Resources.FindObjectsOfTypeAll<MainMenuViewController>()
                .First().GetComponent<VRGraphicRaycaster>()
                ._physicsRaycaster;
            
            if (physRaycaster == null)
                throw new Exception("Could not find PhysicsRaycasterWithCache");
            
            s_raycaster = physRaycaster;
            return s_raycaster;
        }
    }
    
    private static Sprite? s_roundSprite;
    public static Sprite RoundSprite
    {
        get
        {
            if (s_roundSprite != null)
                return s_roundSprite;
            
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream("VainSabers.ui_round.png");
            if (stream == null)
                throw new Exception("Could not find embedded resource 'VainSabers.ui_round.png'");
            
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);
            texture.LoadImage(data);
            
            var half = texture.width / 2;
            var border = new Vector4(half, half, half, half);
            s_roundSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f, 320, 0, SpriteMeshType.FullRect, border);
            
            return s_roundSprite;
        }
    }
}