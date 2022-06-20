using UnityEngine;
using UnityEngine.UI;

namespace CrowdMorph.CharacterSample
{
   public class CharacterAnimatorMenu : MonoBehaviour
   {
      static CharacterAnimatorMenu s_Singleton;
      public static CharacterAnimatorMenu Singleton
      {
         get
         {
            if (s_Singleton == null)
               s_Singleton = FindObjectOfType<CharacterAnimatorMenu>();

            return s_Singleton;
         }
      }

      [SerializeField]
      Button m_WaveButton;
      public Button WaveButton => m_WaveButton;

      [SerializeField]
      Button m_TakeDamageButton;
      public Button TakeDamageButton => m_TakeDamageButton;

      [SerializeField]
      Toggle m_IsAirboneToggle;
      public Toggle IsAirboneToggle => m_IsAirboneToggle;

      [SerializeField]
      Slider m_SpeedSlider;
      public Slider SpeedSlider => m_SpeedSlider;
   }
}

