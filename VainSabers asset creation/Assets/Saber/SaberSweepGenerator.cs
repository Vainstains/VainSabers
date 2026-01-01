using UnityEngine;
internal class SaberSweepGenerator : MonoBehaviour
{
    private static readonly int BlurAmountProp = Shader.PropertyToID("_BlurAmount");
    
    private Transform? m_saber;

    private SaberSweepData? m_sweepData;
    private Vector3[]? m_positions;
    private Vector3[]? m_bladeDirections;
    private Vector3[]? m_perpendiculars;
    private SaberSweepMeshGenerator? m_sweepMeshGenerator;
    private Material? m_saberMaterial;
    private SaberProfile? m_profile;
    
    private float m_lookBackTime;
    private float m_interpolationBaseOffset;
    public void Init(Transform saber, SaberSweepData sweepData, int sampleCount, float lookBackTime, SaberProfile profile)
    {
        m_saber = saber;
        m_sweepData = sweepData;
        m_positions = new Vector3[sampleCount];
        m_bladeDirections = new Vector3[sampleCount];
        m_perpendiculars = new Vector3[sampleCount];
        
        m_lookBackTime = lookBackTime;
        m_saberMaterial = new Material(Shader.Find("VainSabers/Saber"));

        m_profile = profile;

        m_interpolationBaseOffset = m_profile.Vertices[0].Position.x - 0.2f;
        
        m_sweepMeshGenerator = gameObject.AddInitComponent<SaberSweepMeshGenerator>(
            sampleCount,
            m_profile,
            m_saberMaterial
        );
    }

    public void SetColor(Color color)
    {
        if (!m_saberMaterial)
            return;
        m_saberMaterial!.color = color;
    }
    
    private bool disabled = false;
    
    private float m_rollingTipSpeed = 0;
    private float m_compressedTipSpeed = 0;
    private float m_thresholdCompressor = 0;
    public void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Period))
            disabled = !disabled;
        if (disabled)
            return;
        if (!m_saber || m_positions == null || m_bladeDirections == null || m_perpendiculars == null || m_sweepData == null) return;
        if (m_positions.Length < 2)
        {
            return;
        }
        
        var currentPos = m_saber!.position;
        var currentDir = m_saber!.forward;
        m_sweepData.AddData(currentPos, currentDir, Time.deltaTime, out var currentTipVel);

        m_sweepData.GetDataPointAtTimeAgo(m_lookBackTime, out Vector3 oldPos, out Vector3 oldDir, out var oldTipVel);

        float difference = 0.0f;
        difference += (currentPos - oldPos).magnitude * 0.8f;
        difference += 1.0f - Vector3.Dot(oldDir, currentDir);
        UpdateRollingTipSpeed(m_sweepData!.GetBladeTipSpeed(), difference);
        
        oldPos = Vector3.Lerp(currentPos, oldPos, m_thresholdCompressor);
        oldDir = Vector3.Lerp(currentDir, oldDir, m_thresholdCompressor);
        oldTipVel = Vector3.Lerp(currentTipVel, oldTipVel, m_thresholdCompressor);
        
        InterpolateData(currentPos, currentDir, -currentTipVel, oldPos, oldDir, -oldTipVel, m_saber.up);
        
        m_sweepMeshGenerator?.GenerateSweepVerts(m_positions, m_bladeDirections, m_perpendiculars);
        
        float blurT = Mathf.Clamp01(0.2f * (m_compressedTipSpeed - 1.0f));
        m_saberMaterial?.SetFloat(BlurAmountProp, blurT);
    }

    private void UpdateRollingTipSpeed(float rawTipSpeed, float difference)
    {
        m_rollingTipSpeed += (rawTipSpeed - m_rollingTipSpeed) * (1.0f - Mathf.Exp(-10 * Time.deltaTime));
        if (m_rollingTipSpeed > 1.5f && difference > 0.02f)
        {
            m_thresholdCompressor = Mathf.MoveTowards(m_thresholdCompressor, 1, Time.deltaTime * 30);
        }
        else
        {
            m_thresholdCompressor = Mathf.MoveTowards(m_thresholdCompressor, 0, Time.deltaTime * 20);
        }
        
        m_compressedTipSpeed = rawTipSpeed * m_thresholdCompressor;
    }

    private void InterpolateData(Vector3 posA, Vector3 dirA, Vector3 dirTangentA, Vector3 posB, Vector3 dirB, Vector3 dirTangentB, Vector3 fallbackTangent)
    {
        if (m_positions == null || m_bladeDirections == null || m_perpendiculars == null)
            return;
        
        int n = m_positions.Length;
        
        // interpolate the direction using a Bézier curve,
        // interpolate the position and tangent linearly
        // use the tangent and direction to get the perpendicular
        
        float weight = Vector3.Distance(dirA, dirB) * 0.3f;
        Vector3
            p0 = dirA,
            p1 = dirA + dirTangentA.normalized * weight,
            p2 = dirB - dirTangentB.normalized * weight,
            p3 = dirB;
        
        posA += dirA * m_interpolationBaseOffset;
        posB += dirB * m_interpolationBaseOffset;
        
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / (n - 1);
            
            float u = 1 - t;
            Vector3 dir = u * u * u * p0 +
                          3 * u * u * t * p1 +
                          3 * u * t * t * p2 +
                          t * t * t * p3;

            m_bladeDirections[i] = dir.normalized;
            
            m_positions[i] = Vector3.Lerp(posA, posB, t) - dir * m_interpolationBaseOffset;

            int i1 = i;
            if (i1 < 1)
                i1 = 1;
            else if (i1 > n - 2)
                i1 = n - 2;
            
            Vector3 pos1 = m_positions[i1 - 1] + m_bladeDirections[i1 - 1] * 0.25f;
            var tangent = (m_positions[i1 + 1] + m_bladeDirections[i1 + 1] * 0.25f) - pos1;
            
            if (tangent.magnitude < 0.001f)
                tangent = fallbackTangent;
            
            m_perpendiculars[i] = Vector3.Cross(tangent, dir).normalized;
        }
    }
}