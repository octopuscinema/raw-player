#ifndef HIGHLIGHT_RECOVERY_GLSL_H
#define HIGHLIGHT_RECOVERY_GLSL_H

#include "Luminance.glsl.h"

#define eHighlightRecovery lowp int 
const eHighlightRecovery HighlightRecoveryOff = 0;
const eHighlightRecovery HighlightRecoveryOn = 1;

#define RAW_WHITE_LEVEL_NORMALISED (1.0)

// Clip level slightly under than 1 to account for gaussian smoothing sum floating point inaccuracies
#define RAW_CLIP_LEVEL_EPSILON (0.01)
#define RAW_CLIP_LEVEL_NORMALISED (RAW_WHITE_LEVEL_NORMALISED - RAW_CLIP_LEVEL_EPSILON) 

// Highlight correction stops-under start
#define HIGHLIGHT_CORRECTION_STOPS_UNDER (RAW_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-0.5))
#define HIGHLIGHT_RECOVERY_STOPS_UNDER (RAW_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-2.0))
#define HIGHLIGHT_RECOVERY_BLEND_STOPS_UNDER (RAW_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-1.0))

// Feather in stops to interpolate between highest/lowest channels
#define HIGHLIGHT_RECOVERY_CHANNEL_FEATHER_STOPS (1.0)

// Workaround for lack of GLSL enum support
#define eRGBChannel lowp int
const eRGBChannel RGBChannelUnset = -1;
const eRGBChannel RGBChannelRed = 0;
const eRGBChannel RGBChannelGreen = 1;
const eRGBChannel RGBChannelBlue = 2;

mediump vec3 SynthesiseThreeChannels(mediump vec3 cameraWhite)
{
	return cameraWhite;
}

mediump vec3 SynthesiseTwoChannels(mediump vec3 rgb, mediump vec3 cameraWhite, eRGBChannel firstClipped, eRGBChannel secondClipped, eRGBChannel unclippedChannel)
{
	mediump float scale = rgb[unclippedChannel] / cameraWhite[unclippedChannel];
	mediump vec3 synthesised = cameraWhite * scale;
	mediump vec3 rgbOut = max(rgb, synthesised);
	rgbOut[unclippedChannel] = rgb[unclippedChannel];

	if ( (rgb[firstClipped] > synthesised[firstClipped] || rgb[secondClipped] > synthesised[secondClipped]) ) {
		mediump float avgScale = ((rgb[firstClipped] / synthesised[firstClipped]) + (rgb[secondClipped] / synthesised[secondClipped])) * 0.5;
		rgbOut[firstClipped] = (rgbOut[firstClipped] * 0.4) + (synthesised[firstClipped] * avgScale * 0.6);
		rgbOut[secondClipped] = (rgbOut[secondClipped] * 0.4) + (synthesised[secondClipped] * avgScale * 0.6);
	}

	// Blend towards three channel version
	mediump float blendThreeChannels = smoothstep(HIGHLIGHT_RECOVERY_STOPS_UNDER, RAW_CLIP_LEVEL_NORMALISED, rgb[unclippedChannel]);

	return mix(rgbOut, SynthesiseThreeChannels(cameraWhite), blendThreeChannels);
}

mediump vec3 SynthesiseOneChannel(mediump vec3 rgb, mediump vec3 cameraWhite, eRGBChannel clippedChannel, eRGBChannel firstUnclipped, eRGBChannel secondUnclipped)
{
	// Find lowest and mid channel
	eRGBChannel lowestChannel = RGBChannelUnset;
	eRGBChannel midChannel = RGBChannelUnset;
	if (rgb[firstUnclipped] < rgb[secondUnclipped]) {
		lowestChannel = firstUnclipped;
		midChannel = secondUnclipped;
	}
	else {
		lowestChannel = secondUnclipped;
		midChannel = firstUnclipped;
	}

	// Scale camera white based on middle channel
	mediump vec3 cameraWhiteScaled = cameraWhite * (cameraWhite[midChannel] / rgb[midChannel]);
	 
	// Use the 2 channel synthesis on the highest two channels
    // Interpolate based on which channel of the two is higher
    mediump float channelDiffStops = log2(rgb[firstUnclipped] / rgb[secondUnclipped]);
    mediump float channelDiffLerp = smoothstep(HIGHLIGHT_RECOVERY_CHANNEL_FEATHER_STOPS * -0.5, HIGHLIGHT_RECOVERY_CHANNEL_FEATHER_STOPS * 0.5, channelDiffStops);
    mediump vec3 synthesisedHighestChannels = mix(SynthesiseTwoChannels(rgb, cameraWhite, clippedChannel, secondUnclipped, firstUnclipped),
        SynthesiseTwoChannels(rgb, cameraWhite, clippedChannel, firstUnclipped, secondUnclipped), channelDiffLerp);

	// Interpolate between the 2 channel synthesis and the scaled camera white
	mediump float lowPoint = min(rgb[midChannel], cameraWhiteScaled[midChannel]);
	mediump float highPoint = RAW_CLIP_LEVEL_NORMALISED;
	mediump float interpolator = smoothstep(lowPoint, highPoint, rgb[midChannel]);
	return mix(synthesisedHighestChannels, cameraWhiteScaled, interpolator);
}

// This algorithm should match behaviour in the Davinci Resolve OCTOPUS CineForm RAW panel
// Input is expected to be normalised debayered RAW sensor data
mediump vec3 HighlightRecovery(mediump vec3 rgbLinear, mediump vec3 cameraWhite)
{
	// Handle 1 or 2 clipped channels
	eRGBChannel firstChannelClipped = RGBChannelUnset;
	eRGBChannel secondChannelClipped = RGBChannelUnset;
	eRGBChannel firstChannelUnclipped = RGBChannelUnset;
	eRGBChannel secondChannelUnclipped = RGBChannelUnset;
	int channelsClipped = 0;
	int channelsUnclipped = 0;

	// RED channel clipped/unclipped
	if (rgbLinear.x >= RAW_CLIP_LEVEL_NORMALISED) {
		firstChannelClipped = RGBChannelRed;
		channelsClipped++;
	}
	else {
		firstChannelUnclipped = RGBChannelRed;
		channelsUnclipped++;
	}

	// GREEN channel clipped/unclipped
	if (rgbLinear.y >= RAW_CLIP_LEVEL_NORMALISED) {
		if (channelsClipped == 0)
			firstChannelClipped = RGBChannelGreen;
		else if (channelsClipped == 1)
			secondChannelClipped = RGBChannelGreen;
		channelsClipped++;
	}
	else {
		if (channelsUnclipped == 0)
			firstChannelUnclipped = RGBChannelGreen;
		else if (channelsUnclipped == 1)
			secondChannelUnclipped = RGBChannelGreen;
		channelsUnclipped++;
	}

	// BLUE channel clipped/unclipped
	if (rgbLinear.z >= RAW_CLIP_LEVEL_NORMALISED) {
		if (channelsClipped == 0)
			firstChannelClipped = RGBChannelBlue;
		else if (channelsClipped == 1)
			secondChannelClipped = RGBChannelBlue;
		channelsClipped++;
	}
	else {
		if (channelsUnclipped == 0)
			firstChannelUnclipped = RGBChannelBlue;
		else if (channelsUnclipped == 1)
			secondChannelUnclipped = RGBChannelBlue;
		channelsUnclipped++;
	}

	// 0 or 1 channel(s) clipped
	if (channelsClipped <= 1) {

		// No channels are clipped but we want to blend towards the scenario where one channel is clipped
		mediump float synthesisedMix = 1.0;
		if (channelsClipped == 0) {
			mediump float maxChannel = max(max(rgbLinear.x, rgbLinear.y), rgbLinear.z);
			if (maxChannel == rgbLinear.x) {
				firstChannelClipped = RGBChannelRed;
				firstChannelUnclipped = RGBChannelGreen;
				secondChannelUnclipped = RGBChannelBlue;
			}
			else if (maxChannel == rgbLinear.y) {
				firstChannelClipped = RGBChannelGreen;
				firstChannelUnclipped = RGBChannelRed;
				secondChannelUnclipped = RGBChannelBlue;
			}
			else {
				firstChannelClipped = RGBChannelBlue;
				firstChannelUnclipped = RGBChannelRed;
				secondChannelUnclipped = RGBChannelGreen;
			}
			synthesisedMix = smoothstep(HIGHLIGHT_RECOVERY_BLEND_STOPS_UNDER, RAW_CLIP_LEVEL_NORMALISED, maxChannel);
		}

		mediump vec3 rgbLinearSynthesised = SynthesiseOneChannel(rgbLinear, cameraWhite, firstChannelClipped, firstChannelUnclipped, secondChannelUnclipped);
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

mediump vec3 HighlightCorrect(mediump vec3 rgbLinear, mediump vec3 cameraWhiteNormalised)
{
	// Blend towards white
	mediump vec3 whiteBlendRGB = smoothstep(vec3(HIGHLIGHT_CORRECTION_STOPS_UNDER), vec3(RAW_CLIP_LEVEL_NORMALISED), rgbLinear);
	mediump float whiteBlend = max(max(whiteBlendRGB.x, whiteBlendRGB.y), whiteBlendRGB.z);
	return mix(rgbLinear, cameraWhiteNormalised, whiteBlend);
}

#endif