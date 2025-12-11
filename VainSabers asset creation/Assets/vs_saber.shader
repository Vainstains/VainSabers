Shader "VainSabers/Saber"
{
Properties {
    _Color ("Color", Color) = (0.3,0.5,1,1)
    _Color1 ("Color1", Color) = (1.1,0.1,0.4,1)
    _HandleColor ("Handle Color", Color) = (0.15,0.15,0.18,1)
    _BlurAmount ("Blur", Range(0,1)) = 1
}

Category {
    SubShader {
        Tags { "Queue"="Transparent+100" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        
        Cull Off
        ZWrite On
        Pass {
            ColorMask 0
            
            CGPROGRAM
            
            #pragma fragment frag
            #include "Saber.cginc"

            fixed4 frag (v2f i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual
        Pass {
            ColorMask RGB
            
            CGPROGRAM
            
            #pragma fragment frag
            #include "Saber.cginc"

            fixed4 frag (v2f i) : SV_Target
            {
                SaberFragVariables vars = GetCommonSaberVars(i);
                fixed4 col;
                fixed3 handleColor = vars.handleColor;
                handleColor *= 0.8 + 0.3 * max(-0.8, dot(i.trueNormal, normalize(float3(0, 2, 1) - i.worldPos)));
                handleColor *= dot(i.trueNormal, normalize(_WorldSpaceCameraPos.xyz - i.worldPos));
                col.rgb = lerp(handleColor, vars.bladeColor, vars.bladeColorFactor);
                col.a = vars.alpha;
                return col;
            }
            ENDCG
        }
        Pass {
            ColorMask A
            CGPROGRAM
            
            #pragma fragment frag
            #include "Saber.cginc"

            fixed4 frag (v2f i) : SV_Target
            {
                SaberFragVariables vars = GetCommonSaberVars(i);

                float glow = vars.glowStrength * (0.9f - vars.motionFactor * 0.4f) * vars.alpha;
                
                return fixed4(0.0, 0.0, 0.0, glow);
            }
            ENDCG
        }
    }
}}
