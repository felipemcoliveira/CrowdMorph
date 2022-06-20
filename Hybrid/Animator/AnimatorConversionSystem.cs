#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

using Hash128 = Unity.Entities.Hash128;

namespace CrowdMorph.Hybrid
{
   [ConverterVersion(userName: "CrowdMorph.Hybrid.AnimatorConversionSystem", version: 1)]
   public class AnimatorConversionSystem : GameObjectConversionSystem
   {
      Dictionary<AnimationClip, int> m_AnimationClipToClipHashCode = new Dictionary<AnimationClip, int>();
      Dictionary<AvatarMask, int> m_AvatarMaskToSkeletonMaskHashCode = new Dictionary<AvatarMask, int>();

      protected override void OnUpdate()
      {
         var animatorComputationContext = new BlobAssetComputationContext<int, AnimatorControllerDefinition>(BlobAssetStore, 16, Allocator.Temp);
         var clipComputationContext = new BlobAssetComputationContext<int, Clip>(BlobAssetStore, 64, Allocator.Temp);

         Entities.ForEach((Animator animator) =>
         {
            var animatorController = AnimatorControllerUtility.GetAnimatorController(animator.Controller);

            if (animatorController == null)
               return;

            DeclareAssetDependency(animator.gameObject, animatorController);

            var entity = GetPrimaryEntity(animator);
            var animatorControllerEntity = GetPrimaryEntity(animator.Controller);

            AddClipRefBuffer(animatorController, animator.gameObject, clipComputationContext, animatorControllerEntity);
            AddSkeletonRefRefBuffer(animatorController, animator.gameObject, animatorControllerEntity);

            var animatorAssetHash = GetAssetHash(animatorController);

            animatorComputationContext.AssociateBlobAssetWithUnityObject(animatorAssetHash, animatorController);
            if (animatorComputationContext.NeedToComputeBlobAsset(animatorAssetHash))
            {
               animatorComputationContext.AddBlobAssetToCompute(animatorAssetHash, default);

               var computedAnimator = AnimatorControllerBuilder.Build(
                  animatorController,
                  m_AnimationClipToClipHashCode,
                  m_AvatarMaskToSkeletonMaskHashCode,
                  animator.ParametersComponentType
               );

               if (computedAnimator.IsCreated)
                  animatorComputationContext.AddComputedBlobAsset(animatorAssetHash, computedAnimator);
            }
            animatorComputationContext.GetBlobAsset(animatorAssetHash, out var controller);

            AddParametersComponentData(entity, controller);

            DstEntityManager.AddComponentData(animatorControllerEntity, new AnimatorControllerOwner { Controller = controller });
            DstEntityManager.AddSharedComponentData(entity, new SharedAnimatorController { Controller = controller });
         });

         animatorComputationContext.Dispose();
         clipComputationContext.Dispose();
      }

      private void AddParametersComponentData(Entity entity, BlobAssetReference<AnimatorControllerDefinition> animator)
      {
         Core.ValidateIsCreated(animator);
         int parametersComponentTypeIndex = TypeManager.GetTypeIndexFromStableTypeHash(animator.Value.ParametersStableTypeHash);
         var parametersComponentType = ComponentType.FromTypeIndex(parametersComponentTypeIndex);
         DstEntityManager.AddComponent(entity, parametersComponentType);
      }

      private void AddSkeletonRefRefBuffer(
         AnimatorController animatorController,
         GameObject gameObject,
         Entity animatorControllerEntity
      )
      {
         if (EntityManager.HasComponent<SkeletonMaskRef>(animatorControllerEntity))
            return;

         var skeletonRefBuffer = DstEntityManager.AddBuffer<SkeletonMaskRef>(animatorControllerEntity);
         var avatarMasks = new List<AvatarMask>();

         foreach (var layer in animatorController.layers)
         {
            if (layer.avatarMask && !avatarMasks.Contains(layer.avatarMask))
               avatarMasks.Add(layer.avatarMask);
         }

         foreach (var avatarMask in avatarMasks)
         {
            DeclareAssetDependency(gameObject, avatarMask);
            var skeletonMask = SkeletonMaskBuilder.Build(avatarMask);
            BlobAssetStore.AddUniqueBlobAsset(ref skeletonMask);

            skeletonRefBuffer.Add(new SkeletonMaskRef { Value = skeletonMask });

            m_AvatarMaskToSkeletonMaskHashCode[avatarMask] = skeletonMask.Value.GetHashCode();
         }
      }

      private void AddClipRefBuffer(
         AnimatorController animatorController,
         GameObject gameObject,
         BlobAssetComputationContext<int, Clip> clipComputationContext,
         Entity animatorControllerEntity
      )
      {
         if (EntityManager.HasComponent<ClipRef>(animatorControllerEntity))
            return;

         var clipRefBuffer = DstEntityManager.AddBuffer<ClipRef>(animatorControllerEntity);
         var clips = AnimatorControllerUtility.GetAnimationClips(animatorController);

         foreach (var authoringClip in clips)
         {
            DeclareAssetDependency(gameObject, authoringClip);
            
            var clipAssetHash = GetAssetHash(authoringClip);

            clipComputationContext.AssociateBlobAssetWithUnityObject(clipAssetHash, authoringClip);
            if (clipComputationContext.NeedToComputeBlobAsset(clipAssetHash))
            {
               clipComputationContext.AddBlobAssetToCompute(clipAssetHash, default);
               var clip = ClipBuilder.Build(authoringClip);

               if (clip.IsCreated)
                  clipComputationContext.AddComputedBlobAsset(clipAssetHash, clip);
            }
            clipComputationContext.GetBlobAsset(clipAssetHash, out var uniqueClip);
            clipRefBuffer.Add(new ClipRef { Value = uniqueClip });

            m_AnimationClipToClipHashCode[authoringClip] = uniqueClip.Value.GetHashCode();
         }
      }

      public static void GetAllAnimatorControllerLayerStates(AnimatorStateMachine stateMachine, List<AnimatorState> outStates)
      {
         foreach (var childState in stateMachine.states)
            outStates.Add(childState.state);

         foreach (var childStateMachine in stateMachine.stateMachines)
            GetAllAnimatorControllerLayerStates(childStateMachine.stateMachine, outStates);
      }

      public static Hash128 GetAssetHash(UnityEngine.Object asset)
      {
         if (asset == null)
         {
            return default;
         }

         if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long fileID))
         {
            var hash = new Hash128(guid);
            hash.Value.w ^= (uint)fileID;
            hash.Value.z ^= (uint)(fileID >> 32);
            return hash;
         }
 
         return new Hash128((uint)asset.GetInstanceID(), (uint)asset.GetType().GetHashCode(), 0xABCD, 0xEF01);
      }
   }
}

#endif