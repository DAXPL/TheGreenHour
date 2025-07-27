using GreenHour.Enviroment;
using UnityEngine;
namespace GreenHour.Interactions
{
    [RequireComponent(typeof(Interactor))]
    public class Bed : MonoBehaviour
    {
        public void OnUse()
        {
            DayCycle dayCycle = DayCycle.Instance;
            if (dayCycle == null) return;
            DayCycle.DayPhase dayPhase = dayCycle.GetCurrentPhase();
            if (dayPhase == DayCycle.DayPhase.Night || dayPhase == DayCycle.DayPhase.Dusk)
            {
                dayCycle.ResetDayCycle();
            }
            else
            {
                Debug.Log($"You can only sleep at night or during dusk.\nCurrently is {dayPhase}");
            }
        }
    }
}
