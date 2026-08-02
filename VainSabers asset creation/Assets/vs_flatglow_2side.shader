Shader "Unlit/vs_flatglow_2side"
{
    Properties
        {
            _ColorBoost ("RGB Multiplier", Range(0,4)) = 1
            _GlowBoost  ("Glow (Alpha) Multiplier", Range(0,4)) = 1
            _DepthOffset ("Depth Offset", Float) = 0
            _ColorTex ("Albedo + Alpha", 2D) = "white" {}
            _GlowTex ("Glow", 2D) = "white" {}
            _ColorTexEnabled ("Color Texture Enabled", Float) = 0
            _GlowTexEnabled ("Glow Texture Enabled", Float) = 0
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
                #include "UnityCG.cginc"
    
                float _ColorBoost;
                float _DepthOffset;
                sampler2D _ColorTex;
                float _ColorTexEnabled;
    
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
                    o.pos   = UnityObjectToClipPos(v.vertex);
                    o.pos.z += _DepthOffset;
                    o.uv    = v.uv;
                    o.color = v.color;
                    return o;
                }
    
                fixed4 frag (v2f i) : SV_Target
                {
                    fixed4 col = fixed4(saturate(i.color.rgb * _ColorBoost), i.color.a);
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
                #include "UnityCG.cginc"
    
                float _GlowBoost;
                float _DepthOffset;
                sampler2D _GlowTex;
                float _GlowTexEnabled;
    
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
                        glow *= tex2D(_GlowTex, i.uv).r;
                    return fixed4(0, 0, 0, glow);
                }
                ENDCG
            }
        }
    
        FallBack Off
}
