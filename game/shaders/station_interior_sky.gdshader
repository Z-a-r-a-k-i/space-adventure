// Interior radiance for the station: never drawn as a background (the environment keeps its flat colour),
// only sampled for ambient and reflections. Cool ceiling panels overhead and a warm/cool horizon split give
// metal and armour soft highlights and let vertical faces read apart from the deck. No time input.
shader_type sky;
render_mode use_debanding;

uniform vec3 zenith_color : source_color = vec3(0.34, 0.42, 0.5);
uniform vec3 horizon_color : source_color = vec3(0.1, 0.13, 0.16);
uniform vec3 ground_color : source_color = vec3(0.018, 0.022, 0.028);
uniform vec3 panel_color : source_color = vec3(0.82, 0.9, 1.0);
uniform float panel_energy = 1.6;
uniform vec3 warm_color : source_color = vec3(1.0, 0.6, 0.32);
uniform vec3 warm_direction = vec3(-0.55, 0.25, 0.8);
uniform float warm_energy = 0.55;
uniform vec3 cool_color : source_color = vec3(0.2, 0.75, 0.85);
uniform vec3 cool_direction = vec3(0.7, 0.2, -0.6);
uniform float cool_energy = 0.35;

void sky() {
	vec3 d = normalize(EYEDIR);
	vec3 color = mix(horizon_color, zenith_color, smoothstep(0.02, 0.85, d.y));
	color = mix(color, ground_color, smoothstep(0.0, -0.35, d.y));
	if (d.y > 0.2) {
		// Rows of ceiling fixtures projected on a plane above the viewer.
		vec2 plane = d.xz / d.y;
		vec2 cell = abs(fract(plane * vec2(0.55, 0.9)) - 0.5);
		float panel = (1.0 - smoothstep(0.1, 0.14, cell.x)) * (1.0 - smoothstep(0.3, 0.36, cell.y));
		color += panel_color * panel * panel_energy * smoothstep(0.2, 0.55, d.y);
	}
	color += warm_color * warm_energy * pow(max(dot(d, normalize(warm_direction)), 0.0), 6.0);
	color += cool_color * cool_energy * pow(max(dot(d, normalize(cool_direction)), 0.0), 6.0);
	COLOR = color;
}
