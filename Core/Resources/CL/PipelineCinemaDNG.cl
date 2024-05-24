#if defined(cl_khr_fp16)
#pragma OPENCL EXTENSION cl_khr_fp16 : enable
#endif

#include "ComputeDefines.cl.h"
#include "ComputeTypes.cl.h"
#include "Debayer.cl.h"

PRIVATE RGBHalf4 ProcessRgb(RGBHalf4 linearRgb, float2 blackWhiteLevel, float exposure/*, Matrix4x4 cameraToLog, __read_only image3d_t logToDisplay*/)
{
	// Apply black and white level
	float blackWhiteRange = blackWhiteLevel.y - blackWhiteLevel.x;
	half3 blackLevel = make_half3(blackWhiteLevel.x);
	for (int i = 0; i < 4; i++) {
		linearRgb.RGB[i] -= blackLevel;
		linearRgb.RGB[i] /= blackWhiteRange;
	}

	// Prepare for display
	RGBHalf4 displayRgb = linearRgb;

	// Apply tone mapping
	//if (toneMappingOperator != ToneMappingOperatorNone)
		//displayRgb = ToneMap(displayRgb, toneMappingOperator);

	// Apply exposure
	for (int i = 0; i < 4; i++)
		displayRgb.RGB[i] *= exposure;

	return displayRgb;
}

PRIVATE half4 ProcessMono(half4 linearIn, float2 blackWhiteLevel, float exposure/*, __read_only image3d_t logToDisplay*/)
{
	// Apply black and white level
	linearIn -= make_half4(blackWhiteLevel.x);
	linearIn /= (blackWhiteLevel.y - blackWhiteLevel.x);

	// Prepare for display
	half4 display = linearIn;

	// Apply tone mapping

	// Apply exposure
	display *= exposure;

	return display;
}

KERNEL void ProcessBayerNonLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure, /*__read_only image3d_t logToDisplay, */ __write_only image2d_t output,/*
	Matrix4x4 cameraToLog,*/ __read_only image1d_t linearizeTable, float linearizeTableRange)
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
	RGBHalf4 displayRgb = ProcessRgb(cameraRgb, blackWhiteLevel, exposure);

	// Write out image data
	int2 outputCoord = inputCoord;
	write_imageh(output, outputCoord, make_half4(displayRgb.RGB[0], 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 0), make_half4(displayRgb.RGB[1], 0.0f));
	write_imageh(output, outputCoord + make_int2(0, 1), make_half4(displayRgb.RGB[2], 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 1), make_half4(displayRgb.RGB[3], 0.0f));
}

KERNEL void ProcessBayerLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure, /*__read_only image3d_t logToDisplay, */__write_only image2d_t output/*,
	Matrix4x4 cameraToLog*/)
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
	RGBHalf4 displayRgb = ProcessRgb(cameraRgb, blackWhiteLevel, exposure);

	// Write out image data
	int2 outputCoord = inputCoord;
	write_imageh(output, outputCoord, make_half4(displayRgb.RGB[0], 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 0), make_half4(displayRgb.RGB[1], 0.0f));
	write_imageh(output, outputCoord + make_int2(0, 1), make_half4(displayRgb.RGB[2], 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 1), make_half4(displayRgb.RGB[3], 0.0f));
}

KERNEL void ProcessNonLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure, /*__read_only image3d_t logToDisplay,*/ __write_only image2d_t output,
	__read_only image1d_t linearizeTable, float linearizeTableRange)
{
	int2 workCoord = make_int2(GLOBAL_ID_X, GLOBAL_ID_Y);
	int2 inputCoord = workCoord * 2;

	// Read mono 2x2 tile
	const sampler_t rawSampler = CLK_NORMALIZED_COORDS_FALSE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_NEAREST;
	half4 nonLinearMono = make_half4(read_imageh(rawImage, rawSampler, inputCoord).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 0)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(0, 1)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 1)).x) / linearizeTableRange;

	// Linearise mono 2x2 tile
	const sampler_t lineariseSampler = CLK_NORMALIZED_COORDS_TRUE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_LINEAR;
	half4 cameraMono = make_half4(read_imageh(linearizeTable, lineariseSampler, nonLinearMono.x).x,
		read_imageh(linearizeTable, lineariseSampler, nonLinearMono.y).x,
		read_imageh(linearizeTable, lineariseSampler, nonLinearMono.z).x,
		read_imageh(linearizeTable, lineariseSampler, nonLinearMono.w).x);

	// Process
	half4 displayMono = ProcessMono(cameraMono, blackWhiteLevel, exposure);

	// Write out image data
	int2 outputCoord = inputCoord;
	write_imageh(output, outputCoord, make_half4(displayMono.xxx, 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 0), make_half4(displayMono.yyy, 0.0f));
	write_imageh(output, outputCoord + make_int2(0, 1), make_half4(displayMono.zzz, 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 1), make_half4(displayMono.www, 0.0f));
}

KERNEL void ProcessLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure, /*__read_only image3d_t logToDisplay,*/ __write_only image2d_t output)
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
	half4 displayMono = ProcessMono(cameraMono, blackWhiteLevel, exposure);

	// Write out image data
	int2 outputCoord = inputCoord;
	write_imageh(output, outputCoord, make_half4(displayMono.xxx, 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 0), make_half4(displayMono.yyy, 0.0f));
	write_imageh(output, outputCoord + make_int2(0, 1), make_half4(displayMono.zzz, 0.0f));
	write_imageh(output, outputCoord + make_int2(1, 1), make_half4(displayMono.www, 0.0f));
}