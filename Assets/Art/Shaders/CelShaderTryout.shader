// =====================================================================================
//  RestaurantSoftToon.shader
// -------------------------------------------------------------------------------------
//  A single "Soft Unlit Toon" master shader for the restaurant puzzle's environment,
//  wood, conveyor and characters. Not a flat Unlit, not a PBR Lit.
//
//  Final Color = BaseAlbedo * SoftToonLight * AO * VerticalGradient * ShadowAtten
//                then saturation / brightness / contrast, then optional rim.
//
//  VERIFIED AGAINST THIS PROJECT
//  =============================
//  Everything below was read out of the package actually installed here, not assumed:
//
//    Unity            6000.3.20f1
//    URP              com.unity.render-pipelines.universal 17.3.0
//    Renderer         Forward  (URP-Renderer m_RenderingMode: 0)
//    SRP Batcher      ON       (URP-Asset m_UseSRPBatcher: 1)
//    Main light       Per Pixel, shadows on, 4096 map, 4 cascades, distance 150
//    Soft shadows     ON, quality High
//    SSAO feature     ACTIVE on URP-Renderer, Source = DepthNormals
//
//  The active SSAO feature is why this shader ships a DepthNormals pass: with
//  Source = DepthNormals, URP renders a depth-normals prepass and any material without
//  that pass is simply absent from it, so SSAO would either miss these surfaces or
//  occlude them against stale normals. The forward pass also samples the SSAO result
//  through _SCREEN_SPACE_OCCLUSION so the feature actually reaches these materials.
//
//  NO META PASS, ON PURPOSE. URP 17.3.0 has no ShaderLibrary/UnityMetaPass.hlsl - it was
//  replaced by MetaInput.hlsl / UniversalMetaPass.hlsl. More importantly this shader is
//  fake-lit: it has no physical albedo response, so anything it contributed to a
//  lightmap bake would be wrong. There is no baked lighting on the game content in this
//  project. Rather than ship a half-true implementation, the pass is omitted. If baked
//  GI is added later, add a Meta pass using MetaInput.hlsl and feed it the raw
//  _BaseMap * _BaseColor product only.
//
//  Passes: UniversalForward / ShadowCaster / DepthOnly / DepthNormals
//
//  Unity 6.3 / URP 17.3.0 / Forward / Android + iOS.
// =====================================================================================

Shader "RestaurantLoop/RestaurantSoftToon"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClipToggle("Enable Alpha Clipping", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2

        [Header(Ambient Occlusion)]
        _AOMap("AO Map (R)", 2D) = "white" {}
        _AOStrength("AO Map Strength", Range(0,1)) = 0.2
        [Toggle(_VERTEXAO_ON)] _VertexAOToggle("Mesh Has Vertex AO (red channel)", Float) = 0
        _VertexAOIntensity("Vertex AO Intensity", Range(0,1)) = 0.0

        [Header(Soft Toon Ramp)]
        _ShadowTint("Shadow Tint", Color) = (0.62,0.55,0.60,1)
        _LightTint("Light Tint", Color) = (1.0,0.98,0.94,1)
        _ShadowThreshold("Shadow Threshold", Range(0,1)) = 0.28
        _LightThreshold("Light Threshold", Range(0,1)) = 0.82

        [Header(Light Direction)]
        _UseMainLightDirection("Use Main Light Direction", Range(0,1)) = 1.0
        _FakeLightDirection("Fake Light Direction", Vector) = (-0.4,0.8,-0.3,0)

        [Header(Realtime Shadows)]
        _ReceiveShadowStrength("Receive Shadow Strength", Range(0,1)) = 0.5
        _ShadowBrightness("Shadow Minimum Brightness", Range(0,1)) = 0.55

        [Header(Vertical Gradient)]
        _TopGradientColor("Top Gradient Color", Color) = (1.04,1.02,0.98,1)
        _BottomGradientColor("Bottom Gradient Color", Color) = (0.90,0.88,0.92,1)
        _GradientIntensity("Gradient Intensity", Range(0,1)) = 0.10
        _GradientStart("Gradient Start", Float) = -0.5
        _GradientEnd("Gradient End", Float) = 0.5
        [Enum(ObjectSpace,0,WorldSpace,1)] _GradientSpace("Gradient Space", Float) = 0

        [Header(Color Controls)]
        _Saturation("Saturation", Range(0,2)) = 1.0
        _Brightness("Brightness", Range(0,2)) = 1.0
        _Contrast("Contrast", Range(0.5,2)) = 1.0

        [Header(Rim    keep at zero for environment)]
        _RimColor("Rim Color", Color) = (1,0.97,0.92,1)
        _RimPower("Rim Power", Range(0.5,10)) = 4.0
        _RimIntensity("Rim Intensity", Range(0,0.3)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // =============================================================================
        //  PASS 1 - Universal Forward
        // =============================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            // target 3.0 keeps this inside the GLES3 / Vulkan / Metal feature set every
            // mobile device this ships to supports.
            #pragma target 3.0
            #pragma vertex SoftToonVertex
            #pragma fragment SoftToonFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _VERTEXAO_ON

            // Keyword set copied from URP 17.3.0's own Lit.shader so the variants line up
            // with what this project's URP Asset prefilters.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            // The SSAO renderer feature is active in this project, so the forward pass
            // has to consume its result or these materials would be the only ones in the
            // scene that ignore it.
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "RestaurantSoftToonCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float3 positionOS  : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                float4 color       : TEXCOORD5;
                float  fogCoord    : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings SoftToonVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);

                float3 normalOS = SafeNormalize3(input.normalOS, float3(0.0, 1.0, 0.0));
                // GetOddNegativeScale folds in mirrored / negatively scaled objects,
                // which otherwise shade inverted.
                float3 normalWS = TransformObjectToWorldNormal(normalOS) * GetOddNegativeScale();

                output.positionCS  = positionInputs.positionCS;
                output.positionWS  = positionInputs.positionWS;
                output.positionOS  = input.positionOS.xyz;
                output.normalWS    = SafeNormalize3(normalWS, float3(0.0, 1.0, 0.0));
                output.uv          = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color       = input.color;
                // GetShadowCoord internally picks the cascade or screen-space path
                // depending on which _MAIN_LIGHT_SHADOWS_* keyword is live.
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogCoord    = ComputeFogFactor(positionInputs.positionCS.z);

                return output;
            }

            half4 SoftToonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // ---- Albedo -----------------------------------------------------
                half4 baseSample = SampleBase(input.uv);
                half3 albedo = baseSample.rgb;
                half  alpha  = baseSample.a;
                ApplyAlphaClip(alpha);

                half3 normalWS = (half3)SafeNormalize3(input.normalWS, float3(0.0, 1.0, 0.0));

                // ---- Soft toon lighting ------------------------------------------
                float3 lightDirWS = GetToonLightDirection();
                half3  toonLight  = GetSoftToonLight(normalWS, lightDirWS);

                // ---- Realtime shadow, floored so it tints rather than blackens ----
                half shadowAtten = GetSoftShadowAttenuation(input.shadowCoord);

                // ---- Occlusion: material AO, optional vertex AO, and the SSAO
                //      renderer feature this project has enabled -----------------
                half occlusion = GetOcclusion(input.uv, input.color);

                #if defined(_SCREEN_SPACE_OCCLUSION)
                    float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    half ssao = SampleAmbientOcclusion(normalizedScreenSpaceUV);
                    // Floored for the same reason as the material AO: contact darkening,
                    // never a black crease.
                    occlusion *= max(ssao, 0.4h);
                #endif

                // ---- Compose ------------------------------------------------------
                half3 color = albedo * toonLight * occlusion * shadowAtten;

                color = ApplyVerticalGradient(color, input.positionOS, input.positionWS);
                color = ApplyColorControls(color);

                // Rim last so the colour controls cannot amplify it into a white halo.
                color = ApplyRim(color, normalWS, (half3)SafeNormalize3(
                    GetWorldSpaceViewDir(input.positionWS), float3(0.0, 0.0, 1.0)));

                color = MixFog(color, input.fogCoord);

                // Opaque: alpha is written as 1 so a stray texture alpha cannot interact
                // with anything downstream.
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 2 - Shadow Caster
        // =============================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "RestaurantSoftToonCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Set by URP's shadow passes before each draw.
            float3 _LightDirection;
            float3 _LightPosition;

            struct AttributesShadow
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsShadow
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsShadow ShadowVertex(AttributesShadow input)
            {
                VaryingsShadow output = (VaryingsShadow)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 normalOS   = SafeNormalize3(input.normalOS, float3(0.0, 1.0, 0.0));
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = SafeNormalize3(TransformObjectToWorldNormal(normalOS),
                                                   float3(0.0, 1.0, 0.0));

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = SafeNormalize3(_LightPosition - positionWS,
                                                             float3(0.0, 1.0, 0.0));
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                // Clamp to the near plane so geometry behind the shadow camera does not
                // punch holes in the shadow map.
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFragment(VaryingsShadow input) : SV_Target
            {
                ApplyAlphaClip(SampleBase(input.uv).a);
                return 0;
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 3 - Depth Only
        // =============================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "RestaurantSoftToonCommon.hlsl"

            struct AttributesDepth
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDepth
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsDepth DepthOnlyVertex(AttributesDepth input)
            {
                VaryingsDepth output = (VaryingsDepth)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthOnlyFragment(VaryingsDepth input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ApplyAlphaClip(SampleBase(input.uv).a);
                return 0;
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 4 - Depth Normals
        //
        //  Required because this project's SSAO renderer feature is set to
        //  Source = DepthNormals. URP 17.3.0's DepthNormalOnlyPass collects both the
        //  "DepthNormals" and "DepthNormalsOnly" tags; "DepthNormals" is the one URP's
        //  own Lit shader uses and is what is used here.
        // =============================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "RestaurantSoftToonCommon.hlsl"

            struct AttributesDepthNormals
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDepthNormals
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsDepthNormals DepthNormalsVertex(AttributesDepthNormals input)
            {
                VaryingsDepthNormals output = (VaryingsDepthNormals)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 normalOS = SafeNormalize3(input.normalOS, float3(0.0, 1.0, 0.0));

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = SafeNormalize3(
                    TransformObjectToWorldNormal(normalOS) * GetOddNegativeScale(),
                    float3(0.0, 1.0, 0.0));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthNormalsFragment(VaryingsDepthNormals input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ApplyAlphaClip(SampleBase(input.uv).a);

                float3 normalWS = SafeNormalize3(input.normalWS, float3(0.0, 1.0, 0.0));
                return half4((half3)normalWS, 0.0h);
            }
            ENDHLSL
        }
    }

    // No custom editor and no Fallback. A Fallback would silently swap in a lit shader
    // if this one ever failed to compile, which hides the error instead of showing it.
}
