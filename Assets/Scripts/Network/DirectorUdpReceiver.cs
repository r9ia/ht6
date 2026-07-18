using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using DreadDirector.Director;
using UnityEngine;

namespace DreadDirector.Network
{
    /// <summary>Receives high-level Director JSON on a local UDP port and forwards it on Unity's main thread.</summary>
    public sealed class DirectorUdpReceiver : MonoBehaviour
    {
        [Min(1)] public int Port = 7777;
        public DirectorGameBridge Bridge;
        [Min(1)] public int MaxMessagesPerFrame = 16;

        private readonly Queue<string> pendingPayloads = new Queue<string>();
        private readonly object payloadLock = new object();
        private readonly object socketLock = new object();
        private Thread receiveThread;
        private UdpClient udpClient;
        private volatile bool isReceiving;

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

                if (DirectorMessage.TryParse(payload, out var message, out var error))
                {
                    if (Bridge != null)
                    {
                        Bridge.ReceiveMessage(message, "UDP");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Dread Director] Ignored UDP message: {error}", this);
                }
            }
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
                    Name = "DreadDirectorUdpReceiver"
                };
                receiveThread.Start();
                Debug.Log($"[Dread Director] Listening for Director JSON on UDP {Port}.", this);
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
