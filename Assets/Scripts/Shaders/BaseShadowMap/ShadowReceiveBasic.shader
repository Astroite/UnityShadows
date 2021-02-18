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

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog

			#include "UnityCG.cginc"	
			#include "../includes/ShadowCG.cginc"

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
			uniform float4 _MainLightPosWS;

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

			float VSM_FLITER(float2 moments, float fragDepth)
			{
				if (fragDepth <= moments.x)
					return 1.0;

				float variance = moments.y - moments.x * moments.x;
				variance = max(variance, 0.00002);

				float mD = moments.x - fragDepth;
				float p = variance / (variance + mD * mD);

				return p;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				//Compute depth
				float fragDepth = distance(_MainLightPosWS.xyz, i.vertex.xyz);
				// Sample DepthTexture
				float4 posInLight = ComputeScreenPosInLight(mul(_LightViewClipMatrix, float4(i.worldPos, 1)));
				float2 moments = tex2D(_ShadowDepthTex, posInLight.xy/ posInLight.w).rg;

				float shadow = VSM_FLITER(moments, fragDepth);
				return shadow.xxxx;
				
				fixed4 col = tex2D(_MainTex, i.uv) * _Color;
				return col;
			}
			ENDCG
		}
	}
}
