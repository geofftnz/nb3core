#version 450
precision highp float;
layout (location = 0) in vec2 texcoord;
layout (location = 0) out vec4 out_Col;

uniform float time;
uniform float aspectRatio;
uniform sampler2D input0Tex;

mat2 rot(float a)
{
	float c = cos(a);
	float s = sin(a);
	return mat2(c,-s,s,c);
}

void main(void)
{
	vec2 U = texcoord * 2.0 - vec2(1.0);

	U.x *= aspectRatio;
	U *= 0.6;

	out_Col = vec4(0.);
	int s = 6;
	mat2 r = rot(3.1415927 * 2. / float(s));

	U *= rot(time * -0.05);
	U = length(U) * cos( abs( mod( atan(U.y,U.x), 1.05) - .525 ) - .525  + vec2(0,11) );
	U *= rot(time * 0.1);

	for (int i=0;i<s;i++)
	{
		out_Col += texture2D(input0Tex,mod(U*0.5+0.5,1.0)) * 0.16;
		U = U * r;
	}


	out_Col.a = 1.;	
}

