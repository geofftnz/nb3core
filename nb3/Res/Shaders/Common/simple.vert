#version 450
precision highp float;
layout (location = 0) in vec2 vertex;
layout (location = 0) out vec2 texcoord;

void main() 
{
	gl_Position = vec4(vertex.xy,0.0,1.0);
	texcoord = vertex.xy * 0.5 + 0.5;
}
