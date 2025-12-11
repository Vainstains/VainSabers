// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "VainSabers/Blur Part"
{
	Properties
	{
		_Glow ("Glow", Range (0, 1)) = 0
	    _DepthOffset("Depth Offset", Float) = 0.0 // new property
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
    #pragma vertex vert
    #pragma fragment frag
    #pragma target 2.0
    #include "BlurPart.cginc"

    fixed4 frag(v2f i) : SV_Target
    {
        SaberFragVariables vars = GetCommonSaberVars(i);
        if(vars.alpha > 0.999)
            return 1; // color ignored, depth written
        discard;
        return 0;
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
            
            #pragma fragment frag
            #include "BlurPart.cginc"
            fixed4 frag (v2f i) : SV_Target
            {
                SaberFragVariables vars = GetCommonSaberVars(i);
                fixed4 col;
                col.rgb = vars.color;
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
            
            #pragma fragment frag
            #include "BlurPart.cginc"

            fixed4 frag (v2f i) : SV_Target
            {
                SaberFragVariables vars = GetCommonSaberVars(i);

                float glow = vars.glowStrength * vars.alpha;
                
                return fixed4(0.0, 0.0, 0.0, glow);
            }
            ENDCG
        }
	}
}
