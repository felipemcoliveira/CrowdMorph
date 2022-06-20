
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdMorph.Tests
{
   public class ComputeBufferTests
   {
      [Test]
      public unsafe void ShouldCopyUInt4Buffer()
      {
         var BufferA = new ComputeBuffer(4, sizeof(uint4), ComputeBufferType.Structured);
         var BufferB = new ComputeBuffer(5, sizeof(uint4), ComputeBufferType.Structured);

         var SequencialArray = new uint4[5];
         for (uint Index = 0; Index < SequencialArray.Length; Index++)
         {
            SequencialArray[Index] = new uint4(~Index, 0, 0, 0);
         }

         BufferA.SetData(SequencialArray, 0, 0, 4);
         ComputeBufferUtility.Copy<int4>(BufferA, BufferB, math.min(BufferA.count, BufferB.count));


         var BufferBData = new uint4[5];
         BufferB.GetData(BufferBData);

         for (int Index = 0; Index < BufferA.count; Index++)
         {
            Assert.IsTrue(math.all(BufferBData[Index] == SequencialArray[Index]));
         }

         BufferA.Dispose();
         BufferB.Dispose();
      }
   }
}
