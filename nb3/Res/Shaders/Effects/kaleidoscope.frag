#version 450
precision highp float;
layout (location = 0) in vec2 texcoord;
layout (location = 0) out vec4 out_Col;

uniform float time;
uniform float aspectRatio;
uniform sampler2D input0Tex;
uniform sampler2D spectrumTex;
uniform sampler2D audioDataTex;
uniform float currentPosition;
uniform float currentPositionEst;

#include "Common/filterParametersRuntime.glsl"


#define DATARES 256
float getAudioDataSample(float index, float time)
{
	return texture2D(audioDataTex,vec2((index+0.5)/DATARES,time)).r;
}


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

	float t = time * 0.;

	// time shift based on beat (bass drum)
	t += mod(getAudioDataSample(A_BD_counter,currentPositionEst) * 64.,1.0)*8.;

	
	U *= rot(t * -0.05);
	U = length(U) * cos( abs( mod( atan(U.y,U.x), 1.05) - .525 ) - .525  + vec2(0,11) );
	U *= rot(t * 0.1);

	for (int i=0;i<s;i++)
	{
		out_Col += texture2D(input0Tex,mod(U*0.5+0.5,1.0)) * 0.16;
		U = U * r;
	}

	/*
	// additional repitition
	float a = 0.1;
	//r = rot(3.1415927 * 2. / float(s * 4.));
	for (int i=0;i<s*2;i++)
	{
		out_Col += texture2D(input0Tex,mod(U*0.5+0.5,1.0)) * a;
		U = U * r;
		U *= (sin(time * 0.4) * 0.2 + 1.1);
		a *= 0.8;
	}*/
	
	//out_Col *= 0.0;

	// mix in original
	//out_Col = texture2D(input0Tex,texcoord);
	//	out_Col += texture2D(input0Tex,texcoord)*0.5;


	out_Col.a = 1.;	
}

