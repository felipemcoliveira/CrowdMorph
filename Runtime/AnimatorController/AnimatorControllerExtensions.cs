using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace CrowdMorph
{
   public static class AnimatorControllerExtensions
   {
      public unsafe static void Reset(this DynamicBuffer<LayerState> layerStates)
      {
         UnsafeUtility.MemClear(layerStates.GetUnsafePtr(), sizeof(LayerState) * layerStates.Length);
      }
   }
}