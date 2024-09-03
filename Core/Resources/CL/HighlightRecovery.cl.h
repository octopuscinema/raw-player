#ifndef HIGHLIGHT_RECOVERY_CL_H
#define HIGHLIGHT_RECOVERY_CL_H

#include "ComputeDefines.cl.h"
#include "ComputeFunctions.cl.h"
#include "ComputeMaths.cl.h"
#include "ComputeTypes.cl.h"

#define RAW_WHITE_LEVEL_NORMALISED (1.0f)

// Clip level slightly under than 1 to account for gaussian smoothing sum floating point inaccuracies
#define RAW_CLIP_LEVEL_EPSILON (0.01f)
#define RAW_CLIP_LEVEL_NORMALISED (RAW_WHITE_LEVEL_NORMALISED - RAW_CLIP_LEVEL_EPSILON) 

// Highlight correction stops-under start
#define HIGHLIGHT_CORRECTION_STOPS_UNDER (RAW_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-0.5f))
#define HIGHLIGHT_RECOVERY_STOPS_UNDER (RAW_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-2.0f))
#define HIGHLIGHT_RECOVERY_BLEND_STOPS_UNDER (RAW_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-1.0f))

// Feather in stops to interpolate between highest/lowest channels
#define HIGHLIGHT_RECOVERY_CHANNEL_FEATHER_STOPS (1.0f)

#define HIGHLIGHT_RECOVERY_EPSILON ((half)0.0001f)

PRIVATE half3 SynthesiseThreeChannels(half3 cameraWhite)
{
	return cameraWhite;
}

PRIVATE half3 SynthesiseTwoChannels(half3 rgb, half3 cameraWhite, eRGBChannel firstClipped, eRGBChannel secondClipped, eRGBChannel unclippedChannel)
{
	half scale = IndexHalf3(rgb,unclippedChannel) / fmax(HIGHLIGHT_RECOVERY_EPSILON, IndexHalf3(cameraWhite, unclippedChannel));
	half3 synthesised = cameraWhite * scale;
	half3 rgbOut = fmax(rgb, synthesised);

	half rgbOut3[3] = { rgbOut.x, rgbOut.y, rgbOut.z };

	rgbOut3[unclippedChannel] = IndexHalf3(rgb,unclippedChannel);

	if ( IndexHalf3(rgb,firstClipped) > IndexHalf3(synthesised,firstClipped) || IndexHalf3(rgb,secondClipped) > IndexHalf3(synthesised,secondClipped) ) {
		half avgScale = ((IndexHalf3(rgb,firstClipped) / fmax(HIGHLIGHT_RECOVERY_EPSILON, IndexHalf3(synthesised,firstClipped))) + (IndexHalf3(rgb,secondClipped) / fmax(HIGHLIGHT_RECOVERY_EPSILON, IndexHalf3(synthesised,secondClipped)))) * 0.5f;
		rgbOut3[firstClipped] = (rgbOut3[firstClipped] * 0.4f) + (IndexHalf3(synthesised,firstClipped) * avgScale * 0.6f);
		rgbOut3[secondClipped] = (rgbOut3[secondClipped] * 0.4f) + (IndexHalf3(synthesised,secondClipped) * avgScale * 0.6f);
	}

	// Blend towards three channel version
	half blendThreeChannels = smoothstep((half)HIGHLIGHT_RECOVERY_STOPS_UNDER, (half)RAW_CLIP_LEVEL_NORMALISED, IndexHalf3(rgb,unclippedChannel));

	return mix(make_half3(rgbOut3[0], rgbOut3[1], rgbOut3[2]), SynthesiseThreeChannels(cameraWhite), blendThreeChannels);
}

PRIVATE half3 SynthesiseOneChannel(half3 rgb, half3 cameraWhite, eRGBChannel clippedChannel, eRGBChannel firstUnclipped, eRGBChannel secondUnclipped)
{
	// Find lowest and mid channel
	eRGBChannel lowestChannel = RGB_CHANNEL_UNSET;
	eRGBChannel midChannel = RGB_CHANNEL_UNSET;
	if (IndexHalf3(rgb,firstUnclipped) < IndexHalf3(rgb,secondUnclipped)) {
		lowestChannel = firstUnclipped;
		midChannel = secondUnclipped;
	}
	else {
		lowestChannel = secondUnclipped;
		midChannel = firstUnclipped;
	}

	// Scale camera white based on middle channel
	half3 cameraWhiteScaled = cameraWhite * (IndexHalf3(cameraWhite,midChannel) / fmax(HIGHLIGHT_RECOVERY_EPSILON, IndexHalf3(rgb,midChannel)));
	 
	// Use the 2 channel synthesis on the highest two channels
    // Interpolate based on which channel of the two is higher
    half channelDiffStops = log2(IndexHalf3(rgb,firstUnclipped) / fmax(HIGHLIGHT_RECOVERY_EPSILON, IndexHalf3(rgb,secondUnclipped)));
    half channelDiffLerp = smoothstep((half)(HIGHLIGHT_RECOVERY_CHANNEL_FEATHER_STOPS * -0.5f), (half)(HIGHLIGHT_RECOVERY_CHANNEL_FEATHER_STOPS * 0.5f), channelDiffStops);
    half3 synthesisedHighestChannels = mix(SynthesiseTwoChannels(rgb, cameraWhite, clippedChannel, secondUnclipped, firstUnclipped),
        SynthesiseTwoChannels(rgb, cameraWhite, clippedChannel, firstUnclipped, secondUnclipped), channelDiffLerp);

	// Interpolate between the 2 channel synthesis and the scaled camera white
	half lowPoint = fmin(IndexHalf3(rgb,midChannel), IndexHalf3(cameraWhiteScaled,midChannel));
	half highPoint = RAW_CLIP_LEVEL_NORMALISED;
	half interpolator = smoothstep(lowPoint, highPoint, IndexHalf3(rgb,midChannel));
	return mix(synthesisedHighestChannels, cameraWhiteScaled, interpolator);
}

// This algorithm should match behaviour in the Davinci Resolve OCTOPUS CineForm RAW panel
// Input is expected to be normalised debayered RAW sensor data
PRIVATE half3 HighlightRecovery(half3 rgbLinear, half3 cameraWhite)
{
	// Handle 1 or 2 clipped channels
	eRGBChannel firstChannelClipped = RGB_CHANNEL_UNSET;
	eRGBChannel secondChannelClipped = RGB_CHANNEL_UNSET;
	eRGBChannel firstChannelUnclipped = RGB_CHANNEL_UNSET;
	eRGBChannel secondChannelUnclipped = RGB_CHANNEL_UNSET;
	int channelsClipped = 0;
	int channelsUnclipped = 0;

	// RED channel clipped/unclipped
	if (rgbLinear.x >= RAW_CLIP_LEVEL_NORMALISED) {
		firstChannelClipped = RGB_CHANNEL_RED;
		channelsClipped++;
	}
	else {
		firstChannelUnclipped = RGB_CHANNEL_RED;
		channelsUnclipped++;
	}

	// GREEN channel clipped/unclipped
	if (rgbLinear.y >= RAW_CLIP_LEVEL_NORMALISED) {
		if (channelsClipped == 0)
			firstChannelClipped = RGB_CHANNEL_GREEN;
		else if (channelsClipped == 1)
			secondChannelClipped = RGB_CHANNEL_GREEN;
		channelsClipped++;
	}
	else {
		if (channelsUnclipped == 0)
			firstChannelUnclipped = RGB_CHANNEL_GREEN;
		else if (channelsUnclipped == 1)
			secondChannelUnclipped = RGB_CHANNEL_GREEN;
		channelsUnclipped++;
	}

	// BLUE channel clipped/unclipped
	if (rgbLinear.z >= RAW_CLIP_LEVEL_NORMALISED) {
		if (channelsClipped == 0)
			firstChannelClipped = RGB_CHANNEL_BLUE;
		else if (channelsClipped == 1)
			secondChannelClipped = RGB_CHANNEL_BLUE;
		channelsClipped++;
	}
	else {
		if (channelsUnclipped == 0)
			firstChannelUnclipped = RGB_CHANNEL_BLUE;
		else if (channelsUnclipped == 1)
			secondChannelUnclipped = RGB_CHANNEL_BLUE;
		channelsUnclipped++;
	}

	// 0 or 1 channel(s) clipped
	if (channelsClipped <= 1) {

		// No channels are clipped but we want to blend towards the scenario where one channel is clipped
		half synthesisedMix = 1.0f;
		if (channelsClipped == 0) {
			half maxChannel = fmax(fmax(rgbLinear.x, rgbLinear.y), rgbLinear.z);
			if (maxChannel == rgbLinear.x) {
				firstChannelClipped = RGB_CHANNEL_RED;
				firstChannelUnclipped = RGB_CHANNEL_GREEN;
				secondChannelUnclipped = RGB_CHANNEL_BLUE;
			}
			else if (maxChannel == rgbLinear.y) {
				firstChannelClipped = RGB_CHANNEL_GREEN;
				firstChannelUnclipped = RGB_CHANNEL_RED;
				secondChannelUnclipped = RGB_CHANNEL_BLUE;
			}
			else {
				firstChannelClipped = RGB_CHANNEL_BLUE;
				firstChannelUnclipped = RGB_CHANNEL_RED;
				secondChannelUnclipped = RGB_CHANNEL_GREEN;
			}
			synthesisedMix = smoothstep((half)HIGHLIGHT_RECOVERY_BLEND_STOPS_UNDER, (half)RAW_CLIP_LEVEL_NORMALISED, maxChannel);
		}

		half3 rgbLinearSynthesised = SynthesiseOneChannel(rgbLinear, cameraWhite, firstChannelClipped, firstChannelUnclipped, secondChannelUnclipped);
		rgbLinear = (channelsClipped == 1) ? rgbLinearSynthesised : mix(rgbLinear, rgbLinearSynthesised, synthesisedMix);
	}

	// Synthesise for 2 clipped channels
	else if (channelsClipped == 2)
		rgbLinear = SynthesiseTwoChannels(rgbLinear, cameraWhite, firstChannelClipped, secondChannelClipped, firstChannelUnclipped);

	// Synthesise 3 channels
	else if (channelsClipped == 3)
		rgbLinear = SynthesiseThreeChannels(cameraWhite);

	return rgbLinear;
}

PRIVATE half3 HighlightCorrect(half3 rgbLinear, half3 cameraWhiteNormalised)
{
	// Blend towards white
	half3 whiteBlendRGB = smoothstep(make_half3(HIGHLIGHT_CORRECTION_STOPS_UNDER), make_half3(RAW_CLIP_LEVEL_NORMALISED), rgbLinear);
	half whiteBlend = fmax(fmax(whiteBlendRGB.x, whiteBlendRGB.y), whiteBlendRGB.z);
	return mix(rgbLinear, cameraWhiteNormalised, whiteBlend);
}

PRIVATE RGBHalf4 HighlightRecovery4(RGBHalf4 rgbLinear, float3 cameraWhite)
{
	RGBHalf4 rgbOut;
	
	for(int i = 0; i < 4; i++)
		rgbOut.RGB[i] = HighlightRecovery(rgbLinear.RGB[i], convert_half3(cameraWhite));
	
	return rgbOut;
}

PRIVATE RGBHalf4 HighlightCorrect4(RGBHalf4 rgbLinear, float3 cameraWhiteNormalised)
{
	RGBHalf4 Out;
	for (int i = 0; i < 4; i++)
		Out.RGB[i] = HighlightCorrect(rgbLinear.RGB[i], convert_half3(cameraWhiteNormalised));
	return Out;
}

#endif
