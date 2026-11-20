Shader "Unlit/vs_flatglow"
{
    Properties
        {
            _ColorBoost ("RGB Multiplier", Range(0,4)) = 1
            _GlowBoost  ("Glow (Alpha) Multiplier", Range(0,4)) = 1
            _DepthOffset ("Depth Offset", Float) = 0
            _CustomColor ("Custom Color", Color) = (1,1,1,1)
        }
    
        SubShader
        {
            Tags { "Queue"="Transparent+180" "RenderType"="Transparent" "IgnoreProjector"="True" }
            LOD 100
            Cull Off
            ZWrite Off
            // ZTest defaults to LEqual; keep it unless you need different sorting behavior

            CGINCLUDE
            #include "UnityCG.cginc"
            float _TipBlurMaskRadius;
            float4 _TipBlurStarts[3];
            float4 _TipBlurEnds[3];

            float2 TipScreenPoint(float4 clip)
            {
                float aspect = abs(UNITY_MATRIX_P._m11 / UNITY_MATRIX_P._m00);
                return clip.xy * float2(aspect, 1.0) / max(clip.w, 0.001);
            }

            float Cross2(float2 a, float2 b)
            {
                return a.x * b.y - a.y * b.x;
            }

            float TipEdgeDistance(float2 p, float2 a, float2 b)
            {
                float2 edge = b - a;
                return Cross2(edge, p - a) * rsqrt(max(dot(edge, edge), 1e-8));
            }

            float OutsideTipTriangle(float2 p, float2 a, float2 b, float2 c)
            {
                float area = Cross2(b - a, c - a);
                if (abs(area) < 1e-6)
                    return 1.0;
                float winding = sign(area);
                return max(-winding * TipEdgeDistance(p, a, b),
                       max(-winding * TipEdgeDistance(p, b, c), -winding * TipEdgeDistance(p, c, a)));
            }

            float TipTrailVisibility(float4 clip)
            {
                if (_TipBlurMaskRadius <= 0.0)
                    return 1.0;
                float2 pixel = TipScreenPoint(clip);
                float visibility = 1.0;
                [unroll]
                for (int i = 0; i < 2; i++)
                {
                    float4 c0 = UnityWorldToClipPos(_TipBlurStarts[i].xyz);
                    float4 c1 = UnityWorldToClipPos(_TipBlurEnds[i].xyz);
                    float4 c2 = UnityWorldToClipPos(_TipBlurStarts[i + 1].xyz);
                    float4 c3 = UnityWorldToClipPos(_TipBlurEnds[i + 1].xyz);
                    if (min(min(c0.w, c1.w), min(c2.w, c3.w)) <= 0.001)
                        continue;
                    float2 a = TipScreenPoint(c0), b = TipScreenPoint(c1);
                    float2 c = TipScreenPoint(c2), d = TipScreenPoint(c3);
                    float outside = min(OutsideTipTriangle(pixel, a, b, c), OutsideTipTriangle(pixel, c, b, d));
                    float radius = _TipBlurMaskRadius * abs(UNITY_MATRIX_P._m11) / (0.25 * (c0.w + c1.w + c2.w + c3.w));
                    float width = max(abs(TipEdgeDistance(c, a, b)), abs(TipEdgeDistance(d, a, b)));
                    float mask = smoothstep(radius, max(4.0 * radius, 0.001), width);
                    float edge = smoothstep(0.0, max(2.0 * radius, 0.001), outside);
                    visibility = min(visibility, lerp(1.0, edge, mask));
                }
                return visibility;
            }
            ENDCG
    
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
                #pragma target 3.5
    
                float _ColorBoost;
                float _DepthOffset;
                float4 _CustomColor;
                float _CustomBlend;
    
                struct appdata
                {
                    float4 vertex : POSITION;
                    float4 color  : COLOR;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };
    
                struct v2f
                {
                    float4 pos   : SV_POSITION;
                    fixed4 color : COLOR0;
                    float4 clip : TEXCOORD0;
                    UNITY_VERTEX_OUTPUT_STEREO
                };
    
                v2f vert (appdata v)
                {
                    v2f o;
                    UNITY_SETUP_INSTANCE_ID(v);
                    UNITY_INITIALIZE_OUTPUT(v2f, o);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                    o.pos   = UnityObjectToClipPos(v.vertex);
                    o.clip = o.pos;
                    o.pos.z += _DepthOffset;
                    o.color = v.color;
                    return o;
                }

                fixed4 frag (v2f i) : SV_Target
                {
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                    // Gradient base color from vertex, then custom blend in shader
                    float3 baseRgb = i.color.rgb;
                    float3 blended = lerp(baseRgb, _CustomColor.rgb, saturate(_CustomBlend));
                    return fixed4(saturate(blended * _ColorBoost), i.color.a * TipTrailVisibility(i.clip));
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
                #pragma target 3.5
    
                float _GlowBoost;
                float _DepthOffset;
    
                struct appdata
                {
                    float4 vertex : POSITION;
                    float4 color  : COLOR;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };
    
                struct v2f
                {
                    float4 pos   : SV_POSITION;
                    fixed  alpha : TEXCOORD0;
                    float4 clip : TEXCOORD1;
                    UNITY_VERTEX_OUTPUT_STEREO
                };
    
                v2f vert (appdata v)
                {
                    v2f o;
                    UNITY_SETUP_INSTANCE_ID(v);
                    UNITY_INITIALIZE_OUTPUT(v2f, o);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                    o.pos   = UnityObjectToClipPos(v.vertex);
                    o.clip = o.pos;
                    o.pos.z += _DepthOffset;
                    o.alpha = v.color.a;
                    return o;
                }
    
                fixed4 frag (v2f i) : SV_Target
                {
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                    // Write only alpha (glow), RGB is discarded by ColorMask.
                    return fixed4(0, 0, 0, saturate(i.alpha * _GlowBoost) * TipTrailVisibility(i.clip));
                }
                ENDCG
            }
        }
    
        FallBack Off
}
