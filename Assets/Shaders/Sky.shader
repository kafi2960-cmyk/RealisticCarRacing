// Daylight sky for the circuit: horizon-to-zenith gradient, sun disc with a forward-scattering halo, and a
// cloud layer projected onto a flat sky plane from one tiling noise texture (two samples + one towards the sun
// for self-shadowing). Cheap enough for phones: the sky only shades pixels nothing else covers, with three
// texture reads and no loops. Also feeds the ambient light and the reflection probes when they are baked.
Shader "RealisticCarRacing/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.16, 0.33, 0.66, 1)
        _HorizonColor ("Horizon", Color) = (0.62, 0.74, 0.86, 1)
        _GroundColor ("Below horizon", Color) = (0.38, 0.4, 0.38, 1)
        _SunDirection ("Direction towards the sun", Vector) = (0, 0.6, 0.8, 0)
        _SunColor ("Sun colour", Color) = (1, 0.95, 0.86, 1)
        _SunSize ("Sun disc (cosine of radius)", Range(0.999, 0.99999)) = 0.99985
        _SunIntensity ("Sun disc intensity (HDR)", Float) = 40
        _HaloIntensity ("Halo intensity", Float) = 0.35
        _HaloPower ("Halo tightness", Float) = 12
        _CloudTex ("Cloud noise (R)", 2D) = "black" {}
        _CloudScale ("Cloud scale", Float) = 0.35
        _CloudCoverage ("Cloud coverage", Range(0, 1)) = 0.42
        _CloudSoftness ("Cloud softness", Range(0.01, 0.5)) = 0.16
        _CloudLitColor ("Cloud lit colour", Color) = (1, 1, 1, 1)
        _CloudShadeColor ("Cloud shaded colour", Color) = (0.58, 0.63, 0.7, 1)
        _Exposure ("Exposure", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor, _CloudLitColor, _CloudShadeColor;
                float4 _SunDirection;
                float _SunSize, _SunIntensity, _HaloIntensity, _HaloPower;
                float _CloudScale, _CloudCoverage, _CloudSoftness, _Exposure;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz; // the skybox mesh is centred on the camera
                return output;
            }

            half Clouds(float2 uv)
            {
                half a = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv).r;
                half b = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv * 2.7 + 0.37).r;
                return a * 0.68 + b * 0.32;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float3 sun = normalize(_SunDirection.xyz);
                float up = saturate(d.y);
                float cosSun = dot(d, sun);

                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, 0.5));
                sky = lerp(sky, _GroundColor.rgb, smoothstep(0.0, -0.06, d.y));

                // Forward scattering: a soft halo round the sun and a warmer, brighter horizon on the sun's side.
                half towardsSun = saturate(cosSun * 0.5 + 0.5);
                sky += _SunColor.rgb * (pow(saturate(cosSun), _HaloPower) * _HaloIntensity
                                        + pow(1.0 - up, 5.0) * pow(towardsSun, 3.0) * 0.18);
                half disc = smoothstep(_SunSize, _SunSize + (1.0 - _SunSize) * 0.25, cosSun);

                if (d.y > 0.0)
                {
                    // Flat cloud deck: directions near the horizon reach far across it, so clouds shrink towards it.
                    float2 uv = d.xz / (d.y + 0.1) * _CloudScale;
                    half density = Clouds(uv);
                    half edge = 1.0 - _CloudCoverage;
                    half cover = smoothstep(edge - _CloudSoftness, edge + _CloudSoftness, density);
                    // Thicker cloud between this point and the sun means this side is in shade.
                    half towards = Clouds(uv + sun.xz * 0.05);
                    half lit = saturate(0.6 + (density - towards) * 3.0);
                    half3 cloud = lerp(_CloudShadeColor.rgb, _CloudLitColor.rgb * _SunColor.rgb, lit);
                    cloud += _SunColor.rgb * pow(saturate(cosSun), 8.0) * (1.0 - cover) * 0.8; // bright thin edges near the sun
                    cover *= smoothstep(0.0, 0.2, d.y); // fades into the haze at the horizon
                    sky = lerp(sky, cloud, cover * 0.95);
                    disc *= 1.0 - cover;
                }

                sky += _SunColor.rgb * disc * _SunIntensity;
                return half4(sky * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
