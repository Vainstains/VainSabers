// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "VainSabers/Test"
{
	Properties
	{
		_Color ("Color", Color) = (1,1,1,1)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

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
				UNITY_VERTEX_INPUT_INSTANCE_ID
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

			float4 _Color;
			
			fixed4 frag (v2f i) : SV_Target
			{
				float chessboard = floor(i.uv.x * 10) + floor(i.uv.y * 10);
    			//divide it by 2 and get the fractional part, resulting in a value of 0 for even and 0.5 for odd numbers.
    			chessboard = frac(chessboard * 0.5);
    			//multiply it by 2 to make odd values white instead of grey
    			chessboard *= 2;

				float3 col = _Color * (chessboard * 0.3 + 0.7);
				
				return float4(col.rgb,0.5);
			}
			ENDCG
		}
	}
}
