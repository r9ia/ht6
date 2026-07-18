using UnityEngine;

namespace DreadDirector.Horror
{
    /// <summary>
    /// Independently flickers a set of ceiling fluorescent lights with buzzing dropouts, giving
    /// the backrooms their characteristic failing-tube atmosphere. Purely cosmetic — it does not
    /// touch gameplay or the Director signal.
    /// </summary>
    public sealed class FluorescentFlicker : MonoBehaviour
    {
        public Light[] Lights;

        [Tooltip("Per-light chance each frame to begin a brief flicker burst.")]
        [Range(0f, 1f)] public float FlickerChance = 0.012f;
        [Min(0.01f)] public float MinFlickerTime = 0.04f;
        [Min(0.02f)] public float MaxFlickerTime = 0.28f;
        [Tooltip("How dark a tube can dip mid-flicker (0 = fully off).")]
        [Range(0f, 1f)] public float MinDip = 0.05f;

        private float[] baseIntensities;
        private float[] flickerUntil;
        private float[] dipLevel;

        private void Awake()
        {
            if (Lights == null || Lights.Length == 0)
            {
                enabled = false;
                return;
            }

            baseIntensities = new float[Lights.Length];
            flickerUntil = new float[Lights.Length];
            dipLevel = new float[Lights.Length];
            for (var i = 0; i < Lights.Length; i++)
            {
                baseIntensities[i] = Lights[i] != null ? Lights[i].intensity : 1f;
            }
        }

        private void Update()
        {
            for (var i = 0; i < Lights.Length; i++)
            {
                var light = Lights[i];
                if (light == null)
                {
                    continue;
                }

                if (Time.time >= flickerUntil[i])
                {
                    if (Random.value < FlickerChance)
                    {
                        // Start a new flicker burst for this tube.
                        flickerUntil[i] = Time.time + Random.Range(MinFlickerTime, MaxFlickerTime);
                        dipLevel[i] = Random.Range(MinDip, 0.65f);
                    }
                    else
                    {
                        light.intensity = baseIntensities[i];
                        continue;
                    }
                }

                // Buzz between the dip level and full brightness while the burst lasts.
                var buzz = Mathf.PerlinNoise(Time.time * 45f, i * 4.13f);
                light.intensity = baseIntensities[i] * Mathf.Lerp(dipLevel[i], 1.05f, buzz);
            }
        }
    }
}
