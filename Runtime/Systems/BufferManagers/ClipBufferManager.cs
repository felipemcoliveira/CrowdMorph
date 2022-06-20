using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using Unity.Rendering;

namespace CrowdMorph
{
   public class ClipBufferManager
   {
      public NativeHashMap<int, int> ClipInstanceHashToSampleBufferIndex
      {
         get => m_ClipInstanceHashToSampleBufferIndex;
      }

      public NativeHashMap<int, BlobAssetReference<Clip>> ClipInstanceHashToClip
      {
         get => m_ClipInstanceHashToClip;
      }

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      internal void OnCreate()
      {
         m_ClipSamplesBuffer = new ComputeBufferWrapper<AffineTransform>("_ClipSamples", k_ClipSamplesBufferChunkSize);
         m_ClipSamplesHeapAllocator = new HeapAllocator(256 * 1024 * 1024, 4);

         m_ClipInstanceHashToRefCount = new NativeHashMap<int, int>(16, Allocator.Persistent);
         m_ClipInstanceHashToSampleBufferIndex = new NativeHashMap<int, int>(16, Allocator.Persistent);
         m_ClipInstanceHashToSampleBufferEnd = new NativeHashMap<int, int>(16, Allocator.Persistent);
         m_ClipInstanceHashToClip = new NativeHashMap<int, BlobAssetReference<Clip>>(16, Allocator.Persistent);
      }

      internal void OnDestroy()
      {
         m_ClipSamplesBuffer.Dispose();
         m_ClipSamplesHeapAllocator.Dispose();
         m_ClipInstanceHashToRefCount.Dispose();
         m_ClipInstanceHashToSampleBufferIndex.Dispose();
         m_ClipInstanceHashToSampleBufferEnd.Dispose();
         m_ClipInstanceHashToClip.Dispose();
      }

      public bool RetainClipInstance(BlobAssetReference<Clip> clip, BlobAssetReference<SkeletonDefinition> skeleton)
      {
         int clipInstanceHashCode = HashUtility.GetClipInstanceHash(clip, skeleton);

         if (m_ClipInstanceHashToRefCount.TryGetValue(clipInstanceHashCode, out int instanceCount))
         {
            m_ClipInstanceHashToRefCount[clipInstanceHashCode] = instanceCount + 1;
            return false;
         }

         m_ClipInstanceHashToClip[clipInstanceHashCode] = clip;

         var samples = Core.SampleClipInstanceMatrices(skeleton, clip);
         var allocatedBlock = m_ClipSamplesHeapAllocator.Allocate((ulong)samples.Length);
         
         int requiredSize = (int)m_ClipSamplesHeapAllocator.OnePastHighestUsedAddress;
         ResizeClipSamplesBufferIfRequired(requiredSize);

         m_ClipSamplesBuffer.SetData(samples, 0, (int)allocatedBlock.begin, samples.Length);
         samples.Dispose();

         m_ClipInstanceHashToRefCount[clipInstanceHashCode] = 1;
         m_ClipInstanceHashToSampleBufferIndex[clipInstanceHashCode] = (int)allocatedBlock.begin;
         m_ClipInstanceHashToSampleBufferEnd[clipInstanceHashCode] = (int)allocatedBlock.end;

         return true;
      }

      public bool ReleaseClipInstance(int clipHashCode, int skeletonHashCode)
      {
         int clipInstanceHashCode = HashUtility.GetClipInstanceHash(clipHashCode, skeletonHashCode);
         int instanceCount = m_ClipInstanceHashToRefCount[clipInstanceHashCode] - 1;

         if (instanceCount > 0)
         {
            m_ClipInstanceHashToRefCount[clipInstanceHashCode] = instanceCount;
            return false;
         }

         m_ClipSamplesHeapAllocator.Release(new HeapBlock
         {
            begin = (ulong)m_ClipInstanceHashToSampleBufferIndex[clipInstanceHashCode],
            end = (ulong)m_ClipInstanceHashToSampleBufferEnd[clipInstanceHashCode]
         });

         m_ClipInstanceHashToClip.Remove(clipInstanceHashCode);
         m_ClipInstanceHashToRefCount.Remove(clipInstanceHashCode);
         m_ClipInstanceHashToSampleBufferIndex.Remove(clipInstanceHashCode);
         m_ClipInstanceHashToSampleBufferEnd.Remove(clipInstanceHashCode);

         int requiredSize = (int)m_ClipSamplesHeapAllocator.OnePastHighestUsedAddress;
         ResizeClipSamplesBufferIfRequired(requiredSize);

         return true;
      }

      internal void PushAnimationMatricesBufferToShader(ComputeShader shader, int kernelIndex)
      {
         m_ClipSamplesBuffer.PushDataToShader(shader, kernelIndex);
      }

      private bool ResizeClipSamplesBufferIfRequired(int requiredSize)
      {
         var bufferSize = m_ClipSamplesBuffer.BufferSize;
         if (bufferSize <= requiredSize || bufferSize - requiredSize > k_ClipSamplesBufferChunkSize)
         {
            var newBufferSize = ((requiredSize / k_ClipSamplesBufferChunkSize) + 1) * k_ClipSamplesBufferChunkSize;
            m_ClipSamplesBuffer.Resize(newBufferSize, true);
            return true;
         }
         return false;
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      const int k_ClipSamplesBufferChunkSize = 16 * 1024;

      private ComputeBufferWrapper<AffineTransform> m_ClipSamplesBuffer;
      private HeapAllocator m_ClipSamplesHeapAllocator;
      private NativeHashMap<int, int> m_ClipInstanceHashToRefCount;
      private NativeHashMap<int, int> m_ClipInstanceHashToSampleBufferIndex;
      private NativeHashMap<int, int> m_ClipInstanceHashToSampleBufferEnd;

      private NativeHashMap<int, BlobAssetReference<Clip>> m_ClipInstanceHashToClip;
   }
}