using UnityEngine;

namespace DreadDirector.Director
{
    /// <summary>Tracks the initial non-medical signal calibration period used by the demo pacing.</summary>
    public sealed class CalibrationController : MonoBehaviour
    {
        [Min(1f)] public float DurationSeconds = 60f;
        public DirectorGameBridge Bridge;

        public float ElapsedSeconds { get; private set; }
        public bool IsComplete { get; private set; }
        public float Progress => Mathf.Clamp01(ElapsedSeconds / DurationSeconds);
        public float RemainingSeconds => Mathf.Max(0f, DurationSeconds - ElapsedSeconds);

        private void OnEnable()
        {
            Begin();
        }

        public void Begin()
        {
            ElapsedSeconds = 0f;
            IsComplete = false;
        }

        private void Update()
        {
            if (IsComplete)
            {
                return;
            }

            ElapsedSeconds += Time.deltaTime;
            if (ElapsedSeconds < DurationSeconds)
            {
                return;
            }

            ElapsedSeconds = DurationSeconds;
            IsComplete = true;
            Bridge?.NotifyCalibrationComplete();
        }
    }
}
