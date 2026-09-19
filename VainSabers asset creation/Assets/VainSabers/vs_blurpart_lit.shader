Shader "VainSabers/vs_blurpart_lit"
{
    Properties
    {
        _SpecularStrength ("Specular Strength", Range(0,2)) = 0.6
        _SpecularPower ("Specular Power (Shininess)", Range(4,512)) = 32
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0
        _ColorBoost ("RGB Multiplier", Range(0,4)) = 1
        _Glow ("Glow", Range(0,1)) = 0.5
        _DepthOffset("Depth Offset", Float) = 0.0
        _ColorTex ("Color", 2D) = "white" {}
        _GlowTex ("Glow Texture", 2D) = "white" {}
        _ColorTexAtlasCount ("Color Atlas Count", Vector) = (1,1,0,0)
        _ColorTexAtlasSpeedFlip ("Color Atlas Speed+Flips", Vector) = (1,0,0,0)
        _GlowTexAtlasCount ("Glow Atlas Count", Vector) = (1,1,0,0)
        _GlowTexAtlasSpeedFlip ("Glow Atlas Speed+Flips", Vector) = (1,0,0,0)

        _FresnelCubemap ("Fresnel Cubemap", Cube) = "" {}
        _CubemapStrength ("Cubemap Strength", Range(0, 2)) = 1.0
        _CubemapRotation ("Cubemap Rotation", Range(0, 360)) = 0.0
    }

    SubShader
    {
        Pass
        {
            Name "DepthPrepass"
            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            CGPROGRAM
            // #pragma multi_compile_instancing
            #pragma fragment frag
            #pragma multi_compile _ _DISABLE_DEPTH_PREPASS
            #pragma multi_compile __ _GEOMETRY_SPRITE _GEOMETRY_OBJ
            // #pragma target 2.0
            #include "UnityCG.cginc"
            #include "BlurPart.cginc"

            fixed4 frag(v2f i) : SV_Target
            {
#if defined(_DISABLE_DEPTH_PREPASS)
                discard;
                return 0;
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
        
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        

        Pass
        {
            LOD 200
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            CGPROGRAM
            // #pragma multi_compile_instancing
            #pragma fragment frag
            #pragma multi_compile __ _GEOMETRY_SPRITE _GEOMETRY_OBJ
            // #pragma target 2.0

            #include "UnityCG.cginc"
            #include "BlurPart.cginc"

            float _SpecularStrength;
            float _SpecularPower;
            float _Metallic;
            float _Smoothness;
            float _ColorBoost;

            float _FresnelPower;
            float _FresnelStrength;
            float4 _RimColor;
            
            samplerCUBE _FresnelCubemap;
            float _CubemapStrength;
            float _CubemapRotation;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            float3 rotateAroundY(float3 dir, float angle)
            {
                float rad = angle * UNITY_PI / 180.0;
                float s, c;
                sincos(rad, s, c);
                return float3(
                    dir.x * c - dir.z * s,
                    dir.y,
                    dir.x * s + dir.z * c
                );
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                SaberFragVariables vars = GetCommonSaberVars(i);

                float3 N = normalize(vars.normal);
                float3 V = normalize(vars.viewDir);
                float3 L = normalize(float3(0,1,0));

                float diffuseStrength = 1.0 - _Metallic * 0.9;
                float specStrength = lerp(_SpecularStrength, 1.0, _Metallic);
                float shininess = lerp(_SpecularPower, 512.0, _Smoothness);

                float NdotL = saturate(dot(N,L) * 0.4 + 0.6);
                float3 diffuse = vars.color * NdotL * NdotL * diffuseStrength;

                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N,H)), shininess) * specStrength;

                
                float fresnel = 1.0 - saturate(dot(N, V));
                fresnel = pow(fresnel, _FresnelPower);
                float3 reflectDir = reflect(-V, N);
                reflectDir = rotateAroundY(reflectDir, _CubemapRotation);

                float4 cubemap = texCUBE(_FresnelCubemap, reflectDir);
                float3 rimLight = fresnel * _FresnelStrength * _RimColor.rgb;
                float3 cubemapEffect = cubemap.rgb * _CubemapStrength * sqrt(fresnel) * lerp(0.2, 1.0, _Metallic);

                float3 rimFinal = rimLight + cubemapEffect;
                float3 rgb = (diffuse + spec + rimFinal) * _ColorBoost * max(0.0, vars.rimFactor);

                float alpha = vars.alpha;

                alpha = saturate(alpha);

                return float4(rgb, alpha);
            }
            ENDCG
        }

        Pass
        {
            Cull Back
            ZWrite Off
            ZTest LEqual
            ColorMask A

            CGPROGRAM
            #pragma fragment frag
            #pragma multi_compile _ _DISABLE_GLOW_PASS
            #pragma multi_compile __ _GEOMETRY_SPRITE _GEOMETRY_OBJ
            #include "UnityCG.cginc"
            #include "BlurPart.cginc"

            fixed4 frag(v2f i) : SV_Target
            {
#if defined(_DISABLE_GLOW_PASS)
                discard;
                return fixed4(0.0, 0.0, 0.0, 0.0);
#else
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Lit parts now glow – same logic as unlit blur part
                SaberFragVariables vars = GetCommonSaberVars(i);
                float glow = vars.glowStrength * vars.alpha;
                return fixed4(0.0, 0.0, 0.0, glow);
#endif
            }
            ENDCG
        }
    }

    FallBack Off
}