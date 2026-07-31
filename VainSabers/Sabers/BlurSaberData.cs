using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using VainSabers.Config;

namespace VainSabers.Sabers;

public class BlurSaberData : MonoBehaviour
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerSettings PresetJsonSettings = new()
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.Auto
    };

    private PluginConfig? m_config = null;
    public Color CustomColor;
    public float BlurTime => m_config != null ? m_config.BlurMS * 0.001f : 0.04f;
    
    private readonly List<BlurSaberPart> m_components = new List<BlurSaberPart>();
    public IReadOnlyList<BlurSaberPart> Components => m_components.AsReadOnly();
    public int ComponentCount => m_components.Count;

    public bool UseCustomTrails { get; private set; }
    public List<SaberTrailData> TipTrails { get; private set; } = new();
    public SaberTrailData? BladeTrail { get; private set; }

    public event Action? TrailsChanged;

    public void Init(PluginConfig config)
    {
        m_config = config;
    }

    public void RefreshComponentList()
    {
        m_components.Clear();
        foreach (Transform child in transform)
        {
            var part = child.GetComponent<BlurSaberPart>();
            if (part != null)
                m_components.Add(part);
        }
    }

    public BlurSaberPart AddComponent(string partName = "New Part")
    {
        var go = new GameObject(partName);
        go.transform.SetParent(transform, false);
        
        var newPart = go.AddComponent<BlurSaberPart>();
        if (VainSabersAssets.NormalSaberMaterial != null)
            newPart.Material = VainSabersAssets.NormalSaberMaterial;
        if (VainSabersAssets.InvertedSaberMaterial != null)
            newPart.InvertedMaterial = VainSabersAssets.InvertedSaberMaterial;
        if (VainSabersAssets.NormalLitSaberMaterial != null)
            newPart.LitMaterial = VainSabersAssets.NormalLitSaberMaterial;
        if (VainSabersAssets.InvertedLitSaberMaterial != null)
            newPart.LitInvertedMaterial = VainSabersAssets.InvertedLitSaberMaterial;
        
        newPart.Length = 0.1f;
        newPart.StartRadius = 0.03f;
        newPart.EndRadius = 0.03f;
        newPart.StartColor = Color.white;
        newPart.EndColor = Color.white;
        newPart.StartCustomColorWeight = 1f;
        newPart.EndCustomColorWeight = 1f;
        newPart.StartGlow = 1f;
        newPart.EndGlow = 1f;
        newPart.StartOpacity = 1f;
        newPart.EndOpacity = 1f;
        newPart.Inverted = false;
        newPart.BlurFactor = 1f;
        newPart.BlurFadeFactor = 1f;
        newPart.EnableEndCaps = true;
        newPart.EndCapExtension = 0.25f;
        newPart.UseLookDir = false;
        newPart.LookDir = Vector3.zero;
        newPart.Lit = false;
        newPart.DepthOffset = 0f;
        
        newPart.Config = m_config!;

        m_components.Add(newPart);
        return newPart;
    }

    public bool RemoveComponent(BlurSaberPart part, bool destroyGameObject = true)
    {
        if (part == null || !m_components.Contains(part))
            return false;

        m_components.Remove(part);

        if (destroyGameObject)
        {
#if UNITY_EDITOR
            if (Application.isEditor)
                DestroyImmediate(part.gameObject);
            else
                Destroy(part.gameObject);
#else
            Destroy(part.gameObject);
#endif
        }

        return true;
    }

    public bool RemoveComponentAt(int index, bool destroyGameObject = true)
    {
        if (index < 0 || index >= m_components.Count)
            return false;

        var part = m_components[index];
        return RemoveComponent(part, destroyGameObject);
    }

    public void RemoveAllComponents(bool destroyGameObjects = true)
    {
        var componentsToRemove = new List<BlurSaberPart>(m_components);
        foreach (var part in componentsToRemove)
            RemoveComponent(part, destroyGameObjects);
    }

    public BlurSaberPart FindComponent(string name)
    {
        return m_components.Find(part => part.gameObject.name == name);
    }

    public bool HasComponent(BlurSaberPart part)
    {
        return m_components.Contains(part);
    }

    #region Trail Management

    public void SetUseCustomTrails(bool value)
    {
        UseCustomTrails = value;
        TrailsChanged?.Invoke();
    }

    public void AddTipTrail()
    {
        TipTrails.Add(new SaberTrailData(
            position: new float[] { 0f, 0f, 1f },
            color: new float[] { 1f, 1f, 1f },
            customBlend: 1f,
            glow: 1f,
            opacity: 1f,
            width: 0.008f,
            length: 140,
            queueOffset: 0
        ));
        TrailsChanged?.Invoke();
    }

    public void RemoveTipTrail(int index)
    {
        if (index < 0 || index >= TipTrails.Count)
            return;
        TipTrails.RemoveAt(index);
        TrailsChanged?.Invoke();
    }

    public void SetTipTrail(int index, SaberTrailData data)
    {
        if (index < 0 || index >= TipTrails.Count)
            return;
        TipTrails[index] = data;
        TrailsChanged?.Invoke();
    }

    public void SetBladeTrail(SaberTrailData data)
    {
        BladeTrail = data;
        TrailsChanged?.Invoke();
    }

    public void EnsureDefaultTrails()
    {
        if (TipTrails.Count == 0)
        {
            TipTrails.Add(new SaberTrailData(
                position: new float[] { 0f, 0f, 1f },
                color: new float[] { 1f, 1f, 1f },
                customBlend: 1f,
                glow: 1f,
                opacity: 1f,
                width: 0.008f,
                length: m_config?.TipTrailMS ?? 140,
                queueOffset: 0
            ));
        }

        if (BladeTrail == null)
        {
            BladeTrail = new SaberTrailData(
                position: new float[] { 0f, 0f, 1f },
                color: new float[] { 1f, 1f, 1f },
                customBlend: 1f,
                glow: 1f,
                opacity: 0.3f,
                width: 0.01f,
                length: m_config?.BladeTrailMS ?? 60,
                queueOffset: 0
            );
        }
    }

    #endregion

    public BlurSaberPart DuplicateComponent(BlurSaberPart source)
    {
        var newPart = AddComponent($"{source.gameObject.name} Copy");
        newPart.RotX = source.RotX;
        newPart.RotY = source.RotY;
        newPart.RotZ = source.RotZ;
        newPart.CopyVisualPropertiesFrom(source);

        newPart.Animators = new List<BlurPartAnimationModulator>(source.Animators.Count);
        foreach (var animator in source.Animators)
            newPart.Animators.Add(animator.Clone());

        return newPart;
    }

    public static bool IsSupportedVersion(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return true;

        if (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            string json = File.ReadAllText(path);
            var preset = JsonConvert.DeserializeObject<PresetData>(json, PresetJsonSettings);
            return preset == null || preset.Version <= CurrentVersion;
        }
        catch
        {
            return true;
        }
    }

    public bool ImportFromFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogError($"File not found: {path}");
            return false;
        }

        if (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            ImportFromLegacyTxt(path);
            return true;
        }

        return ImportFromJson(path);
    }

    private bool ImportFromJson(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            var preset = JsonConvert.DeserializeObject<PresetData>(json, PresetJsonSettings);
            if (preset?.Parts == null)
            {
                Debug.LogWarning($"No parts found in {path}");
                return false;
            }

            if (preset.Version > CurrentVersion)
            {
                Plugin.Log.Warn($"Rejected preset {path}: file version {preset.Version} is newer than supported version {CurrentVersion}");
                return false;
            }

            RemoveAllComponents();

            foreach (var partData in preset.Parts)
            {
                var part = AddComponent(partData.Name ?? "Part");

                part.Position = ArrToVec3(partData.Position);
                part.RotX = partData.Rotation[0];
                part.RotY = partData.Rotation[1];
                part.RotZ = partData.Rotation[2];
                part.Length = partData.Length;
                part.GeometryHandling = partData.GeometryMode;
                part.HueShift = partData.HueShift;

                part.StartRadius = partData.StartRadius;
                part.StartColor = ArrToColor(partData.StartColor);
                part.StartCustomColorWeight = partData.StartCustomWeight;
                part.StartGlow = partData.StartGlow;
                part.StartOpacity = partData.StartOpacity;

                part.EndRadius = partData.EndRadius;
                part.EndColor = ArrToColor(partData.EndColor);
                part.EndCustomColorWeight = partData.EndCustomWeight;
                part.EndGlow = partData.EndGlow;
                part.EndOpacity = partData.EndOpacity;

                part.Inverted = partData.Inverted;
                part.Lit = partData.Lit;
                part.BlurFactor = Mathf.Clamp01(partData.Blur);
                part.BlurFadeFactor = Mathf.Clamp(partData.BlurFade, 0f, 10f);
                part.EnableEndCaps = partData.EnableEndCaps;
                part.EnableRoundedNormals = partData.EnableRoundedNormals;
                part.EndCapExtension = Mathf.Clamp(partData.EndCapExtension, 0f, 3f);

                part.LookDir = ArrToVec3(partData.LookDir);
                part.UseLookDir = partData.UseLookDir;
                part.LinkedPartIndex = partData.LinkedPartIndex;

                part.BulgeAmount = Mathf.Clamp(partData.BulgeAmount, -1f, 1f);
                part.MinimumRings = Mathf.Clamp(partData.MinimumRings, 2, 10);
                part.RenderQueueOffset = partData.RenderQueueOffset;
                part.DepthOffset = partData.DepthOffset;

                part.RimFactor = partData.RimFactor;
                part.RimPower = partData.RimPower;
                part.RimPerpendicular = partData.RimPerpendicular;
                part.SpecularStrength = partData.SpecularStrength;
                part.SpecularPower = partData.SpecularPower;
                part.Metallic = partData.Metallic;
                part.Smoothness = partData.Smoothness;
                part.CubemapStrength = partData.CubemapStrength;
                part.CubemapRotation = partData.CubemapRotation;
                part.FresnelStrength = partData.FresnelStrength;
                part.FresnelPower = partData.FresnelPower;
                part.RimColor = ArrToColor(partData.RimColor);
                part.ColorTextureName = partData.ColorTexture;
                part.GlowTextureName = partData.GlowTexture;
                part.TextureWrap = (TextureWrapMode)Mathf.Clamp(partData.TextureWrap, 0, 3);

                if (partData.Animators != null)
                    part.Animators = partData.Animators;

                if (partData.Rings != null)
                {
                    foreach (var ring in partData.Rings)
                    {
                        part.RingParams.Add(new BlurSaberRingParams(
                            posAlongPart01: ring.Position,
                            radius: ring.Radius,
                            color: ArrToColor(ring.Color),
                            customWeight: ring.CustomWeight,
                            glow: ring.Glow,
                            opacity: ring.Opacity,
                            inverted: ring.Inverted,
                            offset: new Vector2(ring.OffsetX, ring.OffsetY),
                            uvOffset: ring.UvOffset
                        ));
                    }
                }
            }

            Debug.Log($"Imported saber with {ComponentCount} parts from {path}");

            UseCustomTrails = preset.UseCustomTrails;
            TipTrails.Clear();
            if (preset.TipTrails != null)
            {
                foreach (var td in preset.TipTrails)
                {
                    TipTrails.Add(new SaberTrailData(
                        position: td.Position ?? new float[] { 0, 0, 1 },
                        color: td.Color ?? new float[] { 1, 1, 1 },
                        customBlend: td.CustomBlend,
                        glow: td.Glow,
                        opacity: td.Opacity,
                        width: td.Width,
                        length: td.Length,
                        queueOffset: td.QueueOffset
                    ));
                }
            }

            if (preset.BladeTrail != null)
            {
                var bt = preset.BladeTrail;
                BladeTrail = new SaberTrailData(
                    position: bt.Position ?? new float[] { 0, 0, 1 },
                    color: bt.Color ?? new float[] { 1, 1, 1 },
                    customBlend: bt.CustomBlend,
                    glow: bt.Glow,
                    opacity: bt.Opacity,
                    width: bt.Width,
                    length: bt.Length,
                    queueOffset: bt.QueueOffset
                );
            }
            else
            {
                BladeTrail = null;
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to import JSON preset from {path}: {ex.Message}");
            return false;
        }
    }

    public void SaveToFile(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Plugin.Log.Info("Save path cannot be null or empty");
            return;
        }

        try
        {
            var preset = new PresetData { Version = CurrentVersion, Parts = new List<PartData>() };

            for (int i = 0; i < m_components.Count; i++)
            {
                var part = m_components[i];
                if (part == null) continue;

                var partData = new PartData
                {
                    Name = part.gameObject.name,
                    Position = new float[] { part.Position.x, part.Position.y, part.Position.z },
                    Rotation = new float[] { part.RotX, part.RotY, part.RotZ },
                    LinkedPartIndex = part.LinkedPartIndex,
                    Length = part.Length,
                    GeometryMode = part.GeometryHandling,
                    HueShift = part.HueShift,

                    StartRadius = part.StartRadius,
                    StartColor = new float[] { part.StartColor.r, part.StartColor.g, part.StartColor.b },
                    StartCustomWeight = part.StartCustomColorWeight,
                    StartGlow = part.StartGlow,
                    StartOpacity = part.StartOpacity,

                    EndRadius = part.EndRadius,
                    EndColor = new float[] { part.EndColor.r, part.EndColor.g, part.EndColor.b },
                    EndCustomWeight = part.EndCustomColorWeight,
                    EndGlow = part.EndGlow,
                    EndOpacity = part.EndOpacity,

                    Inverted = part.Inverted,
                    Lit = part.Lit,
                    Blur = part.BlurFactor,
                    BlurFade = part.BlurFadeFactor,
                    EnableEndCaps = part.EnableEndCaps,
                    EnableRoundedNormals = part.EnableRoundedNormals,
                    EndCapExtension = part.EndCapExtension,

                    LookDir = new float[] { part.LookDir.x, part.LookDir.y, part.LookDir.z },
                    UseLookDir = part.UseLookDir,

                    BulgeAmount = part.BulgeAmount,
                    MinimumRings = part.MinimumRings,
                    RenderQueueOffset = part.RenderQueueOffset,
                    DepthOffset = part.DepthOffset,

                    RimFactor = part.RimFactor,
                    RimPower = part.RimPower,
                    RimPerpendicular = part.RimPerpendicular,

                    SpecularStrength = part.SpecularStrength,
                    SpecularPower = part.SpecularPower,
                    Metallic = part.Metallic,
                    Smoothness = part.Smoothness,
                    CubemapStrength = part.CubemapStrength,
                    CubemapRotation = part.CubemapRotation,
                    FresnelStrength = part.FresnelStrength,
                    FresnelPower = part.FresnelPower,
                    RimColor = new float[] { part.RimColor.r, part.RimColor.g, part.RimColor.b },

                    ColorTexture = part.ColorTextureName,
                    GlowTexture = part.GlowTextureName,
                    TextureWrap = (int)part.TextureWrap,
                    Animators = part.Animators.Count > 0 ? part.Animators : null
                };

                if (part.GeometryHandling == BlurSaberPart.GeometryType.Advanced && part.RingParams.Count > 0)
                {
                    partData.Rings = new List<RingData>();
                    foreach (var ring in part.RingParams)
                    {
                    partData.Rings.Add(new RingData
                    {
                        Position = ring.PosAlongPart01,
                        Radius = ring.Radius,
                        Color = new float[] { ring.Color.r, ring.Color.g, ring.Color.b },
                        CustomWeight = ring.CustomWeight,
                        Glow = ring.Glow,
                        Opacity = ring.Opacity,
                        Inverted = ring.Inverted,
                        OffsetX = ring.Offset.x,
                        OffsetY = ring.Offset.y,
                        UvOffset = ring.UvOffset
                    });
                    }
                }

                preset.Parts.Add(partData);
            }

            preset.UseCustomTrails = UseCustomTrails;
            if (TipTrails.Count > 0)
            {
                preset.TipTrails = new List<TrailData>();
                foreach (var td in TipTrails)
                {
                    preset.TipTrails.Add(new TrailData
                    {
                        Position = td.Position,
                        Color = td.Color,
                        CustomBlend = td.CustomBlend,
                        Glow = td.Glow,
                        Opacity = td.Opacity,
                        Width = td.Width,
                        Length = td.Length,
                        QueueOffset = td.QueueOffset
                    });
                }
            }

            if (BladeTrail.HasValue)
            {
                var td = BladeTrail.Value;
                preset.BladeTrail = new TrailData
                {
                    Position = td.Position,
                    Color = td.Color,
                    CustomBlend = td.CustomBlend,
                    Glow = td.Glow,
                    Opacity = td.Opacity,
                    Width = td.Width,
                    Length = td.Length,
                    QueueOffset = td.QueueOffset
                };
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(preset, PresetJsonSettings);
            File.WriteAllText(path, json);
            Plugin.Log.Info($"Saved saber with {ComponentCount} parts to {path}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to save saber to {path}: {ex.Message}");
        }
    }

    #region Legacy .txt Format Support

    private void ImportFromLegacyTxt(string path)
    {
        string[] lines = File.ReadAllLines(path);
        RemoveAllComponents();

        BlurSaberPart currentPart = null!;

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            if (line.StartsWith(".part"))
            {
                var partName = line.Length <= 6 ? "Part" : line.Substring(6).Trim();
                currentPart = AddComponent(partName);
                continue;
            }

            if (currentPart == null)
                continue;

            string[] tokens = line.Split(' ');
            string key = tokens[0];
            float[] vals = ParseFloats(tokens);

            switch (key)
            {
                case "pos":
                    currentPart.Position = new Vector3(vals[0], vals[1], vals[2]);
                    break;
                case "rot":
                    currentPart.RotX = vals[0];
                    currentPart.RotY = vals[1];
                    currentPart.RotZ = vals[2];
                    break;
                case "length":
                    currentPart.Length = vals[0];
                    break;
                case "geometryMode":
                    currentPart.GeometryHandling = Mathf.Approximately(vals[0], 1f)
                        ? BlurSaberPart.GeometryType.Advanced
                        : BlurSaberPart.GeometryType.Simple;
                    break;
                case "ring":
                    if (vals.Length != 9 && vals.Length != 10)
                    {
                        Debug.LogWarning($"Skipping malformed ring line in {path}: '{line}'");
                        break;
                    }
                    currentPart.RingParams.Add(new BlurSaberRingParams(
                        posAlongPart01: vals[0],
                        radius: vals[1],
                        color: new Color(vals[2], vals[3], vals[4], 1f),
                        customWeight: vals[5],
                        glow: vals[6],
                        opacity: vals[7],
                        inverted: Mathf.Approximately(vals[8], 1f),
                        offset: Vector2.zero,
                        uvOffset: vals.Length >= 10 ? vals[9] : 0f
                    ));
                    break;
                case "startRad":
                    currentPart.StartRadius = vals[0];
                    break;
                case "startColor":
                    currentPart.StartColor = new Color(vals[0], vals[1], vals[2], 1f);
                    break;
                case "startCustomWeight":
                    currentPart.StartCustomColorWeight = vals[0];
                    break;
                case "startGlow":
                    currentPart.StartGlow = vals[0];
                    break;
                case "startOpacity":
                    currentPart.StartOpacity = vals[0];
                    break;
                case "endRad":
                    currentPart.EndRadius = vals[0];
                    break;
                case "endColor":
                    currentPart.EndColor = new Color(vals[0], vals[1], vals[2], 1f);
                    break;
                case "endCustomWeight":
                    currentPart.EndCustomColorWeight = vals[0];
                    break;
                case "endGlow":
                    currentPart.EndGlow = vals[0];
                    break;
                case "endOpacity":
                    currentPart.EndOpacity = vals[0];
                    break;
                case "inverted":
                    currentPart.Inverted = Mathf.Approximately(vals[0], 1f);
                    break;
                case "blur":
                    currentPart.BlurFactor = Mathf.Clamp01(vals[0]);
                    break;
                case "blurFade":
                    currentPart.BlurFadeFactor = Mathf.Clamp(vals[0], 0f, 10f);
                    break;
                case "endCapExtension":
                    currentPart.EndCapExtension = Mathf.Clamp(vals[0], 0.0f, 3.0f);
                    break;
                case "enableEndCaps":
                    currentPart.EnableEndCaps = Mathf.Approximately(vals[0], 1f);
                    break;
                case "enableRoundedNormals":
                    currentPart.EnableRoundedNormals = Mathf.Approximately(vals[0], 1f);
                    break;
                case "lookDir":
                    currentPart.LookDir = new Vector3(vals[0], vals[1], vals[2]);
                    break;
                case "useLookDir":
                    currentPart.UseLookDir = Mathf.Approximately(vals[0], 1f);
                    break;
                case "bulgeAmount":
                    currentPart.BulgeAmount = Mathf.Clamp(vals[0], -1f, 1f);
                    break;
                case "minimumRings":
                    currentPart.MinimumRings = Mathf.Clamp((int)vals[0], 2, 10);
                    break;
                case "renderQueueOffset":
                    currentPart.RenderQueueOffset = Mathf.RoundToInt(vals[0]);
                    break;
                case "depthOffset":
                    currentPart.DepthOffset = vals[0];
                    break;
                case "lit":
                    currentPart.Lit = Mathf.Approximately(vals[0], 1f);
                    break;
                case "hueShift":
                    currentPart.HueShift = vals[0];
                    break;
                case "rimFactor":
                    currentPart.RimFactor = vals[0];
                    break;
                case "rimPower":
                    currentPart.RimPower = vals[0];
                    break;
                case "rimPerpendicular":
                    currentPart.RimPerpendicular = vals[0];
                    break;
                case "specularStrength":
                    currentPart.SpecularStrength = vals[0];
                    break;
                case "specularPower":
                    currentPart.SpecularPower = vals[0];
                    break;
                case "metallic":
                    currentPart.Metallic = vals[0];
                    break;
                case "smoothness":
                    currentPart.Smoothness = vals[0];
                    break;
                case "cubemapStrength":
                    currentPart.CubemapStrength = vals[0];
                    break;
                case "cubemapRotation":
                    currentPart.CubemapRotation = vals[0];
                    break;
                case "fresnelStrength":
                    currentPart.FresnelStrength = vals[0];
                    break;
                case "fresnelPower":
                    currentPart.FresnelPower = vals[0];
                    break;
                case "rimColor":
                    if (vals.Length >= 3)
                        currentPart.RimColor = new Color(vals[0], vals[1], vals[2], 1f);
                    break;
            }
        }

        Debug.Log($"Imported saber with {ComponentCount} parts from legacy txt: {path}");
    }

    #endregion

    #region Legacy Format Conversion

    public static void ConvertLegacyFile(string txtPath)
    {
        try
        {
            if (!File.Exists(txtPath))
                return;

            string jsonPath = Path.ChangeExtension(txtPath, ".json");
            if (File.Exists(jsonPath))
                return;

            var data = new GameObject().AddComponent<BlurSaberData>();
            data.ImportFromLegacyTxt(txtPath);
            data.SaveToFile(jsonPath);
#if UNITY_EDITOR
            DestroyImmediate(data.gameObject);
#else
            Destroy(data.gameObject);
#endif
            Plugin.Log.Debug($"Converted legacy preset: {Path.GetFileName(txtPath)} -> {Path.GetFileName(jsonPath)}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to convert legacy preset {txtPath}: {ex.Message}");
        }
    }

    #endregion

    private float[] ParseFloats(string[] tokens)
    {
        float[] vals = new float[tokens.Length - 1];
        for (int i = 1; i < tokens.Length; i++)
            vals[i - 1] = float.Parse(tokens[i], CultureInfo.InvariantCulture);
        return vals;
    }

    #region JSON Data Types

    private class PresetData
    {
        public int Version { get; set; } = 1;
        public List<PartData>? Parts { get; set; }
        public bool UseCustomTrails { get; set; }
        public List<TrailData>? TipTrails { get; set; }
        public TrailData? BladeTrail { get; set; }
    }

    private class PartData
    {
        public string? Name { get; set; }
        public float[] Position { get; set; } = new float[3];
        public float[] Rotation { get; set; } = new float[3];
        public int LinkedPartIndex { get; set; } = -1;
        public float Length { get; set; } = 0.1f;
        public BlurSaberPart.GeometryType GeometryMode { get; set; }
        public float HueShift { get; set; }

        public float StartRadius { get; set; }
        public float[] StartColor { get; set; } = new float[3];
        public float StartCustomWeight { get; set; } = 1f;
        public float StartGlow { get; set; } = 1f;
        public float StartOpacity { get; set; } = 1f;

        public float EndRadius { get; set; }
        public float[] EndColor { get; set; } = new float[3];
        public float EndCustomWeight { get; set; } = 1f;
        public float EndGlow { get; set; } = 1f;
        public float EndOpacity { get; set; } = 1f;

        public bool Inverted { get; set; }
        public bool Lit { get; set; }
        public float Blur { get; set; } = 1f;
        public float BlurFade { get; set; } = 1f;
        public bool EnableEndCaps { get; set; } = true;
        public bool EnableRoundedNormals { get; set; } = true;
        public float EndCapExtension { get; set; } = 0.25f;

        public float[] LookDir { get; set; } = new float[3];
        public bool UseLookDir { get; set; }

        public float BulgeAmount { get; set; }
        public int MinimumRings { get; set; } = 4;
        public int RenderQueueOffset { get; set; }
        public float DepthOffset { get; set; }

        public float RimFactor { get; set; }
        public float RimPower { get; set; } = 3f;
        public float RimPerpendicular { get; set; }

        public float SpecularStrength { get; set; } = 0.41f;
        public float SpecularPower { get; set; } = 48f;
        public float Metallic { get; set; }
        public float Smoothness { get; set; }
        public float CubemapStrength { get; set; } = 0.78f;
        public float CubemapRotation { get; set; }
        public float FresnelStrength { get; set; } = 0.6f;
        public float FresnelPower { get; set; } = 2.89f;
        public float[] RimColor { get; set; } = new float[3];

        public string? ColorTexture { get; set; }
        public string? GlowTexture { get; set; }
        public int TextureWrap { get; set; }

        public List<BlurPartAnimationModulator>? Animators { get; set; }

        public List<RingData>? Rings { get; set; }
    }

    private class RingData
    {
        public float Position { get; set; }
        public float Radius { get; set; }
        public float[] Color { get; set; } = new float[3];
        public float CustomWeight { get; set; }
        public float Glow { get; set; }
        public float Opacity { get; set; } = 1f;
        public bool Inverted { get; set; }
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
        public float UvOffset { get; set; }
    }

    private class TrailData
    {
        public float[] Position { get; set; } = new float[] { 0, 0, 1 };
        public float[] Color { get; set; } = new float[] { 1, 1, 1 };
        public float CustomBlend { get; set; } = 1f;
        public float Glow { get; set; } = 1f;
        public float Opacity { get; set; } = 1f;
        public float Width { get; set; } = 0.008f;
        public int Length { get; set; } = 140;
        public int QueueOffset { get; set; } = 0;
    }

    private static Vector3 ArrToVec3(float[] arr) =>
        arr.Length >= 3 ? new Vector3(arr[0], arr[1], arr[2]) : Vector3.zero;

    private static Color ArrToColor(float[] arr) =>
        arr.Length >= 3 ? new Color(arr[0], arr[1], arr[2], 1f) : Color.white;

    #endregion
}
