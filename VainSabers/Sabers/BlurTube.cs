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

    internal static class GpuBlurMeshBuilder
    {
        public static Mesh BuildGpuSprite(int divisionsX, int divisionsY, bool doubleSided,
            float sizeX, float sizeY,
            Color color, float glow, float customWeight, float opacity)
        {
            int vertsX = divisionsX + 1;
            int vertsY = divisionsY + 1;
            int frontVertCount = vertsX * vertsY;
            int surfaceCount = doubleSided ? frontVertCount * 2 : frontVertCount;
            int vertCount = surfaceCount + 4;
            int cellCount = divisionsX * divisionsY;
            int indexCount = cellCount * 6 * (doubleSided ? 2 : 1) + 12;

            var mesh = new Mesh
            {
                indexFormat = vertCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            var vertices = new BlurVertex[vertCount];
            var indices = new int[indexCount];

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
                    indices[t++] = bl; indices[t++] = tl; indices[t++] = br;
                    indices[t++] = br; indices[t++] = tl; indices[t++] = tr;
                    if (doubleSided)
                    {
                        int backBl = bl + frontVertCount;
                        int backBr = br + frontVertCount;
                        int backTl = tl + frontVertCount;
                        int backTr = tr + frontVertCount;
                        indices[t++] = backBl; indices[t++] = backBr; indices[t++] = backTl;
                        indices[t++] = backBr; indices[t++] = backTr; indices[t++] = backTl;
                    }
                }
            }

            float halfX = sizeX * 0.5f;
            float halfY = sizeY * 0.5f;
            Color vertCol = new Color(color.r, color.g, color.b, glow);
            int vIdx = 0;
            for (int iy = 0; iy < vertsY; iy++)
            {
                float v = (float)iy / (vertsY - 1);
                float y = Mathf.Lerp(halfY, -halfY, v);
                for (int ix = 0; ix < vertsX; ix++)
                {
                    float u = (float)ix / (vertsX - 1);
                    float x = Mathf.Lerp(-halfX, halfX, u);
                    ref var vert = ref vertices[vIdx++];
                    vert.position = new Vector3(x, y, 0f);
                    vert.normal = new Vector3(0f, 0f, 1f); // sign via polar.z
                    // store sign in normal.z via polar, but we use same vertex normal; shader reads sign from polar.z
                    // For double sided we will override second half
                    vert.tangent = new Vector4(0f, 0f, 0f, 0f);
                    vert.color = vertCol;
                    vert.uv = new Vector2(u, v);
                    vert.bladeDir = new Vector4(customWeight, opacity, 0f, 0f);
                    vert.uv2 = new Vector2(0f, 0f);
                    // encode sign in normal.z via separate handling for double sided second half
                    // polar is alias of normal attribute, so we set normal = (0,0,sign)
                    vert.normal = new Vector3(0f, 0f, 1f);
                }
            }
            if (doubleSided)
            {
                for (int i = 0; i < frontVertCount; i++)
                {
                    ref var src = ref vertices[i];
                    ref var dst = ref vertices[i + frontVertCount];
                    dst.position = src.position;
                    dst.normal = new Vector3(0f, 0f, -1f);
                    dst.tangent = src.tangent;
                    dst.color = src.color;
                    dst.uv = src.uv;
                    dst.bladeDir = src.bladeDir;
                    dst.uv2 = src.uv2;
                }
            }

            bool vertical = sizeY >= sizeX;
            Vector3 axis = vertical ? Vector3.up : Vector3.right;
            float halfLength = vertical ? halfY : halfX;
            float radius = vertical ? halfX : halfY;
            for (int row = 0; row < 2; row++)
            for (int edge = 0; edge < 2; edge++)
            {
                vertices[surfaceCount + row * 2 + edge] = new BlurVertex
                {
                    position = axis * ((2 * row - 1) * halfLength),
                    normal = axis,
                    tangent = new Vector4(radius, 0f, 0f, 0f),
                    color = vertCol,
                    uv = new Vector2(edge, row),
                    bladeDir = new Vector4(customWeight, opacity, 0f, 0f),
                    uv2 = Vector2.one
                };
            }
            WriteStripQuad(indices, ref t, surfaceCount, surfaceCount + 1, surfaceCount + 2, surfaceCount + 3);

            mesh.SetVertexBufferParams(vertCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2)
            );
            mesh.SetVertexBufferData(vertices, 0, 0, vertCount, 0, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetTriangles(indices, 0, false);
            mesh.bounds = BlurBounds.Giant;
            return mesh;
        }

        public static Mesh BuildGpuObj(ObjMeshData data, Color color, float glow, float customWeight,
            float opacity, out Bounds modelBounds)
        {
            const int LengthSections = 16;
            int sourceCount = data.Positions.Length;
            const int RowSize = 2;
            int blurVertCount = (LengthSections + 1) * RowSize;
            int vertCount = sourceCount + blurVertCount;
            var mesh = new Mesh
            {
                indexFormat = vertCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            var vertices = new BlurVertex[vertCount];
            var indices = new int[LengthSections * 12 + data.Triangles.Length];
            Color vertCol = new Color(color.r, color.g, color.b, glow);
            var materialParams = new Vector4(customWeight, opacity, 0f, 0f);
            modelBounds = new Bounds(sourceCount > 0 ? data.Positions[0] : Vector3.zero, Vector3.zero);
            for (int i = 0; i < sourceCount; i++)
            {
                ref var vert = ref vertices[i];
                vert.position = data.Positions[i];
                modelBounds.Encapsulate(vert.position);
                Vector3 normal = data.Normals[i].normalized;
                vert.normal = normal == Vector3.zero ? Vector3.forward : normal;
                vert.color = vertCol;
                vert.uv = data.Uvs[i];
                vert.bladeDir = materialParams;
            }

            Vector3 extents = modelBounds.extents;
            int axis = extents.x > extents.y ? 0 : 1;
            if (extents.z >= extents[axis]) axis = 2;
            Vector3 lengthAxis = Vector3.zero;
            lengthAxis[axis] = 1f;
            Vector3 crossExtents = extents;
            crossExtents[axis] = 0f;

            int index = 0;
            for (int row = 0; row <= LengthSections; row++)
            {
                float along = (float)row / LengthSections;
                Vector3 center = modelBounds.center + lengthAxis * ((2f * along - 1f) * extents[axis]);
                for (int column = 0; column < RowSize; column++)
                {
                    int v = sourceCount + row * RowSize + column;
                    vertices[v] = new BlurVertex
                    {
                        position = center,
                        normal = lengthAxis,
                        tangent = crossExtents,
                        color = vertCol,
                        uv = new Vector2(column, along),
                        bladeDir = materialParams,
                        uv2 = Vector2.one
                    };

                    if (row == LengthSections || column == 1) continue;
                    int next = v + RowSize;
                    WriteStripQuad(indices, ref index, v, v + 1, next, next + 1);
                }
            }

            // Composite the continuous sweep before the current textured model.
            Array.Copy(data.Triangles, 0, indices, index, data.Triangles.Length);
            mesh.SetVertexBufferParams(vertCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2)
            );
            mesh.SetVertexBufferData(vertices, 0, 0, vertCount, 0, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetTriangles(indices, 0, false);
            mesh.bounds = BlurBounds.Giant;
            return mesh;
        }

        // this is terrible lmao
        public static Mesh BuildGpuTube(int ringVerts, int ringCount,
            System.Func<int, float> getZPos, System.Func<int, float> getOffX, System.Func<int, float> getOffY,
            System.Func<int, float> getRadius, System.Func<int, float> getRadiusSlope, System.Func<int, bool> getIsZero,
            System.Func<int, float> getRingT, System.Func<int, Color> getColor, System.Func<int, float> getGlow,
            System.Func<int, float> getCustomWeight, System.Func<int, float> getOpacity,
            System.Func<int, float> getUvOffset, bool temporalExposure)
        {
            int vertsPerRing = ringVerts + 1;
            int surfaceCount = vertsPerRing * ringCount;
            int vertCount = surfaceCount + (temporalExposure ? 2 * ringCount : 0);
            int stripCount = System.Math.Max(ringCount - 1, 0);
            int indexCount = stripCount * (ringVerts * 6 + (temporalExposure ? 12 : 0));

            var mesh = new Mesh
            {
                indexFormat = vertCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            var vertices = new BlurVertex[vertCount];
            var indices = new int[indexCount];
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
                    indices[t++] = a; indices[t++] = c; indices[t++] = b;
                    indices[t++] = b; indices[t++] = c; indices[t++] = d;
                }
            }

            int vIdx = 0;
            for (int r = 0; r < ringCount; r++)
            {
                float zPos = getZPos(r);
                float offX = getOffX(r);
                float offY = getOffY(r);
                float radius = getRadius(r);
                float radiusSlope = getRadiusSlope(r);
                bool isZero = getIsZero(r);
                float ringT = getRingT(r);
                Color col = getColor(r);
                float glow = getGlow(r);
                float cw = getCustomWeight(r);
                float op = getOpacity(r);
                float uvOff = getUvOffset(r);
                float sign = System.Math.Sign(radius) >= 0 ? 1f : -1f;
                // for isZero raw radius, sign still from raw
                float absRadius = UnityEngine.Mathf.Abs(radius);
                // color a = glow
                Color vertCol = new Color(col.r, col.g, col.b, glow);
                for (int i = 0; i <= ringVerts; i++)
                {
                    float theta = 2.0f * UnityEngine.Mathf.PI * i / ringVerts;
                    float cosT = UnityEngine.Mathf.Cos(theta);
                    float sinT = UnityEngine.Mathf.Sin(theta);
                    float u = sign * (float)i / ringVerts + 0.5f * (1.0f - sign);
                    float v_ = ringT + uvOff;
                    ref var vert = ref vertices[vIdx++];
                    vert.position = new Vector3(zPos, offX, offY);
                    vert.normal = new Vector3(cosT, sinT, sign);
                    vert.tangent = new Vector4(radiusSlope, isZero ? 1f : 0f, ringT, absRadius);
                    vert.color = vertCol;
                    vert.uv = new Vector2(u, v_);
                    vert.bladeDir = new Vector4(cw, op, 0f, 0f);
                }

                if (temporalExposure)
                {
                    int left = surfaceCount + 2 * r;
                    var edge = vertices[r * vertsPerRing];
                    edge.uv.x = 0f;
                    edge.uv2 = Vector2.one;
                    vertices[left] = edge;
                    edge.uv.x = 1f;
                    vertices[left + 1] = edge;
                    if (r < ringCount - 1)
                        WriteStripQuad(indices, ref t, left, left + 1, left + 2, left + 3);
                }
            }

            mesh.SetVertexBufferParams(vertCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2)
            );
            mesh.SetVertexBufferData(vertices, 0, 0, vertCount, 0, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetTriangles(indices, 0, false);
            mesh.bounds = BlurBounds.Giant;
            return mesh;
        }

        private static void WriteStripQuad(int[] indices, ref int index, int a, int b, int c, int d)
        {
            // Each side uses the same vertices. Back-face culling selects one side per eye.
            indices[index++] = a;
            indices[index++] = c;
            indices[index++] = b;
            indices[index++] = b;
            indices[index++] = c;
            indices[index++] = d;
            indices[index++] = a;
            indices[index++] = b;
            indices[index++] = c;
            indices[index++] = b;
            indices[index++] = d;
            indices[index++] = c;
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

