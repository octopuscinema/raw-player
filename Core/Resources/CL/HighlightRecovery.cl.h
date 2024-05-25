#ifndef HIGHLIGHT_RECOVERY_CL_H
#define HIGHLIGHT_RECOVERY_CL_H

#include "ComputeDefines.cl.h"
#include "ComputeMaths.cl.h"
#include "ComputeTypes.cl.h"

#define RAW_WHITE_LEVEL_NORMALISED convert_half(1.0f)

// Clip level slightly under than 1 to account for gaussian smoothing sum floating point inaccuracies
#define RAW_CLIP_LEVEL_EPSILON convert_half(0.01f)
#define RAW_CLIP_LEVEL_NORMALISED (RAW_WHITE_LEVEL_NORMALISED - RAW_CLIP_LEVEL_EPSILON)

// Highlight correction stops-under start
#define HIGHLIGHT_CORRECTION_STOPS_UNDER (RAW_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-0.5f))
#define HIGHLIGHT_RECOVERY_STOPS_UNDER (RAW_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-2.0f))
#define HIGHLIGHT_RECOVERY_BLEND_STOPS_UNDER (RAW_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-1.0f))

PRIVATE half3 SynthesiseThreeChannels(half3 cameraWhite)
{
    eRGBChannel firstClipped = RGB_CHANNEL_GREEN;
    eRGBChannel secondClipped = RGB_CHANNEL_RED;
    eRGBChannel unclippedChannel = RGB_CHANNEL_BLUE;
    
    // Convert to array based rgb
	half rgb3[3] = { RAW_WHITE_LEVEL_NORMALISED, RAW_WHITE_LEVEL_NORMALISED, RAW_WHITE_LEVEL_NORMALISED };
	half cameraWhite3[3] = { cameraWhite.x,cameraWhite.y, cameraWhite.z };
	
	half2 unclippedWhite = make_half2(cameraWhite3[firstClipped], cameraWhite3[secondClipped]);
	
	half scale = RAW_WHITE_LEVEL_NORMALISED / cameraWhite3[unclippedChannel];
	half2 synthesised = unclippedWhite * scale;
	
	rgb3[firstClipped] = fmax(RAW_WHITE_LEVEL_NORMALISED, synthesised.x);
	rgb3[secondClipped] = fmax(RAW_WHITE_LEVEL_NORMALISED, synthesised.y);
 
    return make_half3(rgb3[0], rgb3[1], rgb3[2]);
}

PRIVATE half3 SynthesiseTwoChannels(half3 rgb, half3 cameraWhite, eRGBChannel firstClipped, eRGBChannel secondClipped, eRGBChannel unclippedChannel)
{
	// Convert to array based rgb
	half rgb3[3] = { rgb.x, rgb.y, rgb.z };
	half cameraWhite3[3] = { cameraWhite.x,cameraWhite.y, cameraWhite.z };
	
	half2 unclippedWhite = make_half2(cameraWhite3[firstClipped], cameraWhite3[secondClipped]);
	
	half scale = rgb3[unclippedChannel] / cameraWhite3[unclippedChannel];
	half2 synthesised = unclippedWhite * scale;
	
	rgb3[firstClipped] = fmax(rgb3[firstClipped], synthesised.x);
	rgb3[secondClipped] = fmax(rgb3[secondClipped], synthesised.y);
 
    half3 rgbOut = make_half3(rgb3[0], rgb3[1], rgb3[2]);
    
    // Blend towards three channel version
    half blendThreeChannels = smoothstep(HIGHLIGHT_RECOVERY_STOPS_UNDER, RAW_CLIP_LEVEL_NORMALISED, rgb3[unclippedChannel]);
    
    return mix(rgbOut, SynthesiseThreeChannels(cameraWhite), blendThreeChannels);
}

PRIVATE half3 SynthesiseOneChannel(half3 rgb, half3 cameraWhite, eRGBChannel clippedChannel, eRGBChannel firstUnclipped, eRGBChannel secondUnclipped)
{
    half rgb3[3] = { rgb.x, rgb.y, rgb.z };
	half cameraWhite3[3] = { cameraWhite.x,cameraWhite.y, cameraWhite.z };

    // Find lowest and mid channel
    eRGBChannel lowestChannel = RGB_CHANNEL_UNSET;
    eRGBChannel midChannel = RGB_CHANNEL_UNSET;
    if ( rgb3[firstUnclipped] < rgb3[secondUnclipped] ) {
        lowestChannel = firstUnclipped;
        midChannel = secondUnclipped;
    } else {
        lowestChannel = secondUnclipped;
        midChannel = firstUnclipped;
    }
    
    // Scale camera white based on lowest channel
    half3 cameraWhiteScaled = cameraWhite * (cameraWhite3[lowestChannel]/rgb3[lowestChannel]);
    half cameraWhiteScaled3[3] = { cameraWhiteScaled.x, cameraWhiteScaled.y, cameraWhiteScaled.z };
    
    // Use the 2 channel synthesis on the highest two channels
    half3 synthesisedHighestChannels = SynthesiseTwoChannels(rgb, cameraWhite, clippedChannel, midChannel, lowestChannel);
    
    // Interpolate between the 2 channel synthesis and the scaled camera white
    half lowPoint = fmin(rgb3[midChannel], cameraWhiteScaled3[midChannel]);
    half highPoint = RAW_CLIP_LEVEL_NORMALISED;
    half interpolator = smoothstep(lowPoint, highPoint, rgb3[midChannel]);
    return mix(synthesisedHighestChannels, cameraWhiteScaled, interpolator);
}

// This algorithm should match behaviour in the Davinci Resolve OCTOPUS CineForm RAW panel
// Input is expected to be normalised debayered RAW sensor data
PRIVATE half3 HighlightRecovery(half3 rgbLinear, float3 cameraWhitef)
{
	// Work in half precision if possible
#ifdef SUPPORTS_NATIVE_HALF_PRECISION
	half3 cameraWhite = convert_half3(cameraWhitef);
#else
	half3 cameraWhite = cameraWhitef;
#endif

	// Handle 1 or 2 clipped channels
	eRGBChannel firstChannelClipped = RGB_CHANNEL_UNSET;
	eRGBChannel secondChannelClipped = RGB_CHANNEL_UNSET;
	eRGBChannel firstChannelUnclipped = RGB_CHANNEL_UNSET;
	eRGBChannel secondChannelUnclipped = RGB_CHANNEL_UNSET;
	int channelsClipped = 0;
	int channelsUnclipped = 0;
	
	// RED channel clipped/unclipped
	if ( rgbLinear.x >= RAW_CLIP_LEVEL_NORMALISED ) {
		firstChannelClipped = RGB_CHANNEL_RED;
		channelsClipped++;
	} else {
		firstChannelUnclipped = RGB_CHANNEL_RED;
		channelsUnclipped++;
	}
	
	// GREEN channel clipped/unclipped
	if ( rgbLinear.y >= RAW_CLIP_LEVEL_NORMALISED ) {
		if ( channelsClipped == 0 )
			firstChannelClipped = RGB_CHANNEL_GREEN;
		else if ( channelsClipped == 1 )
			secondChannelClipped = RGB_CHANNEL_GREEN;
		channelsClipped++;
	} else {
		if ( channelsUnclipped == 0 )
			firstChannelUnclipped = RGB_CHANNEL_GREEN;
		else if ( channelsUnclipped == 1 )
			secondChannelUnclipped = RGB_CHANNEL_GREEN;
		channelsUnclipped++;
	}
	
	// BLUE channel clipped/unclipped
	if ( rgbLinear.z >= RAW_CLIP_LEVEL_NORMALISED ) {
		if ( channelsClipped == 0 )
			firstChannelClipped = RGB_CHANNEL_BLUE;
		else if ( channelsClipped == 1 )
			secondChannelClipped = RGB_CHANNEL_BLUE;
		channelsClipped++;
	} else {
		if ( channelsUnclipped == 0 )
			firstChannelUnclipped = RGB_CHANNEL_BLUE;
		else if ( channelsUnclipped == 1 )
			secondChannelUnclipped = RGB_CHANNEL_BLUE;
		channelsUnclipped++;
	}
 
    // 0 or 1 channel(s) clipped
    if ( channelsClipped <= 1 ) {
    
        // No channels are clipped but we want to blend towards the scenario where one channel is clipped
        half synthesisedMix = 1.0f;
        if ( channelsClipped == 0 ) {
             half maxChannel = fmax(fmax(rgbLinear.x, rgbLinear.y), rgbLinear.z);
            if ( maxChannel == rgbLinear.x) {
                firstChannelClipped = RGB_CHANNEL_RED;
                firstChannelUnclipped = RGB_CHANNEL_GREEN;
                secondChannelUnclipped = RGB_CHANNEL_BLUE;
            } else if ( maxChannel == rgbLinear.y ) {
                firstChannelClipped = RGB_CHANNEL_GREEN;
                firstChannelUnclipped = RGB_CHANNEL_RED;
                secondChannelUnclipped = RGB_CHANNEL_BLUE;
            } else {
                firstChannelClipped = RGB_CHANNEL_BLUE;
                firstChannelUnclipped = RGB_CHANNEL_RED;
                secondChannelUnclipped = RGB_CHANNEL_GREEN;
            }
            synthesisedMix = smoothstep(HIGHLIGHT_RECOVERY_BLEND_STOPS_UNDER, RAW_WHITE_LEVEL_NORMALISED, maxChannel);
        }
        
        half3 rgbLinearSynthesised = SynthesiseOneChannel(rgbLinear, cameraWhite, firstChannelClipped, firstChannelUnclipped, secondChannelUnclipped);
        rgbLinear = (channelsClipped==1) ? rgbLinearSynthesised : mix(rgbLinear, rgbLinearSynthesised, synthesisedMix);
    }
    
    // Synthesise for 2 clipped channels
	else if ( channelsClipped == 2 )
		rgbLinear = SynthesiseTwoChannels(rgbLinear, cameraWhite, firstChannelClipped, secondChannelClipped, firstChannelUnclipped);
	
	// Synthesise 3 channels
	else if ( channelsClipped == 3 )
        rgbLinear = SynthesiseThreeChannels(cameraWhite);
        
    return rgbLinear;
}

PRIVATE RGBHalf4 HighlightRecovery4(RGBHalf4 rgbLinear, float3 cameraWhite)
{
	RGBHalf4 rgbOut;
	
	for(int i = 0; i < 4; i++)
		rgbOut.RGB[i] = HighlightRecovery(rgbLinear.RGB[i], cameraWhite);
	
	return rgbOut;
}

PRIVATE half3 HighlightCorrect(half3 rgbLinear, float3 cameraWhiteNormalised)
{
    // Blend towards white
    half3 whiteBlendRGB = smoothstep(make_half3(HIGHLIGHT_CORRECTION_STOPS_UNDER), make_half3(RAW_CLIP_LEVEL_NORMALISED), rgbLinear);
    half whiteBlend = fmax(fmax(whiteBlendRGB.x, whiteBlendRGB.y), whiteBlendRGB.z);
    return mix(rgbLinear, convert_half3(cameraWhiteNormalised), whiteBlend);
}

PRIVATE RGBHalf4 HighlightCorrect4(RGBHalf4 rgbLinear, float3 cameraWhiteNormalised)
{
	RGBHalf4 Out;
	for (int i = 0; i < 4; i++)
		Out.RGB[i] = HighlightCorrect(rgbLinear.RGB[i], cameraWhiteNormalised);
	return Out;
}

#endif
