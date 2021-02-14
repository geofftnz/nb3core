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

#include "Common/filterParameters.glsl"

// math ------------------------------------------------------------------------------
#define PI 3.1415926535897932384626433832795
#define PIOVER2 1.5707963267948966192313216916398
#define LOGe10 2.30258509299

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

vec3 colscale(float s)
{
	vec3 col  = vec3(0.0,0.0,0.0   );

	vec3 col0 = vec3(0.0,0.0,0.05   );
	vec3 col1 = vec3(0.0,0.1,0.9 );
	vec3 col2 = vec3(0.0,0.6,0.6   );
	vec3 col3 = vec3(0.9,0.9,0.0  );
	vec3 col4 = vec3(0.9,0.0,0.0  );
	vec3 col5 = vec3(1.0,1.0,1.0);
	
	col = mix(col0,col1,clamp(s*2.5,0.0,1.0));
	col = mix(col,col2,clamp((s-0.3)*2.0,0.0,1.0));
	col = mix(col,col3,clamp((s-0.5)*4.0,0.0,1.0));
	col = mix(col,col4,clamp((s-0.6)*5.0,0.0,1.0));
	col = mix(col,col5,clamp((s-0.8)*6.0,0.0,1.0));

	return col;
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

struct PosCol
{
	vec4 pos;
	vec4 col;
};

PosCol blob1(vec2 coord)
{
	vec3 p;
	float s;

	float group = floor(coord.y * 16.0);
	float group2 = floor(coord.x * 2.0);

	p = sphere(coord,1.0); 

	float freq = fscale(coord.x);
	vec4 samp = scaleSpectrum(getSample(spectrumTex,vec2(freq,currentPositionEst - coord.y * 0.01)));

	float intensity = (samp.b * samp.b) * 2.0;

	p.x -= (samp.a) * 0.7; //pan

	p *= 0.1 + (1.0-sqrt(freq)) * 0.9 + coord.y * 0.02;

	p *= 0.5;

	s = 0.1 + intensity*5.0;

	vec4 col = vec4(getFreqColour(freq),intensity*0.1);

	return PosCol(vec4(p,s),col);
}

PosCol plane1(vec2 coord)
{
	vec3 p;
	float s = 1.5;
	vec4 col = vec4(0.4,0.4,0.4,0.5);

	//coord.y += fract(currentPositionEst*1024.0);

	float fcoord = abs(coord.x-0.5)*2.0+0.01;
	float freq = fscale(fcoord);
	vec4 samp = scaleSpectrum(getSample(spectrumTex,vec2(freq,currentPositionEst - coord.y * 0.2)));

	p.xz = (coord - vec2(0.5)) * 2.0;
	p.z += 0.13;  // front-back shift
	p.y -= 0.17;   // eye height

	p.y += ((coord.x<0)?samp.r:samp.g) * 0.2;

	col.rgb = colscale((coord.x<0)?samp.r:samp.g);
	col.a = 1.0 / (1.0 + (p.z+1.0)*4.0);

	// mix in various effects

	float pulsePos = currentPositionEst - coord.y * 0.02;
	// bass pulse
	col.rgb += vec3(1.0) * (1.0-smoothstep(0.1,0.2,fcoord)) * pow(getAudioDataSample(audioDataTex,A_BD_edge,pulsePos),4.0 );

	// snare
	col.rgb += vec3(1.0) * (smoothpulse(fcoord,0.1,0.2,0.4,0.5)) * pow(getAudioDataSample(audioDataTex,A_SN_level,pulsePos),4.0 );

	// hihats
	col.rgb += vec3(1.0) * (smoothpulse(fcoord,0.4,0.5,0.9,1.0)) * pow(getAudioDataSample(audioDataTex,A_HH1_edge,pulsePos),4.0 );


	return PosCol(vec4(p,s),col);
}


void main(void)
{
	PosCol b = blob1(texcoord);
	PosCol a;

	a = plane1(texcoord);

	float m = sin((texcoord.y+sin(time * 0.7)) * (texcoord.x * 3.3 + sin(time * 1.3))) * 0.5 + 0.5;
	a.pos = mix(a.pos,b.pos,m);
	a.col = mix(a.col,b.col,m);

	//if (texcoord.y<0.5){
	//	a = plane1(texcoord * vec2(1.0,2.0));
	//}
	//else{
	//	//a = blob1((texcoord - vec2(0.0,0.5)) * vec2(1.0,2.0));
	//}

	out_Pos = a.pos;
	out_Col = a.col;
}




