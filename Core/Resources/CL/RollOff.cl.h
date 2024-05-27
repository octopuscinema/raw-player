#ifndef ROLLOFF_CL_H
#define ROLLOFF_CL_H

#include "ComputeTypes.cl.h"
#include "ComputeFunctions.cl.h"

#define WHITE_CLIP_LEVEL_NORMALISED (1.0f)

#define ROLL_OFF_DEFAULT_HIGHLIGHT ROLL_OFF_MEDIUM
#define ROLL_OFF_DEFAULT_SHADOW ROLL_OFF_MEDIUM

PRIVATE RollOffParams HighlightRollOffParams(eRollOff rollOff)
{
    RollOffParams params;
    switch(rollOff) {
    case ROLL_OFF_LOW:
        params.OverLevel = WHITE_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(0.25f);
        params.UnderLevel = WHITE_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-2.0f);
        params.Power = 0.5f;
        params.Strength = 1.0f;
        return params;
    case ROLL_OFF_HIGH:
        params.OverLevel = WHITE_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(0.25f);
        params.UnderLevel = WHITE_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-2.5f);
        params.Power = 0.3f;
        params.Strength = 1.0f;
        return params;
    default:
        params.OverLevel = WHITE_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(0.25f);
        params.UnderLevel = WHITE_CLIP_LEVEL_NORMALISED * STOPS_TO_LIN(-2.5f);
        params.Power = 0.4f;
        params.Strength = 1.0f;
	    return params;
    }
}

PRIVATE RollOffParams ShadowRollOffParams(eRollOff rollOff)
{
    RollOffParams params;
    switch(rollOff) {
    case ROLL_OFF_LOW: // Low, bottom 2 stops of 12 stop range
        params.OverLevel = 2.0f / 4095.0f;
        params.UnderLevel = 0.0f / 4095.0f;
        params.Power = 0.5f;
        params.Strength = 1.0f;
        return params;
    case ROLL_OFF_HIGH: // High, bottom 6 stops of 12 stop range
        params.OverLevel = 32.0f / 4095.0f;
        params.UnderLevel = 2.0f / 4095.0f;
        params.Power = 0.5f;
        params.Strength = 1.0f;
        return params;
    default: // Medium, bottom 4.5 stops of 12 stop range
        params.OverLevel = 12.0f / 4095.0f;
        params.UnderLevel = 1.0f / 4095.0f;
        params.Power = 0.5f;
        params.Strength = 1.0f;
	    return params;
    }
}

PRIVATE RGBHalf4 HighlightRollOff709(RGBHalf4 toneMapped, half4 luminance, eRollOff rollOff)
{
	RollOffParams rollOffParams = HighlightRollOffParams(rollOff);

	// Create a saturation roll-off towards the highlights by blending towards neutral
    RGBHalf4 out;
    for(int i = 0; i < 4; i++)
    {
	    half rolloff = smoothstep(rollOffParams.UnderLevel, rollOffParams.OverLevel, IndexHalf4(luminance,i));
        half rolloffMixer = clamp(pow(rolloff,rollOffParams.Power) * rollOffParams.Strength, 0.0f, 1.0f);
	    out.RGB[i] = mix(toneMapped.RGB[i], make_half3(IndexHalf4(luminance,i)), rolloffMixer);
    }

    return out;
}

PRIVATE half3 ShadowRollOff(half3 rgbLinear, half3 cameraWhiteNormalised, half rgbLuminance, half3 rawLuminanceWeight, eRollOff rollOff)
{
    RollOffParams rollOffParams = ShadowRollOffParams(rollOff);

    // Create a saturation roll-off towards the shadows by blending towards neutral cameraWhite based on the luminance
    half3 neutral = cameraWhiteNormalised * rgbLuminance;
	half rolloff = smoothstep(rollOffParams.UnderLevel, rollOffParams.OverLevel, rgbLuminance);
    half rolloffMixer = clamp(pow(rolloff,rollOffParams.Power) * rollOffParams.Strength, 0.0f, 1.0f);
	
    rgbLinear = mix(neutral, rgbLinear, rolloffMixer);
    half incorrectLuminance = LuminanceWeight(rgbLinear, rawLuminanceWeight);
   
    return ModifyLuminance(rgbLinear, incorrectLuminance, rgbLuminance);
}

#endif