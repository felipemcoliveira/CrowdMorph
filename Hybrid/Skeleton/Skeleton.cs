using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace CrowdMorph.Hybrid
{
   [AddComponentMenu("CrowdMorph/Skeleton")]
   public class Skeleton : MonoBehaviour
   {
      static readonly List<Transform> s_TransformBuffer = new List<Transform>(64);

      [SerializeField]
      internal List<Transform> m_ExcludeBones = new List<Transform>();
      [SerializeField]
      Transform[] m_Bones = Array.Empty<Transform>();

      [SerializeField]
      bool m_DestroySkeletonHierarchy;

      [SerializeField]
      Transform m_RootBone;

      public Transform RootBone
      {
         get { return m_RootBone == null ? transform : m_RootBone; }
         set
         {
            if (value && !value.IsChildOf(transform))
               throw new ArgumentException($"{nameof(RootBone)} can only be set to a child of, or the transform of this Skeletom Component");

            if (value != m_RootBone)
            {
               m_RootBone = value;
               UpdateHierarchyCache();
            }
         }
      }

      public bool DestroySkeletonHierarchy => m_DestroySkeletonHierarchy;

      public Transform[] GetBones()
      {
         UpdateHierarchyCache();
         var array = new Transform[m_Bones.Length - 1];
         for (int i = 1; i < m_Bones.Length; i++)
            array[i - 1] = m_Bones[i];

         return array;
      }

      public void OnValidate()
      {
         UpdateHierarchyCache();
      }

      internal void Reset()
      {
         RootBone.GetComponentsInChildren(true, s_TransformBuffer);
         s_TransformBuffer.Remove(RootBone);
         m_Bones = s_TransformBuffer.ToArray();
         m_ExcludeBones = new List<Transform>();
         s_TransformBuffer.Clear();
      }

      internal void IncludeBonesFromSkinnedMeshRenderers()
      {
         var skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
         foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
         {
            foreach (var bone in skinnedMeshRenderer.bones)
               IncludeBoneAndAncestors(bone);
         }
      }

      internal bool IsBoneIncluded(Transform bone)
      {
         return bone != null && m_Bones.Contains(bone) && !m_ExcludeBones.Contains(bone);
      }

      static readonly List<Transform> s_UsedBoneTransforms = new List<Transform>(64);
      internal void UpdateHierarchyCache()
      {
         s_UsedBoneTransforms.Clear();
         RootBone.GetComponentsInChildren(true, s_TransformBuffer);

         foreach (var bone in s_TransformBuffer)
         {
            if (bone != RootBone && bone.parent && m_ExcludeBones.Contains(bone.parent) && !m_ExcludeBones.Contains(bone))
            {
               m_ExcludeBones.Add(bone);
               continue;
            }

            if (!m_ExcludeBones.Contains(bone))
               s_UsedBoneTransforms.Add(bone);
         }

         m_Bones = s_UsedBoneTransforms.ToArray();

         s_TransformBuffer.Clear();
         s_UsedBoneTransforms.Clear();
      }

      void CheckValidTransformBone(Transform bone)
      {
         if (bone == null)
            throw new ArgumentNullException($"The Argument {nameof(bone)} cannot be null.");

         if (!bone.IsChildOf(RootBone))
            throw new ArgumentException($"Bone must be a child transform of {RootBone}", nameof(bone));
      }

      public void IncludeBoneAndAncestors(Transform bone)
      {
         CheckValidTransformBone(bone);

         if (bone)
         {
            var parent = transform.parent;
            do
            {
               m_ExcludeBones.Remove(bone);
               bone = bone.parent;
            }
            while (bone && bone != parent);
         }
         UpdateHierarchyCache();
      }

      public void IncludeBoneAndDescendants(Transform bone)
      {
         CheckValidTransformBone(bone);

         bone.GetComponentsInChildren(true, s_TransformBuffer);
         foreach (var child in s_TransformBuffer)
         {
            if (m_ExcludeBones.Contains(child))
               m_ExcludeBones.Remove(child);
         }

         bone.GetComponentsInParent(true, s_TransformBuffer);
         foreach (var child in s_TransformBuffer)
         {
            if (m_ExcludeBones.Contains(child))
               m_ExcludeBones.Remove(child);
         }
         s_TransformBuffer.Clear();

         UpdateHierarchyCache();
      }

      public void ExcludeBoneAndDescendants(Transform bone)
      {
         CheckValidTransformBone(bone);

         bone.GetComponentsInChildren(true, s_TransformBuffer);
         foreach (var child in s_TransformBuffer)
         {
            if (!m_ExcludeBones.Contains(child))
               m_ExcludeBones.Add(child);
         }
         s_TransformBuffer.Clear();

         UpdateHierarchyCache();
      }

      internal int FindTransformIndex(Transform bone)
      {
         if (bone == null)
            return -1;
         
         return Array.IndexOf(m_Bones, bone);
      }
   }
}