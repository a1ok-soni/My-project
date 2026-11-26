using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class Movement : MonoBehaviour
{
    [Header("Input Actions")]
    public InputActionAsset playerControls;

    private InputAction moveAction;
    private InputAction lookAction;

    [Header("Movement")]
    public CharacterController controller;
    public float playerSpeed = 5f;

    [Header("Witch Escape Boost")]
    public float boostedSpeed = 9f;
    public float boostDuration = 5f;

    private float boostTimer = 0f;
    private bool isSpeedBoosted = false;
    public float slowWalkSpeed = 2f;
    public float runSpeed = 8f;
    public float gravity = -9.81f;

    [Header("Camera")]
    public Transform cameraPivot;
    public float mouseSensitivity = 0.1f;
    public float maxLookAngle = 85f;

    private Vector3 velocity;
    private float pitch;

    [Header("Stun Settings")]
    private bool isStunned = false;
    private float stunTimer = 0f;

    [Header("Footstep Sounds")]
    public AudioSource footstepAudioSource;
    public AudioClip[] footstepClips;

    [Header("Jump Sound")]
    public AudioClip jumpClip;

    public float walkStepInterval = 0.5f;
    public float runStepInterval = 0.3f;

    private float footstepTimer = 0f;

    // NEW: AI HEARING SYSTEM
    [Header("AI Hearing Settings")]
    public float walkNoiseRadius = 7f;
    public float runNoiseRadius = 13f;
    [Header("Jump Sound")]
    public float jumpNoiseRadius = 10f;

    public bool hasKey = false;

    // Event sent whenever the player makes a footstep
    public static event Action<Vector3, float> OnPlayerFootstep;

    private void Awake()
    {
        moveAction = playerControls.FindAction("Player/Move");
        lookAction = playerControls.FindAction("Player/Look");

        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (footstepAudioSource == null)
            footstepAudioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        lookAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        lookAction?.Disable();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (footstepAudioSource != null)
        {
            footstepAudioSource.playOnAwake = false;
            footstepAudioSource.loop = false;
        }
    }

    // Called by Scout when it stuns the Player
    public void ApplyStun(float duration)
    {
        isStunned = true;
        stunTimer = Mathf.Max(stunTimer, duration);

        velocity.x = 0f;
        velocity.z = 0f;

        footstepTimer = 0f;

        if (footstepAudioSource != null)
            footstepAudioSource.Stop();
    }

    private void Update()
    {
        if (isSpeedBoosted)
        {
            boostTimer -= Time.deltaTime;

            if (boostTimer <= 0f)
            {
                isSpeedBoosted = false;
                boostTimer = 0f;
                Debug.Log("Witch escape speed boost ended.");
            }
        }
        // STUN CHECK
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;

            if (stunTimer <= 0f)
            {
                isStunned = false;
                stunTimer = 0f;
            }

            bool grounded = controller.isGrounded;

            if (grounded && velocity.y < 0f)
                velocity.y = -2f;

            velocity.y += gravity * Time.deltaTime;

            controller.Move(
                Vector3.up * velocity.y * Time.deltaTime
            );

            footstepTimer = 0f;
            return;
        }

        // CAMERA LOOK
        Vector2 lookInput = lookAction.ReadValue<Vector2>();

        transform.Rotate(
            Vector3.up,
            lookInput.x * mouseSensitivity
        );

        pitch -= lookInput.y * mouseSensitivity;

        pitch = Mathf.Clamp(
            pitch,
            -maxLookAngle,
            maxLookAngle
        );

        cameraPivot.localRotation =
            Quaternion.Euler(pitch, 0f, 0f);

        // GROUND CHECK
        bool isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        // PLAYER MOVEMENT
        Vector2 input = moveAction.ReadValue<Vector2>();

        Vector3 move =
            transform.right * input.x +
            transform.forward * input.y;

        move = Vector3.ClampMagnitude(move, 1f);

        // CHECK SHIFT AND CTRL
        bool isSlowWalking =
            Keyboard.current != null &&
            Keyboard.current.leftShiftKey.isPressed;

        bool isRunning =
            Keyboard.current != null &&
            Keyboard.current.leftCtrlKey.isPressed &&
            !isSlowWalking;

        // SELECT MOVEMENT SPEED
        float currentSpeed = playerSpeed;

        if (isSlowWalking)
            currentSpeed = slowWalkSpeed;
        else if (isRunning)
            currentSpeed = runSpeed;

        // Witch escape boost overrides normal movement speeds
        if (isSpeedBoosted)
        {
            currentSpeed = boostedSpeed;
        }

        Vector3 previousPosition = transform.position;

        controller.Move(
            move * currentSpeed * Time.deltaTime
        );


        // GRAVITY
        velocity.y += gravity * Time.deltaTime;

        controller.Move(
            velocity * Time.deltaTime
        );

        // FOOTSTEP SOUND SYSTEM
        Vector3 displacement =
            transform.position - previousPosition;

        displacement.y = 0f;

        bool isMoving = input.sqrMagnitude > 0.01f;

        bool isOnGround = controller.isGrounded;

        if (isMoving && isOnGround && !isSlowWalking)
        {
            
            float stepInterval = isRunning
                ? runStepInterval
                : walkStepInterval;

            footstepTimer -= Time.deltaTime;

            if (footstepTimer <= 0f)
            {
                // Play footstep audio
                PlayFootstep();

                // NEW: SEND NOISE SIGNAL TO ZOMBIE AI
                float noiseRadius = isRunning
                    ? runNoiseRadius
                    : walkNoiseRadius;

                OnPlayerFootstep?.Invoke(
                    transform.position,
                    noiseRadius
                );

                footstepTimer = Mathf.Max(
                    0.05f,
                    stepInterval
                );
            }
        }
        else
        {
            footstepTimer = walkStepInterval;
        }
    }

    public void ActivateSpeedBoost()
    {
        isSpeedBoosted = true;
        boostTimer = boostDuration;

        Debug.Log("Witch screamed! Player speed boosted!");
    }

    private void PlayFootstep()
    {
        if (footstepAudioSource == null ||
            footstepClips == null ||
            footstepClips.Length == 0)
        {
            return;
        }

        int randomIndex = UnityEngine.Random.Range(
            0,
            footstepClips.Length
        );

        AudioClip selectedClip = footstepClips[randomIndex];

        if (selectedClip != null)
        {
            footstepAudioSource.PlayOneShot(selectedClip);
        }
    }
}