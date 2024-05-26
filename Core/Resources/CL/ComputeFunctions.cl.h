#ifndef COMPUTE_FUNCTIONS_CL_H
#define COMPUTE_FUNCTIONS_CL_H

#include "ComputeMaths.cl.h"
#include "ComputeTypes.cl.h"

#define LUMINANCE_WEIGHTS_709 0.2126f, 0.7152f, 0.0722f

PRIVATE half Luminance(half3 rgbLinear)
{
	return dot(rgbLinear, make_half3(LUMINANCE_WEIGHTS_709));
}

PRIVATE half4 Luminance4(RGBHalf4 rgbLinear)
{
	return make_half4( dot(rgbLinear.RGB[0], make_half3(LUMINANCE_WEIGHTS_709)),
		dot(rgbLinear.RGB[1], make_half3(LUMINANCE_WEIGHTS_709)),
		dot(rgbLinear.RGB[2], make_half3(LUMINANCE_WEIGHTS_709)),
		dot(rgbLinear.RGB[3], make_half3(LUMINANCE_WEIGHTS_709)) );
}

PRIVATE half3 ModifyLuminance(half3 rgbLinear, half OldLuminance, half NewLuminance)
{
    return rgbLinear * (NewLuminance / OldLuminance);
}

PRIVATE RGBHalf4 ModifyLuminance4(RGBHalf4 rgbLinear, half4 OldLuminance4, half4 NewLuminance4)
{
	return RGBHalf4Scalar(rgbLinear, NewLuminance4 / OldLuminance4);
}

PRIVATE half LuminanceWeight(half3 rgbLinear, half3 weights)
{
	return dot(rgbLinear, weights);
}

#endif