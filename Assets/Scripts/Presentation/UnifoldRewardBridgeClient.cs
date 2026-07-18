using System;
using System.Collections;
using System.Text;
using DreadDirector.Director;
using DreadDirector.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace DreadDirector.Presentation
{
    /// <summary>
    /// Opt-in "Night Watch Contract" client. It rewards the player for surviving longer
    /// and staying calmer (being less scared) with a sandbox stablecoin payout issued by
    /// the local <c>unifold-bridge</c>.
    ///
    /// Privacy boundary: survival time and composure are measured locally and folded into
    /// ONE opaque achievement tier. Only <c>{version, claimId, tier}</c> is sent to the
    /// loopback bridge — never biometrics, Director scores, identity, or a wallet. The
    /// bridge is the sole module that talks to Unifold.
    /// </summary>
    public sealed class UnifoldRewardBridgeClient : MonoBehaviour
    {
        [Serializable]
        private sealed class RewardRequest
        {
            public int version = 1;
            public string claimId;
            public string tier;
        }

        [Serializable]
        private sealed class RewardResponse
        {
            public string status;
            public double amountUsd;
            public string tier;
            public string mode;
            public bool reused;
            public string reference;
            public string error;
        }

        [Header("Local bridge")]
        public string Endpoint = "http://127.0.0.1:8788/v1/reward/claim";
        [Min(1)] public int TimeoutSeconds = 8;

        [Header("Scene references (auto-found if empty)")]
        public DirectorGameBridge Director;
        public BiometricDebugHud DebugHud;

        [Header("Reward eligibility (survive longer, stay calmer)")]
        [Tooltip("Seconds survived before the smallest bounty (endured) unlocks.")]
        [Min(0f)] public float EnduredSeconds = 45f;
        [Tooltip("Seconds survived for the standard survivor bounty.")]
        [Min(0f)] public float SurvivorSeconds = 90f;
        [Tooltip("Seconds survived required for the top bounty.")]
        [Min(0f)] public float LongNightSeconds = 180f;
        [Tooltip("Average composure (higher = calmer) needed for the composed bonus.")]
        public float CalmComposure = 0.15f;
        [Tooltip("Average composure needed for the top 'unshaken' bounty.")]
        public float VeryCalmComposure = 0.45f;
        [Tooltip("Average sustained stress must stay under this for the top bounty.")]
        [Range(0f, 1f)] public float MaxCalmStress = 0.35f;

        [Header("Input")]
        [Tooltip("Enable the [5] key to claim the earned bounty.")]
        public bool EnableClaimHotkey = true;

        private float survivalSeconds;
        private double composureSum;
        private double stressSum;
        private long sampleCount;
        private bool tracking;
        private bool claimInFlight;
        private string sessionClaimId;

        private void Awake()
        {
            if (Director == null)
            {
                Director = FindFirstObjectByType<DirectorGameBridge>();
            }
            if (DebugHud == null)
            {
                DebugHud = Director != null ? Director.DebugHud : FindFirstObjectByType<BiometricDebugHud>();
            }
        }

        private void Update()
        {
            // Only accumulate survival + composure once calibration is done, so the
            // quiet setup phase never inflates the reward.
            if (!tracking && (Director == null || Director.CalibrationComplete))
            {
                tracking = true;
            }

            if (tracking && Director != null)
            {
                survivalSeconds += Time.unscaledDeltaTime;
                composureSum += Director.Composure;
                stressSum += Director.SustainedStress;
                sampleCount++;
            }

            if (EnableClaimHotkey && !claimInFlight)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null && keyboard.digit5Key.wasPressedThisFrame)
                {
                    ClaimReward();
                }
            }
        }

        /// <summary>Claim the bounty for the current run. Safe to call repeatedly — the
        /// bridge is idempotent on the per-session claim id.</summary>
        public void ClaimReward()
        {
            if (claimInFlight)
            {
                return;
            }

            var tier = EarnedTier();
            if (tier == null)
            {
                ShowLine("Survive longer and stay calmer to earn a Night Watch bounty.", "not-eligible");
                return;
            }

            if (!IsLoopbackEndpoint(Endpoint))
            {
                Debug.LogWarning("[Dread Director] Reward endpoint must be loopback.", this);
                return;
            }

            // One opaque claim id per run makes retries idempotent and carries no identity.
            sessionClaimId ??= "nightwatch_" + Guid.NewGuid().ToString("N").Substring(0, 24);
            claimInFlight = true;
            StartCoroutine(SendClaim(sessionClaimId, tier));
        }

        /// <summary>Fold local survival time and average composure into one opaque tier,
        /// or <c>null</c> when the player has not yet earned a bounty.</summary>
        public string EarnedTier()
        {
            var averageComposure = sampleCount > 0 ? (float)(composureSum / sampleCount) : 0f;
            var averageStress = sampleCount > 0 ? (float)(stressSum / sampleCount) : 1f;

            if (survivalSeconds >= LongNightSeconds && averageComposure >= VeryCalmComposure && averageStress <= MaxCalmStress)
            {
                return "unshaken";
            }
            if (survivalSeconds >= SurvivorSeconds && averageComposure >= CalmComposure)
            {
                return "composed_survivor";
            }
            if (survivalSeconds >= SurvivorSeconds)
            {
                return "survivor";
            }
            if (survivalSeconds >= EnduredSeconds)
            {
                return "endured";
            }
            return null;
        }

        private IEnumerator SendClaim(string claimId, string tier)
        {
            var requestBody = JsonUtility.ToJson(new RewardRequest { version = 1, claimId = claimId, tier = tier });
            using var request = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestBody)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds
            };
            request.SetRequestHeader("Content-Type", "application/json");
            DebugHud?.ShowEvent($"NIGHT WATCH BOUNTY // {tier} // claiming...");
            yield return request.SendWebRequest();
            claimInFlight = false;

            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowLine("The bounty ledger is unreachable. Core gameplay is unaffected.", "bridge-offline");
                Debug.Log($"[Dread Director] Unifold reward bridge unavailable: HTTP {request.responseCode}.", this);
                yield break;
            }

            var response = JsonUtility.FromJson<RewardResponse>(request.downloadHandler.text);
            if (response == null || string.IsNullOrWhiteSpace(response.status))
            {
                ShowLine("The bounty could not be confirmed.", "bridge-error");
                yield break;
            }

            switch (response.status)
            {
                case "completed":
                    ShowLine($"Bounty settled: {response.amountUsd:0.00} USDC ({response.mode}){(response.reused ? " [already claimed]" : string.Empty)}.", "completed");
                    break;
                case "pending":
                    ShowLine($"Bounty of {response.amountUsd:0.00} USDC is settling on-chain.", "pending");
                    break;
                case "cancelled":
                    ShowLine("The bounty was cancelled.", "cancelled");
                    break;
                default:
                    ShowLine("The bounty failed to settle. You can try again.", "failed");
                    break;
            }
        }

        private void ShowLine(string line, string source)
        {
            DebugHud?.ShowEvent($"NIGHT WATCH: {line}");
            Debug.Log($"[Dread Director] Night Watch bounty ({source}): {line}", this);
        }

        private static bool IsLoopbackEndpoint(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && uri.IsLoopback;
        }
    }
}
