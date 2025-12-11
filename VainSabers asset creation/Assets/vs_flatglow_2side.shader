Shader "Unlit/vs_flatglow_2side"
{
    Properties
        {
            _ColorBoost ("RGB Multiplier", Range(0,4)) = 1
            _GlowBoost  ("Glow (Alpha) Multiplier", Range(0,4)) = 1
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
    
                struct appdata
                {
                    float4 vertex : POSITION;
                    float4 color  : COLOR;
                };
    
                struct v2f
                {
                    float4 pos   : SV_POSITION;
                    fixed4 color : COLOR0;
                };
    
                v2f vert (appdata v)
                {
                    v2f o;
                    o.pos   = UnityObjectToClipPos(v.vertex);
                    o.color = v.color;
                    return o;
                }
    
                fixed4 frag (v2f i) : SV_Target
                {
                    // Flat color: just vertex color RGB (scaled), alpha ignored here.
                    return fixed4(saturate(i.color.rgb * _ColorBoost), i.color.a);
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
    
                struct appdata
                {
                    float4 vertex : POSITION;
                    float4 color  : COLOR;
                };
    
                struct v2f
                {
                    float4 pos   : SV_POSITION;
                    fixed  alpha : TEXCOORD0;
                };
    
                v2f vert (appdata v)
                {
                    v2f o;
                    o.pos   = UnityObjectToClipPos(v.vertex);
                    o.alpha = v.color.a;
                    return o;
                }
    
                fixed4 frag (v2f i) : SV_Target
                {
                    // Write only alpha (glow), RGB is discarded by ColorMask.
                    return fixed4(0, 0, 0, saturate(i.alpha * _GlowBoost));
                }
                ENDCG
            }
        }
    
        FallBack Off
}
