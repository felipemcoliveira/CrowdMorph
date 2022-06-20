using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdMorph
{
   public class HashUtility
   {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static int GetClipInstanceHash(BlobAssetReference<Clip> clip, BlobAssetReference<SkeletonDefinition> rig)
      {
         return GetAnimatorInstanceHash(clip.Value.GetHashCode(), rig.Value.GetHashCode());
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static int GetClipInstanceHash(int clipHashCode, int skeletonHashCode)
      {
         return Hash(clipHashCode, skeletonHashCode);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static int GetAnimatorInstanceHash(BlobAssetReference<AnimatorControllerDefinition> animator, BlobAssetReference<SkeletonDefinition> skeleton)
      {
         return GetAnimatorInstanceHash(animator.Value.GetHashCode(), skeleton.Value.GetHashCode());
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static int GetSkeletonMaskInstanceHash(BlobAssetReference<SkeletonMaskDefinition> mask, BlobAssetReference<SkeletonDefinition> skeleton)
      {
         return GetSkeletonMaskInstanceHash(mask.Value.GetHashCode(), skeleton.Value.GetHashCode());
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static int GetSkeletonMaskInstanceHash(int maskHashCode, int skeletonHashCode)
      {
         if (maskHashCode == 0)
            return 0;

         return Hash(maskHashCode, skeletonHashCode);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static int GetAnimatorInstanceHash(int animatorHashCode, int rigHashCode)
      {
         return Hash(animatorHashCode, rigHashCode);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static int ComputeSkeletonHash(ref SkeletonDefinition skeleton)
      {
         unchecked
         {
            uint hashCode = ComputeHash(ref skeleton.BoneIDs);
            hashCode = ComputeHash(ref skeleton.BoneLocalRotationsDefaultValues, hashCode);
            hashCode = ComputeHash(ref skeleton.BoneLocalTranslationDefaultValues, hashCode);
            hashCode = ComputeHash(ref skeleton.BoneLocalScalesDefaultValues, hashCode);
            return (int)hashCode;
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static int ComputeAvatarMaskHash(ref SkeletonMaskDefinition avatarMask)
      {
         unchecked
         {
            uint hashCode = ComputeHash(ref avatarMask.BoneIDs);
            hashCode = ComputeHash(ref avatarMask.IsBoneActive, hashCode);

            return (int)hashCode;
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static int ComputeSkinnedMeshHash(ref SkinnedMeshDefinition skinnedMesh)
      {
         unchecked
         {
            uint hashCode = ComputeHash(ref skinnedMesh.BindPoses);
            return (int)ComputeHash(ref skinnedMesh.SkinToSkeletonBoneIndices, hashCode);
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static int ComputeClipHash(ref Clip clip)
      {
         unchecked
         {
            var hashCode = math.hash(new float2(clip.FrameRate, clip.Length));
            hashCode = (uint)Hash((int)hashCode, (int)clip.WrapMode);
            hashCode = ComputeHash(ref clip.TranslationsBindings, hashCode);
            hashCode = ComputeHash(ref clip.RotationBindings, hashCode);
            hashCode = ComputeHash(ref clip.ScalesBindings, hashCode);
            hashCode = ComputeHash(ref clip.LocalTranslations, hashCode);
            hashCode = ComputeHash(ref clip.LocalScales, hashCode);
            hashCode = ComputeHash(ref clip.LocalRotations, hashCode);
            hashCode = ComputeHash(ref clip.Events, hashCode);
            return (int)hashCode;
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public unsafe static int ComputeAnimatorHash(ref AnimatorControllerDefinition stateMachine)
      {
         unchecked
         {
            ulong parametersStableTypeHash = stateMachine.ParametersStableTypeHash;
            uint hashCode = math.hash(&parametersStableTypeHash, sizeof(ulong));

            hashCode = ComputeHash(ref stateMachine.Parameters, hashCode);
            hashCode = ComputeHash(ref stateMachine.Layers, hashCode);
            hashCode = ComputeHash(ref stateMachine.Motions, hashCode);

            for (int motionIndex = 0; motionIndex < stateMachine.Motions.Length; motionIndex++)
            {
               ref var motion = ref stateMachine.Motions[motionIndex];
               if (motion.Type == MotionType.BlendTree)
                  hashCode = ComputeHash(ref motion.BlendTree.Nodes, hashCode);
            }

            for (int layerIdx = 0; layerIdx < stateMachine.LayerCount; layerIdx++)
            {
               ref var layer = ref stateMachine.Layers[layerIdx];
               hashCode = ComputeHash(ref layer.StateMachine.States, hashCode);
               for (int stateIdx = 0; stateIdx < layer.StateMachine.States.Length; stateIdx++)
               {
                  ref var state = ref layer.StateMachine.States[stateIdx];
                  hashCode = ComputeHash(ref state.Transitions, hashCode);
                  for (int transitionIdx = 0; transitionIdx < state.Transitions.Length; transitionIdx++)
                  {
                     ref var transition = ref state.Transitions[transitionIdx];
                     hashCode = ComputeHash(ref state.Transitions[transitionIdx].Conditions, hashCode);
                  }
               }
            }
            return (int)hashCode;
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      private static int Hash(int lhs, int rh)
      {
         return unchecked((int)math.hash(new int2(lhs, rh)));  
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      private static unsafe uint ComputeHash<T>(ref BlobArray<T> array, uint seed = 0) where T : struct
      {
         return math.hash(array.GetUnsafePtr(), array.Length * UnsafeUtility.SizeOf<T>(), seed);
      }

   }
}
 
