#ifndef COMPUTE_FUNCTIONS_CL_H
#define COMPUTE_FUNCTIONS_CL_H

#define LUMINANCE_VEC3 0.2126f, 0.7152f, 0.0722f
#define STOPS_TO_LIN(stops) pow(2.0f, stops)

PRIVATE half Luminance(half3 rgbLinear)
{
	return dot(rgbLinear, make_half3(LUMINANCE_VEC3));
}

PRIVATE half3 ModifyLuminance(half3 rgbLinear, half OldLuminance, half NewLuminance)
{
    return rgbLinear * (NewLuminance / OldLuminance);
}

PRIVATE half LuminanceWeight(half3 rgbLinear, half3 weights)
{
	return dot(rgbLinear, weights);
}

#endif