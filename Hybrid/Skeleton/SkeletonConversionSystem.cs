#if UNITY_EDITOR

using Unity.Entities;
using Unity.Transforms;

namespace CrowdMorph.Hybrid
{
   [ConverterVersion(userName: "CrowdMorph.Hybrid.SkeletonConversionSystem", version: 3)]
   public class SkeletonConversionSystem : GameObjectConversionSystem
   {
      protected override void OnUpdate()
      {
         Entities.ForEach((Skeleton authoringSkeleton) =>
         {
            var entity = GetPrimaryEntity(authoringSkeleton);
            var skeleton = SkeletonBuilder.Build(authoringSkeleton);
            BlobAssetStore.AddUniqueBlobAsset(ref skeleton);

            DstEntityManager.AddSharedComponentData(entity, new SharedSkeleton
            {
               Value = skeleton 
            });

            if (authoringSkeleton.DestroySkeletonHierarchy && HasPrimaryEntity(authoringSkeleton.RootBone))
            {
               var rootBoneEntity = GetPrimaryEntity(authoringSkeleton.RootBone);
               DestroyChildren(rootBoneEntity, DstEntityManager);
               DstEntityManager.DestroyEntity(rootBoneEntity);
            }
         });
      }

      private static void DestroyChildren(Entity entity, EntityManager entityManager)
      {
         if (entityManager.HasComponent<Child>(entity))
         {
            var children = entityManager.GetBuffer<Child>(entity);
            foreach (var child in children)
            {
               DestroyChildren(child.Value, entityManager);
               entityManager.DestroyEntity(child.Value);
            }
         }
      }
   }
}

#endif