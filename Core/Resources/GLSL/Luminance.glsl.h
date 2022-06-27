#ifndef LUMINANCE_GLSL_H
#define LUMINANCE_GLSL_H

#define STOPS_TO_LIN(stops) pow(2.0, stops)

mediump float Luminance(mediump vec3 rgbLinear)
{
	return dot(rgbLinear, vec3(0.2126, 0.7152, 0.0722));
}

mediump vec3 ModifyLuminance(mediump vec3 rgbLinear, mediump float OldLuminance, mediump float NewLuminance)
{
	return rgbLinear * (NewLuminance / OldLuminance);
}

mediump float LuminanceWeight(mediump vec3 rgbLinear, mediump vec3 weights)
{
	return dot(rgbLinear, weights);
}

#endif