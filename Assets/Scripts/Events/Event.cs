using System.Collections.Generic;
using UnityEngine;

namespace GreenHour.Gameplay.Events
{
    [CreateAssetMenu(fileName = "Event", menuName = "Scriptable Objects/Event")]
    public class GameEvent : ScriptableObject
    {
        private List<EventListener> listeners = new List<EventListener>();
        public void Raise()
        {
            foreach (var listener in listeners)
            {
                listener.Invoke();
            }
        }
        public void RegisterListener(EventListener listener)
        {
            listeners.Add(listener);
        }
        public void UnRegisterListener(EventListener listener)
        {
            listeners.Remove(listener);
        }
    }
}