#version 450
precision highp float;
//layout (location = 0) in vec2 vertex;
layout (location = 0) in vec3 pos;
layout (location = 1) in float size;
layout (location = 2) in vec4 col;
layout (location = 0) out vec4 out_Colour;

void main(void)
{
	vec2 pc = (gl_PointCoord.st - vec2(0.5)) * 2.0;
	float rsq = dot(pc,pc);

	vec4 col2 = col; //vec4(1.0,0.2,0.1,1.0);

	// circle pattern in alpha
	float a = max(0.0,(1.0 - rsq));
	//a*=a;
	a = step(0.06,a);
	
	// size < 0 alpha falloff
	a *= min(1.0,size*size);

	col2.a *= clamp(a,0.0,1.0);

	out_Colour = col2;
}