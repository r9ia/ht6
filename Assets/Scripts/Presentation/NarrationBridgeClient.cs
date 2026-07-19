using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DreadDirector.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace DreadDirector.Presentation
{
    /// <summary>
    /// Optional localhost monster-voice client. It sends generic event labels only—never
    /// state values or biometrics—and keeps a randomized offline subtitle fallback.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class NarrationBridgeClient : MonoBehaviour
    {
        [Serializable]
        private sealed class NarrationRequest
        {
            public string @event;
        }

        [Serializable]
        private sealed class NarrationResponse
        {
            public string text;
            public string source;
            public string audioUrl;
        }

        private static readonly Dictionary<string, string[]> OfflineLines = new()
        {
            ["calibration_complete"] = new[]
            {
                "Calibration complete. Try not to let the room learn you too quickly.",
                "This part is quiet. Enjoy it.",
                "The room knows your ordinary now. It will not stay ordinary for long."
            },
            ["escalation_low"] = new[]
            {
                "Something in the room has shifted. Keep watching the corners.",
                "Careful. Something changes every time you blink.",
                "There—did you see it too, or just feel it?",
                "Every blink is half a second it gets closer."
            },
            ["escalation_high"] = new[]
            {
                "It knows where you are. Do not look away.",
                "Your heart is giving you away.",
                "Something in here can hear that pulse.",
                "There it is. That is the fear it was waiting for."
            },
            ["panic_backoff"] = new[]
            {
                "Easy now. The room is giving you one breath.",
                "It heard you. Now it is waiting.",
                "The footsteps stopped. That does not mean it left."
            },
            ["recovery"] = new[]
            {
                "You settle. Somewhere in the dark, it starts waiting again.",
                "It has gone still, but it has not gone away.",
                "The room is patient. It can wait longer than you can."
            }
        };

        [Header("Local bridge")]
        public string Endpoint = "http://127.0.0.1:8787/v1/narration";
        public string ConversationEndpoint = "http://127.0.0.1:8787/v1/conversation";
        [Min(1)] public int TimeoutSeconds = 3;
        [Min(1)] public int ConversationTimeoutSeconds = 20;
        [Min(0f)] public float RepeatCooldownSeconds = 8f;
        public BiometricDebugHud DebugHud;

        [Header("Opt-in microphone conversation")]
        public bool EnableMicrophoneConversation = true;
        [Range(1, 10)] public int ConversationSeconds = 5;
        [Range(8000, 48000)] public int ConversationSampleRate = 16000;

        [Header("Monster voice")]
        [Range(0f, 1f)] public float VoiceVolume = 0.75f;
        [Min(0.1f)] public float MinimumDistance = 1.5f;
        [Min(0.1f)] public float MaximumDistance = 16f;

        private readonly Dictionary<string, float> nextAllowedTimes = new();
        private readonly Dictionary<string, int> lastOfflineLineIndices = new();
        private AudioSource voiceSource;
        private bool loggedUnavailable;
        private bool conversationInProgress;
        private string activeMicrophoneDevice;
        private int requestGeneration;

        /// <summary>True while a push-to-talk clip is being captured, so other mic users
        /// (the continuous loudness listener) can yield the single microphone device.</summary>
        public bool ConversationInProgress => conversationInProgress;

        private void Awake()
        {
            voiceSource = GetComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
            voiceSource.spatialBlend = 1f;
            voiceSource.dopplerLevel = 0f;
            voiceSource.rolloffMode = AudioRolloffMode.Linear;
            voiceSource.minDistance = MinimumDistance;
            voiceSource.maxDistance = Mathf.Max(MinimumDistance, MaximumDistance);
        }

        private void Update()
        {
            if (!EnableMicrophoneConversation || conversationInProgress)
            {
                return;
            }

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.vKey.wasPressedThisFrame)
            {
                StartCoroutine(CaptureConversation());
            }
        }

        private void OnDisable()
        {
            if (conversationInProgress)
            {
                Microphone.End(activeMicrophoneDevice);
                conversationInProgress = false;
            }
        }

        public void Announce(string eventLabel)
        {
            if (!IsAllowed(eventLabel))
            {
                Debug.LogWarning($"[Dread Director] Monster voice rejected unsupported event '{eventLabel}'.", this);
                return;
            }

            var now = Time.unscaledTime;
            if (nextAllowedTimes.TryGetValue(eventLabel, out var nextAllowed) && now < nextAllowed)
            {
                return;
            }

            nextAllowedTimes[eventLabel] = now + RepeatCooldownSeconds;
            requestGeneration++;
            voiceSource?.Stop();
            StartCoroutine(RequestNarration(eventLabel, requestGeneration));
        }

        private IEnumerator CaptureConversation()
        {
            conversationInProgress = true;
            if (!IsLoopbackEndpoint(ConversationEndpoint))
            {
                Debug.LogWarning("[Dread Director] Microphone conversation endpoint must be loopback.", this);
                conversationInProgress = false;
                yield break;
            }

            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            }
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone) || Microphone.devices.Length == 0)
            {
                ShowLine("The dark cannot hear you. No microphone is available.", "microphone-local");
                conversationInProgress = false;
                yield break;
            }

            activeMicrophoneDevice = null;
            var clip = Microphone.Start(activeMicrophoneDevice, false, ConversationSeconds, ConversationSampleRate);
            if (clip == null)
            {
                ShowLine("The dark cannot hear you.", "microphone-local");
                conversationInProgress = false;
                yield break;
            }

            DebugHud?.ShowEvent($"MICROPHONE LISTENING // {ConversationSeconds}s // [V]");
            var startDeadline = Time.realtimeSinceStartup + 1f;
            while (Microphone.GetPosition(activeMicrophoneDevice) <= 0 && Time.realtimeSinceStartup < startDeadline)
            {
                yield return null;
            }
            if (Microphone.GetPosition(activeMicrophoneDevice) <= 0)
            {
                Microphone.End(activeMicrophoneDevice);
                Destroy(clip);
                ShowLine("The microphone did not start.", "microphone-local");
                conversationInProgress = false;
                yield break;
            }

            yield return new WaitForSecondsRealtime(ConversationSeconds);
            var capturedFrames = Microphone.GetPosition(activeMicrophoneDevice);
            Microphone.End(activeMicrophoneDevice);
            if (capturedFrames <= 0)
            {
                capturedFrames = clip.samples;
            }

            var samples = new float[capturedFrames * clip.channels];
            clip.GetData(samples, 0);
            var wav = EncodePcm16Wav(samples, clip.channels, clip.frequency);
            Destroy(clip);

            requestGeneration++;
            var generation = requestGeneration;
            voiceSource?.Stop();
            using var request = new UnityWebRequest(ConversationEndpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(wav),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = ConversationTimeoutSeconds
            };
            request.SetRequestHeader("Content-Type", "audio/wav");
            yield return request.SendWebRequest();
            conversationInProgress = false;

            if (generation != requestGeneration)
            {
                yield break;
            }
            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowLine("The dark heard you, but did not answer.", "microphone-fallback");
                Debug.LogWarning($"[Dread Director] Microphone conversation failed: HTTP {request.responseCode}.", this);
                yield break;
            }

            var response = JsonUtility.FromJson<NarrationResponse>(request.downloadHandler.text);
            if (response == null || string.IsNullOrWhiteSpace(response.text))
            {
                ShowLine("The dark heard you, but did not answer.", "microphone-fallback");
                yield break;
            }

            ShowLine(response.text, response.source);
            if (IsLoopbackAudioUrl(response.audioUrl))
            {
                yield return PlayAudio(response.audioUrl, generation);
            }
        }

        private IEnumerator RequestNarration(string eventLabel, int generation)
        {
            var requestBody = JsonUtility.ToJson(new NarrationRequest { @event = eventLabel });
            using var request = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestBody)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds
            };
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            if (generation != requestGeneration)
            {
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowFallback(eventLabel);
                if (!loggedUnavailable)
                {
                    loggedUnavailable = true;
                    Debug.Log("[Dread Director] Local monster-voice bridge unavailable; using offline subtitles.", this);
                }
                yield break;
            }

            var response = JsonUtility.FromJson<NarrationResponse>(request.downloadHandler.text);
            if (response == null || string.IsNullOrWhiteSpace(response.text))
            {
                ShowFallback(eventLabel);
                yield break;
            }

            ShowLine(response.text, response.source);
            loggedUnavailable = false;
            if (IsLoopbackAudioUrl(response.audioUrl))
            {
                yield return PlayAudio(response.audioUrl, generation);
            }
        }

        private IEnumerator PlayAudio(string audioUrl, int generation)
        {
            using var request = UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.MPEG);
            request.timeout = TimeoutSeconds;
            yield return request.SendWebRequest();
            if (generation != requestGeneration || request.result != UnityWebRequest.Result.Success || voiceSource == null)
            {
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(request);
            voiceSource.PlayOneShot(clip, VoiceVolume);
        }

        private void ShowFallback(string eventLabel)
        {
            ShowLine(PickOfflineLine(eventLabel), "offline-local");
        }

        private void ShowLine(string line, string source)
        {
            DebugHud?.ShowEvent($"MONSTER: {line}");
            Debug.Log($"[Dread Director] Monster voice ({source}): {line}", this);
        }

        private string PickOfflineLine(string eventLabel)
        {
            var pool = OfflineLines[eventLabel];
            var index = UnityEngine.Random.Range(0, pool.Length);
            if (pool.Length > 1 && lastOfflineLineIndices.TryGetValue(eventLabel, out var previous) && index == previous)
            {
                index = (index + 1) % pool.Length;
            }

            lastOfflineLineIndices[eventLabel] = index;
            return pool[index];
        }

        private static byte[] EncodePcm16Wav(float[] samples, int channels, int sampleRate)
        {
            const int headerSize = 44;
            var dataLength = samples.Length * sizeof(short);
            var output = new byte[headerSize + dataLength];
            WriteAscii(output, 0, "RIFF");
            WriteInt32(output, 4, 36 + dataLength);
            WriteAscii(output, 8, "WAVE");
            WriteAscii(output, 12, "fmt ");
            WriteInt32(output, 16, 16);
            WriteInt16(output, 20, 1);
            WriteInt16(output, 22, (short)channels);
            WriteInt32(output, 24, sampleRate);
            WriteInt32(output, 28, sampleRate * channels * sizeof(short));
            WriteInt16(output, 32, (short)(channels * sizeof(short)));
            WriteInt16(output, 34, 16);
            WriteAscii(output, 36, "data");
            WriteInt32(output, 40, dataLength);
            for (var i = 0; i < samples.Length; i++)
            {
                WriteInt16(output, headerSize + i * sizeof(short), (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
            }
            return output;
        }

        private static void WriteAscii(byte[] output, int offset, string value)
        {
            Encoding.ASCII.GetBytes(value, 0, value.Length, output, offset);
        }

        private static void WriteInt16(byte[] output, int offset, short value)
        {
            output[offset] = (byte)value;
            output[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteInt32(byte[] output, int offset, int value)
        {
            output[offset] = (byte)value;
            output[offset + 1] = (byte)(value >> 8);
            output[offset + 2] = (byte)(value >> 16);
            output[offset + 3] = (byte)(value >> 24);
        }

        private static bool IsAllowed(string eventLabel)
        {
            return eventLabel != null && OfflineLines.ContainsKey(eventLabel);
        }

        private static bool IsLoopbackEndpoint(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && uri.IsLoopback;
        }

        private static bool IsLoopbackAudioUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
        }
    }
}
