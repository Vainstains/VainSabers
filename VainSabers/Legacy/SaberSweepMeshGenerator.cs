using UnityEngine;

namespace VainSabers.Legacy;

internal class SaberSweepMeshGenerator : MonoBehaviour
{
    private const int SemicircleResolution = 5;
    private int m_sweepSampleCount;
    private LegacySaberProfile? m_profile;
    
    private Vector3[]? m_vertices;
    private Vector4[]? m_normals; // packed into tangents
    private Vector2[]? m_uvs;
    private Color[]? m_colors;
    
    private int[]? m_triangles;
    
    private MeshFilter? m_meshFilter;
    private MeshRenderer? m_meshRenderer;
    private Mesh? m_mesh;
    private Material? m_material;
    
    public void Init(int sweepSamples, LegacySaberProfile legacySaberProfile, Material material)
    {
        m_sweepSampleCount = sweepSamples;
        m_profile = legacySaberProfile;

        int vertsPerRing = 2 * SemicircleResolution + 2 * sweepSamples - 2;
        int vertexCount = m_profile.Vertices.Length * vertsPerRing;

        int triangleCount = (m_profile.Vertices.Length - 1) * vertsPerRing * 6;
        
        m_vertices = new Vector3[vertexCount];
        m_normals = new Vector4[vertexCount];
        m_uvs = new Vector2[vertexCount];
        m_colors = new Color[vertexCount];
        
        m_triangles = new int[triangleCount];
        
        m_meshFilter = gameObject.AddComponent<MeshFilter>();
        m_meshRenderer = gameObject.AddComponent<MeshRenderer>();
        m_mesh = new Mesh();
        m_mesh.MarkDynamic();
        m_meshFilter.sharedMesh = m_mesh;

        m_material = material;
        m_meshRenderer.sharedMaterial = m_material;
        
        GenerateSweepTris();
    }

    private void GenerateSweepTris()
    {
        if (m_triangles == null)
            return;
        
        int triIdx = 0;
        int ringVertexCount = 2 * (SemicircleResolution + 1) + 2 * (m_sweepSampleCount - 2);
        
        for (int r = 0; r < m_profile!.Vertices.Length - 1; r++)
        {
            int startCurrent = r * ringVertexCount;
            int startNext = (r + 1) * ringVertexCount;

            for (int i = 0; i < ringVertexCount; i++)
            {
                int nextI = (i + 1) % ringVertexCount; // wrap around ring
                int a = startCurrent + i;
                int b = startNext + i;
                int c = startNext + nextI;
                int d = startCurrent + nextI;

                // Two triangles for this quad
                m_triangles[triIdx++] = a;
                m_triangles[triIdx++] = b;
                m_triangles[triIdx++] = c;

                m_triangles[triIdx++] = a;
                m_triangles[triIdx++] = c;
                m_triangles[triIdx++] = d;
            }
        }
    }

    public void GenerateSweepVerts(Vector3[] positions, Vector3[] normals, Vector3[] perpendiculars)
    {
        if (m_mesh == null)
            return;
        
        // uv packing: u = sweep progress, v = glow factor
    
        int idx = 0;
        
        var pos0 = positions[0];
        var norm0 = normals[0];
        var perp0 = perpendiculars[0];
        var coperp0 = Vector3.Cross(perp0, norm0);
        
        int lastIdx = positions.Length - 1;
        var pos1 = positions[lastIdx];
        var norm1 = normals[lastIdx];
        var perp1 = perpendiculars[lastIdx];
        var coperp1 = Vector3.Cross(perp1, norm1);
        
        // all rings, ascending
        for (int i = 0; i < m_profile!.Vertices.Length; i++)
        {
            float normParam = m_profile!.Vertices[i].Position.x;
            float radius = m_profile!.Vertices[i].Position.y;
            float glowFactor = m_profile!.Vertices[i].BladeGlowFactor;
            
            Color bladeExtra = new Color(
                m_profile!.Vertices[i].BladeWhiteFactor,
                m_profile!.Vertices[i].BladeFadeFactor,
                m_profile!.Vertices[i].EdgeSoftness,
                Mathf.Clamp01(1.0f - 3 * (m_profile!.Vertices[i].Position.x + 0.5f)) * 0.8f
            );
            
            float edgeLength = 0;
            for (int j = 1; j < positions.Length; j++)
            {
                Vector3 centerVert0 = positions[j-1] + normals[j-1] * normParam;
                Vector3 centerVert1 = positions[j] + normals[j] * normParam;
                edgeLength += Vector3.Distance(centerVert0, centerVert1);
            }

            float semicircleLength = radius * 0.5f;
            float totalWidth = 2.0f * semicircleLength + edgeLength;
            float semicircleUVSize = semicircleLength / totalWidth;
            float edgeUVStart = semicircleLength / totalWidth;
            float edgeUVEnd = (semicircleLength + edgeLength) / totalWidth;
            
            // 1.) start semicircle
            for (int j = 0; j < SemicircleResolution + 1; j++)
            {
                float theta = Mathf.PI * j / SemicircleResolution;
                float sin = Mathf.Sin(theta);
                Vector3 offset = coperp0 * sin + perp0 * Mathf.Cos(theta);
                
                m_vertices![idx] = pos0 + offset * radius + norm0 * normParam;
                m_uvs![idx] = new Vector2((1.0f - sin) * semicircleUVSize, glowFactor);
                m_normals![idx] = perp0;
                m_colors![idx] = bladeExtra;
                
                idx++;
            }
    
            // 2.) pure edge 1
            for (int j = 1; j < positions.Length - 1; j++)
            {
                Vector3 offset = -perpendiculars[j];
                m_vertices![idx] = positions[j] + offset * radius + normals[j] * normParam;
                float t = (float)j / (positions.Length - 1);
                m_uvs![idx] = new Vector2(Mathf.Lerp(edgeUVStart, edgeUVEnd, t), glowFactor);
                m_normals![idx] = perpendiculars[j];
                m_colors![idx] = bladeExtra;
    
                idx++;
            }
    
            // 3.) end semicircle
            for (int j = 0; j < SemicircleResolution + 1; j++)
            {
                float theta = Mathf.PI * j / SemicircleResolution;
                float sin = Mathf.Sin(theta);
                Vector3 offset = coperp1 * sin + perp1 * Mathf.Cos(theta);
                
                m_vertices![idx] = pos1 - offset * radius + norm1 * normParam;
                m_uvs![idx] = new Vector2(edgeUVEnd + sin * semicircleUVSize, glowFactor);
                m_normals![idx] = perp1;
                m_colors![idx] = bladeExtra;
        
                idx++;
            }
    
            // 4.) pure edge 2
            for (int j = positions.Length - 2; j >= 1; j--)
            {
                Vector3 offset = perpendiculars[j];
                m_vertices![idx] = positions[j] + offset * radius + normals[j] * normParam;
                float t = (float)j / (positions.Length - 1);
                m_uvs![idx] = new Vector2(Mathf.Lerp(edgeUVStart, edgeUVEnd, t), glowFactor);
                m_normals![idx] = perpendiculars[j];
                m_colors![idx] = bladeExtra;
    
                idx++;
            }
        }
        
        m_mesh.SetVertices(m_vertices);
        m_mesh.SetUVs(0, m_uvs);
        m_mesh.SetColors(m_colors);
        m_mesh.SetTangents(m_normals);
        m_mesh.SetTriangles(m_triangles, 0);
        m_mesh.RecalculateNormals();
        m_mesh.RecalculateBounds();
    }
}