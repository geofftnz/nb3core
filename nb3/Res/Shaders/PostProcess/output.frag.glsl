#version 450
precision highp float;
layout (location = 0) in vec2 texcoord;
layout (location = 0) out vec4 out_Col;

uniform float time;
uniform sampler2D spectrumTex;
uniform sampler2D audioDataTex;
uniform sampler2D particlePosTex;
uniform sampler2D particlePosPrevTex;
uniform sampler2D particleColTex;
uniform sampler2D inputTex;
uniform float currentPosition;
uniform float currentPositionEst;
#include "Common/filterParameters.glsl"
#include "Common/noise3d.glsl"



const float A = 0.15;
const float B = 0.50;
const float C = 0.10;
const float D = 0.20;
const float E = 0.02;
const float F = 0.30;
const float W = 11.2;

vec3 Uncharted2Tonemap(vec3 x)
{
   return ((x*(A*x+C*B)+D*E)/(x*(A*x+B)+D*F))-E/F;
}

vec2 rot2D(vec2 p, float a)
{
	vec2 sc = vec2(cos(a),sin(a));
	return p * mat2(sc.x,-sc.y,sc.y,sc.x);
}
vec2 rot2D(vec2 p, vec2 c, float a)
{
	p-=c;
	vec2 sc = vec2(cos(a),sin(a));
	return p * mat2(sc.x,-sc.y,sc.y,sc.x) + c;
}

//From Dave (https://www.shadertoy.com/view/XlfGWN)
float hash13(vec3 p){
	p  = fract(p * vec3(.16532,.17369,.15787));
    p += dot(p.xyz, p.yzx + 19.19);
    return fract(p.x * p.y * p.z);
}
float hash(float x){ return fract(cos(x*124.123)*412.0); }
float hash(vec2 x){ return fract(cos(dot(x.xy,vec2(2.31,53.21))*124.123)*412.0); }
float hash(vec3 x){ return fract(cos(dot(x.xyz,vec3(2.31,53.21,17.7))*124.123)*412.0); }

// spectrum & audio data -------------------------------------------------------------
#define DATARES 256
float getAudioDataSample(sampler2D tex, float index, float time)
{
	return texture2D(tex,vec2((index+0.5)/DATARES,time)).r;
}


void main(void)
{

	//vec2 tc = floor(texcoord * 160.)/160.;  // pixelate
	vec2 tc = texcoord;

	vec3 col = texture2D(inputTex,tc).rgb;

	
	// some repetition
	//float twist = getAudioDataSample(audioDataTex,A_DF_LP3,currentPositionEst);
	/*
	vec2 p = (texcoord - vec2(.5));
	float twist;
	float m = .2;
	for (int i=0;i<8;i++)
	{
		m *= 0.5;
		p *= (0.95 + hash(vec3(texcoord*7.,time+m)) * 0.02);
		//p = mod(p + vec2(.5),1.) - vec2(.5);
		//p = rot2D(p,3.1415927/3.);
	
		col += texture2D(inputTex,p + vec2(.5)).rgb * m;
	}
	p=texcoord-vec2(.5); m=.2;
	*/
	//twist = texcoord.x * 3. + sin(texcoord.y * 7.) *2. + time * 0.1;
	//twist = snoise(vec3(texcoord*.7,time * .2));
	//for (int i=0;i<16;i++)
	//{
	//	m *= 0.8;
	//	p *= (1.5 + twist);
	//	p += (twist * .7);
	//	//p.x *= sin(p.y * 3.);
	//	//p = mod(p + vec2(.5),1.) - vec2(.5);
	//	p = rot2D(p,twist);
	//	p = mod(p + vec2(0.5),vec2(1.))-vec2(.5);
	//
	//	col += texture2D(inputTex,p + vec2(.5)).rgb * m;
	//}
	

	// lensflare? (no)
	//vec2 tc = texcoord - vec2(.5);
	//vec2 tc2 = (-tc) * 0.1 + vec2(.5);
	//vec3 col2 = texture2D(inputTex,tc2).rgb;
	//col += col2 * 0.05;

	// horizontal bars
	//vec2 tc2 = vec2(.5,texcoord.y);
	//vec3 col2 = texture2D(inputTex,tc2).rgb;
	//col += col2 * 0.05 * (1. - abs(texcoord.x - tc2.x) * 2.);


   //float bias = 8.0;
   //col = Uncharted2Tonemap(col * bias);
   
   //vec3 whiteScale = vec3(1.0)/Uncharted2Tonemap(vec3(W));
   //col = col * whiteScale;

	// gamma
	col.rgb = pow(col,vec3(1./2.0));

	//col.rgb = col.rgb / (vec3(1.)+col.rgb);
	// todo: tonemap

	out_Col = vec4(col,1.0);
}
