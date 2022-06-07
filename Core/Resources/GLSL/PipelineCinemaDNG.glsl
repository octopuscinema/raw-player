// Interpolated fragment/vertex values
varying vec2 TextureCoordinates;

// GLSL Vertex shader program
#ifdef VERT

attribute vec2 VertexPosition;

//uniform vec4 RectUV;
uniform vec4 RectBounds;
uniform vec2 OrthographicBoundsInverse;

void main(void)
{
	// Calculate Texture Coordinates from vertex position
	vec2 UV0 = vec2(0.0,0.0);//RectUV.xy;
	vec2 UV1 = vec2(1.0,1.0);//RectUV.zw;
	TextureCoordinates = UV0 + VertexPosition*(UV1-UV0);
	
	// Calculate position
	vec2 Translate = RectBounds.xy;
	vec2 Scale = RectBounds.zw;
	vec2 Position = Translate + VertexPosition*Scale;
	
	// Apply orthographic matrix (stored as bounds instead for speed)
	Position = ((Position * OrthographicBoundsInverse)*2.0) - sign(OrthographicBoundsInverse);
	gl_Position = vec4(Position, 0.0, 1.0);
}
#endif

// GLSL Fragment/pixel shader program
#ifdef FRAG
void main() 
{
	gl_FragColor = vec4(0,TextureCoordinates.x,0,1);
}
#endif