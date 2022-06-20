using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace CrowdMorph
{


   [BurstCompatible]
   public static partial class mathex
   {
      const float k_EpsilonRCP = 1e-9f;
      const float k_EpsilonNormal = 1e-30f;
      const float k_EpsilonDeterminant = 1e-6f;

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static float3 rcpsafe(float3 v)
      {
         return math.select(math.rcp(v), float3.zero, math.abs(v) < k_EpsilonRCP);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static float3x3 mulScale(float3x3 m, float3 s)
      {
         return new float3x3(m.c0 * s.x, m.c1 * s.y, m.c2 * s.z);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      static float3x3 adj(float3x3 m, out float det)
      {
         float3x3 adjT;
         adjT.c0 = math.cross(m.c1, m.c2);
         adjT.c1 = math.cross(m.c2, m.c0);
         adjT.c2 = math.cross(m.c0, m.c1);
         det = math.dot(m.c0, adjT.c0);

         return math.transpose(adjT);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static float3x3 scaleMul(float3 s, float3x3 m)
      {
         return new float3x3(s * m.c0, s * m.c1, s * m.c2);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      static bool adjInverse(float3x3 m, out float3x3 i, float epsilon = k_EpsilonNormal)
      {
         i = adj(m, out float det);
         bool c = math.abs(det) > epsilon;
         var detInv = math.select(math.float3(1f), math.rcp(det), c);
         i = scaleMul(detInv, i);
         return c;
      }

      [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
      static void ThrowSingularMatrixException()
      {
         throw new ArithmeticException("Singular matrix.");
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static float3x3 inverse(float3x3 m)
      {
         float scaleSq = 0.333333f * (math.dot(m.c0, m.c0) + math.dot(m.c1, m.c1) + math.dot(m.c2, m.c2));
         if (scaleSq < k_EpsilonNormal)
            return float3x3.zero;

         var scaleInv = math.rsqrt(scaleSq);
         var ms = mulScale(m, scaleInv);
         if (!adjInverse(ms, out float3x3 i, k_EpsilonDeterminant))
         {
            // TODO handle singular exceptions with SVD
            ThrowSingularMatrixException();
            return float3x3.identity;
         }
         return mulScale(i, scaleInv);
      }
   }
}
