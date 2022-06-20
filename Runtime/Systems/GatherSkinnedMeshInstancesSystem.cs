using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;

namespace CrowdMorph
{
   public struct SkinnedMeshInstanceBatch
   {
      public int BoneCount;
      public int InstanceOffset;
      public NativeList<SkinnedMeshInstance> Instances;
      public JobHandle InstancesGathererHandle;
      public int SkinnedMeshBoneBufferIndex;

      public int InstanceCount => Instances.Length;
   }

   public abstract unsafe class GatherSkinnedMeshInstancesSystemBase : JobComponentSystem
   {
      internal bool GatherSkinnedMeshInstances { get; set; }

      // ----------------------------------------------------------------------------------------
      // Job Structures
      // ----------------------------------------------------------------------------------------

      struct GatherSkinnedMeshInstancesJob : IJobEntityBatch
      {
         public static EntityQueryDesc QueryDesc => new EntityQueryDesc()
         {
            All = new ComponentType[]
            {
               ComponentType.ReadOnly<SkeletonMatrixBufferIndex>(),
               ComponentType.ReadOnly<SkinMatrixBufferIndex>(),
               ComponentType.ReadOnly<SharedSkinnedMeshData>(),
               ComponentType.ReadOnly<SharedSkinnedMesh>()
            },
         };

         [ReadOnly]
         public ComponentTypeHandle<SkeletonMatrixBufferIndex> SkeletonType;

         [ReadOnly]
         public ComponentTypeHandle<SkinMatrixBufferIndex> SkinMatrixBufferIndexType;

         [WriteOnly]
         public NativeList<SkinnedMeshInstance>.ParallelWriter Instances;

         [SkipLocalsInit]
         public void Execute(ArchetypeChunk chunk, int batchIndex)
         {
            var skeletonArray = chunk.GetNativeArray(SkeletonType);
            var skinMatrixBufferIndexArray = chunk.GetNativeArray(SkinMatrixBufferIndexType);

            var instances = stackalloc SkinnedMeshInstance[chunk.Count];

            for (int i = 0; i < chunk.Count; i++)
            {
               instances[i] = new SkinnedMeshInstance
               {
                  SkeletonMatixBufferIndex = skeletonArray[i].Value,
                  SkinMatixBufferIndex = skinMatrixBufferIndexArray[i].Value,
               };
            }

            Instances.AddRangeNoResize(instances, chunk.Count);
         }
      }

      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      protected override void OnCreate()
      {
         m_AllUniqueSharedSkinnedMeshData = new List<SharedSkinnedMeshData>();
         m_InstancesBatches = new List<SkinnedMeshInstanceBatch>();
         m_EntityQuery = GetEntityQuery(GatherSkinnedMeshInstancesJob.QueryDesc);
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
         if (!GatherSkinnedMeshInstances)
            return inputDeps;

         foreach (var previousInstanceBatch in m_InstancesBatches)
            previousInstanceBatch.Instances.Dispose();

         m_InstancesBatches.Clear();

         m_AllUniqueSharedSkinnedMeshData.Clear();
         EntityManager.GetAllUniqueSharedComponentData(m_AllUniqueSharedSkinnedMeshData);

         int instanceBufferOffset = 0;
         for (int i = 0; i < m_AllUniqueSharedSkinnedMeshData.Count; i++)
         {
            var sharedSkinnedMeshData = m_AllUniqueSharedSkinnedMeshData[i];
            if (sharedSkinnedMeshData.BoneCount == 0)
               continue;

            m_EntityQuery.SetSharedComponentFilter(sharedSkinnedMeshData);
            var entityCount = m_EntityQuery.CalculateEntityCount();
            if (entityCount == 0)
               continue;

            var instances = new NativeList<SkinnedMeshInstance>(entityCount, Allocator.Persistent);

            inputDeps = new GatherSkinnedMeshInstancesJob
            {
               SkinMatrixBufferIndexType = GetComponentTypeHandle<SkinMatrixBufferIndex>(true),
               SkeletonType = GetComponentTypeHandle<SkeletonMatrixBufferIndex>(true),
               Instances = instances.AsParallelWriter()
            }.ScheduleParallel(m_EntityQuery, 8, inputDeps);
               
            m_EntityQuery.ResetFilter();
               
            m_InstancesBatches.Add(new SkinnedMeshInstanceBatch
            {
               BoneCount = sharedSkinnedMeshData.BoneCount,
               Instances = instances,
               InstancesGathererHandle = inputDeps,
               InstanceOffset = instanceBufferOffset,
               SkinnedMeshBoneBufferIndex = sharedSkinnedMeshData.SkinnedMeshBoneBufferIndex
            });

            instanceBufferOffset += entityCount;
         }

         m_BatchesVersion++;
         GatherSkinnedMeshInstances = false;
         return inputDeps;
      }

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      public List<SkinnedMeshInstanceBatch> GetSkinnedMeshInstanceBatches(out int batchesVersion)
      {
         batchesVersion = m_BatchesVersion;
         return m_InstancesBatches;
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      EntityQuery m_EntityQuery;
      List<SharedSkinnedMeshData> m_AllUniqueSharedSkinnedMeshData;
      List<SkinnedMeshInstanceBatch> m_InstancesBatches;
      int m_BatchesVersion;
   }
}