Shader "Hidden/BloomComposite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Blur1 ("Blur 1", 2D) = "black" {}
        _Blur2 ("Blur 2", 2D) = "black" {}
        _Blur3 ("Blur 3", 2D) = "black" {}
        _Blur4 ("Blur 4", 2D) = "black" {}
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
            sampler2D _Blur1, _Blur2, _Blur3, _Blur4;
            float4 _ScreenRes;
            
            // Time-based noise function (converted from original)
            float rand(float2 n)
            {
                return frac(sin(dot(n + sin(_Time.y * 40.0) * 20.0, 
                    float2(12.98764598 + sin(_Time.y * 24.2345) * 2.0, 4.14143456))) * 
                    43758.54346553 + _Time.y * 8.8568765234);
            }
            
            float noise(float2 p)
            {
                float2 ip = floor(p);
                float2 u = frac(p);
                u = u * u * (3.0 - 2.0 * u);
                
                float res = lerp(
                    lerp(rand(ip), rand(ip + float2(1.0, 0.0)), u.x),
                    lerp(rand(ip + float2(0.0, 1.0)), rand(ip + float2(1.0, 1.0)), u.x), u.y);
                return res * res;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 original = tex2D(_MainTex, i.uv);
                original.rgb += saturate(original.a);
                
                // Combine blur levels
                fixed4 bloom = tex2D(_Blur1, i.uv) + tex2D(_Blur2, i.uv);
                bloom += tex2D(_Blur3, i.uv) + tex2D(_Blur4, i.uv);
                
                // Apply bloom with dirt modulation
                fixed4 result = original + bloom;
                
                // Add noise effect
                float noiseValue = noise(i.uv * _ScreenRes.xy * 4.0);
                result *= (1.0 + 0.06 * noiseValue);
                
                return result;
            }
            ENDCG
        }
    }
}