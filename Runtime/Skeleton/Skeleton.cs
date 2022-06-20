using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


namespace CrowdMorph
{
   [BurstCompatible]
   public unsafe struct SkeletonDefinition
   {
      public BlobArray<StringHash> BoneIDs;
      public BlobArray<int> BoneParentIndices;
      public BlobArray<float3> BoneLocalTranslationDefaultValues;
      public BlobArray<quaternion> BoneLocalRotationsDefaultValues;
      public BlobArray<float3> BoneLocalScalesDefaultValues;
      internal int HashCode;

      public int BoneCount
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => BoneIDs.Length;
      }

      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public override int GetHashCode() => HashCode;
   }
} 
