// Local interactive water: a damped 2D wave equation on a ping-pong RGHalf height field
// (R = height now, G = height one step ago). Pass 0 steps the simulation (with window scrolling,
// optional advection by a current, absorbing window edges and reflecting walls), pass 1 adds
// disturbances (footsteps, wakes, splashes, rain, fish). Driven by WaterRippleSimulation.cs.
Shader "Hidden/ContextSpeak/Water Ripple Sim"
{
    Properties { _MainTex ("State", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_MainTex);
        SamplerState ripple_sim_linear_clamp_sampler;
        float4 _MainTex_TexelSize;      // 1/w, 1/h, w, h
        float4 _Area;                   // window origin x, z, size (m), unused
        float4 _Shift;                  // uv shift since the last step (window moved), uv advection per step
        float4 _Wave;                   // c² (Courant²), damping, edge absorb texels, wall mode (1 = rect walls)
        float4 _Rect;                   // water rectangle xmin, zmin, xmax, zmax (world)
        float4 _Drops[64];              // x, z, radius, amount (m)
        int _DropCount;

        struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }
        float2 State(float2 uv)
        {
            if (uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y > 1) return 0;
            return SAMPLE_TEXTURE2D_LOD(_MainTex, ripple_sim_linear_clamp_sampler, uv, 0).rg;
        }
        ENDHLSL

        Pass
        {
            Name "Step"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv + _Shift.xy - _Shift.zw;
                float2 t = _MainTex_TexelSize.xy;
                float2 c = State(uv);
                float lap = State(uv + float2(t.x, 0)).r + State(uv - float2(t.x, 0)).r + State(uv + float2(0, t.y)).r + State(uv - float2(0, t.y)).r - 4 * c.r;
                float h = (2 * c.r - c.g + _Wave.x * lap) * _Wave.y;
                // Absorb at the edges of the moving window so waves leave instead of bouncing back.
                float edge = min(min(input.uv.x, 1 - input.uv.x), min(input.uv.y, 1 - input.uv.y)) * _MainTex_TexelSize.z;
                h *= lerp(0.85, 1, saturate(edge / max(_Wave.z, 1)));
                // Walls / banks: no water outside the rectangle (Dirichlet boundary reflects the waves).
                float2 world = _Area.xy + input.uv * _Area.z;
                if (_Wave.w > 0.5 && (world.x < _Rect.x || world.y < _Rect.y || world.x > _Rect.z || world.y > _Rect.w)) h = 0;
                return float4(clamp(h, -2, 2), c.r, 0, 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Disturb"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(Varyings input) : SV_Target
            {
                float2 s = State(input.uv);
                float2 world = _Area.xy + input.uv * _Area.z;
                for (int i = 0; i < _DropCount; i++)
                {
                    float2 d = world - _Drops[i].xy;
                    float r = max(_Drops[i].z, 0.01);
                    float q = dot(d, d) / (r * r);
                    // Gaussian dip with a slight raised rim: reads as a real ring once it propagates.
                    s.r += _Drops[i].w * exp(-q) * (1 - 0.35 * q);
                }
                return float4(s, 0, 0);
            }
            ENDHLSL
        }
    }
}
