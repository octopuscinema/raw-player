#include "ComputeDefines.cl.h"
#include "ComputeTypes.cl.h"
#include "Debayer.cl.h"
#include "Gamma.cl.h"
#include "HighlightRecovery.cl.h"
#include "ToneMapOperator.cl.h"

PRIVATE RGBHalf4 ProcessRgb(RGBHalf4 linearRgb, float2 blackWhiteLevel, eHighlightRecovery highlightRecovery, float3 cameraWhite,
	float3 cameraWhiteNormalised, float exposure, Matrix4x4 rawToDisplay, eRollOff highlightRollOff, eGamutCompression gamutCompression,
	eToneMappingOperator toneMappingOperator, eGamma gamma)
{
	// Apply black and white level
	half blackWhiteRange = (half)(blackWhiteLevel.y - blackWhiteLevel.x);
	half3 blackLevel = make_half3(blackWhiteLevel.x);
	for (int i = 0; i < 4; i++) {
		linearRgb.RGB[i] -= blackLevel;
		linearRgb.RGB[i] /= blackWhiteRange;
	}

	// Do highlight recovery
	if ( highlightRecovery == HIGHLIGHT_RECOVERY_ON )
		linearRgb = HighlightRecovery4(linearRgb, cameraWhite);
	else
		linearRgb = HighlightCorrect4(linearRgb, cameraWhiteNormalised);

	// Apply exposure
	for (int i = 0; i < 4; i++)
		linearRgb.RGB[i] *= (half)exposure;

	// Transform to display colour space
	RGBHalf4 displayRgb;
	for(int i = 0; i < 4; i++) {
		displayRgb.RGB[i] = Matrix3x3MulHalf3(linearRgb.RGB[i], rawToDisplay);
		displayRgb.RGB[i] = max(make_half3(0.0f), displayRgb.RGB[i]);
	}

	// Tone mapping, roll-off and gamut compression for Rec709/sRGB only
	if ( gamma == GAMMA_REC709 || gamma == GAMMA_SRGB ) {

		// Apply tone mapping and rolloff
		if (toneMappingOperator == TONE_MAP_NONE)
			displayRgb = HighlightRollOff709(displayRgb, Luminance4(displayRgb), highlightRollOff);
		else
			displayRgb = ToneMapAndHighlightRollOff(displayRgb, toneMappingOperator, highlightRollOff);

		// Apply gamut compression
		if ( gamutCompression == GAMUT_COMPRESSION_ON )
			displayRgb = Gamut709Compression(displayRgb);
	}

	// Apply gamma
	switch(gamma) {
    case GAMMA_REC709:
		displayRgb = ApplyGamma709(displayRgb);
		break;
	case GAMMA_SRGB:
		displayRgb = ApplyGammaSRGB(displayRgb);
		break;
	case GAMMA_LOGC3:
		displayRgb = ApplyGammaLogC3(displayRgb);
		break;
	case GAMMA_LOG3G10:
		displayRgb = ApplyGammaLog3G10(displayRgb);
		break;
	default:
		break;
	}

	return displayRgb;
}

PRIVATE half4 ProcessMono(half4 linearIn, float2 blackWhiteLevel, float exposure, eToneMappingOperator toneMappingOperator, eGamma gamma)
{
	// Apply black and white level
	linearIn -= make_half4((half)blackWhiteLevel.x);
	linearIn /= (half)(blackWhiteLevel.y - blackWhiteLevel.x);

	// Apply exposure
	linearIn *= (half)exposure;

	// Prepare for display
	half4 display = linearIn;

	// Apply tone mapping for Rec709/sRGB only
	if ( (gamma == GAMMA_REC709 || gamma == GAMMA_SRGB) && toneMappingOperator != TONE_MAP_NONE)
		display = ToneMapMono(display, toneMappingOperator);

	// Apply gamma
	switch(gamma) {
    case GAMMA_REC709:
		display = ApplyGamma709Mono(display);
		break;
	case GAMMA_SRGB:
		display = ApplyGammaSRGBMono(display);
		break;
	case GAMMA_LOGC3:
		display = ApplyGammaLogC3Mono(display);
		break;
	case GAMMA_LOG3G10:
		display = ApplyGammaLog3G10Mono(display);
		break;
	default:
		break;
	}

	return display;
}

KERNEL void ProcessBayer(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure, eToneMappingOperator toneMappingOperator, eGamma gamma,
	__write_only image2d_t output, eHighlightRecovery highlightRecovery, float3 cameraWhite, float3 cameraWhiteNormalised, Matrix4x4 rawToDisplay, eRollOff highlightRollOff,
	eGamutCompression gamutCompression
#ifdef LINEARIZE
	, __read_only image1d_t linearizeTable, float linearizeTableRange
#endif
	)
{
	int2 workCoord = make_int2(GLOBAL_ID_X, GLOBAL_ID_Y);
	int2 inputCoord = workCoord * 2;

	// Linearise and/or debayer
#ifdef LINEARIZE
	RGBHalf4 cameraRgb = LineariseDebayerBGGR(rawImage, inputCoord, linearizeTable, linearizeTableRange);
#else
	RGBHalf4 cameraRgb = DebayerBGGR(rawImage, inputCoord);
#endif
#ifdef BAYER_RB
	for (int i = 0; i < 4; i++)
		cameraRgb.RGB[i].xz = cameraRgb.RGB[i].zx;
#endif

	// Process
	RGBHalf4 displayRgb = ProcessRgb(cameraRgb, blackWhiteLevel, highlightRecovery, cameraWhite, cameraWhiteNormalised, exposure, rawToDisplay,
		highlightRollOff, gamutCompression, toneMappingOperator, gamma);

	// Write out image data
	int2 outputCoord = inputCoord;
	write_imageh(output, outputCoord, make_half4(displayRgb.RGB[0], 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 0), make_half4(displayRgb.RGB[1], 0.0f));
	write_imageh(output, outputCoord + make_int2(0, 1), make_half4(displayRgb.RGB[2], 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 1), make_half4(displayRgb.RGB[3], 0.0f));
}

KERNEL void ProcessBayerLUT(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure, eToneMappingOperator toneMappingOperator, eGamma gamma,
	__read_only image3d_t logToDisplay, __write_only image2d_t output, eHighlightRecovery highlightRecovery, float3 cameraWhite, float3 cameraWhiteNormalised,
	Matrix4x4 rawToDisplay, eRollOff highlightRollOff, eGamutCompression gamutCompression
#ifdef LINEARIZE
	, __read_only image1d_t linearizeTable, float linearizeTableRange
#endif
	)
{
	int2 workCoord = make_int2(GLOBAL_ID_X, GLOBAL_ID_Y);
	int2 inputCoord = workCoord * 2;

	// Linearise and/or debayer
#ifdef LINEARIZE
	RGBHalf4 cameraRgb = LineariseDebayerBGGR(rawImage, inputCoord, linearizeTable, linearizeTableRange);
#else
	RGBHalf4 cameraRgb = DebayerBGGR(rawImage, inputCoord);
#endif
#ifdef BAYER_RB
	for (int i = 0; i < 4; i++)
		cameraRgb.RGB[i].xz = cameraRgb.RGB[i].zx;
#endif

	// Process
	RGBHalf4 logRgb = ProcessRgb(cameraRgb, blackWhiteLevel, highlightRecovery, cameraWhite, cameraWhiteNormalised, exposure, rawToDisplay,
		highlightRollOff, gamutCompression, toneMappingOperator, gamma);

	// Apply lut
	RGBHalf4 displayRgb = ApplyLUT(logRgb, logToDisplay);

	// Write out image data
	int2 outputCoord = inputCoord;
	write_imageh(output, outputCoord, make_half4(displayRgb.RGB[0], 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 0), make_half4(displayRgb.RGB[1], 0.0f));
	write_imageh(output, outputCoord + make_int2(0, 1), make_half4(displayRgb.RGB[2], 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 1), make_half4(displayRgb.RGB[3], 0.0f));
}

KERNEL void Process(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure,
	eToneMappingOperator toneMappingOperator, eGamma gamma, __write_only image2d_t output
#ifdef LINEARIZE
	__read_only image1d_t linearizeTable, float linearizeTableRange
#endif
	)
{
	int2 workCoord = make_int2(GLOBAL_ID_X, GLOBAL_ID_Y);
	int2 inputCoord = workCoord * 2;

#ifdef LINEARIZE

	// Read mono 2x2 tile
	const sampler_t rawSampler = CLK_NORMALIZED_COORDS_FALSE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_NEAREST;
	half4 nonLinearMono = make_half4(read_imageh(rawImage, rawSampler, inputCoord).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 0)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(0, 1)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 1)).x) / (half)linearizeTableRange;

	// Linearise mono 2x2 tile
	const sampler_t lineariseSampler = CLK_NORMALIZED_COORDS_TRUE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_LINEAR;
	half4 cameraMono = make_half4(read_imageh(linearizeTable, lineariseSampler, (float)nonLinearMono.x).x,
		read_imageh(linearizeTable, lineariseSampler, (float)nonLinearMono.y).x,
		read_imageh(linearizeTable, lineariseSampler, (float)nonLinearMono.z).x,
		read_imageh(linearizeTable, lineariseSampler, (float)nonLinearMono.w).x);

#else

	// Read mono 2x2 tile
	const sampler_t rawSampler = CLK_NORMALIZED_COORDS_FALSE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_NEAREST;
	half4 cameraMono = make_half4(read_imageh(rawImage, rawSampler, inputCoord).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 0)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(0, 1)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 1)).x);

#endif

	// Process
	half4 displayMono = ProcessMono(cameraMono, blackWhiteLevel, exposure, toneMappingOperator, gamma);

	// Write out image data
	int2 outputCoord = inputCoord;
	write_imageh(output, outputCoord, make_half4(displayMono.xxx, 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 0), make_half4(displayMono.yyy, 0.0f));
	write_imageh(output, outputCoord + make_int2(0, 1), make_half4(displayMono.zzz, 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 1), make_half4(displayMono.www, 0.0f));
}

KERNEL void ProcessLUT(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure,
	eToneMappingOperator toneMappingOperator, eGamma gamma, __read_only image3d_t logToDisplay, __write_only image2d_t output
#ifdef LINEARIZE
	__read_only image1d_t linearizeTable, float linearizeTableRange
#endif
	)
{
	int2 workCoord = make_int2(GLOBAL_ID_X, GLOBAL_ID_Y);
	int2 inputCoord = workCoord * 2;

#ifdef LINEARIZE

	// Read mono 2x2 tile
	const sampler_t rawSampler = CLK_NORMALIZED_COORDS_FALSE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_NEAREST;
	half4 nonLinearMono = make_half4(read_imageh(rawImage, rawSampler, inputCoord).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 0)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(0, 1)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 1)).x) / (half)linearizeTableRange;

	// Linearise mono 2x2 tile
	const sampler_t lineariseSampler = CLK_NORMALIZED_COORDS_TRUE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_LINEAR;
	half4 cameraMono = make_half4(read_imageh(linearizeTable, lineariseSampler, (float)nonLinearMono.x).x,
		read_imageh(linearizeTable, lineariseSampler, (float)nonLinearMono.y).x,
		read_imageh(linearizeTable, lineariseSampler, (float)nonLinearMono.z).x,
		read_imageh(linearizeTable, lineariseSampler, (float)nonLinearMono.w).x);

#else

	// Read mono 2x2 tile
	const sampler_t rawSampler = CLK_NORMALIZED_COORDS_FALSE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_NEAREST;
	half4 cameraMono = make_half4(read_imageh(rawImage, rawSampler, inputCoord).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 0)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(0, 1)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 1)).x);

#endif

	// Process
	half4 displayMono = ProcessMono(cameraMono, blackWhiteLevel, exposure, toneMappingOperator, gamma);

	// Write out image data
	int2 outputCoord = inputCoord;
	write_imageh(output, outputCoord, make_half4(displayMono.xxx, 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 0), make_half4(displayMono.yyy, 0.0f));
	write_imageh(output, outputCoord + make_int2(0, 1), make_half4(displayMono.zzz, 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 1), make_half4(displayMono.www, 0.0f));
}