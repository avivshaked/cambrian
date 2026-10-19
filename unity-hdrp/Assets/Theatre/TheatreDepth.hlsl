#ifndef EVOSIM_THEATRE_DEPTH
#define EVOSIM_THEATRE_DEPTH

// The scene's depth, for the two additive things drawn in the water (the shafts and the snow)
// to soften against whatever is behind them: a beam that stops dead at a body's edge, or a
// mote drawn across a body's face, are the two tells the look's design pass named
// (logbook/specs/striking-theatre-menu.md, C1b and C2). A shader that includes this must
// include URP's Core.hlsl first, and the pipeline asset carries the depth texture the sampler
// reads (m_RequireDepthTexture, 2026-09-16).
//
// Both cameras are served. The theatre's snapshot views are orthographic and the fly camera is
// not; the depth buffer is linear in the first and hyperbolic in the second, and URP's
// LinearEyeDepth is only the second, so the orthographic case is unwound by hand from the
// projection's near and far.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

// Metres from the eye to whatever the scene drew at a screen position.
float EvoSceneEyeDepth(float2 screenUV)
{
    float raw = SampleSceneDepth(screenUV);

    if (unity_OrthoParams.w > 0.5)
    {
        #if UNITY_REVERSED_Z
        raw = 1.0 - raw;
        #endif

        return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
    }

    return LinearEyeDepth(raw, _ZBufferParams);
}

// One at soft metres or more in front of the scene, zero at the scene, for a fragment whose
// own eye depth the vertex stage wrote (-TransformWorldToView(positionWS).z, which is the same
// number under both projections).
float EvoSoftAgainstScene(float4 positionCS, float eyeDepth, float softMetres)
{
    float2 uv = GetNormalizedScreenSpaceUV(positionCS);
    float scene = EvoSceneEyeDepth(uv);

    return saturate((scene - eyeDepth) / max(0.01, softMetres));
}

#endif
