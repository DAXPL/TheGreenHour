using UnityEngine;
using WebSocketSharp;

namespace GreenHour.Electonics
{
    public class ImmersionReaderController : MonoBehaviour
    {
        private WebSocket ws;
        void Start()
        {
            ws = new WebSocket("ws://ImmersionReader.local:81");
            ws.OnMessage += OnMessageReceived;
            ws.Connect();
        }

        void OnMessageReceived(object sender, MessageEventArgs e)
        {
            SensorData data = JsonUtility.FromJson<SensorData>(e.Data);
            Debug.Log($"Temperatura: {data.Temp}°C, GSR: {data.GSR}, HR: {data.HR}, SPO2: {data.SPO}");
        }

        void OnDestroy()
        {
            if (ws != null && ws.IsAlive)
                ws.Close();
        }
    }
    [System.Serializable]
    public class SensorData
    {
        public float Temp;
        public int GSR;
        public int HR;
        public int SPO;
    }
}