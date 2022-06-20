using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


namespace CrowdMorph
{
   [BurstCompatible]
   public unsafe struct ClipInstance
   {
      public Clip Clip;
      public int SkeletonHashCode;
      public int ClipHashCode;
      public BlobArray<int> BindingsMap;
   }

   public struct ClipEvent
   {
      public StringHash FunctionNameHash;
      public float Time;
      public int IntParameter;
      public float FloatParameter;
   }

   [BurstCompatible]
   public unsafe struct Clip
   {
      public float FrameRate;
      public float Length;
      public WrapMode WrapMode;
      public BlobArray<StringHash> TranslationsBindings;
      public BlobArray<float3> LocalTranslations;
      public BlobArray<StringHash> RotationBindings;
      public BlobArray<quaternion> LocalRotations;
      public BlobArray<StringHash> ScalesBindings;
      public BlobArray<float3> LocalScales;
      public BlobArray<ClipEvent> Events;

      internal int HashCode;

      public int FrameCount
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => Core.GetFrameCount(FrameRate, Length);
      }

      public int SampleCount
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => Core.GetSampleCount(FrameRate, Length);
      }

      public float LastFrameError
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => Core.GetLastFrameError(FrameRate, Length);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public override int GetHashCode() => HashCode;
   }
}