#ifndef DEBAYERPROXY_GLSL_H
#define DEBAYERPROXY_GLSL_H

vec3 DebayerXGGX(sampler2D source, highp vec2 coords, highp vec4 sourceSize)
{
	highp ivec2 integerCoord = ivec2(coords * sourceSize.xy);

	// Lookup 21 pixels (TODO)
	mediump float centre = texelFetch(source, integerCoord, 0).r;
	mediump float neighbours[12];
	neighbours[0] = texelFetch(source, integerCoord - ivec2(1, 0), 0).r;
	neighbours[1] = texelFetch(source, integerCoord + ivec2(1, 0), 0).r;
	neighbours[2] = texelFetch(source, integerCoord - ivec2(0, 1), 0).r;
	neighbours[3] = texelFetch(source, integerCoord + ivec2(0, 1), 0).r;
	neighbours[4] = texelFetch(source, integerCoord - ivec2(1,1), 0).r;
	neighbours[5] = texelFetch(source, integerCoord + ivec2(1,1), 0).r;
	neighbours[6] = texelFetch(source, integerCoord + ivec2(1, -1), 0).r;
	neighbours[7] = texelFetch(source, integerCoord + ivec2(-1, 1), 0).r;
	neighbours[8] = texelFetch(source, integerCoord - ivec2(2, 0), 0).r;
	neighbours[9] = texelFetch(source, integerCoord + ivec2(2, 0), 0).r;
	neighbours[10] = texelFetch(source, integerCoord - ivec2(0, 2), 0).r;
	neighbours[11] = texelFetch(source, integerCoord + ivec2(0, 2), 0).r;

	mediump ivec2 bayerIndex = ivec2(mod(vec2(integerCoord), 2.0));
	if (bayerIndex == ivec2(0, 1) || bayerIndex == ivec2(1, 0)) // green
	{
		mediump float green = centre*0.25 + (neighbours[4] + neighbours[5] + neighbours[6] + neighbours[7])*0.1875;
		mediump float nonGreen1 = (neighbours[0] + neighbours[1]) * 0.5;
		mediump float nonGreen2 = (neighbours[2] + neighbours[3]) * 0.5;
		return (bayerIndex == ivec2(1, 0)) ? vec3(nonGreen1, green, nonGreen2) : vec3(nonGreen2, green, nonGreen1);
	}
	else // non-green
	{
		mediump float nonGreen1 = centre*0.25 + (neighbours[8] + neighbours[9] + neighbours[10] + neighbours[11])*0.1875;
		mediump float green = (neighbours[0] + neighbours[1] + neighbours[2] + neighbours[3]) * 0.25;
		mediump float nonGreen2 = (neighbours[4] + neighbours[5] + neighbours[6] + neighbours[7]) * 0.25;
		return (bayerIndex == ivec2(0,0)) ? vec3(nonGreen1, green, nonGreen2) : vec3(nonGreen2, green, nonGreen1);
	}
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