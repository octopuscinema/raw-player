// GLSL Vertex shader program
#ifdef VERT

// Interpolated fragment/vertex values
out highp vec2 normalisedCoordinates;

layout(location = 0) in highp vec2 VertexPosition;

uniform vec4 RectUV;
uniform highp vec4 RectBounds;
uniform highp vec2 OrthographicBoundsInverse;

void main(void)
{
	// Calculate Texture Coordinates from vertex position
	highp vec2 UV0 = RectUV.xw;
	highp vec2 UV1 = RectUV.zy;
	normalisedCoordinates = mix(UV0, UV1, VertexPosition);
	
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

#include "Gamma.glsl.h"
#include "ToneMap.glsl.h"

#ifndef MONOCHROME
#include "DebayerDraft.glsl.h"
#include "HighlightRecovery.glsl.h"
#include "RollOff.glsl.h"
uniform mediump mat3 cameraToDisplayColour;
uniform mediump vec3 cameraWhite;
uniform mediump vec3 cameraWhiteNormalised;
uniform mediump vec3 rawLuminanceWeight;
uniform eHighlightRecovery highlightRecovery;
uniform eRollOff highlightRollOff;
uniform eGamutCompression gamutCompression;
#endif

uniform eToneMappingOperator toneMappingOperator;
uniform highp vec2 blackWhiteLevel;
uniform sampler2D rawImage;
uniform mediump float exposure;

#ifdef LINEARIZE
uniform highp float linearizeTableRange;
uniform sampler1D linearizeTable;
#endif

out lowp vec4 fragColor;

void main() 
{
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

	// Linearise
#ifdef LINEARIZE
	mediump vec3 linearizeTableIndex = min(vec3(1.0), cameraRgb / linearizeTableRange);
	cameraRgb.x = texture(linearizeTable, linearizeTableIndex.x).r;
	cameraRgb.y = texture(linearizeTable, linearizeTableIndex.y).r;
	cameraRgb.z = texture(linearizeTable, linearizeTableIndex.z).r;
#endif

	// Apply black and white level
	cameraRgb -= vec3(blackWhiteLevel.x);
	cameraRgb /= (blackWhiteLevel.y-blackWhiteLevel.x);

#ifdef MONOCHROME
	mediump vec3 displayRgb = cameraRgb;
	
	// Apply tone mapping
	if ( toneMappingOperator != ToneMappingOperatorNone)
		displayRgb = ToneMap(displayRgb, toneMappingOperator);

	// Apply exposure
	displayRgb *= exposure;

#else
	// Highlight recovery
	if ( highlightRecovery == HighlightRecoveryOn )
		cameraRgb = HighlightRecovery(cameraRgb, cameraWhite);
	else
		cameraRgb = HighlightCorrect(cameraRgb, cameraWhiteNormalised);

	// Apply exposure pre-rolloff
	if ( exposure > 1.0 )
		cameraRgb *= exposure;

	// Perform highlight/shadow rolloff
	if ( highlightRollOff != RollOffNone )
	{
		mediump float rawLuminance = LuminanceWeight(cameraRgb, rawLuminanceWeight);
		cameraRgb = HighlightRolloff(cameraRgb, cameraWhiteNormalised, rawLuminance, rawLuminanceWeight, highlightRollOff);
	}
	//rawLuminance  = LuminanceWeight(cameraRgb, rawLuminanceWeight);
	//cameraRgb = ShadowRolloff(cameraRgb, cameraWhiteNormalised, rawLuminance, rawLuminanceWeight, RollOffLow);

	// Apply exposure post-rolloff
	if ( exposure < 1.0 )
		cameraRgb *= exposure;

	// Transform camera to display colour space
	mediump vec3 displayRgb = cameraRgb * cameraToDisplayColour;

	// Apply tone mapping operator
	if ( toneMappingOperator != ToneMappingOperatorNone)
		displayRgb = ToneMap(displayRgb, toneMappingOperator);
	
	// Apply gamut compression
	if ( gamutCompression == GamutCompressionRec709 )
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