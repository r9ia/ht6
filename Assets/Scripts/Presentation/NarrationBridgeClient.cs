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
        [Min(1)] public int TimeoutSeconds = 3;
        [Min(0f)] public float RepeatCooldownSeconds = 8f;
        public BiometricDebugHud DebugHud;

        [Header("Monster voice")]
        [Range(0f, 1f)] public float VoiceVolume = 0.75f;
        [Min(0.1f)] public float MinimumDistance = 1.5f;
        [Min(0.1f)] public float MaximumDistance = 16f;

        private readonly Dictionary<string, float> nextAllowedTimes = new();
        private readonly Dictionary<string, int> lastOfflineLineIndices = new();
        private AudioSource voiceSource;
        private bool loggedUnavailable;
        private int requestGeneration;

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

        private static bool IsAllowed(string eventLabel)
        {
            return eventLabel != null && OfflineLines.ContainsKey(eventLabel);
        }

        private static bool IsLoopbackAudioUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
        }
    }
}
