using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 9f;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float gravity = -9.81f;

    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDrainPerSecond = 25f;
    [SerializeField] private float staminaRegenPerSecond = 15f;
    [SerializeField] private float minStaminaToStartSprint = 15f;

    private CharacterController controller;
    private float pitch;
    private float verticalVelocity;
    private float currentStamina;
    private bool isSprinting;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        currentStamina = maxStamina;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = !Cursor.visible;
        }
    }

    private void HandleLook()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseDelta.x);

        pitch = Mathf.Clamp(pitch - mouseDelta.y, -85f, 85f);
        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        Vector2 input = Vector2.zero;
        Keyboard keyboard = Keyboard.current;
        if (keyboard.wKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.aKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        bool wantsToSprint = keyboard.leftShiftKey.isPressed && input.sqrMagnitude > 0f;

        if (wantsToSprint && !isSprinting && currentStamina > minStaminaToStartSprint)
        {
            isSprinting = true;
        }

        if (isSprinting && (!wantsToSprint || currentStamina <= 0f))
        {
            isSprinting = false;
        }

        if (isSprinting)
        {
            currentStamina = Mathf.Max(0f, currentStamina - staminaDrainPerSecond * Time.deltaTime);
        }
        else
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenPerSecond * Time.deltaTime);
        }

        float speed = isSprinting ? sprintSpeed : moveSpeed;
        Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        verticalVelocity += gravity * Time.deltaTime;

        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }

    private void OnGUI()
    {
        if (!isSprinting) return;

        float barWidth = 220f;
        float barHeight = 18f;
        float x = Screen.width / 2f - barWidth / 2f;
        float y = Screen.height - 60f;

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), Texture2D.whiteTexture);

        float fillRatio = currentStamina / maxStamina;
        GUI.color = Color.yellow;
        GUI.DrawTexture(new Rect(x, y, barWidth * fillRatio, barHeight), Texture2D.whiteTexture);

        GUI.color = Color.white;
    }
}
