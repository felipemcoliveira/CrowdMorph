using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

namespace CrowdMorph.Hybrid
{
#if UNITY_EDITOR
   public static class AnimatorControllerUtility
   {
      public static List<AnimationClip> GetAnimationClips(RuntimeAnimatorController runtimeAnimatorController)
      {
         var animatorController = GetAnimatorController(runtimeAnimatorController);
         return GetAnimationClips(animatorController);
      }

      public static List<AnimationClip> GetAnimationClips(AnimatorController animatorController)
      {
         void GetAllAnimatorControllerLayerStates(AnimatorStateMachine stateMachine, List<AnimatorState> outStates)
         {
            foreach (var childState in stateMachine.states)
               outStates.Add(childState.state);

            foreach (var childStateMachine in stateMachine.stateMachines)
               GetAllAnimatorControllerLayerStates(childStateMachine.stateMachine, outStates);
         }

         if (animatorController == null)
            return null;

         var clips = new List<AnimationClip>();
         foreach (var layer in animatorController.layers)
         {
            var states = new List<AnimatorState>();
            GetAllAnimatorControllerLayerStates(layer.stateMachine, states);

            foreach (var state in states)
            {
               if (state.motion is AnimationClip clip && !clips.Contains(clip))
                  clips.Add(clip);
            }
         }
         return clips;
      }

      public static AnimatorController GetAnimatorController(RuntimeAnimatorController runtimeAnimatorController)
      {
         if (runtimeAnimatorController == null)
            return null;

         string animmatorControllerAssetPath = AssetDatabase.GetAssetPath(runtimeAnimatorController);
         return AssetDatabase.LoadAssetAtPath<AnimatorController>(animmatorControllerAssetPath);
      }
   }
#endif
}