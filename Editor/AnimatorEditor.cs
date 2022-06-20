using UnityEditor;
using CrowdMorph.Hybrid;
using UnityEditor.Animations;
using System;
using Unity.Entities;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Animator = CrowdMorph.Hybrid.Animator;

namespace CrowdMorph.Editor
{
   [CustomEditor(typeof(Hybrid.Animator))]
   [CanEditMultipleObjects]
   class AnimatorEditor : UnityEditor.Editor
   {
      SerializedProperty m_ControllerProp;
      SerializedProperty m_ParametersComponentTypeProp;

      string[] m_ParametersComponentTypeErrors;
      string m_ParametersComponentTypeErrorsMessage;

      public void OnEnable()
      {
         m_ControllerProp = serializedObject.FindProperty("m_Controller");
         m_ParametersComponentTypeProp = serializedObject.FindProperty("m_ParametersComponentType");

         UpdateParameterComponentTypeErrors();
      }

      public override void OnInspectorGUI()
      {
         serializedObject.Update();
         EditorGUI.BeginChangeCheck();
         {
            EditorGUILayout.PropertyField(m_ControllerProp);

            if (m_ParametersComponentTypeErrors == null)
               UpdateParameterComponentTypeErrors();

            if (m_ParametersComponentTypeErrors != null && m_ParametersComponentTypeErrors.Length > 0)
               EditorGUILayout.HelpBox(m_ParametersComponentTypeErrorsMessage, MessageType.Error, true);

            EditorGUILayout.PropertyField(m_ParametersComponentTypeProp);
         }
         if (EditorGUI.EndChangeCheck())
         {
            serializedObject.ApplyModifiedProperties();
            UpdateParameterComponentTypeErrors();
         }
      }

      private void UpdateParameterComponentTypeErrors()
      {
         if (targets.Length == 1 && target is Animator animator)
         {
            m_ParametersComponentTypeErrors = ParseParameterComponetTypeErrors(animator.Controller, animator.ParametersComponentType);
            for (int i = 0; i < m_ParametersComponentTypeErrors.Length; i++)
               m_ParametersComponentTypeErrors[i] = m_ParametersComponentTypeErrors[i];

            m_ParametersComponentTypeErrorsMessage = "This component type is not valid:\n";
            m_ParametersComponentTypeErrorsMessage += string.Join("\n", m_ParametersComponentTypeErrors); 
         }
      }

      private static string[] ParseParameterComponetTypeErrors(RuntimeAnimatorController runtimeAnimatorController, Type componentType)
      {
         if (runtimeAnimatorController == null)
            return Array.Empty<string>();

         string animmatorControllerAssetPath = AssetDatabase.GetAssetPath(runtimeAnimatorController);
         var animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(animmatorControllerAssetPath);

         var parameters = animatorController.parameters;
         if (parameters.Length == 0 && componentType == null)
            return Array.Empty<string>();

         List<string> errors = new List<string>();
         foreach (var parameter in parameters)
         {
               var parameterField = componentType?.GetField(parameter.name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
               if (parameterField == null)
               {
                  errors.Add($"Missing parameter \"{parameter.name}\" field.");
               }
               else
               {
                  var fieldType = parameterField.FieldType;
                  Type expectedType = null;
                  string expectedTypeName = null;
                  switch (parameter.type)
                  {
                     case AnimatorControllerParameterType.Float:
                        expectedType = typeof(float);
                        expectedTypeName = "float";
                        break;
                     case AnimatorControllerParameterType.Int:
                        expectedType = typeof(int);
                        expectedTypeName = "int";
                        break;
                     case AnimatorControllerParameterType.Bool:
                        expectedType = typeof(bool);
                        expectedTypeName = "bool";
                        break;
                     case AnimatorControllerParameterType.Trigger:
                        expectedType = typeof(bool);
                        expectedTypeName = "bool";
                        break;
                  }

                  if (fieldType != expectedType)
                     errors.Add($"Field \"{parameterField.Name}\" should be of type {expectedTypeName} to match parameter type.");
            }
         }
         return errors.ToArray();
      }
   }
}