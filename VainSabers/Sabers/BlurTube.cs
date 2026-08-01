using System;
using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Object = UnityEngine.Object;

namespace VainSabers.Sabers
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct BlurVertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector4 tangent;
        public Vector4 color;
        public Vector2 uv;
        public Vector4 bladeDir;
        public Vector2 uv2;
    }

    internal static class BlurBounds
    {
        public static Bounds Giant = new Bounds(Vector3.zero, Vector3.one * 5);
    }

    internal class BlurTube
    {
        public Mesh TubeMesh { get; private set; }
        public int RingVerts { get; private set; }
        public int VertsPerRing => RingVerts + 1;
        public int RingCount { get; private set; }

        private BlurVertex[] _vertices;
        private int[] _indices;

        public BlurTube(int ringVerts, int ringCount)
        {
            RingVerts = ringVerts;
            RingCount = ringCount;

            int vertsPerRing = ringVerts + 1;
            int vertCount = vertsPerRing * ringCount;
            // Fewer than 2 rings means there's no adjacent ring pair to strip between.
            int stripCount = Math.Max(ringCount - 1, 0);
            int indexCount = ringVerts * stripCount * 6;

            TubeMesh = new Mesh
            {
                indexFormat = vertCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            TubeMesh.MarkDynamic();

            _vertices = new BlurVertex[vertCount];
            _indices = new int[indexCount];

            int t = 0;
            for (int ring = 0; ring < stripCount; ring++)
            {
                int ringStart = ring * vertsPerRing;
                int nextRingStart = (ring + 1) * vertsPerRing;

                for (int i = 0; i < ringVerts; i++)
                {
                    int a = ringStart + i;
                    int b = ringStart + i + 1;
                    int c = nextRingStart + i;
                    int d = nextRingStart + i + 1;

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
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2), // uv (angle, ringPos)
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),  // bladeDir + opacity
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2)  // uv2 (sweepCoord, sweepRatio)
            );
            // Set initial vertex data and indices
            TubeMesh.SetVertexBufferData(_vertices, 0, 0, vertCount, 0, MeshUpdateFlags.DontRecalculateBounds);
            TubeMesh.SetTriangles(_indices, 0, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertex(int idx, Vector3 pos, Vector3 normal, float u, float v, Color color, Vector3 planeNormal, Vector3 bladeDir, float sweepCoord, float sweepRatio, float opacity)
        {
            ref var vert = ref _vertices[idx];
            vert.position = pos;
            vert.normal = normal;
            vert.tangent = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, 0);
            vert.uv = new Vector2(u, v);
            vert.uv2 = new Vector2(sweepCoord, sweepRatio);
            vert.bladeDir = new Vector4(bladeDir.x, bladeDir.y, bladeDir.z, opacity);
            vert.color = color;
        }

        public void RefreshMesh()
        {
            TubeMesh.SetVertexBufferData(_vertices, 0, 0, _vertices.Length, 0, MeshUpdateFlags.DontRecalculateBounds);
            TubeMesh.bounds = BlurBounds.Giant;
        }

        public void Destroy()
        {
            Object.DestroyImmediate(TubeMesh);
        }
    }

    internal class BlurSprite
{
    public Mesh SpriteMesh { get; private set; }
    public int DivisionsX { get; private set; }
    public int DivisionsY { get; private set; }

    private BlurVertex[] _vertices;
    private int[] _indices;

    public BlurSprite(int divisionsX, int divisionsY)
    {
        DivisionsX = divisionsX;
        DivisionsY = divisionsY;

        int vertsX = divisionsX + 1;
        int vertsY = divisionsY + 1;
        int vertCount = vertsX * vertsY;
        int cellCount = divisionsX * divisionsY;
        int indexCount = cellCount * 6;

        SpriteMesh = new Mesh
        {
            indexFormat = vertCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };
        SpriteMesh.MarkDynamic();

        _vertices = new BlurVertex[vertCount];
        _indices = new int[indexCount];
        
        int t = 0;
        for (int iy = 0; iy < divisionsY; iy++)
        {
            for (int ix = 0; ix < divisionsX; ix++)
            {
                int rowStart0 = iy * vertsX;
                int rowStart1 = (iy + 1) * vertsX;

                int bl = rowStart0 + ix;
                int br = rowStart0 + ix + 1;
                int tl = rowStart1 + ix;
                int tr = rowStart1 + ix + 1;
                
                _indices[t++] = bl; _indices[t++] = tl; _indices[t++] = br;
                _indices[t++] = br; _indices[t++] = tl; _indices[t++] = tr;
            }
        }
        
        SpriteMesh.SetVertexBufferParams(vertCount,
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2)
        );
        
        SpriteMesh.SetVertexBufferData(_vertices, 0, 0, vertCount, 0, MeshUpdateFlags.DontRecalculateBounds);
        SpriteMesh.SetTriangles(_indices, 0, false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetVertex(int idx, Vector3 pos, Vector3 normal, float u, float v, Color color, Vector3 planeNormal, Vector3 bladeDir, float sweepCoord, float sweepRatio, float opacity)
    {
        ref var vert = ref _vertices[idx];
        vert.position = pos;
        vert.normal = normal;
        vert.tangent = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, 0);
        vert.uv = new Vector2(u, v);
        vert.uv2 = new Vector2(sweepCoord, sweepRatio);
        vert.bladeDir = new Vector4(bladeDir.x, bladeDir.y, bladeDir.z, opacity);
        vert.color = color;
    }

    public void RefreshMesh()
    {
        SpriteMesh.SetVertexBufferData(_vertices, 0, 0, _vertices.Length, 0, MeshUpdateFlags.DontRecalculateBounds);
        SpriteMesh.bounds = BlurBounds.Giant;
    }

    public void Destroy()
    {
        Object.DestroyImmediate(SpriteMesh);
    }
}
}

