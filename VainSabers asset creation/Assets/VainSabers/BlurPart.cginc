#pragma vertex vert
#pragma target 2.0

#include "UnityCG.cginc"

struct appdata_t {
    float4 vertex : POSITION;
    float3 trueNormal : NORMAL;
    float4 planeNormal : TANGENT;  // tangent xyz vector in model space, w is sweepFactor
    float2 uv : TEXCOORD0;
    float4 color  : COLOR;
    float4 bladeDir : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f {
    float4 vertex   : SV_POSITION;
    float2 uv       : TEXCOORD0;
    float4 planeNormal : TEXCOORD1;
    float3 normal   : TEXCOORD2;
    float4 color : TEXCOORD3;
    float3 worldPos  : TEXCOORD4;
    float4 bladeDir : TEXCOORD5;
    float2 uv2 : TEXCOORD6;
    UNITY_VERTEX_OUTPUT_STEREO
};

float _Glow;
float _DepthOffset;

sampler2D _ColorTex;
float4 _ColorTex_ST;
sampler2D _GlowTex;
float4 _GlowTex_ST;
float _ColorTexEnabled;
float _GlowTexEnabled;

v2f vert (appdata_t v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.vertex = UnityObjectToClipPos(v.vertex);
    o.vertex.z += _DepthOffset;
    o.uv = v.uv;

    o.planeNormal = float4(UnityObjectToWorldNormal(v.planeNormal), v.planeNormal.w);
    o.normal = UnityObjectToWorldNormal(v.trueNormal);
    
    o.color = v.color;

    float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.worldPos = worldPos;
    o.bladeDir = float4(UnityObjectToWorldNormal(v.bladeDir.xyz), v.bladeDir.w);

    o.uv2 = v.uv2;

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
static const float _BlurTunableConstant = 3.0;
float _BlurPartIsBlade;
static const float _MotionViewBoost = 1.0;
static const float _MotionViewPower = 2.0;
static const float _MotionViewThreshold = 0.30;
static const float _PlanarCoplanarBoost = 1.0;
static const float _PlanarCoplanarPower = 7.0;
static const float _PlanarCoplanarThreshold = 0.65;

float _RimFactor;
float _RimPower;
float _RimPerpendicular;

// blur goes from 0 to 1
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

    float sweepRatio = vertStage.uv2.y;
    float sweepCoord = vertStage.uv2.x;

    float b = sweepRatio * _BlurTunableConstant * _VainSaberBlurSoftness;
    float a = saturate(b);
    float blurFac = b;

    SaberFragVariables commonVars;
    commonVars.color = vertStage.color;
    commonVars.glowStrength = _Glow * vertStage.color.w;

    commonVars.sweepRatio = 1 - saturate(b);

    commonVars.viewDir = viewDir;
    commonVars.normal = (dot(vertStage.normal, vertStage.normal) > 1e-6)
                        ? normalize(vertStage.normal)
                        : float3(0,0,1);

    float3 N = commonVars.normal;
    float3 V = commonVars.viewDir;

    float3 blade = (dot(vertStage.bladeDir.xyz, vertStage.bladeDir.xyz) > 1e-6)
                   ? normalize(vertStage.bladeDir.xyz)
                   : float3(0,1,0);
    
    float3 Nperp = N - blade * dot(N, blade);
    float nPerpLenSq = dot(Nperp, Nperp);
    Nperp = (nPerpLenSq > 1e-6) ? Nperp * rsqrt(nPerpLenSq) : N;

    float3 Vperp = V - blade * dot(V, blade);
    float vPerpLenSq = dot(Vperp, Vperp);
    Vperp = (vPerpLenSq > 1e-6) ? Vperp * rsqrt(vPerpLenSq) : V;
    float isBlade = saturate(_BlurPartIsBlade);
    float3 Vfinal = normalize(lerp(V, Vperp, isBlade));

    float x = sqrt(saturate(dot(Nperp, Vfinal))) * 4 * (sweepCoord - sweepCoord * sweepCoord);
    // that is super janky but uhh... i think it works

    // Opacity = (1 - saturate(10a) * saturate(1-x)^(2/a) )^2 * 1/((0.5b)^2+1)
    float safeA = max(a, 0.001);
    float powTerm = pow(saturate(1.0 - x), 2.0 / safeA);
    float term = saturate(10.0 * a) * powTerm;
    float opacity = pow(saturate(1.0 - term), 2.0) / ((0.5 * b)*(0.5 * b) + 1.0);
    opacity = saturate(opacity);

    // fix blur when saber moves toward/away from eye
    {
        float3 planeN = vertStage.planeNormal.xyz;
        float lenSq = dot(planeN, planeN);
        float motionView = 0;
        if (lenSq > 1e-6)
        {
            planeN *= rsqrt(lenSq);
            float3 motionDir = cross(blade, planeN);
            float mLenSq = dot(motionDir, motionDir);
            if (mLenSq > 1e-6)
            {
                motionDir *= rsqrt(mLenSq);
                motionView = abs(dot(motionDir, viewDir));
            }
        }
        float motionBiased = saturate((motionView - _MotionViewThreshold) / (1.0 - _MotionViewThreshold));
        float motionP = pow(motionBiased, _MotionViewPower);
        opacity = lerp(opacity, 1.0, motionP * _MotionViewBoost);
    }
    {
        float3 planeN2 = vertStage.planeNormal.xyz;
        float lenSq2 = dot(planeN2, planeN2);
        float planarView = 0;
        if (lenSq2 > 1e-6)
        {
            planeN2 *= rsqrt(lenSq2);
            planarView = abs(dot(planeN2, viewDir));
        }
        float planar = saturate(1.0 - planarView);
        float planarBiased = saturate((planar - _PlanarCoplanarThreshold) / (1.0 - _PlanarCoplanarThreshold));
        float planarP = pow(planarBiased, _PlanarCoplanarPower);
        opacity = lerp(opacity, 1.0, planarP * _PlanarCoplanarBoost);
    }

    opacity *= pow(saturate(vertStage.bladeDir.w), 1.5);
    commonVars.alpha = saturate(opacity);
    
    float fresnelFull = 1.0 - saturate(abs(dot(N, V)));
    float fresnelPerp = 1.0 - saturate(dot(Nperp, Vperp));

    float fresnelTerm = lerp(fresnelFull, fresnelPerp, saturate(_RimPerpendicular));
    fresnelTerm = pow(saturate(fresnelTerm), max(_RimPower, 0.0001));

    commonVars.rimFactor = 1.0 + _RimFactor * fresnelTerm;
    
    float lodBias = blurFac * 8.0 - 1.0;
    float2 texUv = vertStage.uv;

    if (_ColorTexEnabled > 0.5)
    {
        float4 texCol = tex2Dbias(_ColorTex, float4(texUv, 0, lodBias));
        commonVars.color *= texCol.rgb;
        commonVars.alpha *= texCol.a;
    }

    if (_GlowTexEnabled > 0.5)
    {
        float4 texGlow = tex2Dbias(_GlowTex, float4(texUv, 0, lodBias));
        commonVars.glowStrength *= texGlow.r;
    }

    return commonVars;
}
