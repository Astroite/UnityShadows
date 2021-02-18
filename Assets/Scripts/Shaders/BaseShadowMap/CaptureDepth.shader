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
			uniform float MaxDepth = 200;

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
			
			float4 frag (v2f i) : SV_Target
			{
				float depth = distance(_MainLightPosWS.xyz, i.vertex.xyz);
				depth = depth / MaxDepth;
				// return float4(0.1, 0, 0.2, 0.5);
				return float4(depth, depth * depth, 0, 1);
			}
			ENDCG
		}
	}
}
