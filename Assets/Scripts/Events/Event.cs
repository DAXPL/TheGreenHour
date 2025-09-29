using System.Collections.Generic;
using UnityEngine;

namespace GreenHour.Gameplay.Events
{
    [CreateAssetMenu(fileName = "Event", menuName = "Scriptable Objects/Event")]
    public class GameEvent : ScriptableObject
    {
        private List<EventListener> listeners = new List<EventListener>();
        private string payload;
        public void Raise()
        {
            foreach (var listener in listeners)
            {
                listener.Invoke();
            }
            payload = null;
        }
        public void Raise(string val)
        {
            payload = val;
            Raise();
        }
        public void RegisterListener(EventListener listener)
        {
            if (!listeners.Contains(listener)) listeners.Add(listener);
        }
        public void UnRegisterListener(EventListener listener)
        {
            listeners.Remove(listener);
        }
        public string GetPayload() 
        {
            return payload;
        }
    }
}