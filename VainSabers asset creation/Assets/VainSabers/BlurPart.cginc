#pragma vertex vert
#pragma target 2.0

#include "UnityCG.cginc"

struct appdata_t {
    float4 vertex : POSITION;
    float3 trueNormal : NORMAL;
    float4 planeNormal : TANGENT;  // tangent xyz vector in model space, w is sweepFactor
    float2 uv : TEXCOORD0;
    float4 color  : COLOR;
    float3 bladeDir : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f {
    float4 vertex   : SV_POSITION;
    float2 uv       : TEXCOORD0;
    float4 planeNormal : TEXCOORD1;
    float3 normal   : TEXCOORD2;
    float4 color : TEXCOORD3;
    float3 worldPos  : TEXCOORD4;
    float3 bladeDir : TEXCOORD5;
};

float _Glow;
float _DepthOffset;

v2f vert (appdata_t v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);

    // standard position + uv
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.vertex.z += _DepthOffset;
    o.uv = v.uv;

    // world normal
    o.planeNormal = float4(UnityObjectToWorldNormal(v.planeNormal), v.planeNormal.w);
    o.normal = UnityObjectToWorldNormal(v.trueNormal);
    
    o.color = v.color;

    // compute view direction (fragment → camera)
    float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.worldPos = worldPos;
    o.bladeDir = UnityObjectToWorldNormal(v.bladeDir);

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
};

#define MINIMUM_EDGE_SOFTNESS 0.05



SaberFragVariables GetCommonSaberVars(v2f vertStage)
{
    // _Color: The color of the blade
    // _HandleColor: The color of the handle
    // _BlurAmount: How much the saber sweep is in motion and should blur
    // viewGraze: How much of a grazing angle (to the saber sweep plane)
    // sweepPosition: The horizontal position in the sweep
    // glowFactor: How much the blade should glow
    // whiteFactor: How much the blade color is blended to solid white
    // fadeFactor: How much the blade should fade out when in motion
    // bladeFactor: How much this fragment is the blade or the handle (0 is handle, 1 is blade)
    // edgeSoftness: The percent from the edge to the middle that the "blurry" part of the fade takes up
    
    // Safe viewDir
    float3 viewDelta = _WorldSpaceCameraPos.xyz - vertStage.worldPos;
    float viewDeltaLenSq = dot(viewDelta, viewDelta);
    float3 viewDir = (viewDeltaLenSq > 1e-6) ? normalize(viewDelta) : float3(0,0,1);

    // Sweep factor
    float sweepFactor = vertStage.uv.y;

    // Distance to edge
    float distanceToEdge = min(vertStage.uv.x * 2.0, 2.0 - 2.0 * vertStage.uv.x);
    distanceToEdge += 0.1 / max(sweepFactor, 0.01);
    distanceToEdge *= 2;

    // Safe plane normal
    float3 planeNormal = (dot(vertStage.planeNormal.xyz, vertStage.planeNormal.xyz) > 1e-6)
                         ? normalize(vertStage.planeNormal.xyz)
                         : float3(0,0,1);

    // Blur strength (avoid pow of 0)
    float3 blade = normalize(vertStage.bladeDir);
    float3 viewDirTangent = normalize(viewDir - blade * dot(viewDir, blade));
    float blurStrength = saturate(2*abs(dot(planeNormal, viewDirTangent))-0.3);
    
    // Build return vars
    SaberFragVariables commonVars;
    commonVars.color = vertStage.color;
    commonVars.glowStrength = _Glow * vertStage.color.w;
    commonVars.sweepRatio = 1 - sweepFactor;

    // Alpha calculation with clamp
    float denom = max(sweepFactor, 0.01);
    commonVars.alpha = saturate(distanceToEdge * distanceToEdge * distanceToEdge / (1.4 * denom));
    commonVars.alpha = lerp(1.0, commonVars.alpha, blurStrength);
    commonVars.alpha /= denom + 1;
    commonVars.alpha *= 1.1;
    commonVars.alpha = saturate(commonVars.alpha);
    // Safe normals
    commonVars.viewDir = viewDir;
    commonVars.normal = (dot(vertStage.normal, vertStage.normal) > 1e-6)
                        ? normalize(vertStage.normal)
                        : float3(0,0,1);

    return commonVars;
}
