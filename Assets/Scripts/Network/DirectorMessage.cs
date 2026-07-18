using System;
using UnityEngine;

namespace DreadDirector.Network
{
    /// <summary>High-level Director decision received from the QNX host; never contains raw biometrics.</summary>
    [Serializable]
    public sealed class DirectorMessage
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string type;
        public float arousal;
        public float sustainedStress;
        public float composure;
        public float intensity;

        public static DirectorMessage State(float arousal, float sustainedStress, float composure)
        {
            return new DirectorMessage
            {
                type = "state",
                arousal = Mathf.Clamp01(arousal),
                sustainedStress = Mathf.Clamp01(sustainedStress),
                composure = Mathf.Clamp(composure, -1f, 1f)
            };
        }

        public static DirectorMessage Event(string eventType, float intensity)
        {
            return new DirectorMessage
            {
                type = eventType,
                intensity = Mathf.Clamp01(intensity)
            };
        }

        public static bool TryParse(string json, out DirectorMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "UDP payload was empty.";
                return false;
            }

            try
            {
                message = JsonUtility.FromJson<DirectorMessage>(json);
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }

            if (message == null)
            {
                error = "JSON did not contain a Director message.";
                return false;
            }

            return message.IsValid(out error);
        }

        public bool IsValid(out string error)
        {
            error = null;
            if (version != CurrentVersion)
            {
                error = $"Unsupported Director message version {version}.";
                return false;
            }

            type = type == null ? string.Empty : type.Trim().ToLowerInvariant();
            if (type != "state" && type != "escalate" && type != "panic" && type != "recovery")
            {
                error = $"Unsupported Director message type '{type}'.";
                return false;
            }

            arousal = Mathf.Clamp01(arousal);
            sustainedStress = Mathf.Clamp01(sustainedStress);
            composure = Mathf.Clamp(composure, -1f, 1f);
            intensity = Mathf.Clamp01(intensity);
            return true;
        }
    }
}
