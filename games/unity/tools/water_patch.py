"""Small, idempotent in-place edits for the character–water interaction system (run from the Unity
project root). Edits only the lines it needs so concurrent work on these files is preserved:
  - CafePlayer.cs: SpeedScale (water drag) multiplies the input speed.
  - WaterSurface.shader / Ocean.shader: read the interactive ripple field (normals + foam; optional
    vertex displacement on the inland shader)."""
import io, sys

def patch(path, edits):
    with io.open(path, encoding="utf-8") as f: s = f.read()
    changed = 0
    for marker, old, new in edits:
        if marker in s: continue
        if old not in s:
            print("SKIP (anchor not found): %s :: %r" % (path, old[:60])); continue
        s = s.replace(old, new, 1); changed += 1
    if changed:
        with io.open(path, "w", encoding="utf-8") as f: f.write(s)
    print("%s: %d edit(s)" % (path, changed))

patch("Assets/_Game/Player/CafePlayer.cs", [
    ("public float SpeedScale",
     "        public Gait DefaultGait = Gait.Walk;",
     "        public Gait DefaultGait = Gait.Walk;\n        [Tooltip(\"Multiplies movement speed (water drag, deep snow…). Set by CharacterWaterInteraction.\")]\n        public float SpeedScale = 1;"),
    ("Mathf.Clamp(SpeedScale",
     "                    targetSpeed *= Mathf.Clamp01(input.magnitude); // analogue sticks walk slowly at small tilt",
     "                    targetSpeed *= Mathf.Clamp01(input.magnitude); // analogue sticks walk slowly at small tilt\n                    targetSpeed *= Mathf.Clamp(SpeedScale, 0.05f, 2f);"),
])

INCLUDE = '#include "Assets/ContextSpeak/Runtime/Water/Shaders/WaterRipple.hlsl"'
patch("Assets/ContextSpeak/Runtime/Environment/Shaders/WaterSurface.shader", [
    ("_RippleDisplace (",
     '        _Vertical ("Falling sheet (waterfall)", Range(0, 1)) = 0',
     '        _Vertical ("Falling sheet (waterfall)", Range(0, 1)) = 0\n        _RippleDisplace ("Interactive ripple displacement", Range(0, 1)) = 0'),
    ("_Smoothness, _Vertical, _RippleDisplace;",
     "_Smoothness, _Vertical;",
     "_Smoothness, _Vertical, _RippleDisplace;"),
    (INCLUDE,
     '            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"',
     '            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"\n            ' + INCLUDE),
    ("CSRippleHeightLod(output.positionWS)",
     "                output.normalWS = TransformObjectToWorldNormal(input.normalOS);\n                output.positionCS = TransformWorldToHClip(output.positionWS);",
     "                output.normalWS = TransformObjectToWorldNormal(input.normalOS);\n                output.positionWS.y += CSRippleHeightLod(output.positionWS) * _RippleDisplace * (1 - _Vertical);   // interactive ripples (dense meshes)\n                output.positionCS = TransformWorldToHClip(output.positionWS);"),
    ("CSRipple(input.positionWS, rippleSlope, rippleFoam);",
     "                float3 normal = normalize(baseN + (tangent * (h - hx) + bitangent * (h - hz)) / e * strength * 0.15);",
     "                float3 normal = normalize(baseN + (tangent * (h - hx) + bitangent * (h - hz)) / e * strength * 0.15);\n"
     "                // Interactive ripples from characters, objects, rain and fish (WaterRippleSimulation).\n"
     "                float2 rippleSlope; float rippleFoam;\n"
     "                CSRipple(input.positionWS, rippleSlope, rippleFoam);\n"
     "                rippleSlope *= 1 - _Vertical; rippleFoam *= 1 - _Vertical;\n"
     "                normal = normalize(normal + float3(-rippleSlope.x, 0, -rippleSlope.y) * (front ? 1 : -1));"),
    ("foam = saturate(foam + rippleFoam);",
     "                colour = lerp(colour, _FoamColor.rgb * (SampleSH(float3(0, 1, 0)) + sun.color * sun.shadowAttenuation * 0.8), foam);",
     "                foam = saturate(foam + rippleFoam);\n                colour = lerp(colour, _FoamColor.rgb * (SampleSH(float3(0, 1, 0)) + sun.color * sun.shadowAttenuation * 0.8), foam);"),
])

patch("Assets/ContextSpeak/Runtime/Beach/Shaders/Ocean.shader", [
    (INCLUDE,
     '            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"',
     '            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"\n            ' + INCLUDE),
    ("CSRipple(input.positionWS, rippleSlope, rippleFoam);",
     "                float3 normal = normalize(float3(input.normalWS.x + detail.x, input.normalWS.y, input.normalWS.z + detail.z));",
     "                float3 normal = normalize(float3(input.normalWS.x + detail.x, input.normalWS.y, input.normalWS.z + detail.z));\n"
     "                // Interactive ripples layered on the Gerstner waves (the base waves are untouched).\n"
     "                float2 rippleSlope; float rippleFoam;\n"
     "                CSRipple(input.positionWS, rippleSlope, rippleFoam);\n"
     "                normal = normalize(normal + float3(-rippleSlope.x, 0, -rippleSlope.y));"),
    ("foam = saturate(foam + rippleFoam);",
     "                colour = lerp(colour, _FoamColor.rgb * (0.6 + 0.4 * sun.color), foam);",
     "                foam = saturate(foam + rippleFoam);\n                colour = lerp(colour, _FoamColor.rgb * (0.6 + 0.4 * sun.color), foam);"),
])

patch("Assets/ContextSpeak/Editor/ForestGreenhouse.cs", [
    ("if (settings == null) settings = renderer.gameObject.AddComponent<WaterSettings>();",
     "                var settings = renderer.GetComponent<WaterSettings>() ?? renderer.gameObject.AddComponent<WaterSettings>();",
     "                var settings = renderer.GetComponent<WaterSettings>();\n                if (settings == null) settings = renderer.gameObject.AddComponent<WaterSettings>();"),
])
