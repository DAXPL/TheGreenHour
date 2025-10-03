using GreenHour.Enviroment;
using GreenHour.Gameplay;
using UnityEngine;
namespace GreenHour.Interactions
{
    [RequireComponent(typeof(Interactor))]
    public class Bed : MonoBehaviour
    {
        public void OnUse()
        {
            DayCycle dayCycle = DayCycle.Instance;
            GameManager manager = GameManager.Instance;
            if (dayCycle == null) return;
            if (manager == null) return;
            DayCycle.DayPhase dayPhase = dayCycle.GetCurrentPhase();
            if (dayPhase == DayCycle.DayPhase.Night || dayPhase == DayCycle.DayPhase.Dusk)
            {
                manager.EndDay(true);
            }
            else
            {
                Debug.Log($"You can only sleep at night or during dusk.\nCurrently is {dayPhase}");
            }
        }
    }
}
