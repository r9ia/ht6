using DreadDirector.Horror;
using DreadDirector.Presentation;
using DreadDirector.UI;
using UnityEngine;

namespace DreadDirector.Director
{
    /// <summary>
    /// Uses continuous microphone loudness as a live intensity input: the louder the room
    /// (screaming, talking, banging), the more agitated the creature becomes. Staying quiet keeps
    /// it calmer. It is capped so noise alone never fully maxes the creature; the Director signal
    /// still owns the top end.
    ///
    /// Privacy: the audio is analyzed in memory for loudness (RMS) only. Nothing is recorded to
    /// disk or sent anywhere. It yields the microphone device to the opt-in push-to-talk voice line
    /// whenever that is capturing.
    /// </summary>
    public sealed class MicIntensityInput : MonoBehaviour
    {
        public ApparitionController Apparition;
        public NarrationBridgeClient Narration;
        public BiometricDebugHud DebugHud;

        [Tooltip("Turn the whole feature off.")]
        public bool Enabled = true;

        [Tooltip("Loudness (RMS) below this counts as silence.")]
        [Range(0f, 0.5f)] public float NoiseFloor = 0.03f;
        [Tooltip("Loudness (RMS) that maps to the maximum contribution.")]
        [Range(0.05f, 1f)] public float LoudRms = 0.25f;
        [Tooltip("Most intensity noise can add on its own (kept modest on purpose).")]
        [Range(0f, 1f)] public float MaxContribution = 0.5f;
        [Min(0f)] public float RiseSpeed = 4f;
        [Min(0f)] public float FallSpeed = 1.2f;

        private const int ClipSeconds = 1;
        private const int SampleRate = 16000;
        private const int Window = 1024;

        private AudioClip micClip;
        private string device;
        private bool capturing;
        private bool requestedAuth;
        private float[] buffer;
        private float smoothed;
        private float lastLoudness;

        private void Awake()
        {
            if (Apparition == null) Apparition = FindAnyObjectByType<ApparitionController>();
            if (Narration == null) Narration = FindAnyObjectByType<NarrationBridgeClient>();
            buffer = new float[Window];
        }

        private void OnDisable()
        {
            StopCapture();
            smoothed = 0f;
            Apparition?.SetNoiseLevel(0f);
        }

        private void Update()
        {
            if (!Enabled || Apparition == null)
            {
                return;
            }

            // Yield the single microphone device while push-to-talk is capturing. We release
            // ownership WITHOUT ending the device, so we never cut off the voice recording.
            if (Narration != null && Narration.ConversationInProgress)
            {
                capturing = false;
                micClip = null;
                smoothed = 0f;
                Apparition.SetNoiseLevel(0f);
                return;
            }

            if (!capturing && !TryStartCapture())
            {
                return;
            }

            var loudness = ReadLoudness();
            var target = Mathf.Clamp01(Mathf.InverseLerp(NoiseFloor, LoudRms, loudness)) * MaxContribution;
            var speed = target > smoothed ? RiseSpeed : FallSpeed;
            smoothed = Mathf.MoveTowards(smoothed, target, speed * Time.deltaTime);
            Apparition.SetNoiseLevel(smoothed);
        }

        private bool TryStartCapture()
        {
            if (Microphone.devices.Length == 0)
            {
                return false;
            }

            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                if (!requestedAuth)
                {
                    requestedAuth = true;
                    Application.RequestUserAuthorization(UserAuthorization.Microphone);
                }
                return false;
            }

            device = null; // default device
            micClip = Microphone.Start(device, true, ClipSeconds, SampleRate);
            capturing = micClip != null;
            return capturing;
        }

        private void StopCapture()
        {
            if (capturing)
            {
                Microphone.End(device);
                capturing = false;
            }

            if (micClip != null)
            {
                Destroy(micClip);
                micClip = null;
            }
        }

        private float ReadLoudness()
        {
            if (micClip == null)
            {
                return 0f;
            }

            var position = Microphone.GetPosition(device) - Window;
            if (position < 0 || !micClip.GetData(buffer, position))
            {
                return lastLoudness; // samples not ready yet (start or loop wrap); hold last value
            }

            var sum = 0f;
            for (var i = 0; i < buffer.Length; i++)
            {
                sum += buffer[i] * buffer[i];
            }

            lastLoudness = Mathf.Sqrt(sum / buffer.Length);
            return lastLoudness;
        }
    }
}
