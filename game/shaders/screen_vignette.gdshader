// Soft corner falloff drawn over the 3D view and under the HUD, pulling the eye to the play area.
shader_type canvas_item;

uniform vec4 tint : source_color = vec4(0.0, 0.008, 0.018, 1.0);
uniform float strength = 0.4;
uniform float inner = 0.42;
uniform float outer = 0.95;

void fragment() {
	vec2 centred = (UV - 0.5) * vec2(1.0, 0.8);
	float falloff = smoothstep(inner, outer, length(centred) * 1.414);
	COLOR = vec4(tint.rgb, falloff * strength);
}
