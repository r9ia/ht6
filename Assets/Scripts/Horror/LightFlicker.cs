using UnityEngine;

namespace DreadDirector.Horror
{
    /// <summary>Turns normalized Director tension into subtle color shifts and brief light failures.</summary>
    public sealed class LightFlicker : MonoBehaviour
    {
        public Light RoomLight;
        [Min(0f)] public float CalmIntensity = 1.78f;
        [Min(0f)] public float TenseIntensity = 0.72f;
        public Color CalmColor = new Color(0.95f, 0.93f, 0.78f);
        public Color TenseColor = new Color(0.7f, 0.08f, 0.11f);

        // Flat ambient endpoints. Backrooms read as unnaturally, flatly bright when calm; the
        // room curdles to a dim red as Director tension rises.
        private static readonly Color CalmAmbient = new Color(0.46f, 0.44f, 0.35f);
        private static readonly Color TenseAmbient = new Color(0.20f, 0.05f, 0.055f);

        private float tension;
        private float flickerUntil;
        private float flickerStrength;

        private void Update()
        {
            if (RoomLight == null)
            {
                return;
            }

            var intensity = Mathf.Lerp(CalmIntensity, TenseIntensity, tension);
            if (Time.time < flickerUntil)
            {
                var noise = Mathf.PerlinNoise(Time.time * 28f, 0.27f);
                intensity *= Mathf.Lerp(0.2f, 1.15f, noise);
            }

            RoomLight.intensity = intensity;
            RoomLight.color = Color.Lerp(CalmColor, TenseColor, tension);
            RenderSettings.ambientLight = Color.Lerp(CalmAmbient, TenseAmbient, tension);
        }

        public void BeginSubtleWrongness()
        {
            SetTension(Mathf.Max(tension, 0.22f));
        }

        public void SetTension(float value)
        {
            tension = Mathf.Clamp01(value);
        }

        public void TriggerFlicker(float intensity)
        {
            tension = Mathf.Max(tension, Mathf.Clamp01(intensity));
            flickerStrength = Mathf.Clamp01(intensity);
            flickerUntil = Time.time + Mathf.Lerp(0.5f, 2.2f, flickerStrength);
        }

        public void BackOff()
        {
            tension = 0.08f;
            flickerUntil = 0f;
            flickerStrength = 0f;
        }
    }
}
