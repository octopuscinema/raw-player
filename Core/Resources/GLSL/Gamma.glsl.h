#ifndef GAMMA_GLSL_H
#define GAMMA_GLSL_H

mediump vec3 ApplyGamma709(mediump vec3 linearRgb)
{
    mediump float a = 1.09929682680944f;
    mediump float b = 0.018053968510807f;
    mediump float gammaPower = 1.0f / 2.2f;

    mediump vec3 Path1 = linearRgb * 4.5f;
    mediump vec3 Path2 = vec3(a) * pow(linearRgb, vec3(gammaPower)) - (vec3(a) - vec3(1.0f));

    return vec3(linearRgb.x < b ? Path1.x : Path2.x,
        linearRgb.y < b ? Path1.y : Path2.y,
        linearRgb.z < b ? Path1.z : Path2.z);
}

mediump vec3 ApplyGammaSRGB(mediump vec3 linearRgb)
{
    mediump float a = 1.055f;
    mediump float b = 0.0031308f;
    mediump float gammaPower = 1.0f / 2.4f;

    mediump vec3 Path1 = linearRgb * 12.92f;
    mediump vec3 Path2 = vec3(a) * pow(linearRgb, vec3(gammaPower)) - (vec3(a) - vec3(1.0f));

    return vec3(linearRgb.x < b ? Path1.x : Path2.x,
        linearRgb.y < b ? Path1.y : Path2.y,
        linearRgb.z < b ? Path1.z : Path2.z);
}

#endif