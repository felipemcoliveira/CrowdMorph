using Unity.Entities;
using Unity.Rendering;

namespace CrowdMorph
{
   public struct SharedSkinnedMesh : ISharedComponentData
   {
      public BlobAssetReference<SkinnedMeshDefinition> Value;
   }

   public unsafe struct SharedSkinnedMeshData : ISystemStateSharedComponentData
   {
      public int SkinnedMeshHashCode;
      public int SkinnedMeshBoneBufferIndex;
      public int BoneCount;
   }

   [MaterialProperty("_CrowdMorphSkinMatrixIndex", MaterialPropertyFormat.Float)]
   public struct SkinMatrixBufferIndex : ISystemStateComponentData
   {
      public int Value;
   }
}