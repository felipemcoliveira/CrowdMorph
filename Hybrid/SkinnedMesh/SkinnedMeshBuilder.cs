using System;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace CrowdMorph.Hybrid
{

   public static class SkinnedMeshBuilder
   {
      public static BlobAssetReference<SkinnedMeshDefinition> Build(SkinnedMeshRenderer skinnedMeshRenderer, Skeleton authoringSkeleton)
      {
         if (skinnedMeshRenderer == null || authoringSkeleton == null || skinnedMeshRenderer.sharedMesh == null)
         {
            return BlobAssetReference<SkinnedMeshDefinition>.Null;
         }

         var blobBuilder = new BlobBuilder(Allocator.Temp);
         
         ref var skinnedMesh = ref blobBuilder.ConstructRoot<SkinnedMeshDefinition>();
         int boneCount = skinnedMeshRenderer.bones.Length;

         var skinToSkeletonBoneIndices = blobBuilder.Allocate(ref skinnedMesh.SkinToSkeletonBoneIndices, boneCount);
         var bindPoses = blobBuilder.Allocate(ref skinnedMesh.BindPoses, boneCount);

         var skeletonBones = authoringSkeleton.GetBones();
         
         for (int i = 0; i < boneCount; i++)
         {
            bindPoses[i] = new AffineTransform(skinnedMeshRenderer.sharedMesh.bindposes[i]);
            skinToSkeletonBoneIndices[i] = Array.IndexOf(skeletonBones, skinnedMeshRenderer.bones[i]);
         }

         var outputSkinnedMesh = blobBuilder.CreateBlobAssetReference<SkinnedMeshDefinition>(Allocator.Persistent);
         outputSkinnedMesh.Value.HashCode = HashUtility.ComputeSkinnedMeshHash(ref outputSkinnedMesh.Value);
         blobBuilder.Dispose();
         return outputSkinnedMesh;
      }
   }
}
