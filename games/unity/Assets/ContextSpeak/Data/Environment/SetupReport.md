# Environment setup report (2026-11-13 19:41)

## Changes
- Inspected: Unity 6000.3.28f1, URP asset Assets/Generated/CafePipeline.asset (HDR True, MSAA 2×, shadows 2048 px / 28 m / 4 cascades), quality level 'Ultra'. Pipeline unchanged (URP).
- Quality presets: Performance (0.9× scale, 2048 px shadows, 60 m, 2 cascades, soft low, SMAA, no SSAO), High (1.0×, 2048 px, 100 m, 4 cascades, soft medium, MSAA 2×, SSAO), Ultra (1.0×, 4096 px, 140 m, 4 cascades, soft high, MSAA 4×, SSAO). Assigned to Unity quality levels; HDR on; depth + opaque textures on (water).
- Calibrated 16 generated flat-colour materials (albedo ≤ 0.86 sRGB; glass smoothness 0.92, non-metal).
- Reusable light prefabs (colour temperature, auto day/night via SkyLightSwitch, no shadows by default): Assets/ContextSpeak/Data/Environment/Lights
- MetroCity: exposure recalibrated, environment controllers + post volume
- ForestSlice: exposure recalibrated, environment controllers + post volume
- Hospital: exposure recalibrated, environment controllers + post volume, 3 lighting zones with local reflection probes
- Restaurant: exposure recalibrated, environment controllers + post volume, 1 lighting zones with local reflection probes
- Airport: exposure recalibrated, environment controllers + post volume
- AircraftCabin: exposure recalibrated, environment controllers + post volume, 1 lighting zones with local reflection probes
- ArrivalAirport: exposure recalibrated, environment controllers + post volume
- TropicalBeach: exposure recalibrated, environment controllers + post volume

## Missing / placeholders
- HDRI skies (optional): put day/sunset/night .hdr or .exr files in Assets/Art/HDRI/ (e.g. Poly Haven, CC0) — the procedural sky is used meanwhile
