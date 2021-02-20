Shader "Astroite/ESM/ESMGenerator"
{
	Properties{}

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
            #pragma enable_d3d11_debug_symbols

			float4 _MainLightPosWS;
            float _ESMScaleFactor;
            float c = 10;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float3 worldPos : TEXCOORD1;
			};
			
			v2f vert (appdata v)
			{
				v2f o;

				o.vertex = UnityObjectToClipPos(v.vertex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.uv = v.uv;
				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{
				float depth = distance(_MainLightPosWS.xyz, i.worldPos);

                float emoment = exp(depth * _ESMScaleFactor);

				return float4(emoment, 0, 0, 1);
			}
			ENDCG
		}
	}
}

