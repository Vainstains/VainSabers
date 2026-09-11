using UnityEngine;

namespace VainSabers.Sabers;

public class SaberRibbonTrail : MonoBehaviour
{
    public int SegmentCount => _segmentCount;
    private int _segmentCount = 30;

    private void UpdateSegmentCount(int lengthMs)
    {
        int target = Mathf.Clamp(lengthMs / 4, 4, 512);
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

        m_trailColor = new Color(trailData.Color[0], trailData.Color[1], trailData.Color[2], 1f);
        UpdateFinalColor();
    }

    public void SetGameColor(Color color)
    {
        m_gameColor = color;
        UpdateFinalColor();
    }

    private void UpdateFinalColor()
    {
        m_trailColor = Color.Lerp(m_trailColor, m_gameColor, m_trailData.CustomBlend);
    }

    private void InitializeMeshData()
    {
        int vertexCount = (SegmentCount + 1) * 2;
        int triangleCount = SegmentCount * 2 * 3;
        
        _vertices = new Vector3[vertexCount];
        _colors = new Color[vertexCount];
        _uvs = new Vector2[vertexCount];
        _triangles = new int[triangleCount];
        
        for (int i = 0; i < SegmentCount; i++)
        {
            int triIndex = i * 6;
            int vertIndex = i * 2;
            
            _triangles[triIndex] = vertIndex;
            _triangles[triIndex + 1] = vertIndex + 2;
            _triangles[triIndex + 2] = vertIndex + 1;
            
            _triangles[triIndex + 3] = vertIndex + 1;
            _triangles[triIndex + 4] = vertIndex + 2;
            _triangles[triIndex + 5] = vertIndex + 3;
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
            
            _vertices[vertexIndex] = basePos;
            _vertices[vertexIndex + 1] = tipPos;

            _uvs[vertexIndex] = new Vector2(t, 0f);
            _uvs[vertexIndex + 1] = new Vector2(t, 1f);
            
            float segmentOpacity = CalculateSegmentOpacity(t);
            Color baseColor = new Color(m_trailColor.r, m_trailColor.g, m_trailColor.b, 0f);
            Color tipColor = new Color(m_trailColor.r, m_trailColor.g, m_trailColor.b, segmentOpacity * _opacity * m_trailData.Opacity);
            
            _colors[vertexIndex] = baseColor;
            _colors[vertexIndex + 1] = tipColor;
            
            vertexIndex += 2;
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
