using System;
using System.ComponentModel;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdMorph
{
   public class ComputeBufferWrapper<DataType> : IDisposable where DataType : unmanaged
   {
      public int BufferSize { get; private set; }

      public ComputeBuffer UnderlyingBuffer => m_Buffer;

      // ----------------------------------------------------------------------------------------
      // Constructor
      // ----------------------------------------------------------------------------------------

      public ComputeBufferWrapper(string name, int initialSize)
      {
         BufferSize = initialSize;
         m_PropertyID = Shader.PropertyToID(name);
         m_Buffer = new ComputeBuffer(initialSize, UnsafeUtility.SizeOf<DataType>(), ComputeBufferType.Default);
      }

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      public void Resize(int newSize, bool copyFromOldBuffer)
      {
         if (BufferSize == newSize)
            return;

         BufferSize = newSize;
         var previousBuffer = m_Buffer;

         m_Buffer = new ComputeBuffer(newSize, UnsafeUtility.SizeOf<DataType>(), ComputeBufferType.Default);

         if (copyFromOldBuffer)
            ComputeBufferUtility.Copy<DataType>(previousBuffer, m_Buffer, math.min(previousBuffer.count, m_Buffer.count));

         previousBuffer.Dispose();
      }

      public unsafe void SetData(NativeArray<DataType> data, int nativeBufferStartIndex, int computeBufferStartIndex, int count)
      {
         Core.ValidateGreater(m_Buffer.count, 0);
         Core.ValidateArgumentIsCreated(data);
         Core.ValidateLessOrEqual(nativeBufferStartIndex, computeBufferStartIndex);
         Core.ValidateLessOrEqual(nativeBufferStartIndex + count, BufferSize);
         m_Buffer.SetData(data, nativeBufferStartIndex, computeBufferStartIndex, count);
      }

      public void PushDataToGlobal()
      {
         Core.ValidateGreater(m_Buffer.count, 0);
         Shader.SetGlobalBuffer(m_PropertyID, m_Buffer);
      }

      public void PushDataToShader(ComputeShader shader, int kernelIndex)
      {
         Core.ValidateArgumentIsNotNull(shader);
         Core.ValidateGreater(m_Buffer.count, 0);
         shader.SetBuffer(kernelIndex, m_PropertyID, m_Buffer);
      }

      public void Dispose()
      {
         BufferSize = -1;
         m_PropertyID = -1;
         m_Buffer.Dispose();
      }

      // ----------------------------------------------------------------------------------------
      // Debugging
      // ----------------------------------------------------------------------------------------

      [EditorBrowsable(EditorBrowsableState.Never)]
      internal DataType[] DebugData
      {
         get
         {
            var data = new DataType[m_Buffer.count];
            m_Buffer.GetData(data);
            return data;
         }
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      ComputeBuffer m_Buffer;
      int m_PropertyID;
   }
}

