using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using CrowdMorph;

namespace CrowdMorph.Editor
{
   [CustomPropertyDrawer(typeof(SerializableComponentType), true)]
   public class SerializableComponentTypePropertyDrawer : PropertyDrawer
   {
      static string[] s_DisplayTypeNames;
      static string[] s_TypeNames;
      static int s_CachedSelectedIndex;

      public unsafe override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
      {
         EditorGUI.BeginProperty(position, label, property);
         if (s_DisplayTypeNames == null)
         {
            var displayTypeNames = new List<string>(128);
            var typeNames = new List<string>(128);

            var types = new List<Type>(128);

            types.AddRange(TypeCache.GetTypesDerivedFrom(typeof(IComponentData)));
            types.AddRange(TypeCache.GetTypesDerivedFrom(typeof(ISystemStateComponentData)));

            displayTypeNames.Add("Not Selected");
            typeNames.Add(string.Empty);

            foreach (var type in types)
            {
               if (type.IsGenericType || type.IsAbstract || !type.IsValueType || type.IsInterface)
                  continue;

               displayTypeNames.Add(type.FullName.Replace('.', '/'));
               typeNames.Add(type.AssemblyQualifiedName);
            }

            s_DisplayTypeNames = displayTypeNames.ToArray();
            s_TypeNames = typeNames.ToArray();
         }

         var typeNameProperty = property.FindPropertyRelative("m_TypeName");
         position = EditorGUI.PrefixLabel(position, label);

         string typeName = typeNameProperty.stringValue;
         int selectedIndex;
         if (s_CachedSelectedIndex >= 0 && s_CachedSelectedIndex < s_TypeNames.Length && s_TypeNames[s_CachedSelectedIndex] == typeName)
         {
            selectedIndex = s_CachedSelectedIndex;
         }
         else
         {
            selectedIndex = Array.IndexOf(s_TypeNames, typeNameProperty.stringValue);
            s_CachedSelectedIndex = selectedIndex;
         }


         EditorGUI.BeginChangeCheck();
         int newSelectedIndex = EditorGUI.Popup(position, selectedIndex, s_DisplayTypeNames);
         if (EditorGUI.EndChangeCheck())
            typeNameProperty.stringValue = s_TypeNames[newSelectedIndex];

         EditorGUI.EndProperty();
      }
   }
}