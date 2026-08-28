// =====================================================================================
//  RestaurantLoop / SoftMatteWood
// -------------------------------------------------------------------------------------
//  Procedural stylised wood, sharing SoftMatte's exact lighting core so a wooden prop
//  and a painted prop sit in the same art world. Planks, seam grooves, grain and
//  per-plank colour variation, all generated in HLSL - no wood texture needed.
//
//  WHY PROCEDURAL HERE
//  ===================
//  There is a lot of wood in this game and almost none of it is unwrapped. A texture
//  approach would need a UV pass over every prop first. This generates the pattern from
//  OBJECT-SPACE position instead, so:
//    * no UVs required
//    * the grain is welded to the mesh, so a tray travelling down the conveyor carries
//      its wood with it instead of sliding through a world-fixed pattern
//    * one material can drive a whole set of props at different sizes
//
//  HOW THE PATTERN IS BUILT
//  ========================
//   1. Object position is projected onto the two axes most perpendicular to the normal.
//      Projecting onto a frame derived from the normal instead would collapse to zero on
//      any round object - on a sphere the position is parallel to the normal, so both
//      dot products vanish and the whole mesh samples one point.
//   2. That 2D coordinate is cut into planks across one axis and optionally into
//      staggered segments along the other, exactly like floorboards.
//   3. Seam grooves are pure smoothstep maths - no noise - so they are cheap enough to
//      evaluate three times for a gradient, which lets the grooves tilt the normal and
//      pick up SoftMatte's crease shadow. That is what makes them read as cut into the
//      surface rather than painted on.
//   4. Grain is two octaves of value noise stretched hard along the plank direction.
//      Each plank gets its own noise offset and tint from its index, so no two planks
//      repeat.
//
//  Everything is static. There is no _Time anywhere in this shader.
//
//  Passes: UniversalForward / ShadowCaster / DepthOnly / DepthNormals
//  Unity 6.x / URP 14-17 / Forward / Android + iOS.
// =====================================================================================

Shader "RestaurantLoop/SoftMatteWood"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap("Base Map (optional overlay)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClipToggle("Enable Alpha Clipping", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2

        [Header(Wood Colour)]
        _WoodLight("Wood Light Tone", Color) = (0.93,0.60,0.33,1)
        _WoodDark("Wood Dark Tone", Color) = (0.60,0.29,0.13,1)
        _GrooveColor("Groove Color", Color) = (0.40,0.20,0.11,1)

        [Header(Planks)]
        _WoodScale("Wood Scale", Float) = 3.0
        [Enum(AlongX,0,AlongY,1)] _PlankAxis("Plank Direction", Float) = 0
        _PlankWidth("Plank Width", Range(0.02,2)) = 0.30
        _PlankLength("Plank Length (0 = no cross cuts)", Range(0,4)) = 1.6
        _PlankStagger("Plank Stagger", Range(0,1)) = 0.5
        _PlankTintVariation("Per Plank Tint Variation", Range(0,0.5)) = 0.10

        [Header(Grooves)]
        _GrooveWidth("Groove Width", Range(0.005,0.3)) = 0.045
        _GrooveDepth("Groove Darkening", Range(0,1)) = 0.55
        _GrooveNormalStrength("Groove Relief", Range(0,3)) = 1.0

        [Header(Grain)]
        _GrainScale("Grain Scale", Float) = 28.0
        _GrainStretch("Grain Stretch Along Plank", Range(1,30)) = 14.0
        _GrainStrength("Grain Strength", Range(0,1)) = 0.62
        _GrainContrast("Grain Contrast", Range(0.2,3)) = 1.8
        _GrainDetail("Second Octave", Range(0,1)) = 0.4

        [Header(Rounded Corners)]
        _Roundness("Roundness", Range(0,1)) = 0.25
        _RoundPivot("Round Pivot (Object Space)", Vector) = (0,0,0,0)

        [Header(Hemisphere    the top down look)]
        _SkyColor("Sky Color (up facing)", Color) = (1.06,1.03,0.98,1)
        _GroundColor("Ground Color (down facing)", Color) = (0.60,0.50,0.46,1)
        _HemisphereWrap("Hemisphere Softness", Range(0.01,1)) = 0.90

        [Header(Soft Directional)]
        _LightTint("Light Tint", Color) = (1.0,0.98,0.94,1)
        _ShadowTint("Shadow Tint", Color) = (0.70,0.60,0.60,1)
        _ShadowThreshold("Shadow Threshold", Range(0,1)) = 0.10
        _LightThreshold("Light Threshold", Range(0,1)) = 0.92
        _DirectionalStrength("Directional Strength", Range(0,1)) = 0.60

        [Header(Toon Stepping)]
        _ToonSteps("Toon Steps", Range(1,6)) = 3
        _ToonStepSoftness("Toon Step Softness", Range(0.05,1)) = 0.55

        [Header(Fake Self Light and Shadow)]
        _CurvatureScale("Curvature Scale", Range(0.01,4)) = 0.6
        _CreaseShadow("Crease Shadow", Range(0,1)) = 0.35
        _CreaseColor("Crease Color", Color) = (0.58,0.44,0.38,1)
        _EdgeLight("Edge Light", Range(0,1)) = 0.18
        _EdgeLightColor("Edge Light Color", Color) = (1.0,0.96,0.90,1)

        [Header(Light Direction)]
        _UseMainLightDirection("Use Scene Light Direction", Range(0,1)) = 0.0
        _FakeLightDirection("Fixed Light Direction", Vector) = (-0.35,0.85,-0.4,0)

        [Header(Realtime Shadows)]
        _ReceiveShadowStrength("Receive Shadow Strength", Range(0,1)) = 0.45
        _ShadowBrightness("Shadow Minimum Brightness", Range(0,1)) = 0.58
        _ShadowColorTint("Received Shadow Tint", Color) = (0.68,0.58,0.58,1)

        [Header(Ambient Occlusion)]
        _AOMap("AO Map (R)", 2D) = "white" {}
        _AOStrength("AO Strength", Range(0,1)) = 0.25
        _AOFloor("AO Minimum", Range(0,1)) = 0.45

        [Header(Vertical Gradient)]
        _TopGradientColor("Top Gradient", Color) = (1.05,1.02,0.98,1)
        _BottomGradientColor("Bottom Gradient", Color) = (0.88,0.82,0.82,1)
        _GradientIntensity("Gradient Intensity", Range(0,1)) = 0.14
        _GradientStart("Gradient Start", Float) = -0.5
        _GradientEnd("Gradient End", Float) = 0.5
        [Enum(ObjectSpace,0,WorldSpace,1)] _GradientSpace("Gradient Space", Float) = 0

        [Header(Colour)]
        _Saturation("Saturation", Range(0,2)) = 0.98
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

            half4 _WoodLight;
            half4 _WoodDark;
            half4 _GrooveColor;

            float _WoodScale;
            half  _PlankAxis;
            float _PlankWidth;
            float _PlankLength;
            half  _PlankStagger;
            half  _PlankTintVariation;

            float _GrooveWidth;
            half  _GrooveDepth;
            half  _GrooveNormalStrength;

            float _GrainScale;
            float _GrainStretch;
            half  _GrainStrength;
            half  _GrainContrast;
            half  _GrainDetail;

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

        // The base map is an OPTIONAL overlay here - the wood itself is generated. With
        // no texture assigned Unity supplies white, so this multiplies by 1 and the
        // procedural wood comes through untouched.
        half4 SampleBaseMapSafe(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
        }

        float3 RoundedNormalOS(float3 positionOS, float3 normalOS)
        {
            float3 n = SafeNormal(normalOS, float3(0.0, 1.0, 0.0));
            float3 radial = SafeNormal(positionOS - _RoundPivot.xyz, n);
            return SafeNormal(lerp(n, radial, saturate(_Roundness)), n);
        }

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

        // =============================================================================
        //  Noise. The sin-free Dave Hoskins hash: sin()-based hashes drift on mobile
        //  GPUs with relaxed trig precision and band visibly. Must stay float - half
        //  precision destroys the hash.
        // =============================================================================
        float Hash21(float2 p)
        {
            float3 p3 = frac(p.xyx * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float Hash11(float n)
        {
            return Hash21(float2(n, n * 1.7321 + 4.13));
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = Hash21(i);
            float b = Hash21(i + float2(1.0, 0.0));
            float c = Hash21(i + float2(0.0, 1.0));
            float d = Hash21(i + float2(1.0, 1.0));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        // =============================================================================
        //  Wood coordinate
        //
        //  Picks the two object axes most perpendicular to the normal and reads the
        //  position's components along them, then swaps so .x always runs ALONG the
        //  plank. Where the dominant axis changes the plank direction changes with it,
        //  which on a box prop lands exactly on the geometric edges - the same place a
        //  carpenter would change board direction.
        // =============================================================================
        float2 GetWoodCoord(float3 positionOS, float3 normalOS)
        {
            float3 an = abs(SafeNormal(normalOS, float3(0.0, 1.0, 0.0)));
            float3 p = positionOS * _WoodScale;

            float2 coord;
            if (an.x >= an.y && an.x >= an.z)      coord = float2(p.z, p.y);
            else if (an.y >= an.z)                 coord = float2(p.x, p.z);
            else                                   coord = float2(p.x, p.y);

            // _PlankAxis swaps which of the two runs along the boards.
            return (_PlankAxis < 0.5h) ? coord : coord.yx;
        }

        // Row / column indices and the local position inside one plank.
        //  .x = fraction across the plank (0..1, the seam is at 0 and 1)
        //  .y = fraction along the plank
        //  .z = row index
        //  .w = column index
        float4 GetPlankCell(float2 wc)
        {
            float rowF = wc.y / max(_PlankWidth, 1e-3);
            float row  = floor(rowF);
            float across = rowF - row;

            // Stagger each row along its length so seams do not line up in a grid.
            float alongPos = wc.x + Hash11(row) * _PlankStagger * max(_PlankLength, 1.0);

            float col = 0.0;
            float along = 0.5;
            if (_PlankLength > 1e-3)
            {
                float colF = alongPos / _PlankLength;
                col = floor(colF);
                along = colF - col;
            }

            return float4(across, along, row, col);
        }

        // Seam grooves. Pure smoothstep maths, no noise, which is why this is cheap
        // enough to evaluate three times for a gradient.
        float GrooveMask(float2 wc)
        {
            float4 cell = GetPlankCell(wc);
            float gw = max(_GrooveWidth, 1e-4);

            float acrossMask = smoothstep(0.0, gw, cell.x) * smoothstep(0.0, gw, 1.0 - cell.x);
            float mask = 1.0 - acrossMask;

            if (_PlankLength > 1e-3)
            {
                float alongMask = smoothstep(0.0, gw, cell.y) * smoothstep(0.0, gw, 1.0 - cell.y);
                mask = max(mask, 1.0 - alongMask);
            }

            return saturate(mask);
        }

        // Grain: two octaves of value noise, squashed hard along the plank so it reads
        // as fibre rather than blobs. Offset per plank so no two boards repeat.
        float WoodGrain(float2 wc, float row, float col)
        {
            float2 g = float2(wc.x / max(_GrainStretch, 1.0), wc.y) * _GrainScale;
            g += float2(row * 17.31 + col * 5.77, row * 3.19 - col * 11.53);

            float n = ValueNoise(g);
            n = lerp(n, n * 0.6 + ValueNoise(g * 2.37 + 9.1) * 0.4, _GrainDetail);

            // Contrast around the midpoint, so strength 0 leaves the wood tone flat.
            n = saturate((n - 0.5) * _GrainContrast + 0.5);
            return n;
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
                float3 normalOS    : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                float  fogCoord    : TEXCOORD6;
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
                float3 rawNormalOS = SafeNormal(input.normalOS, float3(0.0, 1.0, 0.0));
                float3 roundedOS   = RoundedNormalOS(input.positionOS.xyz, rawNormalOS);

                output.positionCS  = positionInputs.positionCS;
                output.positionWS  = positionInputs.positionWS;
                output.positionOS  = input.positionOS.xyz;
                // The RAW normal drives the wood projection, so the plank layout does
                // not swim when _Roundness is dialled up.
                output.normalOS    = rawNormalOS;
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

                half4 baseSample = SampleBaseMapSafe(input.uv);
                ClipAlpha(baseSample.a);

                float3 normalWSf = SafeNormal(input.normalWS, float3(0.0, 1.0, 0.0));
                half3  normalWS  = (half3)normalWSf;

                // =================================================================
                //  WOOD
                // =================================================================
                float2 wc = GetWoodCoord(input.positionOS, input.normalOS);
                float4 cell = GetPlankCell(wc);

                float groove = GrooveMask(wc);
                float grain  = WoodGrain(wc, cell.z, cell.w);

                // Per-plank tint so no two boards read the same.
                float plankRnd = Hash21(float2(cell.z, cell.w));
                float plankTint = lerp(1.0 - _PlankTintVariation,
                                       1.0 + _PlankTintVariation, plankRnd);

                half3 wood = lerp(_WoodDark.rgb, _WoodLight.rgb,
                                  (half)lerp(0.5, grain, _GrainStrength));
                wood *= (half)plankTint;
                wood = lerp(wood, _GrooveColor.rgb, (half)groove * _GrooveDepth);

                half3 albedo = wood * _BaseColor.rgb * baseSample.rgb;

                // Groove relief. Three cheap smoothstep evaluations give the groove's
                // slope in wood-coordinate space; tilting the normal by it is what makes
                // the seams catch the crease shadow below instead of looking painted on.
                if (_GrooveNormalStrength > 1e-3h)
                {
                    float e = max(_PlankWidth, 1e-3) * 0.12;
                    float gdx = GrooveMask(wc + float2(e, 0.0)) - groove;
                    float gdy = GrooveMask(wc + float2(0.0, e)) - groove;

                    // The wood coordinate lives in the plane the projection picked, so
                    // its two axes are already perpendicular to the dominant normal
                    // axis. Building the tilt from world-space derivatives of the
                    // position keeps it correct whatever that plane turned out to be.
                    float3 dPx = ddx(input.positionWS);
                    float3 dPy = ddy(input.positionWS);
                    float3 tangentish = SafeNormal(dPx, float3(1.0, 0.0, 0.0));
                    float3 bitangentish = SafeNormal(dPy, float3(0.0, 0.0, 1.0));

                    float3 tilt = tangentish * gdx + bitangentish * gdy;
                    normalWSf = SafeNormal(normalWSf - tilt * _GrooveNormalStrength * 4.0,
                                           normalWSf);
                    normalWS = (half3)normalWSf;
                }

                // =================================================================
                //  LIGHTING - identical maths to RestaurantLoop/SoftMatte
                // =================================================================
                float3 dPx2 = ddx(input.positionWS);
                float3 dPy2 = ddy(input.positionWS);
                float3 dNx  = ddx(normalWSf);
                float3 dNy  = ddy(normalWSf);
                float  pixelArea = dot(dPx2, dPx2) + dot(dPy2, dPy2) + 1e-8;
                float  curvature = (dot(dNx, dPx2) + dot(dNy, dPy2)) / pixelArea;
                curvature = clamp(curvature * _CurvatureScale, -1.0, 1.0);

                half concave = saturate((half)(-curvature));
                half convex  = saturate((half)( curvature));

                half up = normalWS.y * 0.5h + 0.5h;
                up = saturate(lerp(step(0.5h, up), up, _HemisphereWrap));
                half3 hemisphere = lerp(_GroundColor.rgb, _SkyColor.rgb, up);

                float3 fixedDir = SafeNormal(_FakeLightDirection.xyz, float3(-0.35, 0.85, -0.4));
                float3 lightDir = SafeNormal(
                    lerp(fixedDir, _MainLightPosition.xyz, saturate(_UseMainLightDirection)),
                    fixedDir);

                half ndl = dot(normalWS, (half3)lightDir);
                half halfLambert = ndl * 0.5h + 0.5h;
                half lo = min(_ShadowThreshold, _LightThreshold - 1e-3h);
                half ramp = smoothstep(lo, _LightThreshold, halfLambert);
                ramp = ToonQuantize(ramp, _ToonSteps, _ToonStepSoftness);

                half3 directional = lerp(_ShadowTint.rgb, _LightTint.rgb, ramp);
                directional = lerp(half3(1.0h, 1.0h, 1.0h), directional, _DirectionalStrength);

                Light mainLight = GetMainLight(input.shadowCoord);
                half atten = lerp(1.0h, mainLight.shadowAttenuation, saturate(_ReceiveShadowStrength));
                atten = max(atten, _ShadowBrightness);
                half3 shadowTerm = lerp(_ShadowColorTint.rgb, half3(1.0h, 1.0h, 1.0h), atten);

                half texAO = SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, input.uv).r;
                half ao = max(lerp(1.0h, texAO, _AOStrength), _AOFloor);

                half3 color = albedo * hemisphere * directional * shadowTerm * ao;

                color = lerp(color, color * _CreaseColor.rgb, concave * _CreaseShadow);
                color += _EdgeLightColor.rgb * (convex * _EdgeLight) * albedo;

                float height = (_GradientSpace < 0.5h) ? input.positionOS.y : input.positionWS.y;
                float range  = _GradientEnd - _GradientStart;
                float denom  = (abs(range) < 1e-4) ? 1e-4 : range;
                half  gradT  = saturate((half)((height - _GradientStart) / denom));
                half3 gradient = lerp(_BottomGradientColor.rgb, _TopGradientColor.rgb, gradT);
                color = lerp(color, color * gradient, _GradientIntensity);

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
                ClipAlpha(SampleBaseMapSafe(input.uv).a);
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
                ClipAlpha(SampleBaseMapSafe(input.uv).a);
                return 0;
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 4 - Depth Normals
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
                ClipAlpha(SampleBaseMapSafe(input.uv).a);
                return half4((half3)SafeNormal(input.normalWS, float3(0.0, 1.0, 0.0)), 0.0h);
            }
            ENDHLSL
        }
    }

    // No Fallback on purpose.
}
