using UnityEngine;
using UnityEngine.Events;

namespace GreenHour.Gameplay.Events
{
    public class EventListener : MonoBehaviour
    {
        public GameEvent action;
        public UnityEvent response;
        private void OnEnable()
        {
            action.RegisterListener(this);
        }
        private void OnDisable()
        {
            action.UnRegisterListener(this);
        }
        public void Invoke()
        {
            response.Invoke();
        }
    }
}