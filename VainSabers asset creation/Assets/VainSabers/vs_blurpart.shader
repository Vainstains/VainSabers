// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "VainSabers/Blur Part"
{
	Properties
	{
		_Glow ("Glow", Range (0, 1)) = 0
	    _DepthOffset("Depth Offset", Float) = 0.0
		_ColorTex ("Color", 2D) = "white" {}
		_GlowTex ("Glow Texture", 2D) = "white" {}
	}
	SubShader
	{
		Pass {
            Name "DepthPrepass"
            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0
        
            CGPROGRAM
            #pragma multi_compile_instancing
            #pragma fragment frag
            #pragma multi_compile _ _DISABLE_DEPTH_PREPASS
            #include "UnityCG.cginc"
            #include "BlurPart.cginc"
        
            fixed4 frag(v2f i) : SV_Target
            {
#if defined(_DISABLE_DEPTH_PREPASS)
                discard;
#else
                // UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                SaberFragVariables vars = GetCommonSaberVars(i);
                if(vars.alpha > 0.999)
                    return 1; // color ignored, depth written
                discard;
                return 0;
#endif
            }
            ENDCG 
        }
		Tags { "Queue"="Transparent+100" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            ZTest LEqual
            ColorMask RGB
            
            CGPROGRAM
            // #pragma multi_compile_instancing
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "BlurPart.cginc"
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                SaberFragVariables vars = GetCommonSaberVars(i);
                fixed4 col;
                col.rgb = vars.color * max(0.0, vars.rimFactor);
                col.a = vars.alpha;
                return col;
            }
            ENDCG
        }
        Pass {
            Cull Back
            ZWrite Off
            ZTest LEqual
            ColorMask A
            CGPROGRAM
            // #pragma multi_compile_instancing
            #pragma fragment frag
            #pragma multi_compile _ _DISABLE_GLOW_PASS
            #include "UnityCG.cginc"
            #include "BlurPart.cginc"

            fixed4 frag (v2f i) : SV_Target
            {
#if defined(_DISABLE_GLOW_PASS)
                return fixed4(0.0, 0.0, 0.0, 0.0);
#else
                // UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                SaberFragVariables vars = GetCommonSaberVars(i);

                float glow = vars.glowStrength * vars.alpha;
                
                return fixed4(0.0, 0.0, 0.0, glow);
#endif
            }
            ENDCG
        }
	}
}
