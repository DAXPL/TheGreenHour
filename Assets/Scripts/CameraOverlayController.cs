using GreenHour.Enviroment;
using GreenHour.Immersion;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GreenHour.UI
{
    public class CameraOverlayController : MonoBehaviour
    {
        [Header("Gameplay UI")]
        [SerializeField] private TextMeshProUGUI dateTMP;
        [SerializeField] private TextMeshProUGUI resolutionTMP;
        [SerializeField] private TextMeshProUGUI infoTMP;
        [SerializeField] private TextMeshProUGUI statsTMP;
        [SerializeField] private TextMeshProUGUI fastForwardTMP;
        [Space]
        [SerializeField] private Image batteryLevelImage;
        [SerializeField] private Sprite[] batteryLevels;
        [Space]
        [SerializeField] private UnityEvent onFastForward;
        private bool wasFastForward;

        float time = 0;
        private float fpsTimer = 0f;
        private int frames = 0;
        private int curFPS = 0;
        private int currentBatteryLevel = 0;

        private void Start()
        {
            if (fastForwardTMP) fastForwardTMP.gameObject.SetActive(false);
            if (dateTMP) dateTMP.SetText($"{System.DateTime.Now}");
            if (resolutionTMP) resolutionTMP.SetText($"{Screen.height}P");
            if (infoTMP) infoTMP.SetText("");
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
            if (dateTMP && DayCycle.Instance != null)
            {
                dateTMP.SetText(DayCycle.Instance != null ? $"{DayCycle.Instance.GetInGameDate()}\n{DayCycle.Instance.GetInGameTime()}" : "");
            }
            
            if(resolutionTMP)resolutionTMP.SetText($"{Screen.height}P");

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

            if (batteryLevelImage != null && batteryLevels.Length > 0)
            {
                int bLevel = BatteryLevel(batteryLevels.Length);
                if (bLevel != currentBatteryLevel && bLevel<batteryLevels.Length)
                {
                    currentBatteryLevel = bLevel;
                    batteryLevelImage.sprite = batteryLevels[bLevel];
                }
            }
        }

        private int BatteryLevel(int levels)
        {
            if (DayCycle.Instance == null) return 0;

            float t = DayCycle.Instance.GetTime();

            if (t >= 0.9f)
                return 0;

            float normalized = t / 0.9f;
            float value = Mathf.Lerp(levels, 1, normalized);

            return Mathf.Clamp(Mathf.RoundToInt(value), 1, levels);
        }

    }
}