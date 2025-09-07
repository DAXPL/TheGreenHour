using GreenHour.Enviroment;
using GreenHour.Immersion;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace GreenHour.UI
{
    public class CameraOverlayController : MonoBehaviour
    {
        [Header("Gameplay UI")]
        [SerializeField] private TextMeshProUGUI dateTMP;
        [SerializeField] private TextMeshProUGUI resolutionTMP;
        [SerializeField] private TextMeshProUGUI timeTMP;
        [SerializeField] private TextMeshProUGUI statsTMP;
        [SerializeField] private TextMeshProUGUI fastForwardTMP;
        [Space]
        [SerializeField] private UnityEvent onFastForward;
        private bool wasFastForward;

        float time = 0;
        private float fpsTimer = 0f;
        private int frames = 0;
        private int curFPS = 0;

        private void Start()
        {
            if (fastForwardTMP) fastForwardTMP.gameObject.SetActive(false);
            if (dateTMP) dateTMP.SetText($"{System.DateTime.Now}");
            if (resolutionTMP) resolutionTMP.SetText($"{Screen.height}P");
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
            if(dateTMP && DayCycle.Instance!=null)dateTMP.SetText(DayCycle.Instance.GetInGameDate());
            
            if(resolutionTMP)resolutionTMP.SetText($"{Screen.height}P");
            
            if (timeTMP)
            {
                timeTMP.SetText(DayCycle.Instance != null ? DayCycle.Instance.GetInGameTime():"");
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
                bool ff = DayCycle.Instance.IsFastForward();
                if (ff && !wasFastForward) 
                { 
                    onFastForward.Invoke();
                    if (fastForwardTMP) fastForwardTMP.gameObject.SetActive(true);
                }
                if (!ff && wasFastForward)
                {
                    if (fastForwardTMP) fastForwardTMP.gameObject.SetActive(false);
                }
                wasFastForward = ff;
            }
        }

    }
}