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
        [SerializeField] private float minAngle = 30f;
        [SerializeField] private float maxAngle = 185f;
        [SerializeField] private float minHour = 7;
        [SerializeField] private float maxHour = 22;
        [SerializeField] private float dayDuration = 60f;

        [SerializeField] private float fastForwardDuration = 3f;
        private float fastForwardRemaining = 0f;
        private float fastForwardTarget = 0f;

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
        private float penalty = 0;
        private bool timeIsAbleToFlow = true;

        private int day = 0;
        private int month = 0;
        private int year = 0;

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

        private void Start()
        {
            day = System.DateTime.Now.Day;
            month = System.DateTime.Now.Month;
            year = System.DateTime.Now.Year;
        }

        void Update()
        {
            if (timeIsAbleToFlow == false) return;

            float baseFlow = (Time.deltaTime / dayDuration) * timescale;

            if (penalty > 0)
            {
                Debug.Log("Fast forward");
                float step = (fastForwardTarget / fastForwardDuration) * Time.deltaTime;

                time += step / dayDuration;
                penalty -= step;
                fastForwardRemaining -= Time.deltaTime;

                if (penalty < 0) penalty = 0;
            }
            else
            {
                time += baseFlow;
            }

            if (time >= 1f)
            {
                OnDayEnd?.Invoke();
                timeIsAbleToFlow = false;
            }

            if (SunPivot)
            {
                float sunAngle = Mathf.Lerp(minAngle, maxAngle, sunAngleCurve.Evaluate(time));
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
        public string GetInGameTime()
        {
            float totalHours = minHour + time * (maxHour-minHour);

            int hours = Mathf.FloorToInt(totalHours);
            int minutes = Mathf.FloorToInt((totalHours - hours) * 60f);
            return ($"{hours:00}:{minutes:00}");
        }

        public string GetInGameDate()
        {
            return $"{day}:{month}:{year}";
        }

        public void SetTimePenalty(float time)
        {
            penalty += time;
            fastForwardTarget = penalty;
            fastForwardRemaining = fastForwardDuration;
        }
        
        public bool IsFastForward()
        {
            return penalty > 0;
        }
    }
}

