using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private const float lookThreshold = 0.01f;

    private CharacterController controller;

    [SerializeField] private bool canControl = true;

    [Header("Cinemachine")]
    [SerializeField]
    private Transform cinemachineCameraTarget;

    [SerializeField]
    private float topClamp = 70.0f;

    [SerializeField]
    private float bottomClamp = -30.0f;

    [Header("Speed")]

    [SerializeField]
    private float moveSpeed = 8f;

    [SerializeField] private float mouseLookSensitivity = 1f;      // multiplier

    [SerializeField] private float stickLookDegreesPerSecond = 180f; // degrees/sec at full tilt

    [SerializeField] private float gravityValue = -9.81f;

    [SerializeField] private float rotationFactor = 1.0f;

    [Header("Jump")]
    [SerializeField]
    private float jumpForce = 5f;

    [SerializeField] private float coyoteTime = 0.12f;      // seconds after leaving ground you can still jump
    [SerializeField] private float jumpBufferTime = 0.12f;  // seconds before landing jump input is remembered

    private float coyoteTimer;
    private float jumpBufferTimer;

    private Vector2 moveInput;
    private Vector3 currentMovement;
    private float yaw;
    private float pitch;
    private Vector2 lookMouse;
    private Vector2 lookStick;
    private Vector3 playerVelocity;
    private bool groundedPlayer;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        SetCursorLocked(true);
    }

    private void Update()
    {
        if (cinemachineCameraTarget == null)
        {
            Debug.LogError($"{name} has NO cinemachineCameraTarget assigned!");
            return;
        }

        if (!canControl) return;

        HandleMovement();
        HandleRotation();

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);

        if (!Cursor.visible && Mouse.current.leftButton.wasPressedThisFrame)
            SetCursorLocked(true);
    }

    public void SetCameraTarget(Transform target)
    {
        cinemachineCameraTarget = target;
    }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public void SetCanControl(bool enabled)
    {
        canControl = enabled;

        // Stop drift when disabling
        moveInput = Vector2.zero;
        currentMovement = Vector3.zero;
        playerVelocity = Vector3.zero;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
    }

    private void HandleMovement()
    {
        if (controller == null || !controller.enabled) return;

        groundedPlayer = controller.isGrounded;

        // Ground snap / stable grounding
        if (groundedPlayer && playerVelocity.y < -2f)
            playerVelocity.y = -2f;

        // --- Timers ---
        // Coyote: refresh while grounded, count down while airborne
        if (groundedPlayer) coyoteTimer = coyoteTime;
        else coyoteTimer -= Time.deltaTime;

        // Jump buffer: count down after jump pressed
        jumpBufferTimer -= Time.deltaTime;

        // --- Consume buffered jump if allowed (buffer + coyote) ---
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            // perform jump
            playerVelocity.y = Mathf.Sqrt(jumpForce * -2f * gravityValue);

            // consume both so it only happens once
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        // Input (local)
        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        input = Vector3.ClampMagnitude(input, 1f);

        // Camera-relative directions (flattened)
        Vector3 camForward = cinemachineCameraTarget.forward;
        Vector3 camRight = cinemachineCameraTarget.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        // Convert input to world-space relative to camera
        currentMovement = camRight * input.x + camForward * input.z;

        // Gravity
        playerVelocity.y += gravityValue * Time.deltaTime;

        // Move (horizontal + vertical)
        Vector3 finalMove = currentMovement * moveSpeed + Vector3.up * playerVelocity.y;
        controller.Move(finalMove * Time.deltaTime);
    }


    private void HandleRotation()
    {
        if (controller == null || !controller.enabled) return;

        if (currentMovement.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(currentMovement, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationFactor * Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        if (!canControl) return; 
        Look();
    }


    private void Look()
    {
        // Mouse: per-frame delta (no deltaTime needed)
        if (lookMouse.sqrMagnitude >= lookThreshold)
        {
            yaw += lookMouse.x * mouseLookSensitivity;
            pitch -= lookMouse.y * mouseLookSensitivity;
        }

        // Stick: normalized [-1..1] (use degrees/sec)
        if (lookStick.sqrMagnitude >= lookThreshold)
        {
            yaw += lookStick.x * stickLookDegreesPerSecond * Time.deltaTime;
            pitch -= lookStick.y * stickLookDegreesPerSecond * Time.deltaTime;
        }

        yaw = ClampAngle(yaw, float.MinValue, float.MaxValue);
        pitch = ClampAngle(pitch, bottomClamp, topClamp);

        cinemachineCameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    public void SyncLookFromTarget()
    {
        if (cinemachineCameraTarget == null) return;

        Vector3 e = cinemachineCameraTarget.rotation.eulerAngles;

        // Unity eulers are 0..360, convert pitch to -180..180 for clamping consistency
        yaw = e.y;

        float p = e.x;
        if (p > 180f) p -= 360f;
        pitch = Mathf.Clamp(p, bottomClamp, topClamp);

        // Clear deltas so we don’t “jump” from old input
        lookMouse = Vector2.zero;
        lookStick = Vector2.zero;
    }


    private float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }

    public void SetMoveInput(Vector2 v) => moveInput = v;
    public void SetLookMouse(Vector2 v) => lookMouse = v;
    public void SetLookStick(Vector2 v) => lookStick = v;
    public void QueueJump() => jumpBufferTimer = jumpBufferTime;

}
