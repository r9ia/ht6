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

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            stingClip = CreateSting();
        }

        private void OnDestroy()
        {
            if (stingClip != null)
            {
                Destroy(stingClip);
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
    }
}
