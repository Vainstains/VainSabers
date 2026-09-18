Shader "Unlit/vs_flatglow_2side"
{
    Properties
        {
            _ColorBoost ("RGB Multiplier", Range(0,4)) = 1
            _GlowBoost  ("Glow (Alpha) Multiplier", Range(0,4)) = 1
            _DepthOffset ("Depth Offset", Float) = 0
            _CustomColor ("Custom Color", Color) = (1,1,1,1)
            _ColorTex ("Albedo + Alpha", 2D) = "white" {}
            _GlowTex ("Glow", 2D) = "white" {}
            _ColorTexEnabled ("Color Texture Enabled", Float) = 0
            _GlowTexEnabled ("Glow Texture Enabled", Float) = 0
            _ColorTexAtlasCount ("Color Atlas Count", Vector) = (1,1,0,0)
            _ColorTexAtlasSpeedFlip ("Color Atlas Speed+Flips", Vector) = (1,0,0,0)
            _GlowTexAtlasCount ("Glow Atlas Count", Vector) = (1,1,0,0)
            _GlowTexAtlasSpeedFlip ("Glow Atlas Speed+Flips", Vector) = (1,0,0,0)
            _NoiseTex ("Noise 3D", 3D) = "white" {}
            _NoiseIntensity ("Noise Intensity", Float) = 0
            _NoiseScale ("Noise Scale", Float) = 1
            _NoiseSpeed ("Noise Speed", Float) = 0
        }
    
        SubShader
        {
            Tags { "Queue"="Transparent+180" "RenderType"="Transparent" "IgnoreProjector"="True" }
            LOD 100
            Cull Off
            ZWrite Off
    
            // -------- Pass 1: RGB only --------
            Pass
            {
                Name "RGB"
                Blend SrcAlpha OneMinusSrcAlpha
                ColorMask RGB
    
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.5
                #include "UnityCG.cginc"
    
                float _ColorBoost;
                float _DepthOffset;
                float4 _CustomColor;
                float _CustomBlend;
                sampler2D _ColorTex;
                float _ColorTexEnabled;
                float2 _ColorTexAtlasCount;
                float3 _ColorTexAtlasSpeedFlip;
                sampler3D _NoiseTex;
                float _NoiseIntensity;
                float _NoiseScale;
                float _NoiseSpeed;

                float _TrailDuration;

                // GPU history for shader-based ribbon
                #define TRAIL_HIST_COUNT 32
                float4 _TrailHistPos[TRAIL_HIST_COUNT];
                float4 _TrailHistFwd[TRAIL_HIST_COUNT];
                float4 _TrailHistUp[TRAIL_HIST_COUNT];
                int _TrailHistCount;
                float4 _TrailLocalOffset;
                float _TrailBaseFraction;
                float _TrailOpacityScale;
     
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
                    return (uv + float2(tileX, tileY)) / float2(atlasX, atlasY);
                }

                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv    : TEXCOORD0;
                    float4 color  : COLOR;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };
     
                struct v2f
                {
                    float4 pos   : SV_POSITION;
                    float2 uv    : TEXCOORD0;
                    fixed4 color : COLOR0;
                    UNITY_VERTEX_OUTPUT_STEREO
                };
     
                v2f vert (appdata v)
                {
                    v2f o;
                    UNITY_SETUP_INSTANCE_ID(v);
                    UNITY_INITIALIZE_OUTPUT(v2f, o);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                    float t = v.uv.x;
                    float vFrac = v.uv.y;

                    // Shader-based history path
                    if (_TrailHistCount > 0)
                    {
                        float histF = t * (_TrailHistCount - 1);
                        int idx = (int)floor(histF);
                        idx = clamp(idx, 0, _TrailHistCount - 2);
                        float frac = histF - (float)idx;
                        float3 sPos = lerp(_TrailHistPos[idx].xyz, _TrailHistPos[idx+1].xyz, frac);
                        float3 sFwd = normalize(lerp(_TrailHistFwd[idx].xyz, _TrailHistFwd[idx+1].xyz, frac));
                        float3 sUp = normalize(lerp(_TrailHistUp[idx].xyz, _TrailHistUp[idx+1].xyz, frac));
                        // handle zero vectors fallback
                        if (length(sFwd) < 0.0001) sFwd = float3(0,0,1);
                        if (length(sUp) < 0.0001) sUp = float3(0,1,0);
                        float3 sRight = normalize(cross(sUp, sFwd));
                        if (length(sRight) < 0.0001) sRight = float3(1,0,0);
                        sUp = normalize(cross(sFwd, sRight));

                        float4 localOff = _TrailLocalOffset;
                        float baseFrac = _TrailBaseFraction;
                        float3 tipLocal = localOff.xyz;
                        float3 baseLocal = tipLocal * baseFrac;
                        float3 tipWorld = sPos + sRight * tipLocal.x + sUp * tipLocal.y + sFwd * tipLocal.z;
                        float3 baseWorld = sPos + sRight * baseLocal.x + sUp * baseLocal.y + sFwd * baseLocal.z;
                        float3 worldPos = lerp(baseWorld, tipWorld, vFrac);

                        float noiseFactor = t * _NoiseIntensity;
                        if (noiseFactor > 0.0001)
                        {
                            float scroll = _Time.y * _NoiseSpeed - t * 0.1 * _TrailDuration;
                            float3 noiseCoord = worldPos * _NoiseScale * 0.03125 + float3(scroll,scroll,scroll) * 0.2;
                            float4 n = tex3Dlod(_NoiseTex, float4(noiseCoord, 0));
                            float3 dispWorld = (n.rgb * 2.0 - 1.0) * noiseFactor;
                            worldPos += dispWorld;
                        }
                        o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1));
                        o.pos.z += _DepthOffset * o.pos.w;
                        o.uv = v.uv;
                        o.color = v.color;
                        o.color.a *= _TrailOpacityScale;
                        return o;
                    }

                    // CPU fallback (original)
                    float noiseFactor = t * _NoiseIntensity;
                    if (noiseFactor > 0.0001)
                    {
                        float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                        float scroll = _Time.y * _NoiseSpeed - t * 0.1 * _TrailDuration;
                        float3 noiseCoord = worldPos * _NoiseScale * 0.03125 + float3(scroll, scroll, scroll) * 0.2;
                        float4 n = tex3Dlod(_NoiseTex, float4(noiseCoord, 0));
                        float3 dispWorld = (n.rgb * 2.0 - 1.0) * noiseFactor;
                        float3 dispObj = mul((float3x3)unity_WorldToObject, dispWorld);
                        v.vertex.xyz += dispObj;
                    }
                    o.pos   = UnityObjectToClipPos(v.vertex);
                    o.pos.z += _DepthOffset;
                    o.uv    = v.uv;
                    o.color = v.color;
                    o.color.a *= _TrailOpacityScale;
                    return o;
                }
     
                fixed4 frag (v2f i) : SV_Target
                {
                    float3 baseRgb = i.color.rgb;
                    float3 blended = lerp(baseRgb, _CustomColor.rgb, saturate(_CustomBlend));
                    fixed4 col = fixed4(saturate(blended * _ColorBoost), i.color.a);
                    if (_ColorTexEnabled > 0.5)
                    {
                        float2 uvAtlas = ApplyAtlas(i.uv, _ColorTexAtlasCount, _ColorTexAtlasSpeedFlip);
                        fixed4 texCol = tex2D(_ColorTex, uvAtlas);
                        col.rgb *= texCol.rgb;
                        col.a *= texCol.a;
                    }
                    return col;
                }
                ENDCG
            }
    
            // -------- Pass 2: Alpha only --------
            Pass
            {
                Name "ALPHA"
                ColorMask A
                Blend One OneMinusSrcAlpha
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.5
                #include "UnityCG.cginc"
    
                float _GlowBoost;
                float _DepthOffset;
                sampler2D _GlowTex;
                float _GlowTexEnabled;
                float2 _GlowTexAtlasCount;
                float3 _GlowTexAtlasSpeedFlip;
                sampler3D _NoiseTex;
                float _NoiseIntensity;
                float _NoiseScale;
                float _NoiseSpeed;

                float _TrailDuration;

                #define TRAIL_HIST_COUNT 32
                float4 _TrailHistPos[TRAIL_HIST_COUNT];
                float4 _TrailHistFwd[TRAIL_HIST_COUNT];
                float4 _TrailHistUp[TRAIL_HIST_COUNT];
                int _TrailHistCount;
                float4 _TrailLocalOffset;
                float _TrailBaseFraction;
                float _TrailOpacityScale;
     
                float2 ApplyAtlasGlow(float2 uv, float2 atlasCount, float3 atlasSpeedFlip)
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
                    return (uv + float2(tileX, tileY)) / float2(atlasX, atlasY);
                }
     
                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv    : TEXCOORD0;
                    float4 color  : COLOR;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };
     
                struct v2f
                {
                    float4 pos   : SV_POSITION;
                    float2 uv    : TEXCOORD0;
                    fixed  alpha : TEXCOORD1;
                    UNITY_VERTEX_OUTPUT_STEREO
                };
     
                v2f vert (appdata v)
                {
                    v2f o;
                    UNITY_SETUP_INSTANCE_ID(v);
                    UNITY_INITIALIZE_OUTPUT(v2f, o);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                    float t = v.uv.x;
                    float vFrac = v.uv.y;

                    if (_TrailHistCount > 0)
                    {
                        float histF = t * (_TrailHistCount - 1);
                        int idx = (int)floor(histF);
                        idx = clamp(idx, 0, _TrailHistCount - 2);
                        float frac = histF - (float)idx;
                        float3 sPos = lerp(_TrailHistPos[idx].xyz, _TrailHistPos[idx+1].xyz, frac);
                        float3 sFwd = normalize(lerp(_TrailHistFwd[idx].xyz, _TrailHistFwd[idx+1].xyz, frac));
                        float3 sUp = normalize(lerp(_TrailHistUp[idx].xyz, _TrailHistUp[idx+1].xyz, frac));
                        if (length(sFwd) < 0.0001) sFwd = float3(0,0,1);
                        if (length(sUp) < 0.0001) sUp = float3(0,1,0);
                        float3 sRight = normalize(cross(sUp, sFwd));
                        if (length(sRight) < 0.0001) sRight = float3(1,0,0);
                        sUp = normalize(cross(sFwd, sRight));

                        float4 localOff = _TrailLocalOffset;
                        float baseFrac = _TrailBaseFraction;
                        float3 tipLocal = localOff.xyz;
                        float3 baseLocal = tipLocal * baseFrac;
                        float3 tipWorld = sPos + sRight * tipLocal.x + sUp * tipLocal.y + sFwd * tipLocal.z;
                        float3 baseWorld = sPos + sRight * baseLocal.x + sUp * baseLocal.y + sFwd * baseLocal.z;
                        float3 worldPos = lerp(baseWorld, tipWorld, vFrac);

                        float noiseFactor = t * _NoiseIntensity;
                        if (noiseFactor > 0.0001)
                        {
                            float scroll = _Time.y * _NoiseSpeed - t * 0.1 * _TrailDuration;
                            float3 noiseCoord = worldPos * _NoiseScale * 0.03125 + float3(scroll,scroll,scroll) * 0.2;
                            float4 n = tex3Dlod(_NoiseTex, float4(noiseCoord, 0));
                            float3 dispWorld = (n.rgb * 2.0 - 1.0) * noiseFactor;
                            worldPos += dispWorld;
                        }
                        o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1));
                        o.pos.z += _DepthOffset * o.pos.w;
                        o.uv = v.uv;
                        o.alpha = v.color.a * _TrailOpacityScale;
                        return o;
                    }

                    float noiseFactor = t * _NoiseIntensity;
                    if (noiseFactor > 0.0001)
                    {
                        float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                        float scroll = _Time.y * _NoiseSpeed - t * 0.1 * _TrailDuration;
                        float3 noiseCoord = worldPos * _NoiseScale * 0.03125 + float3(scroll, scroll, scroll) * 0.2;
                        float4 n = tex3Dlod(_NoiseTex, float4(noiseCoord, 0));
                        float3 dispWorld = (n.rgb * 2.0 - 1.0) * noiseFactor;
                        float3 dispObj = mul((float3x3)unity_WorldToObject, dispWorld);
                        v.vertex.xyz += dispObj;
                    }
                    o.pos   = UnityObjectToClipPos(v.vertex);
                    o.pos.z += _DepthOffset;
                    o.uv    = v.uv;
                    o.alpha = v.color.a * _TrailOpacityScale;
                    return o;
                }
     
                fixed4 frag (v2f i) : SV_Target
                {
                    float glow = saturate(i.alpha * _GlowBoost);
                    if (_GlowTexEnabled > 0.5)
                    {
                        float2 uvAtlas = ApplyAtlasGlow(i.uv, _GlowTexAtlasCount, _GlowTexAtlasSpeedFlip);
                        float4 glowTex = tex2D(_GlowTex, uvAtlas);
                        glow *= glowTex.r * glowTex.a;
                    }
                    return fixed4(0, 0, 0, glow);
                }
                ENDCG
            }
        }
        FallBack Off
}
