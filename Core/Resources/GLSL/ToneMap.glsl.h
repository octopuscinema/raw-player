#ifndef TONEMAP_GLSL_H
#define TONEMAP_GLSL_H

// These need to match the host c#/c++ ToneMappingOperator enum
#define ToneMappingOperator lowp uint
const ToneMappingOperator ToneMappingOperatorNone = 0u;
const ToneMappingOperator ToneMappingOperatorSDR = 1u;
const ToneMappingOperator ToneMappingOperatorLog = 2u;

// This file should match the algorithm in the Davinci Resolve OCTOPUS CineForm RAW panel Dctl and in the OpenCL algorithm in CLToneMapOperator.h
#define SDR_TONEMAP_LATTITUDE_BOOST 1.0
#define SDR_TONEMAP_MONO_LATTITUDE_BOOST 2.5
#define LOG_TONEMAP_MAX 0.75

// Tone shift preset
#define TONE_SHIFT_CLIP 2.0
#define TONE_SHIFT_SHADOW_PIVOT1 0.015
#define TONE_SHIFT_SHADOW_PIVOT2 0.05
#define TONE_SHIFT_SHADOW_PIVOT3 0.14
#define TONE_SHIFT_SHADOW_PIVOT2_STRENGTH 0.7
#define TONE_SHIFT_SHADOW_PIVOT3_STRENGTH 0.6

#define LUMINANCE_VEC3 0.2126, 0.7152, 0.0722

mediump float Luminance(mediump vec3 rgbLinear)
{
	return dot(rgbLinear, vec3(LUMINANCE_VEC3));
}

mediump vec3 ModifyLuminance(mediump vec3 rgbLinear, mediump float OldLuminance, mediump float NewLuminance)
{
	return rgbLinear * (NewLuminance / OldLuminance);
}

mediump float ToneMapSDRMono(mediump float monoLinearIn, mediump float latitudeBoostStops)
{
	// Apply exposure adjustment
	mediump float exposed = monoLinearIn * pow(2.0, latitudeBoostStops);

	// Use reinhard style limiting
	mediump float limiterStrength = 1.0;
	mediump float luminance = exposed;
	mediump float newLuminance = luminance / (luminance + limiterStrength);
	return newLuminance;
}

mediump vec3 ToneMapSDR(mediump vec3 rgbLinearIn, mediump float latitudeBoostStops)
{
	// Apply exposure adjustment
	mediump vec3 exposed = rgbLinearIn * pow(2.0, latitudeBoostStops);

	// Use reinhard style limiting
	mediump float limiterStrength = 1.0;
	mediump float luminance = Luminance(exposed);
	mediump float newLuminance = luminance / (luminance + limiterStrength);
	return ModifyLuminance(exposed, luminance, newLuminance);
}

mediump vec3 ToneMapLog(mediump vec3 rgbLinearIn, mediump float white)
{
	return rgbLinearIn * white;
}

mediump float ToneMapMono(mediump float monoLinearIn, ToneMappingOperator toneMapOperator)
{
	if (toneMapOperator == ToneMappingOperatorSDR)
		return ToneMapSDRMono(monoLinearIn, SDR_TONEMAP_MONO_LATTITUDE_BOOST);
	return monoLinearIn;
}

mediump vec3 ToneMap(mediump vec3 rgbLinearIn, ToneMappingOperator toneMapOperator)
{
	if (toneMapOperator == ToneMappingOperatorSDR)
		return ToneMapSDR(rgbLinearIn, SDR_TONEMAP_LATTITUDE_BOOST);
	else if (toneMapOperator == ToneMappingOperatorLog)
		return ToneMapLog(rgbLinearIn, LOG_TONEMAP_MAX);

	return rgbLinearIn;
}

mediump vec3 ToneShift(mediump vec3 rgbLinearIn, mediump float pivot, mediump float ev)
{
	mediump float aboveRegion = TONE_SHIFT_CLIP - pivot;
	mediump float exposureAdjust = pow(2.0, ev);
	mediump float newPivot = pivot * exposureAdjust;
	mediump float newAboveRegion = TONE_SHIFT_CLIP - newPivot;

	mediump vec3 distanceToPivot = rgbLinearIn - vec3(pivot);
	mediump vec3 pivotRatio = distanceToPivot / aboveRegion;

	mediump vec3 newAboveValue = vec3(newPivot) + (pivotRatio * newAboveRegion);
	mediump vec3 newBelowValue = rgbLinearIn * exposureAdjust;

	return vec3((rgbLinearIn.x < pivot) ? newBelowValue.x : newAboveValue.x,
		(rgbLinearIn.y < pivot) ? newBelowValue.y : newAboveValue.y,
		(rgbLinearIn.z < pivot) ? newBelowValue.z : newAboveValue.z);
}

mediump vec3 Gamut709Compression(mediump vec3 rgb)
{
	mediump float paramsThreshold = 0.10;
	mediump vec3 paramsMaxCMY = vec3(0.2, 0.2, 0.2);

	// Amount of outer gamut to affect
	mediump vec3 th = vec3(1.0 - paramsThreshold);

	// Distance limit: How far beyond the gamut boundary to compress
	mediump vec3 dl = vec3(1.0) + paramsMaxCMY;

	// Calculate scale so compression function passes through distance limit: (x=dl, y=1)
	mediump vec3 s = (vec3(1.0) - th) / sqrt(max(vec3(1.001), dl) - vec3(1.0));

	// Achromatic axis
	mediump float ac = max(rgb.x, max(rgb.y, rgb.z));

	// Inverse RGB Ratios: distance from achromatic axis
	mediump vec3 d = (ac == 0.0) ? vec3(0.0) : (vec3(ac) - rgb) / abs(ac);

	// Compressed distance
	// Parabolic compression function: https://www.desmos.com/calculator/nvhp63hmtj
	mediump vec3 cd = s * sqrt(d - th + s * s / vec3(4.0)) - s * sqrt(s * s / vec3(4.0)) + th;
	cd.x = (d.x < th.x) ? d.x : cd.x;
	cd.y = (d.y < th.y) ? d.y : cd.y;
	cd.z = (d.z < th.z) ? d.z : cd.z;

	// Inverse RGB Ratios to RGB
	return vec3(ac) - cd * abs(ac);
}

#endif