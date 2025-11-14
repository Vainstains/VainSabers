using UnityEngine;

namespace VainSabers.Sabers
{
    internal class BlurTube
    {
        public Mesh TubeMesh { get; private set; }
        public int RingVerts { get; private set; }
        public int RingCount { get; private set; }
        
        public Vector3[] Vertices { get; private set; }
        public Vector3[] Normals { get; private set; }
        public Vector4[] Tangents { get; private set; }
        public Vector2[] Uvs { get; private set; }
        public Color[] Colors { get; private set; }

        public BlurTube(int ringVerts, int ringCount)
        {
            RingVerts = ringVerts;
            RingCount = ringCount;
            
            TubeMesh = new Mesh();
            
            TubeMesh.MarkDynamic();
            
            var indexCount = RingVerts * (RingCount - 1) * 6;
            var vertCount = ringVerts * ringCount;
            
            var indices = new int[indexCount];

            var t = 0;
            for (var ring = 0; ring < RingCount - 1; ring++)
            {
                var ringStart = ring * RingVerts;
                var nextRingStart = (ring + 1) * RingVerts;

                for (var i = 0; i < RingVerts; i++)
                {
                    var nextI = (i + 1) % RingVerts;

                    var a = ringStart + i;
                    var b = ringStart + nextI;
                    var c = nextRingStart + i;
                    var d = nextRingStart + nextI;

                    indices[t++] = a; indices[t++] = c; indices[t++] = b;
                    indices[t++] = b; indices[t++] = c; indices[t++] = d;
                }
            }
            
            Vertices = new Vector3[vertCount];
            Normals = new Vector3[vertCount];
            Tangents = new Vector4[vertCount];
            Uvs = new Vector2[vertCount];
            Colors = new Color[vertCount];
            
            TubeMesh.SetVertices(Vertices);
            TubeMesh.SetNormals(Normals);
            TubeMesh.SetTangents(Tangents);
            TubeMesh.SetUVs(0, Uvs);
            TubeMesh.SetColors(Colors);
            TubeMesh.SetTriangles(indices, 0);
        }

        public void RefreshMesh()
        {
            TubeMesh.SetVertices(Vertices);
            TubeMesh.SetNormals(Normals);
            TubeMesh.SetTangents(Tangents);
            TubeMesh.SetUVs(0, Uvs);
            TubeMesh.SetColors(Colors);
            
            TubeMesh.RecalculateBounds();
        }

        public void Destroy()
        {
            Object.DestroyImmediate(TubeMesh);
        }
    }
}