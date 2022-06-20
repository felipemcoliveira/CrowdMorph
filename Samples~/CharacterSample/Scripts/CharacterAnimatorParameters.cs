using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdMorph.CharacterSample
{
   [UpdateAfter(typeof(AnimationInSimulation))]
   [UpdateInGroup(typeof(SimulationSystemGroup))]
   public class CharacterSystemGroup : ComponentSystemGroup { }

   public struct CharacterAnimatorParameters : IComponentData
   {
      public float Speed;
      public bool IsAirbone;
      public bool TakeDamage;
      public bool Wave;
   }

   [UpdateInGroup(typeof(CharacterSystemGroup))]
   public class CharacterAnimatorEventSystem : JobComponentSystem
   {
      AnimationSystem m_AnimationSystem;

      protected override void OnCreate()
      {
         m_AnimationSystem = World.GetOrCreateSystem<AnimationSystem>();
      }

      protected override JobHandle OnUpdate(JobHandle inputDeps)
      {
         m_AnimationSystem.CommandProducerHandle.Complete();

         var events = m_AnimationSystem.GetEvents();
         var entityLocalToWorld = GetComponentDataFromEntity<LocalToWorld>(true);

         var eventName = new StringHash("Wave");

         var handle = Job
         .WithReadOnly(entityLocalToWorld)
         .WithReadOnly(events)
         .WithCode(() =>
         {
            foreach (var evt in events.GetValuesForKey(eventName))
            {
               var position = math.mul(entityLocalToWorld[evt.Entity].Value, new float4(0, 0, 0, 1f)).xyz;
               Debug.Log("Hi");
            }
         }).Schedule(inputDeps);


         m_AnimationSystem.AddJobHandleForEventListner(handle);
         return handle;
      }
   }

   public class CharacterAnimatorSystem : JobComponentSystem
   {
      bool m_Wave;
      bool m_TakeDamage;
      float m_Speed;
      bool m_IsAirbone;

      protected override void OnCreate()
      {
         if (CharacterAnimatorMenu.Singleton == null)
         {
            Enabled = false;
            return;
         }   

         CharacterAnimatorMenu.Singleton.WaveButton.onClick.AddListener(() => { m_Wave = true; });
         CharacterAnimatorMenu.Singleton.TakeDamageButton.onClick.AddListener(() => { m_TakeDamage = true; });
         CharacterAnimatorMenu.Singleton.SpeedSlider.onValueChanged.AddListener((value) => { m_Speed = value * 2.5f; });
         CharacterAnimatorMenu.Singleton.IsAirboneToggle.onValueChanged.AddListener((value) => { m_IsAirbone = value; });
      }

      protected override JobHandle OnUpdate(JobHandle inputDeps)
      {
         bool wave = m_Wave;
         bool takeDamage = m_TakeDamage;
         float speed = m_Speed;
         bool airbone = m_IsAirbone;

         m_Wave = false;
         m_TakeDamage = false;

         return Entities.ForEach((ref CharacterAnimatorParameters parameters) =>
         {
            parameters.Wave |= wave;
            parameters.TakeDamage |= takeDamage;
            parameters.Speed = speed;
            parameters.IsAirbone = airbone;
         }).Schedule(inputDeps);
      }
   }
}

