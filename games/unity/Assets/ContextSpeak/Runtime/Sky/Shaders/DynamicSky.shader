// ContextSpeak dynamic sky for URP: physically-inspired gradient (driven per frame by SkySystem),
// sun disc + Mie glow, sunset horizon band, two-layer drifting fbm clouds with self-shadowing and
// silver lining, a phased moon, twinkling stars and an optional HDRI cubemap blend.
Shader "ContextSpeak/Dynamic Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.18, 0.38, 0.75, 1)
        _HorizonColor ("Horizon", Color) = (0.65, 0.78, 0.92, 1)
        _GroundColor ("Ground", Color) = (0.36, 0.34, 0.32, 1)
        _SunDir ("Direction to sun", Vector) = (0, 0.7, 0.7, 0)
        _SunColor ("Sun colour", Color) = (1, 0.95, 0.85, 1)
        _SunSize ("Sun angular size", Range(0.005, 0.08)) = 0.025
        _SunGlow ("Sun glow", Range(0, 4)) = 1
        _SunsetGlow ("Sunset horizon glow", Range(0, 4)) = 0
        _MoonDir ("Direction to moon", Vector) = (0, -0.7, -0.7, 0)
        _MoonColor ("Moon colour", Color) = (0.85, 0.9, 1, 1)
        _MoonPhase ("Moon phase (0 new, 1 full)", Range(0, 1)) = 0.8
        _MoonSize ("Moon angular size", Range(0.01, 0.08)) = 0.035
        _StarIntensity ("Stars", Range(0, 3)) = 0
        _CloudCover ("Cloud coverage", Range(0, 1)) = 0.4
        _CloudDensity ("Cloud density", Range(0, 1.5)) = 0.9
        _CloudLit ("Cloud lit colour", Color) = (1, 1, 1, 1)
        _CloudShade ("Cloud shade colour", Color) = (0.55, 0.6, 0.68, 1)
        _CloudOffset ("Cloud offset (xy layer 1, zw layer 2)", Vector) = (0, 0, 0, 0)
        _CloudScale ("Cloud scale", Float) = 0.9
        _Exposure ("Exposure", Float) = 1
        [NoScaleOffset] _Hdri ("Optional HDRI", Cube) = "" {}
        _HdriBlend ("HDRI blend", Range(0, 1)) = 0
        _HdriExposure ("HDRI exposure", Float) = 1
        _HdriRotation ("HDRI rotation", Range(0, 360)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor, _MoonColor, _CloudLit, _CloudShade;
                float4 _SunDir, _MoonDir, _CloudOffset;
                float _SunSize, _SunGlow, _SunsetGlow, _MoonPhase, _MoonSize, _StarIntensity;
                float _CloudCover, _CloudDensity, _CloudScale, _Exposure, _HdriBlend, _HdriExposure, _HdriRotation;
            CBUFFER_END
            TEXTURECUBE(_Hdri); SAMPLER(sampler_Hdri);

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float Hash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float Noise(float2 p)
            {
                float2 i = floor(p); float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }
            float Fbm(float2 p)
            {
                float value = 0, amplitude = 0.5;
                [unroll] for (int octave = 0; octave < 5; octave++) { value += amplitude * Noise(p); p = p * 2.03 + float2(17.1, 9.2); amplitude *= 0.5; }
                return value;
            }
            float CloudField(float2 uv)
            {
                float base = Fbm(uv + _CloudOffset.xy);
                float detail = Fbm(uv * 2.7 + _CloudOffset.zw);
                return base * 0.75 + detail * 0.35;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float3 sunDir = normalize(_SunDir.xyz);
                float3 moonDir = normalize(_MoonDir.xyz);
                float up = d.y;

                // Sky gradient: dense horizon haze, deep zenith; below the horizon fades to ground.
                float h = saturate(up);
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(h, 0.42));
                sky = lerp(sky, _GroundColor.rgb, saturate(-up * 5.0));
                float sunDot = saturate(dot(d, sunDir));
                // Mie forward scattering around the sun and a warm horizon band at sunrise/sunset.
                sky += _SunColor.rgb * (pow(sunDot, 8.0) * 0.35 + pow(sunDot, 64.0) * 0.6) * _SunGlow;
                float band = pow(1.0 - h, 6.0) * saturate(up * 8.0 + 1.0);
                sky += _SunColor.rgb * band * pow(saturate(dot(normalize(float3(d.x, 0.0, d.z) + 1e-4), normalize(float3(sunDir.x, 0.0, sunDir.z) + 1e-4)) * 0.5 + 0.5), 3.0) * _SunsetGlow;

                // Stars (only above the horizon), twinkling.
                float3 cell = floor(d * 280.0);
                float star = step(0.9965, Hash3(cell));
                float twinkle = 0.6 + 0.4 * sin(_Time.y * (2.0 + Hash3(cell + 7.0) * 4.0) + Hash3(cell + 3.0) * 6.28);
                float3 local = frac(d * 280.0) - 0.5;
                float point0 = saturate(1.0 - length(local) * 3.2);
                sky += star * point0 * twinkle * _StarIntensity * saturate(up * 6.0) * half3(0.9, 0.95, 1.0);

                // Sun disc (HDR) and moon disc with a phase terminator.
                float sunDisc = smoothstep(cos(_SunSize), cos(_SunSize * 0.82), dot(d, sunDir));
                float moonDot = dot(d, moonDir);
                float moonDisc = smoothstep(cos(_MoonSize), cos(_MoonSize * 0.9), moonDot);
                float3 moonRight = normalize(cross(float3(0, 1, 0), moonDir) + 1e-4);
                float3 moonUp = cross(moonDir, moonRight);
                float2 moonUv = float2(dot(d - moonDir, moonRight), dot(d - moonDir, moonUp)) / max(sin(_MoonSize), 1e-4);
                float lit = saturate((moonUv.x + (_MoonPhase * 2.0 - 1.0) * 1.1) * 3.0 + 0.5);
                float maria = 0.82 + 0.18 * Noise(moonUv * 3.0 + 4.0);
                half3 moon = _MoonColor.rgb * moonDisc * lit * maria * 2.2;
                sky += _MoonColor.rgb * pow(saturate(moonDot), 200.0) * 0.12 * _MoonPhase;

                // Clouds: two drifting fbm layers projected on a curved dome, self-shadowed toward the sun.
                half alpha = 0;
                half3 cloudColour = 0;
                if (up > -0.02)
                {
                    float2 uv = d.xz / (up + 0.18) * _CloudScale;
                    float field = CloudField(uv);
                    float threshold = 1.0 - _CloudCover * 0.95;
                    float density = saturate((field - threshold) / max(0.08, 0.32 * _CloudCover + 0.06)) * _CloudDensity;
                    float towardSun = CloudField(uv + normalize(sunDir.xz + 1e-4) * 0.09);
                    float shade = saturate((field - towardSun) * 3.0 + 0.55);
                    cloudColour = lerp(_CloudShade.rgb, _CloudLit.rgb, shade);
                    // Silver lining where thin cloud sits in front of the sun.
                    cloudColour += _SunColor.rgb * pow(sunDot, 10.0) * (1.0 - saturate(density)) * 1.2;
                    alpha = saturate(density) * smoothstep(-0.02, 0.16, up);
                }

                half3 colour = sky + _SunColor.rgb * sunDisc * 18.0 * (1.0 - alpha) + moon * (1.0 - alpha);
                colour = lerp(colour, cloudColour, alpha);
                colour *= _Exposure;

                // Optional HDRI (photographic sky) blended over the procedural sky.
                if (_HdriBlend > 0.001)
                {
                    float angle = radians(_HdriRotation);
                    float3 rotated = float3(d.x * cos(angle) - d.z * sin(angle), d.y, d.x * sin(angle) + d.z * cos(angle));
                    half3 photo = SAMPLE_TEXTURECUBE(_Hdri, sampler_Hdri, rotated).rgb * _HdriExposure;
                    colour = lerp(colour, photo, _HdriBlend);
                }
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
