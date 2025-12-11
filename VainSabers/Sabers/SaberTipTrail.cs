using UnityEngine;
using VainSabers.Config;

namespace VainSabers.Sabers;

internal class SaberTipTrail : MonoBehaviour
{
    private LineRenderer _lineRenderer = null!;
    private Transform _saber = null!;
    private MovementHistoryProvider _sweepData = null!;
    
    private const int CoarseSampleCount = 24; 
    private const int RefinedSampleCount = CoarseSampleCount * 2 - 1;
    private const int RefinedSampleCount2 = RefinedSampleCount * 2 - 1; 

    // Pre-allocated arrays to avoid GC allocations
    private readonly Vector3[] _coarsePositions = new Vector3[CoarseSampleCount];
    private readonly Vector3[] _refinedPositions = new Vector3[RefinedSampleCount];
    private readonly Vector3[] _refinedPositions2 = new Vector3[RefinedSampleCount2];

    private float m_opacity = 0.0f;
    private Color m_color = Color.clear;
    PluginConfig m_config = null!;

    public void Init(PluginConfig conf, Transform saberTransform, MovementHistoryProvider sweepData)
    {
        m_config = conf;
        _saber = saberTransform;
        _sweepData = sweepData;

        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.material = new Material(VainSabersAssets.VertexGlowShader);
        _lineRenderer.widthMultiplier = 0.008f;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = RefinedSampleCount;

        // Width curve over trail lifetime
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 0.0f);
        curve.AddKey(0.3f, 1.0f);
        curve.AddKey(1.0f, 0.0f);
        _lineRenderer.widthCurve = curve;
    }

    private void LateUpdate()
    {
        if (_sweepData == null || !_lineRenderer)
            return;

        float tipSpeed = EstimateTipSpeed();

        // Step 1: Sample coarse positions
        for (int i = 0; i < CoarseSampleCount; i++)
        {
            float t = (i / (float)(CoarseSampleCount - 1)) * m_config.TipTrailMS * 0.001f;
            Pose pose = _sweepData.GetPoseAgo(t);
            _coarsePositions[i] = pose.position + pose.forward;
        }

        _lineRenderer.enabled = m_config.TipTrailMS > 0;

        // Step 2: Refine for smoothness
        RefinePositions(_coarsePositions, _refinedPositions);
        RefinePositions(_refinedPositions, _refinedPositions2);

        // Step 3: Set positions
        _lineRenderer.SetPositions(_refinedPositions2);

        // Opacity based on speed
        tipSpeed *= 0.8f;
        m_opacity = Mathf.Max(
            Mathf.Clamp01(tipSpeed - 0.8f),
            Mathf.MoveTowards(m_opacity, 0.0f, Time.deltaTime * 3.0f));

        // Update gradient
        UpdateGradient(m_opacity);
    }

    private float EstimateTipSpeed()
    {
        Pose now = _sweepData.GetPoseAgo(0.0f);
        Pose prev = _sweepData.GetPoseAgo(0.02f); // ~20 ms ago
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

    private void UpdateGradient(float opacity)
    {
        if (Mathf.Abs(_lineRenderer.colorGradient.alphaKeys[0].alpha - (0.5f * opacity)) > 0.01f)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(m_color, 0.0f),
                    new GradientColorKey(m_color, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.9f * opacity, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            _lineRenderer.colorGradient = gradient;
        }
    }

    public void SetColor(Color color)
    {
        m_color = color;
    }
}
public class SaberRibbonTrail : MonoBehaviour
{
    public int SegmentCount = 30;
    
    private MeshRenderer _meshRenderer = null!;
    private MeshFilter _meshFilter = null!;
    private Mesh _mesh = null!;
    
    // Mesh data arrays
    private Vector3[] _vertices = null!;
    private Color[] _colors = null!;
    private int[] _triangles = null!;
    
    private float _opacity = 0.0f;
    private Color _color = Color.white;
    
    private MovementHistoryProvider _movementHistory = null!;
    private Transform _saberTransform = null!;
    private PluginConfig m_config = null!;

    public void Init(PluginConfig conf, Transform saberTransform, MovementHistoryProvider movementHistory)
    {
        m_config = conf;
        _saberTransform = saberTransform;
        _movementHistory = movementHistory;

        // Create mesh components
        _meshFilter = gameObject.AddComponent<MeshFilter>();
        _meshRenderer = gameObject.AddComponent<MeshRenderer>();
        
        // Create mesh
        _mesh = new Mesh();
        _mesh.name = "SaberRibbonTrail";
        _meshFilter.mesh = _mesh;
        
        // Setup material (using same shader as tip trail)
        _meshRenderer.material = new Material(VainSabersAssets.VertexGlowShader2Side);
        
        InitializeMeshData();
    }

    private void InitializeMeshData()
    {
        int vertexCount = (SegmentCount + 1) * 2; // +1 for present time, ×2 for both edges
        int triangleCount = SegmentCount * 2 * 3; // 2 tris per segment × 3 indices each
        
        _vertices = new Vector3[vertexCount];
        _colors = new Color[vertexCount];
        _triangles = new int[triangleCount];
        
        // Pre-calculate triangles (strip topology)
        for (int i = 0; i < SegmentCount; i++)
        {
            int triIndex = i * 6;
            int vertIndex = i * 2;
            
            // First triangle
            _triangles[triIndex] = vertIndex;
            _triangles[triIndex + 1] = vertIndex + 2;
            _triangles[triIndex + 2] = vertIndex + 1;
            
            // Second triangle
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
        
        _meshRenderer.enabled = m_config.BladeTrailMS > 0;
    }

    private float EstimateTipSpeed()
    {
        Pose now = _movementHistory.GetPoseAgo(0.0f);
        Pose prev = _movementHistory.GetPoseAgo(0.02f);
        return (now.position - prev.position).magnitude / 0.02f;
    }

    private void UpdateOpacity(float tipSpeed)
    {
        // Exact same logic as SaberTipTrail
        tipSpeed *= 0.5f;
        _opacity = Mathf.Max(
            Mathf.Clamp01(tipSpeed - 0.7f),
            Mathf.MoveTowards(_opacity, 0.0f, Time.deltaTime * 4.0f));
    }

    private void UpdateMesh()
    {

        int vertexIndex = 0;
        
        // Sample positions along trail history
        for (int i = 0; i <= SegmentCount; i++)
        {
            float t = (float)i / SegmentCount;
            float timeAgo = t * m_config.BladeTrailMS * 0.001f;
            
            Pose pose = _movementHistory.GetPoseAgo(timeAgo);
            
            // Calculate vertex positions
            Vector3 basePos = pose.position + pose.forward * 0.01f;
            Vector3 tipPos = pose.position + pose.forward;
            
            // Set vertices - base edge (always transparent) and tip edge (animated opacity)
            _vertices[vertexIndex] = basePos;     // Base edge vertex
            _vertices[vertexIndex + 1] = tipPos;  // Tip edge vertex
            
            // Exact same opacity logic as SaberTipTrail's gradient
            float segmentOpacity = CalculateSegmentOpacity(t);
            Color baseColor = new Color(_color.r, _color.g, _color.b, 0f);
            Color tipColor = new Color(_color.r, _color.g, _color.b, segmentOpacity * _opacity * 0.3f);
            
            _colors[vertexIndex] = baseColor;     // Base edge - always transparent
            _colors[vertexIndex + 1] = tipColor;  // Tip edge - animated opacity
            
            vertexIndex += 2;
        }

        // Update mesh
        _mesh.Clear();
        _mesh.vertices = _vertices;
        _mesh.colors = _colors;
        _mesh.triangles = _triangles;
        _mesh.RecalculateBounds();
    }

    private float CalculateSegmentOpacity(float t)
    {
        var a = Mathf.Lerp(0.9f, 0.0f, t) * Mathf.Pow(t, 0.02f);
        return a * a;
    }

    public void SetColor(Color color)
    {
        _color = color;
    }

    private void OnDestroy()
    {
        if (_mesh != null)
        {
            DestroyImmediate(_mesh);
        }
    }
}