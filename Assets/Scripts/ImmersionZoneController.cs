using UnityEngine;
using WebSocketSharp;

namespace GreenHour.Electonics
{
    public class ImmersionZoneController : MonoBehaviour
    {
        public static ImmersionZoneController Instance;
        private WebSocket ws;
        public enum Smells { Forrest, Flowers, Other}
        private void Awake()
        {
            if (Instance == null) 
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
            }
        }

        void Start()
        {
            ws = new WebSocket("ws://ImmersionGiver.local:81");
            ws.Connect();
        }

        void OnDestroy()
        {
            if (ws != null && ws.IsAlive)
                ws.Close();
        }

        public void SendData(Smells smell, float durationSeconds=3)
        {
            if (ws == null || !ws.IsAlive)
            {
                Debug.LogWarning("Connection is closed! Can't send data");
                return;
            }

            string message = $"{smell}:{durationSeconds}";
            ws.Send(message);
        }
    }
}