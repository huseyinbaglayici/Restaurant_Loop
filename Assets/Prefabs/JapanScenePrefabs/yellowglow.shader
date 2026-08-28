Shader "RailSort/StrongYellowGlow"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [HDR] _GlowColor("Glow Color", Color) = (1, 0.45, 0.01, 1)
        _GlowIntensity("Glow Intensity", Range(0, 20)) = 7

        _RimPower("Rim Width", Range(0.5, 8)) = 2
        _RimStrength("Rim Strength", Range(0, 10)) = 5

        _PulseSpeed("Pulse Speed", Range(0, 10)) = 2
        _PulseAmount("Pulse Amount", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "YellowGlow"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _GlowColor;
                float _GlowIntensity;
                float _RimPower;
                float _RimStrength;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 textureColor =
                    SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                half3 baseColor = textureColor.rgb * _BaseColor.rgb;

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirection =
                    normalize(_WorldSpaceCameraPos.xyz - input.positionWS);

                float rim =
                    pow(1.0 - saturate(dot(normalWS, viewDirection)), _RimPower);

                float pulse =
                    1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                half3 generalGlow =
                    _GlowColor.rgb * _GlowIntensity * 0.35 * pulse;

                half3 rimGlow =
                    _GlowColor.rgb *
                    _GlowIntensity *
                    rim *
                    _RimStrength *
                    pulse;

                half3 finalColor = baseColor + generalGlow + rimGlow;

                return half4(finalColor, textureColor.a * _BaseColor.a);
            }

            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}