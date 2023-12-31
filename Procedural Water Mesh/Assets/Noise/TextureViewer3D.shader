Shader "Unlit/TextureViewer3D"
{
	Properties
	{
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

			Texture3D<float> DisplayTexture;
			SamplerState samplerDisplayTexture;
			float sliceDepth;
			float surfaceLevel;

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

			fixed4 frag (v2f i) : SV_Target
			{
				float3 uv3 = float3(i.uv.xy, sliceDepth);
				float val = DisplayTexture.SampleLevel(samplerDisplayTexture, uv3, 0);
				float scaledVal = val;// (1 + val) / 2.75f;
				fixed4 col = val < 0;// (val < surfaceLevel);

				if (scaledVal < surfaceLevel && scaledVal > surfaceLevel - 0.05f) {
					col = fixed4(1, 0, 0, 1);
				}

				return col;
			}
			ENDCG
		}
	}
}
