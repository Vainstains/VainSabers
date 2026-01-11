using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Helpers;

namespace VainSabers.Sabers
{
    [ExecuteInEditMode]
    public class BlurSaberPart : MonoBehaviour
    {
        private const int SampleCount = 16;
        private Pose[] m_poseSamples = new Pose[SampleCount];
        private int RingCount => Math.Max((int)(Length * 8), MinimumRings) + (EnableEndCaps ? 2 : 0);
        private int ringVerts = 0;
        
        public float Length;
        public float StartRadius;
        public float EndRadius;

        public Color StartColor = new Color(1, 0.7f, 0.2f, 1);
        public Color EndColor = new Color(0, 0.6f, 1.0f, 1);
        
        public float StartCustomColorWeight = 1;
        public float EndCustomColorWeight = 1;
        
        public float HueShift = 0f;
        
        public float StartGlow = 1;
        public float EndGlow = 1;

        public bool Inverted;
        public bool Lit;

        public float BlurFactor = 1;
        public float BlurFadeFactor = 1;

        public bool EnableEndCaps = true;

        public float EndCapExtension = 0.25f;

        public float BulgeAmount = 0.00f;
        public int MinimumRings = 4;
        
        public Vector3 LookDir = Vector3.zero;
        public bool UseLookDir = false;
        
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

        private void Start()
        {
            if (UseLookDir)
            {
                transform.localRotation = Quaternion.LookRotation(LookDir);
            }
        }

        void LateUpdate()
        {
            if (!this.Inject(ref m_injected))
            {
                m_blurTube?.Destroy();
                m_blurTube = null;
                return;
            }

            ringVerts = ComputeRingVerts(Mathf.Max(StartRadius, EndRadius));
            m_blurTube ??= new BlurTube(ringVerts, RingCount);

            if (m_blurTube.RingVerts != ringVerts || m_blurTube.RingCount != RingCount)
            {
                m_blurTube.Destroy();
                m_blurTube = new BlurTube(ringVerts, RingCount);
            }
            
            EnsureRuntimeMaterial(ref m_runtimeMaterial, Material);
            EnsureRuntimeMaterial(ref m_runtimeInvertedMaterial, InvertedMaterial);
            EnsureRuntimeMaterial(ref m_runtimeLitMaterial, LitMaterial);
            EnsureRuntimeMaterial(ref m_runtimeLitInvertedMaterial, LitInvertedMaterial);
            
            var activeMat = GetActiveMaterial();
            if (activeMat != null)
            {
                var baseMaterial = GetBaseMaterial();
                activeMat.renderQueue = baseMaterial.renderQueue + RenderQueueOffset + 500;
            }

            m_meshRenderer.sharedMaterial = activeMat;
            m_meshFilter.mesh = m_blurTube.TubeMesh;

            RebuildVerts();
            m_blurTube.RefreshMesh();
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
                BuildRing(samples, 0 - StartRadius * 0.25f * EndCapExtension, startRad, true, startCol, ref idx);
            var mainRingCount = EnableEndCaps ? RingCount - 2 : RingCount;

            for (var i = 0; i < mainRingCount; i++)
            {
                var t = (float)i / (mainRingCount - 1f);

                var radius = Mathf.Lerp(startRad, endRad, t);
                var bulge = 4 * (t - t * t);
                radius *= 1 + bulge * BulgeAmount;
                
                BuildRing(samples, t * Length, radius,
                    false,
                    Color.Lerp(startCol, endCol, t), ref idx);
            }
            if (EnableEndCaps)
                BuildRing(samples, Length + EndRadius * 0.25f * EndCapExtension, endRad, true, endCol, ref idx);
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
            ref int idx)
        {
            var radius = Mathf.Abs(rawRadius);

            var first = samples[0];
            var last = samples[^1];
            var firstPos = first.position + first.forward * zPos;
            var lastPos = last.position + last.forward * zPos;

            var motionDir = lastPos - firstPos;
            var dst = motionDir.magnitude;

            var avgFwd = (first.forward + last.forward).normalized;
            var tangent = Vector3.Cross(avgFwd, transform.up).normalized;
            var right = Vector3.Cross(avgFwd, tangent).normalized;

            motionDir = Vector3.ProjectOnPlane(motionDir, avgFwd).normalized;
            var plane = Vector3.Cross(motionDir, avgFwd);

            var sweepRatio = dst / (1.5f * radius);
            
            if (isZero)
            {
                radius = 0.0001f;
            }

            for (var i = 0; i < ringVerts; i++)
            {
                var theta = 2.0f * Mathf.PI * i / ringVerts;
                var offsetDir = Mathf.Sign(-rawRadius) * Mathf.Cos(theta) * tangent + Mathf.Sin(theta) * right;

                var dot = Vector3.Dot(offsetDir, motionDir);
                var tSample = (dot + 1.0f) * 0.5f;

                var interpSample = SampleAlongCurve(samples, tSample);

                var ringCenter = interpSample.position + interpSample.forward * zPos;
                var normal = offsetDir + avgFwd * (2 * (0.12f * Mathf.Pow(2*(zPos/Length)-1, 9) + Mathf.Pow((2*(zPos/Length)-1) * 0.99f, 171)));

                var vertexPos = ringCenter + offsetDir * (isZero ? 0 : radius);

                m_blurTube!.SetVertex(
                    idx + i,
                    vertexPos,
                    normal,
                    tSample,
                    color,
                    plane,
                    interpSample.forward,
                    sweepRatio * BlurFadeFactor
                );
            }

            idx += ringVerts;
        }
        
        private Pose[] InterpolateData(float time)
        {
            var present = m_movementHistoryProvider.GetPoseAgo(0.0f);
            var past = m_movementHistoryProvider.GetPoseAgo(time);

            var angleDifference = Vector3.Angle(present.forward, past.forward);
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
}