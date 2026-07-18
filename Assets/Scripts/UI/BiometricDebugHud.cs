using DreadDirector.Director;
using UnityEngine;

namespace DreadDirector.UI
{
    /// <summary>Debug-only HUD. It displays normalized Director state, not raw biometric data.</summary>
    public sealed class BiometricDebugHud : MonoBehaviour
    {
        public DirectorGameBridge Bridge;
        public CalibrationController Calibration;

        private string eventText = "SYSTEM ONLINE";
        private float eventUntil;
        private GUIStyle panelStyle;
        private GUIStyle labelStyle;
        private GUIStyle titleStyle;

        public void ShowEvent(string message)
        {
            eventText = message;
            eventUntil = Time.unscaledTime + 4f;
        }

        private void OnGUI()
        {
            if (Bridge == null)
            {
                return;
            }

            EnsureStyles();
            var panel = new Rect(18f, 18f, 355f, 240f);
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.Label(new Rect(34f, 31f, 320f, 28f), "DREAD DIRECTOR // NIGHT WATCH", titleStyle);

            var calibrationText = Calibration != null && !Calibration.IsComplete
                ? $"CALIBRATING SIGNAL  {Calibration.Progress * 100f:000}%  ({Calibration.RemainingSeconds:00}s)"
                : "SIGNAL CALIBRATED // ROOM ARMED";
            GUI.Label(new Rect(34f, 69f, 320f, 22f), calibrationText, labelStyle);
            GUI.Label(new Rect(34f, 97f, 320f, 22f), $"AROUSAL           {Bridge.Arousal:0.00}", labelStyle);
            GUI.Label(new Rect(34f, 121f, 320f, 22f), $"SUSTAINED STRESS  {Bridge.SustainedStress:0.00}", labelStyle);
            GUI.Label(new Rect(34f, 145f, 320f, 22f), $"COMPOSURE         {Bridge.Composure:+0.00;-0.00;0.00}", labelStyle);
            GUI.Label(new Rect(34f, 169f, 320f, 22f), $"LAST EVENT        {Bridge.LastEvent}", labelStyle);
            GUI.Label(new Rect(34f, 193f, 320f, 22f), $"SOURCE            {Bridge.LastSource}", labelStyle);

            var currentEvent = Time.unscaledTime < eventUntil ? eventText : "[1] low  [2] high  [3] panic  [4] recover  [5] bounty";
            GUI.Label(new Rect(34f, 223f, 320f, 22f), currentEvent, labelStyle);
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = Texture2D.blackTexture },
                padding = new RectOffset(12, 12, 12, 12)
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.66f, 0.93f, 0.84f) }
            };
            titleStyle = new GUIStyle(labelStyle)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.28f, 0.3f) }
            };
        }
    }
}
