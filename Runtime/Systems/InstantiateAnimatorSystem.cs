using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace CrowdMorph
{
   public abstract unsafe class InstantiateAnimatorSystemBase : SystemBase
   {
      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      protected override void OnCreate()
      {
         m_AnimationSystem = World.GetExistingSystem<AnimationSystemBase>();
         m_AnimatorInstanceHashToInstanceCount = new NativeHashMap<int, int>(32, Allocator.Persistent);
         m_AnimatorControllerHashToRefCount = new NativeHashMap<int, int>(8, Allocator.Persistent);
         m_AnimatorControllerHashToControllerEntity = new NativeHashMap<int, Entity>(16, Allocator.Persistent);
      }

      protected override void OnDestroy()
      {
         m_AnimatorInstanceHashToInstanceCount.Dispose();
         m_AnimatorControllerHashToControllerEntity.Dispose();
         m_AnimatorControllerHashToRefCount.Dispose();
      }

      protected override void OnUpdate()
      {
         Entities
         .WithNone<AnimatorControllerOwnerData>()
         .WithStructuralChanges()
         .ForEach((Entity entity, ref AnimatorControllerOwner owner) =>
         {
            var controllerHashCode = owner.Controller.Value.GetHashCode();
            if (m_AnimatorControllerHashToControllerEntity.ContainsKey(controllerHashCode))
            {
               EntityManager.DestroyEntity(entity);
               return;
            }

            EntityManager.AddComponentData(entity, new AnimatorControllerOwnerData { AnimatorControllerHashCode = controllerHashCode });
            m_AnimatorControllerHashToControllerEntity[controllerHashCode] = entity;
         }).Run();

         Entities
         .WithNone<AnimatorControllerOwner>()
         .WithStructuralChanges()
         .ForEach((Entity entity, ref AnimatorControllerOwnerData ownerData) =>
         {
            m_AnimatorControllerHashToControllerEntity.Remove(ownerData.AnimatorControllerHashCode);
         }).Run();

         Entities
         .WithNone<SharedAnimatorControllerData>()
         .WithStructuralChanges()
         .WithEntityQueryOptions(EntityQueryOptions.IncludePrefab)
         .ForEach((Entity entity, SharedAnimatorController sharedAnimator, SharedSkeleton sharedSkeleton) =>
         {
            if (sharedAnimator.Controller == BlobAssetReference<AnimatorControllerDefinition>.Null || sharedSkeleton.Value == BlobAssetReference<SkeletonDefinition>.Null)
            {
               EntityManager.AddSharedComponentData(entity, new SharedAnimatorControllerData());
               return;
            }

            int controllerHashCode = sharedAnimator.Controller.Value.GetHashCode();
            int animatorInstanceHashCode = HashUtility.GetAnimatorInstanceHash(sharedAnimator.Controller, sharedSkeleton.Value);

            if (m_AnimatorInstanceHashToInstanceCount.Incremenet(animatorInstanceHashCode))
            {
               var controllerEntity = m_AnimatorControllerHashToControllerEntity[controllerHashCode];
               m_AnimatorControllerHashToRefCount.Incremenet(controllerHashCode);

               var clipRefBuffer = EntityManager.GetBuffer<ClipRef>(controllerEntity);
               foreach (var clipRef in clipRefBuffer)
                  m_AnimationSystem.ClipBufferManager.RetainClipInstance(clipRef.Value, sharedSkeleton.Value);

               var skeletonMaskRefBuffer = EntityManager.GetBuffer<SkeletonMaskRef>(controllerEntity);
               foreach (var skeletonMaskRef in skeletonMaskRefBuffer)
                  m_AnimationSystem.SkeletonBufferManager.PushSkeletonMaskToBuffer(sharedSkeleton.Value, skeletonMaskRef.Value);
            }

            EntityManager.AddSharedComponentData(entity, new SharedAnimatorControllerData
            {
               ControllerHashCode = controllerHashCode
            });

            int layerCount = sharedAnimator.Controller.Value.LayerCount;
            var layerStates = EntityManager.AddBuffer<LayerState>(entity);
            layerStates.ResizeUninitialized(layerCount);
            UnsafeUtility.MemClear(layerStates.GetUnsafePtr(), sizeof(LayerState) * layerCount);
         }).Run();

         Entities
         .WithNone<SharedAnimatorController>()
         .WithStructuralChanges()
         .WithEntityQueryOptions(EntityQueryOptions.IncludePrefab)
         .ForEach((Entity entity, SharedAnimatorControllerData sharedAnimatorData, SharedSkeletonData sharedSkeletonData) =>
         {
            if (sharedAnimatorData.ControllerHashCode == 0 || sharedSkeletonData.SkeletonHashCode == 0)
            {
               EntityManager.RemoveComponent<SharedAnimatorControllerData>(entity);
               return;
            }

            int animatorInstanceHashCode = HashUtility.GetAnimatorInstanceHash(sharedAnimatorData.ControllerHashCode, sharedSkeletonData.SkeletonHashCode);
            int instanceCount = m_AnimatorInstanceHashToInstanceCount[animatorInstanceHashCode] - 1;
            if (m_AnimatorInstanceHashToInstanceCount.Decrement(animatorInstanceHashCode))
            {
               EntityManager.RemoveComponent<SharedAnimatorControllerData>(entity);
               var controllerEntity = m_AnimatorControllerHashToControllerEntity[sharedAnimatorData.ControllerHashCode];

               var clipRefBuffer = EntityManager.GetBuffer<ClipRef>(controllerEntity);
               foreach (var clipRef in clipRefBuffer)
                  m_AnimationSystem.ClipBufferManager.ReleaseClipInstance(clipRef.Value.GetHashCode(), sharedSkeletonData.SkeletonHashCode);

               if (m_AnimatorControllerHashToRefCount.Decrement(sharedAnimatorData.ControllerHashCode))
                  EntityManager.DestroyEntity(controllerEntity);
            }

         }).Run();
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      NativeHashMap<int, int> m_AnimatorInstanceHashToInstanceCount;
      NativeHashMap<int, int> m_AnimatorControllerHashToRefCount;
      NativeHashMap<int, Entity> m_AnimatorControllerHashToControllerEntity;
      AnimationSystemBase m_AnimationSystem;
   }
}

   