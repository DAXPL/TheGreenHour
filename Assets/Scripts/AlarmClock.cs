using GreenHour.Enviroment;
using System;
using TMPro;
using UnityEngine;
namespace GreenHour.Gameplay
{
    [RequireComponent(typeof(AudioSource))]
    public class AlarmClock : MonoBehaviour
    {
        [SerializeField] private TextMeshPro display;
        private AudioSource audioSource;
        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }
        public void ChangeAlarm(bool state)
        {
            if(state) audioSource.Play();
            else audioSource.Stop();
        }
        private void FixedUpdate()
        {
            if (display)
                display.SetText(DayCycle.Instance != null ? $"{DayCycle.Instance.GetInGameTime()}" : "");
        }
    }
}