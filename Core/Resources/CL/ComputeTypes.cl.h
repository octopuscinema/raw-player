#ifndef COMPUTE_TYPES_CL_H
#define COMPUTE_TYPES_CL_H

#include "ComputeDefines.cl.h"

typedef struct
{
	half3 RGB[8];
} RGBHalf8;

typedef struct
{
	half3 RGB[4];
} RGBHalf4;

typedef struct
{
    ushort3 RGB[4];
} RGBUShort4;

typedef struct
{
    float3 RGB[4];
} RGBFloat4;

#ifdef COMPUTE_PLATFORM_METAL
typedef struct
{
	packed_float3 row0;
	packed_float3 row1;
	packed_float3 row2;
} Matrix3x3;
inline float3x3 convert_float3x3(Matrix3x3 m)
{
    float3x3 nativeOut;
    nativeOut[0] = m.row0;
    nativeOut[1] = m.row1;
    nativeOut[2] = m.row2;
    return nativeOut;
}
#endif

typedef struct
{
	float4 row0;
	float4 row1;
	float4 row2;
	float4 row3;
} Matrix4x4;

typedef struct
{
	float3 topLeft;
	float3 topRight;
	float3 bottomLeft;
	float3 bottomRight;
} QuadRGB;

typedef struct
{
	half3 topLeft;
	half3 topRight;
	half3 bottomLeft;
	half3 bottomRight;
} QuadRGBHalf;

typedef struct
{
	float4 Y;
	float2 UV;
} YUV420Quad;

typedef struct
{
	float4 YUYV;
} YUV422Pair;

typedef struct
{
	float4 Y;
	float4 UVUV;
} YUV422Quad;

typedef struct
{
	float4 RGB_R;
	float4 GB_RG;
	float4 B_RGB;
} RGB444Quad;

typedef struct
{
	half UnderLevel;
    half OverLevel;
    half Power;
    half Strength;
} RollOffParams;

typedef enum
{
	GAMMA_REC709 = 0,
	GAMMA_SRGB = 1,
	GAMMA_LOGC3 = 2,
	GAMMA_LOG3G10 = 3,
	GAMMA_FILMGEN5 = 4
} eGamma;

typedef enum
{
	TONE_MAP_NONE = 0,
	TONE_MAP_SDR = 1
} eToneMappingOperator;

typedef enum
{
	HIGHLIGHT_RECOVERY_OFF = 0,
	HIGHLIGHT_RECOVERY_ON = 1
} eHighlightRecovery;

typedef enum
{
	ROLL_OFF_NONE = 0,
    ROLL_OFF_LOW = 1,
    ROLL_OFF_MEDIUM = 2,
    ROLL_OFF_HIGH = 3
} eRollOff;

typedef enum
{
    GAMUT_COMPRESSION_OFF = 0,
    GAMUT_COMPRESSION_ON = 1
} eGamutCompression;

typedef enum
{
	RGB_CHANNEL_UNSET = -1,
	RGB_CHANNEL_RED = 0,
	RGB_CHANNEL_GREEN = 1,
	RGB_CHANNEL_BLUE = 2
} eRGBChannel;

#endif
