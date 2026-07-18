using System.Collections;
using DreadDirector.Horror;
using DreadDirector.Presentation;
using UnityEngine;

namespace DreadDirector.Player
{
    /// <summary>
    /// Plays the lethal "caught" animation when the apparition reaches the player: the creature
    /// lunges into the camera, the view is thrown around as if struck, and the screen floods red
    /// then black. When the lunge finishes it hands off to the <see cref="NightWatchContract"/>,
    /// which shows the death/leaderboard screen. Presentation only — a game-over state, not any
    /// modeling of real harm.
    /// </summary>
    public sealed class PlayerDeathController : MonoBehaviour
    {
        public ApparitionController Apparition;
        public FirstPersonController PlayerMovement;
        public AudioStingController AudioSting;
        public NightWatchContract Contract;

        [Min(0.2f)] public float LungeDuration = 0.9f;

        private bool dying;
        private bool animationDone;
        private float fadeAlpha;
        private Quaternion cameraStartLocalRotation;
        private Vector3 monsterStartScale;
        private Texture2D fadeTexture;

        private void Start()
        {
            // Capture sane defaults so a restart that never triggered the lunge still restores
            // valid camera/monster transforms.
            if (PlayerMovement != null && PlayerMovement.CameraTransform != null)
            {
                cameraStartLocalRotation = PlayerMovement.CameraTransform.localRotation;
            }
            else
            {
                cameraStartLocalRotation = Quaternion.identity;
            }

            if (Apparition != null && Apparition.ApparitionVisual != null)
            {
                monsterStartScale = Apparition.ApparitionVisual.transform.localScale;
            }

            if (Apparition != null)
            {
                Apparition.PlayerCaught += HandleCaught;
            }
        }

        private void OnDestroy()
        {
            if (Apparition != null)
            {
                Apparition.PlayerCaught -= HandleCaught;
            }

            if (fadeTexture != null)
            {
                Destroy(fadeTexture);
            }
        }

        private void HandleCaught()
        {
            if (dying || animationDone)
            {
                return;
            }

            dying = true;

            if (PlayerMovement != null)
            {
                PlayerMovement.enabled = false;
            }

            // Take over the creature so we can drive the lunge without the chase logic fighting us.
            if (Apparition != null)
            {
                Apparition.enabled = false;
            }

            StartCoroutine(LungeSequence());
        }

        private IEnumerator LungeSequence()
        {
            AudioSting?.PlayJumpScare();

            var camera = PlayerMovement != null ? PlayerMovement.CameraTransform : null;
            var monster = Apparition != null && Apparition.ApparitionVisual != null
                ? Apparition.ApparitionVisual.transform
                : null;

            if (camera != null)
            {
                cameraStartLocalRotation = camera.localRotation;
            }
            if (monster != null)
            {
                monsterStartScale = monster.localScale;
            }

            var renderers = monster != null ? monster.GetComponentsInChildren<Renderer>() : null;

            var elapsed = 0f;
            while (elapsed < LungeDuration)
            {
                var t = elapsed / LungeDuration;

                if (camera != null && monster != null)
                {
                    // Rush the creature into the player, staying on the ground (don't float it up).
                    var playerXZ = new Vector3(camera.position.x, monster.position.y, camera.position.z);
                    var toPlayer = playerXZ - monster.position;
                    var dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : monster.forward;
                    var standoff = Mathf.Lerp(1.9f, 0.65f, t);
                    var target = playerXZ - dir * standoff;
                    monster.position = Vector3.Lerp(monster.position, target, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                    // Demon is authored facing -Z, so +Z must point away from the player.
                    monster.rotation = Quaternion.LookRotation(-dir, Vector3.up);
                    monster.localScale = monsterStartScale * Mathf.Lerp(1f, 1.35f, t);

                    // Lock the camera onto the creature's FACE so it fills the screen (fixes it
                    // sitting above the view), then throw the view around as if being struck.
                    var facePoint = FacePoint(renderers, monster);
                    var toFace = facePoint - camera.position;
                    if (toFace.sqrMagnitude > 0.0001f)
                    {
                        var lookRotation = Quaternion.LookRotation(toFace.normalized, Vector3.up);
                        var shake = Mathf.Lerp(1.2f, 10f, t);
                        var offset = new Vector3(
                            (Mathf.PerlinNoise(Time.unscaledTime * 32f, 0f) - 0.5f) * shake,
                            (Mathf.PerlinNoise(0f, Time.unscaledTime * 32f) - 0.5f) * shake,
                            (Mathf.PerlinNoise(Time.unscaledTime * 24f, 5f) - 0.5f) * shake);
                        camera.rotation = lookRotation * Quaternion.Euler(offset);
                    }
                }

                // Hold the red flood back until the second half so the lunge is clearly visible.
                fadeAlpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, t)) * 0.9f;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            fadeAlpha = 1f;
            animationDone = true;
            dying = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;

            // Hand off to the contract, which records the leaderboard and shows the end screen.
            Contract?.EndRun(false);
        }

        /// <summary>Approximates a point on the creature's head to aim the camera at.</summary>
        private static Vector3 FacePoint(Renderer[] renderers, Transform fallback)
        {
            if (renderers != null && renderers.Length > 0)
            {
                var haveBounds = false;
                var bounds = new Bounds();
                for (var i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null)
                    {
                        continue;
                    }

                    if (!haveBounds)
                    {
                        bounds = renderers[i].bounds;
                        haveBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                }

                if (haveBounds)
                {
                    // Bias upward toward the head.
                    return new Vector3(bounds.center.x, Mathf.Lerp(bounds.center.y, bounds.max.y, 0.55f), bounds.center.z);
                }
            }

            return fallback.position + Vector3.up * 1.5f;
        }

        /// <summary>Restores everything after a respawn (called by the contract on restart).</summary>
        public void ResetDeath()
        {
            StopAllCoroutines();
            dying = false;
            animationDone = false;
            fadeAlpha = 0f;

            if (PlayerMovement != null && PlayerMovement.CameraTransform != null)
            {
                PlayerMovement.CameraTransform.localRotation = cameraStartLocalRotation;
            }

            if (Apparition != null)
            {
                if (Apparition.ApparitionVisual != null && monsterStartScale != Vector3.zero)
                {
                    Apparition.ApparitionVisual.transform.localScale = monsterStartScale;
                }
                Apparition.enabled = true;
            }
        }

        private void OnGUI()
        {
            // Only draw the red/black flood during the lunge; once done, the contract owns the screen.
            if (!dying || fadeAlpha <= 0f)
            {
                return;
            }

            EnsureFadeTexture();
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, fadeAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), fadeTexture, ScaleMode.StretchToFill);
            GUI.color = previous;
        }

        private void EnsureFadeTexture()
        {
            if (fadeTexture != null)
            {
                return;
            }

            fadeTexture = new Texture2D(1, 1);
            fadeTexture.SetPixel(0, 0, new Color(0.28f, 0f, 0f));
            fadeTexture.Apply();
        }
    }
}
