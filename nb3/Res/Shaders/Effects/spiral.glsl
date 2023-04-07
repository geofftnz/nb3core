//|effect
#version 410
precision highp float;
layout (location = 0) in vec2 texcoord;
layout (location = 1) in vec2 pos;
layout (location = 0) out vec4 out_Colour;
uniform sampler2D spectrumTex;
uniform sampler2D spectrum2Tex;
uniform sampler2D audioDataTex;
uniform float currentPosition;
uniform float currentPositionEst;

#include "Common/gamma.glsl";

#define DATARES 256
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

float stexel = 1.0/DATARES;
float ttexel = 1.0/1024.0;

float getDataSample(float index, float offset)
{
	vec2 t = vec2(index * stexel, currentPositionEst - offset * ttexel);
	
	float s = texture2D(audioDataTex,t).r;
	return s;
}

vec2 texel = vec2(1.0/1024.0,0.0);

vec4 getSample(sampler2D spectrum, vec2 t)
{
	vec2 raw = texture2D(spectrum,t).rg;

	float sep = asin((raw.g - raw.r) / (raw.g + raw.r)) / PIOVER2;

	vec4 s = vec4(raw.rg,(raw.r+raw.g)*0.5, sep);

	return s;
}

vec4 getOffsetSample(sampler2D spectrum, float freq, float offset)
{
	return getSample(spectrum, vec2(freq, currentPositionEst - offset * ttexel));
}


vec4 scaleSpectrum(vec4 s)
{
	return todB(s);
}

float fscale(float x)
{
	return x * 0.1 + 0.9 * x * x;
}

float vol_min  = 0.; // Minimum volume that shows up
float vol_max  = 1.; // Volume that saturates the color

// Musical parameters
float A        = 440.0 / 2.;       // Lowest note
float tet_root = 1.05946309435929; // 12th root of 2

// Spiral visual parameters from https://www.shadertoy.com/view/WtjSWt
float dis      = .05;
float width    = .02;
float blur     = .05;

// credit for spiral: windytan [oona räisänen] https://www.windytan.com/2021/11/spiral-spectrograms-and-intonation.html 
void main() {
    vec2  uv     = texcoord;
    float aspect = 1.4;  // TODO: pass aspect
    
    vec2 uvcorrected = uv - vec2(0.5, 0.5);
    uvcorrected.x   *= aspect;

    float angle      = atan(uvcorrected.y, uvcorrected.x);
    float offset     = length(uvcorrected) + (angle/(2. * PI)) * dis;
    float which_turn = floor(offset / dis);
    float cents      = (which_turn - (angle / 2. / PI)) * 1200.;
    float freq       = A * pow(tet_root, cents / 100.);
    float bin        = freq / 44100.;

	vec4 samp = getOffsetSample(spectrumTex,bin,0.);

	/*
    float bri        = todB(samp.b);
    
    bri = (bri - vol_min) / (vol_max - vol_min);
    bri = max(bri, 0.);
    
    // Control the curve of the color mapping. Try e.g. 2. or 4.
    bri = pow(bri, 2.);

    vec3 lineColor;
    if (bri < 0.5) {
        lineColor = vec3(bri/.5, 0., bri/.5);
    } else {
        lineColor = vec3(1., (bri - .5) * 2., 1.);
    }*/

	vec3 lineColor = colscale(pow(todB(samp.b),3.0)*2.);

    float circles = mod(offset, dis);
    vec3  col     = bin > 1. ? vec3(0., 0., 0.) :
                    (smoothstep(circles-blur,circles,width) -
                     smoothstep(circles,circles+blur,width)) * lineColor;
    
    out_Colour     = vec4(l2g(col.rgb), 1.);
}
