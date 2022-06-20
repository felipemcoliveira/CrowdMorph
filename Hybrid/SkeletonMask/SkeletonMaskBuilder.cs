using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace CrowdMorph.Hybrid
{
   public static class SkeletonMaskBuilder
   {
      public static BlobAssetReference<SkeletonMaskDefinition> Build(AvatarMask authoringAvatarMask)
      {
         if (authoringAvatarMask == null)
            return BlobAssetReference<SkeletonMaskDefinition>.Null;

         var blobBuilder = new BlobBuilder(Allocator.Temp);

         ref var skeletonMask = ref blobBuilder.ConstructRoot<SkeletonMaskDefinition>();

         var boneIDs = blobBuilder.Allocate(ref skeletonMask.BoneIDs, authoringAvatarMask.transformCount);
         var isBoneActive = blobBuilder.Allocate(ref skeletonMask.IsBoneActive, authoringAvatarMask.transformCount);

         for (int i = 0; i < authoringAvatarMask.transformCount; i++)
         {
            boneIDs[i] = authoringAvatarMask.GetTransformPath(i);
            isBoneActive[i] = authoringAvatarMask.GetTransformActive(i);
         }

         var outputSkeletonMask = blobBuilder.CreateBlobAssetReference<SkeletonMaskDefinition>(Allocator.Persistent);
         outputSkeletonMask.Value.m_HashCode = HashUtility.ComputeAvatarMaskHash(ref outputSkeletonMask.Value);
         blobBuilder.Dispose();

         return outputSkeletonMask;
      }
   }
}