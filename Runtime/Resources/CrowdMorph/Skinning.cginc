// UNITY_SHADER_NO_UPDATE
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#ifndef CROWDMOPRH_SKINNING_INCLUDED
#define CROWDMOPRH_SKINNING_INCLUDED

// ----------------------------------------------------------------------------------------
// Globals
// ----------------------------------------------------------------------------------------

uniform StructuredBuffer<float3x4> _CrowdMorphSkinMatrices;

// ----------------------------------------------------------------------------------------
// Macros
// ----------------------------------------------------------------------------------------

// this is a workaround to access vertex bone weights and indices.
#define Unity_LinearBlendSkinning_float(_indices, _weights, positionIn, normalIn, tangentIn, positionOut, normalOut, tangentOut)    \
    positionOut = positionIn;                                                                                                       \
    normalOut = normalIn;                                                                                                           \
    tangentOut = tangentIn;                                                                                                         \
    float4 VertexIndices = _indices;                                                                                                \
    float4 VertexWeights = _weights;                                                                                                \

// ----------------------------------------------------------------------------------------
// Functions
// ----------------------------------------------------------------------------------------

inline void CrowdMorph_LinearBlendSkinning
(
    uint4 indices,
    float4 weights,
    float3 positionIn,
    float3 normalIn,
    float3 tangentIn,
    out float3 positionOut,
    out float3 normalOut,
    out float3 tangentOut
)
{
    for (int i = 0; i < 3; ++i)
    {
        float3x4 skinMatrix = _CrowdMorphSkinMatrices[indices[i] + asint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_CrowdMorphSkinMatrixIndex, float))];
        float3 vtransformed = mul(skinMatrix, float4(positionIn, 1));
        float3 ntransformed = mul(skinMatrix, float4(normalIn, 0));
        float3 ttransformed = mul(skinMatrix, float4(tangentIn, 0));

        positionOut += vtransformed * weights[i];
        normalOut += ntransformed * weights[i];
        tangentOut += ttransformed * weights[i];
    }
}

#if defined(UNITY_DOTS_INSTANCING_ENABLED)

#define CrowdMorph_LinearBlendSkinning_float(positionIn, normalIn, tangentIn, positionOut, normalOut, tangentOut)                       \
    CrowdMorph_LinearBlendSkinning(VertexIndices, VertexWeights, positionIn, normalIn, tangentIn, positionOut, normalOut, tangentOut)   \

#else 

inline void CrowdMorph_LinearBlendSkinning_float
(
    float3 positionIn,
    float3 normalIn,
    float3 tangentIn, 
    out float3 positionOut, 
    out float3 normalOut,
    out float3 tangentOut
)
{
    positionOut = positionIn;
    normalOut = normalIn;
    tangentOut = tangentIn;
}

#endif

#endif //CROWDMOPRH_SKINNING_INCLUDED