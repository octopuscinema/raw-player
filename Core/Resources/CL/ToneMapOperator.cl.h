#ifndef TONE_MAP_OPERATOR_CL_H
#define TONE_MAP_OPERATOR_CL_H

#include "ComputeFunctions.cl.h"
#include "RollOff.cl.h"

// This file should match the algorithm in the Davinci Resolve OCTOPUS CineForm RAW panel Dctl and in the OpenCL algorithm in CLToneMapOperator.h
#define SDR_TONEMAP_LATTITUDE_BOOST 1.0f
#define SDR_TONEMAP_MONO_LATTITUDE_BOOST 2.5f
#define LOG_TONEMAP_MAX 0.5f

// Tone shift preset
#define TONE_SHIFT_CLIP 2.0f
#define TONE_SHIFT_SHADOW_PIVOT1 0.015f
#define TONE_SHIFT_SHADOW_PIVOT2 0.05f
#define TONE_SHIFT_SHADOW_PIVOT3 0.14f
#define TONE_SHIFT_SHADOW_PIVOT2_STRENGTH 0.7f
#define TONE_SHIFT_SHADOW_PIVOT3_STRENGTH 0.6f

RGBHalf4 ToneMapSDR(RGBHalf4 rgbLinearIn, float latitudeBoostStops)
{
	// Apply exposure adjustment
	half exposureAdjustment = convert_half(exp2(latitudeBoostStops));
	RGBHalf4 exposed = RGBHalf4Scalar(rgbLinearIn, make_half4(exposureAdjustment));

	// Use reinhard style limiting
 	half4 limiterStrength4 = make_half4(1.0f);
	half4 luminance4 = Luminance4(exposed);
	half4 newLuminance4 = luminance4 / (luminance4+limiterStrength4);
	return ModifyLuminance4(exposed, luminance4, newLuminance4);
}

half4 ToneMapSDRMono(half4 monoLinearIn, half latitudeBoostStops)
{
	 // Apply exposure adjustment
    half4 exposed = monoLinearIn * convert_half(exp2(latitudeBoostStops));

    // Use reinhard style limiting
    half4 limiterStrength = make_half4(convert_half(1.0f));
    half4 luminance = exposed;
    half4 newLuminance = luminance / (luminance+limiterStrength);
    return newLuminance;
}

RGBHalf4 ToneMapSDRAndHighlightRollOff(RGBHalf4 rgbLinearIn, half latitudeBoostStops, eRollOff rollOff)
{
	// Apply exposure adjustment
	half exposureAdjustment = convert_half(exp2(latitudeBoostStops));
	RGBHalf4 exposed = RGBHalf4Scalar(rgbLinearIn, make_half4(exposureAdjustment));

	// Use reinhard style limiting
 	half4 limiterStrength4 = make_half4(1.0f);
	half4 luminance4 = Luminance4(exposed);
	half4 newLuminance4 = luminance4 / (luminance4+limiterStrength4);
	return HighlightRolloff709(ModifyLuminance4(exposed, luminance4, newLuminance4), newLuminance4, rollOff);
}

half4 ToneMapMono(half4 monoLinearIn, eToneMappingOperator toneMapOperator)
{
	if ( toneMapOperator == TONE_MAP_SDR )
		return ToneMapSDRMono(monoLinearIn, SDR_TONEMAP_MONO_LATTITUDE_BOOST);
	
	return monoLinearIn;
}

RGBHalf4 ToneMap(RGBHalf4 rgbLinearIn, eToneMappingOperator toneMapOperator)
{
	if ( toneMapOperator == TONE_MAP_SDR )
		return ToneMapSDR(rgbLinearIn, SDR_TONEMAP_LATTITUDE_BOOST);
	
	return rgbLinearIn;
}

RGBHalf4 ToneMapAndHighlightRollOff(RGBHalf4 rgbLinearIn, eToneMappingOperator toneMapOperator, eRollOff rollOff)
{
	if ( toneMapOperator == TONE_MAP_SDR )
		return ToneMapSDRAndHighlightRollOff(rgbLinearIn, SDR_TONEMAP_LATTITUDE_BOOST, rollOff);
	
	return rgbLinearIn;
}

typedef struct
{
	float Threshold;
	float3 MaxCMY;
} GamutCompressParams;

PRIVATE GamutCompressParams GetGamut709CompressParams()
{
	GamutCompressParams params;
	params.Threshold = 0.10f;
	params.MaxCMY = make_float3(0.2f, 0.2f, 0.2f);
	return params;
}

PRIVATE RGBHalf4 Gamut709Compression4(RGBHalf4 rgb)
{
	const GamutCompressParams params = GetGamut709CompressParams();

	// Amount of outer gamut to affect
	float3 th = make_float3(1.0f - params.Threshold);

	// Distance limit: How far beyond the gamut boundary to compress
	float3 dl = make_float3(1.0f) + params.MaxCMY;

	// Calculate scale so compression function passes through distance limit: (x=dl, y=1)
	float3 s = (make_float3(1.0f) - th) / sqrt(fmax(make_float3(1.001f), dl) - make_float3(1.0f));

	for (uint i = 0; i < 4; i++) {

		// Achromatic axis
		float ac = convert_float(fmax(rgb.RGB[i].x, fmax(rgb.RGB[i].y, rgb.RGB[i].z)));

		// Inverse RGB Ratios: distance from achromatic axis
		float3 d = (ac == 0.0f) ? make_float3(0.0f, 0.0f, 0.0f) : (make_float3(ac) - convert_float3(rgb.RGB[i])) / fabs(ac);

		// Compressed distance
		// Parabolic compression function: https://www.desmos.com/calculator/nvhp63hmtj
		float3 cd = s * sqrt(d - th + s * s / make_float3(4.0f)) - s * sqrt(s * s / make_float3(4.0f)) + th;
#ifdef COMPUTE_PLATFORM_OPENCL	
		cd = select(cd, d, isless(d, th));
#endif
#ifdef COMPUTE_PLATFORM_METAL
		cd = select(cd, d, (d < th));
#endif
#ifdef COMPUTE_PLATFORM_CUDA
		cd = select(cd, d, isless(d, th));
#endif

		// Inverse RGB Ratios to RGB
		rgb.RGB[i] = convert_half3(make_float3(ac) - cd * fabs(ac));
	}

	return rgb;
}

#endif