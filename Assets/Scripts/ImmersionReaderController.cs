using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WebSocketSharp;

namespace GreenHour.Electonics
{
    public class ImmersionReaderController : MonoBehaviour
    {
        private bool enableImmersionReader = true;

        private WebSocket ws;
        private CancellationTokenSource reconnectTokenSource;
        private readonly object wsLock = new object();
        private bool wasConnected = false;
        private SensorData currentReadings;
        private void Start()
        {
            enableImmersionReader = GameSettings.GameSettings.CurrentSettings.enableImmersionReader;
            ToggleImmersionReader(enableImmersionReader);
        }

        private void OnDestroy()
        {
            reconnectTokenSource?.Cancel();
            lock (wsLock)
            {
                if (ws != null && ws.IsAlive)
                    ws.Close();
            }
        }

        public void ToggleImmersionReader(bool newState)
        {
            enableImmersionReader = newState;
            GameSettings.GameSettings.CurrentSettings.enableImmersionReader = newState;

            if (enableImmersionReader)
            {
                Debug.Log("ImmersionReader enabled – starting connection loop.");
                reconnectTokenSource = new CancellationTokenSource();
                Task.Run(() => ConnectLoop(reconnectTokenSource.Token));
            }
            else
            {
                Debug.Log("ImmersionReader disabled – closing connection.");
                reconnectTokenSource?.Cancel();
                lock (wsLock)
                {
                    if (ws != null && ws.IsAlive)
                        ws.Close();
                }
            }
        }

        private void ConnectLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                lock (wsLock)
                {
                    if (ws == null || !ws.IsAlive)
                    {
                        try
                        {
                            ws = new WebSocket("ws://ImmersionReader.local:81");
                            ws.OnMessage += OnMessageReceived;

                            ws.OnOpen += (s, e) =>
                            {
                                wasConnected = true;
                                Debug.Log("Connected to ImmersionReader");
                            };

                            ws.OnClose += (s, e) =>
                            {
                                if (wasConnected)
                                    Debug.LogWarning("ImmersionReader connection closed");
                                wasConnected = false;
                            };

                            ws.OnError += (s, e) =>
                            {
                                if (wasConnected)
                                    Debug.LogWarning($"ImmersionReader error: {e.Message}");
                            };

                            ws.Connect();
                        }
                        catch (Exception ex)
                        {
                            if (wasConnected)
                                Debug.LogWarning($"ImmersionReader connection failed: {ex.Message}");
                            wasConnected = false;
                        }
                    }
                }

                Thread.Sleep(wasConnected ? 10000 : 20000);
            }
        }

        private void OnMessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                currentReadings = JsonUtility.FromJson<SensorData>(e.Data);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to parse sensor data: {ex.Message}");
            }
        }

        public SensorData GetReadings()
        {
            return currentReadings;
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
