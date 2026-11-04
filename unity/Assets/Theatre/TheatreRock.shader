// The mushroom reefs' rock (logbook/specs/reef-spec.md; the owner asked on the night of 2026-09-23
// for an appropriate skin for the reefs, so they look more like a natural rocky structure).
//
// The bed's shader's family, and deliberately so: the same lighting (a wrapped main light, the
// additional lights, spherical harmonics), the same caustic net from TheatreWater.hlsl and the
// same column fog, so the rock and the sand read as one sea floor. What differs is the surface.
//
// The shape is in the mesh, not here. TheatreReefRock cuts the cap and the stem inward on the
// CPU, seeded per reef, because a vertex carve in world y (the bed's) would pull a stem's wall
// or a cap's rim out of its own shape; so this shader carves nothing and every drawn point stays
// at or inside the rock the physics pushes bodies by.
//
// The detail normal is the bed's triplanar with its whiteout blend [BG], with a rock's height in
// place of the sand's ripples, projected in the reef's own frame (object space: every reef's
// holder sits on its axis, unrotated), so nothing stretches on the stem and the grain does not
// slide over a reef the camera moves round. The colour's mottle is a solid field in the same
// frame rather than a projected one: a projection's seams show in colour where they hide in a
// normal, and a solid field has none.
//
// The tone: a lighter crust on the lit table that darkens down the rim, a darker and colder
// underside and stem, a cavity term from how deep the mesh was cut (vertex colour red), and the
// cap's shadow on everything under it. The colours arrive from TheatreSkin as linear values set
// with SetVector, so none of them goes through the engine's sRGB conversion twice or not at all.

Shader "Evosim/Theatre Rock"
{
    Properties
    {
        _RockDeep("Rock, underside and stem (linear)", Vector) = (0.0012, 0.0016, 0.0024, 1)
        _RockMid("Rock, rim (linear)", Vector) = (0.0052, 0.0056, 0.0062, 1)
        _RockCrust("Rock, encrusted table (linear)", Vector) = (0.022, 0.021, 0.017, 1)

        _MottleMetres("Metres per mottle patch", Range(0.1, 10)) = 1.4
        _MottleStrength("Mottle strength", Range(0, 1)) = 0.55
        _GrainMetres("Metres per grain", Range(0.02, 2)) = 0.2
        _GrainStrength("Grain strength", Range(0, 3)) = 2.0
        _CavityDark("How much a deep cut darkens", Range(0, 1)) = 0.6

        _CausticColor("Caustic colour", Color) = (0.55, 0.85, 0.95, 1)
        _CausticStrength("Caustic strength", Range(0, 3)) = 0.35
        _CausticReach("Metres below the surface caustics reach", Range(1, 400)) = 18

        _ShadeUnder("Main light left under the cap", Range(0, 1)) = 0.3

        // The glint (EVOSIM_THEATRE_ROCK_GLINT; D208, item 3): the sun's highlight off the wet stone, a
        // reflectance at the lobe's peak (0: none, as before 2026-10-19), and its roughness.
        _GlintStrength("Glint, peak reflectance", Range(0, 0.5)) = 0
        _GlintRoughness("Glint roughness", Range(0.05, 1)) = 0.35
        _Wrap("Diffuse wrap", Range(0, 1)) = 0.3

        // Per reef, through a MaterialPropertyBlock: the reef frame's cap top and underside
        // heights, its largest radius, and its seed in [0, 1).
        _CapTopLocal("Cap top, reef frame", Float) = 1
        _CapUnderLocal("Cap underside, reef frame", Float) = -1
        _CapRadius("Cap radius", Float) = 4
        _RockSeed("Seed", Float) = 0

        // Living coral (EVOSIM_THEATRE_CORAL_LIVE): round 62's colonies only, through the same
        // block. 0 is the rock as it was, to the pixel. The tints are linear.
        _Living("Living coral share", Range(0, 1)) = 0
        _LiveTint("Living tissue (linear)", Vector) = (0.040, 0.028, 0.018, 1)
        _LiveTip("Growing tips (linear)", Vector) = (0.070, 0.062, 0.048, 1)
        _PolypMetres("Metres per corallite", Range(0.01, 0.5)) = 0.14
        // The crown's warm colour (rgb, linear) and its share (w): EVOSIM_THEATRE_CORAL_WARM. w 0 is off.
        _LiveWarm("Warm crown (linear), share in w", Vector) = (0, 0, 0, 0)

        // The coral's shape, further (EVOSIM_THEATRE_CORAL_SHAPE at 2): corallite bumps along the
        // branches and growth rings and radial ridges on the plates, as relief. 0 is off, the rock
        // as it was; the mesh's texture coordinates say which piece a point is on.
        _CoralForm("Coral form relief (0 off)", Float) = 0
        _BumpMetres("Metres per corallite bump", Range(0.005, 0.5)) = 0.035

        // Bare skeleton below the living tissue (EVOSIM_THEATRE_CORAL_LIVE at 2): its share (0 is off),
        // its colour (linear), and the age (1 at the trunk's foot, 0 at the youngest tip) above
        // which the colony is bare.
        _Bare("Bare skeleton share", Range(0, 1)) = 0
        _BareTint("Bare skeleton (linear)", Vector) = (0.075, 0.070, 0.060, 1)
        _BareEdge("Bare below this age", Range(0, 1)) = 0.6

        // The polyps out (EVOSIM_THEATRE_CORAL_POLYP_TUFTS): a tuft of tentacles in each corallite's
        // cup and a soft fringe at the silhouette, on living tissue only. 0 is off, the coral as it was.
        _Polyps("Polyp tufts (0 off)", Float) = 0

        // Bright living tissue (EVOSIM_THEATRE_CORAL_LIVE at 3): the tissue drawn from the brighter tints the
        // block carries, with pale tips where the bush's mesh marks them (the second texture coordinate's y)
        // and a softer wrap. 0 is off, the coral as it was.
        _Vivid("Bright living tissue (0 off)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // The material and the mesh's inputs, shared by the forward pass and the two depth
        // passes (2026-09-24). The rock is not displaced here, so the depth passes draw the mesh
        // as it is; they are explicit rather than the fallback's so the depth texture's rock is
        // this shader's own, two sided as the forward pass is.
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TheatreWater.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RockDeep;
                float4 _RockMid;
                float4 _RockCrust;
                float _MottleMetres;
                float _MottleStrength;
                float _GrainMetres;
                float _GrainStrength;
                float _CavityDark;
                float4 _CausticColor;
                float _CausticStrength;
                float _CausticReach;
                float _ShadeUnder;
                float _GlintStrength;
                float _GlintRoughness;
                float _Wrap;
                float _CapTopLocal;
                float _CapUnderLocal;
                float _CapRadius;
                float _RockSeed;
                float _Living;
                float4 _LiveTint;
                float4 _LiveTip;
                float _PolypMetres;
                float4 _LiveWarm;
                float _CoralForm;
                float _BumpMetres;
                float _Bare;
                float4 _BareTint;
                float _BareEdge;
                float _Polyps;
                float _Vivid;
            CBUFFER_END

            // The texture coordinates are the coral's (TheatreCoralRock.Colony), written only for the
            // further looks: the piece (a plate's centre x and z, its radius, 1; a capsule's 0, 0, its
            // radius, 0) and the age, with the bush's tip reading beside it (0 without the bush). Read
            // only behind _CoralForm, _Bare and _Vivid.
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float4 piece      : TEXCOORD0;
                float2 age        : TEXCOORD1;
            };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Two sided, for the bed's reason: the fly camera can go inside the rock.
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0

            #pragma multi_compile _ _EVO_CAUSTIC_VARIETY
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float4 color      : TEXCOORD3;
                float fogFactor   : TEXCOORD4;
                float4 piece      : TEXCOORD5;
                float2 age        : TEXCOORD6;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                output.positionWS = positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.color = input.color;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.piece = input.piece;
                output.age = input.age;

                return output;
            }

            // Two-dimensional value noise, the bed's grain, in [0, 1].
            float Value2(float2 q)
            {
                float2 cell = floor(q);
                float2 f = q - cell;
                f = f * f * (3.0 - 2.0 * f);

                float a = EvoHash1(cell);
                float b = EvoHash1(cell + float2(1.0, 0.0));
                float c = EvoHash1(cell + float2(0.0, 1.0));
                float d = EvoHash1(cell + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // A rock's grain on one plane: two octaves of lumps and a crease where a slow field
            // crosses its middle, which reads as a fracture line at a distance.
            float RockHeight(float2 p)
            {
                float lumps = 0.65 * Value2(p) + 0.35 * Value2(p * 2.4 + 7.1);
                float crease = 1.0 - saturate(abs(Value2(p * 0.37 + 3.3) - 0.5) * 9.0);

                return lumps - 0.25 * crease;
            }

            float3 RockNormal(float2 p)
            {
                const float e = 0.03;

                float h = RockHeight(p);
                float dx = RockHeight(p + float2(e, 0.0)) - h;
                float dy = RockHeight(p + float2(0.0, e)) - h;

                return normalize(float3(-dx / e * _GrainStrength, -dy / e * _GrainStrength, 6.0));
            }

            float Wrapped(float3 n, float3 l, float wrap)
            {
                return saturate((dot(n, l) + wrap) / (1.0 + wrap));
            }

            // The corallites' relief: each cell a cup, raised at its wall and sunk at its mouth, as
            // a height field differenced along two tangents of the surface, so the cups catch the
            // light as bumps and not only as paint. Shading only; the mesh is not moved.
            float CupHeight(float3 q)
            {
                float f1, f2, tone;
                EvoVoronoi(q, f1, f2, tone);
                float wall = 1.0 - smoothstep(0.0, 0.15, f2 - f1);
                return smoothstep(0.05, 0.45, f1) + 0.5 * wall;
            }

            float3 CupRelief(float3 local, float3 geometric, float3 n, float share)
            {
                float3 q = local / max(0.01, _PolypMetres);
                float3 helper = abs(geometric.y) < 0.9 ? float3(0, 1, 0) : float3(1, 0, 0);
                float3 t1 = normalize(cross(geometric, helper));
                float3 t2 = cross(geometric, t1);

                const float e = 0.08;
                float h = CupHeight(q);
                float d1 = (CupHeight(q + t1 * e) - h) / e;
                float d2 = (CupHeight(q + t2 * e) - h) / e;

                return normalize(n - share * 0.35 * (d1 * t1 + d2 * t2));
            }

            // Living coral's tissue over the colony's stone: a corallite in every Voronoi cell, its
            // mouth darker and its wall paler, the tissue's own slow mottle, and paler growing tips
            // toward the crown and on what faces up. Brightness stays near the crust's, lit by the
            // same light, so the tissue takes the water's light and is never self-lit.
            float3 LivingTissue(float3 local, float height, float cavity, float3 n, float worldY)
            {
                float f1, f2, tone;
                EvoVoronoi(local / max(0.01, _PolypMetres), f1, f2, tone);

                // The cup: dark at the mouth, a pale ridge where two cells meet.
                float mouth = 1.0 - smoothstep(0.0, 0.32, f1);
                float wall = 1.0 - smoothstep(0.0, 0.12, f2 - f1);

                float3 g1, g2;
                float slow = EvoValueNoise(local / 0.9 + 23.0, g1);
                float fine = EvoValueNoise(local / 0.25 + 41.0, g2);

                float3 tissue = _LiveTint.rgb * lerp(0.75, 1.25, slow);
                // A warmer and a cooler patch, so a colony is not one flat paint.
                tissue *= lerp(float3(1.15, 0.95, 0.80), float3(0.85, 1.02, 1.20), smoothstep(0.3, 0.7, fine));

                float tip = smoothstep(0.55, 1.0, height) * (0.45 + 0.55 * saturate(n.y)) * smoothstep(0.35, 0.65, slow);
                tissue = lerp(tissue, _LiveTip.rgb, saturate(tip));

                // The warm crown: in the top few metres (the surface is y = 0), where the water
                // still carries red, the colony's own warm colour over patches of its tissue.
                if (_LiveWarm.w > 0.001)
                {
                    float3 g3;
                    float patches = EvoValueNoise(local / 1.6 + 67.0, g3);
                    float shallow = smoothstep(-9.0, -3.0, worldY);
                    float share = _LiveWarm.w * shallow * smoothstep(0.3, 0.6, patches);
                    tissue = lerp(tissue, _LiveWarm.rgb * lerp(0.85, 1.15, slow), saturate(share));
                }

                tissue *= 1.0 - 0.6 * mouth;
                tissue *= 1.0 + 0.45 * wall;
                tissue *= 0.8 + 0.4 * tone;
                tissue *= 1.0 - 0.5 * cavity * cavity;

                return tissue;
            }

            // The further looks' constants (EVOSIM_THEATRE_CORAL_SHAPE and _LIVE at 2).
            static const float BumpRound = 0.5;           // a corallite bump's radius, in cells
            static const float BumpTilt = 0.25;           // how far a bump tilts the normal
            static const float PlateRingMetres = 0.14;    // between a plate's growth rings
            static const float PlateRingHeight = 0.004;   // a growth ring's height, m
            static const float PlateRidgeMetres = 0.09;   // between radial ridges at the rim
            static const float PlateRidgeHeight = 0.003;  // a radial ridge's height, m
            static const float PlateTilt = 1.5;           // how far the plate's slopes tilt the normal
            static const float ReliefFadeFrom = 0.25;     // cells per pixel at which relief starts to fade
            static const float ReliefFadeTo = 0.6;        // and by which it is gone, so it never shimmers
            static const float BareBand = 0.05;           // half the soft band between bare and living, in age
            static const float BareWander = 0.12;         // how far the border wanders along a branch, in age
            static const float BareWanderMetres = 0.8;    // the wander's scale along a branch
            static const float BareHeightShare = 0.35;    // how much height above the foot counts with age

            // A corallite bump: a rounded dome over each Voronoi cell's middle, low between cells.
            float BumpHeight(float3 q)
            {
                float f1, f2, tone;
                EvoVoronoi(q, f1, f2, tone);
                return saturate(1.0 - f1 * f1 / (BumpRound * BumpRound));
            }

            // The corallite bumps along a branch, as relief: the domes differenced along two tangents,
            // as the cups are, at a few per 10 cm. The mesh is not moved, so nothing passes the rock.
            float3 BumpRelief(float3 local, float3 geometric, float3 n, float fade)
            {
                float3 q = local / max(0.005, _BumpMetres);
                float3 helper = abs(geometric.y) < 0.9 ? float3(0, 1, 0) : float3(1, 0, 0);
                float3 t1 = normalize(cross(geometric, helper));
                float3 t2 = cross(geometric, t1);

                const float e = 0.08;
                float h = BumpHeight(q);
                float d1 = (BumpHeight(q + t1 * e) - h) / e;
                float d2 = (BumpHeight(q + t2 * e) - h) / e;

                return normalize(n - fade * BumpTilt * (d1 * t1 + d2 * t2));
            }

            // A plate's growth rings and radial ridges, as a height in metres over the plate's own
            // centre: rings a little wavering, ridges fading in from the middle toward the rim, their
            // count whole so they close round the plate without a seam.
            float PlateHeight(float2 offset, float radius, float salt)
            {
                float rho = length(offset);
                float3 g;
                float waver = EvoValueNoise(float3(offset.x, salt, offset.y) / 0.45, g) - 0.5;
                float rings = 0.5 + 0.5 * cos(6.2831853 * (rho / PlateRingMetres + 0.6 * waver));

                float count = max(8.0, round(6.2831853 * radius / PlateRidgeMetres));
                float ridge = 0.5 + 0.5 * cos(count * atan2(offset.y, offset.x));
                float outer = smoothstep(0.25, 0.7, rho / max(0.05, radius));

                return PlateRingHeight * rings + PlateRidgeHeight * ridge * ridge * ridge * outer;
            }

            float3 PlateRelief(float3 positionOS, float4 piece, float3 geometric, float3 n, float fade)
            {
                float2 offset = positionOS.xz - piece.xy;
                float radius = piece.z;
                float salt = piece.x * 0.37 + piece.y * 0.73;

                const float e = 0.01;
                float h = PlateHeight(offset, radius, salt);
                float dx = (PlateHeight(offset + float2(e, 0.0), radius, salt) - h) / e;
                float dz = (PlateHeight(offset + float2(0.0, e), radius, salt) - h) / e;

                // On the faces only: the rim's round edge faces sideways and keeps its own shading.
                float faces = abs(geometric.y);
                return normalize(n - fade * faces * PlateTilt * float3(dx, 0.0, dz));
            }

            // How bare a point is: by its age along the colony (1 at the trunk's foot, 0 at the
            // youngest tip) and a little by its height, against this colony's border, which wanders
            // along each branch and is a soft band rather than a line.
            float BareShare(float3 local, float height, float age)
            {
                float3 g;
                float wander = EvoValueNoise(local / BareWanderMetres + 113.0, g);
                float older = lerp(age, 1.0 - height, BareHeightShare);
                float border = _BareEdge + BareWander * (wander - 0.5);

                return smoothstep(border - BareBand, border + BareBand, older);
            }

            // The bare skeleton: a dull off-white with a slow grime and the stone's fine grain, darker
            // in the mesh's cuts. No cup is painted on it; the cups stay as relief only.
            float3 BareSkeleton(float3 local, float cavity)
            {
                float3 g1, g2;
                float grime = EvoValueNoise(local / 0.6 + 89.0, g1);
                float grain = EvoValueNoise(local / 0.07 + 97.0, g2);

                float3 bone = _BareTint.rgb * lerp(0.82, 1.08, grime);
                bone *= 0.9 + 0.2 * grain;
                bone *= 1.0 - 0.5 * cavity * cavity;

                return bone;
            }

            // The bright tissue's constants (EVOSIM_THEATRE_CORAL_LIVE at 3).
            static const float VividSlow = 0.15;          // how far the slow mottle moves the tissue's value, either way
            static const float VividMouth = 0.35;         // how much a corallite's mouth darkens the tissue (LivingTissue's 0.6)
            static const float VividWall = 0.3;           // how much its wall lightens it (LivingTissue's 0.45)
            static const float VividTone = 0.2;           // the spread of a corallite's own value
            static const float VividCrown = 0.2;          // the most of the tip colour the crown's upward faces take, beside the marked tips
            static const float VividTipFrom = 0.15;       // the tip reading at which the pale tip colour begins
            static const float VividTipTo = 0.8;          // and by which it is whole
            static const float VividTipSmooth = 0.7;      // how much of the mouth's darkening a pale tip loses: a growing tip is smooth
            static const float VividWrap = 0.7;           // the tissue's wrap: light spreads in the living layer, so the shaded side keeps some

            // The bright living tissue: LivingTissue's corallites, mottle and warm crown over the brighter tints,
            // with shallower cups, and the pale tip colour where the bush's mesh marks a tip (a prong's end whole,
            // a sprig's end in part) and, much less, on the crown's upward faces. Lit by the same light as all the
            // rock, never self-lit: its value comes from the tint, so the water and the fog still have the last word.
            float3 VividTissue(float3 local, float height, float cavity, float3 n, float tipMark, float worldY)
            {
                float f1, f2, tone;
                EvoVoronoi(local / max(0.01, _PolypMetres), f1, f2, tone);

                float mouth = 1.0 - smoothstep(0.0, 0.32, f1);
                float wall = 1.0 - smoothstep(0.0, 0.12, f2 - f1);

                float3 g1, g2;
                float slow = EvoValueNoise(local / 0.9 + 23.0, g1);
                float fine = EvoValueNoise(local / 0.25 + 41.0, g2);

                float3 tissue = _LiveTint.rgb * lerp(1.0 - VividSlow, 1.0 + VividSlow, slow);
                tissue *= lerp(float3(1.08, 0.98, 0.90), float3(0.92, 1.01, 1.10), smoothstep(0.3, 0.7, fine));

                float marked = smoothstep(VividTipFrom, VividTipTo, tipMark);
                float crown = VividCrown * smoothstep(0.6, 1.0, height) * saturate(n.y) * smoothstep(0.4, 0.7, slow);
                tissue = lerp(tissue, _LiveTip.rgb, saturate(max(marked, crown)));

                // The warm crown, as LivingTissue lays it.
                if (_LiveWarm.w > 0.001)
                {
                    float3 g3;
                    float patches = EvoValueNoise(local / 1.6 + 67.0, g3);
                    float shallow = smoothstep(-9.0, -3.0, worldY);
                    float share = _LiveWarm.w * shallow * smoothstep(0.3, 0.6, patches) * (1.0 - marked);
                    tissue = lerp(tissue, _LiveWarm.rgb * lerp(0.85, 1.15, slow), saturate(share));
                }

                tissue *= 1.0 - VividMouth * mouth * (1.0 - VividTipSmooth * marked);
                tissue *= 1.0 + VividWall * wall;
                tissue *= (1.0 - 0.5 * VividTone) + VividTone * tone;
                tissue *= 1.0 - 0.5 * cavity * cavity;

                return tissue;
            }

            // The polyps' constants (EVOSIM_THEATRE_CORAL_POLYP_TUFTS). Lengths in cells are in corallite
            // cells (_PolypMetres), the cups' own scale.
            static const float TuftInner = 0.05;          // where the strands leave the mouth, in cells
            static const float TuftRootRamp = 0.06;       // over which they rise out of it, in cells
            static const float TuftReach = 0.42;          // where they end, in cells
            static const float TuftTipRamp = 0.05;        // over which they fade at the end, in cells
            static const float TuftWallClear = 0.12;      // how far short of a cell wall they stop, in f2 - f1
            static const float TuftStrandsLow = 10.0;     // strands round the fewest-armed polyp
            static const float TuftStrandsSpread = 6.0;   // and how many more the most-armed one may have
            static const float TuftStrandWidth = 0.035;   // a strand's half width at its root, in cells
            static const float TuftTaper = 0.5;           // the share of that width left at its tip
            static const float TuftSoften = 0.5;          // the share of a pixel's footprint a strand widens by
            static const float TuftKnob = 0.75;           // the share of a strand's length where its pale tip begins
            static const float TuftCover = 0.7;           // how much of the tissue a strand hides; the rest shows through
            static const float TuftPale = 1.35;           // a strand's brightness over the tip colour
            static const float TuftKnobPale = 1.8;        // and its tip's
            static const float TuftWash = 0.3;            // how far a strand's colour washes toward grey, as a thin clear thing's does
            static const float TuftWrap = 0.8;            // a strand's wrap: light spreads through it
            static const float TuftRise = 0.25;           // a strand's height as a share of its half width, for the relief
            static const float TuftSway = 0.07;           // how far a strand's tip wavers, radians (four degrees)
            static const float TuftSwaySeconds = 7.0;     // the waver's period
            static const float TuftSwayMetres = 1.5;      // the distance across a colony over which its phase turns once
            static const float TuftFadeFrom = 0.1;        // cells per pixel at which the tufts start to fade
            static const float TuftFadeTo = 0.3;          // and by which they are gone, so a far colony never shimmers
            static const float TuftFringe = 0.3;          // the fringe's share at the silhouette itself
            static const float TuftFringeSharp = 3.0;     // how close to the silhouette it keeps
            static const float TuftFringePale = 1.4;      // its brightness over the tip colour

            // The corallite cells again, the same field at the same scale as LivingTissue's and CupHeight's,
            // and chosen by the same comparisons, so a tuft sits in the cup the relief draws. It keeps the
            // way to the nearest feature point, the cup's centre a tuft grows from, which EvoVoronoi drops.
            void TuftCell(float3 p, out float3 toCentre, out float f1, out float f2, out float tone)
            {
                float3 cell = floor(p);
                float3 frc = p - cell;

                toCentre = float3(0.0, 0.0, 0.0);
                f1 = 8.0;
                f2 = 8.0;
                tone = 0.5;

                [unroll]
                for (int x = -1; x <= 1; x++)
                {
                    [unroll]
                    for (int y = -1; y <= 1; y++)
                    {
                        [unroll]
                        for (int z = -1; z <= 1; z++)
                        {
                            float3 offset = float3(x, y, z);
                            float3 seed = EvoHash3(cell + offset);
                            float3 rel = offset + seed - frc;

                            float d = dot(rel, rel);

                            if (d < f1)
                            {
                                f2 = f1;
                                f1 = d;
                                tone = seed.x;
                                toCentre = rel;
                            }
                            else if (d < f2)
                            {
                                f2 = d;
                            }
                        }
                    }
                }

                f1 = sqrt(f1);
                f2 = sqrt(f2);
            }

            // A polyp's tuft at a point: how much a strand covers it, how far along to its pale tip it is
            // (knob), and the strands' slope in the surface, in cells per cell, for the relief. The strands
            // are counted round the cup's centre in the surface's own plane, so they lie flat on a branch
            // however it turns, and measured across along the arc, so each keeps its width outward rather
            // than fanning into a wedge. A strand widens by part of the pixel's footprint (soften, in cells)
            // with its strength cut to match, so one thinner than a pixel greys out rather than flickers.
            // The strands waver a few degrees in time, their tips more than their roots, in a slow wave
            // across the colony phased by where each cup sits; EvoTime, so a film rendered twice matches.
            float Tuft(float3 local, float3 geometric, float soften, out float knob, out float3 slope)
            {
                float3 q = local / max(0.01, _PolypMetres);
                float3 toCentre;
                float f1, f2, tone;
                TuftCell(q, toCentre, f1, f2, tone);

                float3 helper = abs(geometric.y) < 0.9 ? float3(0, 1, 0) : float3(1, 0, 0);
                float3 t1 = normalize(cross(geometric, helper));
                float3 t2 = cross(geometric, t1);

                // From the cup's centre to here, in the surface's plane.
                float x = -dot(toCentre, t1);
                float y = -dot(toCentre, t2);
                float r = sqrt(x * x + y * y);
                float3 outward = (x * t1 + y * t2) / max(1e-4, r);
                float3 around = cross(geometric, outward);

                float along = saturate((r - TuftInner) / (TuftReach - TuftInner));

                float3 centre = (q + toCentre) * _PolypMetres;
                float phase = 6.2831853 * (EvoTime() / TuftSwaySeconds + dot(centre, float3(0.8, 0.5, 0.6)) / TuftSwayMetres + 0.3 * tone);
                float sway = TuftSway * along * sin(phase);

                // The nearest strand's axis, in radians, with the star turned a little per polyp.
                float count = TuftStrandsLow + floor(tone * TuftStrandsSpread);
                float turn = (atan2(y, x) + sway) * count / 6.2831853 + 7.0 * tone;
                float delta = (frac(turn + 0.5) - 0.5) * 6.2831853 / count;

                float own = TuftStrandWidth * lerp(1.0, TuftTaper, along);
                float width = own + TuftSoften * soften;
                float s = saturate(r * abs(delta) / width);
                float strand = 1.0 - s * s * (3.0 - 2.0 * s);
                float dStrand = -6.0 * s * (1.0 - s) / width;
                float thin = own / width;

                float body = smoothstep(TuftInner, TuftInner + TuftRootRamp, r) * (1.0 - smoothstep(TuftReach - TuftTipRamp, TuftReach, r));
                body *= smoothstep(0.0, TuftWallClear, f2 - f1);

                knob = smoothstep(TuftKnob, 1.0, along);
                slope = (TuftRise * width * body * dStrand * thin) * (sign(delta) * around + abs(delta) * outward);

                return body * strand * thin;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 geometric = normalize(input.normalWS);
                float3 seed = EvoCarveOffset(_RockSeed);
                float3 local = input.positionOS + seed;

                // The grain: the bed's triplanar, whiteout blended, in the reef's frame.
                float3 blend = pow(abs(geometric), 4.0);
                blend /= max(1e-4, blend.x + blend.y + blend.z);

                float3 p = local / max(0.02, _GrainMetres);

                float3 nx = RockNormal(p.zy);
                float3 ny = RockNormal(p.xz);
                float3 nz = RockNormal(p.xy);

                float3 detail =
                    blend.x * float3(nx.z * sign(geometric.x), nx.y, nx.x) +
                    blend.y * float3(ny.x, ny.z * sign(geometric.y), ny.y) +
                    blend.z * float3(nz.x, nz.y, nz.z * sign(geometric.z));

                // The detail comes back around each projection's own axis; carry the geometric
                // normal's departure from that axis so the cut's big shapes still shade.
                float3 axis = blend.x * float3(sign(geometric.x), 0, 0)
                            + blend.y * float3(0, sign(geometric.y), 0)
                            + blend.z * float3(0, 0, sign(geometric.z));
                float3 n = normalize(detail + (geometric - axis));

                // The tone. Green is the analytic surface's upward component (1 on the table),
                // blue is 1 on the cap, red how deep the mesh was cut there.
                float cavity = input.color.r;
                float table = saturate(input.color.g * 2.0 - 1.0);
                float onCap = input.color.b;

                float y = input.positionOS.y;
                float thickness = max(0.05, _CapTopLocal - _CapUnderLocal);
                float height = saturate((y - _CapUnderLocal) / thickness);

                float3 g1, g2;
                float m1 = EvoValueNoise(local / max(0.1, _MottleMetres), g1);
                float m2 = EvoValueNoise(local / max(0.1, 0.33 * _MottleMetres) + 11.0, g2);
                float mottle = 0.65 * m1 + 0.35 * m2;

                // Down the rim from the table to the underside, cold dark to rock grey.
                float3 rock = lerp(_RockDeep.rgb, _RockMid.rgb, onCap * smoothstep(0.0, 1.0, height));

                // The crust: on what faces up on the cap's top, in patches, thinned into the cuts.
                float crust = onCap * saturate(n.y) * smoothstep(0.35, 0.95, height) * (0.35 + 0.65 * table);
                crust *= smoothstep(0.30, 0.62, mottle) * (1.0 - 0.6 * cavity);
                rock = lerp(rock, _RockCrust.rgb, saturate(crust));

                rock *= lerp(1.0 - _MottleStrength * 0.5, 1.0 + _MottleStrength * 0.5, m2);

                // A speckle under the patches, the grain of the stone itself, finer than the mesh.
                float3 g3;
                float speck = EvoValueNoise(local / 0.07, g3);
                rock *= 0.8 + 0.4 * speck;
                rock *= 1.0 - _CavityDark * cavity * cavity;

                // Living coral: tissue over the stone, a smoother surface (soft tissue over the
                // skeleton's grain) and a softer wrap, as light spreads in a thin living layer.
                float wrap = _Wrap;
                if (_Living > 0.001)
                {
                    float3 stone = rock;
                    rock = lerp(rock, LivingTissue(local, height, cavity, n, input.positionWS.y), _Living);
                    n = normalize(lerp(n, geometric, 0.25 * _Living));
                    n = CupRelief(local, geometric, n, _Living);
                    wrap = lerp(_Wrap, 0.55, _Living);

                    // The bright tissue (EVOSIM_THEATRE_CORAL_LIVE at 3) in place of the tissue above, with its
                    // pale marked tips and its softer wrap; the bare foot below still takes the older parts.
                    [branch] if (_Vivid > 0.5)
                    {
                        rock = lerp(stone, VividTissue(local, height, cavity, n, input.age.y, input.positionWS.y), _Living);
                        wrap = lerp(_Wrap, VividWrap, _Living);
                    }

                    // The bare skeleton (EVOSIM_THEATRE_CORAL_LIVE at 2): the lower, older parts of the
                    // colony show off-white skeleton through the tissue, keeping the cups' relief
                    // above and the stone's wrap, as bare skeleton holds no living layer.
                    if (_Bare > 0.001)
                    {
                        float bare = _Bare * BareShare(local, height, input.age.x);
                        rock = lerp(rock, BareSkeleton(local, cavity), bare);
                        wrap = lerp(wrap, _Wrap, bare);
                    }
                }

                // The coral's shape, further (EVOSIM_THEATRE_CORAL_SHAPE at 2): corallite bumps on the
                // branches, growth rings and radial ridges on the plates, fading out where a cell
                // falls under a few pixels. The footprint is taken outside the branch, where a
                // derivative is always defined.
                float footprint = length(fwidth(input.positionOS));
                [branch] if (_CoralForm > 0.5)
                {
                    if (input.piece.w > 0.5)
                    {
                        float reliefFade = 1.0 - smoothstep(ReliefFadeFrom, ReliefFadeTo, footprint / PlateRidgeMetres);
                        n = PlateRelief(input.positionOS, input.piece, geometric, n, reliefFade);
                    }
                    else
                    {
                        float reliefFade = 1.0 - smoothstep(ReliefFadeFrom, ReliefFadeTo, footprint / max(0.005, _BumpMetres));
                        n = BumpRelief(local, geometric, n, reliefFade);
                    }
                }

                // The polyps out (EVOSIM_THEATRE_CORAL_POLYP_TUFTS): a tuft in each cup, over the cups and
                // the bumps as a polyp sits on its corallite, and a soft fringe where the surface turns
                // from the eye, as the tentacles along an edge catch the light. Living tissue only: none
                // on the bare skeleton or the stone. The tufts go where a corallite falls under a few
                // pixels, by the footprint above; the fringe is a soft edge, not a pattern, and stays.
                [branch] if (_Polyps > 0.5 && _Living > 0.001)
                {
                    float alive = _Living;
                    if (_Bare > 0.001) alive *= 1.0 - _Bare * BareShare(local, height, input.age.x);

                    // Paler and clearer than the tissue: the colony's tip colour, washed toward grey.
                    float grey = (_LiveTip.r + _LiveTip.g + _LiveTip.b) / 3.0;
                    float3 tipColour = lerp(_LiveTip.rgb, float3(grey, grey, grey), TuftWash);

                    float cellsPerPixel = footprint / max(0.01, _PolypMetres);
                    float tuftFade = 1.0 - smoothstep(TuftFadeFrom, TuftFadeTo, cellsPerPixel);
                    if (tuftFade > 0.001)
                    {
                        float knob;
                        float3 slope;
                        float cover = Tuft(local, geometric, cellsPerPixel, knob, slope) * TuftCover * tuftFade * alive;
                        rock = lerp(rock, tipColour * lerp(TuftPale, TuftKnobPale, knob), cover);
                        wrap = lerp(wrap, TuftWrap, cover);
                        n = normalize(n - tuftFade * alive * slope);
                    }

                    float3 eye = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    float edge = 1.0 - abs(dot(geometric, eye));
                    float fringe = TuftFringe * pow(saturate(edge), TuftFringeSharp) * alive;
                    rock = lerp(rock, tipColour * TuftFringePale, fringe);
                    wrap = lerp(wrap, TuftWrap, fringe);
                }

                // The cap's shadow: the room under it keeps a little of the main light, and the
                // stem comes back into it as the sun's slant clears the rim far enough down.
                float below = _CapUnderLocal - y;
                float reach = max(1.0, _CapRadius / 0.3);
                float underCap = saturate(below / 0.3 + 1.0) * (1.0 - smoothstep(0.6 * reach, reach, below));
                float facesDown = saturate(-geometric.y);
                float shade = lerp(1.0, _ShadeUnder, saturate(max(underCap * (1.0 - onCap * table), onCap * facesDown)));

                // The light left at the rock's depth on the run's day, under the underwater look's
                // item 3 (EvoDepthLight); one otherwise.
                float3 depthLight = EvoDepthLight(input.positionWS);

                Light main = GetMainLight();
                float3 lit = rock * main.color * depthLight * Wrapped(n, main.direction, wrap) * shade;

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++)
                {
                    Light extra = GetAdditionalLight(i, input.positionWS);
                    lit += rock * extra.color * extra.distanceAttenuation * Wrapped(n, extra.direction, wrap) * shade;
                }
                #endif

                lit += rock * SampleSH(n) * depthLight * lerp(1.0, 0.55, underCap * (1.0 - onCap * table));

                // The glint: the sun's highlight off the wet stone, a GGX lobe scaled to one at its peak
                // so the dial is the highlight's reflectance there, comparable with the rock's own tones.
                // The caustic net focuses the sun into filaments, so the glint is brightest under them,
                // as HDRP drew it with the net on the sun's cookie (D202); the cuts hold no water film.
                if (_GlintStrength > 0.0)
                {
                    float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    float3 halfway = normalize(main.direction + view);
                    float nh = saturate(dot(n, halfway));
                    float nl = saturate(dot(n, main.direction));
                    float a2 = pow(max(0.05, _GlintRoughness), 4.0);
                    float d = (nh * nh) * (a2 - 1.0) + 1.0;
                    float lobe = a2 * a2 / max(1e-8, d * d);

                    float sky = EvoCausticFade(input.positionWS, _CausticReach) * (1.0 - underCap);
                    float focused = lerp(1.0, min(EvoCausticGain(input.positionWS, EvoTime()), 4.0), saturate(sky));

                    lit += main.color * depthLight * (_GlintStrength * lobe * nl * focused * shade * (1.0 - cavity));
                }

                // The net, on what the sky can see: the table and the rim's shoulder, not the
                // room under the cap.
                float fade = EvoCausticFade(input.positionWS, _CausticReach) * saturate(n.y) * (1.0 - underCap);

                if (fade > 0.001)
                {
                    float net = EvoCausticNet(input.positionWS, EvoTime());

                    lit += _CausticColor.rgb * (net * fade * _CausticStrength) * (0.25 + 0.75 * min(rock / max(1e-4, _RockCrust.rgb), 2.0));
                }

                lit = EvoMixFog(lit, input.fogFactor, input.positionWS);

                return half4(lit, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma target 3.0

            float4 DepthVertex(Attributes input) : SV_POSITION
            {
                return TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz));
            }

            half DepthFragment(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma target 3.0

            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            DepthNormalsVaryings DepthNormalsVertex(Attributes input)
            {
                DepthNormalsVaryings output;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                return output;
            }

            half4 DepthNormalsFragment(DepthNormalsVaryings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octahedral = saturate(PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5);
                    return half4(PackFloat2To888(octahedral), 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
