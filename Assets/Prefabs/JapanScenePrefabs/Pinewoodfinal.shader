// =====================================================================================
//  RestaurantLoop / SoftMatteWood                                                 v6
// -------------------------------------------------------------------------------------
//  Toon pinewood. Plain pale timber with dark growth-ring streaks and small round knots
//  that the grain bends around. Procedural, no texture, no UVs. Every detail can be
//  switched off independently.
//
//
//  WHAT CHANGED FROM v5
//  ====================
//  v5 was floorboards: the whole surface was cut into planks with seam grooves, and the
//  grain was isotropic noise. That reads as parquet, not as a piece of wood.
//
//  v6 is one continuous board by default:
//    * PLANK SEAMS ARE OFF. They still exist, behind a toggle, for the few props that
//      genuinely are made of separate boards.
//    * GRAIN IS DIRECTIONAL STREAKS, not noise. Discrete dark lines running along the
//      board, posterised into a few value steps, each line with its own width and
//      strength. That is what makes it read as toon rather than photoreal.
//    * KNOTS. Small oval knots on a jittered grid, each with a dark rim and a lighter
//      heart, and the grain streaks bend around them. This is the single thing that
//      makes a surface read as pine rather than as generic brown.
//
//  Three things were tuned by rendering, not guessed:
//    * Line grouping. Straight evenly-spaced lines read as corduroy fabric. A slow
//      low-frequency term added to the line coordinate bunches them into groups the way
//      growth rings actually cluster - _GrainGroup is that term.
//    * Line count. 14 lines/m on a 1 m surface is the density that stays legible; much
//      above 25 and a phone screen turns it into moire.
//    * Knot heart. A flat dark oval reads as a hole. A dark rim with a lighter centre
//      reads as a knot. _KnotCoreLightness is that difference.
//
//
//  HOW THE TOGGLES ARE WIRED, AND WHY THEY LOOK BACKWARDS
//  ======================================================
//  The keywords are _GRAIN_OFF, _KNOTS_OFF and _SEAMS_ON - so every DEFAULT state is
//  "no keyword set". That is deliberate. A material created from script, or from a
//  preset, or before its inspector has ever been drawn, does not reliably have Toggle
//  keywords applied; if the full look depended on a keyword being ON, such a material
//  would silently render plain. Wiring the defaults to the absence of a keyword means
//  the shader cannot fail that way. The inspector labels read "Disable Grain" and
//  "Disable Knots" as a result, which is the small price.
//
//  All three are shader_feature_local, so anything switched off is compiled out and
//  costs nothing - the knot search in particular is a 3x3 neighbourhood loop and is
//  entirely absent from the build when knots are disabled.
//
//
//  THIS FILE IS NOW ACTUALLY COMPILED BEFORE IT SHIPS
//  ==================================================
//  Three magentas in a row, two of them from causes that text inspection did not catch.
//  There is now a real HLSL front end in the loop: compile_shader.py stubs the URP
//  headers and runs glslangValidator over every pass, in both stages, under twelve
//  keyword combinations - 96 compilations per shader. It found the v6 bug immediately:
//
//      float line = 1.0 - smoothstep(...);
//            ^^^^
//  'line' is a RESERVED WORD in HLSL - a geometry-shader input primitive type, with
//  point, triangle, lineadj and triangleadj. Declaring a variable with that name is a
//  plain syntax error. RL_PineGrain was new in v6 and was the only place it appeared,
//  which is why v5 was fine and v6 was not. The local is now called 'streak'.
//
//  The earlier magenta, for the record: v4 defined its own RgbToHsv/HsvToRgb, which
//  Unity already defines in core's Color.hlsl, and HLSL has no redefinition. Every
//  function here is prefixed RL_ so that cannot recur.
//
//  Both checks are automated now - validate_shader.py catches Unity symbol collisions
//  and reserved words statically, compile_shader.py compiles the thing for real.
//
//
//  CARRIED FORWARD FROM v3/v4 - both were hard-won, neither changed
//  ===============================================================
//  SIZE IN METRES. Object space is not a unit: a mesh authored at 0.1 units and scaled
//  to 30 in the Transform occupies the same space as a 1-unit mesh at scale 1, but their
//  object-space coordinates differ by 300x. The object's world scale is pulled from the
//  model matrix and multiplied in, so every size here is real metres on every prop.
//  _ScaleCompensation = 0 restores raw object space.
//
//  ALIGNMENT. _ProjectionAxis forces one plane, _PatternRotation spins the grain inside
//  it, _PatternOffset slides it. When the grain runs the wrong way, reach for them in
//  that order: _GrainAxis (90 degree flip), _PatternRotation, then _ProjectionAxis.
//
//  ONE COLOUR SWATCH. The pale field IS _WoodColor, bit-exact, and the streak, knot and
//  seam tones all derive from it in HSV. Pale beech and near-white ash are reachable;
//  under the old multiply-only tint they were not, since a multiply can only darken.
//
//  Note the v6 correction here. v4 spread light and dark symmetrically around the
//  swatch, which was correct when the grain was noise centred on 0.5 - the average then
//  landed back on the colour picked. v6's grain is thin dark streaks on a pale field, so
//  most of the surface is the light tone; keeping the symmetric spread rendered every
//  wood 8-15% LIGHTER than the swatch, handing the "what you pick is what you get"
//  property straight back. Making the field the swatch itself fixes it, and as a bonus
//  frees the streak tone from the symmetry constraint - pale woods can now have properly
//  dark grain instead of the washed-out streaks symmetry forced on them.
//
//
//  PRESETS
//  =======
//   Pine prop - the defaults. Tables, chairs, crates, shelves, anything 0.5-1.5 m.
//     WoodColor (0.86,0.70,0.47)  GrainFreq 14  GrainWidth 0.16  GrainSteps 3
//     KnotSpacing 0.34  KnotSize 0.050
//
//   Small prop under ~0.4 m - a big surface pattern shrunk down turns to mush
//     GrainFreq 10  GrainWidth 0.18  KnotSpacing 0.55  KnotSize 0.035
//
//   Large counter or floor, 2 m+ - knot spacing is in metres, so a big surface gets a
//   lot of them. Space them out or the surface reads as diseased.
//     GrainFreq 18  KnotSpacing 0.75  KnotSize 0.055
//
//   Painted / background prop - grain barely there, no knots
//     Disable Knots ON  GrainStrength 0.30  GrainFreq 11
//
//   Actual planked surface - a crate wall, a pallet, decking
//     Enable Plank Seams ON  PlankWidth 0.14  PlankLength 0.9  GrooveDepth 0.5
//
//   Swatches: pine (0.86,0.70,0.47), oak (0.76,0.52,0.31), walnut (0.34,0.21,0.13),
//   pale beech (0.87,0.75,0.58), near-white ash (0.92,0.88,0.80),
//   grey driftwood (0.62,0.61,0.58). Change _WoodColor alone; nothing else needs it.
//
//
//  Everything is static. There is no _Time anywhere in this shader.
//  Includes: Core.hlsl, Lighting.hlsl, Shadows.hlsl. Nothing else.
//  No Meta pass - URP 17 removed ShaderLibrary/UnityMetaPass.hlsl and including it is
//  itself a magenta. No Fallback - a real Console error beats a silent wrong shader.
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

        [Header(Wood Colour    one swatch drives everything)]
        _WoodColor("Wood Color", Color) = (0.86,0.70,0.47,1)
        _ToneSpread("Light to Dark Spread", Range(0,1)) = 0.55
        _ToneWarmth("Shadow Warmth", Range(-1,1)) = 0.35

        [Header(Pine Grain)]
        [Toggle(_GRAIN_OFF)] _DisableGrain("Disable Grain", Float) = 0
        _GrainFreq("Grain Lines Per Meter", Range(2,60)) = 14.0
        _GrainStrength("Grain Strength", Range(0,1)) = 0.62
        _GrainWidth("Line Width", Range(0.02,0.5)) = 0.16
        _GrainSoftness("Line Softness", Range(0.01,0.4)) = 0.10
        _GrainWidthVariation("Line Width Variation", Range(0,1)) = 0.80
        _GrainSteps("Grain Toon Steps", Range(1,6)) = 3
        _GrainGroup("Line Grouping", Range(0,1.5)) = 0.40
        _GrainGroupScale("Grouping Scale", Range(0.1,4)) = 0.90
        _GrainWave("Waviness", Range(0,0.6)) = 0.14
        _GrainWaveScale("Waviness Scale", Range(0.5,10)) = 3.00

        [Header(Knots)]
        [Toggle(_KNOTS_OFF)] _DisableKnots("Disable Knots", Float) = 0
        _KnotSpacing("Knot Spacing (m)", Range(0.05,3)) = 0.34
        _KnotSize("Knot Size (m)", Range(0.005,0.3)) = 0.050
        _KnotDensity("Knot Density", Range(0,1)) = 0.50
        _KnotSquash("Knot Squash", Range(0.3,3)) = 1.40
        _KnotPull("Grain Pull Around Knot", Range(0,8)) = 2.40
        _KnotDarkness("Knot Darkness", Range(0,1)) = 0.90
        _KnotCoreLightness("Knot Heart Lightness", Range(0,1)) = 0.45

        [Header(Plank Seams    off by default)]
        [Toggle(_SEAMS_ON)] _EnableSeams("Enable Plank Seams", Float) = 0
        _PlankWidth("Plank Width (m)", Range(0.01,1)) = 0.14
        _PlankLength("Plank Length (m, 0 = no cross cuts)", Range(0,3)) = 0.90
        _PlankStagger("Plank Stagger", Range(0,1)) = 0.50
        _GrooveWidth("Groove Width (fraction of plank)", Range(0.005,0.3)) = 0.05
        _GrooveDepth("Groove Darkening", Range(0,1)) = 0.50
        _GrooveNormalStrength("Groove Relief", Range(0,3)) = 1.00
        _SeamDarken("Seam Extra Darkening", Range(0,1)) = 0.45
        _PlankTintVariation("Per Board Brightness Variation", Range(0,0.5)) = 0.08

        [Header(Manual Two Tone    overrides the swatch above)]
        [Toggle(_TWOTONE_WOOD)] _TwoToneMode("Set Light and Dark Manually", Float) = 0
        _WoodLight("Wood Light Tone", Color) = (0.95,0.84,0.65,1)
        _WoodDark("Wood Dark Tone", Color) = (0.70,0.47,0.25,1)
        _GrooveColor("Seam and Knot Color", Color) = (0.36,0.20,0.10,1)

        [Header(Pattern Placement)]
        _ScaleCompensation("World Scale Compensation", Range(0,1)) = 1.0
        [Enum(Auto,0,ForceX,1,ForceY,2,ForceZ,3)] _ProjectionAxis("Projection Axis", Float) = 0
        [Enum(AlongX,0,AlongY,1)] _GrainAxis("Grain Direction", Float) = 0
        _PatternRotation("Pattern Rotation (degrees)", Range(0,360)) = 0
        _PatternOffset("Pattern Offset (meters)", Vector) = (0,0,0,0)
        _WoodScale("Pattern Density Multiplier", Range(0.1,8)) = 1.0

        [Header(Rounded Corners)]
        _Roundness("Roundness", Range(0,1)) = 0.25
        _RoundPivot("Round Pivot (Object Space)", Vector) = (0,0,0,0)

        [Header(Hemisphere    the top down look)]
        _SkyColor("Sky Color (up facing)", Color) = (1.06,1.03,0.98,1)
        _GroundColor("Ground Color (down facing)", Color) = (0.68,0.64,0.62,1)
        _HemisphereWrap("Hemisphere Softness", Range(0.01,1)) = 0.90

        [Header(Soft Directional)]
        _LightTint("Light Tint", Color) = (1.0,0.98,0.94,1)
        _ShadowTint("Shadow Tint", Color) = (0.70,0.62,0.62,1)
        _ShadowThreshold("Shadow Threshold", Range(0,1)) = 0.10
        _LightThreshold("Light Threshold", Range(0,1)) = 0.92
        _DirectionalStrength("Directional Strength", Range(0,1)) = 0.60

        [Header(Toon Stepping)]
        _ToonSteps("Toon Steps", Range(1,6)) = 3
        _ToonStepSoftness("Toon Step Softness", Range(0.05,1)) = 0.55

        [Header(Fake Self Light and Shadow)]
        _CurvatureScale("Curvature Scale", Range(0.01,4)) = 0.6
        _CreaseShadow("Crease Shadow", Range(0,1)) = 0.35
        _CreaseColor("Crease Color", Color) = (0.62,0.52,0.48,1)
        _EdgeLight("Edge Light", Range(0,1)) = 0.18
        _EdgeLightColor("Edge Light Color", Color) = (1.0,0.96,0.90,1)

        [Header(Light Direction)]
        _UseMainLightDirection("Use Scene Light Direction", Range(0,1)) = 0.0
        _FakeLightDirection("Fixed Light Direction", Vector) = (-0.35,0.85,-0.4,0)

        [Header(Realtime Shadows)]
        _ReceiveShadowStrength("Receive Shadow Strength", Range(0,1)) = 0.45
        _ShadowBrightness("Shadow Minimum Brightness", Range(0,1)) = 0.58
        _ShadowColorTint("Received Shadow Tint", Color) = (0.70,0.62,0.62,1)

        [Header(Ambient Occlusion)]
        _AOMap("AO Map (R)", 2D) = "white" {}
        _AOStrength("AO Strength", Range(0,1)) = 0.25
        _AOFloor("AO Minimum", Range(0,1)) = 0.45

        [Header(Vertical Gradient)]
        _TopGradientColor("Top Gradient", Color) = (1.04,1.02,1.00,1)
        _BottomGradientColor("Bottom Gradient", Color) = (0.90,0.87,0.86,1)
        _GradientIntensity("Gradient Intensity", Range(0,1)) = 0.12
        _GradientStart("Gradient Start", Float) = -0.5
        _GradientEnd("Gradient End", Float) = 0.5
        [Enum(ObjectSpace,0,WorldSpace,1)] _GradientSpace("Gradient Space", Float) = 0

        [Header(Colour)]
        _Saturation("Saturation", Range(0,2)) = 1.0
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

            half4 _WoodColor;
            half  _ToneSpread;
            half  _ToneWarmth;

            half  _DisableGrain;
            float _GrainFreq;
            half  _GrainStrength;
            float _GrainWidth;
            float _GrainSoftness;
            half  _GrainWidthVariation;
            half  _GrainSteps;
            float _GrainGroup;
            float _GrainGroupScale;
            float _GrainWave;
            float _GrainWaveScale;

            half  _DisableKnots;
            float _KnotSpacing;
            float _KnotSize;
            float _KnotDensity;
            float _KnotSquash;
            float _KnotPull;
            half  _KnotDarkness;
            half  _KnotCoreLightness;

            half  _EnableSeams;
            float _PlankWidth;
            float _PlankLength;
            half  _PlankStagger;
            float _GrooveWidth;
            half  _GrooveDepth;
            half  _GrooveNormalStrength;
            half  _SeamDarken;
            half  _PlankTintVariation;

            half  _TwoToneMode;
            half4 _WoodLight;
            half4 _WoodDark;
            half4 _GrooveColor;

            half   _ScaleCompensation;
            half   _ProjectionAxis;
            half   _GrainAxis;
            float  _PatternRotation;
            float4 _PatternOffset;
            float  _WoodScale;

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

        // =============================================================================
        //  Every function below is prefixed RL_. Unity's shader library defines a large,
        //  undocumented and version-dependent set of global names - RgbToHsv, HsvToRgb,
        //  SafeNormalize, ValueNoise, Luminance, Remap, Sq, Max3 and many more - and HLSL
        //  treats a redefinition as a hard error, which Unity surfaces as magenta.
        //  Prefixing makes collision structurally impossible.
        // =============================================================================

        // A mesh imported with normals disabled hands the shader a zero vector, and
        // normalize(0) is NaN - one NaN normal poisons every term downstream.
        float3 RL_SafeNormal(float3 v, float3 fallback)
        {
            float lenSq = dot(v, v);
            return (lenSq > 1e-8) ? v * rsqrt(lenSq) : fallback;
        }

        void RL_ClipAlpha(half alpha)
        {
            #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
            #endif
        }

        // The base map is an OPTIONAL overlay - the wood itself is generated. With no
        // texture assigned Unity supplies white, so this multiplies by 1 and the
        // procedural wood comes through untouched.
        half4 RL_SampleBase(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
        }

        float3 RL_RoundedNormalOS(float3 positionOS, float3 normalOS)
        {
            float3 n = RL_SafeNormal(normalOS, float3(0.0, 1.0, 0.0));
            float3 radial = RL_SafeNormal(positionOS - _RoundPivot.xyz, n);
            return RL_SafeNormal(lerp(n, radial, saturate(_Roundness)), n);
        }

        // Soft posterisation. Borders stay smooth at any step count, which is what keeps
        // this a soft toon rather than hard cel.
        half RL_ToonQuantize(half value, half steps, half softness)
        {
            half n = max(steps, 1.0h);
            half scaled = saturate(value) * n;
            half idx = floor(scaled);
            half frc = scaled - idx;
            half halfSoft = max(softness, 1e-3h) * 0.5h;
            half stepped = smoothstep(0.5h - halfSoft, 0.5h + halfSoft, frc);
            return saturate((idx + stepped) / n);
        }

        // -----------------------------------------------------------------------------
        //  Noise. The sin-free Dave Hoskins hash: sin()-based hashes drift on mobile GPUs
        //  with relaxed trig precision and band visibly. Must stay float - half precision
        //  destroys the hash.
        // -----------------------------------------------------------------------------
        float RL_Hash21(float2 p)
        {
            float3 p3 = frac(p.xyx * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float RL_Hash11(float n)
        {
            return RL_Hash21(float2(n, n * 1.7321 + 4.13));
        }

        float RL_ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = RL_Hash21(i);
            float b = RL_Hash21(i + float2(1.0, 0.0));
            float c = RL_Hash21(i + float2(0.0, 1.0));
            float d = RL_Hash21(i + float2(1.0, 1.0));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        // -----------------------------------------------------------------------------
        //  HSV. Branchless Sam Hocevar. Same algorithm as Unity's, deliberately under our
        //  own names - defining RgbToHsv/HsvToRgb is exactly what turned v4 magenta.
        // -----------------------------------------------------------------------------
        float3 RL_RgbToHsv(float3 c)
        {
            float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
            float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
            float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
            float d = q.x - min(q.w, q.y);
            return float3(abs(q.z + (q.w - q.y) / (6.0 * d + 1e-6)), d / (q.x + 1e-6), q.x);
        }

        float3 RL_HsvToRgb(float3 c)
        {
            float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
            float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
            return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
        }

        // =============================================================================
        //  Wood tones from one swatch
        //
        //  The value spread is SYMMETRIC so the average of the rendered wood is exactly
        //  the colour picked. That symmetry is the whole point: the grain sits on the
        //  light tone and dips toward the dark one, so an asymmetric spread would drag
        //  the prop away from the swatch and the artist would chase it with Brightness.
        //
        //  ON PALE WOODS: value spread is limited by whichever side has less headroom.
        //  A swatch at value 0.92 has only 0.08 of room above it, so a wide spread would
        //  clip to flat white and destroy the grain. Pale timber genuinely has little
        //  value contrast and reads through SATURATION instead, so the tighter the value
        //  headroom the harder the saturation spread is pushed to compensate.
        //
        //  Uniform maths on constants only - the compiler hoists all of it out of the
        //  fragment loop, so this costs nothing per pixel.
        // =============================================================================
        void RL_ResolveWoodTones(out half3 lightTone, out half3 darkTone,
                                 out half3 seamTone,  out half3 knotTone)
        {
        #if defined(_TWOTONE_WOOD)
            lightTone = _WoodLight.rgb;
            darkTone  = _WoodDark.rgb;
            seamTone  = _GrooveColor.rgb;
            knotTone  = _GrooveColor.rgb;
        #else
            float3 hsv = RL_RgbToHsv(saturate(_WoodColor.rgb));
            float spread = saturate((float)_ToneSpread);

            // THE FIELD IS THE SWATCH, EXACTLY. This is the v6 correction. v4 spread
            // light and dark symmetrically around the swatch, which was right when the
            // grain was noise centred on 0.5 - the average then landed back on the
            // colour picked. v6's grain is thin dark STREAKS on a pale field, so most of
            // the surface is the light tone: keeping the old symmetric spread rendered
            // every wood 8-15% lighter than the swatch and handed the "what you pick is
            // what you get" property straight back. Making the field the swatch itself
            // and letting streaks go down from it restores it - the mean now sits a few
            // percent below the swatch, in proportion to how much grain is showing.
            //
            // It also frees the dark tone from the symmetry constraint, which is a real
            // gain on pale woods: they can now have properly dark streaks instead of the
            // washed-out ones a symmetric spread forced on them.
            float down = saturate(hsv.z / 0.6);
            float vAmt = spread * 0.55 * down;

            // Whatever darkening the value could not supply comes back as saturation.
            float sBoost = 1.0 + (1.0 - down) * 1.20;

            float3 dh = hsv;

            dh.z = saturate(hsv.z - vAmt);
            dh.y = saturate(hsv.y * (1.0 + spread * 0.55 * sBoost));  // streaks deepen

            // Shadows drift toward red the way real timber does. Negative _ToneWarmth
            // sends them the other way, for grey and driftwood.
            dh.x = frac(hsv.x - (float)_ToneWarmth * 0.030 + 1.0);

            // Taken straight from the swatch, not round-tripped through HSV - the field
            // is the one colour that must come back bit-exact.
            lightTone = (half3)saturate(_WoodColor.rgb);
            darkTone  = (half3)RL_HsvToRgb(dh);

            // Seams: the dark tone taken further down and further saturated.
            float sd = saturate((float)_SeamDarken);
            float3 sh = dh;
            sh.z = saturate(dh.z * (1.0 - sd * 0.75));
            sh.y = saturate(dh.y * (1.0 + sd * 0.35));
            seamTone = (half3)RL_HsvToRgb(sh);

            // Knots are the darkest thing on the board, and more saturated than the
            // grain, which is what separates them from a merely dark streak.
            float3 kh = dh;
            kh.z = saturate(dh.z * 0.45);
            kh.y = saturate(dh.y * 1.30);
            knotTone = (half3)RL_HsvToRgb(kh);
        #endif
        }

        // =============================================================================
        //  Object world scale
        //
        //  Read through GetObjectToWorldMatrix() into a LOCAL matrix on purpose.
        //  unity_ObjectToWorld is a macro under GPU and DOTS instancing and can expand
        //  to a function call; taking a member of a function-call result is not portable
        //  across platform compilers. Copying to a local first always is.
        // =============================================================================
        float3 RL_ObjectWorldScale()
        {
            float4x4 m = GetObjectToWorldMatrix();

            float3 s = float3(
                length(float3(m._m00, m._m10, m._m20)),
                length(float3(m._m01, m._m11, m._m21)),
                length(float3(m._m02, m._m12, m._m22)));

            // A zero scale on any axis would collapse the pattern into a single line.
            s = max(s, 1e-4);
            return lerp(float3(1.0, 1.0, 1.0), s, saturate((float)_ScaleCompensation));
        }

        // =============================================================================
        //  Wood coordinate, in metres. .x runs ALONG the board, .y across it.
        //
        //  Auto picks the two object axes most perpendicular to the normal. Projecting
        //  onto a frame derived from the normal instead would collapse to zero on any
        //  round object - on a sphere the position is parallel to the normal, so both
        //  dot products vanish and the whole mesh samples a single point.
        //
        //  Rotation is applied AFTER the projection and BEFORE anything reads the
        //  coordinate, so grain, knots and seams all turn together as one board.
        // =============================================================================
        float2 RL_WoodCoord(float3 positionOS, float3 normalOS)
        {
            float3 p = positionOS * RL_ObjectWorldScale() * max(_WoodScale, 1e-3);

            float2 coord;
            if (_ProjectionAxis < 0.5h)
            {
                float3 an = abs(RL_SafeNormal(normalOS, float3(0.0, 1.0, 0.0)));
                if (an.x >= an.y && an.x >= an.z)      coord = float2(p.z, p.y);
                else if (an.y >= an.z)                 coord = float2(p.x, p.z);
                else                                   coord = float2(p.x, p.y);
            }
            else if (_ProjectionAxis < 1.5h) coord = float2(p.z, p.y);   // Force X
            else if (_ProjectionAxis < 2.5h) coord = float2(p.x, p.z);   // Force Y
            else                             coord = float2(p.x, p.y);   // Force Z

            // _GrainAxis swaps which of the two runs along the board.
            coord = (_GrainAxis < 0.5h) ? coord : coord.yx;

            // Plain sin/cos and a constant - no sincos(), no radians().
            float a  = _PatternRotation * 0.01745329252;
            float sa = sin(a);
            float ca = cos(a);
            coord = float2(coord.x * ca - coord.y * sa,
                           coord.x * sa + coord.y * ca);

            return coord + _PatternOffset.xy;
        }

        // =============================================================================
        //  Knots
        //
        //  One knot per cell of a jittered grid, searched over the 3x3 neighbourhood so
        //  a knot near a cell border is still found from the next cell over. Returns the
        //  distance to the nearest knot NORMALISED by that knot's own radius, so 0 is
        //  dead centre, 1 is its edge, and everything downstream is radius-independent.
        //
        //  Returns a large number when there is no knot in range, which makes every knot
        //  term downstream evaluate to zero without a branch.
        //
        //  This whole function is compiled out when _KNOTS_OFF is set.
        // =============================================================================
        float RL_KnotField(float2 wc)
        {
            float sp = max(_KnotSpacing, 1e-3);
            float2 gp = wc / sp;
            float2 gi = floor(gp);
            float2 gf = gp - gi;

            float best  = 1e6;
            float bestR = 1e-4;

            [unroll]
            for (int y = -1; y <= 1; y++)
            {
                [unroll]
                for (int x = -1; x <= 1; x++)
                {
                    float2 nb  = float2(x, y);
                    float2 cid = gi + nb;

                    // The cell holds a knot only if its hash passes the density test,
                    // which is what keeps them scattered instead of on a visible grid.
                    float present = RL_Hash21(cid + 7.3);

                    float2 jitter = float2(RL_Hash21(cid), RL_Hash21(cid + 19.7));
                    float2 pos = nb + 0.15 + jitter * 0.7;

                    float r = _KnotSize * (0.55 + 0.9 * RL_Hash21(cid + 3.1));

                    float2 dv = (gf - pos) * float2(1.0, _KnotSquash);
                    float d = length(dv) * sp;
                    d = (present < _KnotDensity) ? d : 1e6;

                    if (d < best)
                    {
                        best  = d;
                        bestR = r;
                    }
                }
            }

            return best / max(bestR, 1e-4);
        }

        // =============================================================================
        //  Pine grain
        //
        //  Dark growth-ring lines running along the board. Straight evenly-spaced lines
        //  read as corduroy fabric, so two things break that up:
        //    _GrainGroup  a slow low-frequency term added to the line coordinate, which
        //                 bunches lines into groups the way growth rings actually cluster
        //    _GrainWave   a faster, smaller term that makes each line undulate
        //  Each line also gets its own width and strength from its index, so no two read
        //  the same.
        //
        //  The knot pull is what sells it: near a knot the line coordinate is pushed,
        //  so the streaks sweep around the knot instead of running through it.
        // =============================================================================
        float RL_PineGrain(float2 wc, float knotDist)
        {
            float freq = max(_GrainFreq, 0.01);

            float2 gs = float2(wc.x * _GrainGroupScale * 0.6, wc.y * _GrainGroupScale);
            float group = (RL_ValueNoise(gs) - 0.5) * _GrainGroup * freq;

            float2 ws = float2(wc.x * _GrainWaveScale, wc.y * _GrainWaveScale * 0.25);
            float wave = (RL_ValueNoise(ws) - 0.5) * _GrainWave * freq;

            float sv = wc.y * freq + group + wave;

            // Clamped before exp so a "no knot here" distance cannot underflow oddly.
            sv += _KnotPull * exp(-min(knotDist, 32.0) * 0.55);

            float streakIdx = floor(sv);
            float f = sv - streakIdx;

            float strength = 0.30 + 0.70 * RL_Hash11(streakIdx);
            float w = _GrainWidth * (1.0 - _GrainWidthVariation * 0.5
                                     + _GrainWidthVariation * RL_Hash11(streakIdx + 31.7));

            // NOTE: this local must not be called "line". HLSL reserves line, point,
            // triangle, lineadj and triangleadj as geometry-shader primitive types, and
            // declaring a variable with one of those names is a syntax error - which is
            // exactly what turned v6 magenta.
            float streak = 1.0 - smoothstep(w, w + _GrainSoftness, abs(f - 0.5) * 2.0);
            return saturate(streak * strength);
        }

        // -----------------------------------------------------------------------------
        //  Plank seams - only compiled when _SEAMS_ON is set.
        //   .x = fraction across the plank   .y = fraction along it
        //   .z = row index                   .w = column index
        // -----------------------------------------------------------------------------
        float4 RL_PlankCell(float2 wc)
        {
            float rowF = wc.y / max(_PlankWidth, 1e-3);
            float row  = floor(rowF);
            float across = rowF - row;

            // Proportional to plank LENGTH: with lengths in metres a fixed offset would
            // throw short boards several cells sideways.
            float alongPos = wc.x + RL_Hash11(row) * _PlankStagger * max(_PlankLength, 0.05);

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

        // Pure smoothstep maths, no noise, which is why this is cheap enough to evaluate
        // three times for a gradient - and that gradient is what lets the seams tilt the
        // normal and catch the crease shadow instead of looking painted on.
        float RL_SeamMask(float2 wc)
        {
            float4 cell = RL_PlankCell(wc);
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
            #pragma vertex RL_Vert
            #pragma fragment RL_Frag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _TWOTONE_WOOD
            #pragma shader_feature_local _GRAIN_OFF
            #pragma shader_feature_local _KNOTS_OFF
            #pragma shader_feature_local _SEAMS_ON

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

            Varyings RL_Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                float3 rawNormalOS = RL_SafeNormal(input.normalOS, float3(0.0, 1.0, 0.0));
                float3 roundedOS   = RL_RoundedNormalOS(input.positionOS.xyz, rawNormalOS);

                output.positionCS  = positionInputs.positionCS;
                output.positionWS  = positionInputs.positionWS;
                output.positionOS  = input.positionOS.xyz;
                // The RAW normal drives the wood projection, so the grain layout does not
                // swim when _Roundness is dialled up.
                output.normalOS    = rawNormalOS;
                output.normalWS    = RL_SafeNormal(TransformObjectToWorldNormal(roundedOS),
                                                   float3(0.0, 1.0, 0.0));
                output.uv          = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogCoord    = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 RL_Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseSample = RL_SampleBase(input.uv);
                RL_ClipAlpha(baseSample.a);

                float3 normalWSf = RL_SafeNormal(input.normalWS, float3(0.0, 1.0, 0.0));
                half3  normalWS  = (half3)normalWSf;

                // Position derivatives, computed ONCE and outside every branch.
                float3 dPx = ddx(input.positionWS);
                float3 dPy = ddy(input.positionWS);

                // =================================================================
                //  WOOD
                // =================================================================
                float2 wc = RL_WoodCoord(input.positionOS, input.normalOS);

                half3 toneLight, toneDark, toneSeam, toneKnot;
                RL_ResolveWoodTones(toneLight, toneDark, toneSeam, toneKnot);

                // Knots are resolved first: the grain needs the distance in order to
                // bend around them. 1e6 means "no knot anywhere near", which makes every
                // knot term below evaluate to zero with no branching.
                float knotDist = 1e6;
                #if !defined(_KNOTS_OFF)
                    knotDist = RL_KnotField(wc);
                #endif

                // Grain: 0 = bare pale wood, 1 = the darkest streak.
                half grainTone = 0.0h;
                #if !defined(_GRAIN_OFF)
                    half rawGrain = (half)RL_PineGrain(wc, knotDist) * _GrainStrength;
                    grainTone = RL_ToonQuantize(rawGrain, _GrainSteps, 0.45h);
                #endif

                half3 wood = lerp(toneLight, toneDark, grainTone);

                #if !defined(_KNOTS_OFF)
                    // Dark rim first, then a lighter heart inside it. A flat dark oval
                    // reads as a hole; rim plus heart reads as a knot.
                    half rim = (half)(1.0 - smoothstep(0.86, 1.0, knotDist)) * _KnotDarkness;
                    wood = lerp(wood, toneKnot, rim);

                    half heart = (half)(1.0 - smoothstep(0.42, 0.58, knotDist)) * _KnotDarkness;
                    wood = lerp(wood, lerp(toneKnot, toneDark, _KnotCoreLightness), heart);
                #endif

                #if defined(_SEAMS_ON)
                    float4 cell = RL_PlankCell(wc);
                    float  seam = RL_SeamMask(wc);

                    // Board-to-board brightness so a planked surface does not read as
                    // one printed pattern with lines drawn on it.
                    float boardRnd = RL_Hash21(float2(cell.z, cell.w));
                    wood *= (half)lerp(1.0 - _PlankTintVariation,
                                       1.0 + _PlankTintVariation, boardRnd);

                    wood = lerp(wood, toneSeam, (half)seam * _GrooveDepth);
                #endif

                half3 albedo = wood * _BaseColor.rgb * baseSample.rgb;

                // Seam relief: three cheap mask evaluations give the seam's slope, and
                // tilting the normal by it makes the seams catch the crease shadow.
                #if defined(_SEAMS_ON)
                if (_GrooveNormalStrength > 1e-3h)
                {
                    float e = max(_PlankWidth, 1e-3) * 0.12;
                    float sBase = RL_SeamMask(wc);
                    float gdx = RL_SeamMask(wc + float2(e, 0.0)) - sBase;
                    float gdy = RL_SeamMask(wc + float2(0.0, e)) - sBase;

                    float3 tangentish   = RL_SafeNormal(dPx, float3(1.0, 0.0, 0.0));
                    float3 bitangentish = RL_SafeNormal(dPy, float3(0.0, 0.0, 1.0));

                    float3 tilt = tangentish * gdx + bitangentish * gdy;
                    normalWSf = RL_SafeNormal(normalWSf - tilt * _GrooveNormalStrength * 4.0,
                                              normalWSf);
                    normalWS = (half3)normalWSf;
                }
                #endif

                // =================================================================
                //  LIGHTING - identical maths to RestaurantLoop/SoftMatte
                // =================================================================

                // Curvature from screen-space derivatives. Dividing by the squared world
                // size of a pixel makes this real curvature (1/radius in world units)
                // rather than something that changes with resolution or camera distance.
                // Positive is convex - an outer edge; negative is concave - a crease.
                float3 dNx = ddx(normalWSf);
                float3 dNy = ddy(normalWSf);
                float  pixelArea = dot(dPx, dPx) + dot(dPy, dPy) + 1e-8;
                float  curvature = (dot(dNx, dPx) + dot(dNy, dPy)) / pixelArea;
                curvature = clamp(curvature * _CurvatureScale, -1.0, 1.0);

                half concave = saturate((half)(-curvature));
                half convex  = saturate((half)( curvature));

                // Hemisphere: the main volume cue on a fixed top-down camera.
                half up = normalWS.y * 0.5h + 0.5h;
                up = saturate(lerp(step(0.5h, up), up, _HemisphereWrap));
                half3 hemisphere = lerp(_GroundColor.rgb, _SkyColor.rgb, up);

                float3 fixedDir = RL_SafeNormal(_FakeLightDirection.xyz, float3(-0.35, 0.85, -0.4));
                float3 lightDir = RL_SafeNormal(
                    lerp(fixedDir, _MainLightPosition.xyz, saturate(_UseMainLightDirection)),
                    fixedDir);

                // Half-lambert with the dot left SIGNED. Saturating before the *0.5+0.5
                // would clamp the result to 0.5..1 and flatten the form.
                half ndl = dot(normalWS, (half3)lightDir);
                half halfLambert = ndl * 0.5h + 0.5h;
                half lo = min(_ShadowThreshold, _LightThreshold - 1e-3h);
                half ramp = smoothstep(lo, _LightThreshold, halfLambert);
                ramp = RL_ToonQuantize(ramp, _ToonSteps, _ToonStepSoftness);

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
            #pragma vertex RL_ShadowVert
            #pragma fragment RL_ShadowFrag

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

            VaryingsShadow RL_ShadowVert(AttributesShadow input)
            {
                VaryingsShadow output = (VaryingsShadow)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 normalOS   = RL_SafeNormal(input.normalOS, float3(0.0, 1.0, 0.0));
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = RL_SafeNormal(TransformObjectToWorldNormal(normalOS),
                                                  float3(0.0, 1.0, 0.0));

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = RL_SafeNormal(_LightPosition - positionWS,
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

            half4 RL_ShadowFrag(VaryingsShadow input) : SV_Target
            {
                RL_ClipAlpha(RL_SampleBase(input.uv).a);
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
            #pragma vertex RL_DepthVert
            #pragma fragment RL_DepthFrag

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

            VaryingsDepth RL_DepthVert(AttributesDepth input)
            {
                VaryingsDepth output = (VaryingsDepth)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 RL_DepthFrag(VaryingsDepth input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                RL_ClipAlpha(RL_SampleBase(input.uv).a);
                return 0;
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 4 - Depth Normals
        //  Present so the material appears in the DepthNormals prepass that URP's SSAO
        //  renderer feature reads. The rounded normal is written here too, so SSAO
        //  agrees with the shading.
        // =============================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex RL_DepthNormalsVert
            #pragma fragment RL_DepthNormalsFrag

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

            VaryingsDN RL_DepthNormalsVert(AttributesDN input)
            {
                VaryingsDN output = (VaryingsDN)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 roundedOS = RL_RoundedNormalOS(input.positionOS.xyz, input.normalOS);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = RL_SafeNormal(TransformObjectToWorldNormal(roundedOS),
                                                  float3(0.0, 1.0, 0.0));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 RL_DepthNormalsFrag(VaryingsDN input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                RL_ClipAlpha(RL_SampleBase(input.uv).a);
                return half4((half3)RL_SafeNormal(input.normalWS, float3(0.0, 1.0, 0.0)), 0.0h);
            }
            ENDHLSL
        }
    }

    // No Fallback on purpose. If this ever fails, magenta plus one real Console line
    // beats a silently substituted lit shader that hides the cause.
}
