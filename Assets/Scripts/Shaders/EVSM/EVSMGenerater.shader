Shader "Astroite/EVSM/EVSMGenerator"
{
	Properties{}

	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			ZWrite On
			ZTest Less
			
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#include "EVSMSetting.cginc"
			
            #pragma enable_d3d11_debug_symbols

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{				
				float depth = i.vertex.z / i.vertex.w;
				float2 exponents = EVSM_GetExponents(_PositiveExponent, _NegativeExponent);
				float2 vsmDepth = EVSM_ApplyDepthWarp(depth, exponents);
				
				return float4(vsmDepth.xy, vsmDepth.xy * vsmDepth.xy);
			}
			ENDCG
		}
	}
}

