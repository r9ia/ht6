using UnityEngine;
using UnityEngine.InputSystem;

namespace DreadDirector.Player
{
    /// <summary>Simple generated-scene movement adapted from the Georgia branch contribution.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        public Transform CameraTransform;
        [Min(0f)] public float MoveSpeed = 2.5f;
        [Min(0f)] public float MouseSensitivity = 0.1f;
        public float Gravity = -9.81f;

        private CharacterController characterController;
        private float pitchDegrees;
        private float verticalVelocity;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            SetCursorLocked(true);
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null || CameraTransform == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
            }

            HandleLook(mouse);
            HandleMovement(keyboard);
        }

        private void HandleLook(Mouse mouse)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            var mouseDelta = mouse.delta.ReadValue() * MouseSensitivity;
            transform.Rotate(Vector3.up * mouseDelta.x);
            pitchDegrees = Mathf.Clamp(pitchDegrees - mouseDelta.y, -85f, 85f);
            CameraTransform.localRotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
        }

        private void HandleMovement(Keyboard keyboard)
        {
            var input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            var movement = (transform.right * input.x + transform.forward * input.y) * MoveSpeed;
            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            verticalVelocity += Gravity * Time.deltaTime;
            movement.y = verticalVelocity;
            characterController.Move(movement * Time.deltaTime);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
