using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using System.Collections.Generic;
namespace CrowdMorph
{
   public class SkeletonBufferManager
   {
      public NativeHashMap<int, int> SkeletonHashToBoneParentBufferIndex => m_SkeletonHashToBoneParentBufferIndex;
      public NativeHashMap<int, int> SkeletonMaskInstanceHashToBufferIndex => m_SkeletonMaskInstanceHashToBufferIndex;

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      internal void OnCreate()
      {
         m_SkeletonInstancesBuffer = new ComputeBufferWrapper<int>("_SkeletonInstances", k_InstanceChunkSize);
         m_SkeletonMatricesBuffer = new ComputeBufferWrapper<float4x3>("_SkeletonMatrices", k_MatricesChunkSize);
         m_SkeletonMaskBuffer = new ComputeBufferWrapper<ulong>("_SkeletonMasks", k_MaskChunkSize);
         m_SkeletonBoneParentsBuffer = new ComputeBufferWrapper<int>("_SkeletonBoneParents", k_BoneParentsChunkSize);
         m_SkeletonHashToBoneParentBufferIndex = new NativeHashMap<int, int>(8, Allocator.Persistent);
         m_SkeletonMaskInstanceHashToBufferIndex = new NativeHashMap<int, int>(8, Allocator.Persistent);

         PushAllBonesActivedMask();
      }

      private void PushAllBonesActivedMask()
      {
         var data = new NativeArray<ulong>(1, Allocator.Persistent);
         data[0] = ~0UL;
         m_SkeletonMaskBuffer.SetData(data, 0, m_SkeletonMaskBufferOffset, 1);
         data.Dispose();
         m_SkeletonMaskInstanceHashToBufferIndex.Add(0, 0);
         m_SkeletonMaskBufferOffset++;
      }

      internal void OnDestroy()
      {
         m_SkeletonInstancesBuffer.Dispose();
         m_SkeletonMatricesBuffer.Dispose();
         m_SkeletonBoneParentsBuffer.Dispose();
         m_SkeletonMaskBuffer.Dispose();
         m_SkeletonHashToBoneParentBufferIndex.Dispose();
         m_SkeletonMaskInstanceHashToBufferIndex.Dispose();
      }

      public void PushSkeletonMaskToBuffer(BlobAssetReference<SkeletonDefinition> skeleton, BlobAssetReference<SkeletonMaskDefinition> mask)
      {
         int skeletonMaskInstanceHashCode = HashUtility.GetSkeletonMaskInstanceHash(mask, skeleton);
         if (m_SkeletonMaskInstanceHashToBufferIndex.ContainsKey(skeletonMaskInstanceHashCode))
            return;

         var data = new NativeArray<ulong>(1, Allocator.Persistent);
         data[0] = Core.ComputeSkeletonMask(mask, skeleton);
         ResizeSkeletonMaskBufferIfRequired(m_SkeletonMaskBufferOffset + 1);
         m_SkeletonMaskBuffer.SetData(data, 0, m_SkeletonMaskBufferOffset, 1);
         data.Dispose();

         m_SkeletonMaskInstanceHashToBufferIndex.Add(skeletonMaskInstanceHashCode, m_SkeletonMaskBufferOffset);
         m_SkeletonMaskBufferOffset++;
      }

      public void PushSharedSkeletonData(BlobAssetReference<SkeletonDefinition> skeleton)
      {
         if (m_SkeletonHashToBoneParentBufferIndex.ContainsKey(skeleton.Value.GetHashCode()))
            return;
         
         ResizeSkeletonBoneParentsBufferIfRequired(m_SkeletonBoneParentBufferOffset + skeleton.Value.BoneCount);

         var boneParents = new NativeArray<int>(skeleton.Value.BoneParentIndices.ToArray(), Allocator.Temp);

         m_SkeletonBoneParentsBuffer.SetData(boneParents, 0, m_SkeletonBoneParentBufferOffset, boneParents.Length);
         m_SkeletonHashToBoneParentBufferIndex.Add(skeleton.Value.GetHashCode(), m_SkeletonBoneParentBufferOffset);
         m_SkeletonBoneParentBufferOffset += skeleton.Value.BoneCount;

         boneParents.Dispose();
      }

      internal void PushSkeletonMatricesToShader(ComputeShader shader, int kernelIndex)
      {
         m_SkeletonMatricesBuffer.PushDataToShader(shader, kernelIndex);
      }

      internal void PushSkeletonMasksToShader(ComputeShader shader, int kernelIndex)
      {
         m_SkeletonMaskBuffer.PushDataToShader(shader, kernelIndex);
      }

      internal void PushPassDataToShader(ComputeShader shader, int kernelIndex)
      {
         m_SkeletonInstancesBuffer.PushDataToShader(shader, kernelIndex);
         m_SkeletonMatricesBuffer.PushDataToShader(shader, kernelIndex);
         m_SkeletonBoneParentsBuffer.PushDataToShader(shader, kernelIndex);
         m_SkeletonMaskBuffer.PushDataToShader(shader, kernelIndex);
      }

      internal void PushSkinnedMeshInstancesToBuffer(List<SkeletonInstanceBatch> batches, int version)
      {
         if (version == m_CurrentBatchVersionInComputeBuffer)
            return;

         int totalInstanceCount = 0;
         foreach (var batch in batches)
            totalInstanceCount += batch.InstanceCount;

         ResizeSkeletonInstancesBufferIfRequired(totalInstanceCount);

         foreach (var batch in batches)
         {
            batch.InstancesGathererHandle.Complete();
            m_SkeletonInstancesBuffer.SetData(batch.Instances, 0, batch.InstanceBufferStartIndex, batch.InstanceCount);
         }
         m_CurrentBatchVersionInComputeBuffer = version;
      }

      internal bool ResizeSkeletonMaskBufferIfRequired(int requiredSize)
      {
         var bufferSize = m_SkeletonMaskBuffer.BufferSize;
         if (bufferSize <= requiredSize || bufferSize - requiredSize > k_MaskChunkSize)
         {
            var newBufferSize = ((requiredSize / k_MaskChunkSize) + 1) * k_MaskChunkSize;
            m_SkeletonMaskBuffer.Resize(newBufferSize, true);
            return true;
         }
         return false;
      }

      internal bool ResizeSkeletonInstancesBufferIfRequired(int requiredSize)
      {
         var bufferSize = m_SkeletonInstancesBuffer.BufferSize;
         if (bufferSize <= requiredSize || bufferSize - requiredSize > k_InstanceChunkSize)
         {
            var newBufferSize = ((requiredSize / k_InstanceChunkSize) + 1) * k_InstanceChunkSize;
            m_SkeletonInstancesBuffer.Resize(newBufferSize, false);
            return true;
         }
         return false;
      }

      internal bool ResizeSkeletonBoneParentsBufferIfRequired(int requiredSize)
      {
         var bufferSize = m_SkeletonBoneParentsBuffer.BufferSize;
         if (bufferSize <= requiredSize || bufferSize - requiredSize > k_BoneParentsChunkSize)
         {
            var newBufferSize = ((requiredSize / k_BoneParentsChunkSize) + 1) * k_BoneParentsChunkSize;
            m_SkeletonBoneParentsBuffer.Resize(newBufferSize, true);
            return true;
         }
         return false;
      }

      internal bool ResizeSkeletonMatricesBufferIfRequired(int requiredSize)
      {
         var bufferSize = m_SkeletonMatricesBuffer.BufferSize;
         if (bufferSize <= requiredSize || bufferSize - requiredSize > k_MatricesChunkSize)
         {
            var newBufferSize = ((requiredSize / k_MatricesChunkSize) + 1) * k_MatricesChunkSize;
            m_SkeletonMatricesBuffer.Resize(newBufferSize, false);
            return true;
         }
         return false;
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      const int k_MatricesChunkSize = 2048;
      const int k_InstanceChunkSize = 256;
      const int k_BoneParentsChunkSize = 64;
      const int k_MaskChunkSize = 16;

      public ComputeBufferWrapper<float4x3> m_SkeletonMatricesBuffer;
      ComputeBufferWrapper<int> m_SkeletonInstancesBuffer;
      ComputeBufferWrapper<int> m_SkeletonBoneParentsBuffer;
      ComputeBufferWrapper<ulong> m_SkeletonMaskBuffer;
      int m_SkeletonBoneParentBufferOffset;
      int m_SkeletonMaskBufferOffset;
      NativeHashMap<int, int> m_SkeletonHashToBoneParentBufferIndex;
      NativeHashMap<int, int> m_SkeletonMaskInstanceHashToBufferIndex;
      int m_CurrentBatchVersionInComputeBuffer;

   }
}