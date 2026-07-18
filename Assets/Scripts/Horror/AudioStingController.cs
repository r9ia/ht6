using UnityEngine;

namespace DreadDirector.Horror
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioStingController : MonoBehaviour
    {
        [Range(0f, 1f)] public float MaximumVolume = 0.32f;
        [Min(0.05f)] public float DurationSeconds = 0.45f;

        private AudioSource audioSource;
        private AudioClip stingClip;
        private AudioClip jumpScareClip;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            stingClip = CreateSting();
            jumpScareClip = CreateJumpScare();
        }

        private void OnDestroy()
        {
            if (stingClip != null)
            {
                Destroy(stingClip);
            }

            if (jumpScareClip != null)
            {
                Destroy(jumpScareClip);
            }
        }

        public void PlaySting(float intensity)
        {
            if (audioSource == null || stingClip == null)
            {
                return;
            }

            audioSource.PlayOneShot(stingClip, Mathf.Lerp(0.08f, MaximumVolume, Mathf.Clamp01(intensity)));
        }

        /// <summary>Loud, harsh burst for jump scares.</summary>
        public void PlayJumpScare()
        {
            if (audioSource == null || jumpScareClip == null)
            {
                return;
            }

            audioSource.PlayOneShot(jumpScareClip, 1f);
        }

        public void Hush()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
            }
        }

        private AudioClip CreateSting()
        {
            const int sampleRate = 44100;
            var samples = Mathf.CeilToInt(sampleRate * DurationSeconds);
            var data = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                var time = i / (float)sampleRate;
                var envelope = Mathf.Pow(1f - i / (float)samples, 2.4f);
                var frequency = Mathf.Lerp(110f, 760f, i / (float)samples);
                data[i] = Mathf.Sin(time * frequency * Mathf.PI * 2f) * envelope * 0.55f;
            }

            var clip = AudioClip.Create("DreadDirector_ProceduralSting", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateJumpScare()
        {
            const int sampleRate = 44100;
            const float duration = 0.75f;
            var samples = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[samples];
            var random = new System.Random(9173);
            for (var i = 0; i < samples; i++)
            {
                var progress = i / (float)samples;
                var time = i / (float)sampleRate;
                // Fast attack, long-ish decay for a violent burst.
                var envelope = Mathf.Min(1f, progress * 40f) * Mathf.Pow(1f - progress, 1.1f);
                // Downward-sweeping dissonant tone stacked with harsh noise.
                var toneA = Mathf.Sin(time * Mathf.Lerp(880f, 120f, progress) * Mathf.PI * 2f);
                var toneB = Mathf.Sin(time * Mathf.Lerp(1240f, 190f, progress) * Mathf.PI * 2f);
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                data[i] = Mathf.Clamp((toneA * 0.32f + toneB * 0.24f + noise * 0.6f) * envelope, -1f, 1f);
            }

            var clip = AudioClip.Create("DreadDirector_JumpScare", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
