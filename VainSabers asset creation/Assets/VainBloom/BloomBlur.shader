Shader "Hidden/BloomBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _ScreenRes;
            
            fixed4 frag (v2f i) : SV_Target
            {
                float2 texelSize = _MainTex_TexelSize.xy;
                float x = texelSize.x;
                float y = texelSize.y;
                // a - b - c
                // - j - k -
                // d - e - f
                // - l - m -
                // g - h - i
                
                fixed3 a = tex2D(_MainTex, i.uv + float2(-2*x,  2*y)).rgb;
                fixed3 b = tex2D(_MainTex, i.uv + float2( 0,     2*y)).rgb;
                fixed3 c = tex2D(_MainTex, i.uv + float2( 2*x,  2*y)).rgb;
                
                fixed3 d = tex2D(_MainTex, i.uv + float2(-2*x,    0)).rgb;
                fixed3 e = tex2D(_MainTex, i.uv).rgb;
                fixed3 f = tex2D(_MainTex, i.uv + float2( 2*x,    0)).rgb;
                
                fixed3 g = tex2D(_MainTex, i.uv + float2(-2*x, -2*y)).rgb;
                fixed3 h = tex2D(_MainTex, i.uv + float2( 0,    -2*y)).rgb;
                fixed3 i_sample = tex2D(_MainTex, i.uv + float2( 2*x, -2*y)).rgb;
                
                fixed3 j = tex2D(_MainTex, i.uv + float2(-x,  y)).rgb;
                fixed3 k = tex2D(_MainTex, i.uv + float2( x,  y)).rgb;
                fixed3 l = tex2D(_MainTex, i.uv + float2(-x, -y)).rgb;
                fixed3 m = tex2D(_MainTex, i.uv + float2( x, -y)).rgb;
                
                // Apply weighted distribution for energy preservation
                fixed3 downsample = e * 0.125;
                downsample += (a + c + g + i_sample) * 0.03125;
                downsample += (b + d + f + h) * 0.0625;
                downsample += (j + k + l + m) * 0.125;
                
                return fixed4(downsample * 1.1, 1.0);
            }
            ENDCG
        }
    }
}