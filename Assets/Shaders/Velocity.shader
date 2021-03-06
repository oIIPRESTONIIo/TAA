﻿Shader "Velocity"
{
	CGINCLUDE

	#include "UnityCG.cginc"

	uniform sampler2D_half velocityTexture;
	uniform float4 velocityTexture_TexelSize; // Is automatically assigned by Unity, apparently

	uniform sampler2D_float _CameraDepthTexture;

	uniform float4 projectionExtents;// xy = frustum extents at distance 1, zw = jitter at distance 1

	uniform float4x4 currentViewMat;
	uniform float4x4 currentViewProjectMat;
	uniform float4x4 currentModelMat;

	uniform float4x4 previousViewProjectMat;
	uniform float4x4 previousViewProjectMat_NoFlip;
	uniform float4x4 previousModelMat;

	struct v2f
	{
		float4 vertPos : SV_POSITION;
		float2 texCoords : TEXCOORD0;
		float2 viewRay : TEXCOORD1;
	};

	v2f vert(appdata_img IN)
	{
		v2f OUT;

		OUT.vertPos = UnityObjectToClipPos(IN.vertex);

		OUT.texCoords = IN.texcoord.xy;

		OUT.viewRay = (2.0 * IN.texcoord.xy - 1.0) * (projectionExtents).xy + (projectionExtents).zw;

		return OUT;
	}

	float4 frag(v2f IN) : SV_Target
	{
		// reconstruct world space position
		float depth = LinearEyeDepth(tex2D(_CameraDepthTexture, IN.texCoords).x);
		float3 viewSpacePos = float3(IN.viewRay, 1.0) * depth;
		float4 worldSpacePos = mul(unity_CameraToWorld, float4(viewSpacePos, 1.0));

		// reproject into previous frame
		float4 previousClipSpacePosition = mul(previousViewProjectMat_NoFlip, worldSpacePos);
		float2 previousNDC = previousClipSpacePosition.xy / previousClipSpacePosition.w;
		float2 previousScreenSpacePosition = 0.5 * previousNDC + 0.5;

		// velcoity = difference
		float2 velocity = IN.texCoords - previousScreenSpacePosition;

		return float4(velocity, 0.0, 0.0);
	}

	ENDCG

	SubShader
	{
		Pass
		{
			ZTest Always Cull Off ZWrite Off
			Fog { Mode Off }

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			ENDCG
		}

	}

	Fallback Off
}