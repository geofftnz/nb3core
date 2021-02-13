#version 450
precision highp float;
layout (location = 0) in vec2 vertex;
layout (location = 0) out vec3 pos;
layout (location = 1) out float size;
layout (location = 2) out vec4 col;

uniform float screenFactor;
uniform mat4 projectionMatrix;
uniform mat4 modelMatrix;
uniform mat4 viewMatrix;
uniform sampler2D particlePositionTexture;
uniform sampler2D particleColourTexture;

void main() 
{
	// https://stackoverflow.com/questions/8608844/resizing-point-sprites-based-on-distance-from-the-camera
	//float s = 2.0 * (sqrt(screenWidth) / 28.0);

	vec4 ptex = texture2D(particlePositionTexture,vertex.xy);
	col = texture2D(particleColourTexture,vertex.xy);

	vec4 p = vec4(ptex.xyz,1.0);
	float s = ptex.a * screenFactor;

	vec4 eyePos = viewMatrix * modelMatrix * p;
	vec4 corner = projectionMatrix * vec4(s,s,eyePos.z,eyePos.w);
	size = corner.x / corner.w;
	gl_Position = projectionMatrix * eyePos;
	gl_PointSize = max(1.0,size);
	pos = p.xyz;
}
