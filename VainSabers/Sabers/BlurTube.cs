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
        public static readonly Bounds Giant = new Bounds(Vector3.zero, Vector3.one * 5);
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

            TubeMesh.SetVertexBufferParams(vertCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2)
            );
            TubeMesh.SetVertexBufferData(_vertices, 0, 0, vertCount, 0, MeshUpdateFlags.DontRecalculateBounds);
            TubeMesh.SetTriangles(_indices, 0, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertex(int idx, in Vector3 pos, in Vector3 normal, float u, float v, in Color color, in Vector3 planeNormal, in Vector3 bladeDir, float sweepCoord, float sweepRatio, float opacity)
        {
            ref var vert = ref _vertices[idx];
            vert.position = pos;
            vert.normal = normal;
            vert.tangent.x = planeNormal.x;
            vert.tangent.y = planeNormal.y;
            vert.tangent.z = planeNormal.z;
            vert.tangent.w = 0f;
            vert.uv.x = u;
            vert.uv.y = v;
            vert.uv2.x = sweepCoord;
            vert.uv2.y = sweepRatio;
            vert.bladeDir.x = bladeDir.x;
            vert.bladeDir.y = bladeDir.y;
            vert.bladeDir.z = bladeDir.z;
            vert.bladeDir.w = opacity;
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
        public bool DoubleSided { get; private set; }

        private BlurVertex[] _vertices;
        private int[] _indices;
        private int _frontVertCount;

        public BlurSprite(int divisionsX, int divisionsY, bool doubleSided = false)
        {
            DivisionsX = divisionsX;
            DivisionsY = divisionsY;
            DoubleSided = doubleSided;

            int vertsX = divisionsX + 1;
            int vertsY = divisionsY + 1;
            _frontVertCount = vertsX * vertsY;
            int vertCount = doubleSided ? _frontVertCount * 2 : _frontVertCount;
            int cellCount = divisionsX * divisionsY;
            int indexCount = cellCount * 6 * (doubleSided ? 2 : 1);

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

                    if (doubleSided)
                    {
                        int backBl = bl + _frontVertCount;
                        int backBr = br + _frontVertCount;
                        int backTl = tl + _frontVertCount;
                        int backTr = tr + _frontVertCount;

                        _indices[t++] = backBl; _indices[t++] = backBr; _indices[t++] = backTl;
                        _indices[t++] = backBr; _indices[t++] = backTr; _indices[t++] = backTl;
                    }
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
        public void SetVertex(int idx, in Vector3 pos, in Vector3 normal, float u, float v, in Color color, in Vector3 planeNormal, in Vector3 bladeDir, float sweepCoord, float sweepRatio, float opacity)
        {
            ref var vert = ref _vertices[idx];
            vert.position = pos;
            vert.normal = normal;
            vert.tangent.x = planeNormal.x;
            vert.tangent.y = planeNormal.y;
            vert.tangent.z = planeNormal.z;
            vert.tangent.w = 0f;
            vert.uv.x = u;
            vert.uv.y = v;
            vert.uv2.x = sweepCoord;
            vert.uv2.y = sweepRatio;
            vert.bladeDir.x = bladeDir.x;
            vert.bladeDir.y = bladeDir.y;
            vert.bladeDir.z = bladeDir.z;
            vert.bladeDir.w = opacity;
            vert.color = color;

            if (DoubleSided)
            {
                ref var backVert = ref _vertices[idx + _frontVertCount];
                backVert.position = pos;
                backVert.normal.x = -normal.x;
                backVert.normal.y = -normal.y;
                backVert.normal.z = -normal.z;
                backVert.tangent.x = planeNormal.x;
                backVert.tangent.y = planeNormal.y;
                backVert.tangent.z = planeNormal.z;
                backVert.tangent.w = 0f;
                backVert.uv.x = u;
                backVert.uv.y = v;
                backVert.uv2.x = sweepCoord;
                backVert.uv2.y = sweepRatio;
                backVert.bladeDir.x = bladeDir.x;
                backVert.bladeDir.y = bladeDir.y;
                backVert.bladeDir.z = bladeDir.z;
                backVert.bladeDir.w = opacity;
                backVert.color = color;
            }
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

    internal class BlurObj
    {
        public Mesh ObjMesh { get; private set; }
        public Vector3[] LocalPositions { get; private set; }
        public Vector3[] LocalNormals { get; private set; }
        public Vector2[] Uvs { get; private set; }
        public string CacheKey { get; private set; }

        private BlurVertex[] _vertices;

        public BlurObj(ObjMeshData data)
        {
            LocalPositions = data.Positions;
            LocalNormals = data.Normals;
            Uvs = data.Uvs;
            CacheKey = data.CacheKey;

            _vertices = new BlurVertex[LocalPositions.Length];

            ObjMesh = new Mesh
            {
                indexFormat = _vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            ObjMesh.MarkDynamic();

            ObjMesh.SetVertexBufferParams(_vertices.Length,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2)
            );
            ObjMesh.SetVertexBufferData(_vertices, 0, 0, _vertices.Length, 0, MeshUpdateFlags.DontRecalculateBounds);
            ObjMesh.SetTriangles(data.Triangles, 0, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertex(int idx, in Vector3 pos, in Vector3 normal, float u, float v, in Color color, in Vector3 planeNormal, in Vector3 bladeDir, float sweepCoord, float sweepRatio, float opacity)
        {
            ref var vert = ref _vertices[idx];
            vert.position = pos;
            vert.normal = normal;
            vert.tangent.x = planeNormal.x;
            vert.tangent.y = planeNormal.y;
            vert.tangent.z = planeNormal.z;
            vert.tangent.w = 0f;
            vert.uv.x = u;
            vert.uv.y = v;
            vert.uv2.x = sweepCoord;
            vert.uv2.y = sweepRatio;
            vert.bladeDir.x = bladeDir.x;
            vert.bladeDir.y = bladeDir.y;
            vert.bladeDir.z = bladeDir.z;
            vert.bladeDir.w = opacity;
            vert.color = color;
        }

        public void RefreshMesh()
        {
            ObjMesh.SetVertexBufferData(_vertices, 0, 0, _vertices.Length, 0, MeshUpdateFlags.DontRecalculateBounds);
            ObjMesh.bounds = BlurBounds.Giant;
        }

        public void Destroy()
        {
            Object.DestroyImmediate(ObjMesh);
        }
    }
}

