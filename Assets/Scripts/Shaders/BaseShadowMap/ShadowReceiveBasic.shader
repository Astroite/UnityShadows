Shader "Astroite/Shadows/BaseShadowMapReveiver"
{
	Properties
	{
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100
		Blend SrcAlpha OneMinusSrcAlpha
		Cull back

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			 #pragma enable_d3d11_debug_symbols

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
			};

			sampler2D _ShadowDepthTex;
			sampler2D _MainTex;
			matrix _LightViewClipMatrix;
			float4 _MainLightPosWS;

			float4 _MainTex_ST;
			float4 _Color;

			float4 ComputeScreenPosInLight(float4 pos)
			{
				float4 o = pos * 0.5f;
				o.xy = o.xy + o.w;
				o.zw = pos.zw;
				return o;
			}
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

				return o;
			}

			float linstep(float minValue, float maxValue, float v)
			{
				return clamp((v - minValue)/(maxValue - minValue), 0, 1);
			}

			float ReduceLightBleeding(float p_max, float Amount)
			{  
				return linstep(Amount, 1, p_max);
			}

			float VSM_FLITER(float2 moments, float fragDepth)
			{
				float E_x2 = moments.y;
				float Ex_2 = moments.x * moments.x;

				float variance = E_x2 - Ex_2;
				variance = max(variance, 0.002);

				float mD = moments.x - fragDepth;
				float mD_2 = mD * mD;
				float p = variance / (variance + mD_2);

				//p = ReduceLightBleeding(p, 0.3);

				float lit = max( p, fragDepth <= moments.x );
				lit = lerp(0.3, 1, lit);

				return lit;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				float4 posInLight = ComputeScreenPosInLight(mul(_LightViewClipMatrix, float4(i.worldPos, 1)));
				float2 moments = tex2Dproj(_ShadowDepthTex, posInLight).rg;
				float depth = distance(_MainLightPosWS.xyz, i.worldPos);

				float shadow = VSM_FLITER(moments, depth);
				
				fixed4 col = tex2D(_MainTex, i.uv) * _Color;
				col.rgb *= shadow;
				return col;
			}
			ENDCG
		}
	}
}
