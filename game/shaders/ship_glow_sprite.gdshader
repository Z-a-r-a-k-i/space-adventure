// Soft additive glow for projectiles, muzzle flashes, impacts and sparks. The mesh is a unit quad
// facing up (+Y); the owner scales and rotates it. `energy` above 1 feeds the environment glow.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

uniform vec4 tint : source_color = vec4(1.0, 0.8, 0.4, 1.0);
uniform float energy = 2.0;
uniform float fade = 1.0;
// 0 = round glow, 1 = elongated bolt with a hot core.
uniform float streak = 0.0;

varying vec2 local;

void vertex() {
	local = VERTEX.xz * 2.0;
}

void fragment() {
	vec2 p = local;
	float round_glow = 1.0 - smoothstep(0.0, 1.0, length(p));
	float along = 1.0 - smoothstep(0.55, 1.0, abs(p.y));
	float across = 1.0 - smoothstep(0.0, 1.0, abs(p.x));
	float bolt = along * across * across;
	float core = (1.0 - smoothstep(0.0, 0.35, abs(p.x))) * (1.0 - smoothstep(0.35, 0.9, abs(p.y)));
	float shape = mix(round_glow * round_glow, bolt + core * 0.8, streak);
	vec3 hot = mix(tint.rgb, vec3(1.0), clamp(core * streak + round_glow * round_glow * (1.0 - streak) * 0.6, 0.0, 1.0));
	ALBEDO = hot * energy;
	ALPHA = clamp(shape * fade * tint.a, 0.0, 1.0);
}
