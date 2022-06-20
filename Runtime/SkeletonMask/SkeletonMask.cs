using System.Runtime.CompilerServices;
using Unity.Entities;

namespace CrowdMorph
{
   public struct SkeletonMaskDefinition
   {
      public BlobArray<StringHash> BoneIDs;
      public BlobArray<bool> IsBoneActive;

      internal int m_HashCode;

      public int BoneCount => BoneIDs.Length;

      // ----------------------------------------------------------------------------------------
      // Overriden Methods
      // ----------------------------------------------------------------------------------------

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public override int GetHashCode() => m_HashCode;
   }
}