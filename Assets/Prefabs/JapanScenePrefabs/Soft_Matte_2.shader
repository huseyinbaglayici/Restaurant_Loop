// =====================================================================================
//  RestaurantLoop / SoftMatte                                                    v2
// -------------------------------------------------------------------------------------
//  Flat, matte, outline-free stylised shading for a top-down casual restaurant game.
//
//  WHAT v2 ADDS
//  ============
//   1. ROUNDED CORNERS (_Roundness). The mesh normal is blended toward the direction
//      from a pivot out to the vertex, so a hard-edged low-poly box shades like a
//      rounded one. Nothing about the mesh changes - only how light reads across it -
//      but it is the cheapest way to make blocky props look softened.
//
//   2. FAKE SELF-LIGHT AND SELF-SHADOW (_CreaseShadow / _EdgeLight). The surface's own
//      curvature is measured from screen-space derivatives:
//
//          curvature = (ddx(N).ddx(P) + ddy(N).ddy(P)) / (|ddx(P)|^2 + |ddy(P)|^2)
//
//      On a sphere of radius r this evaluates to 1/r, so it is real curvature in world
//      units, not a screen-resolution artefact. Positive means convex (an outer edge),
//      negative means concave (a crease). Creases get a touch of shadow, outer edges get
//      a touch of light. No lights, no AO map, no UVs needed - the geometry shades
//      itself. This is the "give it a tiny bit of its own light and shadow" part.
//
//   3. MORE TOON (_ToonSteps). The soft ramp is now optionally quantised into discrete
//      value steps with soft borders. 1 = the old fully smooth behaviour, 3 = a clear
//      but still soft toon read, 5+ = approaching cel. The step borders stay soft, so
//      this never becomes hard anime banding.
//
//  WHAT THE REFERENCE IMAGE SHOWS, AND WHY THIS IS BUILT THIS WAY
//  =============================================================
//   * Value steps come from the GEOMETRY, not from a lighting ramp. Top faces light,
//     side faces darker, each face almost flat in tone, no banding on curves. That is a
//     hemisphere sky/ground term. The directional ramp stays gentle underneath it.
//   * Up-facing surfaces read brighter regardless of the key light - a sky term keyed
//     off normalWS.y, which is the most important part of the look on a fixed top-down
//     camera.
//   * Nothing is shiny. No specular, no fresnel rim, no gloss, no outline.
//   * Dark areas are never black. The belt keeps its warm purple-charcoal hue.
//   * Belt tread arcs and wood grain are albedo texture, or the companion wood shader.
//
//  BUILT FOR RELIABILITY
//  =====================
//   * ONE file, no companion .hlsl.
//   * Three includes only: Core.hlsl, Lighting.hlsl, Shadows.hlsl.
//   * No Meta pass - URP 17 removed ShaderLibrary/UnityMetaPass.hlsl, and including it
//     is what turns a shader magenta on Unity 6.3.
//   * No Fallback, so a failure shows as magenta plus a real Console error rather than
//     being hidden behind a silently substituted lit shader.
//   * Every normalize is NaN-guarded, so a mesh imported without normals shades flat
//     instead of rendering as garbage.
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
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2

        [Header(Rounded Corners)]
        // Blends the mesh normal toward the direction from the pivot out to the vertex.
        // 0 = the mesh's own shading, 1 = shades exactly like a sphere.
        // If a prop's origin is not in the middle of its mesh (common on Blender
        // exports), set _RoundPivot to the mesh's bounds centre or the puff goes lopsided.
        _Roundness("Roundness", Range(0,1)) = 0.30
        _RoundPivot("Round Pivot (Object Space)", Vector) = (0,0,0,0)

        [Header(Hemisphere    the top down look)]
        _SkyColor("Sky Color (up facing)", Color) = (1.06,1.03,0.98,1)
        _GroundColor("Ground Color (down facing)", Color) = (0.62,0.56,0.58,1)
        _HemisphereWrap("Hemisphere Softness", Range(0.01,1)) = 0.92

        [Header(Soft Directional)]
        _LightTint("Light Tint", Color) = (1.0,0.98,0.94,1)
        _ShadowTint("Shadow Tint", Color) = (0.72,0.66,0.70,1)
        // A wide gap between these is what keeps curved surfaces from banding.
        _ShadowThreshold("Shadow Threshold", Range(0,1)) = 0.12
        _LightThreshold("Light Threshold", Range(0,1)) = 0.95
        _DirectionalStrength("Directional Strength", Range(0,1)) = 0.55

        [Header(Toon Stepping)]
        // 1 = fully smooth. 3 = a clear but soft toon read. Borders stay soft at any
        // count, so this never becomes hard cel banding.
        _ToonSteps("Toon Steps", Range(1,6)) = 3
        _ToonStepSoftness("Toon Step Softness", Range(0.05,1)) = 0.55

        [Header(Fake Self Light and Shadow    from curvature)]
        _CurvatureScale("Curvature Scale", Range(0.01,4)) = 0.6
        _CreaseShadow("Crease Shadow", Range(0,1)) = 0.30
        _CreaseColor("Crease Color", Color) = (0.62,0.55,0.58,1)
        _EdgeLight("Edge Light", Range(0,1)) = 0.18
        _EdgeLightColor("Edge Light Color", Color) = (1.0,0.97,0.92,1)

        [Header(Light Direction)]
        // 0 = fixed direction below, 1 = follow the scene's main light. Fixed is the
        // default: on a locked top-down camera it keeps every prop reading the same
        // no matter how the scene light is aimed for shadow casting.
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

        [Header(Vertical Gradient)]
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

            half   _Roundness;
            float4 _RoundPivot;

            half4 _SkyColor;
            half4 _GroundColor;
            half  _HemisphereWrap;

            half4 _LightTint;
            half4 _ShadowTint;
            half  _ShadowThreshold;
            half  _LightThreshold;
            half  _DirectionalStrength;

            half _ToonSteps;
            half _ToonStepSoftness;

            half  _CurvatureScale;
            half  _CreaseShadow;
            half4 _CreaseColor;
            half  _EdgeLight;
            half4 _EdgeLightColor;

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
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_AOMap);   SAMPLER(sampler_AOMap);

        // A mesh imported with normals disabled hands the shader a zero vector, and
        // normalize(0) is NaN - one NaN normal poisons everything downstream.
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

        // Rounded-corner normal: blend the mesh normal toward the radial direction from
        // the pivot. At 1.0 a cube shades exactly like a sphere.
        float3 RoundedNormalOS(float3 positionOS, float3 normalOS)
        {
            float3 n = SafeNormal(normalOS, float3(0.0, 1.0, 0.0));
            float3 radial = SafeNormal(positionOS - _RoundPivot.xyz, n);
            return SafeNormal(lerp(n, radial, saturate(_Roundness)), n);
        }

        // Soft posterisation. Borders stay smooth at any step count, which is what keeps
        // this a soft toon rather than hard cel.
        half ToonQuantize(half value, half steps, half softness)
        {
            half n = max(steps, 1.0h);
            half scaled = saturate(value) * n;
            half idx = floor(scaled);
            half frc = scaled - idx;
            half halfSoft = max(softness, 1e-3h) * 0.5h;
            half stepped = smoothstep(0.5h - halfSoft, 0.5h + halfSoft, frc);
            return saturate((idx + stepped) / n);
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

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                float3 roundedOS = RoundedNormalOS(input.positionOS.xyz, input.normalOS);

                output.positionCS  = positionInputs.positionCS;
                output.positionWS  = positionInputs.positionWS;
                output.positionOS  = input.positionOS.xyz;
                output.normalWS    = SafeNormal(TransformObjectToWorldNormal(roundedOS),
                                                float3(0.0, 1.0, 0.0));
                output.uv          = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogCoord    = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // ---- Albedo -------------------------------------------------------
                half4 baseSample = SampleBaseMap(input.uv);
                half3 albedo = baseSample.rgb;
                ClipAlpha(baseSample.a);

                float3 normalWSf = SafeNormal(input.normalWS, float3(0.0, 1.0, 0.0));
                half3  normalWS  = (half3)normalWSf;

                // ---- Curvature: the fake self-light and self-shadow ---------------
                //  Dividing by the squared world size of a pixel makes this real
                //  curvature (1/radius in world units) rather than something that
                //  changes with screen resolution or camera distance. Positive is
                //  convex - an outer edge; negative is concave - a crease.
                float3 dPx = ddx(input.positionWS);
                float3 dPy = ddy(input.positionWS);
                float3 dNx = ddx(normalWSf);
                float3 dNy = ddy(normalWSf);
                float  pixelArea = dot(dPx, dPx) + dot(dPy, dPy) + 1e-8;
                float  curvature = (dot(dNx, dPx) + dot(dNy, dPy)) / pixelArea;
                curvature = clamp(curvature * _CurvatureScale, -1.0, 1.0);

                half concave = saturate((half)(-curvature));   // creases
                half convex  = saturate((half)( curvature));   // outer edges

                // ---- Hemisphere ---------------------------------------------------
                //  The main volume cue: normalWS.y picks between the ground and sky
                //  tint, so each flat face lands on one uniform value and the
                //  face-to-face difference reads as form. A plain lerp, so nothing
                //  can band on curved geometry.
                half up = normalWS.y * 0.5h + 0.5h;
                up = saturate(lerp(step(0.5h, up), up, _HemisphereWrap));
                half3 hemisphere = lerp(_GroundColor.rgb, _SkyColor.rgb, up);

                // ---- Soft directional, then toon-stepped --------------------------
                float3 fixedDir = SafeNormal(_FakeLightDirection.xyz, float3(-0.35, 0.85, -0.4));
                float3 lightDir = SafeNormal(
                    lerp(fixedDir, _MainLightPosition.xyz, saturate(_UseMainLightDirection)),
                    fixedDir);

                half ndl = dot(normalWS, (half3)lightDir);
                half halfLambert = ndl * 0.5h + 0.5h;

                half lo = min(_ShadowThreshold, _LightThreshold - 1e-3h);
                half ramp = smoothstep(lo, _LightThreshold, halfLambert);

                // The toon stepping goes here, on the ramp, so the hemisphere term
                // stays perfectly smooth underneath and only the directional
                // contribution gains discrete steps.
                ramp = ToonQuantize(ramp, _ToonSteps, _ToonStepSoftness);

                half3 directional = lerp(_ShadowTint.rgb, _LightTint.rgb, ramp);
                directional = lerp(half3(1.0h, 1.0h, 1.0h), directional, _DirectionalStrength);

                // ---- Realtime shadow, tinted and floored -------------------------
                Light mainLight = GetMainLight(input.shadowCoord);
                half atten = lerp(1.0h, mainLight.shadowAttenuation, saturate(_ReceiveShadowStrength));
                atten = max(atten, _ShadowBrightness);
                half3 shadowTerm = lerp(_ShadowColorTint.rgb, half3(1.0h, 1.0h, 1.0h), atten);

                // ---- Ambient occlusion -------------------------------------------
                half texAO = SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, input.uv).r;
                half ao = max(lerp(1.0h, texAO, _AOStrength), _AOFloor);

                // ---- Compose ------------------------------------------------------
                half3 color = albedo * hemisphere * directional * shadowTerm * ao;

                // Fake self-shadow in creases, fake self-light on outer edges. Both
                // deliberately small - this is a hint of form, not a lighting model.
                color = lerp(color, color * _CreaseColor.rgb, concave * _CreaseShadow);
                color += _EdgeLightColor.rgb * (convex * _EdgeLight) * albedo;

                // ---- Vertical gradient -------------------------------------------
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
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

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

            VaryingsShadow ShadowVert(AttributesShadow input)
            {
                VaryingsShadow output = (VaryingsShadow)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 normalOS   = SafeNormal(input.normalOS, float3(0.0, 1.0, 0.0));
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
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
            #pragma multi_compile_instancing

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

            VaryingsDepth DepthVert(AttributesDepth input)
            {
                VaryingsDepth output = (VaryingsDepth)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
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
        //  Present because this project's SSAO renderer feature is set to
        //  Source = DepthNormals; a material with no such pass is absent from that
        //  prepass. The rounded normal is written here too so SSAO agrees with the
        //  shading.
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
            #pragma multi_compile_instancing

            struct AttributesDN
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
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

                float3 roundedOS = RoundedNormalOS(input.positionOS.xyz, input.normalOS);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = SafeNormal(TransformObjectToWorldNormal(roundedOS),
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

    // No Fallback on purpose - magenta plus a Console error is more useful than a
    // silently substituted lit shader.
}
