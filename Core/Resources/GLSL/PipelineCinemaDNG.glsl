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

uniform highp vec2 blackWhiteLevel;
uniform sampler2D rawImage;

out lowp vec4 fragColor;

void main() 
{
	highp float exposure = 4.0;

	// Sample monochrome pixel
#ifdef MONOCHROME
	mediump float cameraMonochrome = texture(rawImage,normalisedCoordinates).r;
#endif

	// Sample and debayer
#ifdef BAYER_XGGX
	mediump vec3 cameraRgb = DebayerXGGX(rawImage, ivec2(normalisedCoordinates * textureSize(rawImage, 0)));
#endif
#ifdef BAYER_GXXG
	mediump vec3 cameraRgb = DebayerGXXG(rawImage, ivec2(normalisedCoordinates * textureSize(rawImage, 0)));
#endif

#ifdef BAYER_BR
	cameraRgb.xz = cameraRgb.zx;
#endif

#ifdef MONOCHROME

	// Apply black and white level
	cameraMonochrome -= blackWhiteLevel.x;
	cameraMonochrome /= (blackWhiteLevel.y-blackWhiteLevel.x);

	// Apply exposure
	cameraRgb *= exposure;

	lowp vec3 rgbOut = vec3(cameraMonochrome, cameraMonochrome, cameraMonochrome);
#else

	// Apply black and white level
	cameraRgb -= vec3(blackWhiteLevel.x);
	cameraRgb /= (blackWhiteLevel.y-blackWhiteLevel.x);

	// Linearise

	// Highlight recovery

	// Perform highlight/shadow rollof

	// Transform camera to display colour space
	mediump vec3 displayRgb = cameraRgb * cameraToDisplayColour;
	lowp vec3 rgbOut = displayRgb;

	// Apply tone mapping operator
	//Sample = ToneMap(Sample, ToneMappingOperator);
	
	// Apply gamut compression
	//if ( GamutCompression == 1.0 )
	//	Sample = Gamut709Compression(Sample);

	// Apply gamma

#endif

	fragColor = vec4(rgbOut,1);
}
#endif