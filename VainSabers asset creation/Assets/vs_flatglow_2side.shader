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
            // ZTest defaults to LEqual; keep it unless you need different sorting behavior
    
            // -------- Pass 1: RGB only (flat color from vertex colors) --------
            Pass
            {
                Name "RGB"
                // Standard premultiplied? No—this assumes non-premultiplied vertex colors.
                Blend SrcAlpha OneMinusSrcAlpha
                // Only write RGB channels; keep destination alpha untouched.
                ColorMask RGB
    
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.0
                #include "UnityCG.cginc"
    
                float _ColorBoost;
                float _DepthOffset;
                float4 _CustomColor;
                float _CustomBlend;
                sampler2D _ColorTex;
                float _ColorTexEnabled;
                sampler3D _NoiseTex;
                float _NoiseIntensity;
                float _NoiseScale;
                float _NoiseSpeed;

                float _TrailDuration;
    
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
                    return o;
                }
    
                fixed4 frag (v2f i) : SV_Target
                {
                    float3 baseRgb = i.color.rgb;
                    float3 blended = lerp(baseRgb, _CustomColor.rgb, saturate(_CustomBlend));
                    fixed4 col = fixed4(saturate(blended * _ColorBoost), i.color.a);
                    if (_ColorTexEnabled > 0.5)
                    {
                        fixed4 texCol = tex2D(_ColorTex, i.uv);
                        col.rgb *= texCol.rgb;
                        col.a *= texCol.a;
                    }
                    return col;
                }
                ENDCG
            }
    
            // -------- Pass 2: Alpha only (glow mask from vertex alpha) --------
            Pass
            {
                Name "ALPHA"
                // Only touch the alpha channel.
                ColorMask A
    
                // Choose ONE of these (default is a standard "alpha over" into A):
                Blend One OneMinusSrcAlpha           // <- default: composite src alpha into dest alpha
                // Blend One Zero                    // <- overwrite: src alpha replaces dest alpha
                // BlendOp Max                       // <- use with Blend One One to take max alpha
                // Blend One One                     // <- additive alpha accumulation (clamped)
    
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 3.0
                #include "UnityCG.cginc"
    
                float _GlowBoost;
                float _DepthOffset;
                sampler2D _GlowTex;
                float _GlowTexEnabled;
                sampler3D _NoiseTex;
                float _NoiseIntensity;
                float _NoiseScale;
                float _NoiseSpeed;

                float _TrailDuration;
    
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
                    o.alpha = v.color.a;
                    return o;
                }
    
                fixed4 frag (v2f i) : SV_Target
                {
                    // Write only alpha (glow), RGB is discarded by ColorMask.
                    float glow = saturate(i.alpha * _GlowBoost);
                    if (_GlowTexEnabled > 0.5)
                    {
                        float4 glowTex = tex2D(_GlowTex, i.uv);
                        glow *= glowTex.r * glowTex.a;
                    }
                        
                    return fixed4(0, 0, 0, glow);
                }
                ENDCG
            }
        }
    
        FallBack Off
}
