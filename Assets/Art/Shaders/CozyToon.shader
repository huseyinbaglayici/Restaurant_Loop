Shader "Custom/CozyToon"
{
    Properties
    {
        [Header(Base Properties)]
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Toon Shading Bands)]
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSmoothness ("Shadow Sharpness (0=Crisp Hard Band)", Range(0.001, 0.3)) = 0.01
        _ShadowColor ("Shadow Tint Color", Color) = (0.6, 0.55, 0.7, 1)

        [Header(Hand Drawn Outline)]
        [Toggle(_ENABLE_OUTLINE)] _EnableOutline ("Enable Outline", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.1, 0.1, 0.15, 1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.08)) = 0.015
        _OutlineWiggle ("Hand-Drawn Wiggle Frequency", Range(0, 50)) = 15.0
        _OutlineJitter ("Hand-Drawn Jitter Amplitude", Range(0, 0.03)) = 0.004

        [Header(Rim Lighting (Edge Highlight))]
        [Toggle(_RIM_LIGHT)] _EnableRim ("Enable Rim Light", Float) = 1
        _RimColor ("Rim Light Color", Color) = (1, 0.95, 0.8, 1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.5
        _RimSmoothness ("Rim Sharpness", Range(0.001, 0.3)) = 0.02

        [Header(Specular Highlight)]
        [Toggle(_SPECULAR_ON)] _EnableSpecular ("Enable Specular", Float) = 1
        _SpecColor ("Specular Color", Color) = (1, 1, 1, 0.6)
        _Glossiness ("Glossiness / Shininess", Range(8, 128)) = 32
        _SpecSmoothness ("Specular Sharpness", Range(0.001, 0.2)) = 0.01
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry" 
        }

        // ==========================================
        // PASS 1: Hand-Drawn Inverted Hull Outline
        // ==========================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _OutlineColor;
                float4 _RimColor;
                float4 _SpecColor;
                float _ShadowThreshold;
                float _ShadowSmoothness;
                float _EnableOutline;
                float _OutlineWidth;
                float _OutlineWiggle;
                float _OutlineJitter;
                float _EnableRim;
                float _RimPower;
                float _RimSmoothness;
                float _EnableSpecular;
                float _Glossiness;
                float _SpecSmoothness;
            CBUFFER_END

            // El çizimi hissi için psödo-gürültü dalga fonksiyonu
            float GetHandDrawnJitter(float3 posOS)
            {
                float wave1 = sin(posOS.x * _OutlineWiggle + posOS.y * _OutlineWiggle * 1.3 + posOS.z * _OutlineWiggle * 0.7);
                float wave2 = cos(posOS.y * _OutlineWiggle * 1.7 - posOS.z * _OutlineWiggle);
                return (wave1 + wave2 * 0.5) * _OutlineJitter;
            }

            Varyings vertOutline(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                if (_EnableOutline < 0.5 || _OutlineWidth <= 0.0001)
                {
                    output.positionCS = float4(0, 0, 0, 0);
                    return output;
                }

                // El çizimi dalgalanması ekle
                float jitter = GetHandDrawnJitter(input.positionOS.xyz);
                float totalWidth = _OutlineWidth + jitter;

                // Vertex normali yönünde genişleterek el çizimi hisli dış hat oluştur
                float3 extrudedPosOS = input.positionOS.xyz + normalize(input.normalOS) * totalWidth;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(extrudedPosOS);

                output.positionCS = vertexInput.positionCS;
                return output;
            }

            half4 fragOutline(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ==========================================
        // PASS 2: Sharp Cel / Toon Shading (UniversalForward)
        // ==========================================
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 viewDirWS    : TEXCOORD2;
                float3 positionWS   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _OutlineColor;
                float4 _RimColor;
                float4 _SpecColor;
                float _ShadowThreshold;
                float _ShadowSmoothness;
                float _EnableOutline;
                float _OutlineWidth;
                float _OutlineWiggle;
                float _OutlineJitter;
                float _EnableRim;
                float _RimPower;
                float _RimSmoothness;
                float _EnableSpecular;
                float _Glossiness;
                float _SpecSmoothness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalize(normalInput.normalWS);
                output.viewDirWS = normalize(GetCameraPositionWS() - vertexInput.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 mainTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float4 albedo = mainTex * _BaseColor;

                // Ana Işık Bilgileri
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDir = normalize(mainLight.direction);
                float3 lightColor = mainLight.color;
                float shadowAtten = mainLight.shadowAttenuation;

                // N dot L (Işık Açısı)
                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);
                float NdotL = dot(N, lightDir);
                float halfLambert = NdotL * 0.5 + 0.5;

                // Keskin Toon Cel Gölgelendirme (Sharp Cel Ramp)
                float lightIntensity = halfLambert * shadowAtten;
                float sSmooth = max(_ShadowSmoothness, 0.001);
                float toonRamp = smoothstep(_ShadowThreshold - sSmooth, _ShadowThreshold + sSmooth, lightIntensity);
                
                // Gölgeli ve Işıklı alan renk geçişi
                float3 diffuseLighting = lerp(_ShadowColor.rgb, lightColor, toonRamp);

                // Ortam Işığı
                float3 ambient = SampleSH(N) * 0.4;
                float3 finalColor = albedo.rgb * (diffuseLighting + ambient);

                // 1. Keskin Rim Light (Kenar Işığı)
                if (_EnableRim > 0.5)
                {
                    float NdotV = 1.0 - saturate(dot(N, V));
                    float rim = pow(NdotV, _RimPower);
                    float rSmooth = max(_RimSmoothness, 0.001);
                    float rimIntensity = smoothstep(0.5 - rSmooth, 0.5 + rSmooth, rim) * toonRamp;
                    finalColor += _RimColor.rgb * rimIntensity * _RimColor.a;
                }

                // 2. Keskin Çizgi Film Parlaması (Sharp Specular Highlight)
                if (_EnableSpecular > 0.5)
                {
                    float3 H = normalize(lightDir + V);
                    float NdotH = saturate(dot(N, H));
                    float spec = pow(NdotH, _Glossiness);
                    float spSmooth = max(_SpecSmoothness, 0.001);
                    float specIntensity = smoothstep(0.5 - spSmooth, 0.5 + spSmooth, spec) * shadowAtten;
                    finalColor += _SpecColor.rgb * specIntensity * _SpecColor.a;
                }

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // ==========================================
        // PASS 3: ShadowCaster Pass
        // ==========================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
