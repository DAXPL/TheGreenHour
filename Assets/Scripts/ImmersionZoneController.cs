using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WebSocketSharp;

namespace GreenHour.Electonics
{
    public class ImmersionZoneController : MonoBehaviour
    {
        public static ImmersionZoneController Instance;
        private bool enableImmersionGiver = true;

        private WebSocket ws;
        private CancellationTokenSource reconnectTokenSource;
        private readonly object wsLock = new object();

        public enum Smells { Forrest, Flowers, Other }

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(this);
        }

        private void Start()
        {
            enableImmersionGiver = GameSettings.GameSettings.CurrentSettings.enableImmersionGiver;
            ToggleImmersionGiver(enableImmersionGiver);
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

        public void ToggleImmersionGiver(bool newState)
        {
            enableImmersionGiver = newState;
            GameSettings.GameSettings.CurrentSettings.enableImmersionGiver = newState;

            if (enableImmersionGiver)
            {
                Debug.Log("ImmersionGiver enabled – starting connection loop.");
                reconnectTokenSource = new CancellationTokenSource();
                Task.Run(() => ConnectLoop(reconnectTokenSource.Token));
            }
            else
            {
                Debug.Log("ImmersionGiver disabled – closing connection.");
                reconnectTokenSource?.Cancel();
                lock (wsLock)
                {
                    if (ws != null && ws.IsAlive)
                        ws.Close();
                }
            }
        }

        public void SendData(Smells smell, float durationSeconds = 3)
        {
            if (!enableImmersionGiver)
                return;

            string message = $"{smell}:{durationSeconds}";
            Task.Run(() =>
            {
                lock (wsLock)
                {
                    if (ws != null && ws.IsAlive)
                    {
                        try
                        {
                            ws.Send(message);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"Send failed: {ex.Message}");
                        }
                    }
                }
            });
        }

        private void ConnectLoop(CancellationToken token)
        {
            bool wasConnected = false;

            while (!token.IsCancellationRequested)
            {
                lock (wsLock)
                {
                    if (ws == null || !ws.IsAlive)
                    {
                        try
                        {
                            ws = new WebSocket("ws://ImmersionGiver.local:81");

                            ws.OnOpen += (s, e) =>
                            {
                                wasConnected = true;
                                Debug.Log("Connected to ImmersionGiver");
                            };

                            ws.OnClose += (s, e) =>
                            {
                                if (wasConnected)
                                    Debug.LogWarning("Connection closed");
                                wasConnected = false;
                            };

                            ws.OnError += (s, e) =>
                            {
                                if (wasConnected)
                                    Debug.LogWarning($"WebSocket error: {e.Message}");
                            };

                            ws.Connect();
                        }
                        catch (Exception ex)
                        {
                            if (wasConnected)
                                Debug.LogWarning($"Connection failed: {ex.Message}");
                            wasConnected = false;
                        }
                    }
                }

                Thread.Sleep(wasConnected ? 10000 : 20000);
            }
        }
    }
}
