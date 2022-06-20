using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdMorph
{
   [ExecuteAlways]
   [UpdateInGroup(typeof(SimulationSystemGroup))]
   public class AnimationInSimulation : ComponentSystemGroup
   {
      protected override void OnCreate()
      {
         if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
         {
            Debug.LogWarning("Warning: No Graphics Device found. Animation systems will not run.");
            Enabled = false;
         }

         base.OnCreate();
      }
   }

   [ExecuteAlways]
   [UpdateInGroup(typeof(AnimationInSimulation))]
   public class InstantiateSkeletonMatricesSystem : InstantiateSkeletonMatricesSystemBase { }

   [ExecuteAlways]
   [UpdateInGroup(typeof(AnimationInSimulation))]
   [UpdateAfter(typeof(InstantiateSkeletonMatricesSystem))]
   public class GatherSkeletonInstancesSystem : GatherSkeletonInstancesSystemBase { }

   [ExecuteAlways]
   [UpdateInGroup(typeof(AnimationInSimulation))]
   [UpdateAfter(typeof(GatherSkeletonInstancesSystem))]
   public class InstantiateSkinnnedMeshSystem : InstantiateSkinnnedMeshSystemBase {  }

   [ExecuteAlways]
   [UpdateInGroup(typeof(AnimationInSimulation))]
   [UpdateAfter(typeof(InstantiateSkinnnedMeshSystem))]
   public class InstantiateAnimatorSystem : InstantiateAnimatorSystemBase { }

   [ExecuteAlways]
   [UpdateInGroup(typeof(AnimationInSimulation))]
   [UpdateAfter(typeof(InstantiateSkinnnedMeshSystem))]
   public class GatherSkinnedMeshInstancesSystem : GatherSkinnedMeshInstancesSystemBase { }

   [ExecuteAlways]
   [UpdateInGroup(typeof(AnimationInSimulation))]
   [UpdateAfter(typeof(GatherSkinnedMeshInstancesSystem))]
   public class UpdateAnimatorSystem : UpdateAnimatorSystemBase  { }

   [ExecuteAlways]
   [UpdateInGroup(typeof(PresentationSystemGroup))]
   public class AnimationInPresentation : ComponentSystemGroup
   {
      protected override void OnCreate()
      {
         if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
         {
            Debug.LogWarning("Warning: No Graphics Device found. Animation systems will not run.");
            Enabled = false;
         }

         base.OnCreate();
      }
   }

   [ExecuteAlways]
   [UpdateInGroup(typeof(AnimationInPresentation))]
   public class AnimationSystem : AnimationSystemBase { }

   [ExecuteAlways]
   [UpdateInGroup(typeof(AnimationInPresentation))]
   [UpdateAfter(typeof(AnimationSystem))]
   public class ComputeLocalToRootSystem : ComputeLocalToRootSystemBase { }

   [ExecuteAlways]
   [UpdateInGroup(typeof(AnimationInPresentation))]
   [UpdateAfter(typeof(ComputeLocalToRootSystem))]
   public class SkinningSystem : SkinningSystemBase { }
}

