using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace CrowdMorph
{
   public abstract unsafe class UpdateAnimatorSystemBase  : JobComponentSystem
   {
      // ----------------------------------------------------------------------------------------
      // Job Structures
      // ----------------------------------------------------------------------------------------

      [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
      internal unsafe struct UpdateAnimatorJob : IJobChunk
      {
         public static EntityQueryDesc QueryDesc => new EntityQueryDesc()
         {
            All = new ComponentType[]
            {
               ComponentType.ReadOnly<SharedAnimatorController>(),
               ComponentType.ReadOnly<SharedAnimatorControllerData>(),
               ComponentType.ReadOnly<SharedSkeleton>(),
               ComponentType.ReadOnly<SharedSkeletonData>(),
               ComponentType.ReadOnly<SkeletonMatrixBufferIndex>()
            },
         };

         const float k_WeightThreshold = 0.01f;

         public BlobAssetReference<AnimatorControllerDefinition> AnimatorController;

         public float DeltaTime;

         public SharedComponentTypeHandle<SharedSkeleton> SharedSkeletonTypeHandle;
         public BufferTypeHandle<LayerState> LayerStateTypeHandle;
         public DynamicComponentTypeHandle ParametersTypeHandle;

         [ReadOnly]
         public EntityTypeHandle EntityTypeHandle;

         [ReadOnly]
         public ComponentTypeHandle<SkeletonMatrixBufferIndex> SkeletonType;

         [ReadOnly]
         public NativeHashMap<int, BlobAssetReference<SkeletonDefinition>> SharedSkeletons;

         public AnimationContext AnimationContext;

         public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
         {
            var chunkParameterComponentDataPtr = GetChunkParameterComponentDataUnsafePtr(ref chunk, AnimatorController);

            var parametersPtr = stackalloc Variant[AnimatorController.Value.ParameterCount];
            var skeletonArray = chunk.GetNativeArray(SkeletonType);
            var layerStateBufferAccessor = chunk.GetBufferAccessor(LayerStateTypeHandle);

            int sharedSkeletonComponentIndex = chunk.GetSharedComponentIndex(SharedSkeletonTypeHandle);
            var skeleton = SharedSkeletons[sharedSkeletonComponentIndex];

            var entities = chunk.GetNativeArray(EntityTypeHandle);

            for (int i = 0; i < chunk.Count; i++)
            {
               var parametersComponentDataPtr = chunkParameterComponentDataPtr + (i * AnimatorController.Value.ParametersComponentTypeSize);

               var layerStateBuffer = layerStateBufferAccessor[i];
               var layerStatePtr = layerStateBuffer.GetUnsafePtr();

               Core.ReadParametersFromComponentData(AnimatorController, parametersPtr, parametersComponentDataPtr);
               var animationTarget = AnimationTarget.Create(skeleton, skeletonArray[i].Value, entities[i]);

               for (int j = 0; j < AnimatorController.Value.LayerCount; j++)
               {
                  ref var layer = ref AnimatorController.Value.Layers[j];
                  ref var layerState = ref UnsafeUtility.ArrayElementAsRef<LayerState>(layerStatePtr, j);

                  float prevCurrentStateTime = layerState.CurrentStateTime;
                  float prevDestinationStateTime = layerState.DestinationStateTime;
                  Core.UpdateLayer(ref layerState, ref layer, parametersPtr, DeltaTime, ref AnimatorController.Value.Motions);

                  if (layerState.HasCurrentStateChanged)
                     prevCurrentStateTime = layerState.CurrentStateTime;

                  if (layerState.HasDestinationStateChanged)
                     prevDestinationStateTime = layerState.DestinationStateTime;

                  if (!animationTarget.IsCreated || layerState.Weight < k_WeightThreshold)
                     continue;

                  float transitionNormalizedTime = 0;
                  if (layerState.IsInTransition)
                  {
                     ref var activeTransition = ref Core.GetActiveTranstion(ref layerState, ref layer.StateMachine);
                     transitionNormalizedTime = (layerState.TransitionTime / activeTransition.Duration);
                  }

                  float currentStateWeight = (1f - transitionNormalizedTime) * layerState.Weight;
                  if (currentStateWeight >= k_WeightThreshold)
                  {
                     ref var currentState = ref layer.StateMachine.States[layerState.CurrentStateIndex];
                     if (currentState.MotionIndex >= 0)
                     {
                        AnimationContext.EvaluateMotion(
                           ref animationTarget,
                           ref AnimatorController.Value.Motions[currentState.MotionIndex],
                           ref AnimatorController.Value.Motions,
                           layer.SkeletonMaskHashCode,
                           parametersPtr,
                           prevCurrentStateTime,
                           layerState.CurrentStateTime,
                           currentStateWeight,
                           layer.BlendingMode
                        );
                     }
                  }

                  float destinationStateWeight = transitionNormalizedTime * layerState.Weight;
                  if (layerState.IsInTransition && destinationStateWeight >= k_WeightThreshold)
                  {
                     ref var destinationState = ref layer.StateMachine.States[layerState.DestinationStateIndex];
                     if (destinationState.MotionIndex >= 0)
                     {
                        AnimationContext.EvaluateMotion(
                           ref animationTarget,
                           ref AnimatorController.Value.Motions[destinationState.MotionIndex],
                           ref AnimatorController.Value.Motions,
                           layer.SkeletonMaskHashCode,
                           parametersPtr,
                           prevDestinationStateTime,
                           layerState.DestinationStateTime,
                           destinationStateWeight,
                           layer.BlendingMode
                        );
                     }
                  }
               }
               Core.ReleaseTriggers(AnimatorController, parametersComponentDataPtr);
            }
         }

         [MethodImpl(MethodImplOptions.AggressiveInlining)]
         private byte* GetChunkParameterComponentDataUnsafePtr(ref ArchetypeChunk chunk, BlobAssetReference<AnimatorControllerDefinition> controller)
         {
            if (controller.Value.ParametersComponentTypeSize == 0)
               return null;

            var parametersArray = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ParametersTypeHandle, controller.Value.ParametersComponentTypeSize);
            return (byte*)parametersArray.GetUnsafePtr();
         }
      }

      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      protected override void OnCreate()
      {
         m_EntityQuery = GetEntityQuery(UpdateAnimatorJob.QueryDesc);

         m_AnimationSystem = World.GetOrCreateSystem<AnimationSystemBase>();

         m_SharedAnimators = new List<SharedAnimatorController>();
         m_SharedSkeletons = new List<SharedSkeleton>();
         m_SharedSkeletonComponentIndices = new List<int>();
      }

      protected override JobHandle OnUpdate(JobHandle inputDeps)
      {
         m_SharedAnimators.Clear();
         EntityManager.GetAllUniqueSharedComponentData(m_SharedAnimators);

         m_SharedSkeletons.Clear();
         m_SharedSkeletonComponentIndices.Clear();
         EntityManager.GetAllUniqueSharedComponentData(m_SharedSkeletons, m_SharedSkeletonComponentIndices);

         var sharedSkeletons = new NativeHashMap<int, BlobAssetReference<SkeletonDefinition>>(8, Allocator.Persistent);

         for (int i = 0; i < m_SharedSkeletons.Count; i++)
            sharedSkeletons.Add(m_SharedSkeletonComponentIndices[i], m_SharedSkeletons[i].Value);

         var animationContext = m_AnimationSystem.GetContext();

         foreach (var sharedAnimator in m_SharedAnimators)
         {
            if (sharedAnimator.Controller == BlobAssetReference<AnimatorControllerDefinition>.Null)
               continue;

            m_EntityQuery.SetSharedComponentFilter(sharedAnimator);

            var parametersComponentType = Core.GetParametersComponentType(sharedAnimator.Controller);

            var updateHandle = new UpdateAnimatorJob
            {
               DeltaTime = Time.DeltaTime,
               AnimatorController = sharedAnimator.Controller,
               LayerStateTypeHandle = GetBufferTypeHandle<LayerState>(),
               ParametersTypeHandle = GetDynamicComponentTypeHandle(parametersComponentType),
               SharedSkeletonTypeHandle = GetSharedComponentTypeHandle<SharedSkeleton>(),
               SkeletonType = GetComponentTypeHandle<SkeletonMatrixBufferIndex>(true),
               SharedSkeletons = sharedSkeletons,
               AnimationContext = animationContext,
               EntityTypeHandle = GetEntityTypeHandle()
            }.ScheduleParallel(m_EntityQuery, inputDeps);

            inputDeps = JobHandle.CombineDependencies(updateHandle, inputDeps);
         }

         m_AnimationSystem.AddJobHandleForCommandProducer(inputDeps);
         inputDeps = sharedSkeletons.Dispose(inputDeps);
         return inputDeps;
      }

      // ----------------------------------------------------------------------------------------
      // Private Fields
      // ----------------------------------------------------------------------------------------

      EntityQuery m_EntityQuery;
      List<SharedAnimatorController> m_SharedAnimators;
      List<SharedSkeleton> m_SharedSkeletons;
      List<int> m_SharedSkeletonComponentIndices;

      AnimationSystemBase m_AnimationSystem;
   }
}
