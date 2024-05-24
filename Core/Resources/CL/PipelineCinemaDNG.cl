#if defined(cl_khr_fp16)
#pragma OPENCL EXTENSION cl_khr_fp16 : enable
#endif

#include "ComputeDefines.cl.h"
#include "ComputeTypes.cl.h"

PRIVATE RGBHalf4 ProcessRGB4(RGBHalf4 linearIn, float2 blackWhiteLevel, float exposure, Matrix4x4 cameraToLog, __read_only image3d_t logToDisplay)
{
	// Apply black and white level
	//linearIn -= vec3(blackWhiteLevel.x);
	//linearIn /= (blackWhiteLevel.y - blackWhiteLevel.x);

	return linearIn;
}

PRIVATE half4 ProcessMono4(half4 linearIn, float2 blackWhiteLevel, float exposure, __read_only image3d_t logToDisplay)
{
	// Apply black and white level
	//linearIn -= vec3(blackWhiteLevel.x);
	//linearIn /= (blackWhiteLevel.y - blackWhiteLevel.x);

	return linearIn;
}

PRIVATE RGBHalf4 LineariseRGB4(RGBHalf4 input, __read_only image1d_t linearizeTable, float linearizeTableRange)
{
	return input;
}

PRIVATE half4 LineariseMono4(half4 input, __read_only image1d_t linearizeTable, float linearizeTableRange)
{
	return input;
}

KERNEL void ProcessBayerNonLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure, /*__read_only image3d_t logToDisplay, */ __write_only image2d_t output/*,
	Matrix4x4 cameraToLog, __read_only image1d_t linearizeTable, float linearizeTableRange*/)
{
	int2 workCoord = make_int2(GLOBAL_ID_X, GLOBAL_ID_Y);
	int2 inputCoord = workCoord * 2;

	// Placeholder debayer
	const sampler_t rawSampler = CLK_NORMALIZED_COORDS_FALSE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_NEAREST;
	float topLeft = read_imagef(rawImage, rawSampler, inputCoord).x;
	float topRight = read_imagef(rawImage, rawSampler, inputCoord + make_int2(1, 0)).x;
	float bottomLeft = read_imagef(rawImage, rawSampler, inputCoord + make_int2(0, 1)).x;
	float bottomRight = read_imagef(rawImage, rawSampler, inputCoord + make_int2(1, 1)).x;
	float3 cameraRgb = make_float3(topLeft, (topRight + bottomLeft) * 0.5f, bottomRight);

	// Apply black and white level
	cameraRgb -= make_float3(blackWhiteLevel.x);
	cameraRgb /= (blackWhiteLevel.y-blackWhiteLevel.x);

	// Write out image data
	int2 outputCoord = inputCoord;
	write_imagef(output, outputCoord, make_float4(cameraRgb, 0.0f));
	write_imagef(output, outputCoord + make_int2(1, 0), make_float4(cameraRgb, 0.0f));
	write_imagef(output, outputCoord + make_int2(0, 1), make_float4(cameraRgb, 0.0f));
	write_imagef(output, outputCoord + make_int2(1, 1), make_float4(cameraRgb, 0.0f));
	
	//RGBHalf4 cameraRGB = DebayerQuad();
	//RGBHalf4 displayRGB = ProcessRGB4(LineariseRGB4(cameraRGB));
	//Writeimage(displayRGB);
}

KERNEL void ProcessBayerLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure, /*__read_only image3d_t logToDisplay, */__write_only image2d_t output/*,
	Matrix4x4 cameraToLog*/)
{
	int2 workCoord = make_int2(GLOBAL_ID_X, GLOBAL_ID_Y);
	int2 inputCoord = workCoord * 2;
	
	// Placeholder debayer
	const sampler_t rawSampler = CLK_NORMALIZED_COORDS_FALSE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_NEAREST;
	float topLeft = read_imagef(rawImage, rawSampler, inputCoord).x;
	float topRight = read_imagef(rawImage, rawSampler, inputCoord + make_int2(1, 0)).x;
	float bottomLeft = read_imagef(rawImage, rawSampler, inputCoord + make_int2(0, 1)).x;
	float bottomRight = read_imagef(rawImage, rawSampler, inputCoord + make_int2(1, 1)).x;
	float3 cameraRgb = make_float3(topLeft, (topRight + bottomLeft) * 0.5f, bottomRight);

	// Apply black and white level
	cameraRgb -= make_float3(blackWhiteLevel.x);
	cameraRgb /= (blackWhiteLevel.y-blackWhiteLevel.x);

	// Write out image data
	int2 outputCoord = inputCoord;
	write_imagef(output, outputCoord, make_float4(cameraRgb, 0.0f));
	write_imagef(output, outputCoord + make_int2(1, 0), make_float4(cameraRgb, 0.0f));
	write_imagef(output, outputCoord + make_int2(0, 1), make_float4(cameraRgb, 0.0f));
	write_imagef(output, outputCoord + make_int2(1, 1), make_float4(cameraRgb, 0.0f));

	//RGBHalf4 cameraRGB = DebayerQuad();
	//RGBHalf4 displayRGB = ProcessRGB4(cameraRGB);
	//Writeimage(displayRGB);
}

KERNEL void ProcessNonLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure, __read_only image3d_t logToDisplay, __write_only image2d_t output,
	__read_only image1d_t linearizeTable, float linearizeTableRange)
{
	//half4 camera = readImage();
	//half4 display = ProcessMono4(LineariseMono4(camera));
	//Writeimage(display);
}

KERNEL void ProcessLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure, __read_only image3d_t logToDisplay, __write_only image2d_t output)
{
	//half4 camera = readImage();
	//half4 display = ProcessMono4(camera);
	//Writeimage(display);
}