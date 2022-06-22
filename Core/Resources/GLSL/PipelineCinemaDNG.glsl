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
#include "Gamma.glsl.h"
#include "ToneMap.glsl.h"

uniform highp vec2 blackWhiteLevel;
uniform sampler2D rawImage;

out lowp vec4 fragColor;

void main() 
{
	const mediump float exposure = 1.4142;

	// Sample monochrome pixel
#ifdef MONOCHROME
	mediump float cameraMonochrome = texture(rawImage,normalisedCoordinates).r;
	mediump vec3 cameraRgb = vec3(cameraMonochrome, cameraMonochrome, cameraMonochrome);
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

	// Apply black and white level
	cameraRgb -= vec3(blackWhiteLevel.x);
	cameraRgb /= (blackWhiteLevel.y-blackWhiteLevel.x);

	// Linearise
	

#ifdef MONOCHROME
	// Apply tone mapping
	mediump vec3 displayRgb = ToneMap(cameraRgb, ToneMappingOperatorSDR);

	// Apply exposure
	displayRgb *= exposure;

#else
	// Highlight recovery


	// Perform highlight/shadow rolloff

	// Transform camera to display colour space
	mediump vec3 displayRgb = cameraRgb * cameraToDisplayColour;

	// Apply exposure
	displayRgb *= exposure;

	// Apply tone mapping operator
	displayRgb = ToneMap(displayRgb, ToneMappingOperatorSDR);
	
	// Apply gamut compression
	displayRgb = Gamut709Compression(displayRgb);

#endif

	// Apply gamma
#ifdef GAMMA_REC709
	lowp vec3 gammaRgb = ApplyGamma709(displayRgb);
#endif
#ifdef GAMMA_SRGB
	lowp vec3 gammaRgb = ApplyGammaSRGB(displayRgb);
#endif

	fragColor = vec4(gammaRgb, 1.0);
}
#endif