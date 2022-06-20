#if UNITY_EDITOR

using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace CrowdMorph.Hybrid
{
   [UpdateAfter(typeof(AnimatorConversionSystem))]
   [ConverterVersion(userName: "CrowdMorph.Hybrid.SkinnedMeshConversionSystem", version: 3)]
   [WorldSystemFilter(WorldSystemFilterFlags.HybridGameObjectConversion)]
   public class SkinnedMeshConversionSystem : GameObjectConversionSystem
   {
      protected override void OnUpdate()
      {
         Entities.ForEach((Entity gameObjectEntity, SkinnedMeshRenderer smr) =>
         {
            var entity = GetPrimaryEntity(smr);

            bool shouldOverrideDefaultConversion = smr.gameObject.GetComponentInParent<Animator>(true);
            if (!shouldOverrideDefaultConversion)
               return;

            // avoid default SkinnedMeshRenderer conversion
            EntityManager.RemoveComponent<SkinnedMeshRenderer>(gameObjectEntity);

            DstEntityManager.SetComponentData(entity, new Translation { Value = float3.zero });
            DstEntityManager.SetComponentData(entity, new Rotation { Value = quaternion.identity });

            var meshDesc = new RenderMeshDescription(smr, smr.sharedMesh);
            RenderMeshUtility.AddComponents(entity, DstEntityManager, meshDesc);

            ConfigureEditorRenderData(entity, smr.gameObject, true);

            var skeleton = smr.gameObject.GetComponentInParent<Skeleton>(true);
            if (skeleton == null)
            {
               Debug.LogWarning("SkinnedMesh cannot find a Skeleton component in any parent.");
               return;
            }

            var skinnedMeshToRootBoneMatrix = skeleton.RootBone.worldToLocalMatrix * smr.rootBone.localToWorldMatrix;
            var bounds = (MinMaxAABB) new AABB
            {
               Center = skinnedMeshToRootBoneMatrix.MultiplyPoint3x4(smr.localBounds.center),
               Extents = skinnedMeshToRootBoneMatrix.MultiplyVector(smr.localBounds.size) / 2,
            };

            //var clips = new List<AnimationClip>();
            //foreach (var clipSources in smr.GetComponentsInParent<IAnimationClipSource>())
            //   clipSources.GetAnimationClips(clips);

            //var skeletonRootWorldToLocal = skeleton.RootBone.worldToLocalMatrix;

            //AnimationMode.StartAnimationMode();
            //AnimationMode.BeginSampling();

            //var skeletonGameObject = skeleton.RootBone.gameObject;
            //foreach (var clip in clips)
            //{
            //   int frameCount = (int)math.ceil(clip.frameRate * clip.length);
            //   for (int i = 0; i <= frameCount; i++)
            //   {
            //      AnimationMode.SampleAnimationClip(skeletonGameObject, clip, i * clip.frameRate);
            //      var rootToBone = skeletonRootWorldToLocal * smr.rootBone.localToWorldMatrix;

            //   }
            //}

            //AnimationMode.EndSampling();
            //AnimationMode.StopAnimationMode();

            DstEntityManager.AddComponentData(entity, new RenderBounds { Value = bounds });

            var skinnedMesh = SkinnedMeshBuilder.Build(smr, skeleton);
            BlobAssetStore.AddUniqueBlobAsset(ref skinnedMesh);
            DstEntityManager.AddSharedComponentData(entity, new SharedSkinnedMesh
            {
               Value = skinnedMesh
            });

            if (skeleton.gameObject != smr.gameObject)
            {
               DstEntityManager.AddComponentData(entity, new SkeletonEntity
               {
                  Value = GetPrimaryEntity(skeleton)
               });
            }
         });
      }
   }
}

#endif
