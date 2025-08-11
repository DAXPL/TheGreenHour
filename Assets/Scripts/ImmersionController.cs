using GreenHour.Electonics;
using UnityEngine;

namespace GreenHour.Immersion
{
    [RequireComponent(typeof(ImmersionReaderController))]
    [RequireComponent(typeof(ImmersionZoneController))]
    public class ImmersionController : MonoBehaviour
    {
        private ImmersionReaderController immersionReaderController;
        private ImmersionZoneController immersionZoneController;

        private string selectedMic = "";
        private int sampleWindow = 128;
        private AudioClip micClip;

        private float volume;
        private SensorData sensorsReadings;
        private void Awake()
        {
            immersionReaderController = GetComponent<ImmersionReaderController>();
            immersionZoneController = GetComponent<ImmersionZoneController>();
        }
        
        void Start()
        {
            if (Microphone.devices.Length <= 0) return;
            selectedMic = GameSettings.GameSettings.CurrentSettings.selectedMicrophone;
            foreach (string mic in Microphone.devices) 
            {
                if (selectedMic == mic)
                {
                    micClip = Microphone.Start(selectedMic, true, 1, 44100);
                    return;
                }
            }

            selectedMic = Microphone.devices[3];
            micClip = Microphone.Start(selectedMic, true, 1, 44100);
            GameSettings.GameSettings.CurrentSettings.selectedMicrophone = selectedMic;
        }
        
        private void OnDestroy()
        {
            if (Microphone.IsRecording(selectedMic)) Microphone.End(selectedMic);
        }

        [ContextMenu("Debug - write out microphones")]
        public void DebugWriteOutAllMicrophones()
        {
            string list = $"There is {Microphone.devices.Length} audio inputs in system:";
            foreach (string mic in Microphone.devices) 
            {
                list += $"\n>{mic}";
            }
            Debug.Log(list);
        }

        void Update()
        {
            if (micClip == null)
                return;

            volume = GetRMSLevel();
            if (immersionReaderController) sensorsReadings = immersionReaderController.GetReadings();
        }

        private float GetRMSLevel()
        {
            float[] samples = new float[sampleWindow];
            int micPos = Microphone.GetPosition(selectedMic) - sampleWindow;
            if (micPos < 0) return 0;

            micClip.GetData(samples, micPos);

            float sum = 0;
            for (int i = 0; i < sampleWindow; i++)
                sum += samples[i] * samples[i];
            return Mathf.Sqrt(sum / sampleWindow);
        }

    }
}