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

    // Sweep factor from uv2 (sweepCoord = x, sweepRatio = y)
    float sweepCoord = vertStage.uv2.x;
    float sweepRatio = vertStage.uv2.y;
    float sweepFactor = sweepRatio * 1.5 * _VainSaberBlurSoftness;
    float blurFac = sweepFactor;

    // Distance to edge (uv.x = ring angular position, 0-1)
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
    commonVars.alpha *= pow(vertStage.bladeDir.w, 1.5); // looks better with ^1.5 i think
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
    
    float lodBias = blurFac * 8.0 - 4.0;
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
