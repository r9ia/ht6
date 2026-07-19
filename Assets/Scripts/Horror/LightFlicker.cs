using UnityEngine;

namespace DreadDirector.Horror
{
    /// <summary>
    /// Holds the room's base lighting steady. Lighting is intentionally NOT tied to the Director's
    /// emotional signal — the dread comes from the monster, audio, and pacing, not from the room
    /// dimming or reddening as you get scared. Atmospheric, random failing-tube flicker lives on
    /// <see cref="FluorescentFlicker"/> (also independent of the Director). The Director hooks below
    /// are kept as no-ops so <c>DirectorGameBridge</c> can still call them without touching lights.
    /// </summary>
    public sealed class LightFlicker : MonoBehaviour
    {
        public Light RoomLight;
        [Min(0f)] public float CalmIntensity = 1.66f;
        public Color CalmColor = new Color(0.95f, 0.93f, 0.78f);

        // Constant flat ambient. Does NOT react to Director tension.
        private static readonly Color SteadyAmbient = new Color(0.47f, 0.45f, 0.36f);

        private void Update()
        {
            if (RoomLight != null)
            {
                RoomLight.intensity = CalmIntensity;
                RoomLight.color = CalmColor;
            }

            RenderSettings.ambientLight = SteadyAmbient;
        }

        // --- Director hooks: kept for bridge compatibility, intentionally do NOT change lighting ---
        public void BeginSubtleWrongness() { }
        public void SetTension(float value) { }
        public void TriggerFlicker(float intensity) { }
        public void BackOff() { }
    }
}
