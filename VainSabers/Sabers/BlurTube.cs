using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

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
        public Vector4 bladeDir;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuRingParams
    {
        public Vector4 colorAndGlow;
        public Vector4 motionDirSign;
        public Vector4 avgFwdRadiusSlope;
        public Vector4 tangent;
        public Vector4 right;
        public Vector4 plane;
        public Vector4 zPosRadiusIsZero;
        public Vector4 offsetSweepOpac;
        public Vector4 lengthRounded;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuSampleData
    {
        public Vector4 position;
        public Vector4 forward;
        public Vector4 up;
        public Vector4 right;
    }

    internal class BlurTube
    {
        public Mesh TubeMesh { get; private set; }
        public int RingVerts { get; private set; }
        public int RingCount { get; private set; }

        private TubeVertex[] _vertices;
        private int[] _indices;

        private readonly ComputeShader _computeShader;
        private readonly int _kernelIndex;
        private readonly ComputeBuffer _vertexBuffer;
        private readonly ComputeBuffer _ringParamsBuffer;
        private readonly ComputeBuffer _sampleDataBuffer;

        private const int THREAD_GROUP_SIZE = 64;

        private const int TUBE_VERTEX_STRIDE = 80;
        private const int RING_PARAMS_STRIDE = 144;
        private const int SAMPLE_DATA_STRIDE = 64;

        public BlurTube(int ringVerts, int ringCount, ComputeShader computeShader)
        {
            RingVerts = ringVerts;
            RingCount = ringCount;
            _computeShader = computeShader;
            _kernelIndex = computeShader.FindKernel("CSMain");
            if (_kernelIndex < 0) {
                Debug.LogError("Failed to find CSMain kernel in BlurTubeComputeShader");
            }

            int vertCount = ringVerts * ringCount;
            int stripCount = Math.Max(ringCount - 1, 0);
            int indexCount = ringVerts * stripCount * 6;

            TubeMesh = new Mesh
            {
                indexFormat = vertCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            TubeMesh.MarkDynamic();

            _vertices = new TubeVertex[vertCount];
            _indices = new int[indexCount];

            int t = 0;
            for (int ring = 0; ring < stripCount; ring++)
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

            TubeMesh.SetVertexBufferParams(vertCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4)
            );
            TubeMesh.SetTriangles(_indices, 0, false);

            _vertexBuffer = new ComputeBuffer(vertCount, TUBE_VERTEX_STRIDE);
            _ringParamsBuffer = new ComputeBuffer(Mathf.Max(ringCount, 1), RING_PARAMS_STRIDE);
            _sampleDataBuffer = new ComputeBuffer(16, SAMPLE_DATA_STRIDE);
        }

        public void SetSampleData(GpuSampleData[] sampleData)
        {
            _sampleDataBuffer.SetData(sampleData);
        }

        public void SetRingParams(GpuRingParams[] ringParams)
        {
            _ringParamsBuffer.SetData(ringParams);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RefreshMesh()
        {
            _computeShader.SetInt("_RingVerts", RingVerts);
            _computeShader.SetInt("_RingCount", RingCount);

            _computeShader.SetBuffer(_kernelIndex, "_Samples", _sampleDataBuffer);
            _computeShader.SetBuffer(_kernelIndex, "_Rings", _ringParamsBuffer);
            _computeShader.SetBuffer(_kernelIndex, "_Vertices", _vertexBuffer);

            int totalThreads = RingVerts * RingCount;
            int groups = Mathf.CeilToInt((float)totalThreads / THREAD_GROUP_SIZE);
            _computeShader.Dispatch(_kernelIndex, groups, 1, 1);

            _vertexBuffer.GetData(_vertices);
            TubeMesh.SetVertexBufferData(_vertices, 0, 0, _vertices.Length, 0, MeshUpdateFlags.DontRecalculateBounds);
            TubeMesh.RecalculateBounds();
        }

        public void Destroy()
        {
            _vertexBuffer?.Release();
            _ringParamsBuffer?.Release();
            _sampleDataBuffer?.Release();
            Object.DestroyImmediate(TubeMesh);
        }
    }
}
