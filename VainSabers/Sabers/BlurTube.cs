using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VainSabers.Sabers
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct TubeVertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector4 tangent;
        public Vector4 color;
        public Vector2 uv;
        public Vector3 bladeDir;
    }

    internal class BlurTube
    {
        public Mesh TubeMesh { get; private set; }
        public int RingVerts { get; private set; }
        public int RingCount { get; private set; }

        private TubeVertex[] _vertices;
        private int[] _indices;

        public BlurTube(int ringVerts, int ringCount)
        {
            RingVerts = ringVerts;
            RingCount = ringCount;

            TubeMesh = new Mesh
            {
                indexFormat = ringVerts * ringCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            TubeMesh.MarkDynamic();

            int vertCount = ringVerts * ringCount;
            int indexCount = ringVerts * (ringCount - 1) * 6;

            _vertices = new TubeVertex[vertCount];
            _indices = new int[indexCount];

            // Fill index buffer
            int t = 0;
            for (int ring = 0; ring < RingCount - 1; ring++)
            {
                int ringStart = ring * RingVerts;
                int nextRingStart = (ring + 1) * RingVerts;

                for (int i = 0; i < RingVerts; i++)
                {
                    int nextI = (i + 1) % RingVerts;
                    int a = ringStart + i;
                    int b = ringStart + nextI;
                    int c = nextRingStart + i;
                    int d = nextRingStart + nextI;

                    _indices[t++] = a; _indices[t++] = c; _indices[t++] = b;
                    _indices[t++] = b; _indices[t++] = c; _indices[t++] = d;
                }
            }

            // Setup vertex buffer layout
            TubeMesh.SetVertexBufferParams(vertCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3), // vertex.xyz
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),   // trueNormal
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),  // planeNormal.xyz + sweepFactor
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),     // color
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2), // uv
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3)  // bladeDir
            );
            // Set initial vertex data and indices
            TubeMesh.SetVertexBufferData(_vertices, 0, 0, vertCount, 0, MeshUpdateFlags.DontRecalculateBounds);
            TubeMesh.SetTriangles(_indices, 0, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertex(int idx, Vector3 pos, Vector3 normal, float sweepCoordinate, Color color, Vector3 planeNormal, Vector3 bladeDir, float sweepRatio)
        {
            ref var v = ref _vertices[idx];
            v.position = pos;
            v.normal = normal;
            v.tangent = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, 0);
            v.uv = new Vector2(sweepCoordinate, Mathf.Clamp01((sweepRatio - 0.7f) * 0.02f));
            v.bladeDir = bladeDir;
            v.color = color;
        }

        public void RefreshMesh()
        {
            TubeMesh.SetVertexBufferData(_vertices, 0, 0, _vertices.Length, 0, MeshUpdateFlags.DontRecalculateBounds);
            TubeMesh.RecalculateBounds();
        }

        public void Destroy()
        {
            Object.DestroyImmediate(TubeMesh);
        }
    }
}
