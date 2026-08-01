using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Helpers;

namespace VainSabers.Sabers
{
    [ExecuteInEditMode]
    public class BlurSaberPart : MonoBehaviour
    {
public enum GeometryType
        {
            [Label("Simple")]
            Simple,
            [Label("Advanced (Per-Ring)")]
            Advanced,
            [Label("Sprite")]
            Sprite
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
        private int ringVerts = 0;
        
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
        public float RimPower = 3;
        public float RimPerpendicular = 0;

        public float SpecularStrength = 0.41f;
        public float SpecularPower = 48f;
        public float Metallic = 0f;
        public float Smoothness = 0f;
        public float CubemapStrength = 0.78f;
        public float CubemapRotation = 0f;
        public float FresnelStrength = 0.6f;
        public float FresnelPower = 2.89f;
        public Color RimColor = new Color(0.47f, 0.51f, 0.57f, 1f);

        public string? ColorTextureName;
        public string? GlowTextureName;
        public string? ColorTextureBase64;
        public string? GlowTextureBase64;
        public TextureWrapMode TextureWrap = TextureWrapMode.Clamp;
        
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
        private BlurTube? m_blurTube;
        private BlurSprite? m_blurSprite;
        
        private Material? m_runtimeMaterial;
        private Material? m_runtimeInvertedMaterial;
        private Material? m_runtimeLitMaterial;
        private Material? m_runtimeLitInvertedMaterial;
        private MaterialPropertyBlock m_propertyBlock = null!;
        
        public PluginConfig Config = null!;

        private static readonly Dictionary<string, Texture2D> s_loadedTextures = new();

        internal static Texture2D? LoadTexture(string? fileName, TextureWrapMode wrapMode, string? embeddedBase64 = null)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            var cacheKey = $"{fileName}|{(int)wrapMode}|{embeddedBase64}";

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
            ColorTextureName = source.ColorTextureName;
            GlowTextureName = source.GlowTextureName;
            ColorTextureBase64 = source.ColorTextureBase64;
            GlowTextureBase64 = source.GlowTextureBase64;
            TextureWrap = source.TextureWrap;
            LookDir = source.LookDir;
            UseLookDir = source.UseLookDir;
            Material = source.Material;
            InvertedMaterial = source.InvertedMaterial;
            LitMaterial = source.LitMaterial;
            LitInvertedMaterial = source.LitInvertedMaterial;
            RenderQueueOffset = source.RenderQueueOffset;
        }
        
        void LateUpdate()
        {
            if (!this.Inject(ref m_injected))
            {
                m_blurTube?.Destroy();
                m_blurSprite?.Destroy();
                m_blurTube = null;
                m_blurSprite = null;
                return;
            }

            if (!ShouldRenderOnCurrentSaber())
            {
                m_blurTube?.Destroy();
                m_blurSprite?.Destroy();
                m_blurTube = null;
                m_blurSprite = null;
                m_meshFilter.mesh = null;
                return;
            }

            if (LinkedPartIndex >= 0 && LinkedPartIndex < m_saberData.ComponentCount)
            {
                var source = m_saberData.Components[LinkedPartIndex];
                if (source != null && source != this)
                    CopyVisualPropertiesFrom(source);
            }

            Material? activeMat;
            if (GeometryHandling == GeometryType.Sprite)
            {
                // Destroy tube mesh if we had one
                if (m_blurTube != null)
                {
                    m_blurTube.Destroy();
                    m_blurTube = null;
                }

                int divX = Mathf.Max(1, DivisionsX);
                int divY = Mathf.Max(1, DivisionsY);

                if (m_blurSprite == null || m_blurSprite.DivisionsX != divX || m_blurSprite.DivisionsY != divY)
                {
                    m_blurSprite?.Destroy();
                    m_blurSprite = new BlurSprite(divX, divY);
                }

                // Material setup (same as before)
                EnsureRuntimeMaterial(ref m_runtimeMaterial, Material);
                EnsureRuntimeMaterial(ref m_runtimeInvertedMaterial, InvertedMaterial);
                EnsureRuntimeMaterial(ref m_runtimeLitMaterial, LitMaterial);
                EnsureRuntimeMaterial(ref m_runtimeLitInvertedMaterial, LitInvertedMaterial);

                activeMat = GetActiveMaterial();
                if (activeMat != null)
                {
                    activeMat.renderQueue = 3600 + RenderQueueOffset;
                        
                    m_propertyBlock ??= new MaterialPropertyBlock();
                    m_propertyBlock.SetFloat("_DepthOffset", DepthOffset + (Inverted ? 0f : 0.001f));

                    m_propertyBlock.SetFloat("_RimFactor", RimFactor);
                    m_propertyBlock.SetFloat("_RimPower", RimPower);
                    m_propertyBlock.SetFloat("_RimPerpendicular", RimPerpendicular);

                    m_propertyBlock.SetFloat("_SpecularStrength", SpecularStrength);
                    m_propertyBlock.SetFloat("_SpecularPower", SpecularPower);
                    m_propertyBlock.SetFloat("_Metallic", Metallic);
                    m_propertyBlock.SetFloat("_Smoothness", Smoothness);
                    m_propertyBlock.SetFloat("_CubemapStrength", CubemapStrength);
                    m_propertyBlock.SetFloat("_CubemapRotation", CubemapRotation);
                    m_propertyBlock.SetFloat("_FresnelStrength", FresnelStrength);
                    m_propertyBlock.SetFloat("_FresnelPower", FresnelPower);
                    m_propertyBlock.SetColor("_RimColor", RimColor);

                    var colorTex = LoadTexture(ColorTextureName, TextureWrap, ColorTextureBase64);
                    var glowTex = LoadTexture(GlowTextureName, TextureWrap, GlowTextureBase64);
                    if (colorTex != null) m_propertyBlock.SetTexture("_ColorTex", colorTex);
                    else m_propertyBlock.SetTexture("_ColorTex", Texture2D.whiteTexture);
                    if (glowTex != null) m_propertyBlock.SetTexture("_GlowTex", glowTex);
                    else m_propertyBlock.SetTexture("_GlowTex", Texture2D.whiteTexture);

                    m_propertyBlock.SetFloat("_ColorTexEnabled", colorTex != null ? 1f : 0f);
                    m_propertyBlock.SetFloat("_GlowTexEnabled", glowTex != null ? 1f : 0f);

                    m_meshRenderer.SetPropertyBlock(m_propertyBlock);
                }
                m_meshRenderer.sharedMaterial = activeMat;
                m_meshRenderer.sortingOrder = 100;

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

            var ringCount = RingCount;
            if (ringCount < 2)
            {
                m_blurTube?.Destroy();
                m_blurTube = null;
                m_meshFilter.mesh = null;
                return;
            }

            ringVerts = ComputeRingVerts(GetProfileRadiusForRingVerts());
            m_blurTube ??= new BlurTube(ringVerts, ringCount);

            if (m_blurTube.RingVerts != ringVerts || m_blurTube.RingCount != ringCount)
            {
                m_blurTube.Destroy();
                m_blurTube = new BlurTube(ringVerts, ringCount);
            }

            EnsureRuntimeMaterial(ref m_runtimeMaterial, Material);
            EnsureRuntimeMaterial(ref m_runtimeInvertedMaterial, InvertedMaterial);
            EnsureRuntimeMaterial(ref m_runtimeLitMaterial, LitMaterial);
            EnsureRuntimeMaterial(ref m_runtimeLitInvertedMaterial, LitInvertedMaterial);

            activeMat = GetActiveMaterial();
            if (activeMat != null)
            {
                activeMat.renderQueue = 3600 + RenderQueueOffset;
                        
                m_propertyBlock ??= new MaterialPropertyBlock();
                m_propertyBlock.SetFloat("_DepthOffset", DepthOffset + (Inverted ? 0f : 0.001f));

                m_propertyBlock.SetFloat("_RimFactor", RimFactor);
                m_propertyBlock.SetFloat("_RimPower", RimPower);
                m_propertyBlock.SetFloat("_RimPerpendicular", RimPerpendicular);

                m_propertyBlock.SetFloat("_SpecularStrength", SpecularStrength);
                m_propertyBlock.SetFloat("_SpecularPower", SpecularPower);
                m_propertyBlock.SetFloat("_Metallic", Metallic);
                m_propertyBlock.SetFloat("_Smoothness", Smoothness);
                m_propertyBlock.SetFloat("_CubemapStrength", CubemapStrength);
                m_propertyBlock.SetFloat("_CubemapRotation", CubemapRotation);
                m_propertyBlock.SetFloat("_FresnelStrength", FresnelStrength);
                m_propertyBlock.SetFloat("_FresnelPower", FresnelPower);
                m_propertyBlock.SetColor("_RimColor", RimColor);

                var colorTex = LoadTexture(ColorTextureName, TextureWrap, ColorTextureBase64);
                var glowTex = LoadTexture(GlowTextureName, TextureWrap, GlowTextureBase64);
                if (colorTex != null) m_propertyBlock.SetTexture("_ColorTex", colorTex);
                else m_propertyBlock.SetTexture("_ColorTex", Texture2D.whiteTexture);
                if (glowTex != null) m_propertyBlock.SetTexture("_GlowTex", glowTex);
                else m_propertyBlock.SetTexture("_GlowTex", Texture2D.whiteTexture);

                m_propertyBlock.SetFloat("_ColorTexEnabled", colorTex != null ? 1f : 0f);
                m_propertyBlock.SetFloat("_GlowTexEnabled", glowTex != null ? 1f : 0f);

                m_meshRenderer.SetPropertyBlock(m_propertyBlock);
            }
            m_meshRenderer.sharedMaterial = activeMat;
            m_meshRenderer.sortingOrder = 100;
            m_meshFilter.mesh = m_blurTube.TubeMesh;

            RebuildVerts();
            m_blurTube.RefreshMesh();
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
            for (var i = 0; i < modulators.Count; i++)
            {
                var modulator = modulators[i];
                modulator.Apply(m_modulatableParams, Time.unscaledDeltaTime);
            }

            transform.localPosition = m_modulatableParams.Position;
            transform.localEulerAngles = m_modulatableParams.RotationEuler;
        }

        private void UpdateMotion()
        {
            if (m_saberData == null || m_movementHistoryProvider == null)
                return;

            var present = m_movementHistoryProvider.GetPoseAgo(0.0f);
            var past = m_movementHistoryProvider.GetPoseAgo(BlurTime);

            var rawMotion = Vector3.Angle(present.forward, past.forward) + 40 * Vector3.Distance(present.position, past.position);
            rawMotion *= 0.2f;
            float dt = Time.deltaTime;
            float attack = 1f - Mathf.Exp(-12f * dt);
            float release = 1f - Mathf.Exp(-8.5f * dt);
            m_smoothedMotion = Mathf.Lerp(m_smoothedMotion, rawMotion, rawMotion > m_smoothedMotion ? attack : release);

            float targetFactor = Mathf.Clamp01(Mathf.InverseLerp(0.3f, 4f, m_smoothedMotion));
            targetFactor = targetFactor * targetFactor * (3f - 2f * targetFactor);

            float smoothRate = dt * (targetFactor > m_motionFactor ? 3f : 1.2f);
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
            m_blurTube?.Destroy();
            m_blurSprite?.Destroy();
            m_blurTube = null;
            m_blurSprite = null;

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

            var idx = 0;

            if (GeometryHandling == GeometryType.Advanced)
            {
                BuildAdvancedRings(samples, ref idx);
                return;
            }
            
            var startCol = Color.Lerp(StartColor, m_saberData.CustomColor, StartCustomColorWeight);
            var endCol = Color.Lerp(EndColor, m_saberData.CustomColor, EndCustomColorWeight);

            var hueShift = m_modulatableParams.HueShift;
            if (Mathf.Abs(hueShift) > 0.001f)
            {
                startCol = ShiftHue(startCol, hueShift);
                endCol = ShiftHue(endCol, hueShift);
            }
            
            startCol.a = StartGlow * m_modulatableParams.GlowMultiplier;
            endCol.a = EndGlow * m_modulatableParams.GlowMultiplier;
            
            var startRad = Inverted ? -StartRadius : StartRadius;
            var endRad = Inverted ? -EndRadius : EndRadius;
            if (EnableEndCaps)
                BuildRing(samples, 0 - StartRadius * 0.25f * EndCapExtension, startRad, true, 0f, startCol, StartOpacity, ref idx);
            var mainRingCount = EnableEndCaps ? RingCount - 2 : RingCount;

            for (var i = 0; i < mainRingCount; i++)
            {
                var t = (float)i / (mainRingCount - 1f);

                var linearRad = Mathf.Lerp(startRad, endRad, t);
                var bulgeFactor = 1 + 4 * (t - t * t) * BulgeAmount;
                var radius = linearRad * bulgeFactor;
                
                var dLinearRad_dt = endRad - startRad;
                var dBulgeFactor_dt = 4 * (1 - 2 * t) * BulgeAmount;
                var dRadius_dt = dLinearRad_dt * bulgeFactor + linearRad * dBulgeFactor_dt;
                var radiusSlope = Length > 0.0001f ? dRadius_dt / Length : 0f;

                BuildRing(samples, t * Length, radius,
                    false, t,
                    Color.Lerp(startCol, endCol, t),
                    Mathf.Lerp(StartOpacity, EndOpacity, t),
                    ref idx, default, radiusSlope);
            }
            if (EnableEndCaps)
                BuildRing(samples, Length + EndRadius * 0.25f * EndCapExtension, endRad, true, 1f, endCol, EndOpacity, ref idx);
        }
        
        void BuildAdvancedRings(Pose[] samples, ref int idx)
        {
            var count = RingParams.Count;
            var hueShift = m_modulatableParams.HueShift;
            for (var i = 0; i < count; i++)
            {
                var ring = RingParams[i];

                var col = Color.Lerp(ring.Color, m_saberData.CustomColor, ring.CustomWeight);
                if (Mathf.Abs(hueShift) > 0.001f)
                    col = ShiftHue(col, hueShift);
                col.a = ring.Glow;

                var rawRadius = ring.Inverted ? -ring.Radius : ring.Radius;
                var isZero = Mathf.Abs(rawRadius) < 0.0002f;

                var radiusSlope = 0f;
                if (!isZero && count > 1 && Length > 0.0001f)
                {
                    var prevRing = RingParams[(i - 1 + count) % count];
                    var nextRing = RingParams[(i + 1) % count];

                    var prevRad = prevRing.Inverted ? -prevRing.Radius : prevRing.Radius;
                    var nextRad = nextRing.Inverted ? -nextRing.Radius : nextRing.Radius;

                    var curT = ring.PosAlongPart01;
                    
                    var dtPrev = curT - prevRing.PosAlongPart01;
                    if (dtPrev <= 0f) dtPrev += 1f;
                    var dtNext = nextRing.PosAlongPart01 - curT;
                    if (dtNext <= 0f) dtNext += 1f;

                    var dt = dtPrev + dtNext;
                    if (dt > 0.0001f)
                        radiusSlope = (nextRad - prevRad) / (dt * Length);
                }

                var opacity = ring.Opacity * m_modulatableParams.OpacityMultiplier;
                col.a *= m_modulatableParams.GlowMultiplier;

                BuildRing(samples, ring.PosAlongPart01 * Length, rawRadius, isZero, ring.PosAlongPart01, col, opacity, ref idx, ring.Offset, radiusSlope, ring.UvOffset);
            }
        }
        
        Pose SampleAlongCurve(Pose[] samples, float t)
        {
            if (samples.Length == 0)
                return new Pose();
    
            t = Mathf.Clamp01(t);
            var idx = Mathf.FloorToInt(t * (samples.Length - 1));
    
            return samples[idx];
        }
        
        // Rings are built by building a circle around the center defined by zPos and the cross-section offset (basically
        // just a fancy roundabout way to use full 3d coordinates), and taking the movement direction at that point (roughly),
        // dot-ing it with the circle offset, and using that dot to select samples backwards in time to build each vertex with.
        // all the fancier stuff is handled in the shader because insert reason here.
        void BuildRing(
            Pose[] samples,
            float zPos,
            float rawRadius,
            bool isZero,
            float ringT,
            Color color,
            float opacity,
            ref int idx,
            Vector2 offset = default,
            float radiusSlope = 0f,
            float uvOffset = 0f)
        {
            var radius = Mathf.Abs(rawRadius);

            var first = samples[0];
            var last = samples[samples.Length - 1];
            var firstPos = first.position + first.forward * zPos;
            var lastPos = last.position + last.forward * zPos;

            var motionDir = lastPos - firstPos;
            var dst = motionDir.magnitude;

            var avgFwd = (first.forward + last.forward).normalized;
            var tangent = (first.up + last.up).normalized;
            var right = (first.right + last.right).normalized;

            motionDir = Vector3.ProjectOnPlane(motionDir, avgFwd).normalized;
            var plane = Vector3.Cross(motionDir, avgFwd);

            var sweepRatio = Config.BlurSoftness * 1.5f * dst / (1.5f * radius);
            
            if (isZero)
            {
                radius = 0.00001f;
            }
            
            var sign = Mathf.Sign(rawRadius);
            
            var normalAdjustment = Vector3.zero;
            if (EnableRoundedNormals)
            {
                normalAdjustment = isZero
                    ? avgFwd * (2 * (0.12f * Mathf.Pow(2*(zPos/Length)-1, 9) + Mathf.Pow((2*(zPos/Length)-1) * 0.99f, 171)))
                    : avgFwd * -radiusSlope;
            }

            for (var i = 0; i <= ringVerts; i++)
            {
                var theta = 2.0f * Mathf.PI * i / ringVerts;
                var offsetDir = sign * Mathf.Cos(theta) * tangent + Mathf.Sin(theta) * right;

                var dot = Vector3.Dot(offsetDir, motionDir);
                var tSample = (dot + 1.0f) * 0.5f;

                var interpSample = SampleAlongCurve(samples, tSample);
                var fwd = interpSample.forward;
                var ringCenter = interpSample.position + fwd * zPos;
                ringCenter += interpSample.up * offset.y + interpSample.right * offset.x;
                var normal = sign * offsetDir;
                normal += normalAdjustment;

                var vertexPos = ringCenter + offsetDir * (isZero ? 0 : radius);

                var u = sign * (float)i / ringVerts + 0.5f * (1.0f - sign);
                var v = ringT + uvOffset;

                m_blurTube!.SetVertex(
                    idx + i,
                    vertexPos,
                    normal,
                    u,
                    v,
                    color,
                    plane,
                    fwd,
                    tSample,
                    Mathf.Clamp((sweepRatio * BlurFadeFactor - 0.7f) * 0.01f, 0.0f, 5.0f),
                    opacity
                );
            }

            idx += ringVerts + 1;
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
                    m_blurSprite.SetVertex(
                        idx, pos, Vector3.forward,
                        u, v, col, planeNormal, interpSample.forward,
                        tSample, sweepRatio, opacity
                    );
                }
            }

            m_blurSprite.RefreshMesh();
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
        public float Glow;
        public float Opacity;
        public float Width;
        public int Length;
        public int QueueOffset;

        public SaberTrailData(
            float[] position,
            float[] color,
            float customBlend,
            float glow,
            float opacity,
            float width,
            int length,
            int queueOffset)
        {
            Position = position;
            Color = color;
            CustomBlend = customBlend;
            Glow = glow;
            Opacity = opacity;
            Width = width;
            Length = length;
            QueueOffset = queueOffset;
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
    public abstract void Apply(BlurPartAnimationModulatableParams paramsToModulate, float deltaTime);

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

    private float m_time;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float deltaTime)
    {
        m_time += deltaTime;
        paramsToModulate.HueShift += Speed * m_time;
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

    private float m_time;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float deltaTime)
    {
        m_time += deltaTime;
        paramsToModulate.HueShift += Amplitude * Mathf.Sin(2f * Mathf.PI * Frequency * m_time);
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

    private float m_time;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float deltaTime)
    {
        m_time += deltaTime;
        var offset = Amplitude * Mathf.Sin(2f * Mathf.PI * Frequency * m_time);
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

    private float m_angle;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float deltaTime)
    {
        m_angle += Speed * deltaTime;
        switch (Axis)
        {
            case Axis.X:
                paramsToModulate.RotationEuler.x += m_angle;
                break;
            case Axis.Y:
                paramsToModulate.RotationEuler.y += m_angle;
                break;
            case Axis.Z:
                paramsToModulate.RotationEuler.z += m_angle;
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

    private float m_time;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float deltaTime)
    {
        m_time += deltaTime;
        var offset = Amplitude * Mathf.Sin(2f * Mathf.PI * Frequency * m_time);
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

    private float m_time;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float deltaTime)
    {
        m_time += deltaTime;
        paramsToModulate.OpacityMultiplier += Amplitude * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * Frequency * m_time));
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

    private float m_time;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float deltaTime)
    {
        m_time += deltaTime;
        paramsToModulate.GlowMultiplier += Amplitude * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * Frequency * m_time));
    }
}

public class MotionPositionOffset : BlurPartAnimationModulator
{
    public Axis Axis = Axis.X;

    [Range(-1f, 1f)]
    [Step(0.01f)]
    [SensitivityCoef(0.25f)]
    public float Amount = 0.2f;

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float deltaTime)
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

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float deltaTime)
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

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float deltaTime)
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

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float deltaTime)
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

    public override void Apply(BlurPartAnimationModulatableParams paramsToModulate, float deltaTime)
    {
        paramsToModulate.OpacityMultiplier += paramsToModulate.Motion * Amount + Addend;
    }
}