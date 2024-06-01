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

PRIVATE RGBHalf4 ApplyGammaLog3G10(RGBHalf4 linearRgb)
{
    // Constants
    half a = 0.224282f;
    half b = 155.975327f;
    half c = 0.01f;
    half g = 15.1927f;

    RGBHalf4 out;
    for(int i = 0; i < 4; i++)
    {
        half3 x = linearRgb.RGB[i] + make_half3(c);

        half3 path1 = x*g;
        half3 path2 = log10((x * b) + make_half3(1.0f)) * a;

        out.RGB[i] = select(path2, path1, isless(x, make_half3(0.0f)));
    }

    return out;
}

PRIVATE half4 ApplyGammaLog3G10Mono(half4 linearMono)
{
    // Constants
    half a = 0.224282f;
    half b = 155.975327f;
    half c = 0.01f;
    half g = 15.1927f;
    
    half4 x = linearMono + make_half4(c);

    half4 path1 = x*g;
    half4 path2 = log10((x * b) + make_half4(1.0f)) * a;

    return select(path2, path1, isless(x, make_half4(0.0f)));
}

PRIVATE RGBHalf4 ApplyGammaBlackmagicFilmG5(RGBHalf4 linearRgb)
{
    // Constants
    half a = 0.08692876065491224f;
    half b = 0.005494072432257808f;
    half c = 0.5300133392291939f;
    half d = 8.283605932402494f;
    half e = 0.09246575342465753f;
    half linCut = 0.005f;

    RGBHalf4 out;
    for (int i = 0; i < 4; i++)
    {
        half3 x = linearRgb.RGB[i];

        half3 path1 = make_half3(d)*x + make_half3(e);
        half3 path2 = make_half3(a) * log(x + make_half3(b)) + make_half3(c);

        out.RGB[i] = select(path2, path1, isless(x, make_half3(linCut)));
    }

    return out;
}

PRIVATE half4 ApplyGammaBlackmagicFilmG5Mono(half4 linearMono)
{
    // Constants
    half a = 0.08692876065491224f;
    half b = 0.005494072432257808f;
    half c = 0.5300133392291939f;
    half d = 8.283605932402494f;
    half e = 0.09246575342465753f;
    half linCut = 0.005f;

    half4 x = linearMono;

    half4 path1 = make_half4(d) * x + make_half4(e);
    half4 path2 = make_half4(a) * log(x + make_half4(b)) + make_half4(c);

    return select(path2, path1, isless(x, make_half4(linCut)));
}

PRIVATE RGBHalf4 ApplyLUT(RGBHalf4 rgbLog, __read_only image3d_t logToDisplay)
{
    const sampler_t lutSampler = CLK_NORMALIZED_COORDS_TRUE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_LINEAR;
    RGBHalf4 out;

    for(int i = 0; i < 4; i++)
        out.RGB[i] = read_imageh(logToDisplay, lutSampler, make_float4(rgbLog.RGB[i].xyz, 0.0f)).xyz;

    return out;
}

PRIVATE RGBHalf4 ApplyLUTMono(half4 monoLog, __read_only image3d_t logToDisplay)
{
    const sampler_t lutSampler = CLK_NORMALIZED_COORDS_TRUE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_LINEAR;
    RGBHalf4 out;

    out.RGB[0] = read_imageh(logToDisplay, lutSampler, make_float4(monoLog.xxx, 0.0f)).xyz;
    out.RGB[1] = read_imageh(logToDisplay, lutSampler, make_float4(monoLog.yyy, 0.0f)).xyz;
    out.RGB[2] = read_imageh(logToDisplay, lutSampler, make_float4(monoLog.zzz, 0.0f)).xyz;
    out.RGB[3] = read_imageh(logToDisplay, lutSampler, make_float4(monoLog.www, 0.0f)).xyz;

    return out;
}

#endif