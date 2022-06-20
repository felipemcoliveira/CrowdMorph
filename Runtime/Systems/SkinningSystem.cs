using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

namespace CrowdMorph
{
   public abstract unsafe class SkinningSystemBase : SystemBase
   {
      internal SkinningBufferManager SkinningBufferManager { get; private set; }

      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      protected override void OnCreate()
      { 
         m_ComputeSkinMatricesComputeShader = Resources.Load<ComputeShader>("CrowdMorph/ComputeSkinMatricesComputeShader");
         m_ComputeSkinMatricesComputeKernel = m_ComputeSkinMatricesComputeShader.FindKernel("ComputeSkinMatricesComputeKernel");

         SkinningBufferManager = new SkinningBufferManager();
         SkinningBufferManager.OnCreate();

         m_AnimationSystem = World.GetOrCreateSystem<AnimationSystemBase>();
         m_GatherSkinnedMeshInstancesSystem = World.GetOrCreateSystem<GatherSkinnedMeshInstancesSystemBase>();

         m_InstanceBufferStartIndexPropertyID = Shader.PropertyToID("g_InstanceBufferStartIndex");
         m_InstanceCountPropertyID = Shader.PropertyToID("g_InstanceCount");
         m_BoneCountPropertyID = Shader.PropertyToID("g_BoneCount");
         m_SkinBoneDataBufferIndexPropertyID = Shader.PropertyToID("g_SkinBoneDataBufferIndex");
      }

      protected override void OnDestroy()
      {
         SkinningBufferManager.OnDestroy();
      }

      protected override void OnUpdate()
      {
         var batches = m_GatherSkinnedMeshInstancesSystem.GetSkinnedMeshInstanceBatches(out int batchesVersion);
         SkinningBufferManager.PushSkinnedMeshInstancesToBuffer(batches, batchesVersion);

         SkinningBufferManager.PushPassDataToShader(m_ComputeSkinMatricesComputeShader, m_ComputeSkinMatricesComputeKernel);
         m_AnimationSystem.SkeletonBufferManager.PushSkeletonMatricesToShader(m_ComputeSkinMatricesComputeShader, m_ComputeSkinMatricesComputeKernel);

         foreach (var batch in batches)
         {
            m_ComputeSkinMatricesComputeShader.SetInt(m_InstanceBufferStartIndexPropertyID, batch.InstanceOffset);
            m_ComputeSkinMatricesComputeShader.SetInt(m_InstanceCountPropertyID, batch.InstanceCount);
            m_ComputeSkinMatricesComputeShader.SetInt(m_BoneCountPropertyID, batch.BoneCount);
            m_ComputeSkinMatricesComputeShader.SetInt(m_SkinBoneDataBufferIndexPropertyID, batch.SkinnedMeshBoneBufferIndex);

            int threadGroupsX = (int)math.ceil((float)batch.InstanceCount / 64);
            m_ComputeSkinMatricesComputeShader.Dispatch(m_ComputeSkinMatricesComputeKernel, threadGroupsX, 1, 1);
         }
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      int m_InstanceBufferStartIndexPropertyID;
      int m_InstanceCountPropertyID;
      int m_BoneCountPropertyID;
      int m_SkinBoneDataBufferIndexPropertyID;
      ComputeShader m_ComputeSkinMatricesComputeShader;
      int m_ComputeSkinMatricesComputeKernel;
      AnimationSystemBase m_AnimationSystem;
      GatherSkinnedMeshInstancesSystemBase m_GatherSkinnedMeshInstancesSystem;
   }
}
