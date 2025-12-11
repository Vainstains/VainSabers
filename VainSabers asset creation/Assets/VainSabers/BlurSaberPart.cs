using System;
using UnityEngine;

namespace VainSabers.Sabers
{
    [ExecuteInEditMode]
    public class BlurSaberPart : MonoBehaviour
    {
        private const int SampleCount = 16;
        private int RingCount => Math.Max((int)(Length * 8), MinimumRings) + (EnableEndCaps ? 2 : 0);
        private int ringVerts = 0;
        
        public float Length;
        public float StartRadius;
        public float EndRadius;

        public Color StartColor = new Color(1, 0.7f, 0.2f, 1);
        public Color EndColor = new Color(0, 0.6f, 1.0f, 1);
        
        public float StartCustomColorWeight = 1;
        public float EndCustomColorWeight = 1;
        
        public float StartGlow = 1;
        public float EndGlow = 1;

        public bool Inverted;

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
        public bool Lit = false;

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
                Mathf.RoundToInt(Mathf.Lerp(6, 24, Mathf.InverseLerp(0.0f, 0.02f, radius))),
                6, 24
            )*2;
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
            
            // Ensure runtime material instances exist
            if (Material != null && (m_runtimeMaterial == null || m_runtimeMaterial.name != Material.name + " (Instance)"))
            {
                if (m_runtimeMaterial != null) DestroyImmediate(m_runtimeMaterial);
                m_runtimeMaterial = Instantiate(Material);
                m_runtimeMaterial.name = Material.name + " (Instance)";
            }

            if (InvertedMaterial != null && (m_runtimeInvertedMaterial == null || m_runtimeInvertedMaterial.name != InvertedMaterial.name + " (Instance)"))
            {
                if (m_runtimeInvertedMaterial != null) DestroyImmediate(m_runtimeInvertedMaterial);
                m_runtimeInvertedMaterial = Instantiate(InvertedMaterial);
                m_runtimeInvertedMaterial.name = InvertedMaterial.name + " (Instance)";
            }

            // Pick which to use
            var activeMat = Inverted ? m_runtimeInvertedMaterial : m_runtimeMaterial;
            if (activeMat != null && InvertedMaterial != null && Material != null)
                activeMat.renderQueue = (Inverted ? InvertedMaterial.renderQueue : Material.renderQueue) + RenderQueueOffset + 500;

            m_meshRenderer.sharedMaterial = Inverted ? InvertedMaterial : Material;
            m_meshFilter.mesh = m_blurTube.TubeMesh;

            RebuildVerts();
            m_blurTube.RefreshMesh();
        }

        private void OnDestroy()
        {
            m_blurTube?.Destroy();
            m_blurTube = null!;
            
            if (m_runtimeMaterial != null) DestroyImmediate(m_runtimeMaterial);
            if (m_runtimeInvertedMaterial != null) DestroyImmediate(m_runtimeInvertedMaterial);
        }

        void RebuildVerts()
        {
            var localPose = transform.GetPose().TransformPose(m_movementHistoryProvider.transform.worldToLocalMatrix);
            var samples = InterpolateData(m_saberData.BlurTime * BlurFactor);
            var wtl = transform.worldToLocalMatrix;
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = localPose.TransformPose(samples[i].AsMatrix()).TransformPose(wtl);
                
                // Debug.DrawRay(samples[i].position, samples[i].right * 0.01f, Color.red);
                // Debug.DrawRay(samples[i].position, samples[i].up * 0.01f, Color.green);
                // Debug.DrawRay(samples[i].position, samples[i].forward * 0.01f, Color.blue);
            }

            int idx = 0;
            
            Color startCol = Color.Lerp(StartColor, m_saberData.CustomColor, StartCustomColorWeight);
            Color endCol = Color.Lerp(EndColor, m_saberData.CustomColor, EndCustomColorWeight);
            
            startCol.a = StartGlow;
            endCol.a = EndGlow;
            
            float startRad = Inverted ? -StartRadius : StartRadius;
            float endRad = Inverted ? -EndRadius : EndRadius;
            if (EnableEndCaps)
                BuildRing(samples, 0 - StartRadius * 0.25f * EndCapExtension, startRad, true, startCol, ref idx);
            int mainRingCount = EnableEndCaps ? RingCount - 2 : RingCount;

            for (int i = 0; i < mainRingCount; i++)
            {
                var t = (float)i / (mainRingCount - 1f);

                float radius = Mathf.Lerp(startRad, endRad, t);
                float bulge = 4 * (t - t * t);
                radius *= 1 + bulge * BulgeAmount;
                
                BuildRing(samples, t * Length, radius,
                    false,
                    Color.Lerp(startCol, endCol, t), ref idx);
            }
            if (EnableEndCaps)
                BuildRing(samples, Length + EndRadius * 0.25f * EndCapExtension, endRad, true, endCol, ref idx);
        }

        Pose LerpPose(Pose a, Pose b, float t)
        {
            Vector3 pos = Vector3.Lerp(a.position, b.position, t);
            Quaternion rot = Quaternion.Slerp(a.rotation, b.rotation, t);
            return new Pose(pos, rot);
        }
        Pose SampleAlongCurve(Pose[] samples, float t)
        {
            if (samples.Length == 0)
                return new Pose();

            if (samples.Length == 1)
                return samples[0];

            t = Mathf.Clamp01(t);
            float scaledT = t * (samples.Length - 1);
            int idx = Mathf.FloorToInt(scaledT);
            int nextIdx = Mathf.Min(idx + 1, samples.Length - 1);
            float localT = scaledT - idx;

            Pose a = samples[idx];
            Pose b = samples[nextIdx];

            return LerpPose(a, b, localT);
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

            Pose first = samples[0];
            Pose last = samples[^1];
            var firstPos = first.position + first.forward * zPos;
            var lastPos = last.position + last.forward * zPos;

            Vector3 motionDir = lastPos - firstPos;
            float dst = motionDir.magnitude;

            Vector3 avgFwd = (first.forward + last.forward).normalized;
            Vector3 tangent = Vector3.Cross(avgFwd, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(avgFwd, tangent).normalized;

            motionDir = Vector3.ProjectOnPlane(motionDir, avgFwd).normalized;
            Vector3 plane = Vector3.Cross(motionDir, avgFwd);

            float sweepRatio = dst / (1.5f * radius);
            
            if (isZero)
            {
                radius = 0.0001f;
            }

            for (int i = 0; i < ringVerts; i++)
            {
                float theta = 2.0f * Mathf.PI * i / ringVerts;
                float sin = Mathf.Sin(theta);
                Vector3 offsetDir = Mathf.Sign(-rawRadius) * Mathf.Cos(theta) * tangent + sin * right;

                float dot = Vector3.Dot(offsetDir, motionDir);
                float tSample = (dot + 1.0f) * 0.5f;

                Pose interpSample = SampleAlongCurve(samples, tSample);

                Vector3 ringCenter = interpSample.position + interpSample.forward * zPos;
                Vector3 normal = offsetDir + 2 * avgFwd * (0.12f * Mathf.Pow(2*(zPos/Length)-1, 9) + Mathf.Pow((2*(zPos/Length)-1) * 0.99f, 171));

                Vector3 vertexPos = ringCenter + offsetDir * (isZero ? 0 : radius);

                SetVertex(
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

        private void SetVertex(int idx, Vector3 pos, Vector3 normal, float sweepCoordinate, Color color, Vector3 planeNormal, Vector3 bladeDir, float sweepRatio)
        {
            if (m_blurTube == null)
                return;
            m_blurTube.Vertices[idx] = pos;
            m_blurTube.Normals[idx] = normal;
            m_blurTube.Tangents[idx] = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, 0);
            m_blurTube.Uvs[idx] = new Vector2(sweepCoordinate, Mathf.Clamp01((sweepRatio - 0.7f) * 0.02f));
            m_blurTube.BladeDirs[idx] = bladeDir;
            m_blurTube.Colors[idx] = color;
        }
        
        private Pose[] InterpolateData(float time)
        {
            var present = m_movementHistoryProvider.GetPoseAgo(0.0f);
            var past = m_movementHistoryProvider.GetPoseAgo(time);
            
            var angleDifference = Vector3.Angle(present.forward, past.forward);
            float factor = Mathf.Clamp01((angleDifference - 0.3f) * 0.3f);
            time *= factor;
            
            var poses = m_movementHistoryProvider.Sample(SampleCount, time);
            var smoothedPoses = new Pose[SampleCount];
            
            for (int i = 0; i < SampleCount; i++)
            {
                if (i == 0)
                {
                    smoothedPoses[i] = poses[i]; // first sample (anchor)
                }
                else if (i == SampleCount - 1)
                {
                    smoothedPoses[i] = poses[i]; // last sample (anchor)
                }
                else
                {
                    var prev = poses[i - 1];
                    var current = poses[i];
                    var next = poses[i + 1];

                    var pos = 0.33333f * (prev.position + current.position + next.position);
                    var fwd = (prev.forward + current.forward + next.forward).normalized;
                    var up = (prev.up + current.up + next.up).normalized;
                    
                    smoothedPoses[i] = new Pose(pos, Quaternion.LookRotation(fwd, up));
                }
            }
            
            return smoothedPoses;
        }
    }
}
