// FTL-style shield bubble on a unit XZ plane scaled to the hull ellipse. One bright band per active layer,
// a faint hex lattice near the rim, and up to four impact ripples. Every input is driven from the battle
// presentation clock, so pause freezes it.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, depth_test_disabled, shadows_disabled, fog_disabled;

uniform vec4 shield_color : source_color = vec4(0.36, 0.78, 1.0, 1.0);
uniform float layers = 0.0;
uniform float visibility = 1.0;
uniform float collapse = 0.0;
uniform float regrow = 0.0;
// xy: impact point in unit plane coordinates, z: age in seconds (negative = inactive), w: strength.
uniform vec4 hit0 = vec4(0.0, 0.0, -1.0, 0.0);
uniform vec4 hit1 = vec4(0.0, 0.0, -1.0, 0.0);
uniform vec4 hit2 = vec4(0.0, 0.0, -1.0, 0.0);
uniform vec4 hit3 = vec4(0.0, 0.0, -1.0, 0.0);

varying vec2 local;

void vertex() {
	local = VERTEX.xz;
}

float hex_distance(vec2 p) {
	p = abs(p);
	return max(dot(p, normalize(vec2(1.0, 1.732))), p.x);
}

vec2 hex_cell(vec2 uv) {
	vec2 r = vec2(1.0, 1.732);
	vec2 h = r * 0.5;
	vec2 a = mod(uv, r) - h;
	vec2 b = mod(uv - h, r) - h;
	return dot(a, a) < dot(b, b) ? a : b;
}

float ripple(vec4 hit, float r) {
	if (hit.z < 0.0 || hit.z > 0.9) {
		return 0.0;
	}
	float d = distance(local, hit.xy);
	float front = hit.z * 1.5;
	float fade = 1.0 - hit.z / 0.9;
	float ring = (1.0 - smoothstep(0.0, 0.09, abs(d - front))) * fade;
	float spot = (1.0 - smoothstep(0.0, 0.32, d)) * max(0.0, 1.0 - hit.z / 0.35) * 1.6;
	float cells = 0.35 + 0.65 * smoothstep(0.40, 0.48, hex_distance(hex_cell(local * 13.0)));
	return (ring * cells + spot) * hit.w * smoothstep(0.55, 0.9, r + 0.25);
}

void fragment() {
	float r = length(local);
	if (r > 1.0) {
		discard;
	}
	float active = step(0.5, layers);
	// Faint inner wash that only brightens right at the rim, so the hull stays readable.
	float rim = smoothstep(0.86, 1.0, r);
	float bands = 0.0;
	for (int i = 0; i < 4; i++) {
		if (float(i) < layers) {
			float centre = 0.982 - float(i) * 0.03;
			bands += 1.0 - smoothstep(0.0, 0.009, abs(r - centre));
		}
	}
	float lattice = smoothstep(0.42, 0.48, hex_distance(hex_cell(local * 13.0))) * smoothstep(0.9, 0.99, r);
	float glow = (rim * rim * 0.09 + bands * 0.75 + lattice * 0.08) * active * visibility;
	glow += regrow * (1.0 - smoothstep(0.0, 0.012, abs(r - 0.982))) * 0.9;
	float hits = ripple(hit0, r) + ripple(hit1, r) + ripple(hit2, r) + ripple(hit3, r);
	float burst = collapse * (smoothstep(0.7, 1.0, r) * 1.2 + smoothstep(0.42, 0.48, hex_distance(hex_cell(local * 13.0))) * smoothstep(0.6, 1.0, r) * 0.6);
	float intensity = glow + hits * 1.3 + burst;
	ALBEDO = shield_color.rgb * 1.6;
	ALPHA = clamp(intensity, 0.0, 1.0);
}
