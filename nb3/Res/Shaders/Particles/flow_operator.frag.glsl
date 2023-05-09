#version 450
precision highp float;
layout (location = 0) in vec2 texcoord;
layout (location = 0) out vec4 out_Pos;
layout (location = 1) out vec4 out_Col;

uniform float time;
uniform float deltaTime;
uniform sampler2D spectrumTex;
uniform sampler2D audioDataTex;
uniform sampler2D particlePosTex;
uniform sampler2D particlePosPrevTex;
uniform sampler2D particleColTex;
uniform float currentPosition;
uniform float currentPositionEst;

#include "Common/filterParametersRuntime.glsl"
#include "Common/noise4d.glsl"


// math ------------------------------------------------------------------------------
#define PI 3.14159265
#define PIOVER2 1.570796
#define LOGe10 2.302585

vec3 log10(vec3 s)
{
	return log(s.rgb) / LOGe10;
}

vec4 todB(vec4 s)
{
	// ignore 4th component (stereo angle)
	s.rgb = 20.0*log10(s.rgb);
	s.rgb = max(vec3(0.0),vec3(1.0) + (s.rgb / vec3(100.0)));
	return s;
}
float todB(float s)
{
	s = 20.0*log(s);
	s = max(0.0,1.0 + ((s+20.0) / 200.0));
	return s;
}

float smoothpulse(float a, float low1, float high1, float high2, float low2)
{
	return smoothstep(low1,high1,a) * (1.0 - smoothstep(high2,low2,a));
}

// spectrum & audio data -------------------------------------------------------------
#define DATARES 256
float getAudioDataSample(sampler2D tex, float index, float time)
{
	return texture2D(tex,vec2((index+0.5)/DATARES,time)).r;
}
vec4 scaleSpectrum(vec4 s)
{
	return todB(s);
}

float fscale(float x)
{
	return x * 0.1 + 0.9 * x * x;
}

float fscale_inv(float y)
{
	return (sqrt(360.*y+1.)-1)/18.;
}

vec2 texel = vec2(1.0/1024.0,0.0);

vec4 getSample(sampler2D spectrum, vec2 t)
{
	vec2 raw = texture2D(spectrum,t).rg;

	// TODO: potentially don't need this
	float sep = asin((raw.g - raw.r) / (raw.g + raw.r)) / PIOVER2;

	vec4 s = vec4(raw.rg,(raw.r+raw.g)*0.5, sep);

	return s;
}

vec3 getFreqColour(float f)
{
	// bass purple - blue
	if (f < 0.02) return mix(vec3(0.2,0.0,1.0),vec3(0.0,0.0,1.0),f / 0.02);

	// red
	if (f < 0.05) return mix(vec3(0.0,0.0,1.0),vec3(1.0,0.0,0.0),(f-0.02) / 0.03);

	// yellow
	if (f < 0.07) return mix(vec3(1.0,0.0,0.0),vec3(1.0,1.0,0.0),(f-0.05) / 0.02);

	// green
	if (f < 0.1) return mix(vec3(1.0,1.0,0.0),vec3(0.0,1.0,0.0),(f-0.07) / 0.03);

	// skyblue
	if (f < 0.2) return mix(vec3(0.0,1.0,0.0),vec3(0.0,0.5,1.0),(f-0.1) / 0.1);

	// remainder
	return mix(vec3(0.0,0.5,1.0),vec3(1.0,1.0,1.0),(f-0.2) / 0.8);
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


vec3 randomPos01(vec2 particle, float nonce)
{	
	return vec3(
		hash13(vec3(particle,hash(nonce))),
		hash13(vec3(particle*133.7,hash(nonce))),
		hash13(vec3(particle*1393.7,hash(nonce)))
	) ;
}
vec3 randomPos(vec2 particle, float nonce)
{
	return randomPos01(particle,nonce)	* vec3(2.0) - vec3(1.0);
}

// random points on sphere surface, centered at origin, radius r
vec3 sphere(vec2 particle, float r)
{
	vec3 p = randomPos(particle,1.0); // floor(time * 1.0)

	if (dot(p,p)>1.0) p = randomPos(particle,1.0);
	if (dot(p,p)>1.0) p = randomPos(particle,2.0);
	//if (dot(p,p)>1.0) p = randomPos(particle,3.0);
	//if (dot(p,p)>1.0) p = randomPos(particle,4.0);

	p = normalize(p) * r;
	return p;	
}

vec3 hsv2rgb(vec3 c)
{
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

struct PosCol
{
	vec4 pos;
	vec4 col;
};


PosCol flow1(vec2 coord)
{
	vec3 p = vec3(0.0);
	//float s = 0.5 + hash13(vec3(coord,0.)) * 3.;
	float s = 1.5;
	vec4 col = vec4(1.0,0.1,0.05,0.1);

	// get last position & col
	vec3 p0 = texture(particlePosTex,coord).rgb;
	vec4 col0 = texture(particleColTex,coord);
	
	// get last-1 position
	vec3 pminus1 = texture(particlePosPrevTex,coord).rgb;
	
	vec3 v = p0 - pminus1;
	if (dot(v,v)>1.) v = vec3(0.);

	float beat_counter1 = getAudioDataSample(audioDataTex,A_BEAT1_acc1,currentPositionEst);
	float beat_counter4 = getAudioDataSample(audioDataTex,A_BEAT1_acc4,currentPositionEst);
	float beat_counter_bd = getAudioDataSample(audioDataTex,A_BD_counter,currentPositionEst);
	float complexity = getAudioDataSample(audioDataTex,A_DF_LP3,currentPositionEst);


	// base colour, will cycle
	vec3 base_col = hsv2rgb(vec3(mod(beat_counter4 * 8.,1.),0.8,0.8));
	
	// determine if we need to init position+velocity
	if ((dot(p0,p0) == 0. && dot(pminus1,pminus1) == 0.) || (hash13(p0*17. + vec3(time)) > 0.995))
	{
		// random position
		p0 = randomPos(coord,time);
		if (dot(p0,p0) > 1.) p0 = randomPos(coord,time+1.);
		if (dot(p0,p0) > 1.) p0 = randomPos(coord,time+2.);

		v = vec3(0.);

		col0.rgb = base_col + normalize(p0.xyz) * 0.1;
		col0.a = 0.0;
	}
	else
	{
		// fade in alpha, clamp to max
		col0.a = min(0.1,col0.a + 0.0002);
	}

	/*
	float sn = getAudioDataSample(audioDataTex,A_SN_level,currentPositionEst);
	float hh = getAudioDataSample(audioDataTex,A_HH1_level,currentPositionEst);
	float bd = getAudioDataSample(audioDataTex,A_BD_level,currentPositionEst);
	float complexity = getAudioDataSample(audioDataTex,A_DF_LP3,currentPositionEst);
	*/



	// decay motion
	v *= 0.5;
	//p0 *= 0.99;

	// add noise1
	//vec3 pv = p0 * 3.7 * (complexity + .3);
	//float a = 0.01 + complexity * 0.0001 + hash13(vec3(coord,0.)) * 0.00001;
	//a*= 0.5;

	vec3 pv = p0 * 1.1;
	float a = 0.01;

	float octaves = min(5.0, 0.5 + complexity * 6.);
	float last_octave_scale = fract(octaves);
	octaves = floor(octaves);

	float t = time * 0.01;

	// whole octaves of noise
	float octave = 1.;
	for (; octave <= octaves; octave += 1.)
	{
		v.x += (snoise(vec4(pv,0.+t)))*a;
		v.y += (snoise(vec4(pv,1.+t)))*a;
		v.z += (snoise(vec4(pv,2.+t)))*a;
		pv *= 2.0;
		a *= 0.5;
		t *= 1.05;
	}

	// final octave of noise
	a *= last_octave_scale;
	v.x += (snoise(vec4(pv,0.+t)))*a;
	v.y += (snoise(vec4(pv,1.+t)))*a;
	v.z += (snoise(vec4(pv,2.+t)))*a;


	//float distToSphere = length(p0) - 0.4;
	//v += -normalize(p0) * distToSphere * (0.5);


	// move
	p = p0 + v * (1.+.1*hash13(vec3(coord,1.)));
	col = col0;

	//col.rgb = normalize(p.xyz) * 0.9 + vec3(0.1);
	//col.rgb = normalize(v) * 0.5 + 0.5;

	return PosCol(vec4(p,s),col);
}


void main(void)
{
	PosCol a;

	a = flow1(texcoord);

	out_Pos = a.pos;
	out_Col = a.col;
}




