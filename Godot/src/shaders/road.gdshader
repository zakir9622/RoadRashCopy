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

	float edge = stripe(u, 0.028, 0.018) + stripe(u, 0.972, 0.018);
	float dash_mask = step(0.42, fract(track_uv.y * 0.48));
	// RR3D: bold yellow centre, often a double line on packed streets.
	float center = stripe(u, 0.5, 0.016) * (0.55 + 0.45 * dash_mask);
	if (extra_lanes > 0.5) {
		center = stripe(u, 0.47, 0.012) + stripe(u, 0.53, 0.012);
	}
	float lanes = stripe(u, 0.33, 0.010) * dash_mask + stripe(u, 0.67, 0.010) * dash_mask;
	if (extra_lanes > 0.5) {
		lanes += stripe(u, 0.22, 0.008) * dash_mask;
		lanes += stripe(u, 0.78, 0.008) * dash_mask;
	}

	float paint_wear = texture(albedo_tex, uv * 2.7).r;
	float wear_mask = 0.78 + 0.22 * smoothstep(0.08, 0.35, paint_wear);
	edge *= wear_mask;
	center *= wear_mask;
	lanes *= wear_mask;

	albedo = mix(albedo, line_color.rgb, clamp(edge + lanes, 0.0, 1.0) * 0.95);
	albedo = mix(albedo, center_color.rgb, clamp(center, 0.0, 1.0) * 0.98);

	float tracks = stripe(u, 0.38, 0.045) + stripe(u, 0.62, 0.045);
	albedo *= 1.0 - tracks * (0.08 + wetness * 0.16);

	float puddle = wetness * smoothstep(0.32, 0.62, texture(albedo_tex, uv * 0.28).r);
	float ripple = sin(TIME * 9.0 + track_uv.y * 14.0 + track_uv.x * 22.0) * 0.5 + 0.5;

	ALBEDO = albedo;
	vec3 nmap = texture(normal_tex, uv).rgb;
	nmap = mix(nmap, vec3(0.5, 0.5, 1.0) + vec3(ripple - 0.5, ripple - 0.5, 0.0) * 0.22, puddle);
	NORMAL_MAP = nmap;
	AO = arm.r;
	float shine = clamp(edge + center + wetness + puddle, 0.0, 1.0);
	ROUGHNESS = mix(arm.g, 0.12, shine * 0.72);
	METALLIC = mix(arm.b, 0.18, wetness * 0.85 + puddle * 0.2);

	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	SPECULAR = 0.5 + fresnel * (0.3 + wetness * 0.45);
}
