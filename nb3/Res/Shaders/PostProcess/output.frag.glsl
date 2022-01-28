#version 450
precision highp float;
layout (location = 0) in vec2 texcoord;
layout (location = 0) out vec4 out_Col;

uniform sampler2D inputTex;


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

void main(void)
{

	vec3 col = texture2D(inputTex,texcoord).rgb;

	// some repetition
	vec2 p = (texcoord - vec2(.5));
	float m = .2;
	for (int i=0;i<16;i++)
	{
		m *= 0.5;
		p *= 0.95;
		//p = mod(p + vec2(.5),1.) - vec2(.5);
		//p = rot2D(p,.3);

		col += texture2D(inputTex,p + vec2(.5)).rgb * m;
	}
	p=texcoord-vec2(.5); m=.2;
	for (int i=0;i<16;i++)
	{
		m *= 0.5;
		p *= 1.1;
		//p = mod(p + vec2(.5),1.) - vec2(.5);
		//p = rot2D(p,3.1415927/3.);

		col += texture2D(inputTex,p + vec2(.5)).rgb * m;
	}



	// horizontal bars
	//vec2 tc2 = vec2(.5,texcoord.y);
	//vec3 col2 = texture2D(inputTex,tc2).rgb;
	//col += col2 * 0.05 * (1. - abs(texcoord.x - tc2.x) * 2.);


   //float bias = 1.0;
   //col = Uncharted2Tonemap(col * bias);
   //vec3 whiteScale = vec3(1.0)/Uncharted2Tonemap(vec3(W));
   //col = col * whiteScale;

	// gamma
	col.rgb = pow(col,vec3(1./2.2));

	//col.rgb = col.rgb / (vec3(1.)+col.rgb);
	// todo: tonemap

	out_Col = vec4(col,1.0);
}
