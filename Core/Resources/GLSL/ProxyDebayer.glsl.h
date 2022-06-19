#ifndef DEBAYERPROXY_GLSL_H
#define DEBAYERPROXY_GLSL_H

vec3 DebayerXGGX(sampler2D source, vec2 coords, vec4 sourceSize)
{
	vec2 halfTexelSize = sourceSize.zw * 0.5;
	vec2 index = floor(coords * sourceSize.xy * 0.5) * 2.0;
	vec2 topLeft = (index * sourceSize.zw) + halfTexelSize;

	float firstCol = texture2D(source, topLeft).r;
	float green1 = texture2D(source, topLeft + vec2(sourceSize.z, 0.0)).r;
	float green2 = texture2D(source, topLeft + vec2(0.0, sourceSize.w)).r;
	float lastCol = texture2D(source, topLeft + sourceSize.zw).r;

	return vec3(firstCol, (green1 + green2) * 0.5, lastCol);
}

vec3 DebayerGXXG(sampler2D source, vec2 coords, vec4 sourceSize)
{
	vec2 halfTexelSize = sourceSize.zw * 0.5;
	vec2 index = floor(coords * sourceSize.xy * 0.5) * 2.0;
	vec2 topLeft = (index * sourceSize.zw) + halfTexelSize;

	float green1 = texture2D(source, topLeft).r;
	float firstCol = texture2D(source, topLeft + vec2(sourceSize.z, 0.0)).r;
	float lastCol = texture2D(source, topLeft + vec2(0.0, sourceSize.w)).r;
	float green2 = texture2D(source, topLeft + sourceSize.zw).r;

	return vec3(firstCol, (green1 + green2) * 0.5, lastCol);
}

#endif