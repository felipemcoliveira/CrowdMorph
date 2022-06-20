
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdMorph
{ 
   [BurstCompatible]
   public struct AnimatorControllerDefinition
   {
      public BlobArray<Layer> Layers;
      public BlobArray<Parameter> Parameters;
      public BlobArray<int> TriggerParameters;
      public BlobArray<Motion> Motions;

      public ulong ParametersStableTypeHash;
      public int ParametersComponentTypeSize;
      
      internal int HashCode;

      public int ParameterCount
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => Parameters.Length;
      }

      public int LayerCount
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => Layers.Length;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public override int GetHashCode() => HashCode;
   }

   [BurstCompatible]
   public struct Layer
   {
      public StringHash NameHash;
      public BlendingMode BlendingMode;
      public float DefaultWeight;
      public int SkeletonMaskHashCode;
      public StateMachine StateMachine;
   }

   [BurstCompatible]
   public unsafe partial struct StateMachine
   {
      public BlobArray<State> States;
      public BlobArray<Transition> GlobalTransitions;
      public int InitialStateIndex;
   }

   [StructLayout(LayoutKind.Sequential)]
   public unsafe struct State
   {
      public StringHash NameHash;
      public float Speed;
      public int SpeedMultiplierParameter;
      public BlobArray<Transition> Transitions;
      public int AdditivePoseReferenceFrameIndex;
      public int MotionIndex;
   }

   public enum ParameterFlags
   {
      None = 0,
      AutoRelease = (1 << 0)
   }

   public enum ParameterType
   {
      Float = 1,
      Int = 2,
      Bool = 3,
      Trigger = 4
   }

   [BurstCompatible]
   [Serializable]
   public unsafe struct Parameter
   {
      public StringHash NameHash;
      public ParameterType Type;
      public ParameterFlags Flags;
      public int ComponentTypeFieldOffset;

      public bool AutoRelease
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => (Flags & ParameterFlags.AutoRelease) != 0;
      }
   }

   public enum CompareOperation
   {
      If,
      IfNot,
      IntGreater,
      IntEquals,
      IntLess,
      IntNotEqual,
      IntGreaterOrEqual,
      IntLessOrEqual,
      FloatGreater,
      FloatLess,
      FloatEquals,
      FloatNotEqual,
      FloatGreaterOrEqual,
      FloatLessOrEqual,
      Trigger
   }

   [BurstCompatible]
   [StructLayout(LayoutKind.Explicit)]
   public struct Condition
   {
      [FieldOffset(0)] public int CompareParameterIndex;
      [FieldOffset(4)] public CompareOperation CompareOperation;
      [FieldOffset(8)] internal Variant m_CompareValue;
      [FieldOffset(8)] public int CompareIntValue;
      [FieldOffset(8)] public float CompareFloatValue;
      [FieldOffset(8)] public bool CompareBooleanValue;
   }

   public enum TransitionInterruptionSource
   {
      None = 0,
      Source = 1,
      Destination = 2,
      SourceThenDestination = 3,
      DestinationThenSource = 4
   }

   [BurstCompatible]
   public struct Transition
   {
      public float Offset;
      public float Duration;
      public bool HasExitTime;
      public float ExitTime;
      public bool HasFixedDuration;
      public bool CanTransitionToSelf;
      public int DestinationStateIndex;
      public BlobArray<Condition> Conditions;
      public TransitionInterruptionSource InterruptionSource;
      public bool OrderedInterruption;
   }

   public enum MotionType
   {
      Clip,
      BlendTree
   }

   public enum BlendTreeType
   {
      Simple1D,
      SimpleDirectional2D
   }

   [BurstCompatible]
   [StructLayout(LayoutKind.Sequential)]
   public struct MotionClip
   {
      // Motion header
      public MotionType Type;
      public float AverageLength;
      public WrapMode WrapMode;

      // MotionClip data
      public int ClipHashCode;
      public float FrameRate;
      public int SampleCount;
   }

   [BurstCompatible]
   [StructLayout(LayoutKind.Sequential)]
   public struct MotionBlendTree
   {
      [StructLayout(LayoutKind.Sequential)]
      public struct Node
      {
         public int MotionIndex;
         public float MotionSpeed;
         public float2 Position;
      }

      // Motion header
      public MotionType Type;
      public float AverageLength;
      public WrapMode WrapMode;

      // MotionBlendTree data
      public BlendTreeType BlendTreeType;
      public int HorizontalParameterIndex;
      public int VerticalParameterIndex;
      public BlobArray<Node> Nodes;
   }

   [BurstCompatible]
   [StructLayout(LayoutKind.Explicit)]
   public struct Motion
   {
      // header
      [FieldOffset(0)]
      public MotionType Type;
      [FieldOffset(sizeof(MotionType))]
      public float AverageLength;
      [FieldOffset(sizeof(MotionType) + sizeof(float))]
      public WrapMode WrapMode;

      [FieldOffset(0)]
      public MotionClip Clip;

      [FieldOffset(0)]
      public MotionBlendTree BlendTree;
   }

   [BurstCompatible]
   [Serializable]
   [StructLayout(LayoutKind.Explicit)]
   public struct Variant
   {
      [FieldOffset(0)] public int IntValue;
      [FieldOffset(0)] public float FloatValue;
      [FieldOffset(0)] public bool BooleanValue;

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static implicit operator Variant(bool b) => new Variant { BooleanValue = b };

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static implicit operator Variant(float f) => new Variant { FloatValue = f };

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static implicit operator Variant(int i) => new Variant { IntValue = i };

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static implicit operator float(Variant v) => v.FloatValue;

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static implicit operator int(Variant v) => v.IntValue;

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static implicit operator bool(Variant v) => v.BooleanValue;
   }
}
