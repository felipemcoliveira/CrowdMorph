using Unity.Entities;
using UnityEngine;
using Unity.Collections;

namespace CrowdMorph
{
   internal class AnimationCommandBufferManager
   {
      internal NativeHashMap<int, AnimationCommandList> SkeletonHashToAnimationCommandList
      {
         get => m_SkeletonHashToAnimationCommandList;
      }

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      internal void OnCreate()
      {
         m_RequiredAnimationCommandBufferSize = 0;
         m_AnimationCommandsBuffer = new ComputeBufferWrapper<AnimationCommand>("_AnimationCommands", k_ChunkSize);
         m_SkeletonHashToAnimationCommandList = new NativeHashMap<int, AnimationCommandList>(8, Allocator.Persistent);
      }

      internal void OnDestroy() 
      {
         m_AnimationCommandsBuffer.Dispose();
         m_SkeletonHashToAnimationCommandList.Dispose();
      }

      internal void PushAnimationCommandsToBuffer(NativeList<BlobAssetReference<SkeletonDefinition>> skeletons, NativeList<AnimationCommandBatch> outBatches)
      {
         outBatches.Clear();
         ResizeAnimationCommandBufferIfRequired(m_RequiredAnimationCommandBufferSize);
         
         int computeBufferOffset = 0;
         for (int i = 0; i < skeletons.Length; i++)
         {
            var skeleton = skeletons[i];

            if (skeleton == BlobAssetReference<SkeletonDefinition>.Null)
               continue;

            var commandList = m_SkeletonHashToAnimationCommandList[skeleton.Value.GetHashCode()];

            for (int j = 0; j < commandList.PassCount; j++)
            {
               if (commandList.GetPassLength(j) == 0)
                  continue;

               var passCommandsArray = commandList.GetPassAsNativeArray(j);
               m_AnimationCommandsBuffer.SetData(passCommandsArray, 0, computeBufferOffset, passCommandsArray.Length);

               outBatches.Add(new AnimationCommandBatch
               {
                  Skeleton = skeleton,
                  ComputeBufferStartIndex = computeBufferOffset,
                  CommandCount = passCommandsArray.Length
               });

               computeBufferOffset += passCommandsArray.Length;
            }

            commandList.Clear();
         }
      }

      internal void ResizeAnimationCommandListIfRequired(int skeletonHashCode, int requiredSize)
      {
         if (m_SkeletonHashToAnimationCommandList.TryGetValue(skeletonHashCode, out var commandList))
         {
            var internalBufferSize = commandList.InternalPassCapacity;
            m_RequiredAnimationCommandBufferSize -= commandList.CommandCapacity;

            if (internalBufferSize <= requiredSize || internalBufferSize - requiredSize > k_PassInternalChunkSize)
            {
               var newInternalBufferSize = ((requiredSize / k_PassInternalChunkSize) + 1) * k_PassInternalChunkSize;
               commandList.SetPassInternalCapacity(newInternalBufferSize);
            }
         }
         else
         {
            commandList = new AnimationCommandList(k_PassCount, k_PassInternalChunkSize, Allocator.Persistent);
            m_SkeletonHashToAnimationCommandList.Add(skeletonHashCode, commandList);
         }

         m_RequiredAnimationCommandBufferSize += commandList.CommandCapacity;
      }

      internal void PushAnimationCommandsBufferToShader(ComputeShader shader, int kernelIndex)
      {
         m_AnimationCommandsBuffer.PushDataToShader(shader, kernelIndex);
      }

      private bool ResizeAnimationCommandBufferIfRequired(int requiredSize)
      {
         var bufferSize = m_AnimationCommandsBuffer.BufferSize;
         if (bufferSize <= requiredSize || bufferSize - requiredSize > k_ChunkSize)
         {
            var newBufferSize = ((requiredSize / k_ChunkSize) + 1) * k_ChunkSize;
            m_AnimationCommandsBuffer.Resize(newBufferSize, false);
            return true;
         }
         return false;
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      const int k_ChunkSize = 2048;
      const int k_PassCount = 12;
      const int k_PassInternalChunkSize = 64;

      ComputeBufferWrapper<AnimationCommand> m_AnimationCommandsBuffer;
      NativeHashMap<int, AnimationCommandList> m_SkeletonHashToAnimationCommandList;
      int m_RequiredAnimationCommandBufferSize;
   }
}
