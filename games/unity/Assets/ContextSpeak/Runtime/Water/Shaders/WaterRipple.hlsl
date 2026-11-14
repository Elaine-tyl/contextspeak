// Reads the local interactive ripple field written by WaterRippleSimulation (global shader values).
// Include after URP Core.hlsl. Returns zero when no simulation runs or the point is outside it.
#ifndef CONTEXTSPEAK_WATER_RIPPLE_INCLUDED
#define CONTEXTSPEAK_WATER_RIPPLE_INCLUDED

TEXTURE2D(_CSRippleTex);
SamplerState cs_ripple_linear_clamp_sampler;
float4 _CSRippleArea;     // window origin x, z · size (m) · 1/resolution
float4 _CSRippleParams;   // normal strength · foam amount · enabled · vertex displacement scale

float2 CSRippleUV(float3 positionWS) { return (positionWS.xz - _CSRippleArea.xy) / max(_CSRippleArea.z, 0.001); }

float CSRippleEdge(float2 uv) { return saturate(min(min(uv.x, 1 - uv.x), min(uv.y, 1 - uv.y)) * 14); }

float CSRippleHeightLod(float3 positionWS)
{
    if (_CSRippleParams.z < 0.5) return 0;
    float2 uv = CSRippleUV(positionWS);
    if (uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y > 1) return 0;
    return SAMPLE_TEXTURE2D_LOD(_CSRippleTex, cs_ripple_linear_clamp_sampler, uv, 0).r * CSRippleEdge(uv);
}

// slope = d(height)/dx, d(height)/dz (scaled by the normal strength); foam 0…1 where the water is energetic.
void CSRipple(float3 positionWS, out float2 slope, out float foam)
{
    slope = 0; foam = 0;
    if (_CSRippleParams.z < 0.5) return;
    float2 uv = CSRippleUV(positionWS);
    if (uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y > 1) return;
    float t = _CSRippleArea.w;
    float cell = t * _CSRippleArea.z;
    float2 c = SAMPLE_TEXTURE2D_LOD(_CSRippleTex, cs_ripple_linear_clamp_sampler, uv, 0).rg;
    float hl = SAMPLE_TEXTURE2D_LOD(_CSRippleTex, cs_ripple_linear_clamp_sampler, uv - float2(t, 0), 0).r;
    float hr = SAMPLE_TEXTURE2D_LOD(_CSRippleTex, cs_ripple_linear_clamp_sampler, uv + float2(t, 0), 0).r;
    float hd = SAMPLE_TEXTURE2D_LOD(_CSRippleTex, cs_ripple_linear_clamp_sampler, uv - float2(0, t), 0).r;
    float hu = SAMPLE_TEXTURE2D_LOD(_CSRippleTex, cs_ripple_linear_clamp_sampler, uv + float2(0, t), 0).r;
    float edge = CSRippleEdge(uv);
    slope = float2(hr - hl, hu - hd) / (2 * cell) * _CSRippleParams.x * edge;
    float speed = abs(c.r - c.g);                            // vertical velocity × step
    foam = saturate((speed * 160 + max(0, length(slope) - 0.35) * 1.4) * _CSRippleParams.y - 0.08) * edge;
}

#endif
