  using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using System.Runtime.CompilerServices;
using Unity.Entities;

namespace CrowdMorph
{
   internal unsafe struct AnimationCommandListData : IDisposable
   {      
      public int PassCount { get; private set; }
      public int InternalPassCapacity { get; private set; }

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      public static AnimationCommandListData* Create(int passCount, int initialPassCapacity, Allocator allocator)
      {
         var data = (AnimationCommandListData*)UnsafeUtility.Malloc(sizeof(AnimationCommandListData), sizeof(void*), allocator);

         data->PassCount = passCount;
         data->InternalPassCapacity = -1;
         data->m_Allocator = allocator;
         data->m_PassLengths = null;
         data->m_PassBuffers = null;

         data->SetInternalPassCapacity(initialPassCapacity);

         return data; 
      }

      public static void Destroy(AnimationCommandListData* ptr, Allocator allocator)
      {
         ptr->Dispose();
         UnsafeUtility.Free(ptr, allocator);
      }

      public void SetInternalPassCapacity(int newPassCapacity)
      {
         Debug.Assert(newPassCapacity > 0);

         if (m_PassBuffers != null && m_PassLengths != null)
         {
            UnsafeUtility.Free(m_PassBuffers, m_Allocator);
            UnsafeUtility.Free(m_PassLengths, m_Allocator);
         }

         if (newPassCapacity != InternalPassCapacity)
         {
            InternalPassCapacity = newPassCapacity;

            m_PassLengths = (int*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<int>() * PassCount, 4, m_Allocator);
            UnsafeUtility.MemClear(m_PassLengths, UnsafeUtility.SizeOf<int>() * PassCount);
            int passesBufferSize = UnsafeUtility.SizeOf<AnimationCommand>() * PassCount * newPassCapacity;
            m_PassBuffers = (AnimationCommand*)UnsafeUtility.Malloc(passesBufferSize, UnsafeUtility.AlignOf<AnimationCommand>(), m_Allocator);
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public int GetPassLength(int passIdx)  
      {
         Debug.Assert(passIdx >= 0 && passIdx < PassCount);
         return m_PassLengths[passIdx];
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public AnimationCommand* GetPassUnsafePtr(int passIdx)
      {
         Debug.Assert(passIdx >= 0 && passIdx < PassCount);
         return &m_PassBuffers[InternalPassCapacity * passIdx];
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public void Dispatch(int passIndex, in AnimationCommand command)
      {
         var queuePtr = GetPassUnsafePtr(passIndex);
         int idx = Interlocked.Increment(ref m_PassLengths[passIndex]) - 1;
         UnsafeUtility.WriteArrayElement(queuePtr, idx, command);
      } 

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public void Clear()
      {
         UnsafeUtility.MemClear(m_PassLengths, PassCount * sizeof(int));
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public NativeArray<AnimationCommand> GetPassAsNativeArray(int PassIndex)
      {
         var listPtr = GetPassUnsafePtr(PassIndex);
         int length = GetPassLength(PassIndex);

         var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<AnimationCommand>(listPtr, length, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
         var safetyHandle = AtomicSafetyHandle.Create();
         AtomicSafetyHandle.SetAllowReadOrWriteAccess(safetyHandle, true);
         NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, safetyHandle);
#endif
         return array;
      }

      public void Dispose()
      {
         if (m_PassBuffers != null)
         {
            UnsafeUtility.Free(m_PassLengths, m_Allocator);
            UnsafeUtility.Free(m_PassBuffers, m_Allocator);
            m_Allocator = Allocator.None;
            InternalPassCapacity = 0;
            PassCount = 0;
         }
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      int* m_PassLengths;
      AnimationCommand* m_PassBuffers;
      Allocator m_Allocator;
   }
      
   internal unsafe struct AnimationCommandList : IDisposable
   {
      AnimationCommandListData* m_Data;
      Allocator m_Allocator;

      public bool IsCreated
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_Data != null;
      }

      public int InternalPassCapacity
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_Data->InternalPassCapacity;
      }

      public int PassCount
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_Data->PassCount;
      }

      public int CommandCapacity
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_Data->InternalPassCapacity * m_Data->PassCount;
      }

      public AnimationCommandList(int passCount, int initialCapacity, Allocator allocator)
      {
         m_Allocator = allocator;
         m_Data = AnimationCommandListData.Create(passCount, initialCapacity, allocator);
         
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public int GetPassLength(int passIndex)
      {
         return m_Data->GetPassLength(passIndex);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public void Dispatch(
         int passIndex, 
         ClipKeyframe keyframe,
         float weight,
         int skeletonMatrixBufferIndex,
         int clipSampleBufferIndex,
         int additiveReferencePoseMatrixBufferIndex,
         int skeletonMaskBufferIndex,
         BlendingMode blendingMode)
      {
         unchecked
         {
            m_Data->Dispatch(passIndex,  new AnimationCommand
            {
               LeftKeyframe = (ushort)keyframe.Left,
               RightKeyframe = (ushort)keyframe.Right,
               KeyframeWeight = keyframe.Weight,
               Weight = passIndex == 0 ? 1f : weight, 
               BlendingMode = (int)blendingMode,
               ClipSampleBufferIndex = clipSampleBufferIndex,
               SkeletonMatrixBufferIndex = skeletonMatrixBufferIndex,
               AdditiveReferencePoseMatrixBufferIndex = additiveReferencePoseMatrixBufferIndex,
               SkeletonMaskBufferIndex = skeletonMaskBufferIndex
            });
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public void SetPassInternalCapacity(int newCapacity)
      {
         m_Data->SetInternalPassCapacity(newCapacity);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public NativeArray<AnimationCommand> GetPassAsNativeArray(int passIndex)
      {
         return m_Data->GetPassAsNativeArray(passIndex);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public void Clear()
      {
         m_Data->Clear();
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public void Dispose()
      {
         if (IsCreated)
         {
            m_Data->Dispose();
            UnsafeUtility.Free(m_Data, m_Allocator);
            m_Data = null;
         }
      }
   }

   public struct AnimationCommandBatch
   {
      public int ComputeBufferStartIndex;
      public int CommandCount;
      public BlobAssetReference<SkeletonDefinition> Skeleton;
   }

   [StructLayout(LayoutKind.Sequential)]
   struct AnimationCommand
   {
      public ushort LeftKeyframe;
      public ushort RightKeyframe;
      public float KeyframeWeight;
      public float Weight;
      public int BlendingMode;
      public int ClipSampleBufferIndex;
      public int SkeletonMatrixBufferIndex;
      public int AdditiveReferencePoseMatrixBufferIndex;
      public int SkeletonMaskBufferIndex;
   }
}