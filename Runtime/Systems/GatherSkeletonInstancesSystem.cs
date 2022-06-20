using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace CrowdMorph
{
   public struct SkeletonInstanceBatch
   {
      public int BoneCount;
      public int InstanceBufferStartIndex;
      public NativeList<int> Instances;
      public JobHandle InstancesGathererHandle;
      public int SkeletonHashCode;

      public int InstanceCount => Instances.Length;
   }

   public abstract unsafe class GatherSkeletonInstancesSystemBase : JobComponentSystem
   {
      internal bool GatherSkeletonInstances { get; set; }

      // ----------------------------------------------------------------------------------------
      // Job Structures
      // ----------------------------------------------------------------------------------------

      struct GatherSkeletonInstancesJob : IJobEntityBatch
      {
         public static EntityQueryDesc QueryDesc => new EntityQueryDesc()
         {
            All = new ComponentType[]
            {
               ComponentType.ReadOnly<SkeletonMatrixBufferIndex>(),
               ComponentType.ReadOnly<SharedSkeletonData>(),
               ComponentType.ReadOnly<SharedSkeleton>()
            },
         };

         [ReadOnly]
         public ComponentTypeHandle<SkeletonMatrixBufferIndex> SkeletonMatrixBufferIndexType;

         [WriteOnly]
         public NativeList<int>.ParallelWriter Instances;

         [SkipLocalsInit]
         public void Execute(ArchetypeChunk chunk, int batchIndex)
         {
            var skeletonMatrixBufferIndexArray = chunk.GetNativeArray(SkeletonMatrixBufferIndexType);
            Instances.AddRangeNoResize(skeletonMatrixBufferIndexArray.GetUnsafeReadOnlyPtr(), chunk.Count);
         }
      }

      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      protected override void OnCreate()
      {
         m_AllUniqueSharedSkeletonData = new List<SharedSkeletonData>();
         m_InstancesBatches = new List<SkeletonInstanceBatch>();
         m_EntityQuery = GetEntityQuery(GatherSkeletonInstancesJob.QueryDesc);
         m_BatchesVersion = 0;
      }

      protected override void OnDestroy()
      {
          foreach (var batch in m_InstancesBatches)
         {
            if (batch.Instances.IsCreated)
               batch.Instances.Dispose();
         }
      }

      protected override JobHandle OnUpdate(JobHandle inputDeps)
      {
         if (!GatherSkeletonInstances)
            return inputDeps;

         foreach (var previousInstanceBatch in m_InstancesBatches)
            previousInstanceBatch.Instances.Dispose();

         m_InstancesBatches.Clear();

         m_AllUniqueSharedSkeletonData.Clear();
         EntityManager.GetAllUniqueSharedComponentData(m_AllUniqueSharedSkeletonData);

         int instanceBufferOffset = 0;
         for (int i = 0; i < m_AllUniqueSharedSkeletonData.Count; i++)
         {
            var sharedSkeletonData = m_AllUniqueSharedSkeletonData[i];
            if (sharedSkeletonData.BoneCount == 0)
               continue;

            m_EntityQuery.SetSharedComponentFilter(sharedSkeletonData);
            var entityCount = m_EntityQuery.CalculateEntityCount();
            if (entityCount == 0)
               continue;

            var instances = new NativeList<int>(entityCount, Allocator.Persistent);

            inputDeps = new GatherSkeletonInstancesJob
            {
               SkeletonMatrixBufferIndexType = GetComponentTypeHandle<SkeletonMatrixBufferIndex>(true),
               Instances = instances.AsParallelWriter()
            }.ScheduleParallel(m_EntityQuery, 32, inputDeps);
               
            m_EntityQuery.ResetFilter();
               
            m_InstancesBatches.Add(new SkeletonInstanceBatch
            {
               BoneCount = sharedSkeletonData.BoneCount,
               SkeletonHashCode = sharedSkeletonData.SkeletonHashCode,
               Instances = instances,
               InstancesGathererHandle = inputDeps,
               InstanceBufferStartIndex = instanceBufferOffset,
            });

            instanceBufferOffset += entityCount;
         }

         m_BatchesVersion++;
         GatherSkeletonInstances = false;
         return inputDeps;
      }

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      public List<SkeletonInstanceBatch> GetSkeletonInstanceBatches(out int batchesVersion)
      {
         batchesVersion = m_BatchesVersion;
         return m_InstancesBatches;
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      EntityQuery m_EntityQuery;
      List<SharedSkeletonData> m_AllUniqueSharedSkeletonData;
      List<SkeletonInstanceBatch> m_InstancesBatches;
      int m_BatchesVersion;

   }
}