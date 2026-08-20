shader_type spatial;
// Highway asphalt: CC0 albedo/normal/ARM, procedural lane markings painted in
// UV space (u across the road, v down it), centre wear darkening, and a
// fresnel sheen so low sun rakes across the surface like real tarmac.

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D arm_tex : hint_default_white, filter_linear_mipmap, repeat_enable;

uniform float tile_across = 3.0;
uniform vec4 line_color : source_color = vec4(0.95, 0.95, 0.92, 1.0);
uniform vec4 center_color : source_color = vec4(1.0, 0.78, 0.24, 1.0);

varying vec2 track_uv;

void vertex() {
	track_uv = UV;
}

// Anti-aliased stripe: 1 inside the band around `center` of width `w`.
float stripe(float x, float center, float w) {
	float d = abs(x - center);
	return 1.0 - smoothstep(w * 0.5, w * 0.5 + fwidth(x) * 1.5, d);
}

void fragment() {
	vec2 uv = vec2(track_uv.x * tile_across, track_uv.y);
	vec3 albedo = texture(albedo_tex, uv).rgb;
	vec3 arm = texture(arm_tex, uv).rgb;

	// Wheel-track wear: two darker bands per lane where tyres actually run.
	float u = track_uv.x;
	float wear = 0.0;
	wear += stripe(u, 0.30, 0.10) * 0.5;
	wear += stripe(u, 0.70, 0.10) * 0.5;
	albedo *= 1.0 - wear * 0.18;

	// Edge lines (solid white) and centre dashes (warm yellow).
	float edge = stripe(u, 0.035, 0.012) + stripe(u, 0.965, 0.012);
	float dash_mask = step(0.5, fract(track_uv.y * 0.55));
	float center = stripe(u, 0.5, 0.010) * dash_mask;

	// Paint sits on top of the asphalt, slightly worn by the same noise.
	float paint_wear = texture(albedo_tex, uv * 2.7).r;
	edge *= smoothstep(0.15, 0.45, paint_wear);
	center *= smoothstep(0.15, 0.45, paint_wear);

	albedo = mix(albedo, line_color.rgb, clamp(edge, 0.0, 1.0) * 0.85);
	albedo = mix(albedo, center_color.rgb, clamp(center, 0.0, 1.0) * 0.9);

	ALBEDO = albedo;
	NORMAL_MAP = texture(normal_tex, uv).rgb;
	AO = arm.r;
	ROUGHNESS = mix(arm.g, 0.25, clamp(edge + center, 0.0, 1.0) * 0.5);
	METALLIC = arm.b;

	// Grazing-angle sheen: distant road catches the sky like real asphalt.
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	SPECULAR = 0.5 + fresnel * 0.3;
}
