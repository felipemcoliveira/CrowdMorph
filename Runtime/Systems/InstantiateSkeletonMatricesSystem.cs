using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Rendering;

namespace CrowdMorph
{

   public abstract unsafe class InstantiateSkeletonMatricesSystemBase : SystemBase
   {
      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      protected override void OnCreate()
      {
         m_AnimationSystem = World.GetOrCreateSystem<AnimationSystemBase>();
         m_GatherSkeletonInstancesSystem = World.GetOrCreateSystem<GatherSkeletonInstancesSystemBase>();

         m_SkeletonHashToInstanceCount = new NativeHashMap<int, int>(32, Allocator.Persistent);

         m_SkeletonMatricesAllocator = new HeapAllocator(128 * 1024 * 1024);
      }

      protected override void OnDestroy()
      {
         m_SkeletonHashToInstanceCount.Dispose();
         m_SkeletonMatricesAllocator.Dispose();
      }

      protected override void OnUpdate()
      {
         Entities
         .WithNone<SharedSkeletonData>()
         .WithStructuralChanges()
         .WithEntityQueryOptions(EntityQueryOptions.IncludePrefab)
         .ForEach((Entity entity, SharedSkeleton sharedSkeleton) =>
         {
            if (sharedSkeleton.Value == BlobAssetReference<SkeletonDefinition>.Null)
            {
               EntityManager.AddSharedComponentData(entity, new SharedSkeletonData());
               return;
            }

            var skeleton = sharedSkeleton.Value;
            int skeletonHashCode = skeleton.Value.GetHashCode();
            m_AnimationSystem.SkeletonBufferManager.PushSharedSkeletonData(skeleton);

            m_SkeletonHashToInstanceCount.Incremenet(skeletonHashCode);

            int instanceCount = m_SkeletonHashToInstanceCount[skeletonHashCode];
            m_AnimationSystem.AnimationCommandBufferManager.ResizeAnimationCommandListIfRequired(skeletonHashCode, instanceCount + 1);

            EntityManager.AddSharedComponentData(entity, new SharedSkeletonData
            {
               BoneCount = skeleton.Value.BoneCount,
               SkeletonHashCode = skeletonHashCode,
            });
         }).Run();

         Entities
         .WithNone<SkeletonMatrixBufferIndex>()
         .WithStructuralChanges()
         .ForEach((Entity entity, SharedSkeletonData sharedSkeletonData) =>
         {
            if (sharedSkeletonData.SkeletonHashCode == 0)
            {
               EntityManager.AddComponentData(entity, new SkeletonMatrixBufferIndex());
               return;
            }

            var matrixBufferBlock = m_SkeletonMatricesAllocator.Allocate((ulong)sharedSkeletonData.BoneCount);
            int requiredSize = (int)m_SkeletonMatricesAllocator.OnePastHighestUsedAddress;
            m_AnimationSystem.SkeletonBufferManager.ResizeSkeletonMatricesBufferIfRequired(requiredSize);
            m_GatherSkeletonInstancesSystem.GatherSkeletonInstances = true;

            EntityManager.AddComponentData(entity, new SkeletonMatrixBufferIndex { Value = (int)matrixBufferBlock.begin });
         }).Run();

         Entities
         .WithNone<SharedSkeleton>()
         .WithStructuralChanges()
         .WithEntityQueryOptions(EntityQueryOptions.IncludePrefab)
         .ForEach((Entity entity, SharedSkeletonData sharedSkeletonData) =>
         {
            if (sharedSkeletonData.SkeletonHashCode == 0)
            {
               EntityManager.RemoveComponent<SharedSkeletonData>(entity);
               return;
            }

            int instanceCount = m_SkeletonHashToInstanceCount[sharedSkeletonData.SkeletonHashCode];
            m_AnimationSystem.AnimationCommandBufferManager.ResizeAnimationCommandListIfRequired(sharedSkeletonData.SkeletonHashCode, instanceCount);

            if (m_SkeletonHashToInstanceCount.Decrement(sharedSkeletonData.SkeletonHashCode))
            {
               if (EntityManager.HasComponent<SkeletonMatrixBufferIndex>(entity))
               {
                  var skeleton = EntityManager.GetComponentData<SkeletonMatrixBufferIndex>(entity);

                  m_SkeletonMatricesAllocator.Release(new HeapBlock
                  {
                     begin = (ulong)skeleton.Value,
                     end = (ulong)(skeleton.Value + sharedSkeletonData.BoneCount)
                  });

                  int requiredSize = (int)m_SkeletonMatricesAllocator.OnePastHighestUsedAddress;
                  m_AnimationSystem.SkeletonBufferManager.ResizeSkeletonMatricesBufferIfRequired(requiredSize);
               }
            }

            EntityManager.RemoveComponent<SharedSkeletonData>(entity);
            EntityManager.RemoveComponent<SkeletonMatrixBufferIndex>(entity);
            m_GatherSkeletonInstancesSystem.GatherSkeletonInstances = true;
         }).Run();
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      NativeHashMap<int, int> m_SkeletonHashToInstanceCount;
      AnimationSystemBase m_AnimationSystem;
      GatherSkeletonInstancesSystemBase m_GatherSkeletonInstancesSystem;
      HeapAllocator m_SkeletonMatricesAllocator;

   }
}

