using System;
using System.Collections.Generic;
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
            [Label("Simple (Interpolated)")]
            Simple,
            [Label("Advanced (Per-Ring)")]
            Advanced
        }

        private const int SampleCount = 32;
        private Pose[] m_poseSamples = new Pose[SampleCount];
        private int RingCount =>
            GeometryHandling == GeometryType.Advanced
                ? RingParams.Count
                : Math.Max((int)(Length * 8), MinimumRings) + (EnableEndCaps ? 2 : 0);
        private int ringVerts = 0;
        
        public float RotX, RotY, RotZ;
        
        public float Length;

        public GeometryType GeometryHandling = GeometryType.Simple;
        // for advanced geometry handling, just use these directly and ignore everything else
        public List<BlurSaberRingParams> RingParams = new();

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

        public bool EnableEndCaps = true;
        public bool EnableRoundedNormals = true;

        public float EndCapExtension = 0.25f;

        public float BulgeAmount = 0.00f;
        public int MinimumRings = 4;

        public float RimFactor = 0;
        public float RimPower = 3;
        public float RimPerpendicular = 0;
        
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
        
        private Material? m_runtimeMaterial;
        private Material? m_runtimeInvertedMaterial;
        private Material? m_runtimeLitMaterial;
        private Material? m_runtimeLitInvertedMaterial;
        private MaterialPropertyBlock m_propertyBlock = null!;
        
        public PluginConfig Config = null!;

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
            return Mathf.Clamp(
                Mathf.RoundToInt(Config.SaberQuality * Mathf.Lerp(6, 36, Mathf.InverseLerp(0.0f, 0.02f, radius))),
                6, 36
            );
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
            Length = source.Length;
            GeometryHandling = source.GeometryHandling;
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
            EnableRoundedNormals = source.EnableRoundedNormals;
            RimFactor = source.RimFactor;
            RimPower = source.RimPower;
            RimPerpendicular = source.RimPerpendicular;
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
                m_blurTube = null;
                return;
            }

            if (LinkedPartIndex >= 0 && LinkedPartIndex < m_saberData.ComponentCount)
            {
                var source = m_saberData.Components[LinkedPartIndex];
                if (source != null && source != this)
                    CopyVisualPropertiesFrom(source);
            }

            var ringCount = RingCount;
            if (ringCount < 2)
            {
                // Fewer than two rings means there's no adjacent pair to build a quad strip
                // between - nothing valid to render, so bail out cleanly rather than let
                // BlurTube construct degenerate/negative-length buffers.
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
            
            var activeMat = GetActiveMaterial();
            if (activeMat != null)
            {
                activeMat.renderQueue = 3600 + RenderQueueOffset;
                
                m_propertyBlock ??= new MaterialPropertyBlock();
                m_propertyBlock.SetFloat("_DepthOffset", DepthOffset + (Inverted ? 0f : 0.001f));

                m_propertyBlock.SetFloat("_RimFactor", RimFactor);
                m_propertyBlock.SetFloat("_RimPower", RimPower);
                m_propertyBlock.SetFloat("_RimPerpendicular", RimPerpendicular);
                m_meshRenderer.SetPropertyBlock(m_propertyBlock);
            }

            m_meshRenderer.sharedMaterial = activeMat;
            m_meshFilter.mesh = m_blurTube.TubeMesh;

            RebuildVerts();
            m_blurTube.RefreshMesh();
        }

        private void Update()
        {
            transform.localEulerAngles = new Vector3(RotX, RotY, RotZ);
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
            m_blurTube = null!;
            
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

            var samples = InterpolateData(m_saberData.BlurTime * BlurFactor);
            
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

            var idx = 0;

            if (GeometryHandling == GeometryType.Advanced)
            {
                BuildAdvancedRings(samples, ref idx);
                return;
            }
            
            var startCol = Color.Lerp(StartColor, m_saberData.CustomColor, StartCustomColorWeight);
            var endCol = Color.Lerp(EndColor, m_saberData.CustomColor, EndCustomColorWeight);
            
            if (Mathf.Abs(HueShift) > 0.001f)
            {
                startCol = ShiftHue(startCol, HueShift);
                endCol = ShiftHue(endCol, HueShift);
            }
            
            startCol.a = StartGlow;
            endCol.a = EndGlow;
            
            var startRad = Inverted ? -StartRadius : StartRadius;
            var endRad = Inverted ? -EndRadius : EndRadius;
            if (EnableEndCaps)
                BuildRing(samples, 0 - StartRadius * 0.25f * EndCapExtension, startRad, true, startCol, StartOpacity, ref idx);
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
                    false,
                    Color.Lerp(startCol, endCol, t),
                    Mathf.Lerp(StartOpacity, EndOpacity, t),
                    ref idx, default, radiusSlope);
            }
            if (EnableEndCaps)
                BuildRing(samples, Length + EndRadius * 0.25f * EndCapExtension, endRad, true, endCol, EndOpacity, ref idx);
        }
        
        void BuildAdvancedRings(Pose[] samples, ref int idx)
        {
            var count = RingParams.Count;
            for (var i = 0; i < count; i++)
            {
                var ring = RingParams[i];

                var col = Color.Lerp(ring.Color, m_saberData.CustomColor, ring.CustomWeight);
                if (Mathf.Abs(HueShift) > 0.001f)
                    col = ShiftHue(col, HueShift);
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

                BuildRing(samples, ring.PosAlongPart01 * Length, rawRadius, isZero, col, ring.Opacity, ref idx, ring.Offset, radiusSlope);
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

        void BuildRing(
            Pose[] samples,
            float zPos,
            float rawRadius,
            bool isZero,
            Color color,
            float opacity,
            ref int idx,
            Vector2 offset = default,
            float radiusSlope = 0f)
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

            for (var i = 0; i < ringVerts; i++)
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

                m_blurTube!.SetVertex(
                    idx + i,
                    vertexPos,
                    normal,
                    tSample,
                    color,
                    plane,
                    fwd,
                    sweepRatio * BlurFadeFactor,
                    opacity
                );
            }

            idx += ringVerts;
        }
        
        private Pose[] InterpolateData(float time)
        {
            var present = m_movementHistoryProvider.GetPoseAgo(0.0f);
            var past = m_movementHistoryProvider.GetPoseAgo(time);

            var angleDifference = Vector3.Angle(present.forward, past.forward) + 40 * Vector3.Distance(present.position, past.position);
            var factor = Mathf.Clamp01((angleDifference - 0.3f) * 0.3f);
            time *= factor;

            m_movementHistoryProvider.SampleNonAlloc(SampleCount, time, m_poseSamples);

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

    public record struct BlurSaberRingParams (
        float PosAlongPart01,
        float Radius,
        Color Color,
        float CustomWeight,
        float Glow,
        float Opacity,
        bool Inverted,
        Vector2 Offset
    )
    {
        public override string ToString() => nameof(BlurSaberRingParams);
    }

    public record struct SaberTrailData (
        float[] Position,
        float[] Color,
        float CustomBlend,
        float Glow,
        float Opacity,
        float Width,
        int Length,
        int QueueOffset
    )
    {
        public override string ToString() => nameof(SaberTrailData);
    }
}