#if defined(cl_khr_fp16)
#pragma OPENCL EXTENSION cl_khr_fp16 : enable
#endif

#include "ComputeDefines.cl.h"
#include "ComputeTypes.cl.h"

inline RGBHalf4 ProcessRGB4(RGBHalf4 linearIn, float2 blackWhiteLevel, float exposure, Matrix4x4 cameraToLog, __read_only image3d_t logToDisplay)
{
	// Apply black and white level
	//linearIn -= vec3(blackWhiteLevel.x);
	//linearIn /= (blackWhiteLevel.y - blackWhiteLevel.x);

	return linearIn;
}

inline half4 ProcessMono4(half4 linearIn, float2 blackWhiteLevel, float exposure, __read_only image3d_t logToDisplay)
{
	// Apply black and white level
	//linearIn -= vec3(blackWhiteLevel.x);
	//linearIn /= (blackWhiteLevel.y - blackWhiteLevel.x);

	return linearIn;
}

inline RGBHalf4 LineariseRGB4(RGBHalf4 input, __read_only image1d_t linearizeTable, float linearizeTableRange)
{
	return input;
}

inline half4 LineariseMono4(half4 input, __read_only image1d_t linearizeTable, float linearizeTableRange)
{
	return input;
}

kernel void ProcessBayerNonLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, __read_only image1d_t linearizeTable, float linearizeTableRange, 
	float exposure, Matrix4x4 cameraToLog, __read_only image3d_t logToDisplay, __write_only image2d_t output)
{
	//RGBHalf4 cameraRGB = DebayerQuad();
	//RGBHalf4 displayRGB = ProcessRGB4(LineariseRGB4(cameraRGB));
	//Writeimage(displayRGB);
}

kernel void ProcessBayerLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure, Matrix4x4 cameraToLog, __read_only image3d_t logToDisplay,
	__write_only image2d_t output)
{
	//RGBHalf4 cameraRGB = DebayerQuad();
	//RGBHalf4 displayRGB = ProcessRGB4(cameraRGB);
	//Writeimage(displayRGB);
}

kernel void ProcessNonLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, __read_only image1d_t linearizeTable, float linearizeTableRange, 
	float exposure, __read_only image3d_t logToDisplay, __write_only image2d_t output)
{
	//half4 camera = readImage();
	//half4 display = ProcessMono4(LineariseMono4(camera));
	//Writeimage(display);
}

kernel void ProcessLinear(__read_only image2d_t rawImage, float2 blackWhiteLevel, float exposure, __read_only image3d_t logToDisplay, __write_only image2d_t output)
{
	//half4 camera = readImage();
	//half4 display = ProcessMono4(camera);
	//Writeimage(display);
}