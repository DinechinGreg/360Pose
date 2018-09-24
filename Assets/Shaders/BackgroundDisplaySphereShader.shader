Shader "Custom/BackgroundDisplaySphereShader"
{
	Properties
	{
		_MainTex("Color and depth", 2D) = "white" {}
	}
	SubShader
	{
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#include "CustomCG.cginc"

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;

			//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
			v2f vert (appdata_base v)
			{
				v2f o;
				float4 depthUV = float4(v.texcoord.x, clamp(v.texcoord.y/2.0,0.000001,0.499999), 0, 0);
				fixed3 digitalRGB = ToDigital(255 * tex2Dlod(_MainTex, depthUV).rgb);
				float depth = RGBToDepth(digitalRGB);
				depth *= 1.005;
				o.vertex = UnityObjectToClipPos(depth * v.normal);
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
				return o;
			}

			//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
			fixed4 frag (v2f i) : SV_Target
			{
				float2 uv = float2(i.uv.x, clamp(i.uv.y/2.0 + 0.5, 0.500001, 0.999999));
				fixed4 col = tex2D(_MainTex, uv);
				return col;
			}
			ENDCG
		}
	}
}