using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using System.Runtime.InteropServices;
using Unity.Rendering;

namespace CrowdMorph
{
   [StructLayout(LayoutKind.Sequential)]
   public struct SkinnedMeshBoneData
   {
      public AffineTransform BindPose;
      public int SkeletonBoneIndex;
      internal float3 m_Pad;
   }

   public abstract unsafe class InstantiateSkinnnedMeshSystemBase : SystemBase
   {
      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      protected override void OnCreate()
      {
         m_SkinningSystem = World.GetOrCreateSystem<SkinningSystem>();
         m_GatherSkinnedMeshInstancesSystem = World.GetOrCreateSystem<GatherSkinnedMeshInstancesSystemBase>();

         m_SkinnedMeshHashToInstanceCount = new NativeHashMap<int, int>(16, Allocator.Persistent);
         m_SkinnedMeshHashToSharedData = new NativeHashMap<int, SharedSkinnedMeshData>(16, Allocator.Persistent);

         m_SkinMatricesHeapAllocator = new HeapAllocator(128 * 1024 * 1024);
      }
         
      protected override void OnDestroy()
      {
         m_SkinnedMeshHashToInstanceCount.Dispose();
         m_SkinnedMeshHashToSharedData.Dispose();
         m_SkinMatricesHeapAllocator.Dispose();
      }

      protected unsafe override void OnUpdate()
      {
         Entities
         .WithNone<SharedSkinnedMeshData, SkinMatrixBufferIndex>()
         .WithStructuralChanges()
         .ForEach((Entity entity, in SharedSkinnedMesh sharedSkinnedMesh) => {
            if (sharedSkinnedMesh.Value == BlobAssetReference<SkinnedMeshDefinition>.Null)
            {
               EntityManager.AddSharedComponentData(entity, new SharedSkinnedMeshData());
               return;
            }

            ref var skinnedMesh = ref sharedSkinnedMesh.Value.Value;

            if (m_SkinnedMeshHashToInstanceCount.TryGetValue(skinnedMesh.GetHashCode(), out int instanceCount))
            {
               var sharedSkinnedMeshData = m_SkinnedMeshHashToSharedData[skinnedMesh.GetHashCode()];
               EntityManager.AddSharedComponentData(entity, sharedSkinnedMeshData);
               m_SkinnedMeshHashToInstanceCount[skinnedMesh.GetHashCode()] = instanceCount + 1;
            }
            else
            {
               int skinnedMeshBoneBufferIndex = m_SkinningSystem.SkinningBufferManager.PushSkinnedMeshBonesToBuffer(sharedSkinnedMesh.Value);

               var sharedSkinnedMeshData = new SharedSkinnedMeshData
               {
                  BoneCount = skinnedMesh.BoneCount,
                  SkinnedMeshBoneBufferIndex = skinnedMeshBoneBufferIndex,
                  SkinnedMeshHashCode = skinnedMesh.GetHashCode(),
               };

               EntityManager.AddSharedComponentData(entity, sharedSkinnedMeshData);
               m_SkinnedMeshHashToSharedData.Add(skinnedMesh.GetHashCode(), sharedSkinnedMeshData);
               m_SkinnedMeshHashToInstanceCount.Add(skinnedMesh.GetHashCode(), 1);
            }

            SkeletonMatrixBufferIndex skeleton;
            if (EntityManager.HasComponent<SkeletonEntity>(entity))
            {
               var skeletonEntity = EntityManager.GetComponentData<SkeletonEntity>(entity);
               if (EntityManager.HasComponent<SkeletonMatrixBufferIndex>(skeletonEntity.Value))
               {
                  skeleton = EntityManager.GetComponentData<SkeletonMatrixBufferIndex>(skeletonEntity.Value);
                  EntityManager.AddComponentData(entity, skeleton);
               }
            }
            else
            {
               skeleton = EntityManager.GetComponentData<SkeletonMatrixBufferIndex>(entity);
            }

            var skinMatrixBufferBlock = m_SkinMatricesHeapAllocator.Allocate((ulong)skinnedMesh.BoneCount);
            EntityManager.AddComponentData(entity, new SkinMatrixBufferIndex { Value = (int)skinMatrixBufferBlock.begin });

            m_SkinningSystem.SkinningBufferManager.ResizeSkinMatricesBufferIfRequired((int)m_SkinMatricesHeapAllocator.OnePastHighestUsedAddress);

            m_GatherSkinnedMeshInstancesSystem.GatherSkinnedMeshInstances = true;
         }).Run();


         Entities
         .WithNone<SharedSkinnedMesh>()
         .WithStructuralChanges()
         .ForEach((Entity entity, in SharedSkinnedMeshData sharedSkinnedMeshData, in SkinMatrixBufferIndex skinMatrixBufferIndex) => {
            if (sharedSkinnedMeshData.SkinnedMeshHashCode == 0)
            {
               EntityManager.RemoveComponent<SharedSkinnedMeshData>(entity);
               EntityManager.RemoveComponent<SkinMatrixBufferIndex>(entity);
               return;
            }

            int instanceCount = m_SkinnedMeshHashToInstanceCount[sharedSkinnedMeshData.SkinnedMeshHashCode] - 1;
            m_SkinnedMeshHashToInstanceCount[sharedSkinnedMeshData.SkinnedMeshHashCode] = instanceCount;

            EntityManager.RemoveComponent<SkinMatrixBufferIndex>(entity);
            EntityManager.RemoveComponent<SharedSkinnedMeshData>(entity);
            EntityManager.RemoveComponent<SkeletonMatrixBufferIndex>(entity);

            m_SkinMatricesHeapAllocator.Release(new HeapBlock
            {
               begin = (ulong)skinMatrixBufferIndex.Value,
               end = (ulong)(skinMatrixBufferIndex.Value + sharedSkinnedMeshData.BoneCount)
            });
            m_SkinningSystem.SkinningBufferManager.ResizeSkinMatricesBufferIfRequired((int)m_SkinMatricesHeapAllocator.OnePastHighestUsedAddress);

            m_GatherSkinnedMeshInstancesSystem.GatherSkinnedMeshInstances = true;
         }).Run();
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      NativeHashMap<int, int> m_SkinnedMeshHashToInstanceCount;
      NativeHashMap<int, SharedSkinnedMeshData> m_SkinnedMeshHashToSharedData;
      HeapAllocator m_SkinMatricesHeapAllocator;
      GatherSkinnedMeshInstancesSystemBase m_GatherSkinnedMeshInstancesSystem;
      SkinningSystem m_SkinningSystem;
   }
}

