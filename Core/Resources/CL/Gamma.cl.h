#ifndef GAMMA_CL_H
#define GAMMA_CL_H

#include "ComputeDefines.cl.h"
#include "ComputeTypes.cl.h"

PRIVATE RGBHalf4 ApplyGamma709(RGBHalf4 linearRgb)
{
    half a = 1.09929682680944f;
    half b = 0.018053968510807f;
    half gammaPower = 1.0f / 2.2f;

    RGBHalf4 out;
    for(int i = 0; i < 4; i++)
    {
        half3 Path1 = linearRgb.RGB[i] * 4.5f;
        half3 Path2 = make_half3(a) * pow(linearRgb.RGB[i], make_half3(gammaPower)) - (make_half3(a) - make_half3(1.0f));

        out.RGB[i] = make_half3(linearRgb.RGB[i].x < b ? Path1.x : Path2.x,
            linearRgb.RGB[i].y < b ? Path1.y : Path2.y,
            linearRgb.RGB[i].z < b ? Path1.z : Path2.z);
    }
    return out;
}

PRIVATE half4 ApplyGamma709Mono(half4 linearMono)
{
    half a = 1.09929682680944f;
    half b = 0.018053968510807f;
    half gammaPower = 1.0f / 2.2f;

    half4 Path1 = linearMono * 4.5f;
    half4 Path2 = make_half4(a) * pow(linearMono, make_half4(gammaPower)) - (make_half4(a) - make_half4(1.0f));

    return select(Path2, Path1, isgreater(make_half4(b), linearMono));
}

PRIVATE RGBHalf4 ApplyGammaSRGB(RGBHalf4 linearRgb)
{
    half a = 1.055f;
    half b = 0.0031308f;
    half gammaPower = 1.0f / 2.4f;

    RGBHalf4 out;
    for(int i = 0; i < 4; i++)
    {
        half3 Path1 = linearRgb.RGB[i] * 12.92f;
        half3 Path2 = make_half3(a) * pow(linearRgb.RGB[i], make_half3(gammaPower)) - (make_half3(a) - make_half3(1.0f));

        out.RGB[i] = make_half3(linearRgb.RGB[i].x < b ? Path1.x : Path2.x,
            linearRgb.RGB[i].y < b ? Path1.y : Path2.y,
            linearRgb.RGB[i].z < b ? Path1.z : Path2.z);
    }
    return out;
}

PRIVATE half4 ApplyGammaSRGBMono(half4 linearMono)
{
    half a = 1.055f;
    half b = 0.0031308f;
    half gammaPower = 1.0f / 2.4f;

    half4 Path1 = linearMono * 12.92f;
    half4 Path2 = make_half4(a) * pow(linearMono, make_half4(gammaPower)) - (make_half4(a) - make_half4(1.0f));

    return select(Path2, Path1, isgreater(make_half4(b), linearMono));
}

#endif