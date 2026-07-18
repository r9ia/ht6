using DreadDirector.Horror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DreadDirector.Player
{
    /// <summary>
    /// Lethal outcome handler. When the apparition reaches the player it catches them: the game
    /// freezes, a death overlay appears, and the player can respawn in place. This is presentation
    /// only — the "death" is a game-over state, not any modeling of real harm.
    /// </summary>
    public sealed class PlayerDeathController : MonoBehaviour
    {
        public ApparitionController Apparition;
        public FirstPersonController PlayerMovement;
        public AudioStingController AudioSting;

        private bool dead;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private GUIStyle titleStyle;
        private GUIStyle promptStyle;
        private GUIStyle overlayStyle;
        private Texture2D overlayTexture;

        private void Start()
        {
            if (PlayerMovement != null)
            {
                spawnPosition = PlayerMovement.transform.position;
                spawnRotation = PlayerMovement.transform.rotation;
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

            if (overlayTexture != null)
            {
                Destroy(overlayTexture);
            }
        }

        private void Update()
        {
            if (!dead)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                Respawn();
            }
        }

        private void HandleCaught()
        {
            if (dead)
            {
                return;
            }

            dead = true;
            AudioSting?.PlayJumpScare();

            // Disabling the controller also releases the cursor (see FirstPersonController.OnDisable).
            if (PlayerMovement != null)
            {
                PlayerMovement.enabled = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Freeze the world so the creature stays in the player's face on the death frame.
            Time.timeScale = 0f;
        }

        private void Respawn()
        {
            dead = false;
            Time.timeScale = 1f;

            if (PlayerMovement != null)
            {
                // A CharacterController resists direct transform moves; toggle it around the teleport.
                var controller = PlayerMovement.GetComponent<CharacterController>();
                if (controller != null)
                {
                    controller.enabled = false;
                }

                PlayerMovement.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

                if (controller != null)
                {
                    controller.enabled = true;
                }

                PlayerMovement.enabled = true;
            }

            Apparition?.ResetToHome();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnGUI()
        {
            if (!dead)
            {
                return;
            }

            EnsureStyles();
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), overlayTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(0f, Screen.height * 0.5f - 70f, Screen.width, 70f), "YOU DIED", titleStyle);
            GUI.Label(new Rect(0f, Screen.height * 0.5f + 8f, Screen.width, 32f), "Press R to try again", promptStyle);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            overlayTexture = new Texture2D(1, 1);
            overlayTexture.SetPixel(0, 0, new Color(0.05f, 0f, 0f, 0.82f));
            overlayTexture.Apply();

            titleStyle = new GUIStyle
            {
                fontSize = 72,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.78f, 0.05f, 0.06f) }
            };
            promptStyle = new GUIStyle
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.82f, 0.78f) }
            };
        }
    }
}
