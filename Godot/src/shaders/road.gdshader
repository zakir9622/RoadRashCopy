shader_type spatial;
// Highway asphalt: CC0 albedo/normal/ARM, procedural lane markings in UV
// space. Biome uniforms tint, wet the surface, and add extra city lanes.

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D arm_tex : hint_default_white, filter_linear_mipmap, repeat_enable;

uniform float tile_across = 3.0;
uniform vec3 asphalt_tint = vec3(1.0);
uniform float wetness = 0.0;
uniform float extra_lanes = 0.0;
uniform vec4 line_color : source_color = vec4(0.95, 0.95, 0.92, 1.0);
uniform vec4 center_color : source_color = vec4(1.0, 0.78, 0.24, 1.0);

varying vec2 track_uv;

void vertex() {
	track_uv = UV;
}

float stripe(float x, float center, float w) {
	float d = abs(x - center);
	return 1.0 - smoothstep(w * 0.5, w * 0.5 + fwidth(x) * 1.5, d);
}

void fragment() {
	vec2 uv = vec2(track_uv.x * tile_across, track_uv.y);
	vec3 albedo = texture(albedo_tex, uv).rgb * asphalt_tint;
	vec3 arm = texture(arm_tex, uv).rgb;

	float u = track_uv.x;
	float wear = 0.0;
	wear += stripe(u, 0.30, 0.10) * 0.5;
	wear += stripe(u, 0.70, 0.10) * 0.5;
	albedo *= 1.0 - wear * 0.18;

	float edge = stripe(u, 0.035, 0.012) + stripe(u, 0.965, 0.012);
	float dash_mask = step(0.5, fract(track_uv.y * 0.55));
	float center = stripe(u, 0.5, 0.010) * dash_mask;
	// City / night: extra dashed lane splits so it reads as a packed boulevard.
	float lanes = 0.0;
	if (extra_lanes > 0.5) {
		lanes += stripe(u, 0.25, 0.007) * dash_mask;
		lanes += stripe(u, 0.75, 0.007) * dash_mask;
	}

	float paint_wear = texture(albedo_tex, uv * 2.7).r;
	float wear_mask = 0.55 + 0.45 * smoothstep(0.08, 0.35, paint_wear);
	edge *= wear_mask;
	center *= wear_mask;
	lanes *= wear_mask;

	albedo = mix(albedo, line_color.rgb, clamp(edge + lanes, 0.0, 1.0) * 0.85);
	albedo = mix(albedo, center_color.rgb, clamp(center, 0.0, 1.0) * 0.9);

	ALBEDO = albedo;
	NORMAL_MAP = texture(normal_tex, uv).rgb;
	AO = arm.r;
	float shine = clamp(edge + center + wetness, 0.0, 1.0);
	ROUGHNESS = mix(arm.g, 0.18, shine * 0.65);
	METALLIC = mix(arm.b, 0.15, wetness);

	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	SPECULAR = 0.5 + fresnel * (0.3 + wetness * 0.4);
}
