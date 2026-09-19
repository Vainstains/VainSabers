#pragma vertex vert
#pragma target 3.5
#pragma multi_compile __ _GEOMETRY_SPRITE _GEOMETRY_OBJ
#include "UnityCG.cginc"

#define GPU_HIST_COUNT 16
float4 _HistPos[GPU_HIST_COUNT];
float4 _HistFwd[GPU_HIST_COUNT];
float4 _HistUp[GPU_HIST_COUNT];
int _HistCount; // 16
float _VertexBlurFade;
float _VertexHueShift;
float3 _VertexCustomColor;
float _VertexGlowMul;
float _VertexOpacityMul;
float _VertexEnableRoundedNormals;
float _VertexLength;
float _VertexEnabled; // legacy, unused with keywords
float _VertexGeometry;
float _VertexManualIsZero;

float _VainSaberBlurSoftness;

// new uniforms for sprite/obj GPU path
float2 _VertexSpriteSize; // x=SizeX y=SizeY
float _VertexObjScale;
float3 _VertexObjBoundsMin;
float3 _VertexObjBoundsMax;

// god this is messy
struct appdata_t {
    float4 vertex : POSITION;    // tube: x=zPos y=offX z=offY w=1   obj: xyz=localPos   sprite: x=localX y=localY
    float3 polar : NORMAL;       // tube: x=cosTheta y=sinTheta z=sign  obj: xyz=localNormal  sprite: z=sign
    float4 meta : TANGENT;       // tube: x=radiusSlope y=isZeroFlag z=ringT w=radius
    float4 color  : COLOR;
    float2 uv : TEXCOORD0;
    float4 dataC : TEXCOORD1;    // tube: x=customWeight y=opacity  sprite/obj: x=customWeight y=opacity
    float2 dataD : TEXCOORD2;    // spare
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
float2 _ColorTexAtlasCount;
float3 _ColorTexAtlasSpeedFlip;
float2 _GlowTexAtlasCount;
float3 _GlowTexAtlasSpeedFlip;

float2 ApplyAtlas(float2 uv, float2 atlasCount, float3 atlasSpeedFlip)
{
    float atlasX = atlasCount.x;
    float atlasY = atlasCount.y;
    float speed = atlasSpeedFlip.x;
    float flipX = atlasSpeedFlip.y;
    float flipY = atlasSpeedFlip.z;
    if (atlasX < 1.5 && atlasY < 1.5) return uv;
    atlasX = max(1, atlasX);
    atlasY = max(1, atlasY);
    float count = atlasX * atlasY;
    float frame = floor(fmod(_Time.y * speed, count) + 0.0001);
    float tileX = fmod(frame, atlasX);
    float tileY = floor(frame / atlasX);
    if (flipX > 0.5) tileX = atlasX - 1 - tileX;
    if (flipY > 0.5) tileY = atlasY - 1 - tileY;
    float2 uvAtlas = (uv + float2(tileX, tileY)) / float2(atlasX, atlasY);
    return uvAtlas;
}

float3 RGBToHSV_Unity(float3 c)
{
    float r=c.r, g=c.g, b=c.b;
    float maxC = max(r, max(g,b));
    float minC = min(r, min(g,b));
    float delta = maxC - minC;
    float h=0, s=0, v=maxC;
    if (delta > 0.00001)
    {
        s = delta / maxC;
        if (abs(r - maxC) < 0.00001) h = (g - b) / delta;
        else if (abs(g - maxC) < 0.00001) h = 2.0 + (b - r) / delta;
        else h = 4.0 + (r - g) / delta;
        h /= 6.0;
        if (h < 0) h += 1.0;
    }
    return float3(h,s,v);
}
float3 HSVToRGB_Unity(float3 hsv)
{
    float h=hsv.x*6.0, s=hsv.y, v=hsv.z;
    if (s < 0.00001) return float3(v,v,v);
    float f = frac(h);
    int i = (int)floor(h);
    float p = v*(1.0 - s);
    float q = v*(1.0 - s*f);
    float t = v*(1.0 - s*(1.0 - f));
    i = i % 6;
    if (i==0) return float3(v,t,p);
    else if (i==1) return float3(q,v,p);
    else if (i==2) return float3(p,v,t);
    else if (i==3) return float3(p,q,v);
    else if (i==4) return float3(t,p,v);
    else return float3(v,p,q);
}
float3 ShiftHue(float3 c, float hShift)
{
    if (abs(hShift) < 0.0001) return c;
    float3 hsv = RGBToHSV_Unity(c);
    hsv.x = frac(hsv.x + hShift);
    return HSVToRGB_Unity(hsv);
}

// slerp between two normalized vectors
float3 SlerpNormalized(float3 a, float3 b, float t)
{
    float d = clamp(dot(a,b), -1.0, 1.0);
    float angle = acos(d);
    if (angle < 1e-4) return normalize(lerp(a, b, t));
    float sinA = sin(angle);
    float wA = sin((1.0 - t) * angle) / sinA;
    float wB = sin(t * angle) / sinA;
    return normalize(wA * a + wB * b);
}

v2f vert (appdata_t v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

#if defined(_GEOMETRY_SPRITE)
    // ===== SPRITE GPU PATH =====
    float localX = v.vertex.x;
    float localY = v.vertex.y;
    float signVal = v.polar.z; // 1 or -1 for double sided
    if (abs(signVal) < 0.1) signVal = 1;
    float customWeight = v.dataC.x;
    float baseOpacity = v.dataC.y;
    float2 baseUv = v.uv;
    float4 baseColor = v.color;

    float3 colRgb = lerp(baseColor.rgb, _VertexCustomColor, saturate(customWeight));
    colRgb = ShiftHue(colRgb, _VertexHueShift);
    float glow = baseColor.a * _VertexGlowMul;
    float opacity = baseOpacity * _VertexOpacityMul;
    float4 outColor = float4(colRgb, glow);

    float3 fPos0 = _HistPos[0].xyz;
    float3 fFwd0 = _HistFwd[0].xyz; fFwd0 = length(fFwd0) > 1e-6 ? normalize(fFwd0) : float3(0,0,1);
    float3 fUp0 = _HistUp[0].xyz; fUp0 = length(fUp0) > 1e-6 ? normalize(fUp0) : float3(0,1,0);
    float3 fRight0 = cross(fUp0, fFwd0); fRight0 = length(fRight0) > 1e-6 ? normalize(fRight0) : float3(1,0,0);
    
    float3 lPos0 = _HistPos[_HistCount-1].xyz;
    float3 lFwd0 = _HistFwd[_HistCount-1].xyz; lFwd0 = length(lFwd0) > 1e-6 ? normalize(lFwd0) : float3(0,0,1);
    float3 lUp0 = _HistUp[_HistCount-1].xyz; lUp0 = length(lUp0) > 1e-6 ? normalize(lUp0) : float3(0,1,0);
    float3 lRight0 = cross(lUp0, lFwd0); lRight0 = length(lRight0) > 1e-6 ? normalize(lRight0) : float3(1,0,0);
    
    float3 sumFwd = fFwd0 + lFwd0;
    float3 sumUp = fUp0 + lUp0;
    float3 sumRight = fRight0 + lRight0;
    float3 avgFwd = length(sumFwd) > 1e-6 ? normalize(sumFwd) : fFwd0;
    float3 avgUp = length(sumUp) > 1e-6 ? normalize(sumUp) : fUp0;
    float3 avgRight = length(sumRight) > 1e-6 ? normalize(sumRight) : fRight0;

    float3 motionVec = lPos0 - fPos0;
    float dst = length(motionVec);
    float3 motionDir = dst > 1e-4 ? motionVec / dst : avgRight;
    // keep motionDir roughly perpendicular to avgFwd like tube? For sprite we keep as is for bending, but also ensure not parallel to avgFwd for plane
    // Do not project onto avgFwd plane here to keep original sprite motion direction

    float sweepRatio = _VainSaberBlurSoftness * dst * 50.0;
    float sweepRatioClamped = clamp((sweepRatio * _VertexBlurFade - 0.7) * 0.01, 0.0, 5.0);

    float bendAmount = saturate(sweepRatioClamped);
    float3 bentRight = SlerpNormalized(avgRight, motionDir, bendAmount);
    float3 bentUpRaw = avgUp - bentRight * dot(avgUp, bentRight);
    float3 bentUp = length(bentUpRaw) > 1e-6 ? normalize(bentUpRaw) : avgUp;
    // re-orthogonalize bentRight if needed
    float3 bentPlane = cross(bentRight, bentUp);
    bentPlane = length(bentPlane) > 1e-6 ? normalize(bentPlane) : float3(0,0,1);

    // motion plane for blur (like tube)
    float3 motionDirForPlane = motionVec;
    if (dst > 1e-4) motionDirForPlane /= dst;
    else motionDirForPlane = float3(0,0,1);
    // project motionDir onto plane perpendicular to avgFwd for stable plane
    float3 tmpMotion = motionDirForPlane - avgFwd * dot(motionDirForPlane, avgFwd);
    float tmpLen = length(tmpMotion);
    if (tmpLen > 1e-6) motionDirForPlane = tmpMotion / tmpLen;
    else motionDirForPlane = avgRight;
    float3 plane = cross(motionDirForPlane, avgFwd);
    float planeLen = length(plane);
    if (planeLen > 1e-6) plane /= planeLen;
    else plane = float3(0,0,1);

    float halfX = _VertexSpriteSize.x * 0.5;
    float halfY = _VertexSpriteSize.y * 0.5;
    // avoid zero size
    halfX = max(halfX, 1e-4);
    halfY = max(halfY, 1e-4);
    float dR = dot(bentRight, motionDir);
    float dU = dot(bentUp, motionDir);
    float maxAbs = halfX * abs(dR) + halfY * abs(dU);
    maxAbs = max(maxAbs, 1e-4);
    float3 offset = bentRight * localX + bentUp * localY;
    float dotVal = dot(offset, motionDir);
    float tSample = (dotVal + maxAbs) / (2.0 * maxAbs);
    tSample = saturate(tSample);

    int idx = (int)floor(tSample * (_HistCount - 1) + 0.001);
    idx = clamp(idx, 0, max(_HistCount-1,0));
    float3 sPos = _HistPos[idx].xyz;
    float3 sFwdRaw = _HistFwd[idx].xyz;
    float3 sUpRaw = _HistUp[idx].xyz;
    float3 sFwd = length(sFwdRaw) > 1e-6 ? normalize(sFwdRaw) : float3(0,0,1);
    float3 sUp = length(sUpRaw) > 1e-6 ? normalize(sUpRaw) : float3(0,1,0);
    float3 sRightTmp = cross(sUp, sFwd);
    float3 sRight = length(sRightTmp) > 1e-6 ? normalize(sRightTmp) : float3(1,0,0);

    float3 vertexPos = sPos + offset; // CPU style: sample pos + bent offset (keeps shape rigid)

    // FIX normals: geometric normal is bentPlane * sign, not constant
    float3 localNormal = bentPlane * signVal;
    float3 worldNormal = UnityObjectToWorldNormal(localNormal);
    float3 worldPlane = UnityObjectToWorldNormal(plane);
    float3 worldBlade = UnityObjectToWorldNormal(sFwd);

    o.vertex = UnityObjectToClipPos(float4(vertexPos,1));
    o.vertex.z += _DepthOffset;
    o.uv = baseUv;
    o.planeNormal = float4(worldPlane, 0);
    o.normal = worldNormal;
    o.color = outColor;
    float3 worldPos = mul(unity_ObjectToWorld, float4(vertexPos,1)).xyz;
    o.worldPos = worldPos;
    o.bladeDir = float4(worldBlade, opacity);
    o.uv2 = float2(tSample, sweepRatioClamped);
    return o;

#elif defined(_GEOMETRY_OBJ)
    // ===== OBJ GPU PATH =====
    float3 localPos = v.vertex.xyz * _VertexObjScale;
    float3 localNormal = v.polar.xyz;
    // normalize local normal if needed
    float lnLen = length(localNormal);
    if (lnLen > 1e-6) localNormal /= lnLen; else localNormal = float3(0,0,1);
    float customWeight = v.dataC.x;
    float baseOpacity = v.dataC.y;
    float2 baseUv = v.uv;
    float4 baseColor = v.color;

    float3 colRgb = lerp(baseColor.rgb, _VertexCustomColor, saturate(customWeight));
    colRgb = ShiftHue(colRgb, _VertexHueShift);
    float glow = baseColor.a * _VertexGlowMul;
    float opacity = baseOpacity * _VertexOpacityMul;
    float4 outColor = float4(colRgb, glow);

    float3 fPos0 = _HistPos[0].xyz;
    float3 fFwd0 = _HistFwd[0].xyz; fFwd0 = length(fFwd0) > 1e-6 ? normalize(fFwd0) : float3(0,0,1);
    float3 fUp0 = _HistUp[0].xyz; fUp0 = length(fUp0) > 1e-6 ? normalize(fUp0) : float3(0,1,0);
    float3 fRight0 = cross(fUp0, fFwd0); fRight0 = length(fRight0) > 1e-6 ? normalize(fRight0) : float3(1,0,0);
    
    float3 lPos0 = _HistPos[_HistCount-1].xyz;
    float3 lFwd0 = _HistFwd[_HistCount-1].xyz; lFwd0 = length(lFwd0) > 1e-6 ? normalize(lFwd0) : float3(0,0,1);
    float3 lUp0 = _HistUp[_HistCount-1].xyz; lUp0 = length(lUp0) > 1e-6 ? normalize(lUp0) : float3(0,1,0);
    float3 lRight0 = cross(lUp0, lFwd0); lRight0 = length(lRight0) > 1e-6 ? normalize(lRight0) : float3(1,0,0);
    
    float3 sumFwd = fFwd0 + lFwd0;
    float3 sumUp = fUp0 + lUp0;
    float3 sumRight = fRight0 + lRight0;
    float3 avgFwd = length(sumFwd) > 1e-6 ? normalize(sumFwd) : fFwd0;
    float3 avgUp = length(sumUp) > 1e-6 ? normalize(sumUp) : fUp0;
    float3 avgRight = length(sumRight) > 1e-6 ? normalize(sumRight) : fRight0;

    float3 motionVec = lPos0 - fPos0;
    float dst = length(motionVec);
    float3 motionDir = dst > 1e-4 ? motionVec / dst : avgRight;
    float sweepRatio = _VainSaberBlurSoftness * dst * 50.0;
    float sweepRatioClamped = clamp((sweepRatio * _VertexBlurFade - 0.7) * 0.05, 0.0, 20.0);

    float3 motionDirForPlane = motionVec;
    if (dst > 1e-4) motionDirForPlane /= dst;
    else motionDirForPlane = float3(0,0,1);
    motionDirForPlane = motionDirForPlane - avgFwd * dot(motionDirForPlane, avgFwd);
    float mdLen = length(motionDirForPlane);
    if (mdLen > 1e-6) motionDirForPlane /= mdLen;
    else motionDirForPlane = avgRight;
    float3 plane = cross(motionDirForPlane, avgFwd);
    float planeLen = length(plane);
    if (planeLen > 1e-6) plane /= planeLen;
    else plane = float3(0,0,1);

    // compute tSample using avg offset and bounds
    float3 offsetAvg = avgRight * localPos.x + avgUp * localPos.y + avgFwd * localPos.z;
    float dR = dot(avgRight, motionDir);
    float dU = dot(avgUp, motionDir);
    float dF = dot(avgFwd, motionDir);
    // min/max dot using bounds
    float3 bMin = _VertexObjBoundsMin * _VertexObjScale;
    float3 bMax = _VertexObjBoundsMax * _VertexObjScale;
    float minDot = (dR >= 0 ? dR * bMin.x : dR * bMax.x) + (dU >= 0 ? dU * bMin.y : dU * bMax.y) + (dF >= 0 ? dF * bMin.z : dF * bMax.z);
    float maxDot = (dR >= 0 ? dR * bMax.x : dR * bMin.x) + (dU >= 0 ? dU * bMax.y : dU * bMin.y) + (dF >= 0 ? dF * bMax.z : dF * bMin.z);
    float range = maxDot - minDot;
    float tSample;
    if (abs(range) < 1e-4) tSample = 0.5;
    else {
        float dotVal = dot(offsetAvg, motionDir);
        tSample = (dotVal - minDot) / range;
        tSample = saturate(tSample);
    }

    int idx = (int)floor(tSample * (_HistCount - 1) + 0.001);
    idx = clamp(idx, 0, max(_HistCount-1,0));
    float3 sPos = _HistPos[idx].xyz;
    float3 sFwdRaw = _HistFwd[idx].xyz;
    float3 sUpRaw = _HistUp[idx].xyz;
    float3 sFwd = length(sFwdRaw) > 1e-6 ? normalize(sFwdRaw) : float3(0,0,1);
    float3 sUp = length(sUpRaw) > 1e-6 ? normalize(sUpRaw) : float3(0,1,0);
    float3 sRightTmp = cross(sUp, sFwd);
    float3 sRight = length(sRightTmp) > 1e-6 ? normalize(sRightTmp) : float3(1,0,0);

    float3 vertexPos = sPos + offsetAvg; // keep CPU style rigid offset

    // FIX normals: rotate localNormal by sample basis (sRight/up/fwd)
    float3 localNormalRotated = localNormal.x * sRight + localNormal.y * sUp + localNormal.z * sFwd;
    float lnRotLen = length(localNormalRotated);
    if (lnRotLen > 1e-6) localNormalRotated /= lnRotLen;

    float3 worldNormal = UnityObjectToWorldNormal(localNormalRotated);
    float3 worldPlane = UnityObjectToWorldNormal(plane);
    float3 worldBlade = UnityObjectToWorldNormal(sFwd);

    o.vertex = UnityObjectToClipPos(float4(vertexPos,1));
    o.vertex.z += _DepthOffset;
    o.uv = baseUv;
    o.planeNormal = float4(worldPlane, 0);
    o.normal = worldNormal;
    o.color = outColor;
    float3 worldPos = mul(unity_ObjectToWorld, float4(vertexPos,1)).xyz;
    o.worldPos = worldPos;
    o.bladeDir = float4(worldBlade, opacity);
    o.uv2 = float2(tSample, sweepRatioClamped);
    return o;

#else
    // ===== TUBE GPU PATH (default) =====
    float zPos = v.vertex.x;
    float offX = v.vertex.y;
    float offY = v.vertex.z;
    float cosTheta = v.polar.x;
    float sinTheta = v.polar.y;
    float signVal = v.polar.z;
    float radiusSlope = v.meta.x;
    float isZeroFlag = v.meta.y;
    float ringT = v.meta.z;
    float radiusAbs = v.meta.w; // abs radius
    float customWeight = v.dataC.x;
    float baseOpacity = v.dataC.y;
    float2 baseUv = v.uv;
    float4 baseColor = v.color; // rgb = base, a = base glow

    float3 colRgb = lerp(baseColor.rgb, _VertexCustomColor, saturate(customWeight));
    colRgb = ShiftHue(colRgb, _VertexHueShift);
    float glow = baseColor.a * _VertexGlowMul;
    float opacity = baseOpacity * _VertexOpacityMul;
    float4 outColor = float4(colRgb, glow);
    
    float3 fPos0 = _HistPos[0].xyz;
    float3 fFwd0 = _HistFwd[0].xyz; fFwd0 = length(fFwd0) > 1e-6 ? normalize(fFwd0) : float3(0,0,1);
    float3 fUp0 = _HistUp[0].xyz; fUp0 = length(fUp0) > 1e-6 ? normalize(fUp0) : float3(0,1,0);
    float3 fRight0 = cross(fUp0, fFwd0); fRight0 = length(fRight0) > 1e-6 ? normalize(fRight0) : float3(1,0,0);
    
    float3 lPos0 = _HistPos[_HistCount-1].xyz;
    float3 lFwd0 = _HistFwd[_HistCount-1].xyz; lFwd0 = length(lFwd0) > 1e-6 ? normalize(lFwd0) : float3(0,0,1);
    float3 lUp0 = _HistUp[_HistCount-1].xyz; lUp0 = length(lUp0) > 1e-6 ? normalize(lUp0) : float3(0,1,0);
    float3 lRight0 = cross(lUp0, lFwd0); lRight0 = length(lRight0) > 1e-6 ? normalize(lRight0) : float3(1,0,0);
    
    float3 firstCenter = fPos0 + fFwd0 * zPos + fRight0 * offX + fUp0 * offY;
    float3 lastCenter = lPos0 + lFwd0 * zPos + lRight0 * offX + lUp0 * offY;

    float3 motionVec = lastCenter - firstCenter;
    float dst = length(motionVec);

    float3 sumFwd = fFwd0 + lFwd0;
    float3 sumUp = fUp0 + lUp0;
    float3 sumRight = fRight0 + lRight0;
    float3 avgFwd = length(sumFwd) > 1e-6 ? normalize(sumFwd) : fFwd0;
    float3 avgUp = length(sumUp) > 1e-6 ? normalize(sumUp) : fUp0;
    float3 avgRight = length(sumRight) > 1e-6 ? normalize(sumRight) : fRight0;

    float3 motionDir = motionVec;
    if (dst > 1e-4) motionDir /= dst;
    else motionDir = float3(0,0,1);
    motionDir = motionDir - avgFwd * dot(motionDir, avgFwd);
    float mdLen = length(motionDir);

    if (mdLen > 1e-6) motionDir /= mdLen;
    else motionDir = avgRight;

    float3 plane = cross(motionDir, avgFwd);
    float planeLen = length(plane);
    if (planeLen > 1e-6) plane /= planeLen;
    else plane = float3(0,0,1);

    float sweepRatio = 0;
    {
        sweepRatio = _VainSaberBlurSoftness * 1.5 * dst / (0.035 * sqrt(radiusAbs + 0.04));
    }
    float sweepRatioClamped = clamp((sweepRatio * _VertexBlurFade - 0.7) * 0.01, 0.0, 5.0);

    // per-vertex offset direction in avg plane
    float3 offsetDir = signVal * cosTheta * avgUp + sinTheta * avgRight;
    float d = dot(offsetDir, motionDir);

    d *= abs(d);
    float tSample = (d + 1.0) * 0.5;
    tSample = saturate(tSample);

    int idx = (int)floor(tSample * (_HistCount - 1) + 0.001);
    idx = clamp(idx, 0, max(_HistCount-1,0));
    float3 sPos = _HistPos[idx].xyz;
    float3 sFwdRaw = _HistFwd[idx].xyz;
    float3 sUpRaw = _HistUp[idx].xyz;

    float3 sFwd = length(sFwdRaw) > 1e-6 ? normalize(sFwdRaw) : float3(0,0,1);
    float3 sUp = length(sUpRaw) > 1e-6 ? normalize(sUpRaw) : float3(0,1,0);
    float3 sRightTmp = cross(sUp, sFwd);
    float3 sRight = length(sRightTmp) > 1e-6 ? normalize(sRightTmp) : float3(1,0,0);

    float3 ringCenter = sPos + sFwd * zPos + sRight * offX + sUp * offY;
    float3 vertexPos = ringCenter + offsetDir * (isZeroFlag > 0.5 ? 0 : radiusAbs);

    float3 normal = signVal * offsetDir;

    if (_VertexEnableRoundedNormals > 0.5)
    {
        float3 adj = 0;
        if (isZeroFlag > 0.5)
        {
            float len = max(_VertexLength, 1e-4);
            float t = 2*(zPos/len)-1;

            float v1 = 0.12 * sign(t) * pow(abs(t), 9);
            float t2 = t*0.99;
            float v2 = sign(t2) * pow(abs(t2), 171);
            adj = avgFwd * (2*(v1+v2));
        }
        else
        {
            adj = avgFwd * (-radiusSlope);
        }
        normal += adj;
    }

    float3 worldNormal = UnityObjectToWorldNormal(normal);
    float3 worldPlane = UnityObjectToWorldNormal(plane);
    float3 worldBlade = UnityObjectToWorldNormal(sFwd);

    o.vertex = UnityObjectToClipPos(float4(vertexPos,1));
    o.vertex.z += _DepthOffset;
    o.uv = baseUv;
    o.planeNormal = float4(worldPlane, 0);
    o.normal = worldNormal;
    o.color = outColor;
    float3 worldPos = mul(unity_ObjectToWorld, float4(vertexPos,1)).xyz;
    o.worldPos = worldPos;
    o.bladeDir = float4(worldBlade, opacity);
    o.uv2 = float2(tSample, sweepRatioClamped);
    return o;
#endif
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

static const float _BlurTunableConstant = 2.0;
float _BlurPartIsBlade;
static const float _MotionViewBoost = 1.0;
static const float _MotionViewPower = 3.0;
static const float _MotionViewThreshold = 0.60;
static const float _PlanarCoplanarBoost = 1.0;
static const float _PlanarCoplanarPower = 10.0;
static const float _PlanarCoplanarThreshold = 0.7;
static const float _OppositeSideFade = 1.0;
static const float _OppositeSideSharpness = 0.9;

float _RimFactor;
sampler2D _RimPowerGradient;
float _RimPerpendicular;
sampler2D _GlowAddendGradient;
sampler2D _OpacityAddendGradient;

SaberFragVariables GetCommonSaberVars(v2f vertStage)
{
#ifdef USING_STEREO_MATRICES
    float3 worldSpaceCameraPos = unity_StereoWorldSpaceCameraPos[unity_StereoEyeIndex].xyz;
#else
    float3 worldSpaceCameraPos = _WorldSpaceCameraPos.xyz;
#endif
    float3 viewDelta = worldSpaceCameraPos - vertStage.worldPos;
    float viewDeltaLenSq = dot(viewDelta, viewDelta);
    float3 viewDir = (viewDeltaLenSq > 1e-6) ? normalize(viewDelta) : float3(0,0,1);

    float sweepRatio = vertStage.uv2.y;
    float sweepCoord = vertStage.uv2.x;

    float b = sweepRatio * _BlurTunableConstant * _VainSaberBlurSoftness;
    float a = saturate(b);
    float blurFac = b;

    SaberFragVariables commonVars;
    commonVars.color = vertStage.color.rgb;
    commonVars.glowStrength = _Glow * vertStage.color.a;

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

    float safeA = max(a, 0.001);
    float powTerm = pow(saturate(1.0 - x), 2.0 / safeA);
    float term = saturate(10.0 * a) * powTerm;
    float opacity = pow(saturate(1.0 - term), 2.0) / ((0.5 * b)*(0.5 * b) + 1.0);
    opacity = saturate(opacity);
    float rawOpacity = opacity;
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
    {
        float3 planeN3 = vertStage.planeNormal.xyz;
        float lenSq3 = dot(planeN3, planeN3);
        if (lenSq3 > 1e-6 && a > 0.01)
        {
            planeN3 *= rsqrt(lenSq3);
            float normalSide = dot(N, planeN3);
            float cameraSide = dot(viewDir, planeN3);
            float opposite = saturate(-normalSide * cameraSide * _OppositeSideSharpness);
            float motionFade = saturate(a * 25);
            opposite *= motionFade;
            float bladeMask = saturate(_BlurPartIsBlade + 0.1);
            opacity *= lerp(1.0, saturate(1.0 - opposite * _OppositeSideFade), bladeMask);
        }
    }

    // the above fixes look good in most cases at *low speeds*, but
    // it seems at high speeds it works against the blur's interest.
    // genius solution: remove correction when faster so it only fixes
    // cases where it's needed. (magic numbers go brrr)
    opacity = lerp(opacity, rawOpacity, saturate(sweepRatio * 0.8 - 0.3));



    opacity *= pow(saturate(vertStage.bladeDir.w), 1.5);
    commonVars.alpha = saturate(opacity);
    
    float fresnelFull = 1.0 - saturate(abs(dot(N, V)));
    float fresnelPerp = 1.0 - saturate(dot(Nperp, Vperp));

    float fresnelRaw = lerp(fresnelFull, fresnelPerp, saturate(_RimPerpendicular));
    float gradientLodBias = blurFac * 4.0;
    float fresnelTerm = tex2Dbias(_RimPowerGradient, float4(saturate(fresnelRaw), 0.5, 0, gradientLodBias)).r;

    commonVars.rimFactor = 1.0 + fresnelTerm;
    // Angle-mapped glow / opacity addends (sampled with same fresnelRaw)
    float glowAddend = tex2Dbias(_GlowAddendGradient, float4(saturate(fresnelRaw), 0.5, 0, gradientLodBias)).r;
    float opacityAddend = tex2Dbias(_OpacityAddendGradient, float4(saturate(fresnelRaw), 0.5, 0, gradientLodBias)).r;
    commonVars.glowStrength += glowAddend;
    commonVars.alpha = saturate(commonVars.alpha + opacityAddend);
    
    float lodBias = blurFac * 8.0 - 1.0;
    float2 texUv = vertStage.uv;

    if (_ColorTexEnabled > 0.5)
    {
        float2 uvAtlas = ApplyAtlas(texUv, _ColorTexAtlasCount, _ColorTexAtlasSpeedFlip);
        float4 texCol = tex2Dbias(_ColorTex, float4(uvAtlas, 0, lodBias));
        commonVars.color *= texCol.rgb;
        commonVars.alpha *= texCol.a;
    }

    if (_GlowTexEnabled > 0.5)
    {
        float2 uvAtlasGlow = ApplyAtlas(texUv, _GlowTexAtlasCount, _GlowTexAtlasSpeedFlip);
        float4 texGlow = tex2Dbias(_GlowTex, float4(uvAtlasGlow, 0, lodBias));
        commonVars.glowStrength *= texGlow.r * texGlow.a;
    }

    return commonVars;
}
