using DreadDirector.Horror;
using DreadDirector.Network;
using DreadDirector.Presentation;
using DreadDirector.UI;
using UnityEngine;

namespace DreadDirector.Director
{
    /// <summary>Single gameplay entry point for fake controls and QNX Director events.</summary>
    public sealed class DirectorGameBridge : MonoBehaviour
    {
        [Header("Scene systems")]
        public CalibrationController Calibration;
        public LightFlicker LightFlicker;
        public ApparitionController Apparition;
        public AudioStingController AudioSting;
        public BiometricDebugHud DebugHud;
        public NarrationBridgeClient Narration;

        public float Arousal { get; private set; }
        public float SustainedStress { get; private set; }
        public float Composure { get; private set; }
        public float LastIntensity { get; private set; }
        public string LastEvent { get; private set; } = "Waiting for Director";
        public string LastSource { get; private set; } = "None";
        public bool CalibrationComplete => Calibration == null || Calibration.IsComplete;

        private void Start()
        {
            SetStatus("Calibration in progress. Demo controls are available.", "System");
        }

        public void ReceiveMessage(DirectorMessage message, string source = "Unknown")
        {
            if (message == null)
            {
                Debug.LogWarning("[Dread Director] Rejected an empty message.", this);
                return;
            }

            if (!message.IsValid(out var error))
            {
                Debug.LogWarning($"[Dread Director] Rejected message: {error}", this);
                return;
            }

            LastSource = source;
            if (message.type != "state")
            {
                Debug.Log($"[Dread Director] {source} -> {message.type} ({message.intensity:0.00})", this);
            }

            switch (message.type)
            {
                case "state":
                    ApplyState(message);
                    break;
                case "escalate":
                    Escalate(message.intensity);
                    break;
                case "panic":
                    Panic(message.intensity);
                    break;
                case "recovery":
                    Recover();
                    break;
            }
        }

        public void NotifyCalibrationComplete()
        {
            LightFlicker?.BeginSubtleWrongness();
            SetStatus("Calibration complete. The room has started to notice you.", "Calibration");
            Narration?.Announce("calibration_complete");
        }

        private void ApplyState(DirectorMessage message)
        {
            Arousal = message.arousal;
            SustainedStress = message.sustainedStress;
            Composure = message.composure;
            var stateIntensity = Mathf.Max(Arousal, SustainedStress * 0.75f);
            LastIntensity = stateIntensity;
            LastEvent = "State update";
            LightFlicker?.SetTension(stateIntensity);
            Apparition?.SetIntensity(stateIntensity);
            DebugHud?.ShowEvent($"Director state updated — monster {stateIntensity:0.00}");
        }

        private void Escalate(float intensity)
        {
            LastIntensity = Mathf.Clamp01(intensity);
            LastEvent = "Escalate";
            LightFlicker?.TriggerFlicker(LastIntensity);
            Apparition?.SetIntensity(LastIntensity);
            AudioSting?.PlaySting(LastIntensity);
            DebugHud?.ShowEvent(LastIntensity >= 0.7f
                ? $"ATTACK INTENSITY {LastIntensity:0.00}"
                : $"AGITATION INTENSITY {LastIntensity:0.00}");
            Narration?.Announce(LastIntensity >= 0.7f ? "escalation_high" : "escalation_low");
        }

        private void Panic(float intensity)
        {
            LastIntensity = Mathf.Clamp01(intensity);
            LastEvent = "Panic / monster backing off";
            LightFlicker?.BackOff();
            Apparition?.Retreat();
            AudioSting?.Hush();
            DebugHud?.ShowEvent("PANIC DETECTED — MONSTER BACKING OFF");
            Narration?.Announce("panic_backoff");
        }

        private void Recover()
        {
            LastIntensity = 0f;
            LastEvent = "Recovery / monster calm";
            LightFlicker?.BackOff();
            Apparition?.Rearm();
            DebugHud?.ShowEvent("RECOVERY DETECTED — MONSTER CALM");
            Narration?.Announce("recovery");
        }

        private void SetStatus(string status, string source)
        {
            LastEvent = status;
            LastSource = source;
            DebugHud?.ShowEvent(status);
        }
    }
}
