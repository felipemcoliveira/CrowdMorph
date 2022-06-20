using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdMorph
{
   public struct AnimatorControllerOwner : IComponentData
   {
      public BlobAssetReference<AnimatorControllerDefinition> Controller;
   }

   public struct AnimatorControllerOwnerData : ISystemStateComponentData
   {
      public int AnimatorControllerHashCode;
   }

   public struct SharedAnimatorController : ISharedComponentData
   {
      public BlobAssetReference<AnimatorControllerDefinition> Controller;
   }

   public struct SharedAnimatorControllerData : ISystemStateSharedComponentData
   {
      public int ControllerHashCode;
   }

   [InternalBufferCapacity(32)]
   public struct ClipRef : IBufferElementData
   {
      public BlobAssetReference<Clip> Value;
   }

   [InternalBufferCapacity(8)]
   public struct SkeletonMaskRef : IBufferElementData
   {
      public BlobAssetReference<SkeletonMaskDefinition> Value;
   }

   public struct NoParametersTag : IComponentData { }

   public enum LayerStateFlags : byte
   {
      None = 0,
      Initialized = (1 << 0),
      IsInTransition = (1 << 1),
      IsActiveTransitionGlobal = (1 << 2),
      IsActiveTransitionFromDestinationState = (1 << 3),
      HasCurrentStateChanged = (1 << 4),
      HasDestinationStateChanged = (1 << 5),
      
      UpdateFlags = HasCurrentStateChanged |HasDestinationStateChanged,
      TransitionFlags = IsInTransition | IsActiveTransitionGlobal | IsActiveTransitionFromDestinationState
   }

   [BurstCompatible]
   [InternalBufferCapacity(4)]
   public unsafe struct LayerState : IBufferElementData
   {
      public bool IsActiveTransitionGlobal
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => (Flags & LayerStateFlags.IsActiveTransitionGlobal) != 0;
      }

      public bool IsActiveTransitionFromDestinationState
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => (Flags & LayerStateFlags.IsActiveTransitionFromDestinationState) != 0;
      }

      public bool IsInTransition
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => (Flags & LayerStateFlags.IsInTransition) != 0;
      }

      public bool Initialized
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => (Flags & LayerStateFlags.Initialized) != 0;
      }
      public bool HasDestinationStateChanged
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => (Flags & LayerStateFlags.HasDestinationStateChanged) != 0;
      }

      public bool HasCurrentStateChanged
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => (Flags & LayerStateFlags.HasCurrentStateChanged) != 0;
      }

      public float CurrentStateTime
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_CurrentStateTime;
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         internal set => m_CurrentStateTime = value;
      }

      public float DestinationStateTime
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_DestinationStateTime;
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         internal set => m_DestinationStateTime = value;
      }

      public float Weight
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_Weight;
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         set => m_Weight = math.clamp(value, 0, 1);
      }

      public byte CurrentStateIndex
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_CurrentStateIndex;
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         internal set => m_CurrentStateIndex = value;
      }

      public byte DestinationStateIndex
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_DestinationStateIndex;
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         internal set => m_DestinationStateIndex = value;
      }

      public byte ActiveTransitionIndex
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_ActiveTransitionIndex;
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         internal set => m_ActiveTransitionIndex = value;
      }

      public LayerStateFlags Flags
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_Flags;
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         internal set => m_Flags = value;
      }

      public float TransitionTime
      {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         get => m_TransitionTime;
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         set => m_TransitionTime = value;
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      float m_CurrentStateTime;
      float m_DestinationStateTime;
      float m_TransitionTime;
      float m_Weight;
      byte m_CurrentStateIndex;
      byte m_DestinationStateIndex;
      byte m_ActiveTransitionIndex;
      LayerStateFlags m_Flags;
   }
}