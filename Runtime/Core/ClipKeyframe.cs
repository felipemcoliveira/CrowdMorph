using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace CrowdMorph
{
   public struct ClipKeyframe
   {
      public int Left;
      public int Right;
      public float Weight;

      // ----------------------------------------------------------------------------------------
      // Methods
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static ClipKeyframe Create(float time, ref Clip clip)
      {
         return Create(time, clip.WrapMode, clip.Length, clip.FrameRate, clip.SampleCount);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static ClipKeyframe Create(float time, WrapMode wrapMode, float duration, float frameRate, int sampleCount)
      {
         float frameIndex = Wrap(time, duration, wrapMode) * frameRate;
         int left = (int)math.floor(frameIndex);

         var keyframe = new ClipKeyframe
         {
            Left = left,
            Weight = math.frac(frameIndex)
         };


         if (wrapMode == WrapMode.Loop)
            keyframe.Right = (left + 1) % sampleCount;
         else
            keyframe.Right = math.min(left + 1, sampleCount - 1);

         return keyframe;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      private static float Wrap(float time, float duration, WrapMode wrapMode)
      {
         if (wrapMode == WrapMode.Loop)
         {
            time = math.fmod(time, duration);
            return time < 0 ? duration + time : time;
         }
         return math.clamp(time, 0, duration);
      }
   }
}  