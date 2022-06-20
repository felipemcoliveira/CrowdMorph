using Unity.Entities;
using System.Runtime.CompilerServices;

namespace CrowdMorph
{
   public unsafe struct AnimationTarget
   {
      public BlobAssetReference<SkeletonDefinition> Skeleton;

      public Entity AnimatedEntity
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_AnimatedEntity;
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         set => m_AnimatedEntity = value;
      }

      internal int SkeletonMatrixBufferIndex
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_SkeletonMatrixBufferIndex;
      }

      internal int FrameDeferredCommandCount
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_FrameDeferredCommandCount;

         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         set => m_FrameDeferredCommandCount = value;
      }

      internal bool IsCreated
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => Skeleton != BlobAssetReference<SkeletonDefinition>.Null;
      }

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static AnimationTarget Create(BlobAssetReference<SkeletonDefinition> skeleton, int skeletonMatrixBufferIndex, Entity Entity)
      {
         return new AnimationTarget
         {
            Skeleton = skeleton,
            m_SkeletonMatrixBufferIndex = skeletonMatrixBufferIndex,
            m_FrameDeferredCommandCount = 0,
            m_AnimatedEntity = Entity
         }; 
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      int m_SkeletonMatrixBufferIndex;
      int m_FrameDeferredCommandCount;
      Entity m_AnimatedEntity;
   }
}
