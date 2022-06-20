using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using CrowdMorph.Hybrid;

namespace CrowdMorph.Editor
{

   [CustomEditor(typeof(Hybrid.Skeleton))]
   [CanEditMultipleObjects]
   class SkeletonEditor : UnityEditor.Editor
   {
      static readonly GUIContent s_SkeletonRootBone = EditorGUIUtility.TrTextContent("Skeleton Root Bone", "This sets the top most root bone of the skeleton. This transform must be below the Skeleton in the hierarchy. When empty the transform of the Skeleton is used as the root");
      static readonly GUIContent s_EditButton = EditorGUIUtility.TrTextContent("Edit");
      static readonly GUIContent s_FinishButton = EditorGUIUtility.TrTextContent("Finish");
      static readonly GUIContent s_MultipleTargetsNotSupported = EditorGUIUtility.TrTextContent("Displaying a bone hierarchy for more than one target object is not supported");
      static readonly GUIContent s_CheckAll = EditorGUIUtility.TrTextContent("Check All");
      static readonly GUIContent s_UncheckAll = EditorGUIUtility.TrTextContent("Uncheck All");
      static readonly GUIContent s_IncludeSkinnedMeshRenderersBone = EditorGUIUtility.TrTextContent("Include SkinnedMeshRenderer bones");


      static readonly string kNoBonesToDisplay = L10n.Tr("No bones have been selected");
      static readonly string kSkeletonRootBoneChanged = L10n.Tr("Skeleton Root Bone Changed");
      static readonly string kSkeletonRootBoneMustBeChildOfSkeletonComponent = L10n.Tr("A Skeleton Root Bone must be a child of, or the transform of this Skeleton Component");
      static readonly string kCheckAllUndo = L10n.Tr("Added all bones to Skeleton");
      static readonly string kIncludeSMRBonesUndo = L10n.Tr("Added all bones from SkinnedMeshRenderers");
      static readonly string kUnCheckAllUndo = L10n.Tr("Removed all bones from Skeleton");

      static GUIStyle s_SearchFieldStyle;
      static GUIStyle SearchFieldStyle => s_SearchFieldStyle ??= "SearchTextField";

      SerializedProperty m_RootBoneProp;
      SerializedProperty m_BonesProp;

      SearchField m_SearchField;
      SkeletonBoneTreeView m_TreeView;
      [SerializeField] 
      TreeViewState m_TreeViewState;

      public void OnEnable()
      {
         m_RootBoneProp = serializedObject.FindProperty("m_RootBone");
         m_BonesProp = serializedObject.FindProperty("m_Bones");
         
         if (m_TreeViewState == null)
            m_TreeViewState = new TreeViewState();

         m_TreeView = new SkeletonBoneTreeView(m_TreeViewState);
         if (targets.Length == 1)
         {
            m_TreeView.EditMode = false;
            m_TreeView.SkeletonComponent = target as Hybrid.Skeleton;
            m_TreeView.Reload();
            m_TreeView.ExpandAll();
         }

         m_SearchField = new SearchField();
         m_SearchField.downOrUpArrowKeyPressed += m_TreeView.SetFocusAndEnsureSelectedItem;

         Undo.undoRedoPerformed -= UndoRedoPerformed;
         Undo.undoRedoPerformed += UndoRedoPerformed;
      }

      public void OnDisable()
      {
         Undo.undoRedoPerformed -= UndoRedoPerformed;
      }
    

      public void UndoRedoPerformed()
      {
         m_TreeView.Reload();
         Repaint();
      }

      void ShowEditSkeletonRootBone(Skeleton authoringSkeleton, SerializedProperty skeletonRootBoneProp)
      {
         EditorGUI.BeginChangeCheck();
         var previewValue = skeletonRootBoneProp.objectReferenceValue;
         EditorGUILayout.PropertyField(skeletonRootBoneProp, s_SkeletonRootBone, false);
         if (EditorGUI.EndChangeCheck())
         {
            var newSkeletonRootBone = skeletonRootBoneProp.objectReferenceValue as Transform;
            if (newSkeletonRootBone && !newSkeletonRootBone.IsChildOf(authoringSkeleton.transform))
            {
               Debug.LogError(kSkeletonRootBoneMustBeChildOfSkeletonComponent);
               skeletonRootBoneProp.objectReferenceValue = previewValue;
            }
            else
            {
               serializedObject.ApplyModifiedProperties();
               Undo.RecordObjects(targets, kSkeletonRootBoneChanged);
               serializedObject.UpdateIfRequiredOrScript();
               m_TreeView.Reload();
               m_TreeView.ExpandAll();
               GUIUtility.ExitGUI();
            }
         }
      }

      void ShowTransformHierarchy(Skeleton authoringSkeleton, SerializedProperty bonesProp, SerializedProperty skeletonRootBoneProp)
      {
         if (targets.Length > 1)
         {
            EditorGUILayout.HelpBox(s_MultipleTargetsNotSupported);
            return;
         }

         var rootBone = authoringSkeleton.RootBone;
         if (bonesProp.editable)
         {
            if (GUILayout.Button(m_TreeView.EditMode ? s_FinishButton : s_EditButton))
            {
               m_TreeView.EditMode = !m_TreeView.EditMode;
               m_TreeView.Reload();
               GUIUtility.ExitGUI();
            }
         }
         else
         {
            m_TreeView.EditMode = false;
         }

         int numberOfBonesToDisplay = m_TreeView.NodeCount;
         if (numberOfBonesToDisplay == 0)
         {
            EditorGUILayout.HelpBox(kNoBonesToDisplay, MessageType.Warning);
            return;
         }

         if (m_TreeView.EditMode && (numberOfBonesToDisplay > 1 || rootBone != null))
            ShowEditSkeletonRootBone(authoringSkeleton, skeletonRootBoneProp);

         if (numberOfBonesToDisplay > 1)
         {
            var searchRect = GUILayoutUtility.GetRect(0, 10000, 0, EditorGUIUtility.singleLineHeight);
            if (string.IsNullOrWhiteSpace(m_TreeView.searchString))
            {
               m_TreeView.searchString = m_SearchField.OnGUI(searchRect, m_TreeView.searchString, SearchFieldStyle, GUIStyle.none, GUIStyle.none);
            }
            else
            {
               m_TreeView.searchString = m_SearchField.OnGUI(searchRect, m_TreeView.searchString);
            }

            if (m_TreeView.EditMode)
            {
               var topRect = GUILayoutUtility.GetRect(0, 100000, 0, EditorGUIUtility.singleLineHeight);
               GUI.Box(topRect, GUIContent.none);

               var leftButtonRect = topRect;
               var rightButtonRect = topRect;

               leftButtonRect.width /= 2;
               rightButtonRect.xMin = leftButtonRect.xMax;

               if (GUI.Button(leftButtonRect, s_CheckAll))
               {
                  CheckAll();
                  m_TreeView.Reload();
               }
               if (GUI.Button(rightButtonRect, s_UncheckAll))
               {
                  UncheckAll();
                  m_TreeView.Reload();
               }

               var bottomRect = GUILayoutUtility.GetRect(0, 100000, 0, EditorGUIUtility.singleLineHeight);
               if (GUI.Button(bottomRect, s_IncludeSkinnedMeshRenderersBone))
               {
                  IncludeBonesFromSkinnedMeshRenderers();
                  m_TreeView.Reload();
               }
            }
         }
         else
         {
            m_TreeView.searchString = string.Empty;
         }

         var treeRect = GUILayoutUtility.GetRect(0, 100000, 0, m_TreeView.totalHeight);
         m_TreeView.OnGUI(treeRect);
      }

      void CheckAll()
      {
         Undo.RecordObjects(targets, kCheckAllUndo);
         foreach (var target in targets)
         {
            var authoringSkeleton = target as Hybrid.Skeleton;
            authoringSkeleton.IncludeBoneAndDescendants(authoringSkeleton.RootBone);
         }
      }

      void UncheckAll()
      {
         Undo.RecordObjects(targets, kUnCheckAllUndo);
         foreach (var target in targets)
         {
            var authoringSkeleton = target as Hybrid.Skeleton;
            authoringSkeleton.ExcludeBoneAndDescendants(authoringSkeleton.RootBone);
         }
      }

      void IncludeBonesFromSkinnedMeshRenderers()
      {
         Undo.RecordObjects(targets, kIncludeSMRBonesUndo);
         foreach (var target in targets)
         {
            var authoringSkeleton = target as Skeleton;
            authoringSkeleton.IncludeBonesFromSkinnedMeshRenderers();
         }
      }

      public override void OnInspectorGUI()
      {
         EditorGUI.BeginChangeCheck();
         {
            var targetSkeleton = target as Skeleton;
            ShowTransformHierarchy(targetSkeleton, m_BonesProp, m_RootBoneProp);
         }
         if (EditorGUI.EndChangeCheck())
         {
            serializedObject.ApplyModifiedProperties();
            m_TreeView.Reload();
         }
      }
   }
}