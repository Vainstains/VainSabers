using UnityEngine;
using VainSabers.Config;

namespace VainSabers.Sabers;

internal class SaberTipTrail : MonoBehaviour
{
    private LineRenderer _lineRenderer = null!;
    private Transform _saber = null!;
    private MovementHistoryProvider _sweepData = null!;
    
    private int _coarseSampleCount = 24;
    private int _refinedSampleCount = 47;
    private int _refinedSampleCount2 = 93;
    
    private Pose[] _poseBuffer = new Pose[24];
    private Vector3[] _coarsePositions = new Vector3[24];
    private Vector3[] _refinedPositions = new Vector3[47];
    private Vector3[] _refinedPositions2 = new Vector3[93];

    private void UpdateSampleCounts(int lengthMs)
    {
        int coarse = Mathf.Clamp(lengthMs / 4, 4, 256);
        if (coarse == _coarseSampleCount) return;
        _coarseSampleCount = coarse;
        _refinedSampleCount = _coarseSampleCount * 2 - 1;
        _refinedSampleCount2 = _refinedSampleCount * 2 - 1;
        _poseBuffer = new Pose[_coarseSampleCount];
        _coarsePositions = new Vector3[_coarseSampleCount];
        _refinedPositions = new Vector3[_refinedSampleCount];
        _refinedPositions2 = new Vector3[_refinedSampleCount2];
        if (_lineRenderer != null)
            _lineRenderer.positionCount = _refinedSampleCount2;
    }

    private float m_opacity = 0.0f;
    private Color m_trailColor = Color.white;
    private Color m_gameColor = Color.white;
    private SaberTrailData m_trailData;

    public void Init(MovementHistoryProvider sweepData, SaberTrailData trailData, Transform saberTransform)
    {
        _sweepData = sweepData;
        _saber = saberTransform;
        m_trailData = trailData;

        UpdateSampleCounts(trailData.Length);
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.material = new Material(VainSabersAssets.VertexGlowShader);
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = _refinedSampleCount2;

        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 0.0f);
        curve.AddKey(0.3f, 1.0f);
        curve.AddKey(1.0f, 0.0f);
        _lineRenderer.widthCurve = curve;

        ApplyConfig(trailData);
    }

    public void ApplyConfig(SaberTrailData trailData)
    {
        m_trailData = trailData;
        UpdateSampleCounts(trailData.Length);
        if (_lineRenderer == null) return;
        _lineRenderer.positionCount = _refinedSampleCount2;
        _lineRenderer.widthMultiplier = trailData.Width;
        _lineRenderer.sortingOrder = 100;
        _lineRenderer.material.renderQueue = 3600 + trailData.QueueOffset;
        _lineRenderer.material.SetFloat("_GlowBoost", trailData.Glow);
        _lineRenderer.material.SetFloat("_DepthOffset", trailData.DepthOffset);
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

    private void LateUpdate()
    {
        if (_sweepData == null || !_lineRenderer)
            return;

        float tipSpeed = EstimateTipSpeed();
        Vector3 localOffset = new Vector3(m_trailData.Position[0], m_trailData.Position[1], m_trailData.Position[2]);

        _sweepData.SampleNonAlloc(_coarseSampleCount, m_trailData.Length * 0.001f, _poseBuffer);
        for (var i = 0; i < _coarseSampleCount; i++)
            _coarsePositions[i] = _poseBuffer[i].position + _poseBuffer[i].rotation * localOffset;

        _lineRenderer.enabled = m_trailData.Length > 0;

        RefinePositions(_coarsePositions, _refinedPositions);
        RefinePositions(_refinedPositions, _refinedPositions2);

        _lineRenderer.SetPositions(_refinedPositions2);

        tipSpeed *= 0.8f;
        m_opacity = Mathf.Max(
            Mathf.Clamp01(tipSpeed - 0.8f),
            Mathf.MoveTowards(m_opacity, 0.0f, Time.deltaTime * 3.0f));

        UpdateGradient(m_opacity * m_trailData.Opacity);
    }

    private float EstimateTipSpeed()
    {
        Pose now = _sweepData.GetPoseAgo(0.0f);
        Pose prev = _sweepData.GetPoseAgo(0.02f);
        return (now.position - prev.position).magnitude / 0.02f;
    }

    private void RefinePositions(Vector3[] coarse, Vector3[] refined)
    {
        int newLength = refined.Length;

        for (int i = 0; i < coarse.Length - 1; i++)
        {
            refined[2 * i] = coarse[i];
            refined[2 * i + 1] = (coarse[i] + coarse[i + 1]) * 0.5f;
        }
        refined[newLength - 1] = coarse[coarse.Length - 1];

        for (int i = 1; i < coarse.Length - 1; i++)
        {
            int index = 2 * i;
            Vector3 midpointAverage = (refined[index - 1] + refined[index + 1]) * 0.5f;
            refined[index] = (refined[index] + midpointAverage) * 0.5f;
        }
    }
    
    private readonly Gradient _cachedGradient = new Gradient();
    private readonly GradientColorKey[] _colorKeys = new GradientColorKey[2];
    private readonly GradientAlphaKey[] _alphaKeys = new GradientAlphaKey[2];

    private void UpdateGradient(float opacity)
    {
        _colorKeys[0] = new GradientColorKey(m_trailColor, 0f);
        _colorKeys[1] = new GradientColorKey(m_trailColor, 1f);
        _alphaKeys[0] = new GradientAlphaKey(0.9f * opacity, 0f);
        _alphaKeys[1] = new GradientAlphaKey(0.9f * opacity * (1f - m_trailData.Fade), 1f);
        _cachedGradient.SetKeys(_colorKeys, _alphaKeys);
        _lineRenderer.colorGradient = _cachedGradient;
    }
}
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
        
        _meshRenderer.material = new Material(VainSabersAssets.VertexGlowShader2Side);
        
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
        tipSpeed *= 0.5f;
        _opacity = Mathf.Max(
            Mathf.Clamp01(tipSpeed - 0.7f),
            Mathf.MoveTowards(_opacity, 0.0f, Time.deltaTime * 4.0f));
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
