Shader "Reprojection"
{
	Properties
	{
		mainTexture ("Base (RGB)", 2D) = "white" {}
	}

	CGINCLUDE

	#include "UnityCG.cginc"

	#pragma multi_compile __ SHOW_VELOCITY

	uniform float4 jitter; // frustum jitter uv deltas, where xy = current frame, zw = previous

	uniform sampler2D mainTexture;
	uniform float4 mainTexture_TexelSize; // Is automatically assgined by Unity, apparently

	uniform sampler2D_half velocityBuffer;

	uniform sampler2D historyTexture;
	uniform float blendWeightMin;
	uniform float blendWeightMax;
	uniform float motionBlurStrength;

	uniform sampler2D_float cameraDepthTexture;
	uniform float4 cameraDepthTexture_TexelSize;

	uniform bool showVelocity;

	struct v2f
	{
		float4 vertPos : SV_POSITION;
		float2 texCoords : TEXCOORD0;
	};

	v2f vert(appdata_img IN)
	{
		v2f OUT;

		OUT.vertPos = UnityObjectToClipPos(IN.vertex);

		OUT.texCoords = IN.texcoord.xy;


		return OUT;
	}

	// clip colour to bounding box
	float4 clipColour(float3 minBound, float3 maxBound, float4 neighbourAverage, float4 historyColour)
	{
		float3 maxClipBound = 0.5 * (maxBound + minBound);
		float3 minClipBound = 0.5 * (maxBound - minBound);

		float4 colourClipSpace = historyColour - float4(maxClipBound, neighbourAverage.w);
		float3 colourUnitSpace = colourClipSpace.xyz / minClipBound;
		colourUnitSpace = abs(colourUnitSpace);
		float maxColourValue = max(colourUnitSpace.x, max(colourUnitSpace.y, colourUnitSpace.z));

		if (maxColourValue > 1.0)
		{
			return float4(maxClipBound, neighbourAverage.w) + colourClipSpace / maxColourValue;
		} else {
			return historyColour;
		}
	}

	float4 reproject(float2 texCoords, float2 screenSpaceVelocity, float depth)
	{
		float4 screenColour = tex2D(mainTexture, texCoords - jitter.xy);
		float4 historyColour = tex2D(historyTexture, texCoords - screenSpaceVelocity);
		float2 uv = texCoords;

		// get neighbourhood 3x3
		float2 width = float2(mainTexture_TexelSize.x, 0.0);
		float2 height = float2(0.0, mainTexture_TexelSize.y);

		float4 c00 = tex2D(mainTexture, uv - height - width); // top left
		float4 c01 = tex2D(mainTexture, uv - height); // top middle
		float4 c02 = tex2D(mainTexture, uv - height + width); // top right
		float4 c10 = tex2D(mainTexture, uv - width); // middle left
		float4 c11 = tex2D(mainTexture, uv); // middle
		float4 c12 = tex2D(mainTexture, uv + width); // middle right
		float4 c20 = tex2D(mainTexture, uv + height - width); // bottom left
		float4 c21 = tex2D(mainTexture, uv + height); // bottom middle
		float4 c22 = tex2D(mainTexture, uv + height + width); // bottom right

		// minimum and maximum values of 3x3 neighbourhood
		float4 cmin = min(c00, min(c01, min(c02, min(c10, min(c11, min(c12, min(c20, min(c21, c22))))))));
		float4 cmax = max(c00, max(c01, max(c02, max(c10, max(c11, max(c12, max(c20, max(c21, c22))))))));
		
		float4 cavg = (c00 + c01 + c02 + c10 + c11 + c12 + c20 + c21 + c22) / 9.0;

		// minimum and maximum values of 5-tap '+' pattern
		float4 cmin5 = min(c01, min(c10, min(c11, min(c12, c21))));
		float4 cmax5 = max(c01, max(c10, max(c11, max(c12, c21))));
		float4 cavg5 = (c01 + c10 + c11 + c12 + c21) / 5.0;

		cmin = 0.5 * (cmin + cmin5);
		cmax = 0.5 * (cmax + cmax5);
		cavg = 0.5 * (cavg + cavg5);

		// clip history to neighbourhood of current sample
		historyColour = clipColour(cmin.xyz, cmax.xyz, cavg, historyColour);

		// feedback weight from unbiased luminance diff (t.lottes)
		float lum0 = Luminance(screenColour.rgb);
		float lum1 = Luminance(historyColour.rgb);
		float difference = abs(lum0 - lum1);
		float weight = 1.0 - difference;
		float feedbackWeight = lerp(blendWeightMin, blendWeightMax, weight);

		// output
		return lerp(screenColour, historyColour, feedbackWeight);
	}

	float3 find_closest_fragment_3x3(float2 uv)
	{
		float2 dd = abs(cameraDepthTexture_TexelSize.xy);
		float2 du = float2(dd.x, 0.0);
		float2 dv = float2(0.0, dd.y);

		float3 dtl = float3(-1, -1, tex2D(cameraDepthTexture, uv - dv - du).x);
		float3 dtc = float3(0, -1, tex2D(cameraDepthTexture, uv - dv).x);
		float3 dtr = float3(1, -1, tex2D(cameraDepthTexture, uv - dv + du).x);

		float3 dml = float3(-1, 0, tex2D(cameraDepthTexture, uv - du).x);
		float3 dmc = float3(0, 0, tex2D(cameraDepthTexture, uv).x);
		float3 dmr = float3(1, 0, tex2D(cameraDepthTexture, uv + du).x);

		float3 dbl = float3(-1, 1, tex2D(cameraDepthTexture, uv + dv - du).x);
		float3 dbc = float3(0, 1, tex2D(cameraDepthTexture, uv + dv).x);
		float3 dbr = float3(1, 1, tex2D(cameraDepthTexture, uv + dv + du).x);

		float3 dmin = dtl;
		if (dmin.z < dtc.z) dmin = dtc;
		if (dmin.z < dtr.z) dmin = dtr;

		if (dmin.z < dml.z) dmin = dml;
		if (dmin.z < dmc.z) dmin = dmc;
		if (dmin.z < dmr.z) dmin = dmr;

		if (dmin.z < dbl.z) dmin = dbl;
		if (dmin.z < dbc.z) dmin = dbc;
		if (dmin.z < dbr.z) dmin = dbr;

		return float3(uv + dd.xy * dmin.xy, dmin.z);
	}


	struct f2rt
	{
		fixed4 buffer : SV_Target0;
		fixed4 screen : SV_Target1;
	};

	f2rt frag(v2f IN)
	{
		f2rt OUT;
		float2 uv = IN.texCoords;

		// retrieve closest fragment in a 3x3 neighbourhood;
		float3 closestFragment = find_closest_fragment_3x3(uv);
		float2 screenSpaceVelocity = tex2D(velocityBuffer, closestFragment.xy).xy;
		float depth = LinearEyeDepth(tex2D(cameraDepthTexture, closestFragment).z);

		// temporal resolve
		float4 screenColour = reproject(IN.texCoords, screenSpaceVelocity, depth);

		// prepare outputs
		float4 bufferColour = screenColour;

		//motion blur
		screenSpaceVelocity = motionBlurStrength * screenSpaceVelocity;
		float velocityMagnitude = length(screenSpaceVelocity * mainTexture_TexelSize.zw) / 2;
		float trustLowerBound = 2.5;
		float trustUpperBound = 15.0;
		float trustSpan = trustUpperBound - trustLowerBound;
		float trust = 1.0 - clamp(velocityMagnitude - trustLowerBound, 0.0, trustSpan) / trustSpan;

		float2 unijitteredUV = IN.texCoords - jitter.xy;
		float2 velocity = 0.5 * screenSpaceVelocity;
		int samples = 5;

		float2 velocityStep = velocity / samples;
		float2 UVpos = unijitteredUV + velocityStep;
		float4 accumilation = 0.0;

		[unroll]
		for (int i = 0; i < samples; i++)
		{
			accumilation += tex2D(mainTexture, UVpos + i * velocityStep);
		}

		screenColour = lerp((accumilation / samples), screenColour, trust);


#if SHOW_VELOCITY
		// display velocity
		screenColour.g += 25 * length(screenSpaceVelocity);
		screenColour = float4(25 * abs(screenSpaceVelocity), 0.0, 0.0);
#endif

		/// @brief adds noise to the final colour output (which adds a surprising uplift in quality)
		/// Modified from :-
		/// PlayDeadGames, Lasse Jon Fuglsang Pedersen (31 March, 2017). Temporal Reprojection Anti-Aliasing for Unity 5.0+.
		/// [Accessed 2019]. Available from: "https://github.com/playdeadgames/temporal/blob/master/Assets/Shaders/TemporalReprojection.shader".
		float4 noise4 = frac(sin(dot((IN.texCoords + _SinTime.x + 0.6959174).xy, float2(12.9898f, 78.233f)))* float4(43758.5453f, 28001.8384f, 50849.4141f, 12996.89f)) / 510.0f;
		/// end citation
		OUT.buffer = saturate(bufferColour + noise4);
		OUT.screen = saturate(screenColour + noise4);

		// done
		return OUT;
	}

	ENDCG

	SubShader
	{
		ZTest Always Cull Off ZWrite Off
		Fog { Mode off }

		Pass
		{
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			ENDCG
		}
	}

		Fallback off
}