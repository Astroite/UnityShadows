Shader "ShadowMap/CaptureDepth"
{
	Properties
	{
		
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			float4 _MainLightPosWS;

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
				
				float moment1 = depth;
				float moment2 = depth * depth;

				// Adjusting moments (this is sort of bias per pixel) using partial derivative	
				float dx = ddx(depth);
				float dy = ddy(depth);
				moment2 += 0.25 * (dx * dx + dy * dy);

				return float4(moment1, moment2, 0, 1);
			}
			ENDCG
		}
	}
}
