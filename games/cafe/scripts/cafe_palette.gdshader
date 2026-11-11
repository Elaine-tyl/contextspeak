shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
uniform sampler2D base_texture : source_color, filter_linear_mipmap;
uniform vec4 accent : source_color = vec4(0.66, 0.48, 0.30, 1.0);
uniform vec4 sage_colour : source_color = vec4(0.435, 0.549, 0.475, 1.0);
uniform bool upholstery = false;
varying vec3 world_at;

void vertex() {
    world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec3 original = texture(base_texture, UV).rgb;
    // Keep the licensed atlas details and metal regions. Warm timber and muted
    // upholstery replace the saturated orange/green blocks in the original art.
    float orange = step(0.45, original.r) * step(original.g + 0.14, original.r)
        * step(original.b + 0.07, original.g);
    float green = step(original.r + 0.05, original.g) * step(original.b + 0.03, original.g);
    float phase = (world_at.z + world_at.y * 0.7) * 31.0 + sin(world_at.x * 0.9) * 0.7;
    float grain_visibility = 1.0 - smoothstep(0.5, 2.0, fwidth(phase));
    float grain = 0.995 + 0.005 * sin(phase) * grain_visibility;
    vec3 oak = accent.rgb * (0.88 + 0.12 * original.r) * grain;
    vec3 sage = sage_colour.rgb * (0.84 + original.g * 0.16);
    ALBEDO = mix(original, upholstery ? sage : oak, orange);
    ALBEDO = mix(ALBEDO, sage, green);
    // The light grey enamel stays creamy, with enough contrast to read its form.
    float enamel = step(0.72, min(original.r, min(original.g, original.b)));
    ALBEDO = mix(ALBEDO, original * vec3(0.95, 0.91, 0.82), enamel * 0.6);
    ROUGHNESS = upholstery ? 0.94 : 0.76;
    SPECULAR = 0.22;
}
