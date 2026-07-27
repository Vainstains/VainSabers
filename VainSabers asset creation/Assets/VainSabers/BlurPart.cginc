#pragma vertex vert
#pragma target 3.0

#include "UnityCG.cginc"

struct RingVertex {
    float4 position;
    float4 normal;
    float4 tangent;
    float4 color;
    float4 uv;
    float4 bladeDir;
};

StructuredBuffer<RingVertex> _RingVertices;
int _RingVerts;
int _RingCount;

struct v2f {
    float4 vertex   : SV_POSITION;
    float2 uv       : TEXCOORD0;
    float4 planeNormal : TEXCOORD1;
    float3 normal   : TEXCOORD2;
    float4 color : TEXCOORD3;
    float3 worldPos  : TEXCOORD4;
    float4 bladeDir : TEXCOORD5;
    UNITY_VERTEX_OUTPUT_STEREO
};

float _Glow;
float _DepthOffset;

v2f vert (uint vid : SV_VertexID)
{
    v2f o;
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    uint rv = (uint)_RingVerts;

    uint quadIndex = vid / 6;
    uint vertexInQuad = vid % 6;
    uint ringIdx = quadIndex / rv;
    uint vertIdx = quadIndex % rv;
    uint nextVertIdx = (vertIdx + 1) % rv;

    uint idxA = ringIdx * rv + vertIdx;
    uint idxB = ringIdx * rv + nextVertIdx;
    uint idxC = (ringIdx + 1) * rv + vertIdx;
    uint idxD = (ringIdx + 1) * rv + nextVertIdx;

    uint readIdx;
    if (vertexInQuad == 0) readIdx = idxA;
    else if (vertexInQuad == 1) readIdx = idxC;
    else if (vertexInQuad == 2) readIdx = idxB;
    else if (vertexInQuad == 3) readIdx = idxB;
    else if (vertexInQuad == 4) readIdx = idxC;
    else readIdx = idxD;

    RingVertex rv_data = _RingVertices[readIdx];

    float3 objectPos = rv_data.position.xyz;
    o.vertex = UnityObjectToClipPos(float4(objectPos, 1.0));
    o.vertex.z += _DepthOffset;
    o.uv = rv_data.uv.xy;

    o.planeNormal = float4(UnityObjectToWorldNormal(rv_data.tangent.xyz), rv_data.tangent.w);
    o.normal = UnityObjectToWorldNormal(rv_data.normal.xyz);

    o.color = rv_data.color;

    float3 worldPos = mul(unity_ObjectToWorld, float4(objectPos, 1.0)).xyz;
    o.worldPos = worldPos;
    o.bladeDir = float4(UnityObjectToWorldNormal(rv_data.bladeDir.xyz), rv_data.bladeDir.w);

    return o;
}

struct SaberFragVariables {
    float3 color;
    float glowStrength;
    float alpha;
    float blur;
    float3 viewDir;
    float3 normal;
    float sweepRatio;
    float rimFactor;
};

#define MINIMUM_EDGE_SOFTNESS 0.05

float _VainSaberBlurSoftness;

float _RimFactor;
float _RimPower;
float _RimPerpendicular;

float getFresnelBlurFadeFactor(float x, float blur)
{
    float p = 100.0 / (blur * blur + 0.005);
    float base = max(1.0, 1.0 - 1.1 * pow(x, p));
    return base * base * base;
}

SaberFragVariables GetCommonSaberVars(v2f vertStage)
{
    float3 viewDelta = _WorldSpaceCameraPos.xyz - vertStage.worldPos;
    float viewDeltaLenSq = dot(viewDelta, viewDelta);
    float3 viewDir = (viewDeltaLenSq > 1e-6) ? normalize(viewDelta) : float3(0,0,1);

    float sweepFactor = vertStage.uv.y * 1.5 * _VainSaberBlurSoftness;
    float blurFac = sweepFactor;

    float distanceToEdge = min(vertStage.uv.x * 2.0, 2.0 - 2.0 * vertStage.uv.x);
    distanceToEdge += 0.1 / max(sweepFactor, 0.01);
    distanceToEdge *= 2;

    float3 planeNormal = (dot(vertStage.planeNormal.xyz, vertStage.planeNormal.xyz) > 1e-6)
                         ? normalize(vertStage.planeNormal.xyz)
                         : float3(0,0,1);

    float3 blade = normalize(vertStage.bladeDir);
    float3 motionDirRaw = cross(blade, planeNormal);
    float3 motionDir = (dot(motionDirRaw, motionDirRaw) > 1e-6)
                       ? normalize(motionDirRaw)
                       : float3(0,0,1);

    float blurStrength = saturate(1.3 - 1.5 * abs(dot(motionDir, viewDir)));

    SaberFragVariables commonVars;
    commonVars.color = vertStage.color;
    commonVars.glowStrength = _Glow * vertStage.color.w;
    commonVars.sweepRatio = 1 - sweepFactor;

    float denom = max(sweepFactor, 0.01);
    commonVars.alpha = saturate(distanceToEdge * distanceToEdge / (1.9 * denom));
    commonVars.alpha = 1.0 - blurStrength * (commonVars.alpha - 1.0) * (commonVars.alpha - 1.0);
    commonVars.alpha /= denom + 1;
    commonVars.alpha *= 1.1;
    commonVars.alpha = saturate(commonVars.alpha);
    commonVars.alpha *= pow(vertStage.bladeDir.w, 1.5);
    commonVars.alpha = saturate(commonVars.alpha);
    commonVars.viewDir = viewDir;
    commonVars.normal = (dot(vertStage.normal, vertStage.normal) > 1e-6)
                        ? normalize(vertStage.normal)
                        : float3(0,0,1);

    float3 N = commonVars.normal;
    float3 V = commonVars.viewDir;

    float fresnelFull = 1.0 - saturate(abs(dot(N, V)));
    commonVars.alpha *= saturate(getFresnelBlurFadeFactor(fresnelFull, blurFac));

    float3 Nperp = N - blade * dot(N, blade);
    float3 Vperp = V - blade * dot(V, blade);
    float nPerpLenSq = dot(Nperp, Nperp);
    float vPerpLenSq = dot(Vperp, Vperp);
    Nperp = (nPerpLenSq > 1e-6) ? Nperp * rsqrt(nPerpLenSq) : N;
    Vperp = (vPerpLenSq > 1e-6) ? Vperp * rsqrt(vPerpLenSq) : V;
    float fresnelPerp = 1.0 - saturate(dot(Nperp, Vperp));

    float fresnelTerm = lerp(fresnelFull, fresnelPerp, saturate(_RimPerpendicular));
    fresnelTerm = pow(saturate(fresnelTerm), max(_RimPower, 0.0001));

    commonVars.rimFactor = 1.0 + _RimFactor * fresnelTerm;

    return commonVars;
}
