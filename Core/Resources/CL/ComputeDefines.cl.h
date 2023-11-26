#ifndef COMPUTE_DEFINES_CL_H
#define COMPUTE_DEFINES_CL_H

// Cross platform Compute API defines
#ifdef __OPENCL_VERSION__
#define COMPUTE_PLATFORM_OPENCL
#endif

#ifdef __METAL_VERSION__
#define COMPUTE_PLATFORM_METAL
#endif

#if defined(cl_khr_fp16) || defined(COMPUTE_PLATFORM_METAL)
#define SUPPORTS_NATIVE_HALF_PRECISION
#endif

// From https://gist.github.com/yohhoy/dafa5a47dade85d8b40625261af3776a
#define YUV_COEFFICIENTS_REC709_ABC (float3)(0.2126f, 0.7152f, 0.0722f)
#define YUV_COEFFICIENTS_REC709_DE (float2)(1.8556f, 1.5748f)
#define YUV_COEFFICIENTS_REC2020_ABC (float3)(0.2627f, 0.6780f, 0.0593f)
#define YUV_COEFFICIENTS_REC2020_DE (float2)(1.8814f, 1.4747f)

// Misc stuff
#define CLIP_LEVEL_12BIT 4095
#define LUMINANCE_WEIGHTS_709 0.2126f, 0.7152f, 0.0722f

// OpenCL 'half' precision
#ifdef COMPUTE_PLATFORM_OPENCL
#ifdef cl_khr_fp16
#pragma OPENCL EXTENSION cl_khr_fp16 : enable
#else
#define half float
#define half2 float2
#define half3 float3
#define half4 float4
#define half8 float8
#define convert_half2 convert_float2
#define convert_half3 convert_float3
#define convert_half4 convert_float4
#define read_imageh read_imagef
#define write_imageh write_imagef
#endif
#endif

// Cross platform vector constructor
#ifdef COMPUTE_PLATFORM_OPENCL
#define make_half2 (half2)
#define make_half3 (half3)
#define make_half4 (half4)
#define make_float2 (float2)
#define make_float3 (float3)
#define make_float4 (float4)
#define make_ushort2 (ushort2)
#define make_ushort3 (ushort3)
#define make_ushort4 (ushort4)
#define make_short2 (short2)
#define make_short3 (short3)
#define make_short4 (short4)
#define make_uint2 (uint2)
#define make_uint3 (uint3)
#define make_uint4 (uint4)
#define make_int2 (int2)
#define make_int3 (int3)
#define make_int4 (int4)
#endif
#ifdef COMPUTE_PLATFORM_METAL
#define make_half2 half2
#define make_half3 half3
#define make_half4 half4
#define make_float2 float2
#define make_float3 float3
#define make_float4 float4
#define make_ushort2 ushort2
#define make_ushort3 ushort3
#define make_ushort4 ushort4
#define make_short2 short2
#define make_short3 short3
#define make_short4 short4
#define make_uint2 uint2
#define make_uint3 uint3
#define make_uint4 uint4
#define make_int2 int2
#define make_int3 int3
#define make_int4 int4
#endif

// Metal type conversion wrappers to match OpenCL
#ifdef COMPUTE_PLATFORM_METAL
#define convert_half2 static_cast<half2>
#define convert_half3 static_cast<half3>
#define convert_half4 static_cast<half4>
#define convert_float2 static_cast<float2>
#define convert_float3 static_cast<float3>
#define convert_float4 static_cast<float4>
#define convert_int2 static_cast<int2>
#define convert_int3 static_cast<int3>
#define convert_int4 static_cast<int4>
#define convert_uint2 static_cast<uint2>
#define convert_uint3 static_cast<uint3>
#define convert_uint4 static_cast<uint4>
#define convert_ushort2 static_cast<ushort2>
#define convert_ushort3 static_cast<ushort3>
#define convert_ushort4 static_cast<ushort4>
#endif

// Simulate missing 'native' functions for metal
#ifdef COMPUTE_PLATFORM_METAL
#define native_powr powr
#define native_log2 log2
#endif

// Cross platform popular sampler modes
#ifdef COMPUTE_PLATFORM_OPENCL
#define SAMPLER sampler_t
#define SAMPLER_NORMALIZED_CLAMP_TO_EDGE_LINEAR (CLK_NORMALIZED_COORDS_TRUE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_LINEAR)
#define SAMPLER_NORMALIZED_CLAMP_TO_EDGE_POINT (CLK_NORMALIZED_COORDS_TRUE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_NEAREST)
#endif
#ifdef COMPUTE_PLATFORM_METAL
#define SAMPLER sampler
#define SAMPLER_NORMALIZED_CLAMP_TO_EDGE_LINEAR sampler(coord::normalized, address::clamp_to_edge, filter::linear)
#define SAMPLER_NORMALIZED_CLAMP_TO_EDGE_POINT sampler(coord::normalized, address::clamp_to_edge, filter::nearest)
#endif

// Cross platform buffer/image wrapper
#ifdef COMPUTE_PLATFORM_OPENCL
#define BUFFER_READ_ONLY(T) global const T* restrict
#define IMAGE1D_READ_ONLY(T) __read_only image1d_t
#define IMAGE2D_READ_ONLY(T) __read_only image2d_t
#define IMAGE1D_WRITE_ONLY(T) __write_only image1d_t
#define IMAGE2D_WRITE_ONLY(T) __write_only image2d_t
#endif
#ifdef COMPUTE_PLATFORM_METAL
#define BUFFER_READ_ONLY(T) const device T*
#define IMAGE1D_READ_ONLY(T) texture1d<T, access::sample>
#define IMAGE2D_READ_ONLY(T) texture2d<T, access::sample>
#define IMAGE1D_WRITE_ONLY(T) texture1d<T, access::write>
#define IMAGE2D_WRITE_ONLY(T) texture2d<T, access::write>
#endif

#endif
