using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace CrowdMorph.Editor
{
   internal class SkeletonBoneTreeView : TreeView
   {
      static class Styles
      {
         public static readonly string IndexFormatString = L10n.Tr("(Index {0})");
         public static readonly GUIStyle Index = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };
      }

      const float k_ToggleWidth = 16f;
      const float k_ToggleSpacing = 2f;
      const float k_IconSpacing = 20f;
      static readonly string k_AddBone = L10n.Tr("Add Bone");
      static readonly string k_RemoveBone = L10n.Tr("Remove Bone");

      public bool EditMode { get; set; }
      public Hybrid.Skeleton SkeletonComponent { get; set; }
      public int NodeCount { get { return s_TransformIndices.Count; } }

      static GUIContent s_MeshRendererContent;
      static GUIContent MeshRendererContent => s_MeshRendererContent ??= new GUIContent(string.Empty, AssetPreview.GetMiniTypeThumbnail(typeof(MeshRenderer)));
     
      static GUIContent s_SkinnedMeshRendererContent;
      static GUIContent SkinnedMeshRendererContent => s_SkinnedMeshRendererContent ??= new GUIContent(string.Empty, AssetPreview.GetMiniTypeThumbnail(typeof(SkinnedMeshRenderer)));

      public SkeletonBoneTreeView(TreeViewState state) : base(state)
      {
         useScrollView = false;
         showBorder = true;
      }

      protected override bool CanStartDrag(CanStartDragArgs args)
      {
         return true;
      }

      protected override TreeViewItem BuildRoot()
      {
         var root = new TreeViewItem { id = 0, depth = -1 };

         s_TransformIndices.Clear();
         int index = 0;
         if (SkeletonComponent != null)
         {
            var rootBone = SkeletonComponent.RootBone;
            if (!EditMode && !SkeletonComponent.IsBoneIncluded(rootBone))
            {
               AddChildrenRecursive(rootBone, root, ref index);
            }
            else
            {
               s_TransformIndices[rootBone] = index;
               index++;
               var rootItem = new TreeViewItem(rootBone.GetInstanceID(), -1, rootBone.name);
               root.AddChild(rootItem);
               if (rootBone.childCount > 0)
                  AddChildrenRecursive(rootBone, rootItem, ref index);
            }
         }

         SetupDepthsFromParentsAndChildren(root);
         return root;
      }

      Transform GetTransform(int InstanceID)
      {
         return (Transform)EditorUtility.InstanceIDToObject(InstanceID);
      }

      static readonly Dictionary<Transform, int> s_TransformIndices = new Dictionary<Transform, int>();
      void AddChildrenRecursive(Transform parentTransfrom, TreeViewItem item, ref int index)
      {
         int childCount = parentTransfrom.childCount;

         item.children = new List<TreeViewItem>(childCount);
         for (int i = 0; i < childCount; ++i)
         {
            var childTransform = parentTransfrom.GetChild(i);

            if (!EditMode && !SkeletonComponent.IsBoneIncluded(childTransform))
               continue;

            s_TransformIndices[childTransform] = index;
            index++;

            var childItem = new TreeViewItem(childTransform.GetInstanceID(), -1, childTransform.name);
            item.AddChild(childItem);
            if (childTransform.childCount > 0)
               AddChildrenRecursive(childTransform, childItem, ref index);
         }
      }

      protected override void DoubleClickedItem(int id)
      {
         base.DoubleClickedItem(id);
         var transform = GetTransform(id);
         EditorGUIUtility.PingObject(transform);
      }

      static GUIContent GetIconForTransform(Transform transform)
      {
         if (transform.TryGetComponent(out SkinnedMeshRenderer _))
            return SkinnedMeshRendererContent;

         if (transform.TryGetComponent(out MeshRenderer _))
            return MeshRendererContent;
         
         return null;
      }

      protected override void RowGUI(RowGUIArgs args)
      {
         var transform = GetTransform(args.item.id);
         if (transform == null)
            return;

         float offset = 0;
         if (EditMode)
         {
            extraSpaceBeforeIconAndLabel = k_ToggleWidth + k_ToggleSpacing;
            var toggleRect = args.rowRect;
            toggleRect.x += GetContentIndent(args.item);
            toggleRect.width = k_ToggleWidth;
            offset = toggleRect.x + k_ToggleWidth;
            var evt = UnityEngine.Event.current;
            if (evt.type == EventType.MouseDown && toggleRect.Contains(evt.mousePosition))
               SelectionClick(args.item, false);

            EditorGUI.BeginChangeCheck();
            var included = SkeletonComponent.IsBoneIncluded(transform);
            included = EditorGUI.Toggle(toggleRect, included);
            if (EditorGUI.EndChangeCheck())
            {
               Undo.RecordObject(SkeletonComponent, included ? k_AddBone : k_RemoveBone);
               if (included)
               {
                  SkeletonComponent.IncludeBoneAndAncestors(transform);
                  if (evt.alt)
                     SkeletonComponent.IncludeBoneAndDescendants(transform);
               }
               else
               {
                  SkeletonComponent.ExcludeBoneAndDescendants(transform);
               }
               EditorUtility.SetDirty(SkeletonComponent);
               Reload();
            }
         }
         else
         {
            extraSpaceBeforeIconAndLabel = 0;
         }

         var iconContent = GetIconForTransform(transform);
         if (iconContent != null)
         {
            if (offset == 0)
               offset = GetContentIndent(args.item);
            
            var iconRect = args.rowRect;
            iconRect.x += offset;
            iconRect.width = k_IconSpacing;
            extraSpaceBeforeIconAndLabel += k_IconSpacing;
            GUI.Label(iconRect, iconContent);
         }

         var idx = SkeletonComponent.FindTransformIndex(transform);
         if (idx >= 0)
         {
            if (!s_IndexLabels.TryGetValue(idx, out var indexLabel))
               s_IndexLabels[idx] = indexLabel = string.Format(Styles.IndexFormatString, idx);

            using (new EditorGUI.DisabledScope(true))
               GUI.Label(args.rowRect, indexLabel, Styles.Index);
         }
         base.RowGUI(args);
      }

      static Dictionary<int, string> s_IndexLabels = new Dictionary<int, string>();
      static List<Object> s_SortedObjectList = new List<UnityEngine.Object>();
      protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
      {
         DragAndDrop.PrepareStartDrag();

         var sortedDraggedIDs = SortItemIDsInRowOrder(args.draggedItemIDs);
         s_SortedObjectList.Clear();
         if (s_SortedObjectList.Capacity < sortedDraggedIDs.Count)
         {
            s_SortedObjectList.Capacity = sortedDraggedIDs.Count;
         }
         foreach (var id in sortedDraggedIDs)
         {
            var @object = GetTransform(id);
            if (@object != null)
               s_SortedObjectList.Add(@object);
         }

         DragAndDrop.objectReferences = s_SortedObjectList.ToArray();
         DragAndDrop.StartDrag(s_SortedObjectList.Count > 1 ? "<Multiple>" : s_SortedObjectList[0].name);

         s_SortedObjectList.Clear(); 
      }
   }
}