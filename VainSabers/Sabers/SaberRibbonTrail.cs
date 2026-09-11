using System.Collections.Generic;
using UnityEngine;
using VainSabers.Data;

namespace VainSabers.Sabers;

public class SaberRibbonTrail : MonoBehaviour
{
    public int SegmentCount => _segmentCount;
    private int _segmentCount = 30;
    private const int VerticalSubdivisions = 6;
    private const int VerticalVertexCount = VerticalSubdivisions + 1;

    private void UpdateSegmentCount(int lengthMs)
    {
        int target = Mathf.Clamp(lengthMs / 6, 4, 512);
        if (target == _segmentCount) return;
        _segmentCount = target;
        InitializeMeshData();
        // reassign mesh after reinit
        if (_meshFilter != null && _mesh != null)
            _meshFilter.mesh = _mesh;
    }
    
    private MeshRenderer _meshRenderer = null!;
    private MeshFilter _meshFilter = null!;
    private Mesh _mesh = null!;
    
    private Vector3[] _vertices = null!;
    private Color[] _colors = null!;
    private Vector2[] _uvs = null!;
    private int[] _triangles = null!;
    
    private float _opacity = 0.0f;
    private Color m_trailColor = Color.white;
    private Color m_baseColor = Color.white;
    private Color m_gameColor = Color.white;
    private SaberTrailData m_trailData;
    
    private MovementHistoryProvider _movementHistory = null!;
    private Transform _saberTransform = null!;

    private BlurSaberPart.AssetKeyCache m_colorTexKey = new();
    private BlurSaberPart.AssetKeyCache m_glowTexKey = new();

    // Shared 32x32x32 random RGB 3D noise texture, sampled in vertex shader with tex3Dlod
    private static Texture3D? s_noiseTex;
    private static Texture3D GetOrCreateNoiseTexture()
    {
        if (s_noiseTex != null) return s_noiseTex;
        const int size = 32;
        s_noiseTex = new Texture3D(size, size, size, TextureFormat.RGBA32, false);
        s_noiseTex.wrapMode = TextureWrapMode.Repeat;
        s_noiseTex.filterMode = FilterMode.Bilinear;
        var rand = new System.Random(12345);
        var colors = new Color[size * size * size];
        for (int z = 0; z < size; z++)
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            colors[x + y * size + z * size * size] = new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
        }
        s_noiseTex.SetPixels(colors);
        s_noiseTex.Apply();
        return s_noiseTex;
    }

    public void Init(MovementHistoryProvider movementHistory, SaberTrailData trailData, Transform saberTransform)
    {
        _movementHistory = movementHistory;
        _saberTransform = saberTransform;
        m_trailData = trailData;

        _meshFilter = gameObject.AddComponent<MeshFilter>();
        _meshRenderer = gameObject.AddComponent<MeshRenderer>();
        
        _mesh = new Mesh();
        _mesh.name = "SaberRibbonTrail";
        _meshFilter.mesh = _mesh;
        
        _meshRenderer.material = new Material(VainSabersAssets.VertexGlowShader);
        
        UpdateSegmentCount(trailData.Length);
        // InitializeMeshData called by UpdateSegmentCount
        ApplyConfig(trailData);
    }

    public void ApplyConfig(SaberTrailData trailData)
    {
        m_trailData = trailData;
        UpdateSegmentCount(trailData.Length);
        var mat = _meshRenderer.material;
        _meshRenderer.sortingOrder = 100;
        mat.renderQueue = 3600 + trailData.QueueOffset;
        mat.SetFloat("_GlowBoost", trailData.Glow);
        mat.SetFloat("_DepthOffset", trailData.DepthOffset);

        var colorTex = BlurSaberPart.LoadTexture(trailData.ColorTextureName, trailData.TextureWrap, trailData.ColorTextureBase64, ref m_colorTexKey);
        var glowTex = BlurSaberPart.LoadTexture(trailData.GlowTextureName, trailData.TextureWrap, trailData.GlowTextureBase64, ref m_glowTexKey);
        mat.SetTexture("_ColorTex", colorTex ?? Texture2D.whiteTexture);
        mat.SetTexture("_GlowTex", glowTex ?? Texture2D.whiteTexture);
        mat.SetFloat("_ColorTexEnabled", colorTex != null ? 1f : 0f);
        mat.SetFloat("_GlowTexEnabled", glowTex != null ? 1f : 0f);

        // Noise: world-space 3D scrolling noise. If NoiseEnabled is false, intensity is forced to 0.
        var noiseTex = GetOrCreateNoiseTexture();
        mat.SetTexture("_NoiseTex", noiseTex);
        float effectiveIntensity = trailData.NoiseEnabled ? trailData.NoiseIntensity : 0f;
        mat.SetFloat("_NoiseIntensity", effectiveIntensity);
        mat.SetFloat("_NoiseScale", trailData.NoiseScale);
        mat.SetFloat("_NoiseSpeed", trailData.NoiseSpeed);

        mat.SetFloat("_TrailDuration", trailData.Length * 0.001f);

        m_baseColor = new Color(trailData.Color[0], trailData.Color[1], trailData.Color[2], 1f);
        UpdateFinalColor();
    }

    public void SetGameColor(Color color)
    {
        m_gameColor = color;
        UpdateFinalColor();
    }

    private void UpdateFinalColor()
    {
        Color tonemappedGame = SquarePreserveLuminance(m_gameColor * 0.8f);
        tonemappedGame.a = m_gameColor.a;
        if (_meshRenderer != null && _meshRenderer.material != null)
        {
            _meshRenderer.material.SetColor("_CustomColor", tonemappedGame);
        }
        m_trailColor = m_baseColor;
        m_tonemappedGame = tonemappedGame;
    }

    private Color m_tonemappedGame = Color.white;

    private float EvaluateCustomBlend(float t)
    {
        var keys = m_trailData.CustomBlendGradientKeys;
        if (keys == null || keys.Count == 0)
            return Mathf.Clamp01(m_trailData.CustomBlend);
        if (keys.Count == 1)
            return Mathf.Clamp01(keys[0].Value);
        keys.Sort((a, b) => a.Time.CompareTo(b.Time));
        t = Mathf.Clamp01(t);
        if (t <= keys[0].Time) return Mathf.Clamp01(keys[0].Value);
        if (t >= keys[keys.Count - 1].Time) return Mathf.Clamp01(keys[keys.Count - 1].Value);
        for (int i = 1; i < keys.Count; i++)
        {
            var prev = keys[i - 1];
            var next = keys[i];
            if (t < next.Time)
            {
                float t0 = t - prev.Time;
                float interval = next.Time - prev.Time;
                float t1 = interval > 0.0001f ? t0 / interval : 0f;
                return Mathf.Clamp01(Mathf.Lerp(prev.Value, next.Value, prev.Easing.Evaluate(t1)));
            }
        }
        return Mathf.Clamp01(keys[keys.Count - 1].Value);
    }

    private Color EvaluateTrailGradient(float t)
    {
        var keys = m_trailData.ColorGradientKeys;
        if (keys == null || keys.Count == 0)
            return m_baseColor;
        if (keys.Count == 1)
            return keys[0].Color;
        // Sort copy to avoid mutating original
        var sorted = new List<ColorGradientKey>(keys);
        sorted.Sort((a, b) => a.Time.CompareTo(b.Time));
        t = Mathf.Clamp01(t);
        if (t <= sorted[0].Time) return sorted[0].Color;
        if (t >= sorted[sorted.Count - 1].Time) return sorted[sorted.Count - 1].Color;
        for (int i = 1; i < sorted.Count; i++)
        {
            var prev = sorted[i - 1];
            var next = sorted[i];
            if (t < next.Time)
            {
                float t0 = t - prev.Time;
                float interval = next.Time - prev.Time;
                float t1 = interval > 0.0001f ? t0 / interval : 0f;
                return Color.Lerp(prev.Color, next.Color, prev.Easing.Evaluate(t1));
            }
        }
        return sorted[sorted.Count - 1].Color;
    }

    private static Color SquarePreserveLuminance(Color c)
    {
        float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        float r2 = c.r * c.r;
        float g2 = c.g * c.g;
        float b2 = c.b * c.b;
        float lum2 = 0.299f * r2 + 0.587f * g2 + 0.114f * b2;
        float scale = (lum2 > 0.00001f) ? (lum / lum2) : 0f;
        return new Color(
            Mathf.Clamp01(r2 * scale),
            Mathf.Clamp01(g2 * scale),
            Mathf.Clamp01(b2 * scale),
            c.a
        );
    }

    private void InitializeMeshData()
    {
        int vertexCount = (SegmentCount + 1) * VerticalVertexCount;
        int triangleCount = SegmentCount * VerticalSubdivisions * 2 * 3;
        
        _vertices = new Vector3[vertexCount];
        _colors = new Color[vertexCount];
        _uvs = new Vector2[vertexCount];
        _triangles = new int[triangleCount];
        
        for (int i = 0; i < SegmentCount; i++)
        {
            for (int v = 0; v < VerticalSubdivisions; v++)
            {
                int quadIndex = i * VerticalSubdivisions + v;
                int triIndex = quadIndex * 6;
                int vert00 = i * VerticalVertexCount + v;
                int vert01 = vert00 + 1;
                int vert10 = (i + 1) * VerticalVertexCount + v;
                int vert11 = vert10 + 1;
                
                _triangles[triIndex] = vert00;
                _triangles[triIndex + 1] = vert10;
                _triangles[triIndex + 2] = vert01;
                
                _triangles[triIndex + 3] = vert01;
                _triangles[triIndex + 4] = vert10;
                _triangles[triIndex + 5] = vert11;
            }
        }
    }

    private void LateUpdate()
    {
        if (_movementHistory == null || _saberTransform == null)
            return;

        float tipSpeed = EstimateTipSpeed();
        UpdateOpacity(tipSpeed);
        UpdateMesh();
        
        _meshRenderer.enabled = m_trailData.Length > 0;
    }

    private float EstimateTipSpeed()
    {
        Pose now = _movementHistory.GetPoseAgo(0.0f);
        Pose prev = _movementHistory.GetPoseAgo(0.02f);
        return (now.position - prev.position).magnitude / 0.02f;
    }

    private void UpdateOpacity(float tipSpeed)
    {
        float activation = Mathf.Clamp01(m_trailData.MotionActivation);
        if (activation <= 0.001f)
        {
            _opacity = 1f;
            return;
        }
        tipSpeed *= 0.5f;
        float expFactor = Mathf.Exp((1f - activation) * 2.2f);
        float threshold = 0.7f * activation;
        float gated = Mathf.Clamp01((tipSpeed - threshold) * expFactor);
        float decay = 4.0f * expFactor;
        _opacity = Mathf.Max(gated, Mathf.MoveTowards(_opacity, 0.0f, Time.deltaTime * decay));
    }

    private void UpdateMesh()
    {
        Vector3 localOffset = new Vector3(m_trailData.Position[0], m_trailData.Position[1], m_trailData.Position[2]);
        float baseFraction = Mathf.Clamp01(m_trailData.Width);

        // Compute average total distance of top and bottom edges for MotionFadePower
        float motionFade = 1f;
        if (m_trailData.MotionFadePower > 0.001f)
        {
            float tipTotal = 0f;
            float baseTotal = 0f;
            Vector3 prevTip = Vector3.zero;
            Vector3 prevBase = Vector3.zero;
            bool first = true;
            for (int i = 0; i <= SegmentCount; i++)
            {
                float t = (float)i / SegmentCount;
                float timeAgo = t * m_trailData.Length * 0.001f;
                Pose pose = _movementHistory.GetPoseAgo(timeAgo);
                Vector3 tipWorld = pose.position + pose.rotation * localOffset;
                Vector3 baseWorld = pose.position + pose.rotation * (localOffset * baseFraction);
                if (!first)
                {
                    tipTotal += Vector3.Distance(tipWorld, prevTip);
                    baseTotal += Vector3.Distance(baseWorld, prevBase);
                }
                prevTip = tipWorld;
                prevBase = baseWorld;
                first = false;
            }
            float avgDist = (tipTotal + baseTotal) * 0.5f;
            float avgSpeed = avgDist;
            motionFade = Mathf.Exp(-avgSpeed * m_trailData.MotionFadePower);
        }

        int vertexIndex = 0;
        
        for (int i = 0; i <= SegmentCount; i++)
        {
            float t = (float)i / SegmentCount;
            float timeAgo = t * m_trailData.Length * 0.001f;
            
            Pose pose = _movementHistory.GetPoseAgo(timeAgo);
            
            Vector3 tipPosWorld = pose.position + pose.rotation * localOffset;
            Vector3 basePosWorld = pose.position + pose.rotation * (localOffset * baseFraction);

            Vector3 basePos = transform.InverseTransformPoint(basePosWorld);
            Vector3 tipPos = transform.InverseTransformPoint(tipPosWorld);
            
            float segmentOpacity = CalculateSegmentOpacity(t);
            float finalOpacity = segmentOpacity * _opacity * m_trailData.Opacity * motionFade;
            Color gradColor = EvaluateTrailGradient(t);
            float blend = EvaluateCustomBlend(t);
            Color blended = Color.Lerp(gradColor, m_tonemappedGame, blend);
            Color tipColorFull = new Color(blended.r, blended.g, blended.b, finalOpacity);
            Color baseColor = new Color(blended.r, blended.g, blended.b, 0f);

            for (int v = 0; v < VerticalVertexCount; v++)
            {
                float vFrac = (float)v / VerticalSubdivisions;
                _vertices[vertexIndex] = Vector3.Lerp(basePos, tipPos, vFrac);
                _uvs[vertexIndex] = new Vector2(t, vFrac);
                _colors[vertexIndex] = Color.Lerp(baseColor, tipColorFull, vFrac);
                vertexIndex++;
            }
        }

        _mesh.Clear();
        _mesh.vertices = _vertices;
        _mesh.colors = _colors;
        _mesh.uv = _uvs;
        _mesh.triangles = _triangles;
        _mesh.RecalculateBounds();
    }

    private float CalculateSegmentOpacity(float t)
    {
        var a = Mathf.Lerp(0.9f, 0.0f, t * m_trailData.Fade) * Mathf.Pow(t, 0.02f);
        return a * a;
    }

    private void OnDestroy()
    {
        if (_mesh != null)
        {
            DestroyImmediate(_mesh);
        }
    }
}
