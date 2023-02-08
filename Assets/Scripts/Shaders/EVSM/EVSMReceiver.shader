Shader "Astroite/EVSM/EVSMReceiver"
{
	Properties
	{
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		// Tags { "RenderType"="Opaque" }
		LOD 100
		// Blend SrcAlpha OneMinusSrcAlpha
		Cull back

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#pragma enable_d3d11_debug_symbols

			#include "UnityCG.cginc"
			#include "EVSMSetting.cginc"

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

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Color;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
				
				float visibility = EVSM_FLITER(i.worldPos);
				float shadow = 1 - visibility;
				shadow = shadow * 0.7 + 0.3;
				// return float4(visibility, 0, 0, 0);
				return col * shadow;
			}
			ENDCG
		}
	}
}

