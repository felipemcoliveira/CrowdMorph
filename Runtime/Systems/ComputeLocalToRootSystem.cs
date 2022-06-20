using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

namespace CrowdMorph
{
   public abstract unsafe class ComputeLocalToRootSystemBase : SystemBase
   {
      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------
      protected override void OnCreate()
      {
         m_ComputeShader = Resources.Load<ComputeShader>("CrowdMorph/ComputeLocalToRootComputeShader");
         m_KernelIndex = m_ComputeShader.FindKernel("ComputeLocalToRootComputeKernel");

         m_AnimationSystem = World.GetOrCreateSystem<AnimationSystemBase>();
         m_GatherSkeletonInstancesSystem = World.GetOrCreateSystem<GatherSkeletonInstancesSystemBase>();

         m_InstanceBufferStartIndexPropertyID = Shader.PropertyToID("g_InstanceBufferStartIndex");
         m_InstanceCountPropertyID = Shader.PropertyToID("g_InstanceCount");
         m_BoneCountPropertyID = Shader.PropertyToID("g_BoneCount");
         m_SkeletonBoneParentBufferIndexPropertyID = Shader.PropertyToID("g_SkeletonBoneParentBufferIndex");
      }

      protected override void OnUpdate()
      {
         var batches = m_GatherSkeletonInstancesSystem.GetSkeletonInstanceBatches(out int version);
         m_AnimationSystem.SkeletonBufferManager.PushSkinnedMeshInstancesToBuffer(batches, version);

         m_AnimationSystem.SkeletonBufferManager.PushPassDataToShader(m_ComputeShader, m_KernelIndex);

         foreach (var batch in batches)
         {
            int boneParentBufferIndex = m_AnimationSystem.SkeletonBufferManager.SkeletonHashToBoneParentBufferIndex[batch.SkeletonHashCode];

            m_ComputeShader.SetInt(m_BoneCountPropertyID, batch.BoneCount);
            m_ComputeShader.SetInt(m_InstanceBufferStartIndexPropertyID, batch.InstanceBufferStartIndex);
            m_ComputeShader.SetInt(m_InstanceCountPropertyID, batch.InstanceCount);
            m_ComputeShader.SetInt(m_SkeletonBoneParentBufferIndexPropertyID, boneParentBufferIndex);

            int threadGroupsX = (int)math.ceil((float)batch.InstanceCount / 64);
            m_ComputeShader.Dispatch(m_KernelIndex, threadGroupsX, 1, 1);
         }
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      int m_InstanceBufferStartIndexPropertyID;
      int m_InstanceCountPropertyID;
      int m_BoneCountPropertyID;
      int m_SkeletonBoneParentBufferIndexPropertyID;
      ComputeShader m_ComputeShader;
      int m_KernelIndex;
      GatherSkeletonInstancesSystemBase m_GatherSkeletonInstancesSystem;
      AnimationSystemBase m_AnimationSystem;

   }
}

