#pragma vertex vert
#pragma target 3.5
#include "UnityCG.cginc"

#define GPU_HIST_COUNT 8
float4 _HistPos[GPU_HIST_COUNT];
float4 _HistFwd[GPU_HIST_COUNT];
float4 _HistUp[GPU_HIST_COUNT];
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
float3 _VertexBladeAxis;

float _VainSaberBlurSoftness;

// new uniforms for sprite/obj GPU path
float3 _VertexSpriteSize; // width, height, double sided
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
    float2 dataD : TEXCOORD2;    // x marks the exposure strip
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f {
    float4 vertex   : SV_POSITION;
    float2 uv       : TEXCOORD0;
    float3 normal   : TEXCOORD2;
    float4 color : TEXCOORD3;
    float3 worldPos  : TEXCOORD4;
    float4 bladeDir : TEXCOORD5;
    float3 exposure : TEXCOORD6; // age, travel in widths, position along the sweep
    float4 shutter : TEXCOORD7; // lateral coordinate, signed travel, aperture radius, weight
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

float3 NormalizeOr(float3 v, float3 fallback)
{
    float lengthSq = dot(v, v);
    return lengthSq > 1e-8 ? v * rsqrt(lengthSq) : fallback;
}

float3 ViewDirectionInPart(float3 position, float3 fallback)
{
#ifdef USING_STEREO_MATRICES
    float3 cameraPos = 0.5 * (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]);
#else
    float3 cameraPos = _WorldSpaceCameraPos;
#endif
    float3 viewVector = mul(unity_WorldToObject, float4(cameraPos, 1.0)).xyz - position;
    if (unity_OrthoParams.w > 0.5)
        viewVector = mul((float3x3)unity_WorldToObject, UNITY_MATRIX_V[2].xyz);
    return NormalizeOr(viewVector, fallback);
}

float3 ProjectOnViewPlane(float3 motion, float3 view)
{
    return motion - view * dot(motion, view);
}

// The CPU provides evenly timed poses. Interpolate here so nearby vertices
// cannot snap to different poses at the same history bin boundary.
void SampleHistory(float age, out float3 pos, out float3 fwd, out float3 up)
{
    float sampleIndex = saturate(age) * (GPU_HIST_COUNT - 1);
    int newer = min((int)sampleIndex, GPU_HIST_COUNT - 2);
    int older = newer + 1;
    float weight = sampleIndex - newer;

    pos = lerp(_HistPos[newer].xyz, _HistPos[older].xyz, weight);
    fwd = NormalizeOr(lerp(_HistFwd[newer].xyz, _HistFwd[older].xyz, weight), _HistFwd[newer].xyz);
    up = NormalizeOr(lerp(_HistUp[newer].xyz, _HistUp[older].xyz, weight), _HistUp[newer].xyz);
}

#include "TemporalExposure.cginc"

void WriteExposureStrip(inout v2f o, ExposurePath path, float radius, float2 uv,
                        float4 color, float opacity, float3 forward)
{
    float across;
    float3 position = ExposureStripPosition(path, radius, uv.x, across);
    o.vertex = UnityObjectToClipPos(float4(position, 1.0));
    o.vertex.z += _DepthOffset;
    o.worldPos = mul(unity_ObjectToWorld, float4(position, 1.0)).xyz;
    o.normal = UnityObjectToWorldNormal(path.view);
    o.bladeDir = float4(UnityObjectToWorldNormal(forward), opacity);
    o.color = color;
    o.uv = float2(0.5, uv.y);
    o.exposure = float3(0.0, abs(path.distance) / max(2.0 * radius, 0.006), uv.y);
    o.shutter = float4(across, path.distance, max(radius, 0.0001), path.weight);
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
    
    bool vertical = _VertexSpriteSize.y >= _VertexSpriteSize.x;
    float3 axis = vertical ? float3(0, 1, 0) : float3(1, 0, 0);
    float radius = 0.5 * min(_VertexSpriteSize.x, _VertexSpriteSize.y);
    float halfLength = 0.5 * max(_VertexSpriteSize.x, _VertexSpriteSize.y);
    if (v.dataD.x > 0.5)
    {
        ExposurePath path = GetExposurePath(v.vertex.xyz, axis, radius, 0.0);
        if (_VertexSpriteSize.z < 0.5 && dot(path.view, fFwd0) < 0.0)
            path.weight = 0.0;
        WriteExposureStrip(o, path, radius, baseUv, outColor, opacity, fFwd0);
        o.uv = vertical ? float2(0.5, 1.0 - baseUv.y) : float2(baseUv.y, 0.5);
    }
    else
    {
        ExposurePath path = GetExposurePath(float3(0, 0, 0), axis, radius, halfLength);
        float3 position = fPos0 + fRight0 * localX + fUp0 * localY;
        o.vertex = UnityObjectToClipPos(float4(position, 1.0));
        o.vertex.z += _DepthOffset;
        o.worldPos = mul(unity_ObjectToWorld, float4(position, 1.0)).xyz;
        o.normal = UnityObjectToWorldNormal(fFwd0 * signVal);
        o.bladeDir = float4(UnityObjectToWorldNormal(fFwd0), opacity);
        o.color = outColor;
        o.uv = baseUv;
        o.shutter = float4(0.0, 0.0, -1.0, path.weight);
    }
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
    
    float3 boundsMin = _VertexObjBoundsMin * _VertexObjScale;
    float3 boundsMax = _VertexObjBoundsMax * _VertexObjScale;
    float3 center = 0.5 * (boundsMin + boundsMax);
    float3 extent = 0.5 * abs(boundsMax - boundsMin);
    bool longZ = extent.z >= max(extent.x, extent.y);
    float3 axis = longZ ? float3(0, 0, 1)
                : (extent.x > extent.y ? float3(1, 0, 0) : float3(0, 1, 0));
    float halfLength = dot(axis, extent);
    float3 crossExtent = extent * (1.0 - axis);
    float radius = max(crossExtent.x, max(crossExtent.y, crossExtent.z));
    if (v.dataD.x > 0.5)
    {
        ExposurePath path = GetExposurePath(localPos, axis, radius, 0.0);
        float3 localSide = float3(dot(path.side, fRight0), dot(path.side, fUp0), dot(path.side, fFwd0));
        radius = length(localSide * crossExtent);
        if (_ColorTexEnabled > 0.5)
        {
            float2 tintUV = ApplyAtlas(float2(0.5, 0.5), _ColorTexAtlasCount, _ColorTexAtlasSpeedFlip);
            float mip = any(_ColorTexAtlasCount > 1.5) ? 2.0 : 7.0;
            outColor.rgb *= tex2Dlod(_ColorTex, float4(tintUV, 0, mip)).rgb;
        }
        WriteExposureStrip(o, path, radius, baseUv, outColor, opacity, fFwd0);
    }
    else
    {
        ExposurePath path = GetExposurePath(center, axis, radius, halfLength);
        float3 position = fPos0 + fRight0 * localPos.x + fUp0 * localPos.y + fFwd0 * localPos.z;
        float3 normal = localNormal.x * fRight0 + localNormal.y * fUp0 + localNormal.z * fFwd0;
        o.vertex = UnityObjectToClipPos(float4(position, 1.0));
        o.vertex.z += _DepthOffset;
        o.worldPos = mul(unity_ObjectToWorld, float4(position, 1.0)).xyz;
        o.normal = UnityObjectToWorldNormal(normal);
        o.bladeDir = float4(UnityObjectToWorldNormal(fFwd0), opacity);
        o.color = outColor;
        o.uv = baseUv;
        o.exposure.z = -1.0;
        o.shutter = float4(0.0, 0.0, -1.0, path.weight);
    }
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
    
    float3 lPos0 = _HistPos[GPU_HIST_COUNT - 1].xyz;
    float3 lFwd0 = _HistFwd[GPU_HIST_COUNT - 1].xyz; lFwd0 = length(lFwd0) > 1e-6 ? normalize(lFwd0) : float3(0,0,1);
    float3 lUp0 = _HistUp[GPU_HIST_COUNT - 1].xyz; lUp0 = length(lUp0) > 1e-6 ? normalize(lUp0) : float3(0,1,0);
    float3 lRight0 = cross(lUp0, lFwd0); lRight0 = length(lRight0) > 1e-6 ? normalize(lRight0) : float3(1,0,0);
    
    float3 firstCenter = fPos0 + fFwd0 * zPos + fRight0 * offX + fUp0 * offY;
    float3 currentOffset = signVal * cosTheta * fUp0 + sinTheta * fRight0;
    float lastZ = _VertexBladeAxis.x + _VertexLength * _VertexBladeAxis.y;
    bool analyticBlade = _VertexBladeAxis.z > 0.5 && max(_VertexBladeAxis.x, lastZ) > 0.05;
    bool stripVertex = v.dataD.x > 0.5;
    if (!analyticBlade && stripVertex)
    {
        o.vertex = float4(0.0, 0.0, 0.0, -1.0);
        return o;
    }
    float3 ringCenter = firstCenter;
    float3 offsetDir = currentOffset;
    float3 sFwd = fFwd0;
    float3 avgFwd = fFwd0;
    float tSample = 0.0;
    float travel = 0.0;

    if (analyticBlade)
    {
        float radius = isZeroFlag > 0.5 ? 0.0 : radiusAbs;
        ExposurePath path = GetExposurePath(float3(offX, offY, zPos), float3(0, 0, 1), radiusAbs, 0.0);
        if (stripVertex)
        {
            WriteExposureStrip(o, path, radius, baseUv, outColor, opacity, fFwd0);
            o.exposure.z = ringT;
            return o;
        }
        o.shutter = float4(0.0, 0.0, -1.0, path.weight);
    }
    else
    {
        // The menu laser and standalone hilt parts use their original tube renderer.
        float3 lastCenter = lPos0 + lFwd0 * zPos + lRight0 * offX + lUp0 * offY;
        float3 motionVec = lastCenter - firstCenter;
        avgFwd = NormalizeOr(fFwd0 + lFwd0, fFwd0);
        float3 avgUp = NormalizeOr(fUp0 + lUp0, fUp0);
        float3 avgRight = NormalizeOr(fRight0 + lRight0, fRight0);
        float3 transverseMotion = motionVec - avgFwd * dot(motionVec, avgFwd);
        float3 view = ViewDirectionInPart(firstCenter, avgUp);
        float3 screenSide = cross(view, avgFwd);
        float sideLengthSq = dot(screenSide, screenSide);
        screenSide *= rsqrt(max(sideLengthSq, 1e-8));
        float3 screenMotion = ProjectOnViewPlane(transverseMotion, view);
        float3 sweep = lerp(screenMotion, screenSide * dot(transverseMotion, screenSide),
                            smoothstep(0.0, 0.01, sideLengthSq));
        float projectedDistance = length(sweep);
        travel = projectedDistance / max(2.0 * radiusAbs, 0.012);
        float sweepAmount = smoothstep(0.05, 0.75, travel);
        float3 averageOffset = signVal * cosTheta * avgUp + sinTheta * avgRight;
        offsetDir = NormalizeOr(lerp(currentOffset, averageOffset, sweepAmount), currentOffset);
        float3 sweepDir = projectedDistance > 1e-4 ? sweep / projectedDistance : avgRight;
        tSample = saturate(0.5 + 0.5 * dot(offsetDir, sweepDir));
        float transverseFraction = saturate(length(transverseMotion) / max(length(motionVec), 1e-4));
        float3 sPos, sUp;
        SampleHistory(tSample * transverseFraction * sweepAmount, sPos, sFwd, sUp);
        float3 sRight = NormalizeOr(cross(sUp, sFwd), avgRight);
        sUp = cross(sFwd, sRight);
        ringCenter = sPos + sFwd * zPos + sRight * offX + sUp * offY;
    }

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
    float3 worldBlade = UnityObjectToWorldNormal(sFwd);

    o.vertex = UnityObjectToClipPos(float4(vertexPos,1));
    o.vertex.z += _DepthOffset;
    o.uv = baseUv;
    o.normal = worldNormal;
    o.color = outColor;
    float3 worldPos = mul(unity_ObjectToWorld, float4(vertexPos,1)).xyz;
    o.worldPos = worldPos;
    o.bladeDir = float4(worldBlade, opacity);
    o.exposure = float3(tSample, travel * max(0.0, _VertexBlurFade), ringT);
    return o;
#endif
}

struct SaberFragVariables {
    float3 color;
    float glowStrength;
    float alpha;
    float3 viewDir;
    float3 normal;
    float rimFactor;
    float motionSurface;
};

sampler2D _RimPowerGradient;
float _RimPerpendicular;
sampler2D _GlowAddendGradient;
sampler2D _OpacityMultiplierGradient;

#if !defined(_GEOMETRY_OBJ) && !defined(_GEOMETRY_SPRITE)
float LegacyTubeExposure(v2f vertStage, float projectedTravel, float blurMix)
{
    float age = saturate(vertStage.exposure.x);
    float coverage = rsqrt(1.0 + 1.8 * projectedTravel);
    float edgeWidth = lerp(0.19, 0.40, saturate(projectedTravel * 0.18));
    float shutter = smoothstep(0.0, max(edgeWidth, fwidth(age)), min(1.4 * age, 1.0 - age));
    float along = vertStage.exposure.z;
    float pixelLength = fwidth(along);
    float endWidth = lerp(0.07, 0.16, saturate(projectedTravel * 0.18));
    coverage *= shutter * (1.0 - 0.55 * age * age)
              * smoothstep(0.0, max(endWidth * 0.45, pixelLength), along)
              * smoothstep(0.0, max(endWidth, pixelLength), 1.0 - along);
    return lerp(1.0, coverage, blurMix);
}
#endif

SaberFragVariables GetCommonSaberVars(v2f vertStage)
{
#ifdef USING_STEREO_MATRICES
    float3 worldSpaceCameraPos = unity_StereoWorldSpaceCameraPos[unity_StereoEyeIndex].xyz;
#else
    float3 worldSpaceCameraPos = _WorldSpaceCameraPos.xyz;
#endif
    float3 V = NormalizeOr(worldSpaceCameraPos - vertStage.worldPos, float3(0, 0, 1));
    float3 N = NormalizeOr(vertStage.normal, float3(0, 0, 1));
    float3 blade = NormalizeOr(vertStage.bladeDir.xyz, float3(0, 0, 1));
    float3 Nperp = NormalizeOr(N - blade * dot(N, blade), N);
    float3 Vperp = NormalizeOr(V - blade * dot(V, blade), V);
    float projectedTravel = vertStage.exposure.y * saturate(_VainSaberBlurSoftness);
    float motionSurface = 0.0;
#if defined(_GEOMETRY_OBJ)
    motionSurface = step(0.0, vertStage.exposure.z);
#endif
    float blurMix = smoothstep(0.12, 1.4, projectedTravel);
    float exposureCoverage;
    if (vertStage.shutter.z > 0.0)
    {
        float along = vertStage.exposure.z;
        float edge = max(0.055, 1.5 * fwidth(along));
        float endFade = smoothstep(0.0, edge, min(along, 1.0 - along));
        exposureCoverage = ExposureCoverage(vertStage.shutter) * endFade;
    }
#if !defined(_GEOMETRY_OBJ) && !defined(_GEOMETRY_SPRITE)
    else if (vertStage.shutter.z == 0.0)
        exposureCoverage = LegacyTubeExposure(vertStage, projectedTravel, blurMix);
#endif
    else
        exposureCoverage = 1.0 - vertStage.shutter.w;

    if (vertStage.shutter.z != 0.0)
        clip(exposureCoverage - 1e-4);

    float fresnelFull = 1.0 - saturate(abs(dot(N, V)));
    float fresnelPerp = 1.0 - saturate(dot(Nperp, Vperp));
    float fresnelRaw = lerp(fresnelFull, fresnelPerp, saturate(_RimPerpendicular));
    float2 gradientUV = float2(fresnelRaw, 0.5);

    SaberFragVariables commonVars;
    commonVars.color = vertStage.color.rgb;
    commonVars.motionSurface = motionSurface;
    commonVars.viewDir = V;
    commonVars.normal = N;
    commonVars.rimFactor = 1.0 + tex2D(_RimPowerGradient, gradientUV).r * lerp(1.0, 0.40, blurMix);
    commonVars.glowStrength = _Glow * vertStage.color.a + tex2D(_GlowAddendGradient, gradientUV).r;
#if !defined(_GEOMETRY_OBJ) && !defined(_GEOMETRY_SPRITE)
    if (vertStage.shutter.z > 0.0)
        commonVars.glowStrength *= 1.0 + 0.22 * saturate(_VainSaberBlurSoftness);
#endif
    float baseOpacity = saturate(vertStage.bladeDir.w);
    float opacity = baseOpacity * sqrt(baseOpacity) * exposureCoverage;
    commonVars.alpha = saturate(opacity * tex2D(_OpacityMultiplierGradient, gradientUV).r);

    float lodBias = min(2.5, 0.65 * sqrt(projectedTravel));
    float2 texUv = vertStage.uv;

    if (motionSurface < 0.5 && _ColorTexEnabled > 0.5)
    {
        float2 uvAtlas = ApplyAtlas(texUv, _ColorTexAtlasCount, _ColorTexAtlasSpeedFlip);
        float4 texCol = tex2Dbias(_ColorTex, float4(uvAtlas, 0, any(_ColorTexAtlasCount > 1.5) ? 0.0 : lodBias));
        commonVars.color *= texCol.rgb;
        commonVars.alpha *= texCol.a;
    }

    if (motionSurface < 0.5 && _GlowTexEnabled > 0.5)
    {
        float2 uvAtlasGlow = ApplyAtlas(texUv, _GlowTexAtlasCount, _GlowTexAtlasSpeedFlip);
        float4 texGlow = tex2Dbias(_GlowTex, float4(uvAtlasGlow, 0, any(_GlowTexAtlasCount > 1.5) ? 0.0 : lodBias));
        commonVars.glowStrength *= texGlow.r * texGlow.a;
    }

    return commonVars;
}
