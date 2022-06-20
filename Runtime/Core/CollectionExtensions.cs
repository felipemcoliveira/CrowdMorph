using System;
using Unity.Collections;

namespace CrowdMorph
{
   [BurstCompatible]
   public static partial class CollectionExtensions
   {
      public static bool Incremenet<T>(this NativeHashMap<T, int> map, T key) where T : unmanaged, IEquatable<T>
      {
         if (map.TryGetValue(key, out int value))
         {
            map[key] = value + 1;
            return false;
         }

         map.Add(key, 1);
         return true;
      }

      public static bool Decrement<T>(this NativeHashMap<T, int> map, T key) where T : unmanaged, IEquatable<T>
      {
         int value = map[key];

         if(value == 1)
         {
            map.Remove(key);
            return true;
         }

         map[key] = value - 1;
         return false;
      }
   }
}
