// =====================================================================================
//  RestaurantLoop / SoftMatte
// -------------------------------------------------------------------------------------
//  A flat, matte, outline-free stylised shader for a top-down casual restaurant game.
//
//  WHAT THE REFERENCE IMAGE ACTUALLY SHOWS
//  =======================================
//  Reading the screenshot rather than guessing at it:
//
//   * The value steps come from the GEOMETRY, not from a lighting ramp. The wood frame's
//     top faces are light, its side faces are darker, and each individual face is almost
//     perfectly flat in tone. There is no banding anywhere across a curved surface.
//     That is a hemisphere (sky/ground) ambient term, not a hard toon ramp. A strong
//     ramp would band across the rounded corners of the conveyor, and the reference has
//     none of that - which is why this shader's ramp is deliberately very wide and soft
//     and the hemisphere term does most of the work.
//
//   * Up-facing surfaces are brighter regardless of where the key light is. The belt and
//     the wood tops both read light; the vertical wood sides read warm-dark. That is a
//     sky term keyed off normalWS.y, and it is the single most important part of this
//     look for a fixed top-down camera.
//
//   * Nothing is shiny. No specular, no fresnel rim, no gloss lobe, no outline pass.
//     Completely matte.
//
//   * Dark areas are never black. The belt is a warm, slightly purple charcoal and still
//     clearly holds its hue. _ShadowTint and _GroundColor are what keep it there.
//
//   * The curved tread lines on the belt and the wood grain are ALBEDO TEXTURE, not
//     shader work. Nothing here tries to generate them.
//
//   * The soft dark pools under the characters are separate blob-shadow meshes, not this
//     shader. See the note at the bottom of this file.
//
//  BUILT FOR RELIABILITY
//  =====================
//  A previous attempt came back as the magenta error material, so this one is written to
//  minimise every way that can happen:
//
//   * ONE file. No companion .hlsl, so there is no way to copy half of it.
//   * Only Core.hlsl, Lighting.hlsl and Shadows.hlsl are included - the three URP
//     headers that have existed under those exact paths for many versions.
//   * No Meta pass. URP 17 removed ShaderLibrary/UnityMetaPass.hlsl, and including it is
//     what breaks a shader on Unity 6.3.
//   * No SSAO sampling, no screen-space UVs, no MatCap, no triplanar, no procedural
//     noise. Nothing exotic to fail.
//   * No Fallback. If it ever does fail you get magenta AND a real error in the Console,
//     instead of Unity silently swapping in a lit shader and hiding the cause.
//
//  Final colour = Albedo x SoftDirectional x Hemisphere x AO x ShadowAtten,
//                 then saturation / brightness.
//
//  Passes: UniversalForward / ShadowCaster / DepthOnly / DepthNormals
//  Unity 6.x / URP 14-17 / Forward / Android + iOS.
// =====================================================================================

Shader "RestaurantLoop/SoftMatte"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClipToggle("Enable Alpha Clipping", Float) = 0
        [Toggle(_VERTEXCOLOR_ON)] _VertexColorToggle("Mesh Has Vertex Colors", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2

        [Header(Vertex Animation Texture)]
        [Toggle(_VAT_ON)] _VATToggle("VAT Enabled", Float) = 0
        _VATPositions("VAT Position Texture", 2D) = "black" {}
        _VATTexHeight("VAT Texture Height (Total Frames)", Float) = 1

        [Header(Hemisphere    the top down look)]
        // The main volume cue. Up-facing surfaces take _SkyColor, down-facing surfaces
        // take _GroundColor, and every flat face lands on one value. Both stay near 1.0
        // because they multiply the albedo - they tint, they do not light.
        _SkyColor("Sky Color (up facing)", Color) = (1.06,1.03,0.98,1)
        _GroundColor("Ground Color (down facing)", Color) = (0.62,0.56,0.58,1)
        _HemisphereWrap("Hemisphere Softness", Range(0.01,1)) = 0.85

        [Header(Soft Directional    keep it gentle)]
        _LightTint("Light Tint", Color) = (1.0,0.98,0.94,1)
        _ShadowTint("Shadow Tint", Color) = (0.72,0.66,0.70,1)
        // A WIDE gap between these two is what keeps curved surfaces from banding.
        // Bringing them close together turns this into hard cel shading - do not.
        _ShadowThreshold("Shadow Threshold", Range(0,1)) = 0.12
        _LightThreshold("Light Threshold", Range(0,1)) = 0.95
        _DirectionalStrength("Directional Strength", Range(0,1)) = 0.55

        [Header(Light Direction)]
        // 0 = use the fixed direction below, 1 = follow the scene's main light.
        // Fixed is the default: on a locked top-down camera it keeps every prop reading
        // identically no matter how the scene light is aimed for shadow casting.
        _UseMainLightDirection("Use Scene Light Direction", Range(0,1)) = 0.0
        _FakeLightDirection("Fixed Light Direction", Vector) = (-0.35,0.85,-0.4,0)

        [Header(Realtime Shadows)]
        _ReceiveShadowStrength("Receive Shadow Strength", Range(0,1)) = 0.35
        _ShadowBrightness("Shadow Minimum Brightness", Range(0,1)) = 0.62
        _ShadowColorTint("Received Shadow Tint", Color) = (0.70,0.62,0.66,1)

        [Header(Ambient Occlusion)]
        _AOMap("AO Map (R)", 2D) = "white" {}
        _AOStrength("AO Strength", Range(0,1)) = 0.25
        _AOFloor("AO Minimum", Range(0,1)) = 0.45

        [Header(Vertical Gradient    subtle)]
        _TopGradientColor("Top Gradient", Color) = (1.03,1.01,0.99,1)
        _BottomGradientColor("Bottom Gradient", Color) = (0.92,0.89,0.91,1)
        _GradientIntensity("Gradient Intensity", Range(0,1)) = 0.10
        _GradientStart("Gradient Start", Float) = -0.5
        _GradientEnd("Gradient End", Float) = 0.5
        [Enum(ObjectSpace,0,WorldSpace,1)] _GradientSpace("Gradient Space", Float) = 0

        [Header(Colour)]
        _Saturation("Saturation", Range(0,2)) = 0.95
        _Brightness("Brightness", Range(0,2)) = 1.0
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
        LOD 200

        // =============================================================================
        //  Shared material data. Declared once here and reused by every pass, so the
        //  UnityPerMaterial layout is identical across them - which is what SRP Batcher
        //  compatibility requires. Textures and samplers stay outside the CBUFFER.
        // =============================================================================
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Cutoff;
            half   _Cull;
            half   _AlphaClipToggle;

            half4 _SkyColor;
            half4 _GroundColor;
            half  _HemisphereWrap;

            half4 _LightTint;
            half4 _ShadowTint;
            half  _ShadowThreshold;
            half  _LightThreshold;
            half  _DirectionalStrength;

            half   _UseMainLightDirection;
            float4 _FakeLightDirection;

            half  _ReceiveShadowStrength;
            half  _ShadowBrightness;
            half4 _ShadowColorTint;

            half _AOStrength;
            half _AOFloor;

            half4 _TopGradientColor;
            half4 _BottomGradientColor;
            half  _GradientIntensity;
            half  _GradientStart;
            half  _GradientEnd;
            half  _GradientSpace;

            half _Saturation;
            half _Brightness;

            #if defined(_VAT_ON)
                float _VATTexHeight;
            #endif
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_AOMap);   SAMPLER(sampler_AOMap);

        // Per-instance playback state written by VATCustomerVisual via MaterialPropertyBlock —
        // UNITY_INSTANCING_BUFFER keeps it batchable across GPU-instanced draws instead of forcing
        // a draw call per customer. Position-only VAT: normals stay bind-pose (see VATBaker).
        #if defined(_VAT_ON)
            TEXTURE2D(_VATPositions);
            SAMPLER(sampler_VATPositions);

            UNITY_INSTANCING_BUFFER_START(SoftMatteV1VATProps)
                UNITY_DEFINE_INSTANCED_PROP(float, _VATFrameA)
                UNITY_DEFINE_INSTANCED_PROP(float, _VATFrameB)
                UNITY_DEFINE_INSTANCED_PROP(float, _VATBlend)
            UNITY_INSTANCING_BUFFER_END(SoftMatteV1VATProps)
        #endif

        float3 ApplyVAT(float3 positionOS, float2 vatUV)
        {
            #if defined(_VAT_ON)
                float frameA = UNITY_ACCESS_INSTANCED_PROP(SoftMatteV1VATProps, _VATFrameA);
                float frameB = UNITY_ACCESS_INSTANCED_PROP(SoftMatteV1VATProps, _VATFrameB);
                float blend = UNITY_ACCESS_INSTANCED_PROP(SoftMatteV1VATProps, _VATBlend);
                float vA = (frameA + 0.5) / _VATTexHeight;
                float vB = (frameB + 0.5) / _VATTexHeight;
                float3 posA = SAMPLE_TEXTURE2D_LOD(_VATPositions, sampler_VATPositions, float2(vatUV.x, vA), 0).xyz;
                float3 posB = SAMPLE_TEXTURE2D_LOD(_VATPositions, sampler_VATPositions, float2(vatUV.x, vB), 0).xyz;
                return lerp(posA, posB, blend);
            #else
                return positionOS;
            #endif
        }

        // A mesh imported with normals disabled hands the shader a zero vector, and
        // normalize(0) is NaN - one NaN normal poisons everything downstream and the
        // model renders as garbage. Every normalize goes through this.
        float3 SafeNormal(float3 v, float3 fallback)
        {
            float lenSq = dot(v, v);
            return (lenSq > 1e-8) ? v * rsqrt(lenSq) : fallback;
        }

        void ClipAlpha(half alpha)
        {
            #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
            #endif
        }

        half4 SampleBaseMap(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
        }
        ENDHLSL

        // =============================================================================
        //  PASS 1 - Forward
        // =============================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _VERTEXCOLOR_ON
            #pragma shader_feature_local _VAT_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                #if defined(_VAT_ON)
                    float2 vatUV : TEXCOORD1;
                #endif
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
                float  fogCoord    : TEXCOORD5;
                float4 color       : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 animatedOS = input.positionOS.xyz;
                #if defined(_VAT_ON)
                    animatedOS = ApplyVAT(animatedOS, input.vatUV);
                #endif

                VertexPositionInputs positionInputs = GetVertexPositionInputs(animatedOS);
                float3 normalOS = SafeNormal(input.normalOS, float3(0.0, 1.0, 0.0));

                output.positionCS  = positionInputs.positionCS;
                output.positionWS  = positionInputs.positionWS;
                output.positionOS  = animatedOS;
                output.normalWS    = SafeNormal(TransformObjectToWorldNormal(normalOS), float3(0.0, 1.0, 0.0));
                output.uv          = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogCoord    = ComputeFogFactor(positionInputs.positionCS.z);
                output.color       = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // ---- Albedo -------------------------------------------------------
                half4 baseSample = SampleBaseMap(input.uv);
                half3 albedo = baseSample.rgb;
                #if defined(_VERTEXCOLOR_ON)
                    albedo *= input.color.rgb;
                #endif
                ClipAlpha(baseSample.a);

                half3 normalWS = (half3)SafeNormal(input.normalWS, float3(0.0, 1.0, 0.0));

                // ---- Hemisphere ---------------------------------------------------
                //  The heart of the look. normalWS.y remapped to 0..1 picks between the
                //  ground and sky tint, so a flat top face lands on one uniform value
                //  and a flat side face lands on another. That face-to-face value
                //  difference is what reads as volume in the reference, and because it
                //  is a plain lerp there is nothing to band on curved geometry.
                half up = normalWS.y * 0.5h + 0.5h;
                up = saturate(lerp(step(0.5h, up), up, _HemisphereWrap));
                half3 hemisphere = lerp(_GroundColor.rgb, _SkyColor.rgb, up);

                // ---- Soft directional ---------------------------------------------
                //  Deliberately gentle. Half-lambert keeps surfaces facing away from the
                //  light from ever reaching zero, and the wide threshold gap keeps the
                //  transition from turning into a cel band.
                float3 fixedDir = SafeNormal(_FakeLightDirection.xyz, float3(-0.35, 0.85, -0.4));
                float3 lightDir = SafeNormal(
                    lerp(fixedDir, _MainLightPosition.xyz, saturate(_UseMainLightDirection)),
                    fixedDir);

                half ndl = dot(normalWS, (half3)lightDir);
                half halfLambert = ndl * 0.5h + 0.5h;

                half lo = min(_ShadowThreshold, _LightThreshold - 1e-3h);
                half ramp = smoothstep(lo, _LightThreshold, halfLambert);
                half3 directional = lerp(_ShadowTint.rgb, _LightTint.rgb, ramp);

                // Blend toward plain white as _DirectionalStrength drops, so the
                // hemisphere term can be left to carry the whole look if wanted.
                directional = lerp(half3(1.0h, 1.0h, 1.0h), directional, _DirectionalStrength);

                // ---- Realtime shadow ----------------------------------------------
                //  Tinted and floored. A cast shadow here darkens and cools a surface;
                //  it never drives it toward black.
                Light mainLight = GetMainLight(input.shadowCoord);
                half atten = lerp(1.0h, mainLight.shadowAttenuation, saturate(_ReceiveShadowStrength));
                atten = max(atten, _ShadowBrightness);
                half3 shadowTerm = lerp(_ShadowColorTint.rgb, half3(1.0h, 1.0h, 1.0h), atten);

                // ---- Ambient occlusion --------------------------------------------
                //  A white AO map with any strength darkens nothing, and the floor stops
                //  creases from going to black - AO is a contact cue, not a light sim.
                half texAO = SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, input.uv).r;
                half ao = max(lerp(1.0h, texAO, _AOStrength), _AOFloor);

                // ---- Compose ------------------------------------------------------
                half3 color = albedo * hemisphere * directional * shadowTerm * ao;

                // ---- Vertical gradient --------------------------------------------
                //  Object space by default so it is immune to where the prop sits in the
                //  world. The denominator is guarded so a start == end material cannot
                //  divide by zero, and t is clamped so odd object scales cannot push the
                //  gradient past its ends.
                float height = (_GradientSpace < 0.5h) ? input.positionOS.y : input.positionWS.y;
                float range  = _GradientEnd - _GradientStart;
                float denom  = (abs(range) < 1e-4) ? 1e-4 : range;
                half  gradT  = saturate((half)((height - _GradientStart) / denom));
                half3 gradient = lerp(_BottomGradientColor.rgb, _TopGradientColor.rgb, gradT);
                color = lerp(color, color * gradient, _GradientIntensity);

                // ---- Colour -------------------------------------------------------
                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                color = lerp(luminance.xxx, color, _Saturation) * _Brightness;

                color = MixFog(color, input.fogCoord);

                // Opaque, so alpha is forced to 1 and a stray texture alpha cannot
                // interact with anything downstream.
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
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _VAT_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            // Set by URP's shadow rendering before each draw.
            float3 _LightDirection;
            float3 _LightPosition;

            struct AttributesShadow
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                #if defined(_VAT_ON)
                    float2 vatUV : TEXCOORD1;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsShadow
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsShadow ShadowVert(AttributesShadow input)
            {
                VaryingsShadow output = (VaryingsShadow)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 animatedOS = input.positionOS.xyz;
                #if defined(_VAT_ON)
                    animatedOS = ApplyVAT(animatedOS, input.vatUV);
                #endif

                float3 normalOS   = SafeNormal(input.normalOS, float3(0.0, 1.0, 0.0));
                float3 positionWS = TransformObjectToWorld(animatedOS);
                float3 normalWS   = SafeNormal(TransformObjectToWorldNormal(normalOS),
                                               float3(0.0, 1.0, 0.0));

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = SafeNormal(_LightPosition - positionWS,
                                                         float3(0.0, 1.0, 0.0));
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                // Clamp to the near plane so geometry behind the shadow camera cannot
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

            half4 ShadowFrag(VaryingsShadow input) : SV_Target
            {
                ClipAlpha(SampleBaseMap(input.uv).a);
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
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _VAT_ON
            #pragma multi_compile_instancing

            struct AttributesDepth
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                #if defined(_VAT_ON)
                    float2 vatUV : TEXCOORD1;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDepth
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsDepth DepthVert(AttributesDepth input)
            {
                VaryingsDepth output = (VaryingsDepth)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 animatedOS = input.positionOS.xyz;
                #if defined(_VAT_ON)
                    animatedOS = ApplyVAT(animatedOS, input.vatUV);
                #endif

                output.positionCS = TransformObjectToHClip(animatedOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthFrag(VaryingsDepth input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipAlpha(SampleBaseMap(input.uv).a);
                return 0;
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 4 - Depth Normals
        //
        //  Present because this project's renderer has the SSAO feature enabled with its
        //  source set to DepthNormals. URP renders a depth-normals prepass for that, and
        //  a material with no such pass is simply absent from it.
        // =============================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _VAT_ON
            #pragma multi_compile_instancing

            struct AttributesDN
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                #if defined(_VAT_ON)
                    float2 vatUV : TEXCOORD1;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDN
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsDN DepthNormalsVert(AttributesDN input)
            {
                VaryingsDN output = (VaryingsDN)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 animatedOS = input.positionOS.xyz;
                #if defined(_VAT_ON)
                    animatedOS = ApplyVAT(animatedOS, input.vatUV);
                #endif

                float3 normalOS = SafeNormal(input.normalOS, float3(0.0, 1.0, 0.0));

                output.positionCS = TransformObjectToHClip(animatedOS);
                output.normalWS   = SafeNormal(TransformObjectToWorldNormal(normalOS),
                                               float3(0.0, 1.0, 0.0));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthNormalsFrag(VaryingsDN input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipAlpha(SampleBaseMap(input.uv).a);
                return half4((half3)SafeNormal(input.normalWS, float3(0.0, 1.0, 0.0)), 0.0h);
            }
            ENDHLSL
        }
    }

    // No Fallback on purpose. A Fallback would silently swap in a lit shader if this one
    // ever failed to compile, hiding the real error behind a plausible-looking result.
    // Magenta plus a Console error is the more useful failure.
}

// =====================================================================================
//  THE PARTS OF THE REFERENCE THIS SHADER DOES NOT DO, AND SHOULD NOT
// -------------------------------------------------------------------------------------
//  * The curved tread lines on the belt and the wood grain are painted into the albedo
//    texture. Generating them procedurally would make them swim as props move along the
//    conveyor and would cost far more than a texture lookup.
//
//  * The soft dark pools under the characters are blob shadows: a separate quad per
//    character, slightly wider than the character, sitting 0.01-0.02 units above the
//    belt to avoid z-fighting, with a radial-gradient texture, unlit transparent
//    material, ~30% opacity, tinted warm dark brown rather than black. That reads far
//    better on a fixed top-down camera than a realtime shadow and costs almost nothing.
//
//  * The overall darkening toward the edges of the screenshot is post-processing
//    vignette on the camera Volume, not per-material.
// =====================================================================================
