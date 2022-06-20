using Unity.Entities;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif 

namespace CrowdMorph.Hybrid
{
#if UNITY_EDITOR
   [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
   [ConverterVersion(userName: "CrowdMorph.Hybrid.DeclareReferencedObjectsConversionSystem", version: 2)]
   public class DeclareReferencedObjectsConversionSystem : GameObjectConversionSystem
   {
      protected override void OnUpdate()
      {
         Entities.ForEach((Animator animator) =>
         {
            if (animator.Controller != null)
            {
               var runtimeAnimatorController = animator.Controller;
               string animmatorControllerAssetPath = AssetDatabase.GetAssetPath(runtimeAnimatorController);
               var animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(animmatorControllerAssetPath);
               DeclareReferencedAsset(animatorController);
            }
         });
      }
   }
#endif
}