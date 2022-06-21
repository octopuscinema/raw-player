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
uniform mediump mat3 cameraToDisplayColour;
#endif

uniform sampler2D rawImage;

out lowp vec4 fragColor;

void main() 
{
	highp float exposure = 32.0;

	// Sample monochrome pixel
#ifdef MONOCHROME
	mediump float cameraMonochrome = texture(rawImage,normalisedCoordinates).r * exposure;
#endif

	// Sample and debayer
#ifdef BAYER_XGGX
	mediump vec3 cameraRgb = DebayerXGGX(rawImage, ivec2(normalisedCoordinates * textureSize(rawImage, 0))) * exposure;
#endif
#ifdef BAYER_GXXG
	mediump vec3 cameraRgb = DebayerGXXG(rawImage, ivec2(normalisedCoordinates * textureSize(rawImage, 0))) * exposure;
#endif

#ifdef BAYER_BR
	cameraRgb.xz = cameraRgb.zx;
#endif

#ifdef MONOCHROME
	lowp vec3 rgbOut = vec3(cameraMonochrome, cameraMonochrome, cameraMonochrome);
#else
	// Transform camera to display colour space
	mediump vec3 displayRgb = cameraRgb * cameraToDisplayColour;
	lowp vec3 rgbOut = displayRgb;
#endif

	fragColor = vec4(rgbOut,1);
}
#endif