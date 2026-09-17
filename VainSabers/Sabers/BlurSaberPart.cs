using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Data;
using VainSabers.Helpers;

namespace VainSabers.Sabers
{
    // this class is getting too large lmao
    [ExecuteInEditMode]
    public class BlurSaberPart : MonoBehaviour
    {
public enum GeometryType
        {
            [Label("Simple")]
            Simple,
            [Label("Advanced")]
            Advanced,
            [Label("Sprite")]
            Sprite,
            [Label("Obj")]
            Obj
        }

        public enum SaberSide
        {
            Both,
            LeftOnly,
            RightOnly
        }

        private const int SampleCount = 32;
        private Pose[] m_poseSamples = new Pose[SampleCount];
        private int RingCount =>
            GeometryHandling == GeometryType.Advanced
                ? RingParams.Count
                : Math.Max((int)(Length * 8), MinimumRings) + (EnableEndCaps ? 2 : 0);
        public float RotX, RotY, RotZ;
        public Vector3 Position;
        
        public List<BlurPartAnimationModulator> Animators = new();
        
        public float Length;

        public GeometryType GeometryHandling = GeometryType.Simple;
        // for advanced geometry handling, just use these directly and ignore everything else
        public List<BlurSaberRingParams> RingParams = new();

        public int DivisionsX;
        public int DivisionsY;
        public float SizeX;
        public float SizeY;
        public bool DoubleSided;

        public float StartRadius;
        public float EndRadius;

        public Color StartColor = new Color(1, 0.7f, 0.2f, 1);
        public Color EndColor = new Color(0, 0.6f, 1.0f, 1);
        
        public float StartCustomColorWeight = 1;
        public float EndCustomColorWeight = 1;
        
        public float HueShift = 0f;
        
        public float StartGlow = 1;
        public float EndGlow = 1;
        
        public float StartOpacity = 1f;
        public float EndOpacity = 1f;

        public float DepthOffset = 0f;

        public bool DisableGlowPass;
        public bool DisableDepthPrepass;

        public bool Inverted;
        public bool Lit;

        public float BlurFactor = 1;
        public float BlurFadeFactor = 1;

        private float BlurTime => m_saberData != null ? m_saberData.BlurTime * BlurFactor : 0f;

        private float m_smoothedMotion;
        private float m_motionFactor;

        public bool EnableEndCaps = true;
        public bool EnableRoundedNormals = true;

        public bool ManualRingVerts = false;
        public int RingVertsManual = 20;

        public SaberSide Side = SaberSide.Both;

        public float EndCapExtension = 0.25f;

        public float BulgeAmount = 0.00f;
        public int MinimumRings = 4;

        public float RimFactor = 0;
        public float RimPower = 3; // legacy, kept for baking old presets
        public ColorGradient RimPowerGradient = CreateBakedRimGradient(0f, 3f);
        public float RimPerpendicular = 0;

        public static ColorGradient CreateBakedPowerGradient(float power, int keyCount = 8)
        {
            var g = new ColorGradient();
            power = Mathf.Max(power, 0.0001f);
            for (int i = 0; i < keyCount; i++)
            {
                float t = i / (float)(keyCount - 1);
                float v = Mathf.Pow(Mathf.Clamp01(t), power);
                g.Keys.Add(new ColorGradientKey(t, new Color(v, v, v, 1f)) { Easing = Easing.Linear });
            }
            g.SetDirty();
            return g;
        }

        public static ColorGradient CreateBakedRimGradient(float rimFactor, float rimPower, int keyCount = 8)
        {
            var g = new ColorGradient();
            rimPower = Mathf.Max(rimPower, 0.0001f);
            for (int i = 0; i < keyCount; i++)
            {
                float t = i / (float)(keyCount - 1);
                float v = rimFactor * Mathf.Pow(Mathf.Clamp01(t), rimPower);
                g.Keys.Add(new ColorGradientKey(t, new Color(v, v, v, 1f)) { Easing = Easing.Linear });
            }
            g.SetDirty();
            return g;
        }

        public float SpecularStrength = 0.41f;
        public float SpecularPower = 48f;
        public float Metallic = 0f;
        public float Smoothness = 0f;
        public float CubemapStrength = 0.78f;
        public float CubemapRotation = 0f;
        public float FresnelStrength = 0.6f;
        public float FresnelPower = 2.89f;
        public Color RimColor = new Color(0.47f, 0.51f, 0.57f, 1f);
        public float FresnelCustomBlend = 0f;

        public string? ColorTextureName;
        public string? GlowTextureName;
        public string? ColorTextureBase64;
        public string? GlowTextureBase64;
        public TextureWrapMode TextureWrap = TextureWrapMode.Clamp;

        public Vector2 ColorAtlasCount = new Vector2(1, 1);
        public Vector3 ColorAtlasSpeedFlip = new Vector3(1, 0, 0);
        public Vector2 GlowAtlasCount = new Vector2(1, 1);
        public Vector3 GlowAtlasSpeedFlip = new Vector3(1, 0, 0);

        public string? ObjFileName;
        public string? ObjBase64;
        public float ObjScale = 1f;

        public bool MirrorOnLeftSaber = false;
        
        public Vector3 LookDir = Vector3.zero;
        public bool UseLookDir = false;
        
        public int LinkedPartIndex = -1;
        
        public Material Material = null!;
        public Material InvertedMaterial = null!;
        public Material LitMaterial = null!;
        public Material LitInvertedMaterial = null!;
        
        public int RenderQueueOffset = 0;

        [FindComponent(ComponentLocation.InParent)]
        private MovementHistoryProvider m_movementHistoryProvider = null!;
        [FindComponent(ComponentLocation.InParent)]
        private BlurSaberData m_saberData = null!;
        
        [RequiredComponent]
        private MeshRenderer m_meshRenderer = null!;
        [RequiredComponent]
        private MeshFilter m_meshFilter = null!;
        
        private bool m_injected = false;
        private BlurSprite? m_blurSprite;
        private BlurObj? m_blurObj;
        private Vector3[] m_objWorldOffsets = new Vector3[0];

        private Mesh? m_vertexTubeMesh;
        private bool m_vertexTubeDirty = true;
        private int m_vertexLastRingVerts = -1;
        private int m_vertexLastRingCount = -1;
        private Vector4[] m_vertexHistPos = new Vector4[SampleCount];
        private Vector4[] m_vertexHistFwd = new Vector4[SampleCount];
        private Vector4[] m_vertexHistUp = new Vector4[SampleCount];
        private int m_lastVertexStaticHash = 0;
        private bool m_firstVertexHash = true;
        
        private Material? m_runtimeMaterial;
        private Material? m_runtimeInvertedMaterial;
        private Material? m_runtimeLitMaterial;
        private Material? m_runtimeLitInvertedMaterial;
        private MaterialPropertyBlock m_propertyBlock = null!;
        private AssetKeyCache m_colorTexKey = new();
        private AssetKeyCache m_glowTexKey = new();
        private AssetKeyCache m_objKey = new();
        
        public PluginConfig Config = null!;

        private static readonly Dictionary<string, Texture2D> s_loadedTextures = new();
        private static readonly Dictionary<string, ObjMeshData> s_loadedObjs = new();

        private static string ResolveAssetKey(string? base64, ref AssetKeyCache cache)
        {
            if (cache.Key != null && ReferenceEquals(cache.Base64, base64))
                return cache.Key;

            cache.Base64 = base64;
            cache.Key = string.IsNullOrEmpty(base64) ? "" : Fnv64(base64!);
            return cache.Key;
        }

        // wow thanks chatgpt:
        private static string Fnv64(string text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                for (var i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 1099511628211UL;
                }
                return hash.ToString("x16");
            }
        }

        internal struct AssetKeyCache
        {
            public string? Base64;
            public string? Key;
        }

        internal static ObjMeshData LoadObjData(string? fileName, string? embeddedBase64, ref AssetKeyCache keyCache)
        {
            if (string.IsNullOrEmpty(fileName) && string.IsNullOrEmpty(embeddedBase64))
                return new ObjMeshData();

            if (!string.IsNullOrEmpty(embeddedBase64))
            {
                var cacheKey = ResolveAssetKey(embeddedBase64, ref keyCache);
                if (cacheKey.Length > 0 && s_loadedObjs.TryGetValue(cacheKey, out var cached))
                    return cached;

                var data = OBJLoader.Load(fileName, embeddedBase64, cacheKey);
                if (data.Positions.Length > 0)
                    s_loadedObjs[cacheKey] = data;
                return data;
            }

            var fileKey = fileName + "|" + System.IO.File.GetLastWriteTimeUtc(System.IO.Path.Combine(ConfigUtil.ConfigDir, fileName!)).Ticks;
            if (s_loadedObjs.TryGetValue(fileKey, out var cachedFile))
                return cachedFile;

            var fileData = OBJLoader.Load(fileName, embeddedBase64, fileKey);
            if (fileData.Positions.Length > 0)
                s_loadedObjs[fileKey] = fileData;
            return fileData;
        }

        internal static Texture2D? LoadTexture(string? fileName, TextureWrapMode wrapMode, string? embeddedBase64, ref AssetKeyCache keyCache)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            var assetKey = ResolveAssetKey(embeddedBase64, ref keyCache);
            var cacheKey = assetKey.Length > 0
                ? $"{fileName}|{(int)wrapMode}|{assetKey}"
                : $"{fileName}|{(int)wrapMode}";

            if (s_loadedTextures.TryGetValue(cacheKey, out var tex))
                return tex;

            byte[]? data;
            if (!string.IsNullOrEmpty(embeddedBase64))
            {
                try
                {
                    data = Convert.FromBase64String(embeddedBase64!);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Warn($"Failed to decode embedded asset '{fileName}': {ex.Message}");
                    return null;
                }
            }
            else
            {
                var path = Path.Combine(ConfigUtil.ConfigDir, fileName!);
                if (!File.Exists(path))
                    return null;
                data = File.ReadAllBytes(path);
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (texture.LoadImage(data))
            {
                texture.wrapMode = wrapMode;
                texture.filterMode = FilterMode.Trilinear;
                s_loadedTextures[cacheKey] = texture;
                return texture;
            }

            DestroyImmediate(texture);
            return null;
        }

        private void OnEnable()
        {
            m_injected = false;
        }
        
        private void OnDisable()
        {
            m_injected = false;
        }

        private void OnValidate()
        {
            m_vertexTubeDirty = true;
        }
        
        int ComputeRingVerts(float radius)
        {
            if (ManualRingVerts)
                return Mathf.Clamp(RingVertsManual, 4, 20);

            return Mathf.Clamp(
                Mathf.RoundToInt(Config.SaberQuality * Mathf.Lerp(6, 36, Mathf.InverseLerp(0.0f, 0.02f, radius))),
                6, 36
            );
        }

        private bool ShouldRenderOnCurrentSaber()
        {
            if (Side == SaberSide.Both)
                return true;

            bool isLeftSaber = m_saberData?.IsLeftSaber ?? false;
            
            if (isLeftSaber)
                return Side == SaberSide.LeftOnly;
            else
                return Side == SaberSide.RightOnly;
        }
        
        private float GetProfileRadiusForRingVerts()
        {
            if (GeometryHandling != GeometryType.Advanced)
                return Mathf.Max(StartRadius, EndRadius);

            var max = 0.05f;
            for (var i = 0; i < RingParams.Count; i++)
                max = Mathf.Max(max, Mathf.Abs(RingParams[i].Radius));
            return max;
        }

        private bool ShouldUseVertexBlur()
        {
            return GeometryHandling == GeometryType.Simple || GeometryHandling == GeometryType.Advanced;
        }

        private int ComputeVertexStaticHash()
        {
            var h = new HashCode();
            h.Add((int)GeometryHandling);
            h.Add(Length);
            h.Add(StartRadius);
            h.Add(EndRadius);
            h.Add(StartColor.r); h.Add(StartColor.g); h.Add(StartColor.b);
            h.Add(EndColor.r); h.Add(EndColor.g); h.Add(EndColor.b);
            h.Add(StartCustomColorWeight);
            h.Add(EndCustomColorWeight);
            h.Add(StartGlow);
            h.Add(EndGlow);
            h.Add(StartOpacity);
            h.Add(EndOpacity);
            h.Add(BulgeAmount);
            h.Add(EnableEndCaps);
            h.Add(EndCapExtension);
            h.Add(Inverted);
            h.Add(MinimumRings);
            h.Add(ManualRingVerts);
            h.Add(RingVertsManual);
            h.Add(Config != null ? Config.SaberQuality : 1f);
            h.Add(GetProfileRadiusForRingVerts());
            if (GeometryHandling == GeometryType.Advanced)
            {
                h.Add(RingParams.Count);
                for (int i = 0; i < RingParams.Count; i++)
                {
                    var r = RingParams[i];
                    h.Add(r.PosAlongPart01);
                    h.Add(r.Radius);
                    h.Add(r.Color.r); h.Add(r.Color.g); h.Add(r.Color.b);
                    h.Add(r.CustomWeight);
                    h.Add(r.Glow);
                    h.Add(r.Opacity);
                    h.Add(r.Inverted);
                    h.Add(r.Offset.x); h.Add(r.Offset.y);
                    h.Add(r.UvOffset);
                }
            }
            return h.ToHashCode();
        }

        private bool CheckVertexStaticDirty()
        {
            int cur = ComputeVertexStaticHash();
            if (m_firstVertexHash || cur != m_lastVertexStaticHash)
            {
                m_firstVertexHash = false;
                m_lastVertexStaticHash = cur;
                return true;
            }
            return false;
        }

        private void EnsureVertexTubeMesh()
        {
            if (!ShouldUseVertexBlur()) return;
            if (CheckVertexStaticDirty())
                m_vertexTubeDirty = true;
            int ringCount = RingCount;
            if (ringCount < 2) return;
            int ringVerts = ComputeRingVerts(GetProfileRadiusForRingVerts());
            if (m_vertexTubeMesh != null && !m_vertexTubeDirty && m_vertexLastRingVerts == ringVerts && m_vertexLastRingCount == ringCount)
                return;

            if (m_vertexTubeMesh != null)
            {
                DestroyImmediate(m_vertexTubeMesh);
                m_vertexTubeMesh = null;
            }

            if (GeometryHandling == GeometryType.Advanced)
            {
                m_vertexTubeMesh = GpuBlurMeshBuilder.BuildGpuTube(ringVerts, ringCount,
                    r => RingParams[r].PosAlongPart01 * Length,
                    r => RingParams[r].Offset.x,
                    r => RingParams[r].Offset.y,
                    r => RingParams[r].Inverted ? -RingParams[r].Radius : RingParams[r].Radius,
                    r => {
                        var isZero = Mathf.Abs(RingParams[r].Radius) < 0.0002f;
                        if (isZero || RingParams.Count <= 1 || Length < 0.0001f) return 0f;
                        var prev = RingParams[(r - 1 + RingParams.Count) % RingParams.Count];
                        var next = RingParams[(r + 1) % RingParams.Count];
                        float prevRad = prev.Inverted ? -prev.Radius : prev.Radius;
                        float nextRad = next.Inverted ? -next.Radius : next.Radius;
                        float curT = RingParams[r].PosAlongPart01;
                        float dtPrev = curT - prev.PosAlongPart01; if (dtPrev <= 0f) dtPrev += 1f;
                        float dtNext = next.PosAlongPart01 - curT; if (dtNext <= 0f) dtNext += 1f;
                        float dt = dtPrev + dtNext;
                        return dt > 0.0001f ? (nextRad - prevRad) / (dt * Length) : 0f;
                    },
                    r => Mathf.Abs(RingParams[r].Radius) < 0.0002f,
                    r => RingParams[r].PosAlongPart01,
                    r => RingParams[r].Color,
                    r => RingParams[r].Glow,
                    r => RingParams[r].CustomWeight,
                    r => RingParams[r].Opacity,
                    r => RingParams[r].UvOffset);
            }
            else
            {
                int mainCount = EnableEndCaps ? ringCount - 2 : ringCount;
                float startRad = Inverted ? -StartRadius : StartRadius;
                float endRad = Inverted ? -EndRadius : EndRadius;
                m_vertexTubeMesh = GpuBlurMeshBuilder.BuildGpuTube(ringVerts, ringCount,
                    r => {
                        if (EnableEndCaps)
                        {
                            if (r == 0) return 0 - StartRadius * 0.25f * EndCapExtension;
                            if (r == ringCount - 1) return Length + EndRadius * 0.25f * EndCapExtension;
                            int mi = r - 1;
                            float t = mainCount > 1 ? (float)mi / (mainCount - 1f) : 0f;
                            return t * Length;
                        }
                        float tt = ringCount > 1 ? (float)r / (ringCount - 1f) : 0f;
                        return tt * Length;
                    },
                    r => 0f, r => 0f,
                    r => {
                        if (EnableEndCaps)
                        {
                            if (r == 0) return startRad;
                            if (r == ringCount - 1) return endRad;
                            int mi = r - 1;
                            float t = mainCount > 1 ? (float)mi / (mainCount - 1f) : 0f;
                            float lin = Mathf.Lerp(startRad, endRad, t);
                            float bulge = 1 + 4 * (t - t * t) * BulgeAmount;
                            return lin * bulge;
                        }
                        float tt = ringCount > 1 ? (float)r / (ringCount - 1f) : 0f;
                        float lin2 = Mathf.Lerp(startRad, endRad, tt);
                        float bulge2 = 1 + 4 * (tt - tt * tt) * BulgeAmount;
                        return lin2 * bulge2;
                    },
                    r => {
                        if (EnableEndCaps && (r == 0 || r == ringCount - 1)) return 0f;
                        int mi = EnableEndCaps ? r - 1 : r;
                        int cnt = EnableEndCaps ? mainCount : ringCount;
                        float t = cnt > 1 ? (float)mi / (cnt - 1f) : 0f;
                        float dLin = endRad - startRad;
                        float dBulge_dt = 4 * (1 - 2 * t) * BulgeAmount;
                        float lin = Mathf.Lerp(startRad, endRad, t);
                        float bulge = 1 + 4 * (t - t * t) * BulgeAmount;
                        float dRad_dt = dLin * bulge + lin * dBulge_dt;
                        return Length > 0.0001f ? dRad_dt / Length : 0f;
                    },
                    r => {
                        if (EnableEndCaps && (r == 0 || r == ringCount - 1)) return true;
                        float rad;
                        if (EnableEndCaps)
                        {
                            int mi = r - 1;
                            float t = mainCount > 1 ? (float)mi / (mainCount - 1f) : 0f;
                            float lin = Mathf.Lerp(startRad, endRad, t);
                            float bulge = 1 + 4 * (t - t * t) * BulgeAmount;
                            rad = lin * bulge;
                        }
                        else
                        {
                            float tt = ringCount > 1 ? (float)r / (ringCount - 1f) : 0f;
                            float lin = Mathf.Lerp(startRad, endRad, tt);
                            float bulge = 1 + 4 * (tt - tt * tt) * BulgeAmount;
                            rad = lin * bulge;
                        }
                        return Mathf.Abs(rad) < 0.0002f;
                    },
                    r => {
                        if (EnableEndCaps)
                        {
                            if (r == 0) return 0f;
                            if (r == ringCount - 1) return 1f;
                            int mi = r - 1;
                            return mainCount > 1 ? (float)mi / (mainCount - 1f) : 0f;
                        }
                        return ringCount > 1 ? (float)r / (ringCount - 1f) : 0f;
                    },
                    r => {
                        float t;
                        if (EnableEndCaps)
                        {
                            if (r == 0) return StartColor;
                            if (r == ringCount - 1) return EndColor;
                            int mi = r - 1;
                            t = mainCount > 1 ? (float)mi / (mainCount - 1f) : 0f;
                        }
                        else t = ringCount > 1 ? (float)r / (ringCount - 1f) : 0f;
                        return Color.Lerp(StartColor, EndColor, t);
                    },
                    r => {
                        float t;
                        if (EnableEndCaps)
                        {
                            if (r == 0) return StartGlow;
                            if (r == ringCount - 1) return EndGlow;
                            int mi = r - 1;
                            t = mainCount > 1 ? (float)mi / (mainCount - 1f) : 0f;
                        }
                        else t = ringCount > 1 ? (float)r / (ringCount - 1f) : 0f;
                        return Mathf.Lerp(StartGlow, EndGlow, t);
                    },
                    r => {
                        float t;
                        if (EnableEndCaps)
                        {
                            if (r == 0) return StartCustomColorWeight;
                            if (r == ringCount - 1) return EndCustomColorWeight;
                            int mi = r - 1;
                            t = mainCount > 1 ? (float)mi / (mainCount - 1f) : 0f;
                        }
                        else t = ringCount > 1 ? (float)r / (ringCount - 1f) : 0f;
                        return Mathf.Lerp(StartCustomColorWeight, EndCustomColorWeight, t);
                    },
                    r => {
                        float t;
                        if (EnableEndCaps)
                        {
                            if (r == 0) return StartOpacity;
                            if (r == ringCount - 1) return EndOpacity;
                            int mi = r - 1;
                            t = mainCount > 1 ? (float)mi / (mainCount - 1f) : 0f;
                        }
                        else t = ringCount > 1 ? (float)r / (ringCount - 1f) : 0f;
                        return Mathf.Lerp(StartOpacity, EndOpacity, t);
                    },
                    r => 0f);
            }

            m_vertexLastRingVerts = ringVerts;
            m_vertexLastRingCount = ringCount;
            m_vertexTubeDirty = false;
        }

        private void SampleGpuHistory()
        {
            if (m_movementHistoryProvider == null || m_saberData == null) return;
            var localPose = transform.GetPose().TransformPose(m_movementHistoryProvider.transform.worldToLocalMatrix);
            var samples = InterpolateData(BlurTime);
            var localPoseMat = localPose.AsMatrix();
            var wtl = transform.worldToLocalMatrix;
            for (int i = 0; i < samples.Length; i++)
            {
                var combined = wtl * samples[i].AsMatrix() * localPoseMat;
                samples[i] = PoseHelpers.TransformPoseFromMatrix(combined);
                m_vertexHistPos[i] = new Vector4(samples[i].position.x, samples[i].position.y, samples[i].position.z, 1f);
                m_vertexHistFwd[i] = new Vector4(samples[i].forward.x, samples[i].forward.y, samples[i].forward.z, 0f);
                m_vertexHistUp[i] = new Vector4(samples[i].up.x, samples[i].up.y, samples[i].up.z, 0f);
            }
        }

        private void Start()
        {
            if (UseLookDir)
            {
                transform.localRotation = Quaternion.LookRotation(LookDir);
            }
        }

        public void CopyVisualPropertiesFrom(BlurSaberPart source)
        {
            Animators = source.Animators;
            Length = source.Length;
            GeometryHandling = source.GeometryHandling;
            
            DivisionsX = source.DivisionsX;
            DivisionsY = source.DivisionsY;
            SizeX = source.SizeX;
            SizeY = source.SizeY;
            DoubleSided = source.DoubleSided;
            
            RingParams = new List<BlurSaberRingParams>(source.RingParams);

            StartRadius = source.StartRadius;
            EndRadius = source.EndRadius;
            StartColor = source.StartColor;
            EndColor = source.EndColor;
            StartCustomColorWeight = source.StartCustomColorWeight;
            EndCustomColorWeight = source.EndCustomColorWeight;
            HueShift = source.HueShift;
            StartGlow = source.StartGlow;
            EndGlow = source.EndGlow;
            StartOpacity = source.StartOpacity;
            EndOpacity = source.EndOpacity;
            DepthOffset = source.DepthOffset;
            DisableGlowPass = source.DisableGlowPass;
            DisableDepthPrepass = source.DisableDepthPrepass;
            Inverted = source.Inverted;
            Lit = source.Lit;
            BlurFactor = source.BlurFactor;
            BlurFadeFactor = source.BlurFadeFactor;
            EnableEndCaps = source.EnableEndCaps;
            EndCapExtension = source.EndCapExtension;
            BulgeAmount = source.BulgeAmount;
            MinimumRings = source.MinimumRings;
            ManualRingVerts = source.ManualRingVerts;
            RingVertsManual = source.RingVertsManual;
            Side = source.Side;
            EnableRoundedNormals = source.EnableRoundedNormals;
            RimFactor = source.RimFactor;
            RimPower = source.RimPower;
            // deep copy gradient into existing instance to preserve references held by UI
            if (RimPowerGradient == null) RimPowerGradient = new ColorGradient();
            RimPowerGradient.SetFloatKeys(source.RimPowerGradient.GetFloatKeys());
            RimPerpendicular = source.RimPerpendicular;
            SpecularStrength = source.SpecularStrength;
            SpecularPower = source.SpecularPower;
            Metallic = source.Metallic;
            Smoothness = source.Smoothness;
            CubemapStrength = source.CubemapStrength;
            CubemapRotation = source.CubemapRotation;
            FresnelStrength = source.FresnelStrength;
            FresnelPower = source.FresnelPower;
            RimColor = source.RimColor;
            FresnelCustomBlend = source.FresnelCustomBlend;
            ColorTextureName = source.ColorTextureName;
            GlowTextureName = source.GlowTextureName;
            ColorTextureBase64 = source.ColorTextureBase64;
            GlowTextureBase64 = source.GlowTextureBase64;
            TextureWrap = source.TextureWrap;
            ColorAtlasCount = source.ColorAtlasCount;
            ColorAtlasSpeedFlip = source.ColorAtlasSpeedFlip;
            GlowAtlasCount = source.GlowAtlasCount;
            GlowAtlasSpeedFlip = source.GlowAtlasSpeedFlip;
            ObjFileName = source.ObjFileName;
            ObjBase64 = source.ObjBase64;
            ObjScale = source.ObjScale;
            LookDir = source.LookDir;
            UseLookDir = source.UseLookDir;
            Material = source.Material;
            InvertedMaterial = source.InvertedMaterial;
            LitMaterial = source.LitMaterial;
            LitInvertedMaterial = source.LitInvertedMaterial;
            RenderQueueOffset = source.RenderQueueOffset;
            m_vertexTubeDirty = true;
        }
        
        private void ApplyMaterialProps()
        {
            EnsureRuntimeMaterial(ref m_runtimeMaterial, Material);
            EnsureRuntimeMaterial(ref m_runtimeInvertedMaterial, InvertedMaterial);
            EnsureRuntimeMaterial(ref m_runtimeLitMaterial, LitMaterial);
            EnsureRuntimeMaterial(ref m_runtimeLitInvertedMaterial, LitInvertedMaterial);

            var activeMat = GetActiveMaterial();
            if (activeMat != null)
            {
                activeMat.renderQueue = 3600 + RenderQueueOffset;

                if (DisableGlowPass) activeMat.EnableKeyword("_DISABLE_GLOW_PASS");
                else activeMat.DisableKeyword("_DISABLE_GLOW_PASS");
                if (DisableDepthPrepass) activeMat.EnableKeyword("_DISABLE_DEPTH_PREPASS");
                else activeMat.DisableKeyword("_DISABLE_DEPTH_PREPASS");

                m_propertyBlock ??= new MaterialPropertyBlock();
                m_propertyBlock.SetFloat("_DepthOffset", DepthOffset + (Inverted ? 0f : 0.001f));

                // RimFactor is now baked into RimPowerGradient (-3..3), direct sample is used in shader
                m_propertyBlock.SetTexture("_RimPowerGradient", RimPowerGradient.GetGradientTexture());
                m_propertyBlock.SetFloat("_RimPerpendicular", RimPerpendicular);

                // _BlurPartIsBlade: 1 for blade geometry (Simple/Advanced tubes), 0 for images/obj (Sprite/Obj)
                m_propertyBlock.SetFloat("_BlurPartIsBlade",
                    (GeometryHandling == GeometryType.Simple || GeometryHandling == GeometryType.Advanced) ? 1f : 0f);

                m_propertyBlock.SetFloat("_SpecularStrength", SpecularStrength);
                m_propertyBlock.SetFloat("_SpecularPower", SpecularPower);
                m_propertyBlock.SetFloat("_Metallic", Metallic);
                m_propertyBlock.SetFloat("_Smoothness", Smoothness);
                m_propertyBlock.SetFloat("_CubemapStrength", CubemapStrength);
                m_propertyBlock.SetFloat("_CubemapRotation", CubemapRotation);
                m_propertyBlock.SetFloat("_FresnelStrength", FresnelStrength);
                m_propertyBlock.SetFloat("_FresnelPower", FresnelPower);
                var fresnelColor = Color.Lerp(RimColor, m_saberData.CustomColor, Mathf.Clamp01(FresnelCustomBlend));
                m_propertyBlock.SetColor("_RimColor", fresnelColor);

                var colorTex = LoadTexture(ColorTextureName, TextureWrap, ColorTextureBase64, ref m_colorTexKey);
                var glowTex = LoadTexture(GlowTextureName, TextureWrap, GlowTextureBase64, ref m_glowTexKey);
                if (colorTex != null) m_propertyBlock.SetTexture("_ColorTex", colorTex);
                else m_propertyBlock.SetTexture("_ColorTex", Texture2D.whiteTexture);
                if (glowTex != null) m_propertyBlock.SetTexture("_GlowTex", glowTex);
                else m_propertyBlock.SetTexture("_GlowTex", Texture2D.whiteTexture);

                m_propertyBlock.SetFloat("_ColorTexEnabled", colorTex != null ? 1f : 0f);
                m_propertyBlock.SetFloat("_GlowTexEnabled", glowTex != null ? 1f : 0f);

                // compact atlas: float2 count, float3 speed+flips
                var cCount = new Vector4(Mathf.Max(1, ColorAtlasCount.x), Mathf.Max(1, ColorAtlasCount.y), 0, 0);
                var cAnim = new Vector4(Mathf.Clamp(ColorAtlasSpeedFlip.x, 0f, 120f), ColorAtlasSpeedFlip.y > 0.5f ? 1f : 0f, ColorAtlasSpeedFlip.z > 0.5f ? 1f : 0f, 0);
                var gCount = new Vector4(Mathf.Max(1, GlowAtlasCount.x), Mathf.Max(1, GlowAtlasCount.y), 0, 0);
                var gAnim = new Vector4(Mathf.Clamp(GlowAtlasSpeedFlip.x, 0f, 120f), GlowAtlasSpeedFlip.y > 0.5f ? 1f : 0f, GlowAtlasSpeedFlip.z > 0.5f ? 1f : 0f, 0);
                m_propertyBlock.SetVector("_ColorTexAtlasCount", cCount);
                m_propertyBlock.SetVector("_ColorTexAtlasSpeedFlip", cAnim);
                m_propertyBlock.SetVector("_GlowTexAtlasCount", gCount);
                m_propertyBlock.SetVector("_GlowTexAtlasSpeedFlip", gAnim);
                // keep legacy floats for fallback (optional, not needed after bundle rebuild)
                m_propertyBlock.SetFloat("_ColorTexAtlasX", cCount.x);
                m_propertyBlock.SetFloat("_ColorTexAtlasY", cCount.y);
                m_propertyBlock.SetFloat("_ColorTexAtlasSpeed", cAnim.x);
                m_propertyBlock.SetFloat("_ColorTexAtlasFlipX", cAnim.y);
                m_propertyBlock.SetFloat("_ColorTexAtlasFlipY", cAnim.z);
                m_propertyBlock.SetFloat("_GlowTexAtlasX", gCount.x);
                m_propertyBlock.SetFloat("_GlowTexAtlasY", gCount.y);
                m_propertyBlock.SetFloat("_GlowTexAtlasSpeed", gAnim.x);
                m_propertyBlock.SetFloat("_GlowTexAtlasFlipX", gAnim.y);
                m_propertyBlock.SetFloat("_GlowTexAtlasFlipY", gAnim.z);

                bool useGpu = ShouldUseVertexBlur();
                m_propertyBlock.SetFloat("_VertexEnabled", useGpu ? 1f : 0f);
                if (useGpu)
                {
                    SampleGpuHistory();
                    m_propertyBlock.SetVectorArray("_HistPos", m_vertexHistPos);
                    m_propertyBlock.SetVectorArray("_HistFwd", m_vertexHistFwd);
                    m_propertyBlock.SetVectorArray("_HistUp", m_vertexHistUp);
                    m_propertyBlock.SetInt("_HistCount", SampleCount);
                    m_propertyBlock.SetFloat("_VertexBlurFade", BlurFadeFactor);
                    m_propertyBlock.SetFloat("_VertexHueShift", m_modulatableParams.HueShift);
                    var cc = m_saberData != null ? m_saberData.CustomColor : Color.white;
                    m_propertyBlock.SetVector("_VertexCustomColor", new Vector4(cc.r, cc.g, cc.b, 1f));
                    m_propertyBlock.SetFloat("_VertexGlowMul", m_modulatableParams.GlowMultiplier);
                    m_propertyBlock.SetFloat("_VertexOpacityMul", m_modulatableParams.OpacityMultiplier);
                    m_propertyBlock.SetFloat("_VertexEnableRoundedNormals", EnableRoundedNormals ? 1f : 0f);
                    m_propertyBlock.SetFloat("_VertexLength", Length);
                    m_propertyBlock.SetFloat("_VertexGeometry", 0f);
                }

                m_meshRenderer.SetPropertyBlock(m_propertyBlock);
            }
            m_meshRenderer.sharedMaterial = activeMat;
            m_meshRenderer.sortingOrder = 100;
        }

        void LateUpdate()
        {
            if (!this.Inject(ref m_injected))
            {
                m_blurSprite?.Destroy();
                m_blurObj?.Destroy();
                if (m_vertexTubeMesh != null) { DestroyImmediate(m_vertexTubeMesh); m_vertexTubeMesh = null; }
                m_blurSprite = null;
                m_blurObj = null;
                m_vertexTubeDirty = true;
                return;
            }

            if (!ShouldRenderOnCurrentSaber())
            {
                m_blurSprite?.Destroy();
                m_blurObj?.Destroy();
                if (m_vertexTubeMesh != null) { DestroyImmediate(m_vertexTubeMesh); m_vertexTubeMesh = null; }
                m_blurSprite = null;
                m_blurObj = null;
                m_vertexTubeDirty = true;
                m_meshFilter.mesh = null;
                return;
            }

            if (LinkedPartIndex >= 0 && LinkedPartIndex < m_saberData.ComponentCount)
            {
                var source = m_saberData.Components[LinkedPartIndex];
                if (source != null && source != this)
                    CopyVisualPropertiesFrom(source);
            }

            if (GeometryHandling == GeometryType.Obj)
            {
                if (m_blurSprite != null)
                {
                    m_blurSprite.Destroy();
                    m_blurSprite = null;
                }

                var objData = LoadObjData(ObjFileName, ObjBase64, ref m_objKey);
                if (objData.Positions.Length == 0)
                {
                    m_blurObj?.Destroy();
                    m_blurObj = null;
                    m_meshFilter.mesh = null;
                    return;
                }

                if (m_blurObj == null || m_blurObj.CacheKey != objData.CacheKey)
                {
                    m_blurObj?.Destroy();
                    m_blurObj = new BlurObj(objData);
                }

                ApplyMaterialProps();

                m_meshFilter.mesh = m_blurObj.ObjMesh;

                RebuildVerts();
                m_blurObj.RefreshMesh();
                return;
            }

            if (GeometryHandling == GeometryType.Sprite)
            {
                m_blurObj?.Destroy();
                m_blurObj = null;

                int divX = Mathf.Max(1, DivisionsX);
                int divY = Mathf.Max(1, DivisionsY);

                if (m_blurSprite == null || m_blurSprite.DivisionsX != divX || m_blurSprite.DivisionsY != divY || m_blurSprite.DoubleSided != DoubleSided)
                {
                    m_blurSprite?.Destroy();
                    m_blurSprite = new BlurSprite(divX, divY, DoubleSided);
                }

                ApplyMaterialProps();

                m_meshFilter.mesh = m_blurSprite.SpriteMesh;

                RebuildVerts();
                m_blurSprite.RefreshMesh();
                return;
            }
            
            if (m_blurSprite != null)
            {
                m_blurSprite.Destroy();
                m_blurSprite = null;
            }
            m_blurObj?.Destroy();
            m_blurObj = null;

            var ringCountVertex = RingCount;
            if (ringCountVertex < 2)
            {
                if (m_vertexTubeMesh != null) { DestroyImmediate(m_vertexTubeMesh); m_vertexTubeMesh = null; }
                m_meshFilter.mesh = null;
                return;
            }

            EnsureVertexTubeMesh();
            ApplyMaterialProps();
            m_meshFilter.mesh = m_vertexTubeMesh;
            return;
        }

        private BlurPartAnimationModulatableParams m_modulatableParams = new();

        private void Update()
        {
            UpdateMotion();

            m_modulatableParams.Position = Position;
            m_modulatableParams.RotationEuler = new Vector3(RotX, RotY, RotZ);
            m_modulatableParams.HueShift = HueShift;
            m_modulatableParams.OpacityMultiplier = 1.0f;
            m_modulatableParams.GlowMultiplier = 1.0f;
            
            m_modulatableParams.Motion = m_motionFactor * 0.3f;
            m_modulatableParams.Motion *= m_modulatableParams.Motion;
            m_modulatableParams.MotionSpeed = m_smoothedMotion * 0.3f;
            m_modulatableParams.MotionSpeed *= m_modulatableParams.MotionSpeed;

            var modulators = Animators;
            var saberTimeAlive = m_saberData != null ? m_saberData.SaberTimeAlive : Time.unscaledTime;
            for (var i = 0; i < modulators.Count; i++)
            {
                var modulator = modulators[i];
                modulator.Apply(m_modulatableParams, saberTimeAlive);
            }

            var pos = m_modulatableParams.Position;
            var rot = m_modulatableParams.RotationEuler;
            if (MirrorOnLeftSaber && m_saberData is { IsLeftSaber: true })
            {
                pos.x *= -1;
                rot.y *= -1;
                rot.z *= -1;
            }

            transform.localPosition = pos;
            transform.localEulerAngles = rot;
        }

        public void ResetMotion()
        {
            m_smoothedMotion = 0f;
            m_motionFactor = 0f;
        }

        private void UpdateMotion()
        {
            if (m_saberData == null || m_movementHistoryProvider == null)
                return;

            var present = m_movementHistoryProvider.GetPoseAgo(0.0f);
            var past = m_movementHistoryProvider.GetPoseAgo(BlurTime);

            var rawMotion = Vector3.Angle(present.forward, past.forward) + 40 * Vector3.Distance(present.position, past.position);
            rawMotion *= 0.38f;
            float dt = Time.deltaTime;
            float attack = 1f - Mathf.Exp(-350f * dt);
            float release = 1f - Mathf.Exp(-70.5f * dt);
            m_smoothedMotion = Mathf.Lerp(m_smoothedMotion, rawMotion, rawMotion > m_smoothedMotion ? attack : release);

            float targetFactor = Mathf.Clamp01(Mathf.InverseLerp(0.15f, 4f, m_smoothedMotion));
            targetFactor = targetFactor * targetFactor * (3f - 2f * targetFactor);

            float smoothRate = dt * (targetFactor > m_motionFactor ? 5f : 1.2f);
            m_motionFactor = Mathf.MoveTowards(m_motionFactor, targetFactor, smoothRate);
        }

        private void EnsureRuntimeMaterial(ref Material? runtimeMaterial, Material baseMaterial)
        {
            if (baseMaterial != null && (runtimeMaterial == null || runtimeMaterial.name != baseMaterial.name + " (Instance)"))
            {
                if (runtimeMaterial != null) DestroyImmediate(runtimeMaterial);
                runtimeMaterial = Instantiate(baseMaterial);
                runtimeMaterial.name = baseMaterial.name + " (Instance)";
            }
        }

        private Material? GetActiveMaterial()
        {
            if (Lit)
            {
                return Inverted ? m_runtimeLitInvertedMaterial : m_runtimeLitMaterial;
            }
            else
            {
                return Inverted ? m_runtimeInvertedMaterial : m_runtimeMaterial;
            }
        }

        private Material GetBaseMaterial()
        {
            if (Lit)
            {
                return Inverted ? LitInvertedMaterial : LitMaterial;
            }
            else
            {
                return Inverted ? InvertedMaterial : Material;
            }
        }

        private void OnDestroy()
        {
            m_blurSprite?.Destroy();
            m_blurObj?.Destroy();
            m_blurSprite = null;
            m_blurObj = null;
            if (m_vertexTubeMesh != null) { DestroyImmediate(m_vertexTubeMesh); m_vertexTubeMesh = null; }
            m_vertexTubeDirty = true;

            if (m_runtimeMaterial != null) DestroyImmediate(m_runtimeMaterial);
            if (m_runtimeInvertedMaterial != null) DestroyImmediate(m_runtimeInvertedMaterial);
            if (m_runtimeLitMaterial != null) DestroyImmediate(m_runtimeLitMaterial);
            if (m_runtimeLitInvertedMaterial != null) DestroyImmediate(m_runtimeLitInvertedMaterial);
        }

        void RebuildVerts()
        {
            var localPose =
                transform
                    .GetPose()
                    .TransformPose(m_movementHistoryProvider.transform.worldToLocalMatrix);

            var samples = InterpolateData(BlurTime);
            
            var localPoseMat = localPose.AsMatrix();
            var wtl = transform.worldToLocalMatrix;
            
            for (var i = 0; i < samples.Length; i++)
            {
                var combined =
                    wtl *
                    samples[i].AsMatrix() *
                    localPoseMat;

                samples[i] = PoseHelpers.TransformPoseFromMatrix(combined);
            }
            
            if (GeometryHandling == GeometryType.Sprite)
            {
                BuildSprite(samples);
                return;
            }

            if (GeometryHandling == GeometryType.Obj)
            {
                BuildObj(samples);
                return;
            }

            return;
        }
        
        Pose SampleAlongCurve(Pose[] samples, float t)
        {
            if (samples.Length == 0)
                return new Pose();
    
            t = Mathf.Clamp01(t);
            var idx = Mathf.FloorToInt(t * (samples.Length - 1));
    
            return samples[idx];
        }
        
        // Sprites are built by subdividing a rectangle and smearing it based on dot-ing the movement vector with the
        // vertices, very similar to the rings. It has special handling because it's not the surface of a 3d shape,
        // rather the full area of a 2d shape. to make sure the blur at least somewhat visually connects with the previous
        // and future frames, some trickery is pulled in tilting the plane beforehand to make it more coplanar with the
        // movement vector, so that the smear is almost as wide as can be.
        void BuildSprite(Pose[] samples)
        {
            if (m_blurSprite == null) return;
            if (samples.Length < 2) return;

            var first = samples[0];
            var last = samples[samples.Length - 1];

            Vector3 motionVec = last.position - first.position;
            float dst = motionVec.magnitude;
            Vector3 motionDir = dst > 0.0001f ? motionVec / dst : Vector3.forward;
            Vector3 avgRight = (first.right + last.right).normalized;
            Vector3 avgUp = (first.up + last.up).normalized;

            var col = Color.Lerp(StartColor, m_saberData.CustomColor, StartCustomColorWeight);
            if (Mathf.Abs(m_modulatableParams.HueShift) > 0.001f)
                col = ShiftHue(col, m_modulatableParams.HueShift);
            col.a = StartGlow * m_modulatableParams.GlowMultiplier;
            float opacity = StartOpacity * m_modulatableParams.OpacityMultiplier;
            float sweepRatio = Config.BlurSoftness * dst * 50.0f;
            sweepRatio = Mathf.Clamp((sweepRatio * BlurFadeFactor - 0.7f) * 0.01f, 0.0f, 5.0f);
            
            float bendAmount = Mathf.Clamp01(sweepRatio);
            Vector3 bentRight = Vector3.Slerp(avgRight, motionDir, bendAmount);
            if (bentRight.sqrMagnitude < 0.0001f) bentRight = avgRight;
            bentRight.Normalize();
            
            Vector3 bentUp = Vector3.ProjectOnPlane(avgUp, bentRight);
            if (bentUp.sqrMagnitude < 0.0001f)
                bentUp = Vector3.ProjectOnPlane(motionDir, bentRight).sqrMagnitude > 0.0001f
                    ? Vector3.Cross(bentRight, Vector3.Cross(avgUp, bentRight))
                    : avgUp;
            bentUp.Normalize();
            
            Vector3 planeNormal = Vector3.Cross(bentRight, bentUp).normalized;
            if (planeNormal.sqrMagnitude < 0.001f)
                planeNormal = Vector3.Cross(motionDir, Vector3.forward).normalized;

            int vertsX = DivisionsX + 1;
            int vertsY = DivisionsY + 1;

            float halfX = SizeX * 0.5f;
            float halfY = SizeY * 0.5f;
            
            Vector3[] corners =
            {
                bentRight * -halfX + bentUp * -halfY,
                bentRight *  halfX + bentUp * -halfY,
                bentRight * -halfX + bentUp *  halfY,
                bentRight *  halfX + bentUp *  halfY,
            };

            float minDot = float.MaxValue, maxDot = float.MinValue;
            for (int i = 0; i < corners.Length; i++)
            {
                float d = Vector3.Dot(corners[i], motionDir);
                if (d < minDot) minDot = d;
                if (d > maxDot) maxDot = d;
            }
            if (Mathf.Approximately(minDot, maxDot))
            {
                minDot -= 0.5f;
                maxDot += 0.5f;
            }

            for (int iy = 0; iy < vertsY; iy++)
            {
                float v = (float)iy / (vertsY - 1);
                float y = Mathf.Lerp(halfY, -halfY, v);

                for (int ix = 0; ix < vertsX; ix++)
                {
                    float u = (float)ix / (vertsX - 1);
                    float x = Mathf.Lerp(-halfX, halfX, u);

                    Vector3 offset = bentRight * x + bentUp * y;
                    float dot = Vector3.Dot(offset, motionDir);
                    float tSample = Mathf.InverseLerp(minDot, maxDot, dot);

                    var interpSample = SampleAlongCurve(samples, tSample);
                    Vector3 pos = interpSample.position + offset;

                    int idx = iy * vertsX + ix;
                    var atlasUvSprite = ApplyAtlasUV(new Vector2(u, v));
                    m_blurSprite.SetVertex(
                        idx, pos, Vector3.forward,
                        atlasUvSprite.x, atlasUvSprite.y, col, planeNormal, interpSample.forward,
                        tSample, sweepRatio, opacity
                    );
                }
            }

            m_blurSprite.RefreshMesh();
        }

        void BuildObj(Pose[] samples)
        {
            if (m_blurObj == null) return;
            if (samples.Length < 2) return;

            var first = samples[0];
            var last = samples[samples.Length - 1];

            Vector3 motionVec = last.position - first.position;
            float dst = motionVec.magnitude;
            Vector3 motionDir = dst > 0.0001f ? motionVec / dst : Vector3.forward;
            Vector3 avgRight = (first.right + last.right).normalized;
            Vector3 avgUp = (first.up + last.up).normalized;
            Vector3 avgFwd = (first.forward + last.forward).normalized;

            var col = Color.Lerp(StartColor, m_saberData.CustomColor, StartCustomColorWeight);
            if (Mathf.Abs(m_modulatableParams.HueShift) > 0.001f)
                col = ShiftHue(col, m_modulatableParams.HueShift);
            col.a = StartGlow * m_modulatableParams.GlowMultiplier;
            float opacity = StartOpacity * m_modulatableParams.OpacityMultiplier;
            float sweepRatio = Config.BlurSoftness * dst * 50.0f;
            sweepRatio = Mathf.Clamp((sweepRatio * BlurFadeFactor - 0.7f) * 0.05f, 0.0f, 20.0f);

            var positions = m_blurObj.LocalPositions;
            var normals = m_blurObj.LocalNormals;
            var uvs = m_blurObj.Uvs;
            int count = positions.Length;
            if (count == 0) return;
            if (m_objWorldOffsets.Length != count)
                m_objWorldOffsets = new Vector3[count];

            float minDot = float.MaxValue, maxDot = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                Vector3 local = positions[i] * ObjScale;
                Vector3 world = avgRight * local.x + avgUp * local.y + avgFwd * local.z;
                m_objWorldOffsets[i] = world;
                float d = Vector3.Dot(world, motionDir);
                if (d < minDot) minDot = d;
                if (d > maxDot) maxDot = d;
            }
            if (Mathf.Approximately(minDot, maxDot))
            {
                minDot -= 0.5f;
                maxDot += 0.5f;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 offset = m_objWorldOffsets[i];
                float dot = Vector3.Dot(offset, motionDir);
                float tSample = Mathf.InverseLerp(minDot, maxDot, dot);

                var interpSample = SampleAlongCurve(samples, tSample);
                Vector3 pos = interpSample.position + offset;
                Vector3 normal = interpSample.rotation * normals[i];

                var baseUv = uvs[i];
                var atlasUvObj = ApplyAtlasUV(baseUv);
                m_blurObj.SetVertex(
                    i, pos, normal,
                    atlasUvObj.x, atlasUvObj.y, col, normal, interpSample.forward,
                    tSample, sweepRatio, opacity
                );
            }

            m_blurObj.RefreshMesh();
        }
        
        private Pose[] InterpolateData(float maxTime)
        {
            maxTime *= m_motionFactor;

            m_movementHistoryProvider.SampleNonAlloc(SampleCount, maxTime, m_poseSamples);

            const float smoothing = 1f;
            if (smoothing > 0.001f)
            {
                for (int i = 1; i < SampleCount - 1; i++)
                {
                    var prev = m_poseSamples[i - 1];
                    var curr = m_poseSamples[i];
                    var next = m_poseSamples[i + 1];

                    var smoothedPos = Vector3.Lerp(curr.position, (prev.position + curr.position + next.position) / 3f, smoothing);
                    var smoothedFwd = Vector3.Slerp(curr.forward, (prev.forward + curr.forward + next.forward).normalized, smoothing);
                    var smoothedUp = Vector3.Slerp(curr.up, (prev.up + curr.up + next.up).normalized, smoothing);

                    m_poseSamples[i] = new Pose(smoothedPos, Quaternion.LookRotation(smoothedFwd, smoothedUp));
                }
            }

            return m_poseSamples;
        }

        private Vector2 ApplyAtlasUV(Vector2 uv)
        {
            // If shader supports GPU atlas (after asset bundle rebuild), let GPU handle it to support separate color/glow atlases
            var activeMat = GetActiveMaterial();
            if (activeMat != null && (activeMat.HasProperty("_ColorTexAtlasCount") || activeMat.HasProperty("_ColorTexAtlasX")))
                return uv;
            // also check global asset – if bundle was rebuilt, all saber materials will have the property
            if (VainSabersAssets.NormalSaberMaterial != null && (VainSabersAssets.NormalSaberMaterial.HasProperty("_ColorTexAtlasCount") || VainSabersAssets.NormalSaberMaterial.HasProperty("_ColorTexAtlasX")))
                return uv;

            bool hasColorAtlas = ColorAtlasCount.x > 1.5f || ColorAtlasCount.y > 1.5f;
            bool hasGlowAtlas = GlowAtlasCount.x > 1.5f || GlowAtlasCount.y > 1.5f;
            if (!hasColorAtlas && !hasGlowAtlas)
                return uv;

            int ax = 1, ay = 1;
            float spd = 0f;
            // pick atlas based on which texture is assigned; prefer color if both present
            bool useColor = hasColorAtlas;
            if (!hasColorAtlas && hasGlowAtlas)
                useColor = false;
            else if (hasColorAtlas && hasGlowAtlas)
            {
                bool hasColorTex = !string.IsNullOrEmpty(ColorTextureName);
                bool hasGlowTex = !string.IsNullOrEmpty(GlowTextureName);
                if (hasColorTex && !hasGlowTex) useColor = true;
                else if (!hasColorTex && hasGlowTex) useColor = false;
                else useColor = true; // both present -> color primary (shader handles separate glow)
            }

            bool flipX = false, flipY = false;
            if (useColor)
            {
                ax = Mathf.Clamp(Mathf.RoundToInt(ColorAtlasCount.x), 1, 16);
                ay = Mathf.Clamp(Mathf.RoundToInt(ColorAtlasCount.y), 1, 16);
                spd = Mathf.Clamp(ColorAtlasSpeedFlip.x, 0f, 120f);
                flipX = ColorAtlasSpeedFlip.y > 0.5f;
                flipY = ColorAtlasSpeedFlip.z > 0.5f;
            }
            else
            {
                ax = Mathf.Clamp(Mathf.RoundToInt(GlowAtlasCount.x), 1, 16);
                ay = Mathf.Clamp(Mathf.RoundToInt(GlowAtlasCount.y), 1, 16);
                spd = Mathf.Clamp(GlowAtlasSpeedFlip.x, 0f, 120f);
                flipX = GlowAtlasSpeedFlip.y > 0.5f;
                flipY = GlowAtlasSpeedFlip.z > 0.5f;
            }

            if (ax <= 1 && ay <= 1)
                return uv;
            float time = m_saberData != null ? m_saberData.SaberTimeAlive : Time.unscaledTime;
            float count = ax * ay;
            if (count < 1.5f) return uv;
            float frame = Mathf.Floor(Mathf.Repeat(time * spd, count));
            float tileX = Mathf.Repeat(frame, ax);
            float tileY = Mathf.Floor(frame / ax);
            if (flipX) tileX = ax - 1 - tileX;
            if (flipY) tileY = ay - 1 - tileY;
            return new Vector2((uv.x + tileX) / ax, (uv.y + tileY) / ay);
        }

        private Color ShiftHue(Color color, float hueShift)
        {
            Color.RGBToHSV(color, out var h, out var s, out var v);
            
            h = (h + hueShift) % 1f;
            if (h < 0) h += 1f;
            
            return Color.HSVToRGB(h, s, v);
        }
    }

    [Serializable]
    public struct BlurSaberRingParams
    {
        public float PosAlongPart01;
        public float Radius;
        public Color Color;
        public float CustomWeight;
        public float Glow;
        public float Opacity;
        public bool Inverted;
        public Vector2 Offset;
        public float UvOffset;
        public BlurSaberRingParams(
            float posAlongPart01,
            float radius,
            Color color,
            float customWeight,
            float glow,
            float opacity,
            bool inverted,
            Vector2 offset,
            float uvOffset = 0f)
        {
            PosAlongPart01 = posAlongPart01;
            Radius = radius;
            Color = color;
            CustomWeight = customWeight;
            Glow = glow;
            Opacity = opacity;
            Inverted = inverted;
            Offset = offset;
            UvOffset = uvOffset;
        }
    }

    [Serializable]
    public struct SaberTrailData
    {
        public float[] Position;
        public float[] Color;
        public float CustomBlend;
        // New: gradient over length (rgb). If null/empty, fallback to solid Color.
        public List<VainSabers.Data.ColorGradientKey>? ColorGradientKeys;
        // New: custom blend float gradient over length (0-1). If null/empty, fallback to solid CustomBlend.
        public List<VainSabers.Data.FloatGradientKey>? CustomBlendGradientKeys;
        public float Glow;
        public float Opacity;
        public float Width;
        public int Length;
        public int QueueOffset;
        public float DepthOffset;
        public float Fade;
        public string? ColorTextureName;
        public string? GlowTextureName;
        public string? ColorTextureBase64;
        public string? GlowTextureBase64;
        public TextureWrapMode TextureWrap;
        public Vector2 ColorAtlasCount = new Vector2(1, 1);
        public Vector3 ColorAtlasSpeedFlip = new Vector3(1, 0, 0);
        public Vector2 GlowAtlasCount = new Vector2(1, 1);
        public Vector3 GlowAtlasSpeedFlip = new Vector3(1, 0, 0);
        public float MotionActivation; // 0=always visible, 1=gated by movement, exponential toward 0

        // Blade trail vertex noise (world-space 3D noise, blade trails only via vs_flatglow_2side)
        public bool NoiseEnabled;
        public float NoiseIntensity;
        public float NoiseScale;
        public float NoiseSpeed;

        public float MotionFadePower;

        public SaberTrailData(
            float[] position,
            float[] color,
            float customBlend,
            float glow,
            float opacity,
            float width,
            int length,
            int queueOffset,
            float depthOffset = 0f,
            float fade = 1f,
            string? colorTextureName = null,
            string? glowTextureName = null,
            string? colorTextureBase64 = null,
            string? glowTextureBase64 = null,
            TextureWrapMode textureWrap = TextureWrapMode.Clamp,
            float motionActivation = 1f,
            bool noiseEnabled = false,
            float noiseIntensity = 0.02f,
            float noiseScale = 2f,
            float noiseSpeed = 1f,
            float motionFadePower = 0f,
            List<VainSabers.Data.ColorGradientKey>? colorGradientKeys = null,
            List<VainSabers.Data.FloatGradientKey>? customBlendGradientKeys = null,
            Vector2 colorAtlasCount = default,
            Vector3 colorAtlasSpeedFlip = default,
            Vector2 glowAtlasCount = default,
            Vector3 glowAtlasSpeedFlip = default)
        {
            Position = position;
            Color = color;
            CustomBlend = customBlend;
            Glow = glow;
            Opacity = opacity;
            Width = width;
            Length = length;
            QueueOffset = queueOffset;
            DepthOffset = depthOffset;
            Fade = fade;
            ColorTextureName = colorTextureName;
            GlowTextureName = glowTextureName;
            ColorTextureBase64 = colorTextureBase64;
            GlowTextureBase64 = glowTextureBase64;
            TextureWrap = textureWrap;
            MotionActivation = motionActivation;
            NoiseEnabled = noiseEnabled;
            NoiseIntensity = noiseIntensity;
            NoiseScale = noiseScale;
            NoiseSpeed = noiseSpeed;
            MotionFadePower = motionFadePower;
            ColorAtlasCount = colorAtlasCount == default ? new Vector2(1,1) : new Vector2(Mathf.Clamp(colorAtlasCount.x,1,16), Mathf.Clamp(colorAtlasCount.y,1,16));
            ColorAtlasSpeedFlip = colorAtlasSpeedFlip == default ? new Vector3(1,0,0) : new Vector3(Mathf.Clamp(colorAtlasSpeedFlip.x,0f,120f), colorAtlasSpeedFlip.y > 0.5f ? 1f : 0f, colorAtlasSpeedFlip.z > 0.5f ? 1f : 0f);
            GlowAtlasCount = glowAtlasCount == default ? new Vector2(1,1) : new Vector2(Mathf.Clamp(glowAtlasCount.x,1,16), Mathf.Clamp(glowAtlasCount.y,1,16));
            GlowAtlasSpeedFlip = glowAtlasSpeedFlip == default ? new Vector3(1,0,0) : new Vector3(Mathf.Clamp(glowAtlasSpeedFlip.x,0f,120f), glowAtlasSpeedFlip.y > 0.5f ? 1f : 0f, glowAtlasSpeedFlip.z > 0.5f ? 1f : 0f);
            if (colorGradientKeys != null)
                ColorGradientKeys = colorGradientKeys;
            else
            {
                // Default solid gradient from color
                var c = new Color(color[0], color[1], color[2], 1f);
                ColorGradientKeys = new List<VainSabers.Data.ColorGradientKey>
                {
                    new VainSabers.Data.ColorGradientKey(0f, c),
                    new VainSabers.Data.ColorGradientKey(1f, c)
                };
            }
            if (customBlendGradientKeys != null)
                CustomBlendGradientKeys = customBlendGradientKeys;
            else
            {
                CustomBlendGradientKeys = new List<VainSabers.Data.FloatGradientKey>
                {
                    new VainSabers.Data.FloatGradientKey(0f, customBlend),
                    new VainSabers.Data.FloatGradientKey(1f, customBlend)
                };
            }
        }
    }
}

public class BlurPartAnimationModulatableParams
{
    public Vector3 Position;
    public Vector3 RotationEuler;
    public float HueShift;
    public float OpacityMultiplier;
    public float GlowMultiplier;
    public float Motion;
    public float MotionSpeed;
}

public abstract class BlurPartAnimationModulator
{
    public abstract void Apply(BlurPartAnimationModulatableParams paramsToModulate, float saberTimeAlive);

    public virtual BlurPartAnimationModulator Clone() => (BlurPartAnimationModulator)MemberwiseClone();

    public static IReadOnlyList<Type> AvailableTypes { get; } = BuildAvailableTypes();

    private static Type[] BuildAvailableTypes()
    {
        var types = new List<Type>();
        foreach (var type in typeof(BlurPartAnimationModulator).Assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface) continue;
            if (!typeof(BlurPartAnimationModulator).IsAssignableFrom(type)) continue;
            if (type.GetConstructor(Type.EmptyTypes) == null) continue;
            types.Add(type);
        }
        return types.ToArray();
    }
}
public class StepAttribute : Attribute
{
    public float StepSize;
    public StepAttribute(float stepSize)
    {
        StepSize = stepSize;
    }
}

public class SensitivityCoefAttribute : Attribute
{
    public float SensitivityCoef;
    public SensitivityCoefAttribute(float sensitivityCoef)
    {
        SensitivityCoef = sensitivityCoef;
    }
}

public class HueShiftAdder : BlurPartAnimationModulator
{
    [Range(-3f, 3f)]
    [Step(0.01f)]
    [SensitivityCoef(2f)]
    public float Speed = 0.5f;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float saberTimeAlive)
    {
        paramsToModulate.HueShift += Speed * saberTimeAlive;
    }
}

public class HueShiftOscillator : BlurPartAnimationModulator
{
    [Range(-3f, 3f)]
    [Step(0.01f)]
    public float Amplitude = 0.5f;

    [Range(0f, 10f)]
    [SensitivityCoef(5f)]
    public float Frequency = 0.5f;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float saberTimeAlive)
    {
        paramsToModulate.HueShift += Amplitude * Mathf.Sin(2f * Mathf.PI * Frequency * saberTimeAlive);
    }
}

public enum Axis
{
    X,
    Y,
    Z
}

public class PositionOscillator : BlurPartAnimationModulator
{
    public Axis Axis = Axis.X;

    [Range(-1f, 1f)]
    [Step(0.01f)]
    [SensitivityCoef(0.25f)]
    public float Amplitude = 0.5f;

    [Range(0f, 4f)]
    [SensitivityCoef(2f)]
    public float Frequency = 0.5f;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float saberTimeAlive)
    {
        var offset = Amplitude * Mathf.Sin(2f * Mathf.PI * Frequency * saberTimeAlive);
        switch (Axis)
        {
            case Axis.X:
                paramsToModulate.Position.x += offset;
                break;
            case Axis.Y:
                paramsToModulate.Position.y += offset;
                break;
            case Axis.Z:
                paramsToModulate.Position.z += offset;
                break;
        }
    }
}

public class RotationAdder : BlurPartAnimationModulator
{
    public Axis Axis = Axis.X;

    [Range(-180f, 180f)]
    [Step(1f)]
    [SensitivityCoef(90f)]
    public float Speed = 30f;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float saberTimeAlive)
    {
        var angle = Speed * saberTimeAlive;
        switch (Axis)
        {
            case Axis.X:
                paramsToModulate.RotationEuler.x += angle;
                break;
            case Axis.Y:
                paramsToModulate.RotationEuler.y += angle;
                break;
            case Axis.Z:
                paramsToModulate.RotationEuler.z += angle;
                break;
        }
    }
}

public class RotationOscillator : BlurPartAnimationModulator
{
    public Axis Axis = Axis.X;

    [Range(-180f, 180f)]
    [Step(0.01f)]
    public float Amplitude = 0.5f;

    [Range(0f, 4f)]
    [SensitivityCoef(2f)]
    public float Frequency = 0.5f;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float saberTimeAlive)
    {
        var offset = Amplitude * Mathf.Sin(2f * Mathf.PI * Frequency * saberTimeAlive);
        switch (Axis)
        {
            case Axis.X:
                paramsToModulate.RotationEuler.x += offset;
                break;
            case Axis.Y:
                paramsToModulate.RotationEuler.y += offset;
                break;
            case Axis.Z:
                paramsToModulate.RotationEuler.z += offset;
                break;
        }
    }
}

public class OpacityOscillator : BlurPartAnimationModulator
{
    [Range(0f, 3f)]
    [Step(0.01f)]
    public float Amplitude = 0.5f;

    [Range(0f, 4f)]
    [SensitivityCoef(2f)]
    public float Frequency = 0.5f;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float saberTimeAlive)
    {
        paramsToModulate.OpacityMultiplier += Amplitude * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * Frequency * saberTimeAlive));
    }
}

public class GlowOscillator : BlurPartAnimationModulator
{
    [Range(0f, 3f)]
    [Step(0.01f)]
    public float Amplitude = 0.5f;

    [Range(0f, 4f)]
    [SensitivityCoef(2f)]
    public float Frequency = 0.5f;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float saberTimeAlive)
    {
        paramsToModulate.GlowMultiplier += Amplitude * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * Frequency * saberTimeAlive));
    }
}

public class MotionPositionOffset : BlurPartAnimationModulator
{
    public Axis Axis = Axis.X;

    [Range(-1f, 1f)]
    [Step(0.01f)]
    [SensitivityCoef(0.25f)]
    public float Amount = 0.2f;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float saberTimeAlive)
    {
        var offset = paramsToModulate.Motion * Amount;
        switch (Axis)
        {
            case Axis.X:
                paramsToModulate.Position.x += offset;
                break;
            case Axis.Y:
                paramsToModulate.Position.y += offset;
                break;
            case Axis.Z:
                paramsToModulate.Position.z += offset;
                break;
        }
    }
}

public class MotionRotationOffset : BlurPartAnimationModulator
{
    public Axis Axis = Axis.X;

    [Range(-180f, 180f)]
    [Step(1f)]
    [SensitivityCoef(90f)]
    public float Amount = 30f;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float saberTimeAlive)
    {
        var offset = paramsToModulate.Motion * Amount;
        switch (Axis)
        {
            case Axis.X:
                paramsToModulate.RotationEuler.x += offset;
                break;
            case Axis.Y:
                paramsToModulate.RotationEuler.y += offset;
                break;
            case Axis.Z:
                paramsToModulate.RotationEuler.z += offset;
                break;
        }
    }
}

public class MotionHueShift : BlurPartAnimationModulator
{
    [Range(-3f, 3f)]
    [Step(0.01f)]
    public float Amount = 0.5f;

    [Range(-3f, 3f)]
    [Step(0.01f)]
    public float Addend = 0f;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float saberTimeAlive)
    {
        paramsToModulate.HueShift += paramsToModulate.Motion * Amount + Addend;
    }
}

public class MotionGlow : BlurPartAnimationModulator
{
    [Range(-3f, 3f)]
    [Step(0.01f)]
    public float Amount = 1f;

    [Range(-3f, 3f)]
    [Step(0.01f)]
    public float Addend = 0f;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float saberTimeAlive)
    {
        paramsToModulate.GlowMultiplier += paramsToModulate.Motion * Amount + Addend;
    }
}

public class MotionOpacity : BlurPartAnimationModulator
{
    [Range(-3f, 3f)]
    [Step(0.01f)]
    public float Amount = 1f;

    [Range(-3f, 3f)]
    [Step(0.01f)]
    public float Addend = 0f;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float saberTimeAlive)
    {
        paramsToModulate.OpacityMultiplier += paramsToModulate.Motion * Amount + Addend;
    }
}