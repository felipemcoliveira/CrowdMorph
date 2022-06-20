using Unity.Entities;

namespace CrowdMorph
{
   public struct SkeletonMatrixBufferIndex : ISystemStateComponentData
   {
      public int Value;
   }

   public struct SkeletonEntity : IComponentData
   {
      public Entity Value;
   }

   public struct SharedSkeleton : ISharedComponentData
   {
      public BlobAssetReference<SkeletonDefinition> Value;
   }

   public unsafe struct SharedSkeletonData : ISystemStateSharedComponentData
   {
      public int SkeletonHashCode;
      public int BoneCount;
   }
}