//UNITY_SHADER_NO_UPGRADE
#ifndef CROWDMORPH_AFFINETRANSFORM_INCLUDED
#define CROWDMORPH_AFFINETRANSFORM_INCLUDED

// ----------------------------------------------------------------------------------------
// Macros
// ----------------------------------------------------------------------------------------

#define AFFINE_TRANSFORM_IDENTITY { float3x3(1, 0, 0, 0, 1, 0, 0, 0, 1), float3(0, 0, 0) }

// ----------------------------------------------------------------------------------------
// Structures
// ----------------------------------------------------------------------------------------

// maybe align it? 
struct AffineTransform
{
    float3x3 RotationScale;
    float3 Translation;
};

// ----------------------------------------------------------------------------------------
// Functions
// ----------------------------------------------------------------------------------------

inline float3 AffineTransformMulVector(AffineTransform t, float3 v)
{
    return t.Translation + mul(t.RotationScale, v);
}

inline AffineTransform AffineTransformMul(AffineTransform lhs, AffineTransform rhs)
{
    AffineTransform result;
    result.RotationScale = mul(lhs.RotationScale, rhs.RotationScale);
    result.Translation = AffineTransformMulVector(lhs, rhs.Translation);
    return result;
}


inline AffineTransform AffineTransformLerp(AffineTransform lhs, AffineTransform rhs, float t)
{
    AffineTransform result;
    result.RotationScale = lerp(lhs.RotationScale, rhs.RotationScale, t);
    result.Translation = lerp(lhs.Translation, rhs.Translation, t);
    return result;
}

#endif //CROWDMORPH_AFFINETRANSFORM_INCLUDED
