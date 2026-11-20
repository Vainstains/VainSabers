#pragma vertex vert
#pragma target 3.5
#include "UnityCG.cginc"

#define GPU_HIST_COUNT 8
float4 _HistPos[GPU_HIST_COUNT];
float4 _HistFwd[GPU_HIST_COUNT];
float4 _HistUp[GPU_HIST_COUNT];
float _VertexBlurFade;
float _VertexIsMenuPointer;
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
    float2 dataD : TEXCOORD2;    // x marks the OBJ sweep mesh
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f {
    float4 vertex   : SV_POSITION;
    float2 uv       : TEXCOORD0;
    float3 motionDir : TEXCOORD1;
    float3 normal   : TEXCOORD2;
    float4 color : TEXCOORD3;
    float3 worldPos  : TEXCOORD4;
    float4 bladeDir : TEXCOORD5;
    float3 exposure : TEXCOORD6; // age, travel in widths, position along the sweep
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
    
    float3 lPos0 = _HistPos[GPU_HIST_COUNT - 1].xyz;
    float3 lFwd0 = _HistFwd[GPU_HIST_COUNT - 1].xyz; lFwd0 = length(lFwd0) > 1e-6 ? normalize(lFwd0) : float3(0,0,1);
    float3 lUp0 = _HistUp[GPU_HIST_COUNT - 1].xyz; lUp0 = length(lUp0) > 1e-6 ? normalize(lUp0) : float3(0,1,0);
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
    float softness = saturate(_VainSaberBlurSoftness);
    float bendAmount = saturate(dst * 0.5 * max(0.0, _VertexBlurFade) * softness);
    // Align the sprite axis to the motion without flipping it when the swing reverses.
    float3 alignedMotion = dot(avgRight, motionDir) < 0.0 ? -motionDir : motionDir;
    float3 bentRight = NormalizeOr(lerp(avgRight, alignedMotion, bendAmount), avgRight);
    float3 bentUp = NormalizeOr(avgUp - bentRight * dot(avgUp, bentRight), avgUp);
    float3 bentPlane = NormalizeOr(cross(bentRight, bentUp), avgFwd);

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
    float travel = dst * max(0.0, _VertexBlurFade) / max(2.0 * maxAbs, 0.004);
    float tSample = (dotVal + maxAbs) / (2.0 * maxAbs);
    tSample = saturate(tSample);

    float3 sPos, sFwd, sUp;
    SampleHistory(tSample, sPos, sFwd, sUp);

    float3 vertexPos = sPos + offset; // CPU style: sample pos + bent offset (keeps shape rigid)

    // FIX normals: geometric normal is bentPlane * sign, not constant
    float3 localNormal = bentPlane * signVal;
    float3 worldNormal = UnityObjectToWorldNormal(localNormal);
    float3 worldBlade = UnityObjectToWorldNormal(sFwd);

    o.vertex = UnityObjectToClipPos(float4(vertexPos,1));
    o.vertex.z += _DepthOffset;
    o.uv = baseUv;
    o.motionDir = UnityObjectToWorldDir(motionDir);
    o.normal = worldNormal;
    o.color = outColor;
    float3 worldPos = mul(unity_ObjectToWorld, float4(vertexPos,1)).xyz;
    o.worldPos = worldPos;
    o.bladeDir = float4(worldBlade, opacity);
    o.exposure = float3(tSample, travel, 0.0);
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
    
    float3 lPos0 = _HistPos[GPU_HIST_COUNT - 1].xyz;
    float3 lFwd0 = _HistFwd[GPU_HIST_COUNT - 1].xyz; lFwd0 = length(lFwd0) > 1e-6 ? normalize(lFwd0) : float3(0,0,1);
    float3 lUp0 = _HistUp[GPU_HIST_COUNT - 1].xyz; lUp0 = length(lUp0) > 1e-6 ? normalize(lUp0) : float3(0,1,0);
    float3 lRight0 = cross(lUp0, lFwd0); lRight0 = length(lRight0) > 1e-6 ? normalize(lRight0) : float3(1,0,0);
    
    float3 boundsMin = _VertexObjBoundsMin * _VertexObjScale;
    float3 boundsMax = _VertexObjBoundsMax * _VertexObjScale;
    float3 center = 0.5 * (boundsMin + boundsMax);
    float3 extent = 0.5 * abs(boundsMax - boundsMin);
    float3 currentCenter = fPos0 + fRight0 * center.x + fUp0 * center.y + fFwd0 * center.z;
    float3 previousCenter = lPos0 + lRight0 * center.x + lUp0 * center.y + lFwd0 * center.z;
    float3 view = ViewDirectionInPart(currentCenter, fUp0);

    // Use one exposure strength for the entire object, including pure rotation.
    float3 motion = ProjectOnViewPlane(previousCenter - currentCenter, view);
    float3 rightMotion = ProjectOnViewPlane(lRight0 - fRight0, view) * extent.x;
    float3 upMotion = ProjectOnViewPlane(lUp0 - fUp0, view) * extent.y;
    float3 forwardMotion = ProjectOnViewPlane(lFwd0 - fFwd0, view) * extent.z;
    if (dot(rightMotion, rightMotion) > dot(motion, motion)) motion = rightMotion;
    if (dot(upMotion, upMotion) > dot(motion, motion)) motion = upMotion;
    if (dot(forwardMotion, forwardMotion) > dot(motion, motion)) motion = forwardMotion;

    float3 axis = NormalizeOr(motion, fRight0);
    float3 localAxis = float3(dot(axis, fRight0), dot(axis, fUp0), dot(axis, fFwd0));
    float width = max(2.0 * dot(abs(localAxis), extent), 0.004);
    float travel = length(motion) / width * max(0.0, _VertexBlurFade);
    float3 vertexPos = fPos0 + fRight0 * localPos.x + fUp0 * localPos.y + fFwd0 * localPos.z;
    float3 normal = localNormal.x * fRight0 + localNormal.y * fUp0 + localNormal.z * fFwd0;
    float3 modelForward = fFwd0;
    float age = 0.0;
    float along = -1.0; // The actual OBJ surface stays at the current pose.

    if (v.dataD.x > 0.5)
    {
        if (travel * saturate(_VainSaberBlurSoftness) < 0.035)
        {
            o.vertex = float4(0.0, 0.0, 0.0, -1.0);
            return o;
        }

        float3 lengthAxis = v.polar;
        float3 oldPos = lPos0 + lRight0 * localPos.x + lUp0 * localPos.y + lFwd0 * localPos.z;
        float3 averageAxis = NormalizeOr(
            (fRight0 + lRight0) * lengthAxis.x + (fUp0 + lUp0) * lengthAxis.y
            + (fFwd0 + lFwd0) * lengthAxis.z, fFwd0);
        float3 side = cross(view, averageAxis);
        float sideLengthSq = dot(side, side);
        side *= rsqrt(max(1e-8, sideLengthSq));
        float3 screenMotion = ProjectOnViewPlane(oldPos - vertexPos, view);
        float3 lateralMotion = lerp(screenMotion, side * dot(screenMotion, side),
                                    smoothstep(0.0, 0.01, sideLengthSq));
        float distance = length(lateralMotion);
        float3 sweepDir = NormalizeOr(lateralMotion, fRight0);
        float3 localSide = float3(dot(sweepDir, fRight0), dot(sweepDir, fUp0), dot(sweepDir, fFwd0));
        float radius = length(localSide * v.meta.xyz * _VertexObjScale);
        travel = distance * max(0.0, _VertexBlurFade) / max(2.0 * radius, 0.006);
        age = v.uv.x;
        along = v.uv.y;
        float3 sPos, sFwd, sUp;
        SampleHistory(age, sPos, sFwd, sUp);
        float3 sRight = NormalizeOr(cross(sUp, sFwd), fRight0);
        sUp = cross(sFwd, sRight);
        vertexPos = sPos + sRight * localPos.x + sUp * localPos.y + sFwd * localPos.z
                  + sweepDir * radius * (2.0 * age - 1.0);
        normal = view;
        modelForward = sFwd;

        if (_ColorTexEnabled > 0.5)
        {
            // Give the sweep the model's albedo without copying its surface detail.
            float2 tintUV = ApplyAtlas(float2(0.5, 0.5), _ColorTexAtlasCount, _ColorTexAtlasSpeedFlip);
            float mip = any(_ColorTexAtlasCount > 1.5) ? 2.0 : 7.0;
            outColor.rgb *= tex2Dlod(_ColorTex, float4(tintUV, 0, mip)).rgb;
        }
    }

    float3 worldNormal = UnityObjectToWorldNormal(normal);
    float3 worldBlade = UnityObjectToWorldNormal(modelForward);

    o.vertex = UnityObjectToClipPos(float4(vertexPos,1));
    o.vertex.z += _DepthOffset;
    o.uv = baseUv;
    o.normal = worldNormal;
    o.color = outColor;
    float3 worldPos = mul(unity_ObjectToWorld, float4(vertexPos,1)).xyz;
    o.worldPos = worldPos;
    o.bladeDir = float4(worldBlade, opacity);
    o.exposure = float3(age, travel, along);
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
    float3 lastCenter = lPos0 + lFwd0 * zPos + lRight0 * offX + lUp0 * offY;

    float3 motionVec = lastCenter - firstCenter;

    float3 sumFwd = fFwd0 + lFwd0;
    float3 sumUp = fUp0 + lUp0;
    float3 sumRight = fRight0 + lRight0;
    float3 avgFwd = length(sumFwd) > 1e-6 ? normalize(sumFwd) : fFwd0;
    float3 avgUp = length(sumUp) > 1e-6 ? normalize(sumUp) : fUp0;
    float3 avgRight = length(sumRight) > 1e-6 ? normalize(sumRight) : fRight0;

    float3 transverseMotion = motionVec - avgFwd * dot(motionVec, avgFwd);
    float transverseDistance = length(transverseMotion);
    // Apparent travel along the blade mostly overlaps its current silhouette.
    // Use lateral travel to spread the tube and keep its middle dense on a depth swing.
    float3 view = ViewDirectionInPart(firstCenter, avgUp);
    float3 screenSide = cross(view, avgFwd);
    float sideLengthSq = dot(screenSide, screenSide);
    screenSide *= rsqrt(max(sideLengthSq, 1e-8));
    float signedTravel = dot(transverseMotion, screenSide);
    // When the blade points at the lens, it has no stable long axis in the image.
    float3 screenMotion = ProjectOnViewPlane(transverseMotion, view);
    float sideWeight = smoothstep(0.0, 0.01, sideLengthSq);
    float3 sweep = lerp(screenMotion, screenSide * signedTravel, sideWeight);
    float projectedDistance = length(sweep);
    float travel = projectedDistance / max(2.0 * radiusAbs, 0.012);
    float sweepAmount = smoothstep(0.05, 0.75, travel);

    float3 currentOffset = signVal * cosTheta * fUp0 + sinTheta * fRight0;
    float3 averageOffset = signVal * cosTheta * avgUp + sinTheta * avgRight;
    float3 offsetDir = NormalizeOr(lerp(currentOffset, averageOffset, sweepAmount), currentOffset);
    // Parameterize the visible width along the same axis as the exposure.
    // This keeps the mesh and the fade aligned on fast, oblique swings.
    float3 sweepDir = projectedDistance > 1e-4 ? sweep / projectedDistance : avgRight;
    float tSample = saturate(0.5 + 0.5 * dot(offsetDir, sweepDir));

    float3 sPos, sFwd, sUp;
    float transverseFraction = saturate(transverseDistance / max(length(motionVec), 1e-4));
    SampleHistory(tSample * transverseFraction * sweepAmount, sPos, sFwd, sUp);
    float3 sRight = NormalizeOr(cross(sUp, sFwd), avgRight);
    sUp = cross(sFwd, sRight);

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
#if defined(_GEOMETRY_SPRITE)
    float3 motionDir = NormalizeOr(vertStage.motionDir, float3(1, 0, 0));
    float viewMotion = dot(motionDir, V);
    projectedTravel *= sqrt(saturate(1.0 - viewMotion * viewMotion));
#endif
    float motionSurface = 0.0;
#if defined(_GEOMETRY_OBJ)
    motionSurface = step(0.0, vertStage.exposure.z);
    float blurMix = smoothstep(0.035, 0.57, projectedTravel);
#else
    float blurMix = smoothstep(0.12, 1.4, projectedTravel);
#endif
    float age = saturate(vertStage.exposure.x);
    float coverage = rsqrt(1.0 + 1.8 * projectedTravel);
    float edgeWidth = lerp(0.19, 0.40, saturate(projectedTravel * 0.18));
    float pixelWidth = fwidth(age);

#if defined(_GEOMETRY_SPRITE)
    float shutter = smoothstep(0.0, max(edgeWidth, pixelWidth), min(1.4 * age, 1.0 - age));
    float2 border = min(vertStage.uv, 1.0 - vertStage.uv);
    float2 endWidth = max(float2(0.085, 0.085), fwidth(vertStage.uv));
    float2 endFade = smoothstep(0.0, endWidth, border);
    coverage *= endFade.x * endFade.y;
#else
    // Keep the leading fade narrow as the sweep grows, so it stays joined to the current pose.
    float leadingWidth = clamp(0.42 / (1.0 + projectedTravel), 0.023, 0.16);
    float leading = smoothstep(-0.25 * pixelWidth, max(leadingWidth, 1.35 * pixelWidth), age);
    float trailing = smoothstep(0.0, max(edgeWidth, pixelWidth), 1.0 - age);
    float pointerShutter = smoothstep(0.0, max(edgeWidth, pixelWidth), min(1.4 * age, 1.0 - age));
    float shutter = lerp(leading * trailing, pointerShutter, _VertexIsMenuPointer);
    float nearBlade = 1.0 - smoothstep(leadingWidth, max(0.10, 4.0 * leadingWidth), age);
    coverage = lerp(coverage, max(coverage, 0.24), nearBlade * blurMix * (1.0 - _VertexIsMenuPointer));

    float along = saturate(vertStage.exposure.z);
    float pixelLength = fwidth(along);
    float endWidth = lerp(0.07, 0.16, saturate(projectedTravel * 0.18));
    coverage *= smoothstep(0.0, max(endWidth * 0.45, pixelLength), along)
                * smoothstep(0.0, max(endWidth, pixelLength), 1.0 - along);
#endif
    coverage *= shutter * (1.0 - 0.55 * age * age);

#if defined(_GEOMETRY_OBJ)
    float exposureCoverage = motionSurface > 0.5 ? coverage * blurMix : lerp(1.0, 0.64, blurMix);
#else
    float exposureCoverage = lerp(1.0, coverage, blurMix);
#endif

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
