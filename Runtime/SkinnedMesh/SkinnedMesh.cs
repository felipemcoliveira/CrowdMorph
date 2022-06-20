using System.Runtime.CompilerServices;
using Unity.Entities;

namespace CrowdMorph
{
   public struct SkinnedMeshInstance
   {
      public int SkinMatixBufferIndex;
      public int SkeletonMatixBufferIndex;
   }

   public unsafe struct SkinnedMeshDefinition
   {
      public BlobArray<int> SkinToSkeletonBoneIndices;
      public BlobArray<AffineTransform> BindPoses;

      internal int HashCode;

      public int BoneCount
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => SkinToSkeletonBoneIndices.Length;
      }

      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public override int GetHashCode() => HashCode;
   }
}