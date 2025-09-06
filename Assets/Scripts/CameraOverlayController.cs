using GreenHour.Enviroment;
using GreenHour.Immersion;
using TMPro;
using UnityEngine;

namespace GreenHour.UI
{
    public class CameraOverlayController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI dateTMP;
        [SerializeField] private TextMeshProUGUI resolutionTMP;
        [SerializeField] private TextMeshProUGUI timeTMP;
        [SerializeField] private TextMeshProUGUI statsTMP;
        [SerializeField] private TextMeshProUGUI fastForwardTMP;
        float time = 0;
        private float fpsTimer = 0f;
        private int frames = 0;
        private int curFPS = 0;

        private void Start()
        {
            if (fastForwardTMP) fastForwardTMP.gameObject.SetActive(false);
        }

        private void Update()
        {
            time += Time.deltaTime;
            fpsTimer += Time.deltaTime;
            frames++;

            if (fpsTimer >= 1f)
            {
                curFPS = (int)(frames / fpsTimer);
                fpsTimer = 0f;
                frames = 0;
            }
        }
        
        private void FixedUpdate()
        {
            if(dateTMP)dateTMP.SetText($"{System.DateTime.Now}");
            
            if(resolutionTMP)resolutionTMP.SetText($"{Screen.height}P");
            
            if (timeTMP)
            {
                float gameTime = (DayCycle.Instance != null) ? DayCycle.Instance.GetInGameTime() : 0f;
                float totalHours = 6f + gameTime * 16f;

                int hours = Mathf.FloorToInt(totalHours);
                int minutes = Mathf.FloorToInt((totalHours - hours) * 60f);

                timeTMP.SetText($"{hours:00}:{minutes:00}");
            }
            
            if (statsTMP)
            {
                string db = "?? ";
                string hb = "?? ";
                if (ImmersionController.immersionData != null)
                {
                    db = $"{ImmersionController.immersionData.RMS}";
                    if(ImmersionController.immersionData.sensorData != null)
                        hb = $"{ImmersionController.immersionData.sensorData.HR}";
                }
                statsTMP.SetText($"{db}dB\n{hb}HB\n{curFPS}FPS");
            }
            
            if (DayCycle.Instance != null) 
            {
                if(fastForwardTMP)fastForwardTMP.gameObject.SetActive(DayCycle.Instance.IsFastForward());
            }
        }

    }
}