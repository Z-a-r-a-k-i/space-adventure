// Radiance for the ship views: never drawn (the background stays the starfield plane), only sampled for
// ambient and reflections so metallic hulls pick up a cool overhead sheen, a warm key glint and a teal
// nebula bounce. No time input.
shader_type sky;
render_mode use_debanding;

uniform vec3 zenith_color : source_color = vec3(0.22, 0.29, 0.38);
uniform vec3 horizon_color : source_color = vec3(0.05, 0.08, 0.12);
uniform vec3 ground_color : source_color = vec3(0.01, 0.012, 0.02);
uniform vec3 warm_color : source_color = vec3(1.0, 0.72, 0.46);
uniform vec3 warm_direction = vec3(-0.45, 0.75, 0.35);
uniform float warm_energy = 1.4;
uniform vec3 teal_color : source_color = vec3(0.18, 0.62, 0.72);
uniform vec3 teal_direction = vec3(0.6, 0.45, -0.5);
uniform float teal_energy = 0.7;

void sky() {
	vec3 d = normalize(EYEDIR);
	vec3 color = mix(horizon_color, zenith_color, smoothstep(0.0, 0.9, d.y));
	color = mix(color, ground_color, smoothstep(0.0, -0.4, d.y));
	color += warm_color * warm_energy * pow(max(dot(d, normalize(warm_direction)), 0.0), 12.0);
	color += teal_color * teal_energy * pow(max(dot(d, normalize(teal_direction)), 0.0), 5.0);
	COLOR = color;
}
