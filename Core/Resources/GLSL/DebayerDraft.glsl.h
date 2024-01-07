#ifndef DEBAYER_DRAFT_GLSL_H
#define DEBAYER_DRAFT_GLSL_H

void FetchNeighbours(sampler2D bayerImage, inout mediump float neighbours[20], highp ivec2 coords)
{
	neighbours[0] = texelFetch(bayerImage, coords - ivec2(1, 0), 0).r;
	neighbours[1] = texelFetch(bayerImage, coords + ivec2(1, 0), 0).r;
	neighbours[2] = texelFetch(bayerImage, coords - ivec2(0, 1), 0).r;
	neighbours[3] = texelFetch(bayerImage, coords + ivec2(0, 1), 0).r;
	neighbours[4] = texelFetch(bayerImage, coords - ivec2(1,1), 0).r;
	neighbours[5] = texelFetch(bayerImage, coords + ivec2(1,1), 0).r;
	neighbours[6] = texelFetch(bayerImage, coords + ivec2(1, -1), 0).r;
	neighbours[7] = texelFetch(bayerImage, coords + ivec2(-1, 1), 0).r;
	neighbours[8] = texelFetch(bayerImage, coords - ivec2(2, 0), 0).r;
	neighbours[9] = texelFetch(bayerImage, coords + ivec2(2, 0), 0).r;
	neighbours[10] = texelFetch(bayerImage, coords - ivec2(0, 2), 0).r;
	neighbours[11] = texelFetch(bayerImage, coords + ivec2(0, 2), 0).r;
	neighbours[12] = texelFetch(bayerImage, coords + ivec2(1, 2), 0).r;
	neighbours[13] = texelFetch(bayerImage, coords + ivec2(-1, 2), 0).r;
	neighbours[14] = texelFetch(bayerImage, coords + ivec2(1, -2), 0).r;
	neighbours[15] = texelFetch(bayerImage, coords + ivec2(-1, -2), 0).r;
	neighbours[16] = texelFetch(bayerImage, coords + ivec2(2, 1), 0).r;
	neighbours[17] = texelFetch(bayerImage, coords + ivec2(2, -1), 0).r;
	neighbours[18] = texelFetch(bayerImage, coords + ivec2(-2, 1), 0).r;
	neighbours[19] = texelFetch(bayerImage, coords + ivec2(-2, -1), 0).r;
}

mediump vec3 Debayer(sampler2D bayerImage, highp ivec2 coords, mediump ivec2 greenLocations[2], mediump ivec2 nonGreen1Location)
{
	// Lookup 21 pixels
	mediump float centre = texelFetch(bayerImage, coords, 0).r;
	mediump float neighbours[20];
	FetchNeighbours(bayerImage, neighbours, coords);

	// Apply psuedo guassian filter
	mediump ivec2 bayerIndex = ivec2(mod(vec2(coords), 2.0));
	if (bayerIndex == greenLocations[0] || bayerIndex == greenLocations[1])
	{
		// Green
		mediump float green = centre*0.25 + (neighbours[4] + neighbours[5] + neighbours[6] + neighbours[7])*0.1875;
		mediump float nonGreen1 = ((neighbours[0] + neighbours[1]) * 0.5) * 0.25 +
			(neighbours[12] + neighbours[13] + neighbours[14] + neighbours[15]) * 0.1875;
		mediump float nonGreen2 = ((neighbours[2] + neighbours[3]) * 0.5) * 0.25 +
			(neighbours[16] + neighbours[17] + neighbours[18] + neighbours[19]) * 0.1875;
		return (bayerIndex == greenLocations[1]) ? vec3(nonGreen1, green, nonGreen2) : vec3(nonGreen2, green, nonGreen1);
	}
	else
	{
		// Not Green
		mediump float nonGreen1 = centre*0.25 + (neighbours[8] + neighbours[9] + neighbours[10] + neighbours[11])*0.1875;
		mediump float green = (neighbours[0] + neighbours[1] + neighbours[2] + neighbours[3]) * 0.125 +
			(neighbours[12] + neighbours[13] + neighbours[14] + neighbours[15] + neighbours[16] + neighbours[17] + neighbours[18] + neighbours[19]) * 0.0625;
		mediump float nonGreen2 = (neighbours[4] + neighbours[5] + neighbours[6] + neighbours[7]) * 0.25;
		return (bayerIndex == nonGreen1Location) ? vec3(nonGreen1, green, nonGreen2) : vec3(nonGreen2, green, nonGreen1);
	}
}

mediump float Linearize(mediump float input, sampler1D table, highp float tableRange)
{
	return texture(table, min(1.0, input / tableRange)).r;
}

mediump vec3 LinearizeDebayer(sampler2D bayerImage, highp ivec2 coords, mediump ivec2 greenLocations[2], mediump ivec2 nonGreen1Location, sampler1D linearize, highp float linearizeRange)
{
	// Lookup 21 pixels
	mediump float centre = Linearize(texelFetch(bayerImage, coords, 0).r, linearize, linearizeRange);
	mediump float neighbours[20];
	FetchNeighbours(bayerImage, neighbours, coords);
	for (int i = 0; i < 20; i++)
		neighbours[i] = Linearize(neighbours[i], linearize, linearizeRange);

	// Apply psuedo guassian filter
	mediump ivec2 bayerIndex = ivec2(mod(vec2(coords), 2.0));
	if (bayerIndex == greenLocations[0] || bayerIndex == greenLocations[1])
	{
		// Green
		mediump float green = centre * 0.25 + (neighbours[4] + neighbours[5] + neighbours[6] + neighbours[7]) * 0.1875;
		mediump float nonGreen1 = ((neighbours[0] + neighbours[1]) * 0.5) * 0.25 +
			(neighbours[12] + neighbours[13] + neighbours[14] + neighbours[15]) * 0.1875;
		mediump float nonGreen2 = ((neighbours[2] + neighbours[3]) * 0.5) * 0.25 +
			(neighbours[16] + neighbours[17] + neighbours[18] + neighbours[19]) * 0.1875;
		return (bayerIndex == greenLocations[1]) ? vec3(nonGreen1, green, nonGreen2) : vec3(nonGreen2, green, nonGreen1);
	}
	else
	{
		// Not Green
		mediump float nonGreen1 = centre * 0.25 + (neighbours[8] + neighbours[9] + neighbours[10] + neighbours[11]) * 0.1875;
		mediump float green = (neighbours[0] + neighbours[1] + neighbours[2] + neighbours[3]) * 0.125 +
			(neighbours[12] + neighbours[13] + neighbours[14] + neighbours[15] + neighbours[16] + neighbours[17] + neighbours[18] + neighbours[19]) * 0.0625;
		mediump float nonGreen2 = (neighbours[4] + neighbours[5] + neighbours[6] + neighbours[7]) * 0.25;
		return (bayerIndex == nonGreen1Location) ? vec3(nonGreen1, green, nonGreen2) : vec3(nonGreen2, green, nonGreen1);
	}
}

mediump vec3 DebayerXGGX(sampler2D bayerImage, highp ivec2 coords)
{
	mediump ivec2 greenLocations[2] = ivec2[](ivec2(0, 1), ivec2(1, 0));
	mediump ivec2 nonGreen1Location = ivec2(0, 0);
	return Debayer(bayerImage, coords, greenLocations, nonGreen1Location);
}

mediump vec3 DebayerGXXG(sampler2D bayerImage, highp ivec2 coords)
{
	mediump ivec2 greenLocations[2] = ivec2[]( ivec2(0, 0), ivec2(1, 1) );
	mediump ivec2 nonGreen1Location = ivec2(0, 1);
	return Debayer(bayerImage, coords, greenLocations, nonGreen1Location).zyx;
}

mediump vec3 LinearizeDebayerXGGX(sampler2D bayerImage, highp ivec2 coords, sampler1D linearize, highp float linearizeRange)
{
	mediump ivec2 greenLocations[2] = ivec2[](ivec2(0, 1), ivec2(1, 0));
	mediump ivec2 nonGreen1Location = ivec2(0, 0);
	return LinearizeDebayer(bayerImage, coords, greenLocations, nonGreen1Location, linearize, linearizeRange);
}

mediump vec3 LinearizeDebayerGXXG(sampler2D bayerImage, highp ivec2 coords, sampler1D linearize, highp float linearizeRange)
{
	mediump ivec2 greenLocations[2] = ivec2[](ivec2(0, 0), ivec2(1, 1));
	mediump ivec2 nonGreen1Location = ivec2(0, 1);
	return LinearizeDebayer(bayerImage, coords, greenLocations, nonGreen1Location, linearize, linearizeRange).zyx;
}

#endif