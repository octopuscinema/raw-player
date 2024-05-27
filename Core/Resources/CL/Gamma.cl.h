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
        half3 path1 = linearRgb.RGB[i] * 4.5f;
        half3 path2 = make_half3(a) * pow(linearRgb.RGB[i], make_half3(gammaPower)) - (make_half3(a) - make_half3(1.0f));

        out.RGB[i] = select(path2, path1, isgreater(make_half3(b),linearRgb.RGB[i]));
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
        half3 path1 = linearRgb.RGB[i] * 12.92f;
        half3 path2 = make_half3(a) * pow(linearRgb.RGB[i], make_half3(gammaPower)) - (make_half3(a) - make_half3(1.0f));

        out.RGB[i] = select(path2, path1, isgreater(make_half3(b),linearRgb.RGB[i]));
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

PRIVATE RGBHalf4 ApplyGammaLogC3(RGBHalf4 linearRgb)
{
    // Constants
    half cut = 0.010591f;
    half a = 5.555556f;
    half b = 0.052272f;
    half c = 0.247190f;
    half d = 0.385537f;
    half e = 5.367655f;
    half f = 0.092809f;

    RGBHalf4 out;
    for(int i = 0; i < 4; i++)
    {
        half3 x = linearRgb.RGB[i];

        half3 path1 = e * x + make_half3(f);
        half3 path2 = c * log10(a * x + make_half3(b)) + make_half3(d);

        out.RGB[i] = select(path1, path2, isgreater(x, make_half3(cut)));
    }

    return out;
}

PRIVATE half4 ApplyGammaLogC3Mono(half4 linearMono)
{
    // Constants
    half cut = 0.010591f;
    half a = 5.555556f;
    half b = 0.052272f;
    half c = 0.247190f;
    half d = 0.385537f;
    half e = 5.367655f;
    half f = 0.092809f;

    half4 path1 = e * linearMono + make_half4(f);
    half4 path2 = c * log10(a * linearMono + make_half4(b)) + make_half4(d);

    return select(path1, path2, isgreater(linearMono, make_half4(cut)));
}

#endif