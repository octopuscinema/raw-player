#include "ComputeDefines.cl.h"
#include "ComputeTypes.cl.h"
#include "Debayer.cl.h"
#include "Gamma.cl.h"
#include "HighlightRecovery.cl.h"
#include "ToneMapOperator.cl.h"

PRIVATE RGBHalf4 ProcessRgb(RGBHalf4 linearRgb, float2 blackWhiteLevel, eHighlightRecovery highlightRecovery, float3 cameraWhite,
	float3 cameraWhiteNormalised, float exposure, Matrix4x4 rawToDisplay, eRollOff highlightRollOff, eToneMappingOperator toneMappingOperator
	/*, __read_only image3d_t logToDisplay*/)
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
	for(int i = 0; i < 4; i++)
		displayRgb.RGB[i] = Matrix3x3MulHalf3(linearRgb.RGB[i], rawToDisplay);

	// Apply tone mapping
	if (toneMappingOperator != TONE_MAP_NONE)
		displayRgb = ToneMapAndHighlightRollOff(displayRgb, toneMappingOperator, highlightRollOff);

	// Apply gamma
	displayRgb = ApplyGamma709(displayRgb);

	return displayRgb;
}

PRIVATE half4 ProcessMono(half4 linearIn, float2 blackWhiteLevel, float exposure, eToneMappingOperator toneMappingOperator
	/*, __read_only image3d_t logToDisplay*/)
{
	// Apply black and white level
	linearIn -= make_half4((half)blackWhiteLevel.x);
	linearIn /= (half)(blackWhiteLevel.y - blackWhiteLevel.x);

	// Apply exposure
	linearIn *= (half)exposure;

	// Prepare for display
	half4 display = linearIn;

	// Apply tone mapping
	if (toneMappingOperator != TONE_MAP_NONE)
		display = ToneMapMono(display, toneMappingOperator);

	// Apply gamma
	display = ApplyGamma709Mono(display);

	return display;
}

KERNEL void ProcessBayerNonLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure, eToneMappingOperator toneMappingOperator,
	/*__read_only image3d_t logToDisplay, */ __write_only image2d_t output,
	eHighlightRecovery highlightRecovery, float3 cameraWhite, float3 cameraWhiteNormalised, Matrix4x4 rawToDisplay, eRollOff highlightRollOff, __read_only image1d_t linearizeTable, float linearizeTableRange)
{
	int2 workCoord = make_int2(GLOBAL_ID_X, GLOBAL_ID_Y);
	int2 inputCoord = workCoord * 2;

	// Linearise and debayer
	RGBHalf4 cameraRgb = LineariseDebayerBGGR(rawImage, inputCoord, linearizeTable, linearizeTableRange);
#ifdef BAYER_RB
	for (int i = 0; i < 4; i++)
		cameraRgb.RGB[i].xz = cameraRgb.RGB[i].zx;
#endif

	// Process
	RGBHalf4 displayRgb = ProcessRgb(cameraRgb, blackWhiteLevel, highlightRecovery, cameraWhite, cameraWhiteNormalised, exposure, rawToDisplay,
		highlightRollOff, toneMappingOperator);

	// Write out image data
	int2 outputCoord = inputCoord;
	write_imageh(output, outputCoord, make_half4(displayRgb.RGB[0], 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 0), make_half4(displayRgb.RGB[1], 0.0f));
	write_imageh(output, outputCoord + make_int2(0, 1), make_half4(displayRgb.RGB[2], 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 1), make_half4(displayRgb.RGB[3], 0.0f));
}

KERNEL void ProcessBayerLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure, eToneMappingOperator toneMappingOperator,
	/*__read_only image3d_t logToDisplay, */__write_only image2d_t output,
	eHighlightRecovery highlightRecovery, float3 cameraWhite, float3 cameraWhiteNormalised, Matrix4x4 rawToDisplay, eRollOff highlightRollOff)
{
	int2 workCoord = make_int2(GLOBAL_ID_X, GLOBAL_ID_Y);
	int2 inputCoord = workCoord * 2;

	// Debayer
	RGBHalf4 cameraRgb = DebayerBGGR(rawImage, inputCoord);
#ifdef BAYER_RB
	for (int i = 0; i < 4; i++)
		cameraRgb.RGB[i].xz = cameraRgb.RGB[i].zx;
#endif

	// Process
	RGBHalf4 displayRgb = ProcessRgb(cameraRgb, blackWhiteLevel, highlightRecovery, cameraWhite, cameraWhiteNormalised, exposure, rawToDisplay, highlightRollOff,
		toneMappingOperator);

	// Write out image data
	int2 outputCoord = inputCoord;
	write_imageh(output, outputCoord, make_half4(displayRgb.RGB[0], 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 0), make_half4(displayRgb.RGB[1], 0.0f));
	write_imageh(output, outputCoord + make_int2(0, 1), make_half4(displayRgb.RGB[2], 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 1), make_half4(displayRgb.RGB[3], 0.0f));
}

KERNEL void ProcessNonLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure,
	eToneMappingOperator toneMappingOperator, /*__read_only image3d_t logToDisplay,*/ __write_only image2d_t output,
	__read_only image1d_t linearizeTable, float linearizeTableRange)
{
	int2 workCoord = make_int2(GLOBAL_ID_X, GLOBAL_ID_Y);
	int2 inputCoord = workCoord * 2;

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

	// Process
	half4 displayMono = ProcessMono(cameraMono, blackWhiteLevel, exposure, toneMappingOperator);

	// Write out image data
	int2 outputCoord = inputCoord;
	write_imageh(output, outputCoord, make_half4(displayMono.xxx, 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 0), make_half4(displayMono.yyy, 0.0f));
	write_imageh(output, outputCoord + make_int2(0, 1), make_half4(displayMono.zzz, 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 1), make_half4(displayMono.www, 0.0f));
}

KERNEL void ProcessLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure,
	eToneMappingOperator toneMappingOperator, /*__read_only image3d_t logToDisplay,*/ __write_only image2d_t output)
{
	int2 workCoord = make_int2(GLOBAL_ID_X, GLOBAL_ID_Y);
	int2 inputCoord = workCoord * 2;

	// Read mono 2x2 tile
	const sampler_t rawSampler = CLK_NORMALIZED_COORDS_FALSE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_NEAREST;
	half4 cameraMono = make_half4(read_imageh(rawImage, rawSampler, inputCoord).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 0)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(0, 1)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 1)).x);

	// Process
	half4 displayMono = ProcessMono(cameraMono, blackWhiteLevel, exposure, toneMappingOperator);

	// Write out image data
	int2 outputCoord = inputCoord;
	write_imageh(output, outputCoord, make_half4(displayMono.xxx, 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 0), make_half4(displayMono.yyy, 0.0f));
	write_imageh(output, outputCoord + make_int2(0, 1), make_half4(displayMono.zzz, 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 1), make_half4(displayMono.www, 0.0f));
}
