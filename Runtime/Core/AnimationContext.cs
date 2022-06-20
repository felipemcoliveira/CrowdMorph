using Unity.Entities;
using Unity.Collections;
using System.Runtime.CompilerServices;

namespace CrowdMorph
{
   public unsafe struct AnimationContext
   {
      [ReadOnly] internal NativeHashMap<int, AnimationCommandList> SkeletonHashToAnimationCommandList;
      [ReadOnly] internal NativeHashMap<int, int> ClipInstanceHashToSampleBufferIndex;
      [ReadOnly] internal NativeHashMap<int, int> SkeletonMaskInstanceHashToBufferIndex;
      [ReadOnly] internal NativeHashMap<int, BlobAssetReference<Clip>> ClipInstanceHashToClip;
      [WriteOnly] internal NativeMultiHashMap<StringHash, EntityClipEvent>.ParallelWriter EntityClipEvents;

      public bool IsCreated
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => SkeletonHashToAnimationCommandList.IsCreated && ClipInstanceHashToSampleBufferIndex.IsCreated;
      }

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public void EvaluateMotionBlendTree(
         ref AnimationTarget target,
         ref MotionBlendTree blendTree,
         ref BlobArray<Motion> Motions,
         int skeletonMaskHashCode,
         Variant* parametersPtr,
         float previousTime,
         float time,
         float weight,
         BlendingMode blendingMode
      )
      {
         var weightsPtr = stackalloc float[blendTree.Nodes.Length];
         float horizontalParameter = parametersPtr[blendTree.HorizontalParameterIndex];
         float verticalParameter = parametersPtr[blendTree.VerticalParameterIndex];
         var activeNodesRange = Core.ComputeBlendTreeWeights(weightsPtr, horizontalParameter, verticalParameter, ref blendTree);

         float motionTime = time;
         float baseWeight = weight;

         for (int i = activeNodesRange.x; i <= activeNodesRange.y; i++)
         {
            if (weightsPtr[i] < 0.01f)
               continue;

            ref var node = ref blendTree.Nodes[i];
            
            EvaluateMotion(
               ref target,
               ref Motions[node.MotionIndex],
               ref Motions,
               skeletonMaskHashCode,
               parametersPtr,
               previousTime * node.MotionSpeed,
               time * node.MotionSpeed,
               weight * weightsPtr[i],
               blendingMode
            );
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public void EvaluateMotion(
         ref AnimationTarget target,
         ref Motion motion,
         ref BlobArray<Motion> Motions,
         int skeletonMaskHashCode,
         Variant* parametersPtr,
         float previousTime,
         float time,
         float weight,
         BlendingMode blendingMode
      )
      {
         if (motion.Type == MotionType.Clip)
         {
            EvaluateMotionClip(ref target, ref motion.Clip, skeletonMaskHashCode, previousTime, time, weight, blendingMode);
         }
         else if (motion.Type == MotionType.BlendTree)
         {
            EvaluateMotionBlendTree(
               ref target,
               ref motion.BlendTree, 
               ref Motions,
               skeletonMaskHashCode,
               parametersPtr,
               previousTime,
               time, 
               weight,
               blendingMode
            );
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public void EvaluateMotionClip(
         ref AnimationTarget target,
         ref MotionClip clip,
         int skeletonMaskHashCode,
         float previousTime,
         float time,
         float weight,
         BlendingMode blendingMode
      )
      {
         EvaluateClip(
            ref target,
            clip.ClipHashCode,
            skeletonMaskHashCode,
            previousTime,
            time,
            weight,
            blendingMode
         );
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public void EvaluateClip(
         ref AnimationTarget target,
         BlobAssetReference<Clip> clip,
         int skeletonMaskHashCode,
         float previousTime, 
         float time, 
         float weight,
         BlendingMode blendingMode
      )
      {
         EvaluateClip(
            ref target,
            clip.Value.GetHashCode(),
            skeletonMaskHashCode,
            previousTime,
            time,
            weight,
            blendingMode
         );
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public void EvaluateClip(
         ref AnimationTarget target,
         int clipHashCode, 
         int skeletonMaskHashCode,
         float previousTime,
         float time, 
         float weight,
         BlendingMode blendingMode
      )
      {
         if (clipHashCode == 0)
            return;

         int skeletonHashCode = target.Skeleton.Value.GetHashCode();
         int clipInstanceHashCode = HashUtility.GetClipInstanceHash(clipHashCode, skeletonHashCode);

         if (!ClipInstanceHashToClip.TryGetValue(clipInstanceHashCode, out var clipAsset) || !clipAsset.IsCreated)
            return;

         ref var clip = ref clipAsset.Value;

         var clipSampleBufferIndex = ClipInstanceHashToSampleBufferIndex[clipInstanceHashCode];
         int sampleCount = Core.GetSampleCount(clip.FrameRate, clip.Length);
         var keyframe = ClipKeyframe.Create(time, ref clip);

         int additiveReferencePoseMatrixBufferIndex = clipSampleBufferIndex + target.Skeleton.Value.BoneCount * sampleCount;
         var commandList = SkeletonHashToAnimationCommandList[skeletonHashCode];

         int skeletonInstanceHashCode = HashUtility.GetSkeletonMaskInstanceHash(skeletonMaskHashCode, skeletonHashCode);

         commandList.Dispatch(
            target.FrameDeferredCommandCount,
            keyframe,
            weight,
            target.SkeletonMatrixBufferIndex,
            clipSampleBufferIndex,
            additiveReferencePoseMatrixBufferIndex,
            SkeletonMaskInstanceHashToBufferIndex[skeletonInstanceHashCode],
            blendingMode
         );
         target.FrameDeferredCommandCount++;

         for (int i = 0; i < clip.Events.Length; i++)
         {
            ref var evt = ref clip.Events[i];
            if (Core.Overlaps(previousTime, time, evt.Time))
            {
               EntityClipEvents.Add(evt.FunctionNameHash, new EntityClipEvent
               {
                  Entity = target.AnimatedEntity,
                  FloatParameter = evt.FloatParameter,
                  IntParameter = evt.IntParameter
               });
            }
         }
      }
   }
}
