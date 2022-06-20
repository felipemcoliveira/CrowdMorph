#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor.Animations;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;

namespace CrowdMorph.Hybrid
{
   public class AnimatorControllerBuilder
   {
      private AnimatorController m_AnimatorController;
      private Type m_ParametersComponentType;

      private Dictionary<AnimatorStateMachine, AnimatorStateMachine> m_StateMachineToParent;
      private Dictionary<AnimatorState, AnimatorStateMachine> m_StateToStateMachine;
      private Dictionary<AnimatorTransitionBase, AnimatorState> m_TransitionToSourceState;
      private Stack<AnimatorTransitionBase> m_TransitionStack;
      private int m_TransitionStackConditionCount;
      private List<AnimatorState> m_CurrentLayerRecursiveStates;
      private Dictionary<AnimationClip, int> m_AnimationClipToClipHashCode;
      private Dictionary<AvatarMask, int> m_AvatarMaskToSkeletonMaskHashCode;
      private UnityEngine.Motion[] m_FlatMotions;
      private BlobBuilder m_BlobBuilder;

      public static BlobAssetReference<AnimatorControllerDefinition> Build(
         AnimatorController animatorController, 
         Dictionary<AnimationClip, int> animationClipToClipHashCode,
         Dictionary<AvatarMask, int> avatarMaskToSkeletonMaskHashCode,
         Type parametersComponentType
      )
      {
         return new AnimatorControllerBuilder(
            animatorController,
            animationClipToClipHashCode,
            avatarMaskToSkeletonMaskHashCode,
            parametersComponentType
         ).Build();
      }

      private AnimatorControllerBuilder() { }

      private AnimatorControllerBuilder(
         AnimatorController animatorController,
         Dictionary<AnimationClip, int> animationClipToClipHashCode,
         Dictionary<AvatarMask, int> avatarMaskToSkeletonMaskHashCode,
         Type parametersComponentType
      )
      {
         m_AnimatorController = animatorController;
         m_ParametersComponentType = parametersComponentType;
         m_AvatarMaskToSkeletonMaskHashCode = avatarMaskToSkeletonMaskHashCode;
         m_AnimationClipToClipHashCode = animationClipToClipHashCode;
         m_StateToStateMachine = new Dictionary<AnimatorState, AnimatorStateMachine>();
         m_StateMachineToParent = new Dictionary<AnimatorStateMachine, AnimatorStateMachine>();
         m_TransitionToSourceState = new Dictionary<AnimatorTransitionBase, AnimatorState>();
         m_TransitionStack = new Stack<AnimatorTransitionBase>();
      }

      private BlobAssetReference<AnimatorControllerDefinition> Build()
      {
         if (m_AnimatorController == null)
            return BlobAssetReference<AnimatorControllerDefinition>.Null;

         m_BlobBuilder = new BlobBuilder(Allocator.Temp);

         ref var controller = ref m_BlobBuilder.ConstructRoot<AnimatorControllerDefinition>();

         // TODO validate if it's a valid component type
         var parameterComponentType = m_ParametersComponentType ?? typeof(NoParametersTag);
         int parametersComponentTypeIndex = TypeManager.GetTypeIndex(parameterComponentType);
         ref readonly var parametersComponentTypeInfo = ref TypeManager.GetTypeInfo(parametersComponentTypeIndex);
         controller.ParametersStableTypeHash = parametersComponentTypeInfo.StableTypeHash;
         controller.ParametersComponentTypeSize = parametersComponentTypeInfo.TypeSize;

         m_FlatMotions = FlattenMotions(m_AnimatorController);

         FillParameters(ref controller.Parameters, ref controller.TriggerParameters);
         FillLayers(ref controller.Layers);
         FillMotions(ref controller.Motions);

         var outpoutAnimator = m_BlobBuilder.CreateBlobAssetReference<AnimatorControllerDefinition>(Allocator.Persistent);
         outpoutAnimator.Value.HashCode = HashUtility.ComputeAnimatorHash(ref outpoutAnimator.Value);
         m_BlobBuilder.Dispose();

         return outpoutAnimator;
      }

      private void FillParameters(ref BlobArray<Parameter> parametersBlobArray, ref BlobArray<int> triggerParameterBlobArray)
      {
         var authoringParameters = m_AnimatorController.parameters;
         var parameters = m_BlobBuilder.Allocate(ref parametersBlobArray, authoringParameters.Length);
         int triggerParametersCount = 0;

         for (int i = 0; i < authoringParameters.Length; i++)
         {
            var authoringParameter = authoringParameters[i];
            ref var parameter = ref parameters[i];
            var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var parameterFieldInfo = m_ParametersComponentType.GetField(authoringParameter.name, bindingFlags);

            parameter.NameHash = authoringParameter.name;
            parameter.Type = ConvertParameterType(authoringParameter.type);
            parameter.ComponentTypeFieldOffset = UnsafeUtility.GetFieldOffset(parameterFieldInfo);

            if (parameter.Type == ParameterType.Trigger)
               triggerParametersCount++;
         }

         var triggerParameters = m_BlobBuilder.Allocate(ref triggerParameterBlobArray, triggerParametersCount);
         int idx = 0;
         for (int i = 0; i < parameters.Length; i++)
         {
            if (parameters[i].Type == ParameterType.Trigger)
            {
               triggerParameters[idx] = i;
               idx++;
            }
         }
      }

      private void FillLayers(ref BlobArray<Layer> layersBlobArray)
      {
         var authoringLayers = m_AnimatorController.layers;
         var layers = m_BlobBuilder.Allocate(ref layersBlobArray, authoringLayers.Length);

         for (int i = 0; i < authoringLayers.Length; i++)
         {
            var authoringLayer = authoringLayers[i];
            ref var layer = ref layers[i];

            layer.BlendingMode = ConvertBlendingMode(authoringLayer.blendingMode);
            layer.DefaultWeight = math.max(authoringLayer.defaultWeight, i == 0 ? 1f : 0f);
            layer.NameHash = authoringLayer.name;
            layer.SkeletonMaskHashCode = 0;

            if (authoringLayer.avatarMask)
               layer.SkeletonMaskHashCode = m_AvatarMaskToSkeletonMaskHashCode[authoringLayer.avatarMask];

            m_CurrentLayerRecursiveStates = new List<AnimatorState>();
            var authoringStateMachines = new List<AnimatorStateMachine>();

            GetStatesRecursive(m_CurrentLayerRecursiveStates, authoringLayer.stateMachine);
            GetStateMachinesRecursive(authoringStateMachines, authoringLayer.stateMachine);
            CreateRelationshipsRecursive(authoringLayer.stateMachine);

            var states = m_BlobBuilder.Allocate(ref layer.StateMachine.States, m_CurrentLayerRecursiveStates.Count);
            for (int j = 0; j < m_CurrentLayerRecursiveStates.Count; j++)
            {
               var authoringState = m_CurrentLayerRecursiveStates[j];
               ref var state = ref states[j];

               state.MotionIndex = Array.IndexOf(m_FlatMotions, authoringState.motion);
               state.NameHash = authoringState.name;
               state.Speed = authoringState.speed;
               state.SpeedMultiplierParameter = -1;

               if (authoringState.speedParameterActive)
                  state.SpeedMultiplierParameter = GetParameterIndex(authoringState.speedParameter);
            }

            for (int k = 0; k < m_CurrentLayerRecursiveStates.Count; k++)
            {
               var authoringState = m_CurrentLayerRecursiveStates[k];
               ref var state = ref states[k];

               int stateTransitionCount = CalculateTransitionCountRecursive(authoringState.transitions);
               var transitions = m_BlobBuilder.Allocate(ref state.Transitions, stateTransitionCount);
               int trasitionIndex = 0;

               foreach (var authoringTransition in authoringState.transitions)
                  FillTransitionRecursive(authoringTransition, ref transitions, ref trasitionIndex);
            }

            var authoringAnyStateTransitions = authoringStateMachines.SelectMany((stateMachine) => stateMachine.anyStateTransitions);
            int globalTransitionCount = CalculateTransitionCountRecursive(authoringAnyStateTransitions);
            var globalTransitions = m_BlobBuilder.Allocate(ref layer.StateMachine.GlobalTransitions, globalTransitionCount);
            int globalTrasitionIndex = 0;

            foreach (var authoringAnyStateTransition in authoringAnyStateTransitions)
               FillTransitionRecursive(authoringAnyStateTransition, ref globalTransitions, ref globalTrasitionIndex);

            layer.StateMachine.InitialStateIndex = m_CurrentLayerRecursiveStates.IndexOf(authoringLayer.stateMachine.defaultState);
         }
      }

      private int CalculateTransitionCountRecursive(IEnumerable<AnimatorTransitionBase> authoringTransitions)
      {
         int stateTransitionCount = 0;
         foreach (var authoringTransition in authoringTransitions)
            stateTransitionCount += CalculateTransitionCountRecursive(authoringTransition);

         return stateTransitionCount;
      }

      private int CalculateTransitionCountRecursive(AnimatorTransitionBase authoringTransition)
      {
         if (authoringTransition.mute)
            return 0;

         if (authoringTransition.isExit)
         {
            var state = m_TransitionToSourceState[authoringTransition];
            var stateMachine = m_StateToStateMachine[state];

            int transitionCount = 0;
            foreach (var exitTransition in m_StateMachineToParent[stateMachine].GetStateMachineTransitions(stateMachine))
               transitionCount += CalculateTransitionCountRecursive(exitTransition);

            return transitionCount;
         }
         else if (authoringTransition.destinationStateMachine)
         {
            int transitionCount = 0;
            foreach (var entryTransition in authoringTransition.destinationStateMachine.entryTransitions)
               transitionCount += CalculateTransitionCountRecursive(entryTransition);

            if (authoringTransition.destinationStateMachine.defaultState != null)
               transitionCount++;

            return transitionCount;
         }
         else if (authoringTransition.destinationState)
         {
            return 1;
         }
         return 0;
      }

      private void FillTransitionRecursive(AnimatorTransitionBase authoringTransition, ref BlobBuilderArray<Transition> transitions, ref int transitionIndex)
      {
         if (authoringTransition.mute)
            return;
         
         m_TransitionStack.Push(authoringTransition);
         m_TransitionStackConditionCount += authoringTransition.conditions.Length;

         if (authoringTransition.isExit)
         {
            var state = m_TransitionToSourceState[authoringTransition];
            var stateMachine = m_StateToStateMachine[state];

            foreach (var exitTransition in m_StateMachineToParent[stateMachine].GetStateMachineTransitions(stateMachine))
               FillTransitionRecursive(exitTransition, ref transitions, ref transitionIndex);
         }
         else if (authoringTransition.destinationStateMachine)
         {
            foreach (var entryTransition in authoringTransition.destinationStateMachine.entryTransitions)
               FillTransitionRecursive(entryTransition, ref transitions, ref transitionIndex);

            if (authoringTransition.destinationStateMachine.defaultState != null)
            {
               FillTransition(authoringTransition, ref transitions[transitionIndex], authoringTransition.destinationStateMachine.defaultState);
               transitionIndex++;
            }
         }
         else if (authoringTransition.destinationState)
         {
            FillTransition(authoringTransition, ref transitions[transitionIndex], null);
            transitionIndex++;
         }

         var poppedTransition = m_TransitionStack.Pop();
         m_TransitionStackConditionCount -= poppedTransition.conditions.Length;
      }

      private void FillTransition(AnimatorTransitionBase authoringTransition, ref Transition transition, AnimatorState overrideDestinationState)
      {
         foreach (var stackTransition in m_TransitionStack)
         {
            if (stackTransition is AnimatorStateTransition stateTransition)
            {
               transition.CanTransitionToSelf = stateTransition.canTransitionToSelf;
               transition.Duration = stateTransition.duration;
               transition.Offset = stateTransition.offset;
               transition.ExitTime = stateTransition.exitTime;
               transition.HasFixedDuration = stateTransition.hasFixedDuration;
               transition.HasExitTime = stateTransition.hasExitTime;
               transition.InterruptionSource = (TransitionInterruptionSource)stateTransition.interruptionSource;
               transition.OrderedInterruption = stateTransition.orderedInterruption;
               break;
            }
         }

         var destinationState = overrideDestinationState ? overrideDestinationState : authoringTransition.destinationState;
         transition.DestinationStateIndex = m_CurrentLayerRecursiveStates.IndexOf(destinationState);
         var conditions = m_BlobBuilder.Allocate(ref transition.Conditions, m_TransitionStackConditionCount);
         int conditionIdx = 0;

         foreach (var stackTransition in m_TransitionStack)
         {
            foreach (var authoringCondition in stackTransition.conditions)
            {
               ref var condition = ref conditions[conditionIdx];
               conditionIdx++;

               condition.CompareParameterIndex = GetParameterIndex(authoringCondition.parameter);
               var parameterType = ConvertParameterType(m_AnimatorController.parameters[condition.CompareParameterIndex].type);

               if (parameterType == ParameterType.Float)
                  condition.CompareFloatValue = authoringCondition.threshold;
               else if (parameterType == ParameterType.Int)
                  condition.CompareIntValue = (int)authoringCondition.threshold;

               switch (authoringCondition.mode)
               {
                  case AnimatorConditionMode.If:
                     condition.CompareOperation = CompareOperation.If;
                     break;
                  case AnimatorConditionMode.IfNot:
                     condition.CompareOperation = CompareOperation.IfNot;
                     break;
                  case AnimatorConditionMode.Greater:
                     condition.CompareOperation = parameterType == ParameterType.Float ? CompareOperation.FloatGreater : CompareOperation.IntGreater;
                     break;
                  case AnimatorConditionMode.Less:
                     condition.CompareOperation = parameterType == ParameterType.Float ? CompareOperation.FloatLess : CompareOperation.IntLess;
                     break;
                  case AnimatorConditionMode.Equals:
                     condition.CompareOperation = parameterType == ParameterType.Float ? CompareOperation.FloatEquals : CompareOperation.IntEquals;
                     break;
                  case AnimatorConditionMode.NotEqual:
                     condition.CompareOperation = parameterType == ParameterType.Float ? CompareOperation.FloatNotEqual : CompareOperation.FloatEquals;
                     break;
               }
            }
         }
      }

      private int GetParameterIndex(string name)
      {
         for (int i = 0; i < m_AnimatorController.parameters.Length; i++)
         {
            if (m_AnimatorController.parameters[i].name == name)
               return i;
         }
         return -1;
      }

      private void GetStatesRecursive(List<AnimatorState> outStates, AnimatorStateMachine animatorStateMachine)
      {
         foreach (var childState in animatorStateMachine.states)
            outStates.Add(childState.state);

         foreach (var childStateMachine in animatorStateMachine.stateMachines)
            GetStatesRecursive(outStates, childStateMachine.stateMachine);
      }

      private void GetStateMachinesRecursive(List<AnimatorStateMachine> outAnimatorStateMachines, AnimatorStateMachine animatorStateMachine)
      {
         outAnimatorStateMachines.Add(animatorStateMachine);

         foreach (var childStateMachine in animatorStateMachine.stateMachines)
            GetStateMachinesRecursive(outAnimatorStateMachines, childStateMachine.stateMachine);
      }
   
      private void CreateRelationshipsRecursive(AnimatorStateMachine animatorStateMachine)
      {
         foreach (var childState in animatorStateMachine.states)
         {
            m_StateToStateMachine[childState.state] = animatorStateMachine;

            foreach (var transition in childState.state.transitions)
               m_TransitionToSourceState[transition] = childState.state;
         }

         foreach (var childStateMachine in animatorStateMachine.stateMachines)
         {
            m_StateMachineToParent[childStateMachine.stateMachine] = animatorStateMachine;
            CreateRelationshipsRecursive(childStateMachine.stateMachine);
         }
      }

      private static BlendTreeType ConvertBlendTreeType(UnityEditor.Animations.BlendTreeType blendTreeType)
      {
         return blendTreeType switch
         {
            UnityEditor.Animations.BlendTreeType.Simple1D => BlendTreeType.Simple1D,
            UnityEditor.Animations.BlendTreeType.SimpleDirectional2D => BlendTreeType.SimpleDirectional2D,
            _ => throw new NotImplementedException($"CrowdMorph state machine doesn't support {blendTreeType} blend tree type.")
         };
      }

      private static BlendingMode ConvertBlendingMode(AnimatorLayerBlendingMode blendingMode)
      {
         return blendingMode switch
         {
            AnimatorLayerBlendingMode.Additive => BlendingMode.Additive,
            AnimatorLayerBlendingMode.Override => BlendingMode.Override,
            _ => default
         };
      }

      private static ParameterType ConvertParameterType(AnimatorControllerParameterType parameterType)
      {
         return parameterType switch
         {
            AnimatorControllerParameterType.Float => ParameterType.Float,
            AnimatorControllerParameterType.Int => ParameterType.Int,
            AnimatorControllerParameterType.Bool => ParameterType.Bool,
            AnimatorControllerParameterType.Trigger => ParameterType.Trigger,
            _ => default
         };
      }

      private void FillMotions(ref BlobArray<Motion> motionsBlobArray)
      {
         var motions = m_BlobBuilder.Allocate(ref motionsBlobArray, m_FlatMotions.Length);
         for (int i = 0; i < motions.Length; i++)
         {
            var authoringMotion = m_FlatMotions[i];
            ref var motion = ref motions[i];

            if (authoringMotion is AnimationClip clip)
            {
               motion.Type = MotionType.Clip;
               motion.Clip.AverageLength = clip.averageDuration;
               motion.Clip.FrameRate = clip.frameRate;
               motion.Clip.SampleCount = Core.GetSampleCount(clip.frameRate, clip.averageDuration);
               motion.Clip.WrapMode = clip.isLooping ? WrapMode.Loop : WrapMode.Once;

               if (!m_AnimationClipToClipHashCode.TryGetValue(clip, out var clipHashCode))
                  throw new Exception("Invalid clip.");
               
               motion.Clip.ClipHashCode = clipHashCode;
            }
            else if (authoringMotion is BlendTree blendTree)
            {
               motion.Type = MotionType.BlendTree;
               motion.WrapMode = WrapMode.Loop;
               motion.BlendTree.BlendTreeType = ConvertBlendTreeType(blendTree.blendType);
               motion.BlendTree.HorizontalParameterIndex = GetParameterIndex(blendTree.blendParameter);
               motion.BlendTree.VerticalParameterIndex = GetParameterIndex(blendTree.blendParameterY);

               var nodes = m_BlobBuilder.Allocate(ref motion.BlendTree.Nodes, blendTree.children.Length);
               for (int j = 0; j < blendTree.children.Length; j++)
               {
                  var motionChild = blendTree.children[j];
                  ref var node = ref nodes[j];
                  node.MotionIndex = Array.IndexOf(m_FlatMotions, motionChild.motion);
                  node.Position = motionChild.position;
                  node.MotionSpeed = motionChild.timeScale;
               }
            }
         }
      }

      private UnityEngine.Motion[] FlattenMotions(AnimatorController animatorController)
      {
         var motions = new List<UnityEngine.Motion>();
         foreach (var layer in animatorController.layers)
         {
            var states = new List<AnimatorState>();
            GetStatesRecursive(states, layer.stateMachine);

            foreach (var state in states)
               FlattenMotionRecursive(state.motion, motions);
         }
         return motions.ToArray();
      }

      private void FlattenMotionRecursive(UnityEngine.Motion motion, List<UnityEngine.Motion> outMotions)
      {
         if (motion == null)
            return;

         if (motion is AnimationClip)
         {
            outMotions.Add(motion);
         }
         else if (motion is BlendTree blendTree)
         {
            foreach (var child in blendTree.children)
               FlattenMotionRecursive(child.motion, outMotions);
         }
      }
   }
}
#endif