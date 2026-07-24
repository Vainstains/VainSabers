using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUIControls;

namespace VainSabers.UI;

internal static class UIResources
{
    private static readonly Dictionary<string, Sprite> SpriteCache = new();
    public static Sprite LoadSpriteFromResource(
        string resourceName,
        float pixelsPerUnit = 320f,
        Vector2? pivot = null,
        Vector4? borderPixels = null,
        object? borderRatio = null)
    {
        if (SpriteCache.TryGetValue(resourceName, out var cached))
            return cached;
        
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new Exception($"Could not find embedded resource '{resourceName}'");

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var data = new byte[stream.Length];
        stream.Read(data, 0, data.Length);
        texture.LoadImage(data);
        
        var pivotVal = pivot ?? Vector2.one * 0.5f;
        
        Vector4 border;
        if (borderRatio != null)
        {
            float w = texture.width;
            float h = texture.height;
            if (borderRatio is float ratio)
            {
                border = new Vector4(ratio * w, ratio * h, ratio * w, ratio * h);
            }
            else if (borderRatio is Vector4 ratios)
            {
                border = new Vector4(ratios.x * w, ratios.y * h, ratios.z * w, ratios.w * h);
            }
            else
            {
                throw new ArgumentException("borderRatio must be float or Vector4", nameof(borderRatio));
            }
        }
        else
        {
            border = borderPixels ?? Vector4.zero;
        }
        
        var rect = new Rect(0, 0, texture.width, texture.height);
        var sprite = Sprite.Create(texture, rect, pivotVal, pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        
        SpriteCache[resourceName] = sprite;
        return sprite;
    }
    
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
    
    private static TMP_FontAsset? s_gameFont;
    public static TMP_FontAsset GameFont
    {
        get
        {
            if (s_gameFont != null)
                return s_gameFont;
            
            s_gameFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                .FirstOrDefault(t => t.name == "Teko-Medium SDF");
            if (s_gameFont == null)
                throw new Exception("Could not find Teko-Medium SDF font");
            
            return s_gameFont;
        }
    }

    private static Material? s_gameFontMaterial;
    public static Material GameFontMaterial
    {
        get
        {
            if (s_gameFontMaterial != null)
                return s_gameFontMaterial;
            
            var material = Resources.FindObjectsOfTypeAll<Material>()
                .LastOrDefault(m => m.name == "Teko-Medium SDF Curved Softer");
            if (material == null)
                throw new Exception("Could not find Teko-Medium SDF Curved Softer material");
            
            s_gameFontMaterial = new Material(material);
            return s_gameFontMaterial;
        }
    }
}