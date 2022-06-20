using System.Collections.Generic;
using UnityEngine;

namespace CrowdMorph
{
   internal class ComputeBufferUtility
   {
      // ----------------------------------------------------------------------------------------
      // Constructor
      // ----------------------------------------------------------------------------------------

      static ComputeBufferUtility()
      {
         s_CopyBufferShader = Resources.Load<ComputeShader>("CrowdMorph/CopyBufferComputeShader");
         s_CopyBufferKernel = s_CopyBufferShader.FindKernel("CopyBufferComputeKernel");
         s_SupportedDataSizes = new HashSet<int>(new int[] { 4, 8, 16, 32, 48, 64 });
      }

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      public unsafe static void Copy<T>(ComputeBuffer src, ComputeBuffer dst, int size, int srcOffset = 0, int dstOffset = 0) where T : unmanaged
      {
         int dataSize = sizeof(T);

         Debug.Assert(s_SupportedDataSizes.Contains(dataSize));
         Debug.Assert(src.stride == dataSize && dst.stride == dataSize);

         s_CopyBufferShader.EnableKeyword($"_{dataSize}");
         int threadGroups = Mathf.CeilToInt((float)size / 128);

         s_CopyBufferShader.SetBuffer(s_CopyBufferKernel, k_SrcID, src);
         s_CopyBufferShader.SetInt(k_SrcOffsetID, srcOffset);
         s_CopyBufferShader.SetBuffer(s_CopyBufferKernel, k_DstID, dst);
         s_CopyBufferShader.SetInt(k_DstOffsetID, dstOffset);

         s_CopyBufferShader.Dispatch(s_CopyBufferKernel, threadGroups, 1, 1);
         s_CopyBufferShader.DisableKeyword($"_{dataSize}");

      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      static readonly int k_DstID = Shader.PropertyToID("Dst");
      static readonly int k_SrcID = Shader.PropertyToID("Src");
      static readonly int k_DstOffsetID = Shader.PropertyToID("g_DstOffset");
      static readonly int k_SrcOffsetID = Shader.PropertyToID("g_SrcOffset");

      static HashSet<int> s_SupportedDataSizes;
      static ComputeShader s_CopyBufferShader;
      static int s_CopyBufferKernel;

   }
}

