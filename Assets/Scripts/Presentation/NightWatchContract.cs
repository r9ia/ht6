using System;
using DreadDirector.Director;
using DreadDirector.Horror;
using DreadDirector.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DreadDirector.Presentation
{
    /// <summary>
    /// The game-facing "Night Watch Contract". A one-minute run where a Solana balance accrues
    /// faster the calmer (less scared) the player stays. When the run ends — by surviving the full
    /// minute or by being caught — the player's composure score is recorded to a local leaderboard
    /// and they earn Solana proportional to how chill they stayed.
    ///
    /// This is a local, cosmetic game economy (sandbox). It sends nothing anywhere; the separate
    /// <see cref="UnifoldRewardBridgeClient"/> is the only module that talks to the reward bridge,
    /// and only via an opaque claim. No biometrics or identity leave the machine.
    /// </summary>
    public sealed class NightWatchContract : MonoBehaviour
    {
        [Header("Scene references (auto-found if empty)")]
        public DirectorGameBridge Director;
        public DreadDirector.Player.FirstPersonController Player;
        public ApparitionController Apparition;
        public PlayerDeathController Death;

        [Header("Contract")]
        [Tooltip("Length of a Night Watch run, in seconds.")]
        [Min(5f)] public float RunDuration = 60f;
        [Tooltip("Max Solana accrued per second while perfectly calm.")]
        [Min(0f)] public float BalanceRatePerSecond = 0.02f;
        [Tooltip("Bonus Solana for surviving the full night.")]
        [Min(0f)] public float SurvivalBonus = 0.5f;

        private const int LeaderboardSize = 5;
        private const string ScoreKeyPrefix = "nightwatch_lb_score_";
        private const string SolKeyPrefix = "nightwatch_lb_sol_";

        private bool running;
        private bool finished;
        private bool survived;
        private float timeRemaining;
        private double balance;
        private double chillTimeSum;
        private double elapsed;

        private int lastScore;
        private double lastSolana;
        private int lastRank = -1;

        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        private bool stylesReady;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle bigStyle;
        private GUIStyle balanceStyle;
        private GUIStyle rowStyle;
        private Texture2D panelTex;
        private Texture2D fullscreenTex;

        private void Awake()
        {
            if (Director == null) Director = FindAnyObjectByType<DirectorGameBridge>();
            if (Apparition == null) Apparition = FindAnyObjectByType<ApparitionController>();
            if (Death == null) Death = FindAnyObjectByType<PlayerDeathController>();
            if (Player == null) Player = FindAnyObjectByType<DreadDirector.Player.FirstPersonController>();
            timeRemaining = RunDuration;
        }

        private void Start()
        {
            if (Player != null)
            {
                spawnPosition = Player.transform.position;
                spawnRotation = Player.transform.rotation;
            }
        }

        private void Update()
        {
            if (finished)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                {
                    Restart();
                }
                return;
            }

            // Begin the run once calibration completes so the quiet setup phase does not pay out.
            if (!running)
            {
                if (Director == null || Director.CalibrationComplete)
                {
                    running = true;
                }
                else
                {
                    return;
                }
            }

            var dt = Time.deltaTime; // scaled: naturally pauses when the game is frozen on death
            var fear = Director != null ? Mathf.Clamp01(Mathf.Max(Director.Arousal, Director.SustainedStress)) : 0f;
            var chill = 1f - fear;

            balance += chill * BalanceRatePerSecond * dt;
            chillTimeSum += chill * dt;
            elapsed += dt;

            timeRemaining -= dt;
            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                EndRun(true);
            }
        }

        /// <summary>Ends the current run. Called on death (survived=false) or when the timer
        /// expires (survived=true). Records the leaderboard entry and freezes the game.</summary>
        public void EndRun(bool didSurvive)
        {
            if (finished)
            {
                return;
            }

            finished = true;
            running = false;
            survived = didSurvive;

            var avgChill = elapsed > 0.01 ? (float)(chillTimeSum / elapsed) : 0f;
            var survivedFraction = Mathf.Clamp01(1f - timeRemaining / RunDuration);
            lastScore = Mathf.RoundToInt(avgChill * 100f * (didSurvive ? 1f : survivedFraction));
            lastSolana = Math.Round(balance + (didSurvive ? SurvivalBonus : 0.0), 4);
            lastRank = RecordLeaderboard(lastScore, lastSolana);

            if (Player != null)
            {
                Player.enabled = false;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            finished = false;
            running = false;
            survived = false;
            timeRemaining = RunDuration;
            balance = 0.0;
            chillTimeSum = 0.0;
            elapsed = 0.0;

            Death?.ResetDeath();
            Apparition?.ResetToHome();

            if (Player != null)
            {
                var controller = Player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                Player.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
                if (controller != null) controller.enabled = true;
                Player.enabled = true;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ---- Leaderboard (local, PlayerPrefs) ----

        private int RecordLeaderboard(int score, double solana)
        {
            var scores = new int[LeaderboardSize + 1];
            var sols = new double[LeaderboardSize + 1];
            var count = 0;
            for (var i = 0; i < LeaderboardSize; i++)
            {
                if (!PlayerPrefs.HasKey(ScoreKeyPrefix + i)) continue;
                scores[count] = PlayerPrefs.GetInt(ScoreKeyPrefix + i);
                sols[count] = double.Parse(PlayerPrefs.GetString(SolKeyPrefix + i, "0"), System.Globalization.CultureInfo.InvariantCulture);
                count++;
            }

            // Insert the new entry, then sort descending by score.
            scores[count] = score;
            sols[count] = solana;
            count++;

            for (var a = 0; a < count - 1; a++)
            {
                for (var b = a + 1; b < count; b++)
                {
                    if (scores[b] > scores[a])
                    {
                        (scores[a], scores[b]) = (scores[b], scores[a]);
                        (sols[a], sols[b]) = (sols[b], sols[a]);
                    }
                }
            }

            var rank = -1;
            var kept = Mathf.Min(count, LeaderboardSize);
            for (var i = 0; i < kept; i++)
            {
                PlayerPrefs.SetInt(ScoreKeyPrefix + i, scores[i]);
                PlayerPrefs.SetString(SolKeyPrefix + i, sols[i].ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
                if (rank < 0 && scores[i] == score && Math.Abs(sols[i] - solana) < 0.00001)
                {
                    rank = i;
                }
            }
            PlayerPrefs.Save();
            return rank;
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (finished)
            {
                DrawEndScreen();
                return;
            }

            // Top-right live contract HUD.
            const float w = 250f;
            const float h = 96f;
            var x = Screen.width - w - 20f;
            var y = 20f;
            GUI.DrawTexture(new Rect(x, y, w, h), panelTex, ScaleMode.StretchToFill);
            GUI.Label(new Rect(x + 14f, y + 8f, w - 28f, 22f), "NIGHT WATCH CONTRACT", titleStyle);

            var calibrating = !running && Director != null && !Director.CalibrationComplete;
            if (calibrating)
            {
                GUI.Label(new Rect(x + 14f, y + 40f, w - 28f, 24f), "CALIBRATING SIGNAL…", bodyStyle);
                return;
            }

            GUI.Label(new Rect(x + 14f, y + 36f, w - 28f, 26f), $"◎ {balance:0.0000} SOL", balanceStyle);
            var seconds = Mathf.CeilToInt(timeRemaining);
            GUI.Label(new Rect(x + 14f, y + 66f, w - 28f, 22f), $"TIME  0:{seconds:00}", bodyStyle);
        }

        private void DrawEndScreen()
        {
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), fullscreenTex, ScaleMode.StretchToFill);

            var cx = Screen.width * 0.5f;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            var title = survived ? "YOU SURVIVED THE NIGHT" : "YOU DIED";
            var titleColor = survived ? new Color(0.4f, 0.88f, 0.55f) : new Color(0.82f, 0.08f, 0.09f);
            bigStyle.normal.textColor = titleColor;
            GUI.Label(new Rect(cx - 400f, Screen.height * 0.22f, 800f, 64f), title, bigStyle);

            GUI.Label(new Rect(cx - 400f, Screen.height * 0.22f + 70f, 800f, 26f),
                $"Chill score {lastScore}/100      Earned ◎ {lastSolana:0.####} SOL" + (lastRank == 0 ? "      NEW BEST!" : string.Empty),
                bodyStyle);

            // Leaderboard.
            GUI.Label(new Rect(cx - 200f, Screen.height * 0.40f, 400f, 24f), "— LEADERBOARD (chillest nights) —", titleStyle);
            var top = Screen.height * 0.40f + 30f;
            for (var i = 0; i < LeaderboardSize; i++)
            {
                if (!PlayerPrefs.HasKey(ScoreKeyPrefix + i)) break;
                var s = PlayerPrefs.GetInt(ScoreKeyPrefix + i);
                var sol = PlayerPrefs.GetString(SolKeyPrefix + i, "0");
                rowStyle.normal.textColor = i == lastRank ? new Color(0.98f, 0.85f, 0.35f) : new Color(0.8f, 0.85f, 0.82f);
                GUI.Label(new Rect(cx - 200f, top + i * 26f, 400f, 24f), $"#{i + 1}   chill {s,3}/100     ◎ {sol} SOL", rowStyle);
            }

            bodyStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(cx - 400f, Screen.height * 0.72f, 800f, 24f), "Press [R] to play again      ·      press [5] to claim your bounty", bodyStyle);
            bodyStyle.alignment = TextAnchor.MiddleLeft;
        }

        private void EnsureStyles()
        {
            if (stylesReady)
            {
                return;
            }

            panelTex = SolidTexture(new Color(0.03f, 0.03f, 0.02f, 0.85f));
            fullscreenTex = SolidTexture(new Color(0.02f, 0f, 0f, 0.92f));

            titleStyle = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.95f, 0.35f, 0.35f) } };
            bodyStyle = new GUIStyle { fontSize = 14, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.82f, 0.88f, 0.84f) } };
            rowStyle = new GUIStyle { fontSize = 15, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.8f, 0.85f, 0.82f) } };
            bigStyle = new GUIStyle { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.55f, 0.95f, 0.7f) } };
            balanceStyle = new GUIStyle { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.55f, 0.95f, 0.7f) } };
            stylesReady = true;
        }

        private static Texture2D SolidTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            if (panelTex != null) Destroy(panelTex);
            if (fullscreenTex != null) Destroy(fullscreenTex);
        }
    }
}
