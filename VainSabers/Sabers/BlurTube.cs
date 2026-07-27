using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace VainSabers.Sabers
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RingVertex
    {
        public Vector4 position;
        public Vector4 normal;
        public Vector4 tangent;
        public Vector4 color;
        public Vector4 uv;
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
        public int RingVerts { get; private set; }
        public int RingCount { get; private set; }
        public ComputeBuffer RingVertexBuffer { get; private set; }
        public int TriangleVertexCount => RingVerts * Math.Max(RingCount - 1, 0) * 6;

        private readonly ComputeShader _computeShader;
        private readonly int _kernelIndex;
        private readonly ComputeBuffer _ringParamsBuffer;
        private readonly ComputeBuffer _sampleDataBuffer;

        private const int THREAD_GROUP_SIZE = 64;
        private const int RING_VERTEX_STRIDE = 96;
        private const int RING_PARAMS_STRIDE = 144;
        private const int SAMPLE_DATA_STRIDE = 64;

        public BlurTube(int ringVerts, int ringCount, ComputeShader computeShader)
        {
            RingVerts = ringVerts;
            RingCount = ringCount;
            _computeShader = computeShader;
            _kernelIndex = computeShader.FindKernel("CSMain");
            if (_kernelIndex < 0)
            {
                Debug.LogError("Failed to find CSMain kernel in BlurTubeComputeShader");
            }

            int vertCount = ringVerts * ringCount;
            RingVertexBuffer = new ComputeBuffer(vertCount, RING_VERTEX_STRIDE);
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
        public void Dispatch()
        {
            _computeShader.SetInt("_RingVerts", RingVerts);
            _computeShader.SetInt("_RingCount", RingCount);

            _computeShader.SetBuffer(_kernelIndex, "_Samples", _sampleDataBuffer);
            _computeShader.SetBuffer(_kernelIndex, "_Rings", _ringParamsBuffer);
            _computeShader.SetBuffer(_kernelIndex, "_RingVertices", RingVertexBuffer);

            int totalThreads = RingVerts * RingCount;
            int groups = Mathf.CeilToInt((float)totalThreads / THREAD_GROUP_SIZE);
            _computeShader.Dispatch(_kernelIndex, groups, 1, 1);
        }

        public void Destroy()
        {
            RingVertexBuffer?.Release();
            _ringParamsBuffer?.Release();
            _sampleDataBuffer?.Release();
        }
    }
}
