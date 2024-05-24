#ifndef COMPUTE_MATHS_H
#define COMPUTE_MATHS_H

#include "ComputeDefines.cl.h"
#include "ComputeTypes.cl.h"

#define STOPS_TO_LIN(stops) exp2(convert_half(stops))

PRIVATE RGBHalf4 RGBHalf4Scalar(RGBHalf4 rgb4, half4 scalar4)
{
	half4 r4 = make_half4(rgb4.RGB[0].x, rgb4.RGB[1].x, rgb4.RGB[2].x, rgb4.RGB[3].x) * scalar4;
	half4 g4 = make_half4(rgb4.RGB[0].y, rgb4.RGB[1].y, rgb4.RGB[2].y, rgb4.RGB[3].y) * scalar4;
	half4 b4 = make_half4(rgb4.RGB[0].z, rgb4.RGB[1].z, rgb4.RGB[2].z, rgb4.RGB[3].z) * scalar4;

	RGBHalf4 Out;
	Out.RGB[0] = make_half3(r4.x, g4.x, b4.x);
	Out.RGB[1] = make_half3(r4.y, g4.y, b4.y);
	Out.RGB[2] = make_half3(r4.z, g4.z, b4.z);
	Out.RGB[3] = make_half3(r4.w, g4.w, b4.w);

	return Out;
}

PRIVATE RGBHalf4 RGBHalf4Add(RGBHalf4 rgb4, half4 add4)
{
	half4 r4 = make_half4(rgb4.RGB[0].x, rgb4.RGB[1].x, rgb4.RGB[2].x, rgb4.RGB[3].x) + add4;
	half4 g4 = make_half4(rgb4.RGB[0].y, rgb4.RGB[1].y, rgb4.RGB[2].y, rgb4.RGB[3].y) + add4;
	half4 b4 = make_half4(rgb4.RGB[0].z, rgb4.RGB[1].z, rgb4.RGB[2].z, rgb4.RGB[3].z) + add4;

	RGBHalf4 Out;
	Out.RGB[0] = make_half3(r4.x, g4.x, b4.x);
	Out.RGB[1] = make_half3(r4.y, g4.y, b4.y);
	Out.RGB[2] = make_half3(r4.z, g4.z, b4.z);
	Out.RGB[3] = make_half3(r4.w, g4.w, b4.w);

	return Out;
}

#ifdef SUPPORTS_VEC8
PRIVATE RGBHalf8 RGBHalf8Scalar(RGBHalf8 rgb8, half8 scalar8)
{

	half8 r8 = (half8)(rgb8.RGB[0].x, rgb8.RGB[1].x, rgb8.RGB[2].x, rgb8.RGB[3].x, rgb8.RGB[4].x, rgb8.RGB[5].x, rgb8.RGB[6].x, rgb8.RGB[7].x) * scalar8;
	half8 g8 = (half8)(rgb8.RGB[0].y, rgb8.RGB[1].y, rgb8.RGB[2].y, rgb8.RGB[3].y, rgb8.RGB[4].y, rgb8.RGB[5].y, rgb8.RGB[6].y, rgb8.RGB[7].y) * scalar8;
	half8 b8 = (half8)(rgb8.RGB[0].z, rgb8.RGB[1].z, rgb8.RGB[2].z, rgb8.RGB[3].z, rgb8.RGB[4].z, rgb8.RGB[5].z, rgb8.RGB[6].z, rgb8.RGB[7].z) * scalar8;

	RGBHalf8 Out;
	Out.RGB[0] = make_half3(r8.s0, g8.s0, b8.s0);
	Out.RGB[1] = make_half3(r8.s1, g8.s1, b8.s1);
	Out.RGB[2] = make_half3(r8.s2, g8.s2, b8.s2);
	Out.RGB[3] = make_half3(r8.s3, g8.s3, b8.s3);
	Out.RGB[4] = make_half3(r8.s4, g8.s4, b8.s4);
	Out.RGB[5] = make_half3(r8.s5, g8.s5, b8.s5);
	Out.RGB[6] = make_half3(r8.s6, g8.s6, b8.s6);
	Out.RGB[7] = make_half3(r8.s7, g8.s7, b8.s7);

	return Out;
}

PRIVATE RGBHalf8 RGBHalf8Add(RGBHalf8 rgb8, half8 add8)
{

	half8 r8 = (half8)(rgb8.RGB[0].x, rgb8.RGB[1].x, rgb8.RGB[2].x, rgb8.RGB[3].x, rgb8.RGB[4].x, rgb8.RGB[5].x, rgb8.RGB[6].x, rgb8.RGB[7].x) + add8;
	half8 g8 = (half8)(rgb8.RGB[0].y, rgb8.RGB[1].y, rgb8.RGB[2].y, rgb8.RGB[3].y, rgb8.RGB[4].y, rgb8.RGB[5].y, rgb8.RGB[6].y, rgb8.RGB[7].y) + add8;
	half8 b8 = (half8)(rgb8.RGB[0].z, rgb8.RGB[1].z, rgb8.RGB[2].z, rgb8.RGB[3].z, rgb8.RGB[4].z, rgb8.RGB[5].z, rgb8.RGB[6].z, rgb8.RGB[7].z) + add8;

	RGBHalf8 Out;
	Out.RGB[0] = make_half3(r8.s0, g8.s0, b8.s0);
	Out.RGB[1] = make_half3(r8.s1, g8.s1, b8.s1);
	Out.RGB[2] = make_half3(r8.s2, g8.s2, b8.s2);
	Out.RGB[3] = make_half3(r8.s3, g8.s3, b8.s3);
	Out.RGB[4] = make_half3(r8.s4, g8.s4, b8.s4);
	Out.RGB[5] = make_half3(r8.s5, g8.s5, b8.s5);
	Out.RGB[6] = make_half3(r8.s6, g8.s6, b8.s6);
	Out.RGB[7] = make_half3(r8.s7, g8.s7, b8.s7);

	return Out;
}
#endif

PRIVATE float3 Matrix3x3MulFloat3(float3 vector, Matrix4x4 matrix)
{
#ifdef COMPUTE_PLATFORM_METAL
    float3x3 native3x3 = float3x3(matrix.row0.xyz, matrix.row1.xyz, matrix.row2.xyz);
    return vector * native3x3;
#else
	return make_float3( dot(vector, GET_XYZ(float, matrix.row0)), dot(vector, GET_XYZ(float, matrix.row1)), dot(vector, GET_XYZ(float, matrix.row2)) );
#endif
}

#ifdef SUPPORTS_VEC8
PRIVATE RGBHalf8 Matrix3x3Mul3RGB8Half(half8 r, half8 g, half8 b, Matrix4x4 matrix)
{
#ifdef cl_khr_fp16
	half3 matrixRow0 = convert_half3(matrix.row0.xyz);
	half3 matrixRow1 = convert_half3(matrix.row1.xyz);
	half3 matrixRow2 = convert_half3(matrix.row2.xyz);
#else
	half3 matrixRow0 = matrix.row0.xyz;
	half3 matrixRow1 = matrix.row1.xyz;
	half3 matrixRow2 = matrix.row2.xyz;
#endif
	
	half8 vector_a = (half8)(r.s0, g.s0, b.s0, r.s1, g.s1, b.s1, r.s2, g.s2);
	half8 vector_b = (half8)(b.s2, r.s3, g.s3, b.s3, r.s4, g.s4, b.s4, r.s5);
	half8 vector_c = (half8)(g.s5, b.s5, r.s6, g.s6, b.s6, r.s7, g.s7, b.s7);

	half8 row0_a = (half8)(matrixRow0, matrixRow0, matrixRow0.xy) * vector_a;
	half8 row0_b = (half8)(matrixRow0.z, matrixRow0, matrixRow0, matrixRow0.x) * vector_b;
	half8 row0_c = (half8)(matrixRow0.yz, matrixRow0, matrixRow0) * vector_c;
	
	half8 row1_a = (half8)(matrixRow1, matrixRow1, matrixRow1.xy) * vector_a;
	half8 row1_b = (half8)(matrixRow1.z, matrixRow1, matrixRow1, matrixRow1.x) * vector_b;
	half8 row1_c = (half8)(matrixRow1.yz, matrixRow1, matrixRow1) * vector_c;
	
	half8 row2_a = (half8)(matrixRow2, matrixRow2, matrixRow2.xy) * vector_a;
	half8 row2_b = (half8)(matrixRow2.z, matrixRow2, matrixRow2, matrixRow2.x) * vector_b;
	half8 row2_c = (half8)(matrixRow2.yz, matrixRow2, matrixRow2) * vector_c;
	
	RGBHalf8 Out;
	
	half8 Out_a = (half8)(row0_a.s0, row1_a.s0, row2_a.s0, row0_a.s3, row1_a.s3, row2_a.s3, row0_a.s6, row1_a.s6) 
				+ (half8)(row0_a.s1, row1_a.s1, row2_a.s1, row0_a.s4, row1_a.s4, row2_a.s4, row0_a.s7, row1_a.s7) 
				+ (half8)(row0_a.s2, row1_a.s2, row2_a.s2, row0_a.s5, row1_a.s5, row2_a.s5, row0_b.s0, row1_b.s0);
	
	half8 Out_b = (half8)(row2_a.s6, row0_b.s1, row1_b.s1, row2_b.s1, row0_b.s4, row1_b.s4, row2_b.s4, row0_b.s7)
				+ (half8)(row2_a.s7, row0_b.s2, row1_b.s2, row2_b.s2, row0_b.s5, row1_b.s5, row2_b.s5, row0_c.s0)
				+ (half8)(row2_b.s0, row0_b.s3, row1_b.s3, row2_b.s3, row0_b.s6, row1_b.s6, row2_b.s6, row0_c.s1);
	
	half8 Out_c = (half8)(row1_b.s7, row2_b.s7, row0_c.s2, row1_c.s2, row2_c.s2, row0_c.s5, row1_c.s5, row2_c.s5)
				+ (half8)(row1_c.s0, row2_c.s0, row0_c.s3, row1_c.s3, row2_c.s3, row0_c.s6, row1_c.s6, row2_c.s6)
				+ (half8)(row1_c.s1, row2_c.s1, row0_c.s4, row1_c.s4, row2_c.s4, row0_c.s7, row1_c.s7, row2_c.s7);
	
	Out.RGB[0] = Out_a.s012;
	Out.RGB[1] = Out_a.s345;
	Out.RGB[2] = make_half3(Out_a.s67, Out_b.s0);
	Out.RGB[3] = Out_b.s123;
	Out.RGB[4] = Out_b.s456;
	Out.RGB[5] = make_half3(Out_b.s7, Out_c.s01);
	Out.RGB[6] = Out_c.s234;
	Out.RGB[7] = Out_c.s567;
	
	return Out;
}

PRIVATE RGBHalf8 Matrix3x3Mul8RGB3Half(half3 rgb0, half3 rgb1, half3 rgb2, half3 rgb3, half3 rgb4, half3 rgb5, half3 rgb6, half3 rgb7, Matrix4x4 matrix)
{
	half8 r = (half8)(rgb0.x,rgb1.x,rgb2.x,rgb3.x,rgb4.x,rgb5.x,rgb6.x, rgb7.x);
	half8 g = (half8)(rgb0.y,rgb1.y,rgb2.y,rgb3.y,rgb4.y,rgb5.y,rgb6.y, rgb7.y);
	half8 b = (half8)(rgb0.z,rgb1.z,rgb2.z,rgb3.z,rgb4.z,rgb5.z,rgb6.z, rgb7.z);
	return Matrix3x3Mul3RGB8Half(r, g, b, matrix);
}
#endif

PRIVATE half3 Matrix3x3MulHalf3(half3 vector, Matrix4x4 matrix)
{
#ifdef COMPUTE_PLATFORM_METAL
    half3x3 native3x3 = half3x3( convert_half3(matrix.row0.xyz), convert_half3(matrix.row1.xyz), convert_half3(matrix.row2.xyz));
    return vector * native3x3;
#endif
#ifdef COMPUTE_PLATFORM_OPENCL
#ifdef SUPPORTS_NATIVE_HALF_PRECISION
    return make_half3( dot(vector, convert_half3(matrix.row0.xyz)),
        dot(vector, convert_half3(matrix.row1.xyz)),
        dot(vector, convert_half3(matrix.row2.xyz)) );
#else
    return Matrix3x3MulFloat3(vector, matrix);
#endif
#endif
#ifdef COMPUTE_PLATFORM_CUDA
	return make_half3( 
		dot(vector, make_half3(matrix.row0.x, matrix.row0.y, matrix.row0.z)),
        dot(vector, make_half3(matrix.row1.x, matrix.row1.y, matrix.row1.z)),
        dot(vector, make_half3(matrix.row2.x, matrix.row2.y, matrix.row2.z)) );
#endif
}

#endif
