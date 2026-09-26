// Deep-space backdrop under a ship view, evaluated in screen space so stars stay crisp at every zoom and the
// two battle frames read as one continuous sky (each frame passes its offset in that shared sky). A small
// camera parallax keeps depth when panning. Presentation only; no time input, so pause leaves it untouched.
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, depth_draw_never, fog_disabled;

uniform vec3 base_color : source_color = vec3(0.006, 0.011, 0.022);
uniform vec3 nebula_teal : source_color = vec3(0.04, 0.24, 0.32);
uniform vec3 nebula_violet : source_color = vec3(0.2, 0.07, 0.26);
uniform vec3 nebula_ember : source_color = vec3(0.32, 0.12, 0.05);
uniform float nebula_gain = 1.0;
uniform float star_gain = 1.0;
uniform float seed = 0.0;
// Offset of this frame's first pixel in the shared sky, in this viewport's pixels.
uniform vec2 sky_offset = vec2(0.0);
// Camera drift in sky units (1.0 = one view height).
uniform vec2 parallax = vec2(0.0);

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
	for (int i = 0; i < 6; i++) {
		value += amplitude * noise(p);
		p = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)) * p * 2.02 + vec2(1.7, 9.2);
		amplitude *= 0.5;
	}
	return value;
}

// One star layer on a grid of `cell` sky units; radius and glare are in pixels so points stay sharp.
vec3 star_layer(vec2 sky, float cell, float density, float radius_px, float px_per_unit, float glare, float layer_seed) {
	vec2 grid = sky / cell;
	vec2 id = floor(grid);
	float h = hash(id + layer_seed + seed);
	if (h > density) {
		return vec3(0.0);
	}
	vec2 centre = (id + 0.15 + 0.7 * vec2(hash(id + 3.7 + layer_seed), hash(id + 8.1 + layer_seed))) * cell;
	vec2 delta = (sky - centre) * px_per_unit;
	float d = length(delta);
	float brightness = 0.35 + 0.65 * hash(id + 17.0 + layer_seed);
	float core = 1.0 - smoothstep(radius_px * 0.4, radius_px + 0.6, d);
	float spikes = glare * (exp(-abs(delta.y) * 1.4) * exp(-abs(delta.x) * 0.22) + exp(-abs(delta.x) * 1.4) * exp(-abs(delta.y) * 0.22));
	float halo = glare * 0.35 * exp(-d * 0.45);
	float temperature = hash(id + 29.0 + layer_seed);
	vec3 tint = temperature < 0.2 ? vec3(1.0, 0.78, 0.6) : temperature > 0.8 ? vec3(0.7, 0.84, 1.0) : vec3(0.95, 0.96, 1.0);
	return tint * brightness * (core + spikes + halo);
}

void fragment() {
	float height = max(VIEWPORT_SIZE.y, 1.0);
	vec2 pixel = FRAGCOORD.xy + sky_offset;
	vec2 sky = pixel / height + parallax;
	float px_per_unit = height;

	vec2 q = sky * 1.35 + vec2(seed * 0.37, seed * 0.21);
	float warp = fbm(q * 0.9);
	float cloud = fbm(q + vec2(warp * 1.6, -warp * 0.9));
	float veil = fbm(q * 2.1 - vec2(warp, warp * 0.4) + 5.2);
	float ember = fbm(q * 1.2 + vec2(-warp * 0.8, warp) + 11.3);
	float dust = fbm(q * 3.4 + 17.0);

	vec3 color = base_color;
	color += nebula_teal * smoothstep(0.42, 0.9, cloud) * nebula_gain;
	color += nebula_violet * smoothstep(0.5, 0.95, veil) * 0.9 * nebula_gain;
	color += nebula_ember * smoothstep(0.62, 0.98, ember) * 0.55 * nebula_gain;
	color *= 1.0 - smoothstep(0.5, 0.78, dust) * 0.45;

	// Faint dense field, a mid layer, and rare bright stars with a small four-point glare.
	vec3 stars = star_layer(sky, 0.011, 0.3, 0.55, px_per_unit, 0.0, 1.3) * 0.26;
	stars += star_layer(sky, 0.028, 0.3, 0.8, px_per_unit, 0.0, 7.9) * 0.6;
	stars += star_layer(sky, 0.12, 0.22, 1.1, px_per_unit, 0.5, 3.1) * 1.3;
	// Stars dim slightly behind the densest dust.
	color += stars * star_gain * (1.0 - smoothstep(0.55, 0.8, dust) * 0.5);
	ALBEDO = color;
}
