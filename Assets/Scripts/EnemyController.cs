using System.Collections.Generic;
using UnityEngine;

public enum EnemyKind
{
    Crawler,
    Shooter,
    Charger
}

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class EnemyController : MonoBehaviour
{
    // Enemy type and scene references
    public EnemyKind kind;
    public RoomController room;
    public PlayerHealth player;

    // Movement and wall detection
    public float speed = 2f;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private ContactFilter2D wallFilter;
    private readonly List<RaycastHit2D> wallHits = new List<RaycastHit2D>(8);
    private int walls;

    // Shared attack timing and state
    public float attackInterval = 1.8f;
    private const int MovingState = 0;
    private const int WarningState = 1;
    private const int ChargingState = 2;
    private float attackAt;
    private float stateUntil;
    private int attackState;

    // Shooter projectiles and windup
    public Projectile projectilePrefab;
    private const float ShotWindup = 0.5f;
    private const float ShotOffset = 0.55f;

    // Charger movement
    private const float ChargeSpeed = 9f;
    private const float ChargeDuration = 0.55f;
    private Vector2 chargeDirection;

    // Attack warnings: Shooter sparks and Charger lane
    public SpriteRenderer warning;
    private SpriteRenderer[] chargeSparks;
    private ChargeTelegraph chargeTelegraph;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();

        walls = LayerMask.GetMask("Walls");
        wallFilter = new ContactFilter2D { useTriggers = false };
        wallFilter.SetLayerMask(walls);

        attackAt = Time.time + Random.Range(0.8f, 1.8f);

        CreateChargeSparks();

        if (kind == EnemyKind.Charger)
            chargeTelegraph = new ChargeTelegraph(warning, bodyCollider, 0.16f);
    }

    private void FixedUpdate()
    {
        if (room == null || !room.EnemiesCanAct || player == null)
        {
            body.linearVelocity = Vector2.zero;

            if (room == null || !room.Paused)
            {
                if (warning != null)
                    warning.enabled = false;

                HideChargeSparks();
                chargeTelegraph?.Hide();
            }

            return;
        }

        Vector2 delta = (Vector2)player.transform.position - body.position;
        Vector2 direction = delta.normalized;

        if (warning != null && kind != EnemyKind.Charger)
            warning.enabled = attackState == WarningState;

        if (kind == EnemyKind.Charger && attackState != WarningState)
            chargeTelegraph?.Hide();

        if (kind == EnemyKind.Crawler)
            body.linearVelocity = Navigate(direction) * speed;
        else if (kind == EnemyKind.Shooter)
        {
            UpdateShooter(direction, delta.magnitude);
        }
        else
        {
            UpdateCharger(direction);
        }
    }

    private void UpdateShooter(Vector2 direction, float distanceToPlayer)
    {
        bool canSee = !Physics2D.Linecast(body.position, player.transform.position, walls);

        // Approach until in range, then keep a gap instead of touching the player.
        Vector2 movementDirection = Vector2.zero;

        if (!canSee || distanceToPlayer > 5.5f)
            movementDirection = direction;
        else if (distanceToPlayer < 3.5f)
            movementDirection = -direction;

        body.linearVelocity = Navigate(movementDirection) * speed;

        if (attackState == MovingState && Time.time >= attackAt && canSee)
        {
            attackState = WarningState;
            stateUntil = Time.time + ShotWindup;
        }

        if (attackState == WarningState)
        {
            body.linearVelocity = Vector2.zero;

            if (Time.time >= stateUntil)
            {
                Fire(direction, RunState.CurrentFloor >= 4 ? RunState.DeepProjectileSpeed : 5.3f);

                attackState = MovingState;
                attackAt = Time.time + attackInterval;
            }
        }

        UpdateChargingOrb(direction);
    }

    private void UpdateCharger(Vector2 direction)
    {
        if (attackState == MovingState)
        {
            body.linearVelocity = Navigate(direction) * speed;

            if (Time.time >= attackAt && !Physics2D.Linecast(body.position, player.transform.position, walls))
            {
                chargeDirection = direction;
                stateUntil = Time.time + 0.65f;
                attackState = WarningState;

                chargeTelegraph?.Show(chargeDirection, ChargeSpeed * ChargeDuration);
            }
        }
        else if (attackState == WarningState)
        {
            body.linearVelocity = Vector2.zero;

            chargeTelegraph?.Show(chargeDirection, ChargeSpeed * ChargeDuration);

            if (Time.time >= stateUntil)
            {
                attackState = ChargingState;
                stateUntil = Time.time + ChargeDuration;

                chargeTelegraph?.Hide();
                GameSfx.Play(SoundCue.EnemyCharge);
            }
        }
        else
        {
            float nextStep = ChargeSpeed * Time.fixedDeltaTime + 0.02f;

            if (Time.time >= stateUntil || WallAhead(chargeDirection, nextStep))
            {
                attackState = MovingState;
                attackAt = Time.time + attackInterval;
                body.linearVelocity = Vector2.zero;

                if (warning != null)
                    warning.enabled = false;
            }
            else
                body.linearVelocity = chargeDirection * ChargeSpeed;
        }
    }

    private void UpdateChargingOrb(Vector2 direction)
    {
        if (warning == null)
        {
            HideChargeSparks();
            return;
        }

        warning.enabled = attackState == WarningState;

        if (!warning.enabled)
        {
            HideChargeSparks();
            return;
        }

        float progress = Mathf.Clamp01(1f - (stateUntil - Time.time) / ShotWindup);

        // Let the sparks arrive first, then build up the bright projectile core.
        float formation = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.92f, progress));
        float size = Mathf.Lerp(0.06f, 0.4f, formation);

        warning.transform.position = transform.position + (Vector3)(direction * ShotOffset);
        warning.transform.localScale = new Vector3(size, size, 1f);
        warning.color = Color.Lerp(new Color(1f, 0.35f, 0.2f, 0.35f),
            new Color(1f, 0.85f, 0.45f, 1f), formation);

        if (chargeSparks == null)
            return;

        float aimAngle = Mathf.Atan2(direction.y, direction.x);

        for (int i = 0; i < chargeSparks.Length; i++)
        {
            var spark = chargeSparks[i];

            float travel = Mathf.Clamp01(progress / (0.7f + i * 0.045f));
            spark.enabled = travel < 1f;

            if (!spark.enabled)
                continue;

            // Unevenly spaced spirals converge on the muzzle, not around the mob.
            float angle = aimAngle + i * 2.399963f + travel * 2.5f;
            float radius = (0.38f + (i % 3) * 0.055f) * (1f - travel);

            spark.transform.position = warning.transform.position +
                new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;

            float sparkSize = Mathf.Lerp(0.07f + (i % 3) * 0.025f, 0.035f, travel);
            spark.transform.localScale = new Vector3(sparkSize, sparkSize, 1);
            spark.color = Color.Lerp(new Color(1f, 0.4f, 0.12f, 0.85f),
                new Color(1f, 0.85f, 0.45f, 1f), travel);
        }
    }

    private void CreateChargeSparks()
    {
        if (kind != EnemyKind.Shooter || warning == null || chargeSparks != null)
            return;

        // Reuse these visual-only particles for every shot; no per-shot spawning.
        chargeSparks = new SpriteRenderer[7];

        for (int i = 0; i < chargeSparks.Length; i++)
        {
            var sparkObject = new GameObject("Charge spark " + (i + 1));
            sparkObject.layer = warning.gameObject.layer;
            sparkObject.transform.SetParent(transform, false);

            var spark = sparkObject.AddComponent<SpriteRenderer>();
            spark.sprite = warning.sprite;
            spark.sharedMaterial = warning.sharedMaterial;
            spark.sortingLayerID = warning.sortingLayerID;
            spark.sortingOrder = warning.sortingOrder - 1;
            spark.enabled = false;

            chargeSparks[i] = spark;
        }
    }

    private void HideChargeSparks()
    {
        if (chargeSparks == null)
            return;

        foreach (var spark in chargeSparks)
            if (spark != null)
                spark.enabled = false;
    }

    private void OnDisable()
    {
        if (warning != null)
            warning.enabled = false;

        HideChargeSparks();
        chargeTelegraph?.Hide();
    }

    private Vector2 Navigate(Vector2 direction)
    {
        if (direction == Vector2.zero || !WallAhead(direction, 0.7f))
            return direction;

        Vector2 left = new Vector2(-direction.y, direction.x);

        if (!WallAhead(left, 0.7f))
            return left;

        if (!WallAhead(-left, 0.7f))
            return -left;

        if (!WallAhead(-direction, 0.7f))
            return -direction;

        return Vector2.zero;
    }

    private bool WallAhead(Vector2 direction, float distance)
    {
        // Use the real shape: an oversized probe can already overlap a nearby wall.
        int count = bodyCollider.Cast(direction, wallFilter, wallHits, distance);

        for (int i = 0; i < count; i++)
        {
            var hit = wallHits[i];

            if (hit.distance <= 0.001f)
            {
                // Start-overlap casts return an artificial normal opposite the cast.
                // Check the actual wall instead, allowing movement away or alongside it.
                Vector2 center = bodyCollider.bounds.center;
                Vector2 away = center - hit.collider.ClosestPoint(center);

                if (away.sqrMagnitude < 0.000001f)
                {
                    var separation = bodyCollider.Distance(hit.collider);

                    if (separation.isValid)
                        away = separation.normal * (separation.isOverlapped ? -1f : 1f);
                }

                if (away.sqrMagnitude > 0.000001f && Vector2.Dot(direction, away.normalized) >= -0.001f)
                    continue;
            }

            return true;
        }

        return false;
    }

    private void Fire(Vector2 direction, float velocity)
    {
        GameSfx.Play(SoundCue.EnemyShot);

        var shot = Instantiate(projectilePrefab, transform.position + (Vector3)(direction * ShotOffset), Quaternion.identity);
        shot.Launch(room, direction, velocity, 1, false);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        collision.collider.GetComponent<PlayerHealth>()?.TakeDamage(1, true);
    }
}
