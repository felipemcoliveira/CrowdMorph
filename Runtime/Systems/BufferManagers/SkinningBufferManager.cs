using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdMorph
{
   public class SkinningBufferManager
   {
      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      internal void OnCreate()
      {
         m_SkinnedMeshBonesBufferOffset = 0;

         m_SkinMatricesBuffer = new ComputeBufferWrapper<float4x3>("_CrowdMorphSkinMatrices", k_SkinMatricesChunkSize);
         m_SkinnedMeshInstancesBuffer = new ComputeBufferWrapper<SkinnedMeshInstance>("_SkinnedMeshInstances", k_SkinnedMeshInstancesChunkSize);
         m_SkinnedMeshBonesBuffer = new ComputeBufferWrapper<SkinnedMeshBoneData>("_SkinnedMeshBones", k_SkinnedMeshBonesBufferChunkSize);
      }

      internal void OnDestroy()
      {
         m_SkinMatricesBuffer.Dispose();
         m_SkinnedMeshInstancesBuffer.Dispose();
         m_SkinnedMeshBonesBuffer.Dispose();
      }

      internal void PushPassDataToShader(ComputeShader shader, int kernelIndex)
      {
         m_SkinnedMeshInstancesBuffer.PushDataToShader(shader, kernelIndex);
         m_SkinnedMeshBonesBuffer.PushDataToShader(shader, kernelIndex);
         m_SkinMatricesBuffer.PushDataToGlobal();
      }

      internal void PushSkinnedMeshInstancesToBuffer(List<SkinnedMeshInstanceBatch> batches, int version)
      {
         if (version == m_CurrentBatchVersionInComputeBuffer)
            return;

         int totalInstanceCount = 0;
         foreach (var batch in batches)
            totalInstanceCount += batch.InstanceCount;

         ResizeSkinnedMeshInstancesBufferIfRequired(totalInstanceCount);

         foreach (var batch in batches)
         {
            batch.InstancesGathererHandle.Complete();
            m_SkinnedMeshInstancesBuffer.SetData(batch.Instances, 0, batch.InstanceOffset, batch.InstanceCount);
         }
         m_CurrentBatchVersionInComputeBuffer = version;
      }

      public int PushSkinnedMeshBonesToBuffer(BlobAssetReference<SkinnedMeshDefinition> skinnedMesh)
      {
         var skinnedMeshBones = new NativeArray<SkinnedMeshBoneData>(skinnedMesh.Value.BoneCount, Allocator.Persistent);

         for (int i = 0; i < skinnedMesh.Value.BoneCount; i++)
         {
            skinnedMeshBones[i] = new SkinnedMeshBoneData
            {
               BindPose = skinnedMesh.Value.BindPoses[i],
               SkeletonBoneIndex = skinnedMesh.Value.SkinToSkeletonBoneIndices[i]
            };
         }

         ResizeSkinnedMeshBonesBufferrIfRequired(m_SkinnedMeshBonesBufferOffset + skinnedMesh.Value.BoneCount);

         int bufferIndex = m_SkinnedMeshBonesBufferOffset;
         m_SkinnedMeshBonesBuffer.SetData(skinnedMeshBones, 0, m_SkinnedMeshBonesBufferOffset, skinnedMesh.Value.BoneCount);
         m_SkinnedMeshBonesBufferOffset += skinnedMesh.Value.BoneCount;

         skinnedMeshBones.Dispose();
         return bufferIndex;
      }

      internal bool ResizeSkinMatricesBufferIfRequired(int requiredSize)
      {
         var bufferSize = m_SkinMatricesBuffer.BufferSize;
         if (bufferSize <= requiredSize || bufferSize - requiredSize > k_SkinMatricesChunkSize)
         {
            var newBufferSize = ((requiredSize / k_SkinMatricesChunkSize) + 1) * k_SkinMatricesChunkSize;
            m_SkinMatricesBuffer.Resize(newBufferSize, false);
            return true;
         }
         return false;
      }

      internal bool ResizeSkinnedMeshBonesBufferrIfRequired(int requiredSize)
      {
         var bufferSize = m_SkinnedMeshBonesBuffer.BufferSize;
         if (bufferSize <= requiredSize || bufferSize - requiredSize > k_SkinnedMeshBonesBufferChunkSize)
         {
            var newBufferSize = ((requiredSize / k_SkinnedMeshBonesBufferChunkSize) + 1) * k_SkinnedMeshBonesBufferChunkSize;
            m_SkinnedMeshBonesBuffer.Resize(newBufferSize, true);
            return true;
         }
         return false;
      }

      private bool ResizeSkinnedMeshInstancesBufferIfRequired(int requiredSize)
      {
         var bufferSize = m_SkinnedMeshInstancesBuffer.BufferSize;
         if (bufferSize <= requiredSize || bufferSize - requiredSize > k_SkinnedMeshInstancesChunkSize)
         {
            var newBufferSize = ((requiredSize / k_SkinnedMeshInstancesChunkSize) + 1) * k_SkinnedMeshInstancesChunkSize;
            m_SkinnedMeshInstancesBuffer.Resize(newBufferSize, false);
            return true;
         }
         return false;
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      const int k_SkinnedMeshBonesBufferChunkSize = 64;
      const int k_SkinMatricesChunkSize = 8 * 1024;
      const int k_SkinnedMeshInstancesChunkSize = 2048;

      ComputeBufferWrapper<float4x3> m_SkinMatricesBuffer;
      ComputeBufferWrapper<SkinnedMeshInstance> m_SkinnedMeshInstancesBuffer;
      ComputeBufferWrapper<SkinnedMeshBoneData> m_SkinnedMeshBonesBuffer;
      int m_CurrentBatchVersionInComputeBuffer;
      int m_SkinnedMeshBonesBufferOffset;
   }
}