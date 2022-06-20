#if UNITY_EDITOR

using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEditor;

namespace CrowdMorph.Hybrid
{
   public static class SkeletonBuilder
   {
      public static BlobAssetReference<SkeletonDefinition> Build(Skeleton authoringSkeleton)
      {
         var blobBuilder = new BlobBuilder(Allocator.Temp);
         ref var skeleton = ref blobBuilder.ConstructRoot<SkeletonDefinition>();

         var bones = authoringSkeleton.GetBones();
         var root = authoringSkeleton.RootBone;

         var boneIDS = blobBuilder.Allocate(ref skeleton.BoneIDs, bones.Length);
         var boneParentIndices = blobBuilder.Allocate(ref skeleton.BoneParentIndices, bones.Length);
         var boneLocalTranslationDefaultValues = blobBuilder.Allocate(ref skeleton.BoneLocalTranslationDefaultValues, bones.Length);
         var boneLocalRotationsDefaultValues = blobBuilder.Allocate(ref skeleton.BoneLocalRotationsDefaultValues, bones.Length);
         var boneLocalScalesDefaultValues = blobBuilder.Allocate(ref skeleton.BoneLocalScalesDefaultValues, bones.Length);

         for (int i = 0; i < bones.Length; i++)
         {
            var boneTransform = bones[i];
            boneIDS[i] = AnimationUtility.CalculateTransformPath(boneTransform, root);
            boneParentIndices[i] = GetParentIndex(boneTransform, bones);
            boneLocalTranslationDefaultValues[i] = boneTransform.localPosition;
            boneLocalRotationsDefaultValues[i] = boneTransform.localRotation;
            boneLocalScalesDefaultValues[i] = boneTransform.localScale;
         }

         var outputSkeleton = blobBuilder.CreateBlobAssetReference<SkeletonDefinition>(Allocator.Persistent);
         outputSkeleton.Value.HashCode = HashUtility.ComputeSkeletonHash(ref outputSkeleton.Value);
         blobBuilder.Dispose();
         return outputSkeleton;
      }

      private static int GetParentIndex(Transform transform, Transform[] transforms)
      {
         var parent = transform.parent;
         if (parent == null)
            return -1;

         var instanceID = parent.GetInstanceID();
         for (int i = 0; i < transforms.Length; i++)
         {
            if (transforms[i].GetInstanceID() == instanceID)
               return i;
         }
         return -1;
      }
   }
}

#endif