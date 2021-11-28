//|vert
#version 450
precision highp float;
layout (location = 0) in vec3 vertex;
layout (location = 0) out vec2 texcoord;
uniform mat4 projectionMatrix;
uniform mat4 modelMatrix;
uniform mat4 viewMatrix;

void main() 
{
	gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(vertex.xy,0.0,1.0);
	texcoord = vertex.xy * vec2(0.5,-0.5) + vec2(0.5);  // coordinates to 0-1 range, flip vertical
}

//|frag
#version 450
precision highp float;
layout (location = 0) in vec2 texcoord;
layout (location = 0) out vec4 out_Colour;
uniform sampler2D spectrumTex;
uniform sampler2D audioDataTex;
uniform float currentPosition;
uniform float currentPositionEst;

#include "Common/gamma.glsl";

#define DATARES 256


float getAudioDataSample(sampler2D tex, float index, float time)
{
	return texture2D(tex,vec2((index+0.5)/DATARES,time)).r;
}
float fscale(float x)
{
	return x * 0.1 + 0.9 * x * x;
}

float getMultiPeakFilterMarkerIntensity(float markerIndex, float time, float freq)
{
	float df = getAudioDataSample(audioDataTex,5. + markerIndex * 2.,time);  // freq+level pairs starting at index 5
	float da = max(0.0,getAudioDataSample(audioDataTex,6. + markerIndex * 2.,time)-0.05);

	return (1.0 - smoothstep(abs(freq - df),0.0,0.0005)) * da*da*100.0;	
	//return (1.0 - step(0.001,abs(freq - df))) * da*da*100.0;	
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


vec4 renderSpectrum(vec2 t)
{
	float original_tx = t.x;
	t.x = fscale(t.x*0.7);

	vec3 col = vec3(0.0);

	float fade = smoothstep(mod(t.y-currentPositionEst+1.0,1.0)*128.0,0.0,1.0);

	col += getFreqColour(t.x) * getMultiPeakFilterMarkerIntensity(0,t.y,t.x);
	col += getFreqColour(t.x) * getMultiPeakFilterMarkerIntensity(1,t.y,t.x);
	col += getFreqColour(t.x) * getMultiPeakFilterMarkerIntensity(2,t.y,t.x);
	col += getFreqColour(t.x) * getMultiPeakFilterMarkerIntensity(3,t.y,t.x);
	col += getFreqColour(t.x) * getMultiPeakFilterMarkerIntensity(4,t.y,t.x);

	return vec4(col * fade,1.0);
}


void main(void)
{
	vec2 t = texcoord.yx;

	vec4 col = renderSpectrum(t);

	// gamma
	col.rgb = l2g(col.rgb);

	out_Colour = col;	
}

