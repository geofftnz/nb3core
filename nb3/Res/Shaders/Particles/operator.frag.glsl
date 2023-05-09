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
	//coord.y = 1.0-coord.y;

	float freq = fscale(fcoord);
	vec4 samp = scaleSpectrum(getSample(spectrumTex,vec2(freq,currentPositionEst - (0.2 - coord.y * 0.2))));

	p.xz = (coord - vec2(0.5)) * 2.0;
	p.z += 0.13;  // front-back shift
	p.y -= 0.27;   // eye height

	p.y += ((coord.x<0)?samp.r:samp.g) * 0.2;

	col.rgb = colscale((coord.x<0)?samp.r:samp.g);
	col.a = 1.0 / (1.0 + (p.z+1.0)*4.0);

	// mix in various effects

	float pulsePos = currentPositionEst - (0.01 - coord.y * 0.01);
	// bass pulse
	col.rgb += vec3(1.0,0.2,0.1) * (1.0-smoothstep(0.1,0.2,fcoord)) * pow(getAudioDataSample(audioDataTex,A_BD_level,pulsePos),4.0 );

	// snare
	col.rgb += vec3(0.4,1.0,0.1) * 0.5 * (smoothpulse(fcoord,0.1,0.2,0.4,0.5)) * pow(getAudioDataSample(audioDataTex,A_SN_level,pulsePos),4.0 );

	// hihats
	col.rgb += vec3(0.4,0.8,1.0) * 0.5 * (smoothpulse(fcoord,0.4,0.5,0.9,1.0)) * pow(getAudioDataSample(audioDataTex,A_HH1_edge,pulsePos),4.0 );


	return PosCol(vec4(p,s),col);
}

PosCol rose1(vec2 coord)
{
	vec3 p;
	float s = 5.0;
	vec4 col = vec4(0.4,0.4,0.4,0.15);

	//float pulsePos = currentPositionEst - (coord.y * (0.5 + sin(coord.x * 17.0 + time) * 0.05));  // speed of animation
	float pulsePos = currentPositionEst - (coord.y * 0.6);  // speed of animation
	//float pulsePos = currentPositionEst - (coord.y * (0.3 + hash(coord.x * 17.0) * 0.2));  // speed of animation

	float filterIndex = floor(mod(coord.x * 1024.0,5.0));  // index into the peak-frequency outputs.

	float symmetry = 3.0;

	//symmetry *= (1.0 + smoothstep(0.8,0.9,getAudioDataSample(audioDataTex,A_KD3_edge,currentPositionEst))) * 3.0;

	float band = mod(floor(coord.x * 1024.0),2.0)*2.0-1.0;
	band *= (3.0 + coord.y * 3.0) / symmetry;


	float freq = getAudioDataSample(audioDataTex,A_MPFF_Freq1 + filterIndex*2,pulsePos);
	float level = getAudioDataSample(audioDataTex,A_MPFF_Freq1 + filterIndex*2+1,pulsePos);

	//float da = max(0.0,getAudioDataSample(audioDataTex,6. + markerIndex * 2.,time)-0.05);
	//(1.0 - smoothstep(abs(freq - df),0.0,0.0005)) * da*da*100.0;	

	//float spectrumLevel = scaleSpectrum(getSample(spectrumTex,vec2(freq,pulsePos))).b;
	float da = max(0.0,level-0.05);
	float spectrumLevel = da*da*100.0;

	float twist = getAudioDataSample(audioDataTex,A_DF_LP3,currentPositionEst);

	float angle = (freq * (band) + floor(coord.x * symmetry) / symmetry ) * PI * 2.0;  // + coord.y * level

	//angle += sin(coord.y * 7.0) * twist * 0.1;

	//angle += sin((coord.y + cos(time * 0.7))*20.0) * 0.1 + cos(coord.y * 73.0 + sin(time)) * 0.05;
	//angle += hash13(vec3(coord,time*17.9)) * coord.y * 0.02;  // angle noise towards the edge

	//float r = 0.5 + sin(coord.x * 76.0 + time*0.3) * 0.45; // * (0.5 + twist * 0.5);  // radius of tunnel
	float r = 0.05 + hash(coord.x) * 0.5;  // random radius
	vec3 basis = vec3(r*cos(angle),r*0.6*sin(angle),-4.0);

	p = vec3(0.0,0.0,3.0) + basis * coord.y;
	
	//p.y *= (step(0.5,coord.x*coord.y)*2.0) - 1.0;

	float bassdrum = smoothstep(0.8,0.95,getAudioDataSample(audioDataTex,A_KD3_edge,currentPositionEst));
	//float hihat = smoothstep(0.8,0.95,getAudioDataSample(audioDataTex,A_HH2_edge,currentPositionEst));

	//float sizescale = smoothstep(0.8,0.95,bassdrum);


	col.rgb = mix(getFreqColour(freq),vec3(0.1,0.1,0.1),0.8);
	s = 0.5 + spectrumLevel * mix(4.0,12.0,bassdrum);

	return PosCol(vec4(p,s),col);
}

mat2 rot(float a){return mat2(cos(a),sin(a),-sin(a),cos(a));}

PosCol blob2(vec2 coord)
{
	vec3 p = vec3(0.0);
	float s = 4.0;
	vec4 col = vec4(0.2,0.8,1.0,0.02);

	// get last position
	vec3 p0 = texture(particlePosTex,coord).rgb;

	// get last-1 position
	vec3 pminus1 = texture(particlePosPrevTex,coord).rgb;

	vec3 v = vec3(0.);
	
	// determine if we need to init position+velocity
	if (dot(p0,p0) == 0. && dot(pminus1,pminus1) == 0.)
	{
		// random position
		p0 = randomPos(coord,0.);
	}
	else
	{
		// get velocity
		v = (p0 - pminus1);
	}

	float hh = getAudioDataSample(audioDataTex,A_HH1_level,currentPositionEst);
	float bd = getAudioDataSample(audioDataTex,A_BD_level,currentPositionEst);

	// decay motion
	v *= 0.25;

	// brownian motion
	v += randomPos(coord + p0.xy,time) * 0.001;
	//v += randomPos(coord + p0.xy,time) * 0.1;  // reset

	// attract to a certain distance from origin
	float sphereRadius = 0.15 + getAudioDataSample(audioDataTex,A_DF_LP3,currentPositionEst) * 0.25;

	vec3 p0r = p0;
	float a2 = p0.x * 0.7;
	a2 += p0.y * 0.8;
	p0r.yz *= rot(time + a2);
	p0r.xy *= rot(time*1.13 + a2);
	p0r.zx *= rot(time*1.29 + a2);
	float freq = fscale(abs(p0r.y)+0.02);
	vec4 samp = scaleSpectrum(getSample(spectrumTex,vec2(freq,currentPositionEst)));
	sphereRadius += samp.b * 0.3;
	float distToSphere = length(p0) - sphereRadius;
	v += -normalize(p0) * distToSphere * 0.2;

	s *= sphereRadius * 3.;


	// orbiting attractors
	//vec3 attr = vec3(cos(time),0.,-sin(time)).xzy * (sphereRadius + 0.2);
	//v += (normalize(attr - p0) * 0.0001) / (1. + pow(length(attr-p0),4.0));


	// peak trackers (x5)

	if (coord.y < 0.5)
	{
		// choose a peak tracker for this particle
		float filterIndex = floor(mod(coord.x * 1024.0,5.0));  // index into the peak-frequency outputs.

		p0r = p0;
		p0r.yz *= rot(time*0.96);
		p0r.xy *= rot(time*1.07);
		p0r.zx *= rot(time*0.72);

		//float timeOfs = (0.01 - coord.y * 0.2);
		float timeOfs = .0;//(abs(p0.x) * 0.0);
		float freq = getAudioDataSample(audioDataTex,A_MPFF_Freq1 + filterIndex*2,currentPositionEst-timeOfs);
		float level = getAudioDataSample(audioDataTex,A_MPFF_Freq1 + filterIndex*2+1,currentPositionEst-timeOfs);

		// extra brownian motion based on intensity
		v += randomPos(coord + p0.xy,time+0.73) * (0.01 / (max(0.0,level-0.001) * 10. + 0.1));

		// attract y-coordinate to frequency
		v.y += (freq*2. - p0.y-0.2) * level*0.25;
		//v.y=0.;	p0.y = freq*2. - 0.2;
		//col.rgb *= 0.2;
		col.rgb = getFreqColour(freq) + vec3(0.05);
		level = max(0.0,level-0.001);
		col.a = level*level*.8;
	}
	else if (coord.y < 0.8)
	{
		//col.rgb = vec3(1.0);
		col.rgb = vec3(0.4,0.6,0.9);
		col.a = pow(hh,4.0) * 0.2;

		v += p * -0.2 * pow(hh,4.0);
		s *= .4;
	}
	else
	{
		col.rgb = vec3(0.1,0.2,0.9);
		col.a = pow(bd,2.0) * 0.1;

		v += p * -0.2;
		s *= .5;
	}

	// move
	p = p0 + v;

	// colour from spectrum
	//float fcoord = abs(coord.x-0.5)*2.0+0.01;
	//float freq = fscale(coord.y);
	//vec4 samp = scaleSpectrum(getSample(spectrumTex,vec2(freq,currentPositionEst)));
	//col.rgb = getFreqColour(freq);
	//col.a = 0.01 + getAudioDataSample(audioDataTex,A_KD3_edge,currentPositionEst) * 0.02;
	//col.a = 1.0 / (1.0 + (p.z+1.0)*4.0);


	return PosCol(vec4(p,s),col);
}

float getMultiPeakFilterMarkerIntensity(float markerIndex, float time, float freq)
{
	float df = getAudioDataSample(audioDataTex,A_MPFF_Freq1 + markerIndex * 2.,time);  // freq+level pairs starting at index 5
	float da = max(0.0,getAudioDataSample(audioDataTex,6. + markerIndex * 2.,time)-0.05);

	return (1.0 - smoothstep(abs(freq - df),0.0,0.002)) * da*da*200.0;	
}
vec3 getFreqColour3(float f)
{
	return mix(vec3(0.1,0.05,1.0),vec3(0.02,1.0,0.8),f);
}

PosCol blob3(vec2 coord)
{
	vec3 p = vec3(0.0);
	float s = 4.0;
	vec4 col = vec4(0.1,0.2,1.0,0.02);

	// get last position
	vec3 p0 = texture(particlePosTex,coord).rgb;
	
	// get last-1 position
	vec3 pminus1 = texture(particlePosPrevTex,coord).rgb;
	
	vec3 v = vec3(0.);
	
	// determine if we need to init position+velocity
	if ((dot(p0,p0) == 0. && dot(pminus1,pminus1) == 0.) || (hash13(p0 + vec3(time)) < 0.01))
	{
		// random position
		p0 = randomPos(coord,0.);
		if (dot(p0,p0) > 1.) p0 = randomPos(coord,0.);
		if (dot(p0,p0) > 1.) p0 = randomPos(coord,0.);
	}
	else
	{
		// get velocity
		v = (p0 - pminus1);
	}

	float sn = getAudioDataSample(audioDataTex,A_SN_level,currentPositionEst);
	float hh = getAudioDataSample(audioDataTex,A_HH1_level,currentPositionEst);
	float bd = getAudioDataSample(audioDataTex,A_BD_level,currentPositionEst);

	float complexity = getAudioDataSample(audioDataTex,A_DF_LP3,currentPositionEst);

	// decay motion
	v *= 0.1;


	// brownian motion
	v += randomPos(coord + p0.xy,time) * bd * 0.0004;
	//v += randomPos(coord + p0.xy,time) * 0.1;  // reset
	//p0 = normalize(randomPos(coord + p0.xy,time)) * 0.2;

	// attract to a certain distance from origin
	float sphereRadius = 0.2 + complexity * 0.2;

	vec3 p0r = p0;
	float twist = 0.3 + complexity * 0.8;
	float a2 = p0.x * twist;
	a2 += p0.y * twist;

	p0r.yz *= rot(time*0.3 + a2);
	p0r.xy *= rot(time*0.5 + a2);
	p0r.zx *= rot(time*0.7 + a2);
	float freq = fscale(abs(p0r.y)+0.02);
	vec4 samp = scaleSpectrum(getSample(spectrumTex,vec2(freq,currentPositionEst)));
	sphereRadius += samp.b * 0.1;
	float distToSphere = length(p0) - sphereRadius;
	v += -normalize(p0) * distToSphere * 0.8;

	s *= sphereRadius * 1.5;


	// colour based on peak trackers
	vec3 pcol = vec3(0.0);
	pcol += getFreqColour3(freq) * getMultiPeakFilterMarkerIntensity(0,currentPositionEst,freq);
	pcol += getFreqColour3(freq) * getMultiPeakFilterMarkerIntensity(1,currentPositionEst,freq);
	pcol += getFreqColour3(freq) * getMultiPeakFilterMarkerIntensity(2,currentPositionEst,freq);
	pcol += getFreqColour3(freq) * getMultiPeakFilterMarkerIntensity(3,currentPositionEst,freq);
	pcol += getFreqColour3(freq) * getMultiPeakFilterMarkerIntensity(4,currentPositionEst,freq);
	pcol *= 0.2;

	col = vec4(vec3(0.),0.1);
	col.rgb += vec3(1.0,0.2,0.05) * bd*bd * 0.4;
	col.rgb += vec3(0.05,0.2,1.0) * hh*hh * 0.4;
	col.rgb += pcol;

	// move
	p = p0 + v;

	return PosCol(vec4(p,s),col);
}

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
	
	vec3 v = vec3(0.);
	
	// determine if we need to init position+velocity
	if ((dot(p0,p0) == 0. && dot(pminus1,pminus1) == 0.) || (hash13(p0*17. + vec3(time)) > 0.99))
	{
		// random position
		p0 = randomPos(coord,time);
		if (dot(p0,p0) > 1.) p0 = randomPos(coord,time+1.);
		if (dot(p0,p0) > 1.) p0 = randomPos(coord,time+2.);

		col0.rgb = normalize(p0.xyz) * 0.8 + vec3(0.2);
		col0.a = 0.1;
	}
	else
	{
		// get velocity
		v = (p0 - pminus1);
		if (dot(v,v)>1.) v = vec3(0.);
	}

	float sn = getAudioDataSample(audioDataTex,A_SN_level,currentPositionEst);
	float hh = getAudioDataSample(audioDataTex,A_HH1_level,currentPositionEst);
	float bd = getAudioDataSample(audioDataTex,A_BD_level,currentPositionEst);

	float complexity = getAudioDataSample(audioDataTex,A_DF_LP3,currentPositionEst);

	// decay motion
	v *= 0.8;
	//p0 *= 0.99;

	// add noise1
	vec3 pv = p0 * 3.7 * (complexity + .3);
	float a = 0.01 + complexity * 0.0001 + hash13(vec3(coord,0.)) * 0.00001;

	a*= 0.5;

	float t = time * 0.01;
	v.x += (snoise(vec4(pv,0.+t)))*a;
	v.y += (snoise(vec4(pv,1.+t)))*a;
	v.z += (snoise(vec4(pv,2.+t)))*a;


	pv *= 2.; a*= .5; t*= 2.0;
	v.x += (snoise(vec4(pv,0.+t)))*a;
	v.y += (snoise(vec4(pv,1.+t)))*a;
	v.z += (snoise(vec4(pv,2.+t)))*a;

	pv *= 2.; a*= .5; t*= 2.0;
	v.x += (snoise(vec4(pv,0.+t)))*a;
	v.y += (snoise(vec4(pv,1.+t)))*a;
	v.z += (snoise(vec4(pv,2.+t)))*a;

	pv *= 2.; a*= .5;
	v.x += (snoise(vec4(pv,0.+t)))*a;
	v.y += (snoise(vec4(pv,1.+t)))*a;
	v.z += (snoise(vec4(pv,2.+t)))*a;

	//pv *= 2.; a*= .5;
	//v.x += (snoise(vec4(pv,0.+t)))*a;
	//v.y += (snoise(vec4(pv,1.+t)))*a;
	//v.z += (snoise(vec4(pv,2.+t)))*a;

	//col = vec4(1.0,0.6,0.2,0.1);


	// brownian motion
	//v += randomPos(coord + p0.xy,time) * sn * 0.0001;
	//v += randomPos(coord + p0.xy,time) * 0.1;  // reset
	//p0 = normalize(randomPos(coord + p0.xy,time)) * 0.2;

	//v += p0 * -0.0001;
	float distToSphere = length(p0) - 0.4;
	v += -normalize(p0) * distToSphere * (0.005);



	// move
	p = p0 + v * (1.+.1*hash13(vec3(coord,1.)));
	col.rgb = col0.rgb;

	//col.rgb = normalize(p.xyz) * 0.9 + vec3(0.1);
	//col.rgb = normalize(v) * 0.5 + 0.5;

	return PosCol(vec4(p,s),col);
}


void main(void)
{
	//PosCol b = blob1(texcoord);
	//PosCol a = rose1(texcoord);

	//PosCol a = blob1(texcoord);
	//PosCol b = blob2(texcoord);
	//PosCol a = rose1(texcoord);
	//PosCol a = plane1(texcoord);
	//PosCol a = flow1(texcoord);

	//float m = (1.+sin(texcoord.x * 7.5 + time * 0.2))*.5;
	//float m = sin(texcoord.x * 0.5 + time * 0.2) * 0.5 + 0.5;
	float m = 
		getAudioDataSample(audioDataTex,A_DF_LP3,currentPositionEst) * 0.1 +
		getAudioDataSample(audioDataTex,A_DF_LP1,currentPositionEst) * 0.2 +
		0.1;
	m = 0.1;
	
	PosCol a = flow1(texcoord);
	PosCol b = blob3(texcoord);

	a.pos = mix(a.pos,b.pos,m);
	a.col = mix(a.col,b.col,m);
	


	//
	//a.pos = mix(a.pos,c.pos,m2);
	//a.col = mix(a.col,c.col,m2);


	//m = getAudioDataSample(audioDataTex,A_DF_LP1,currentPositionEst)*0.2;
	//a.pos = mix(a.pos,c.pos,m);

	//PosCol a ;
	//if (texcoord.y<0.5){
	//	a = plane1(texcoord * vec2(1.0,2.0));
	//}
	//else{
	//	a = rose1((texcoord - vec2(0.0,0.5)) * vec2(1.0,2.0));
	//}

	out_Pos = a.pos;
	out_Col = a.col;
}




