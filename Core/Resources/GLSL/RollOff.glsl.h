#ifndef ROLLOFF_GLSL_H
#define ROLLOFF_GLSL_H

#include "Luminance.glsl.h"

#define WHITE_CLIP_LEVEL_NORMALISED (1.0)

struct RolloffParams
{
	mediump float UnderLevel;
    mediump float OverLevel;
    mediump float Power;
    mediump float Strength;
};

// Workaround for lack of GLSL enum support
#define eRollOff lowp int
const eRollOff RollOffNone = -1;
const eRollOff RollOffLow = 0;
const eRollOff RollOffMedium = 1;
const eRollOff RollOffHigh = 2;
const eRollOff RollOffDefaultShadow = RollOffLow;
const eRollOff RollOffDefaultHighlight = RollOffLow;

// Needs to match ComputeRolloff.h
// TODO: Use a shared header
RolloffParams HighlightRolloffParams(eRollOff rolloff)
{
    RolloffParams params;
    if ( rolloff == RollOffLow) {
        params.OverLevel = WHITE_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(3.0);
        params.UnderLevel = WHITE_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-2.0);
        params.Power = 0.4;
        params.Strength = 0.75;
    } else if ( rolloff == RollOffHigh) {
        params.OverLevel = WHITE_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(0.25);
        params.UnderLevel = WHITE_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-4.5);
        params.Power = 0.2;
        params.Strength = 1.0;
    } else {
        params.OverLevel = WHITE_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(2.5);
        params.UnderLevel = WHITE_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-1.5);
        params.Power = 0.2;
        params.Strength = 0.8;
    }
	return params;
}
RolloffParams ShadowRolloffParams(eRollOff rolloff)
{
    RolloffParams params;
    if ( rolloff == RollOffLow) { // Low, bottom 2 stops of 12 stop range
        params.OverLevel = 2.0 / 4095.0;
        params.UnderLevel = 0.0 / 4095.0;
        params.Power = 0.5;
        params.Strength = 1.0;
	} else if ( rolloff == RollOffHigh) { // High, bottom 6 stops of 12 stop range
        params.OverLevel = 32.0 / 4095.0;
        params.UnderLevel = 2.0 / 4095.0;
        params.Power = 0.5;
        params.Strength = 1.0;
    } else { // Medium, bottom 4.5 stops of 12 stop range
        params.OverLevel = 12.0 / 4095.0;
        params.UnderLevel = 1.0 / 4095.0;
        params.Power = 0.5;
        params.Strength = 1.0;
    }
	return params;
}

mediump vec3 HighlightRolloff(mediump vec3 rgbLinear, mediump vec3 cameraWhiteNormalised, mediump float rgbLuminance, mediump vec3 RAWLuminanceWeight, eRollOff rollOff)
{
    RolloffParams rollOffParams = HighlightRolloffParams(rollOff);

	// Create a saturation roll-off towards the highlights by blending towards neutral cameraWhite based on the luminance
	mediump vec3 neutral = cameraWhiteNormalised * rgbLuminance;
	mediump float rolloff = smoothstep(rollOffParams.UnderLevel, rollOffParams.OverLevel, rgbLuminance);
    mediump float rolloffMixer = clamp(pow(rolloff,rollOffParams.Power) * rollOffParams.Strength, 0.0, 1.0);
	return mix(rgbLinear, neutral, rolloffMixer);
}
/*
mediump vec3 ShadowRolloff(mediump vec3 rgbLinear, mediump vec3 cameraWhiteNormalised, mediump float rgbLuminance, mediump vec3 RAWLuminanceWeight, eRollOff rollOff)
{
    RolloffParams rollOffParams = ShadowRolloffParams(rollOff);

    // Create a saturation roll-off towards the shadows by blending towards neutral cameraWhite based on the luminance
    mediump vec3 neutral = cameraWhiteNormalised * rgbLuminance;
	mediump float rolloff = smoothstep(rollOffParams.UnderLevel, rollOffParams.OverLevel, rgbLuminance);
    mediump float rolloffMixer = clamp(pow(rolloff,rollOffParams.Power) * rollOffParams.Strength, 0.0, 1.0);
	
    rgbLinear = mix(neutral, rgbLinear, rolloffMixer);
    mediump float incorrectLuminance = LuminanceWeight(rgbLinear, RAWLuminanceWeight);
   
    return ModifyLuminance(rgbLinear, incorrectLuminance, rgbLuminance);
}*/

#endif