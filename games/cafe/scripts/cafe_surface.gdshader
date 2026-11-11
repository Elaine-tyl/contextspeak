shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

// Metre-based, subtle surface detail; no textures, screen effects or animation.
uniform vec4 colour : source_color = vec4(0.65, 0.47, 0.30, 1.0);
uniform int finish = 0; // oak, floorboards, plaster, woven fabric
varying vec3 world_at;

float filtered_joint(float coordinate, float half_width) {
    // Analytic coverage keeps millimetre-sized joints from becoming dotted
    // rows when several floor fragments share a pixel in the overview.
    float footprint = max(fwidth(coordinate), 0.0001);
    float distance_to_joint = abs(fract(coordinate + 0.5) - 0.5);
    float coverage = 1.0 - smoothstep(half_width - footprint * 0.5,
        half_width + footprint * 0.5, distance_to_joint);
    return coverage * min(1.0, half_width * 2.0 / footprint);
}

void vertex() {
    world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    float variation = 1.0;
    if (finish < 2) {
        float across = world_at.z + world_at.y * 0.7;
        float phase = across * 31.0 + sin(world_at.x * 0.9) * 0.7;
        float grain_visibility = 1.0 - smoothstep(0.5, 2.0, fwidth(phase));
        variation = 0.995 + sin(phase) * 0.005 * grain_visibility;
        if (finish == 1) {
            float row = floor(world_at.z / 0.54);
            float seam = filtered_joint(world_at.z / 0.54, 0.0045);
            float joint = (world_at.x + mod(row, 3.0) * 1.13) / 3.4;
            seam = max(seam, filtered_joint(joint, 0.0008));
            variation *= 0.994 + 0.008 * sin(row * 7.13);
            variation *= 1.0 - seam * 0.12;
        }
    }
    ALBEDO = colour.rgb * variation;
    ROUGHNESS = finish < 2 ? 0.74 : 0.94;
    SPECULAR = finish < 2 ? 0.25 : 0.12;
}
