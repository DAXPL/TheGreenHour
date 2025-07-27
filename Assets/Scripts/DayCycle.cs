using UnityEngine;
using UnityEngine.Events;

namespace GreenHour.Enviroment
{
    public class DayCycle : MonoBehaviour
    {
        public static DayCycle Instance { get; private set; }

        public enum DayPhase
        {
            Dawn,
            Day,
            Dusk,
            Night
        }
        [SerializeField] private GameObject SunPivot;
        [SerializeField] private float timescale = 1f;
        [SerializeField] private float maxAngle = 185f;
        [SerializeField] private float dayDuration = 60f;
        [SerializeField] private AnimationCurve sunAngleCurve;
        [Header("Events")]
        public UnityEvent OnDayStart;
        public UnityEvent OnDayEnd;
        [Header("Day Phase Events")]
        public UnityEvent OnNightStart;
        public UnityEvent OnDawnStart;
        public UnityEvent OnDayPhaseStart;
        public UnityEvent OnDuskStart;
        private DayPhase currentPhase = DayPhase.Dawn;

        private float time = 0f;
        private bool timeIsAbleToFlow = true;

        private void Awake()
        {
            if(Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
            }
        }

        void Update()
        {
            if(timeIsAbleToFlow == false) return;

            time += (Time.deltaTime / dayDuration) * timescale;

            if (time >= 1f)
            {
                OnDayEnd?.Invoke();
                timeIsAbleToFlow = false;
            }

            if (SunPivot)
            {
                float sunAngle = sunAngleCurve.Evaluate(time)* maxAngle;
                SunPivot.transform.localRotation = Quaternion.Euler(sunAngle, 0f, 0f);
            }
        }

        void UpdateDayPhase()
        {
            DayPhase newPhase = GetCurrentPhase();
            if (newPhase != currentPhase)
            {
                currentPhase = newPhase;
                switch (currentPhase)
                {
                    case DayPhase.Dawn:
                        OnDawnStart?.Invoke();
                        break;
                    case DayPhase.Day:
                        OnDayPhaseStart?.Invoke();
                        break;
                    case DayPhase.Dusk:
                        OnDuskStart?.Invoke();
                        break;
                    case DayPhase.Night:
                        OnNightStart?.Invoke();
                        break;
                }
            }
        }

        public DayPhase GetPhase(float t)
        {
            if (t < 0.3f) return DayPhase.Dawn;
            else if (t < 0.75f) return DayPhase.Day;
            else if (t < 0.9f) return DayPhase.Dusk;
            else return DayPhase.Night;
        }
        public DayPhase GetCurrentPhase()
        {
            return GetPhase(time);
        }

        [ContextMenu("Restet Day")]
        public void ResetDayCycle()
        {
            time = 0f;
            OnDayStart?.Invoke();
            timeIsAbleToFlow = true;
        }

        public float GetTime()
        {
            return time;
        }
    }
}

