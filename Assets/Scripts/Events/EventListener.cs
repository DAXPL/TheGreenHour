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
            if(action)action.RegisterListener(this);
        }
        private void OnDisable()
        {
            if (action) action.UnRegisterListener(this);
        }
        public void Invoke()
        {
            response.Invoke();
        }
    }
}