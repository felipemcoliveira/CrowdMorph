#if UNITY_EDITOR

using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;

namespace CrowdMorph.Hybrid
{
   public static class ClipBuilder
   {
      public static BlobAssetReference<Clip> Build(AnimationClip authoringClip)
      {
         if (authoringClip == null)
            return BlobAssetReference<Clip>.Null;
         
         Assert.IsFalse(authoringClip.isHumanMotion, "Human montion is not supported.");

         var animationCurveBindings = AnimationUtility.GetCurveBindings(authoringClip);

         var translationBindings = new List<string>();
         var scalesBindings = new List<string>();
         var rotationsBindings = new List<string>();

         foreach (var curveBinding in animationCurveBindings)
         {
            if (curveBinding.type == typeof(Transform))
            {
               switch (curveBinding.propertyName)
               {
                  case "m_LocalPosition.x":
                     translationBindings.Add(curveBinding.path);
                     break;
                  case "m_LocalRotation.x":
                     rotationsBindings.Add(curveBinding.path);
                     break;
                  case "m_LocalScale.x":
                     scalesBindings.Add(curveBinding.path);
                     break;
               }
            }
         }

         var blobBuilder = new BlobBuilder(Allocator.Temp);
         
         ref var clip = ref blobBuilder.ConstructRoot<Clip>();
         clip.FrameRate = authoringClip.frameRate;
         clip.Length = authoringClip.length;
         clip.WrapMode = authoringClip.isLooping ? WrapMode.Loop : WrapMode.Once;

         var scratchCurvers = new AnimationCurve[4];

         var translationBindingsArray = blobBuilder.Allocate(ref clip.TranslationsBindings, translationBindings.Count);
         var translations = blobBuilder.Allocate(ref clip.LocalTranslations, translationBindings.Count * clip.SampleCount);
         for (int i = 0; i < translationBindings.Count; i++)
         {
            string path = translationBindings[i];
            translationBindingsArray[i] = path;

            scratchCurvers[0] = GetEditorCurve(authoringClip, path, "m_LocalPosition.x");
            scratchCurvers[1] = GetEditorCurve(authoringClip, path, "m_LocalPosition.y");
            scratchCurvers[2] = GetEditorCurve(authoringClip, path, "m_LocalPosition.z");
            ConvertCurves(ref clip, ref translations, scratchCurvers, i, translationBindings.Count);
         }

         var rotationBindingArray = blobBuilder.Allocate(ref clip.RotationBindings, rotationsBindings.Count);
         var rotations = blobBuilder.Allocate(ref clip.LocalRotations, rotationsBindings.Count * clip.SampleCount);
         for (int i = 0; i < rotationsBindings.Count; i++)
         {
            string path = rotationsBindings[i];
            rotationBindingArray[i] = path;

            scratchCurvers[0] = GetEditorCurve(authoringClip, path, "m_LocalRotation.x");
            scratchCurvers[1] = GetEditorCurve(authoringClip, path, "m_LocalRotation.y");
            scratchCurvers[2] = GetEditorCurve(authoringClip, path, "m_LocalRotation.z");
            scratchCurvers[3] = GetEditorCurve(authoringClip, path, "m_LocalRotation.w");
            ConvertCurves(ref clip, ref rotations, scratchCurvers, i, rotationsBindings.Count);
         }

         var scaleBindingsArray = blobBuilder.Allocate(ref clip.ScalesBindings, scalesBindings.Count);
         var scales = blobBuilder.Allocate(ref clip.LocalScales, scalesBindings.Count * clip.SampleCount);
         for (int i = 0; i < scalesBindings.Count; i++)
         {
            string path = scalesBindings[i];
            scaleBindingsArray[i] = path;

            scratchCurvers[0] = GetEditorCurve(authoringClip, path, "m_LocalScale.x");
            scratchCurvers[1] = GetEditorCurve(authoringClip, path, "m_LocalScale.y");
            scratchCurvers[2] = GetEditorCurve(authoringClip, path, "m_LocalScale.z");
            ConvertCurves(ref clip, ref scales, scratchCurvers, i, scalesBindings.Count);
         }

         var events = blobBuilder.Allocate(ref clip.Events, authoringClip.events.Length);
         for (int i = 0; i < events.Length; i++)
         {
            var authoringEvent = authoringClip.events[i];
            events[i].FunctionNameHash = authoringEvent.functionName;
            events[i].IntParameter = authoringEvent.intParameter;
            events[i].FloatParameter = authoringEvent.floatParameter;
            events[i].Time = authoringEvent.time;
         }

         var outputClip = blobBuilder.CreateBlobAssetReference<Clip>(Allocator.Persistent);
         outputClip.Value.HashCode = HashUtility.ComputeClipHash(ref outputClip.Value);
         blobBuilder.Dispose();

         return outputClip;
      }

      private static AnimationCurve GetEditorCurve(AnimationClip clip, string path, string propertyName)
      {
         return AnimationUtility.GetEditorCurve(clip, new EditorCurveBinding
         {
            path = path,
            propertyName = propertyName,
            type = typeof(Transform)
         });
      }

      private static void ConvertCurves<T>(ref Clip clip, ref BlobBuilderArray<T> dest, AnimationCurve[] curves, int boneIndex, int curveCount) where T : unmanaged
      {
         var lastValue = default(T);
         for (var frame = 0; frame < clip.FrameCount; frame++)
         {
            lastValue = Evaluate<T>(curves, frame / clip.FrameRate);
            dest[frame * curveCount + boneIndex] = lastValue;
         }
         var atDurationVale = Evaluate<T>(curves, clip.Length);
         dest[clip.FrameCount * curveCount + boneIndex] = AdjustLastFrameValue(lastValue, atDurationVale, clip.LastFrameError);
      }

      private unsafe static T AdjustLastFrameValue<T>(T beforeLastFrame, T atDurationValue, float lastFrameError) where T : unmanaged
      {
         Assert.IsTrue(sizeof(T) % sizeof(float) == 0);
         T result = default;
         for (int valueElementIdx = 0; valueElementIdx < sizeof(T) / sizeof(float); valueElementIdx++)
         {
            float beforeLastFrameAsFloat = UnsafeUtility.ReadArrayElement<float>(&beforeLastFrame, valueElementIdx);
            float atDurationValueAsFloat = UnsafeUtility.ReadArrayElement<float>(&atDurationValue, valueElementIdx);
            UnsafeUtility.WriteArrayElement(&result, valueElementIdx, AdjustLastFrameValue(beforeLastFrameAsFloat, atDurationValueAsFloat, lastFrameError));
         }
         return result;
      }

      private static float AdjustLastFrameValue(float beforeLastValue, float atDurationValue, float lastFrameError)
      {
         return lastFrameError < 1.0f ? math.lerp(beforeLastValue, atDurationValue, 1.0f / (1.0f - lastFrameError)) : atDurationValue;
      }

      private unsafe static T Evaluate<T>(AnimationCurve[] curves, float t) where T : unmanaged
      {
         int floatCount = sizeof(T) / sizeof(float);

         Assert.IsTrue(sizeof(T) % sizeof(float) == 0);
         Assert.IsTrue(floatCount <= curves.Length);

         var result = default(T);

         for (int elementIdx = 0; elementIdx < floatCount; elementIdx++)
         {
            float value = curves[elementIdx].Evaluate(t);
            UnsafeUtility.WriteArrayElement(&result, elementIdx, value);
         }

         return result;
      }
   }
}

#endif