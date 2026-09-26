// Silhouette outline drawn as a material overlay on character and machine meshes: back faces pushed out
// by a constant number of screen pixels, so dark armour separates from the dark deck at any zoom.
// Hostiles and crew use different tints; the host fades strength to zero for fallen actors. Presentation only.
shader_type spatial;
render_mode unshaded, cull_front, depth_draw_never, shadows_disabled, fog_disabled, blend_mix;

uniform vec4 outline_color : source_color = vec4(1.0, 0.36, 0.24, 0.85);
uniform float width_pixels = 1.8;
uniform float strength = 1.0;

void vertex() {
	vec4 clip = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	vec3 view_normal = normalize((MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	vec2 direction = (PROJECTION_MATRIX * vec4(view_normal, 0.0)).xy;
	float length_xy = length(direction);
	if (length_xy > 1e-5) {
		clip.xy += direction / length_xy / VIEWPORT_SIZE * width_pixels * 2.0 * clip.w * step(0.001, strength);
	}
	POSITION = clip;
}

void fragment() {
	ALBEDO = outline_color.rgb;
	ALPHA = outline_color.a * strength;
}
