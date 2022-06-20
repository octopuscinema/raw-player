// GLSL Vertex shader program
#ifdef VERT

// Interpolated fragment/vertex values
out highp vec2 normalisedCoordinates;

layout(location = 0) in highp vec2 VertexPosition;

//uniform vec4 RectUV;
uniform highp vec4 RectBounds;
uniform highp vec2 OrthographicBoundsInverse;

void main(void)
{
	// Calculate Texture Coordinates from vertex position
	highp vec2 UV0 = vec2(0.0,1.0);//RectUV.xy;
	highp vec2 UV1 = vec2(1.0,0.0);//RectUV.zw;
	normalisedCoordinates = UV0 + VertexPosition*(UV1-UV0);
	
	// Calculate position
	vec2 Translate = RectBounds.xy;
	vec2 Scale = RectBounds.zw;
	highp vec2 Position = Translate + VertexPosition*Scale;
	
	// Apply orthographic matrix (stored as bounds instead for speed)
	Position = ((Position * OrthographicBoundsInverse)*2.0) - sign(OrthographicBoundsInverse);
	gl_Position = vec4(Position, 0.0, 1.0);
}
#endif

// GLSL Fragment/pixel shader program
#ifdef FRAG

// Interpolated fragment/vertex values
in highp vec2 normalisedCoordinates;

#ifndef MONOCHROME
#include "DebayerDraft.glsl.h"
#endif

uniform sampler2D rawImage;

out lowp vec4 fragColor;

void main() 
{
	highp float exposure = 32.0;

#ifdef MONOCHROME
	mediump float pixel = texture(rawImage,normalisedCoordinates).r * exposure;
	lowp vec3 rgbOut = vec3(pixel, pixel, pixel);
#endif

#ifdef BAYER_XGGX
	lowp vec3 rgbOut = DebayerXGGX(rawImage, normalisedCoordinates) * exposure;
#endif
#ifdef BAYER_GXXG
	lowp vec3 rgbOut = DebayerGXXG(rawImage, normalisedCoordinates) * exposure;
#endif

#ifdef BAYER_BR
	rgbOut.xz = rgbOut.zx;
#endif

	fragColor = vec4(rgbOut,1);
}
#endif