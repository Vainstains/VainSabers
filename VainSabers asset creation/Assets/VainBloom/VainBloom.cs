using UnityEngine;

[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class VainBloom : MonoBehaviour
{
    
    private RenderTexture[] blurTextures = new RenderTexture[8]; // 4 down, 4 up
    private Material filterMaterial;
    private Material blurMaterial;
    private Material compositeMaterial;
    
    // Shader property IDs for better performance
    private static readonly int ScreenTexture = Shader.PropertyToID("_ScreenTexture");
    private static readonly int ScreenRes = Shader.PropertyToID("_ScreenRes");
    private static readonly int Blur1 = Shader.PropertyToID("_Blur1");
    private static readonly int Blur2 = Shader.PropertyToID("_Blur2");
    private static readonly int Blur3 = Shader.PropertyToID("_Blur3");
    private static readonly int Blur4 = Shader.PropertyToID("_Blur4");
    
    void OnEnable()
    {
        CreateMaterials();
    }
    
    void OnDisable()
    {
        // Cleanup render textures
        for (int i = 0; i < blurTextures.Length; i++)
        {
            if (blurTextures[i] != null)
            {
                blurTextures[i].Release();
                blurTextures[i] = null;
            }
        }
    }
    
    void CreateMaterials()
    {
        if (filterMaterial == null)
        {
            Shader filterShader = Shader.Find("Hidden/BloomFilter");
            if (filterShader != null) filterMaterial = new Material(filterShader);
        }
        
        if (blurMaterial == null)
        {
            Shader blurShader = Shader.Find("Hidden/BloomBlur");
            if (blurShader != null) blurMaterial = new Material(blurShader);
        }
        
        if (compositeMaterial == null)
        {
            Shader compositeShader = Shader.Find("Hidden/BloomComposite");
            if (compositeShader != null) compositeMaterial = new Material(compositeShader);
        }
    }
    
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        CreateMaterials();
        
        if (filterMaterial == null || blurMaterial == null || compositeMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }
        
        int width = source.width;
        int height = source.height;
        
        // Set common properties
        filterMaterial.SetVector(ScreenRes, new Vector4(width, height, 0, 0));
        blurMaterial.SetVector(ScreenRes, new Vector4(width, height, 0, 0));
        compositeMaterial.SetVector(ScreenRes, new Vector4(width, height, 0, 0));
        
        // Filter pass - extract bright areas
        RenderTexture filterRT = GetTempRenderTexture(width/2, height/2, 0, source);
        Graphics.Blit(source, filterRT, filterMaterial, 0);
        
        // Blur passes
        const int levels = 4;
        RenderTexture lastRT = filterRT;
        
        for (int i = 0; i < levels; i++)
        {
            int div = (int)Mathf.Pow(2, i + 2);
            RenderTexture downRT = GetTempRenderTexture(width / div, height / div, i * 2, source);
            RenderTexture upRT = GetTempRenderTexture(width / (div / 2), height / (div / 2), i * 2 + 1, source);
            
            // Downsample
            Graphics.Blit(lastRT, downRT, blurMaterial, 0);
            // Upsample
            Graphics.Blit(downRT, upRT, blurMaterial, 0);
            
            lastRT = upRT;
            blurTextures[i] = upRT;
        }
        
        // Composite pass
        compositeMaterial.SetTexture(Blur1, blurTextures[0]);
        compositeMaterial.SetTexture(Blur2, blurTextures[1]);
        compositeMaterial.SetTexture(Blur3, blurTextures[2]);
        compositeMaterial.SetTexture(Blur4, blurTextures[3]);
        
        Graphics.Blit(source, destination, compositeMaterial, 0);
        
        // Cleanup temporary textures
        RenderTexture.ReleaseTemporary(filterRT);
        for (int i = 0; i < levels * 2; i++)
        {
            if (blurTextures[i] != null)
            {
                RenderTexture.ReleaseTemporary(blurTextures[i]);
                blurTextures[i] = null;
            }
        }
    }
    
    RenderTexture GetTempRenderTexture(int width, int height, int index, RenderTexture source)
    {
        if (blurTextures[index] == null || blurTextures[index].width != width || blurTextures[index].height != height)
        {
            if (blurTextures[index] != null)
                RenderTexture.ReleaseTemporary(blurTextures[index]);
                
            blurTextures[index] = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        }
        return blurTextures[index];
    }
}