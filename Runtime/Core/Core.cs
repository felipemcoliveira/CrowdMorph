using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace CrowdMorph
{
   struct BoneToBindingIndirectMap
   {
      public AffineTransform StaticOffset;
      public int BoneIndex;
   }

   [BurstCompile(FloatPrecision = FloatPrecision.High)]
   unsafe struct SampleClipInstanceJob : IJobParallelFor
   {
      public BlobAssetReference<Clip> Clip;
      public BlobAssetReference<SkeletonDefinition> Skeleton;

      [ReadOnly, DeallocateOnJobCompletion]
      public NativeArray<int> TranslationMap;

      [ReadOnly, DeallocateOnJobCompletion]
      public NativeArray<int> RotationMap;

      [ReadOnly, DeallocateOnJobCompletion]
      public NativeArray<int> ScaleMap;

      [NativeDisableParallelForRestriction]
      public NativeArray<AffineTransform> OutSamples;

      public void Execute(int frameIndex)
      {
         for (int i = 0; i < Skeleton.Value.BoneCount; i++)
            OutSamples[frameIndex * Skeleton.Value.BoneCount + i] = GetLocalToParent(frameIndex, i);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      AffineTransform GetLocalToParent(int frameIndex, int boneIndex)
      {
         var translation = GetLocalTranslation(frameIndex, boneIndex);
         var rotation = GetLocalRotation(frameIndex, boneIndex);
         var scale = GetLocalScale(frameIndex, boneIndex);
         return new AffineTransform(translation, rotation, scale);
      }

      float3 GetLocalTranslation(int frameIndex, int boneIndex)
      {
         int translationBindingIndex = TranslationMap[boneIndex];
         if (translationBindingIndex == -1)
            return Skeleton.Value.BoneLocalTranslationDefaultValues[boneIndex];

         int i = frameIndex * Clip.Value.TranslationsBindings.Length + translationBindingIndex;
         return Clip.Value.LocalTranslations[i];
      }

      quaternion GetLocalRotation(int frameIndex, int boneIndex)
      {
         int rotationBindingIndex = RotationMap[boneIndex];
         if (rotationBindingIndex == -1)
            return Skeleton.Value.BoneLocalRotationsDefaultValues[boneIndex];

         int i = frameIndex * Clip.Value.RotationBindings.Length + rotationBindingIndex;
         return Clip.Value.LocalRotations[i];
      }

      float3 GetLocalScale(int frameIndex, int boneIndex)
      {
         int scaleBindingIndex = ScaleMap[boneIndex];
         if (scaleBindingIndex == -1)
            return Skeleton.Value.BoneLocalScalesDefaultValues[boneIndex];

         int i = frameIndex * Clip.Value.ScalesBindings.Length + scaleBindingIndex;
         return Clip.Value.LocalScales[i];
      }
   }

   public static partial class Core
   {
      // ----------------------------------------------------------------------------------------
      // Methods (Skeleton)
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static int CalculateMatrixSamplesLength(BlobAssetReference<SkeletonDefinition> skeleton, BlobAssetReference<Clip> clip)
      {
         ValidateArgumentIsCreated(skeleton);
         ValidateArgumentIsCreated(clip);
         return (skeleton.Value.BoneCount * (clip.Value.SampleCount + 1));
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static AffineTransform GetLocalToParent(BlobAssetReference<SkeletonDefinition> skeleton, int boneIndex)
      {
         ValidateArgumentIsCreated(skeleton);
         var translation = skeleton.Value.BoneLocalTranslationDefaultValues[boneIndex];
         var rotation = skeleton.Value.BoneLocalRotationsDefaultValues[boneIndex];
         var scale = skeleton.Value.BoneLocalScalesDefaultValues[boneIndex];
         return new AffineTransform(translation, rotation, scale);
      }

      // ----------------------------------------------------------------------------------------
      // Methods (AnimatorController)
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static void UpdateLayer(
         ref LayerState layerState,
         ref Layer layer,
         Variant* parametersPtr,
         float deltaTime,
         ref BlobArray<Motion> motions
      )
      {
         InitializeLayerStateIfNeeded(ref layerState, ref layer);

         layerState.Flags &= ~LayerStateFlags.UpdateFlags;

         ref var currentState = ref layer.StateMachine.States[layerState.CurrentStateIndex];
         float currentStatePreviousTime = layerState.CurrentStateTime;

         layerState.CurrentStateTime = UpdateStateTime(ref currentState, ref motions, parametersPtr, deltaTime, layerState.CurrentStateTime);

         if (EvaluateGlobalTransitions(ref layer.StateMachine.GlobalTransitions, layerState.CurrentStateIndex, parametersPtr, out int globalTransitionIndex))
         {
            ref var globalTransition = ref layer.StateMachine.GlobalTransitions[globalTransitionIndex];
            float cycleOffset = globalTransition.Offset * GetMotionLength(ref layer.StateMachine.States[globalTransition.DestinationStateIndex], ref motions);
            StartTransition(ref layerState, globalTransitionIndex, globalTransition.DestinationStateIndex, cycleOffset);
            layerState.Flags |= LayerStateFlags.IsActiveTransitionGlobal;
         }

         if (!layerState.IsInTransition)
         {
            float motionLength = GetMotionLength(ref currentState, ref motions);

            if (EvaluateStateTransitions(
               ref currentState.Transitions,
               parametersPtr, 
               motionLength,
               currentStatePreviousTime,
               layerState.CurrentStateTime, 
               out int transitionIndex
            ))
            {
               ref var transition = ref currentState.Transitions[transitionIndex];
               float targetStateMotionLength = GetMotionLength(ref layer.StateMachine.States[transition.DestinationStateIndex], ref motions);
               StartTransition(ref layerState, transitionIndex, transition.DestinationStateIndex, transition.Offset * targetStateMotionLength);
            }  
         }

         if (layerState.IsInTransition)
         {
            bool hasBeenInterrupted;
            do
            {
               ref var activeTransition = ref GetActiveTranstion(ref layerState, ref layer.StateMachine);
               UpdateTransition(ref layerState, ref activeTransition, deltaTime);

               ref var destinationState = ref layer.StateMachine.States[activeTransition.DestinationStateIndex];

               float currentStateMotionLength = GetMotionLength(ref currentState, ref motions);
               float destinationStateMotionLength = GetMotionLength(ref destinationState, ref motions);

               float destinationStatePreviousTime = layerState.DestinationStateTime;
               layerState.DestinationStateTime = UpdateStateTime(ref destinationState, ref motions, parametersPtr, deltaTime, layerState.DestinationStateTime);

               hasBeenInterrupted = EvaluateInterruptTransitions(
                  ref activeTransition,
                  ref currentState,
                  ref destinationState,
                  ref layerState,
                  parametersPtr,
                  currentStateMotionLength,
                  destinationStateMotionLength,
                  currentStatePreviousTime,
                  destinationStatePreviousTime
               );
            } while (hasBeenInterrupted);
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static bool EvaluateInterruptTransitions(
         ref Transition transition,
         ref State src, 
         ref State dst,
         ref LayerState layerState,
         Variant* parametersPtr,
         float srcMotionLength,
         float dstMotionLength,
         float srcPreviousTime,
         float dstPreviousTime
      )
      {
         if (transition.InterruptionSource == TransitionInterruptionSource.None)
            return false;

         if (transition.InterruptionSource == TransitionInterruptionSource.Destination || 
             transition.InterruptionSource == TransitionInterruptionSource.DestinationThenSource)
            goto EvaluateDestinationStateTransitions;

         EvaluateSourceStateTransitions:
         {
            int maxTransitionIndex = transition.OrderedInterruption ? layerState.ActiveTransitionIndex : src.Transitions.Length;  
            for (int i = 0; i < maxTransitionIndex; i++)
            {
               if (EvaluateTransition(ref src.Transitions[i], parametersPtr, srcMotionLength, srcPreviousTime, layerState.CurrentStateTime))
               {
                  StartTransition(ref layerState, i, src.Transitions[i].DestinationStateIndex, src.Transitions[i].Offset * srcMotionLength);
                  return true;
               }
            }

            if (transition.InterruptionSource != TransitionInterruptionSource.SourceThenDestination)
               return false;
         }

         EvaluateDestinationStateTransitions:
         {
            for (int i = 0; i < dst.Transitions.Length; i++)
            {
               if (EvaluateTransition(ref dst.Transitions[i], parametersPtr, dstMotionLength, dstPreviousTime, layerState.DestinationStateTime))
               {
                  StartTransition(ref layerState, i, dst.Transitions[i].DestinationStateIndex, dst.Transitions[i].Offset * dstMotionLength);
                  layerState.Flags |= LayerStateFlags.IsActiveTransitionFromDestinationState;
                  return true;
               }
            }

            if (transition.InterruptionSource == TransitionInterruptionSource.DestinationThenSource)
               goto EvaluateSourceStateTransitions;

            return false;
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static float GetMotionLength(ref State state, ref BlobArray<Motion> motions)
      {
         if (state.MotionIndex == -1)
            return 0f;

         ref var motion = ref motions[state.MotionIndex];
         return motion.AverageLength;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static WrapMode GetMotionWrapMode(ref State state, ref BlobArray<Motion> motions)
      {
         if (state.MotionIndex == -1)
            return WrapMode.Once;

         return motions[state.MotionIndex].WrapMode;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool UpdateTransition(ref LayerState layerState, ref Transition transition, float deltaTime)
      {
         layerState.TransitionTime += deltaTime;
         if (layerState.TransitionTime >= transition.Duration)
         {
            layerState.CurrentStateIndex = (byte)transition.DestinationStateIndex;
            layerState.CurrentStateTime = layerState.DestinationStateTime;
            layerState.Flags |= LayerStateFlags.HasCurrentStateChanged;
            layerState.Flags &= ~LayerStateFlags.TransitionFlags;
            return true;
         }
         return false;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static ref Transition GetActiveTranstion(ref LayerState layerState, ref StateMachine stateMachine)
      {
         if (layerState.IsActiveTransitionGlobal)
            return ref stateMachine.GlobalTransitions[layerState.ActiveTransitionIndex];

         if (layerState.IsActiveTransitionFromDestinationState)
         {
            ref var destinationState = ref stateMachine.States[layerState.DestinationStateIndex];
            return ref destinationState.Transitions[layerState.ActiveTransitionIndex];
         }

         ref var currentState = ref stateMachine.States[layerState.CurrentStateIndex];
         return ref currentState.Transitions[layerState.ActiveTransitionIndex];
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void StartTransition(ref LayerState layerState, int transitionIndex, int destinationStateIndex, float cycleOffset)
      {
         layerState.DestinationStateIndex = (byte)destinationStateIndex;
         layerState.DestinationStateTime = cycleOffset;
         layerState.TransitionTime = 0.0f;
         layerState.ActiveTransitionIndex = (byte)transitionIndex;
         layerState.Flags &= ~LayerStateFlags.TransitionFlags;
         layerState.Flags |= LayerStateFlags.IsInTransition | LayerStateFlags.HasDestinationStateChanged;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void InitializeLayerStateIfNeeded(ref LayerState layerState, ref Layer layer)
      {
         if (layerState.Initialized)
            return;

         var unsetFlags = LayerStateFlags.TransitionFlags;

         layerState.CurrentStateIndex = (byte)layer.StateMachine.InitialStateIndex;
         layerState.Weight = layer.DefaultWeight;
         layerState.CurrentStateTime = 0;
         layerState.Flags = (layerState.Flags & ~unsetFlags) | LayerStateFlags.Initialized;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static bool EvaluateGlobalTransitions(
         ref BlobArray<Transition> globalTransitions,
         int currentStateIndex,
         Variant* parametersPtr, 
         out int outTransitionIndex
      )
      {
         for (int i = 0; i < globalTransitions.Length; i++)
         {
            ref var transition = ref globalTransitions[i];

            if (!transition.CanTransitionToSelf && transition.DestinationStateIndex == currentStateIndex)
               continue;

            if (!EvaluateConditions(ref transition.Conditions, parametersPtr))
               continue;
            
            outTransitionIndex = i;
            return true;
         }
         outTransitionIndex = -1;
         return false;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static bool EvaluateStateTransitions(
         ref BlobArray<Transition> transitions,
         Variant* parametersPtr,
         float averageMotionLength,
         float previousStateTime,
         float currentStateTime,
         out int outTransitionIndex
      )
      {
         for (int i = 0; i < transitions.Length; i++)
         {
            if (EvaluateTransition(ref transitions[i], parametersPtr, averageMotionLength, previousStateTime, currentStateTime))
            {
               outTransitionIndex = i;
               return true;
            }
         }
         outTransitionIndex = -1;
         return false;
      }

      public unsafe static bool EvaluateTransition(
         ref Transition transition,
         Variant* parametersPtr, 
         float averageMotionLength,
         float previousStateTime,
         float currentStateTime
      )
      {
         if (!EvaluateConditions(ref transition.Conditions, parametersPtr))
            return false;

         if (!EvaluateTransitionExitTime(ref transition, averageMotionLength, previousStateTime, currentStateTime))
            return false;

         return true;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool EvaluateTransitionExitTime(ref Transition transition, float averageMotionLength, float previousTime, float currentTime)
      {
         if (transition.HasExitTime)
         {
            // https://docs.unity3d.com/Manual/class-Transition

            float normalizedPreviousTime = previousTime / averageMotionLength;
            float normalizedCurrentTime = currentTime / averageMotionLength;

            if (transition.ExitTime >= 1.0f)
               return Overlaps(normalizedPreviousTime, normalizedCurrentTime, transition.ExitTime);

            float normalizedPreviousTimeFrac = math.frac(normalizedPreviousTime);
            float normalizedCurrentTimeFrac = math.frac(normalizedCurrentTime);

            return Overlaps(normalizedPreviousTimeFrac, normalizedCurrentTimeFrac, transition.ExitTime);
         }
         return true;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static bool EvaluateConditions(ref BlobArray<Condition> conditions, Variant* parametersPtr)
      {
         for (int i = 0; i < conditions.Length; i++)
         {
            if (!EvaluateCondition(ref conditions[i], parametersPtr))
               return false;
         }

         return true;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static bool EvaluateCondition(ref Condition condition, Variant* parametersPtr)
      {
         var parameter = parametersPtr[condition.CompareParameterIndex];

         return condition.CompareOperation switch
         {
            CompareOperation.Trigger => parameter.BooleanValue,
            CompareOperation.FloatGreater => parameter.FloatValue > condition.CompareFloatValue,
            CompareOperation.FloatLess => parameter.FloatValue < condition.CompareFloatValue,
            CompareOperation.FloatGreaterOrEqual => parameter.FloatValue >= condition.CompareFloatValue,
            CompareOperation.FloatLessOrEqual => parameter.FloatValue <= condition.CompareFloatValue,
            CompareOperation.FloatEquals => math.abs(parameter.FloatValue - condition.CompareFloatValue) <= 0.01f,
            CompareOperation.FloatNotEqual => math.abs(parameter.FloatValue - condition.CompareFloatValue) > 0.01f,
            CompareOperation.If => parameter.BooleanValue,
            CompareOperation.IfNot => !parameter.BooleanValue,
            CompareOperation.IntGreater => parameter.IntValue > condition.CompareIntValue,
            CompareOperation.IntLess => parameter.IntValue < condition.CompareIntValue,
            CompareOperation.IntEquals => parameter.IntValue == condition.CompareIntValue,
            CompareOperation.IntNotEqual => parameter.IntValue != condition.CompareIntValue,
            CompareOperation.IntGreaterOrEqual => parameter.IntValue >= condition.CompareIntValue,
            CompareOperation.IntLessOrEqual => parameter.IntValue <= condition.CompareIntValue,
            _ => false,
         };
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool Overlaps(float previousTime, float currentTime, float t)
      {
         // backward time direction
         if (previousTime > currentTime)
            return t >= currentTime && t <= previousTime;

         // forward time direction
         return t <= currentTime && t >= previousTime;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static float UpdateStateTime(ref State state, ref BlobArray<Motion> motions, Variant* parametersPtr, float deltaTime, float time)
      {
         float scaledDeltaTime = deltaTime * GetStateSpeed(ref state, parametersPtr);
         var wrapMode = GetMotionWrapMode(ref state, ref motions);
         if (wrapMode == WrapMode.Loop)
            return time + scaledDeltaTime;

         float averageMotionLength = GetMotionLength(ref state, ref motions);
         float unclampedTime = time + scaledDeltaTime;
         return math.clamp(unclampedTime, -averageMotionLength, averageMotionLength);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static float GetStateSpeed(ref State state, Variant* parametersPtr)
      {
         if (state.SpeedMultiplierParameter >= 0)
            return parametersPtr[state.SpeedMultiplierParameter].FloatValue * state.Speed;

         return state.Speed;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static void ReadParametersFromComponentData(BlobAssetReference<AnimatorControllerDefinition> controller, Variant* parametersPtr, byte* parametersComponentDataPtr)
      {
         ValidateArgumentIsCreated(controller);
         for (int i = 0; i < controller.Value.Parameters.Length; i++)
         {
            ref var parameter = ref controller.Value.Parameters[i];
            parametersPtr[i] = UnsafeUtility.AsRef<Variant>(parametersComponentDataPtr + parameter.ComponentTypeFieldOffset);
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static void ReleaseTriggers(BlobAssetReference<AnimatorControllerDefinition> controller, byte* parametersComponentDataPtr)
      {
         ValidateArgumentIsCreated(controller);
         // trigger parameters are the only parameters that can be modified, so they're the only parameters that possibly write back to component data
         for (int i = 0; i < controller.Value.TriggerParameters.Length; i++)
         {
            int parameterIdx = controller.Value.TriggerParameters[i];
            ref var parameter = ref controller.Value.Parameters[parameterIdx];
            UnsafeUtility.AsRef<Variant>(parametersComponentDataPtr + parameter.ComponentTypeFieldOffset).BooleanValue = false;
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static ComponentType GetParametersComponentType(BlobAssetReference<AnimatorControllerDefinition> controller)
      {
         ValidateArgumentIsCreated(controller);
         int parametersComponentTypeIndex = TypeManager.GetTypeIndexFromStableTypeHash(controller.Value.ParametersStableTypeHash);
         return ComponentType.FromTypeIndex(parametersComponentTypeIndex);
      }

      // ----------------------------------------------------------------------------------------
      // Methods (Clip)
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static int GetSampleCount(float frameRate, float duration)
      {
         return GetFrameCount(frameRate, duration) + 1;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static int GetFrameCount(float frameRate, float duration)
      {
         return (int)math.ceil(frameRate * duration);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static float GetLastFrameError(float frameRate, float duration)
      {
         return GetFrameCount(frameRate, duration) - duration * frameRate;
      }

      public static NativeArray<AffineTransform> SampleClipInstanceMatrices(
         BlobAssetReference<SkeletonDefinition> skeleton,
         BlobAssetReference<Clip> clip
      )
      {
         ValidateArgumentIsCreated(skeleton);
         ValidateArgumentIsCreated(clip);

         var samples = new NativeArray<AffineTransform>(skeleton.Value.BoneCount * (clip.Value.SampleCount + 1), Allocator.TempJob);

         var translationMap = MapBinding(ref clip.Value.TranslationsBindings, ref skeleton.Value.BoneIDs, Allocator.TempJob);
         var rotationMap = MapBinding(ref clip.Value.RotationBindings, ref skeleton.Value.BoneIDs, Allocator.TempJob);
         var scaleMap = MapBinding(ref clip.Value.ScalesBindings, ref skeleton.Value.BoneIDs, Allocator.TempJob);

         //var boneToBindingIndex = new NativeArray<int>(skeleton.Value.BoneCount, Allocator.TempJob);
         //var boneToBindingIndirectMap = new NativeArray<BoneToBindingIndirectMap>(skeleton.Value.BoneCount, Allocator.TempJob);
         //MapClipToSkeleton(clip, skeleton, boneToBindingIndex, boneToBindingIndirectMap);

         var handle = new SampleClipInstanceJob
         {
            Clip = clip,
            Skeleton = skeleton,
            OutSamples = samples,
            TranslationMap = translationMap,
            RotationMap = rotationMap,
            ScaleMap = scaleMap
         }.Schedule(clip.Value.SampleCount, 8);
         handle.Complete();

         int inverseMatrixBufferOffset = skeleton.Value.BoneCount * clip.Value.SampleCount;

         for (int i = 0; i < skeleton.Value.BoneCount; i++)
            samples[inverseMatrixBufferOffset + i] = mathex.inverse(samples[i]);

         return samples;
      }

      private static NativeArray<int> MapBinding(ref BlobArray<StringHash> source, ref BlobArray<StringHash> target, Allocator allocator)
      {
         var map = new NativeArray<int>(target.Length, allocator);
         for (int i = 0; i < map.Length; i++)
            map[i] = FindBindingIndex(ref source, target[i]);
         return map;
      }

      private static void MapClipToSkeleton(
         BlobAssetReference<Clip> clip,
         BlobAssetReference<SkeletonDefinition> skeleton,
         NativeArray<int> outBoneToBindingIndex,
         NativeArray<BoneToBindingIndirectMap> outBoneToBindingIndirectMap
      )
      {
         ValidateArgumentIsCreated(skeleton);
         ValidateArgumentIsCreated(clip);
         for (int i = 0; i < skeleton.Value.BoneCount; i++)
         {
            outBoneToBindingIndex[i] = FindBindingIndex(ref clip.Value.TranslationsBindings, skeleton.Value.BoneIDs[i]);

            // indirect mapping
            if (outBoneToBindingIndex[i] == -1)
            {
               int bindingIdx;
               var offset = AffineTransform.Identity;
               int indirectBoneIdx = i;
               do
               {
                  var localToParent = GetLocalToParent(skeleton, indirectBoneIdx);
                  offset = mathex.mul(localToParent, offset);
                  indirectBoneIdx = skeleton.Value.BoneParentIndices[indirectBoneIdx];

                  if (indirectBoneIdx < 0)
                     break;

                  bindingIdx = outBoneToBindingIndex[indirectBoneIdx];
               }
               while (bindingIdx == -1);

               outBoneToBindingIndirectMap[i] = new BoneToBindingIndirectMap
               {
                  BoneIndex = indirectBoneIdx,
                  StaticOffset = offset
               };
            }
         }
      }

      public static int FindBindingIndex(ref BlobArray<StringHash> bindings, StringHash searchedBinding)
      {
         for (int i = 0; i < bindings.Length; i++)
         {
            if (searchedBinding == bindings[i])
               return i;
         }
         return -1;
      }

      // ----------------------------------------------------------------------------------------
      // Methods (BlendTree)
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static int2 ComputeBlendTreeWeights(float* weightsPtr, float horizontalParameter, float verticalParameter, ref MotionBlendTree blendTree)
      {
         ValidateArgumentIsNotNull(weightsPtr);
         if (blendTree.BlendTreeType == BlendTreeType.Simple1D)
            return ComputeSimple1DBlendTreeWeights(weightsPtr, horizontalParameter, ref blendTree);

         if (blendTree.BlendTreeType == BlendTreeType.SimpleDirectional2D)
            NotImplementedException();

         return new int2(0, -1);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      private unsafe static int2 ComputeSimple1DBlendTreeWeights(float* weightsPtr, float parameter, ref MotionBlendTree blendTree)
      {
         ValidateArgumentIsNotNull(weightsPtr);
         int i = 0;
         while (i < blendTree.Nodes.Length)
         {
            if (parameter < blendTree.Nodes[i].Position.x)
               break;

            i++;
         }

         int rightIdx = math.min(i, blendTree.Nodes.Length - 1);
         int leftIdx = math.clamp(i - 1, 0, blendTree.Nodes.Length - 1);
         ref var rightNode = ref blendTree.Nodes[rightIdx];

         if (rightIdx == leftIdx)
         {
            weightsPtr[rightIdx] = 1.0f;
            return new int2(rightIdx, rightIdx);
         }

         ref var leftNode = ref blendTree.Nodes[leftIdx];
         float weight = math.remap(leftNode.Position.x, rightNode.Position.x, 0, 1, parameter);

         weightsPtr[leftIdx] = weight;
         weightsPtr[rightIdx] = 1 - weight;
         return new int2(leftIdx, rightIdx);
      }

      // ----------------------------------------------------------------------------------------
      // Methods (SkeletonMask)
      // ----------------------------------------------------------------------------------------

      public static ulong ComputeSkeletonMask(BlobAssetReference<SkeletonMaskDefinition> mask, BlobAssetReference<SkeletonDefinition> skeleton)
      {
         ulong bits = 0UL;
         int boneCount = math.min(skeleton.Value.BoneCount, 64);
         for (int i = 0; i < boneCount; i++)
         {
            int idx = FindBindingIndex(ref mask.Value.BoneIDs, skeleton.Value.BoneIDs[i]);
            if (idx == -1)
               continue;

            if (mask.Value.IsBoneActive[idx])
               bits |= 1UL << i;
         }
         return bits;
      }
   }
}