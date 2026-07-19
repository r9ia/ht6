using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using DreadDirector.Player;
using UnityEngine;

namespace DreadDirector.Network
{
    /// <summary>
    /// Listens on a local UDP port for a swing trigger sent by a handheld device on the same
    /// WiFi network (e.g. an ESP32/Arduino with a button or motion sensor). Any packet whose
    /// text is "swing" (case-insensitive), or contains a "swing" JSON field/value, triggers the
    /// player's knife swing. Mirrors <see cref="DirectorUdpReceiver"/>'s threading pattern, but
    /// is a separate, simpler channel: no biometric data, just a fire-and-forget trigger.
    /// </summary>
    public sealed class KnifeSwingUdpReceiver : MonoBehaviour
    {
        [Min(1)] public int Port = 7778;
        [Tooltip("Auto-found if empty.")]
        public HeldKnife Knife;
        [Min(1)] public int MaxMessagesPerFrame = 8;

        private readonly Queue<string> pendingPayloads = new Queue<string>();
        private readonly object payloadLock = new object();
        private readonly object socketLock = new object();
        private Thread receiveThread;
        private UdpClient udpClient;
        private volatile bool isReceiving;

        private void Awake()
        {
            if (Knife == null)
            {
                Knife = FindAnyObjectByType<HeldKnife>();
            }
        }

        private void OnEnable()
        {
            StartReceiver();
        }

        private void OnDisable()
        {
            StopReceiver();
        }

        private void Update()
        {
            for (var i = 0; i < MaxMessagesPerFrame; i++)
            {
                string payload;
                lock (payloadLock)
                {
                    if (pendingPayloads.Count == 0)
                    {
                        return;
                    }

                    payload = pendingPayloads.Dequeue();
                }

                HandlePayload(payload);
            }
        }

        private void HandlePayload(string payload)
        {
            var trimmed = payload?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return;
            }

            // Accept a bare "swing" trigger, or a {"type":"swing"} style JSON envelope for
            // devices that prefer to send structured messages.
            var isSwingTrigger = trimmed.Equals("swing", StringComparison.OrdinalIgnoreCase)
                || trimmed.IndexOf("\"swing\"", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isSwingTrigger)
            {
                Debug.LogWarning($"[Dread Director] Ignored unknown knife-swing UDP payload: {trimmed}", this);
                return;
            }

            if (Knife == null)
            {
                Debug.LogWarning("[Dread Director] Knife-swing trigger received but no HeldKnife found.", this);
                return;
            }

            Knife.TriggerSwing();
        }

        private void StartReceiver()
        {
            if (isReceiving)
            {
                return;
            }

            try
            {
                var client = new UdpClient(Port);
                lock (socketLock)
                {
                    udpClient = client;
                }

                isReceiving = true;
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "KnifeSwingUdpReceiver"
                };
                receiveThread.Start();
                Debug.Log($"[Dread Director] Listening for knife-swing triggers on UDP {Port}.", this);
            }
            catch (SocketException exception)
            {
                Debug.LogWarning($"[Dread Director] Could not listen on UDP {Port}: {exception.Message}", this);
            }
        }

        private void StopReceiver()
        {
            isReceiving = false;
            lock (socketLock)
            {
                udpClient?.Close();
                udpClient = null;
            }

            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Join(250);
            }

            receiveThread = null;
        }

        private void ReceiveLoop()
        {
            var endpoint = new IPEndPoint(IPAddress.Any, 0);
            while (isReceiving)
            {
                try
                {
                    UdpClient client;
                    lock (socketLock)
                    {
                        client = udpClient;
                    }

                    if (client == null)
                    {
                        return;
                    }

                    var bytes = client.Receive(ref endpoint);
                    var payload = Encoding.UTF8.GetString(bytes);
                    lock (payloadLock)
                    {
                        pendingPayloads.Enqueue(payload);
                    }
                }
                catch (SocketException)
                {
                    if (!isReceiving)
                    {
                        return;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception)
                {
                    if (!isReceiving)
                    {
                        return;
                    }
                }
            }
        }
    }
}
