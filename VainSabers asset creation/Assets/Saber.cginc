#pragma vertex vert
#pragma target 2.0

#include "UnityCG.cginc"

struct appdata_t {
    float4 vertex : POSITION;
    float2 uv     : TEXCOORD0;
    float3 trueNormal : NORMAL;
    float4 normal : TANGENT;
    float4 color  : COLOR;     // blade direction (RGB compressed)
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f {
    float4 vertex   : SV_POSITION;
    float2 uv       : TEXCOORD0;
    float3 sweepPlaneNormal   : TEXCOORD1;
    float3 trueNormal   : TEXCOORD2;
    float4 bladeExtra : TEXCOORD3;
    float3 worldPos  : TEXCOORD4;
};

float4 _Color;
float4 _Color1;
float4 _HandleColor;
float _BlurAmount;

v2f vert (appdata_t v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);

    // standard position + uv
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = v.uv;

    // world normal
    o.sweepPlaneNormal = UnityObjectToWorldNormal(v.normal);
    o.trueNormal = UnityObjectToWorldNormal(v.trueNormal);
    // decode blade direction from vertex color
    o.bladeExtra = v.color;

    // compute view direction (fragment → camera)
    float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.worldPos = worldPos;

    return o;
}

struct SaberFragVariables {
    float3 bladeColor;
    float3 handleColor;

    float bladeColorFactor;
    float motionFactor;
    float glowStrength;
    float alpha;
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
    
    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - vertStage.worldPos);
    
    float viewGraze = saturate(1.0 - abs(dot(viewDir, vertStage.sweepPlaneNormal)));
    float sweepPosition = lerp(0.5, vertStage.uv.x, saturate(_BlurAmount * 8 - 1));
    float glowFactor = vertStage.uv.y;
    float whiteFactor = vertStage.bladeExtra.x;
    float fadeFactor = vertStage.bladeExtra.y;
    float secondaryFactor = vertStage.bladeExtra.w;
    float bladeFactor = saturate(glowFactor * 100);
    float edgeSoftness = max(vertStage.bladeExtra.z, MINIMUM_EDGE_SOFTNESS) * _BlurAmount;

    float viewFactor = max(0.0, 1.0 - viewGraze * viewGraze * viewGraze * 1.5);
    viewFactor *= viewFactor;
    
    float alpha = (1.0 - abs(2.0 * sweepPosition - 1.0)) / max(edgeSoftness, MINIMUM_EDGE_SOFTNESS);
    if (edgeSoftness < MINIMUM_EDGE_SOFTNESS)
        alpha = 1.0 - (1.0 - alpha) * edgeSoftness / MINIMUM_EDGE_SOFTNESS;
    alpha = lerp(1.0f, alpha, viewFactor);
    
    alpha *= lerp(1.0, (1.0 - fadeFactor * _BlurAmount), bladeFactor);
    if (_BlurAmount < 0.1)
        alpha = 1.0;
    float glow = lerp(0.0, glowFactor, bladeFactor);

    float3 bladeColor = lerp(lerp(_Color.xyz, _Color1.xyz, secondaryFactor), float3(1.0, 1.0, 1.0), whiteFactor);

    SaberFragVariables commonVars;

    commonVars.bladeColor = bladeColor;
    commonVars.handleColor = _HandleColor;
    commonVars.bladeColorFactor = bladeFactor;
    commonVars.motionFactor = _BlurAmount;
    commonVars.glowStrength = glow;
    commonVars.alpha = clamp(alpha * alpha, 0.0f, 1.0f);
    
    return commonVars;
}
