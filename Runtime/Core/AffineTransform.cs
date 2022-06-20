using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace CrowdMorph
{
   [BurstCompatible]
   public struct AffineTransform
   {
      public float3x3 RotationScale;
      public float3 Translation;

      public static readonly AffineTransform Identity = new AffineTransform(float3.zero, float3x3.identity);

      // ----------------------------------------------------------------------------------------
      // Constructors
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public AffineTransform(float4x4 m)
      {
         RotationScale = math.float3x3(m.c0.xyz, m.c1.xyz, m.c2.xyz);
         Translation = m.c3.xyz;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public AffineTransform(float3 t, float3x3 rs)
      {
         RotationScale = rs;
         Translation = t;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public AffineTransform(float3 t, quaternion r, float3 s)
      {
         RotationScale = mathex.mulScale(math.float3x3(r), s);
         Translation = t;
      }

      // ----------------------------------------------------------------------------------------
      // Operators
      // ----------------------------------------------------------------------------------------

      public static implicit operator float3x4(AffineTransform a)
      {
         return new float3x4(a.RotationScale.c0, a.RotationScale.c1, a.RotationScale.c2, a.Translation);
      }

      public static implicit operator float4x4(AffineTransform a)
      {
         return new float4x4(new float4(a.RotationScale.c0, 0f), new float4(a.RotationScale.c1, 0f), new float4(a.RotationScale.c2, 0f), new float4(a.Translation, 1f));
      }

      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      [NotBurstCompatible]
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public override string ToString()
      {
         var rs = RotationScale;
         var t = Translation;
         return string.Format("AffineTransform(({0}f, {1}f, {2}f,  {3}f, {4}f, {5}f,  {6}f, {7}f, {8}f), ({9}f, {10}f, {11}f))",
             rs.c0.x, rs.c1.x, rs.c2.x, rs.c0.y, rs.c1.y, rs.c2.y, rs.c0.z, rs.c1.z, rs.c2.z, t.x, t.y, t.z
         );
      }
   }

   [BurstCompatible]
   public static partial class mathex
   {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static float3 mul(AffineTransform t, float3 v)
      {
         return t.Translation + math.mul(t.RotationScale, v);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static AffineTransform mul(AffineTransform lhs, AffineTransform rhs)
      {
         return new AffineTransform(mul(lhs, rhs.Translation), math.mul(lhs.RotationScale, rhs.RotationScale));
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static AffineTransform inverse(AffineTransform a)
      {
         AffineTransform inv;
         inv.RotationScale = inverse(a.RotationScale);
         inv.Translation = math.mul(inv.RotationScale, -a.Translation);
         return inv;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static uint hash(AffineTransform transform)
      {
         return math.hash(transform.RotationScale) + 0xC5C5394Bu * math.hash(transform.Translation);
      }
   }
}
