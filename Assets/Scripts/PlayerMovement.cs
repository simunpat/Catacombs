using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    // Room and physics references
    public RoomController room;
    private Rigidbody2D body;

    // Aiming and facing direction
    public Transform weapon;
    public SpriteRenderer bodySprite;
    public Vector2 Aim { get; private set; } = Vector2.up;
    private Camera view;

    // Walking and travelled distance
    public bool IsWalking { get; private set; }
    public float DistanceTravelled { get; private set; }
    private Vector2 direction;
    private Vector2 lastDirection = Vector2.up;
    private Vector2 previousPosition;

    // Dash settings, progress and cooldown
    public float dashSpeed = 18f;
    public float dashDuration = 0.16f;
    public bool IsDashing => dashRemaining > 0f;
    public int DashCount { get; private set; }
    public float DashReady => 1f - Mathf.Clamp01(dashCooldown / RunState.DashCooldown);
    private Vector2 dashDirection;
    private float dashRemaining;
    private float dashCooldown;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        view = Camera.main;

        previousPosition = body.position;
    }

    private void Update()
    {
        if (room == null || !room.CanMove)
        {
            IsWalking = false;
            direction = Vector2.zero;

            if (room == null || !room.Paused)
                dashRemaining = 0f;

            return;
        }

        dashRemaining = Mathf.Max(0f, dashRemaining - Time.deltaTime);
        dashCooldown = Mathf.Max(0f, dashCooldown - Time.deltaTime);

        ReadMovementInput();
        UpdateAim();

        if (weapon != null)
            weapon.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(Aim.y, Aim.x) * Mathf.Rad2Deg);

        if (bodySprite != null)
            bodySprite.flipX = Aim.x < 0;
    }

    private void ReadMovementInput()
    {
        Keyboard keys = Keyboard.current;

        direction = Vector2.zero;

        if (keys != null)
        {
            if (keys.wKey.isPressed || keys.upArrowKey.isPressed)
                direction.y++;

            if (keys.sKey.isPressed || keys.downArrowKey.isPressed)
                direction.y--;

            if (keys.aKey.isPressed || keys.leftArrowKey.isPressed)
                direction.x--;

            if (keys.dKey.isPressed || keys.rightArrowKey.isPressed)
                direction.x++;

            // Diagonal movement must not be faster than moving along one axis.
            direction = direction.normalized;

            if (direction != Vector2.zero)
                lastDirection = direction;

            if (keys.spaceKey.wasPressedThisFrame && room.CanDash && dashCooldown <= 0f)
            {
                // A dash keeps its starting direction, even if the player turns.
                dashDirection = lastDirection;
                dashRemaining = dashDuration;
                dashCooldown = RunState.DashCooldown;
                DashCount++;

                GameSfx.Play(SoundCue.PlayerDash);
            }
        }

        if (direction == Vector2.zero || IsDashing)
            IsWalking = false;
    }

    private void UpdateAim()
    {
        if (Mouse.current != null && view != null)
        {
            Vector3 point = view.ScreenToWorldPoint(new Vector3(Mouse.current.position.x.ReadValue(), Mouse.current.position.y.ReadValue(), -view.transform.position.z));
            Vector2 delta = point - transform.position;

            if (delta.sqrMagnitude > 0.01f)
                Aim = delta.normalized;
        }
    }

    private void FixedUpdate()
    {
        float distance = Vector2.Distance(previousPosition, body.position);

        IsWalking = room != null && room.CanMove && !IsDashing && direction != Vector2.zero && distance > 0.001f;
        DistanceTravelled += distance;
        previousPosition = body.position;

        if (room == null || !room.CanMove)
            body.linearVelocity = Vector2.zero;
        else if (IsDashing)
            body.linearVelocity = dashDirection * dashSpeed;
        else
            body.linearVelocity = direction * RunState.Speed;
    }

    public void PlaceAt(Vector2 position)
    {
        body.position = position;
        previousPosition = position;

        body.linearVelocity = Vector2.zero;
        direction = Vector2.zero;

        dashRemaining = dashCooldown = 0f;
        IsWalking = false;
    }
}
