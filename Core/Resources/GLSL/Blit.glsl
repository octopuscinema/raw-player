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

uniform sampler2D image;

out lowp vec4 fragColor;

void main() 
{
	fragColor = vec4(texture(image, normalisedCoordinates).rg, 1.0, 1.0);
}
#endif