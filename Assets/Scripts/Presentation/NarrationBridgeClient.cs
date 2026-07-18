using System;
using System.Collections;
using System.Text;
using DreadDirector.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace DreadDirector.Presentation
{
    /// <summary>Optional localhost narration client. It sends event labels only—never state values or biometrics.</summary>
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

        public string Endpoint = "http://127.0.0.1:8787/v1/narration";
        [Min(1)] public int TimeoutSeconds = 3;
        public BiometricDebugHud DebugHud;

        private AudioSource audioSource;
        private bool loggedUnavailable;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        public void Announce(string eventLabel)
        {
            if (!IsAllowed(eventLabel))
            {
                Debug.LogWarning($"[Dread Director] Narration rejected unsupported event '{eventLabel}'.", this);
                return;
            }

            StartCoroutine(RequestNarration(eventLabel));
        }

        private IEnumerator RequestNarration(string eventLabel)
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

            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowFallback(eventLabel);
                if (!loggedUnavailable)
                {
                    loggedUnavailable = true;
                    Debug.Log("[Dread Director] Local narration bridge unavailable; using offline lines.", this);
                }
                yield break;
            }

            var response = JsonUtility.FromJson<NarrationResponse>(request.downloadHandler.text);
            if (response == null || string.IsNullOrWhiteSpace(response.text))
            {
                ShowFallback(eventLabel);
                yield break;
            }

            DebugHud?.ShowEvent($"NARRATOR: {response.text}");
            Debug.Log($"[Dread Director] Narration ({response.source}): {response.text}", this);
            loggedUnavailable = false;
            if (IsLoopbackAudioUrl(response.audioUrl))
            {
                yield return PlayAudio(response.audioUrl);
            }
        }

        private IEnumerator PlayAudio(string audioUrl)
        {
            using var request = UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.MPEG);
            request.timeout = TimeoutSeconds;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success && audioSource != null)
            {
                var clip = DownloadHandlerAudioClip.GetContent(request);
                audioSource.PlayOneShot(clip, 0.65f);
            }
        }

        private void ShowFallback(string eventLabel)
        {
            DebugHud?.ShowEvent($"NARRATOR: {OfflineLine(eventLabel)}");
        }

        private static bool IsAllowed(string eventLabel)
        {
            return eventLabel == "calibration_complete" || eventLabel == "escalation_low" ||
                   eventLabel == "escalation_high" || eventLabel == "panic_backoff" || eventLabel == "recovery";
        }

        private static bool IsLoopbackAudioUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
        }

        private static string OfflineLine(string eventLabel)
        {
            return eventLabel switch
            {
                "calibration_complete" => "Calibration complete. Try not to let the room learn you too quickly.",
                "escalation_low" => "Something in the room has shifted. Keep watching the corners.",
                "escalation_high" => "It knows where you are. Do not look away.",
                "panic_backoff" => "Easy now. The room is giving you one breath.",
                "recovery" => "Your pulse settles. Somewhere in the dark, it starts waiting again.",
                _ => "The room is listening."
            };
        }
    }
}
