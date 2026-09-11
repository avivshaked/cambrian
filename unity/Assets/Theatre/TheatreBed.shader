// The sea bed.
//
// Why triplanar. The bed is one quad sized from the run's own config, and a run that changes the
// patch count or the area changes its dimensions; a UV mapping would stretch the sand with it and
// the ripples would come out as long smears in whichever direction the box grew. Triplanar takes
// the pattern from the world position instead, so a metre of sand is a metre of sand whatever the
// quad is (research/theatre-look, [CC3]); the normal is rebuilt per projection rather than
// blended as a vector, which is Golus's point [BG].
//
// Why the bed is carved downward and only downward. SeaFloor is a box collider with a flat top,
// and the theatre must not draw a bed a body could appear to sink into. The first day answered
// that by keeping the drawn surface exactly on the collider's plane and putting all the relief in
// the shading, which left a plane: seen from the side it was a ruled line under a world of boxes.
// The second day cuts into it instead, by low frequency noise that is never negative, subtracted
// from the plane. So every drawn point is at or below the collider's top, a body resting on the
// floor can only appear to hover a little rather than to sink, and the bed reads as a bed. The
// fine ripple and the grain stay in the normal, where they cost nothing.
//
// The caustics are the same function the body shader uses, from TheatreWater.hlsl, so the net on
// the sand and the net on a creature swimming above it are one pattern. Since the fourth day that
// pattern is the sea itself, focused: the surface plane overhead, the shafts coming down from it
// and this net are three readings of one wave field (logbook/specs/skin-spec-4.md).

Shader "Evosim/Theatre Bed"
{
    Properties
    {
        // Dark, and measured from a picture rather than guessed. The first cut used a sand four
        // times lighter and the top view came out as a lit beach with dark creatures on it, which
        // inverts the dark field the whole skin is built on: the water is meant to be the black
        // and a body the only thing that scatters. It is also the one surface URP's fog does not
        // reach in three of the four snapshot views (see SnapshotCamera.FrameTheFog), so the bed
        // has to be dark on its own account and not by being far away.
        //
        // Nearly grey, too, and that is the second reading. A warmer sand put the sea floor on the
        // same hue as the absorptive guild: at t = 3000 s in r35-s3 a stomach lying near the bed
        // read as a slightly brighter patch of bed. The guilds are the thing these pictures are
        // for, so the furniture gives up its colour to them.
        _SandDark("Sand, shaded", Color) = (0.012, 0.012, 0.012, 1)
        _SandLight("Sand, lit", Color) = (0.066, 0.065, 0.061, 1)

        // The carve. Amplitude first, then the wavelength: the ratio is the bed's steepness, and
        // the normal composition in the fragment below is a small angle approximation that wants
        // it well under one.
        _BedCarveMetres("How far the bed is cut down, metres", Range(0, 3)) = 0.45
        _BedLobeMetres("Metres per bed lobe", Range(0.5, 40)) = 5.0

        _RippleMetres("Metres per ripple", Range(0.05, 5)) = 0.55
        _RippleStrength("Ripple strength", Range(0, 2)) = 0.9
        _GrainMetres("Metres per grain cell", Range(0.01, 2)) = 0.09
        _GrainStrength("Grain strength", Range(0, 1)) = 0.25

        // The net's scale is the sea's wavelength now, a global pushed once by TheatreSkin
        // (TheatreWater.hlsl), so the bed cannot be lit by a different surface from the bodies
        // above it. The reach comes from the skin too, off the one dial the shafts and the
        // ceiling fade on (EVOSIM_THEATRE_LIGHT_REACH).
        _CausticColor("Caustic colour", Color) = (0.55, 0.85, 0.95, 1)
        _CausticStrength("Caustic strength", Range(0, 3)) = 0.9
        _CausticReach("Metres below the surface caustics reach", Range(1, 400)) = 18

        _Wrap("Diffuse wrap", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Two sided, for WaterBounds' reason: the free fly camera can go under the world, and
            // a back face culled bed would leave a hole to fall through with nothing in it.
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0

            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "TheatreWater.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _BedCarveMetres;
                float _BedLobeMetres;
                float4 _SandDark;
                float4 _SandLight;
                float _RippleMetres;
                float _RippleStrength;
                float _GrainMetres;
                float _GrainStrength;
                float4 _CausticColor;
                float _CausticStrength;
                float _CausticReach;
                float _Wrap;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float fogFactor   : TEXCOORD2;
            };

            // How far the bed is cut down at a point, in metres, and the slope of that cut.
            //
            // Two octaves of the same value noise the bodies are carved by (TheatreWater.hlsl),
            // read in world metres rather than in object units: the bed is one quad sized from
            // the run's config, so a field in its object space would stretch with the box the way
            // a UV map would, which is the reason the sand was triplanar in the first place.
            //
            // In [0, 1] and multiplied by a positive depth, so the result is subtracted from the
            // plane and never added to it. That is the whole of the size argument.
            float BedCarve(float2 xz, out float2 slope)
            {
                float metres = max(0.5, _BedLobeMetres);

                float3 coarseGradient, fineGradient;
                float coarse = EvoValueNoise(float3(xz.x, 0.0, xz.y) / metres, coarseGradient);
                float fine = EvoValueNoise(float3(xz.x, 0.0, xz.y) * (2.7 / metres), fineGradient);

                float value = 0.78 * coarse + 0.22 * fine;

                slope = (0.78 * coarseGradient.xz + 0.22 * 2.7 * fineGradient.xz) / metres;

                return saturate(value);
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));

                float2 slope;
                float carve = BedCarve(positionWS.xz, slope);

                // Only the upward facing side of the quad is cut. The bed is drawn two sided so
                // that a camera under the world does not see a hole, and a downward facing pass
                // of the same surface must move with it.
                positionWS.y -= _BedCarveMetres * carve;

                // The normal of that surface: the height is h(x, z) = -depth * carve, so the
                // surface normal is (-dh/dx, 1, -dh/dz) normalised, kept pointing the same way
                // the flat quad's normal did.
                float2 gradient = -_BedCarveMetres * slope;
                float3 tilted = normalize(float3(-gradient.x, 1.0, -gradient.y));

                output.positionWS = positionWS;
                output.normalWS = normalWS.y < 0.0 ? -tilted : tilted;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            // Sand relief on one plane: two ripple trains crossing at a shallow angle, plus a
            // grain from the cell pattern. Returned as a height, so the caller can take its
            // gradient by finite difference and build a normal for that projection.
            float BedHeight(float2 p)
            {
                float ripple =
                    sin(p.x * 6.2831853 + sin(p.y * 1.7) * 1.3) * 0.6 +
                    sin((p.x * 0.72 + p.y * 0.44) * 6.2831853) * 0.4;

                // The grain is value noise rather than another Voronoi. This height is
                // evaluated three times per pixel to take its gradient, and a 27 cell Worley
                // three times over is the most expensive thing that could be put on the largest
                // surface in the picture, for a detail nobody can resolve.
                float2 q = p * (_RippleMetres / max(0.001, _GrainMetres));
                float2 cell = floor(q);
                float2 f = q - cell;
                f = f * f * (3.0 - 2.0 * f);

                float a = EvoHash1(cell);
                float b = EvoHash1(cell + float2(1.0, 0.0));
                float c = EvoHash1(cell + float2(0.0, 1.0));
                float d = EvoHash1(cell + float2(1.0, 1.0));

                float grain = lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);

                return ripple * _RippleStrength + (grain - 0.5) * _GrainStrength * 2.0;
            }

            // The gradient of that height, as a tangent space normal for one projection.
            float3 BedNormal(float2 p)
            {
                const float e = 0.02;

                float h = BedHeight(p);
                float dx = BedHeight(p + float2(e, 0.0)) - h;
                float dy = BedHeight(p + float2(0.0, e)) - h;

                return normalize(float3(-dx / e, -dy / e, 8.0));
            }

            float Wrapped(float3 n, float3 l)
            {
                return saturate((dot(n, l) + _Wrap) / (1.0 + _Wrap));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 geometric = normalize(input.normalWS);

                // Triplanar. The blend weights are the geometric normal raised to a power, which
                // keeps a face taking almost all of its detail from the projection it faces and
                // narrows the band where two projections cross.
                float3 blend = pow(abs(geometric), 6.0);
                blend /= max(1e-4, blend.x + blend.y + blend.z);

                float3 p = input.positionWS / max(0.001, _RippleMetres);

                float3 nx = BedNormal(p.zy);
                float3 ny = BedNormal(p.xz);
                float3 nz = BedNormal(p.xy);

                // Whiteout blending: each projection's normal is rebuilt into world space by
                // swizzling its axes into place before the three are summed, rather than blending
                // three tangent space vectors that do not share a frame [BG].
                float3 detail =
                    blend.x * float3(nx.z * sign(geometric.x), nx.y, nx.x) +
                    blend.y * float3(ny.x, ny.z * sign(geometric.y), ny.y) +
                    blend.z * float3(nz.x, nz.y, nz.z * sign(geometric.z));

                // The triplanar detail is built around each projection's own axis, so on a bed
                // whose normal has been tilted by the carve it would come back pointing straight
                // up again and the lobes would stop shading. Composing the two by adding the
                // geometric normal's departure from vertical is the small angle form of putting
                // the detail in the tilted frame; the bed's slope is a fraction of a metre over
                // five, so the small angle is the case by construction.
                float3 n = normalize(detail + (geometric - float3(0.0, 1.0, 0.0)));

                float tone = saturate(0.5 + 0.5 * n.y);
                float3 sand = lerp(_SandDark.rgb, _SandLight.rgb, tone);

                Light main = GetMainLight();
                float3 lit = sand * main.color * Wrapped(n, main.direction);

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++)
                {
                    Light extra = GetAdditionalLight(i, input.positionWS);
                    lit += sand * extra.color * extra.distanceAttenuation * Wrapped(n, extra.direction);
                }
                #endif

                lit += sand * SampleSH(n);

                // The same net a body carries, from the same wave field at the same second
                // (TheatreWater.hlsl, EvoCausticNet), so the light on the sand and the light on a
                // creature swimming above it are one surface focusing rather than two patterns
                // that happen to share a colour.
                float fade = EvoCausticFade(input.positionWS, _CausticReach);

                if (fade > 0.001)
                {
                    float net = EvoCausticNet(input.positionWS, _Time.y);

                    lit += _CausticColor.rgb * (net * fade * _CausticStrength) * (0.35 + 0.65 * sand);
                }

                lit = MixFog(lit, input.fogFactor);

                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
