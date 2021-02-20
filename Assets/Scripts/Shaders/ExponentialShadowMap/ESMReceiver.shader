Shader "Astroite/ESM/ESMReceiver"
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
            float _ESMScaleFactor;
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

			float ESM_FLITER(float3 worldPos)
			{
                float4 posInLight = ComputeScreenPosInLight(mul(_LightViewClipMatrix, float4(worldPos, 1)));
				float emoment = tex2Dproj(_ShadowDepthTex, posInLight).r;

				float depth = distance(_MainLightPosWS.xyz, worldPos);
                float edepth = exp(depth * -_ESMScaleFactor);

                float shadow = edepth * emoment;
                shadow = saturate(shadow);
                shadow = lerp(0.3, 1, shadow);

                return shadow;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;

				float shadow = ESM_FLITER(i.worldPos);
				col.rgb *= shadow;

				return col;
			}
			ENDCG
		}
	}
}

