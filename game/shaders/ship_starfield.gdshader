// Static deep-space backdrop under a ship view: layered stars and faint nebula, from world XZ.
// Presentation only; no time input, so pause and terminal outcomes leave it untouched.
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, depth_draw_never, fog_disabled;

uniform vec3 base_color : source_color = vec3(0.012, 0.02, 0.035);
uniform vec3 nebula_a : source_color = vec3(0.05, 0.12, 0.22);
uniform vec3 nebula_b : source_color = vec3(0.16, 0.05, 0.12);
uniform float seed = 0.0;
uniform float star_gain = 1.0;

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

float fbm(vec2 p) {
	float value = 0.0;
	float amplitude = 0.5;
	for (int i = 0; i < 5; i++) {
		value += amplitude * noise(p);
		p = p * 2.03 + vec2(1.7, 9.2);
		amplitude *= 0.5;
	}
	return value;
}

float stars(vec2 p, float density, float size, float threshold) {
	vec2 cell = floor(p * density);
	vec2 local = fract(p * density) - 0.5;
	float h = hash(cell + seed);
	if (h < threshold) {
		return 0.0;
	}
	vec2 offset = vec2(hash(cell + 1.7), hash(cell + 3.1)) - 0.5;
	float d = length(local - offset * 0.7);
	return (1.0 - smoothstep(0.0, size, d)) * (h - threshold) / (1.0 - threshold);
}

void fragment() {
	vec2 p = world_position.xz * 0.07 + vec2(seed * 3.1, seed * 1.7);
	float cloud = fbm(p * 1.4);
	float veil = fbm(p * 2.9 + 11.0);
	vec3 color = base_color;
	color += nebula_a * smoothstep(0.42, 0.95, cloud);
	color += nebula_b * smoothstep(0.55, 1.0, veil) * 0.8;
	vec2 q = world_position.xz + vec2(seed * 13.0, seed * 7.0);
	float s = stars(q, 0.9, 0.09, 0.90) * 1.4 + stars(q + 7.3, 2.2, 0.07, 0.88) * 0.8 + stars(q + 3.1, 4.6, 0.06, 0.86) * 0.45;
	float tint = hash(floor(q * 2.2));
	color += mix(vec3(0.75, 0.85, 1.0), vec3(1.0, 0.86, 0.7), step(0.7, tint)) * s * star_gain;
	ALBEDO = color;
}
