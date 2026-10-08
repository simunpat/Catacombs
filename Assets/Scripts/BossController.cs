using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BossController : MonoBehaviour
{
    // Scene references
    public RoomController room;
    public PlayerHealth player;
    public EnemyHealth health;

    // Floor difficulty
    public int extraHealthPerFloor = 75;
    private bool floorConfigured;

    // Movement and wall detection
    public float moveSpeed = 1.3f;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private ContactFilter2D wallFilter;
    private readonly List<RaycastHit2D> wallHits = new List<RaycastHit2D>(8);
    private int walls;

    // Shooting: timing, aim and projectile patterns
    public Projectile projectilePrefab;
    public float attackInterval = 1.8f;
    private const float ShotOffset = 0.9f;
    private float nextAttack;
    private float fireAt;
    private float windupDuration = 0.85f;
    private bool windingUp;
    private Vector2 lockedAim;
    private int rangedAttacks;
    private bool ringAttack;
    private int ringShots = 12;
    private int fanHalf = 2;
    private float projectileScale = 1f;
    private bool fastProjectiles;

    // Shooting warning: gathering orbs and sparks
    public SpriteRenderer warning;
    private SpriteRenderer[] chargeOrbs;
    private SpriteRenderer[] chargeSparks;

    // Charging: speed, timing and attack cycle
    public float chargeSpeed = 8f;
    public float chargeWindup = 0.9f;
    public float chargeDuration = 0.65f;
    public float chargeRecovery = 1.1f;
    public bool CanCharge { get; private set; }
    public bool IsCharging => chargeState == ChargingState;

    private const int RangedAttackState = 0;
    private const int ChargeWarningState = 1;
    private const int ChargingState = 2;
    private const int ChargeRecoveryState = 3;
    private int chargeState;
    private float chargeUntil;
    private Vector2 chargeDirection;

    // Charging warning: lane and directional arrow
    public SpriteRenderer chargeWarningTemplate;
    private SpriteRenderer chargeLane;
    private ChargeTelegraph chargeTelegraph;

    // Beam and pulse attacks
    public Material pulseMaterial;
    private BossHazards hazards;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();

        walls = LayerMask.GetMask("Walls");
        wallFilter = new ContactFilter2D { useTriggers = false };
        wallFilter.SetLayerMask(walls);

        nextAttack = Time.time + 2f;

        if (chargeWarningTemplate != null)
        {
            chargeLane = Instantiate(chargeWarningTemplate, transform, false);
            chargeLane.name = "Charge warning";
            chargeTelegraph = new ChargeTelegraph(chargeLane, bodyCollider, 0.35f);
        }

        CreateChargeVisuals();
    }

    public void ConfigureForFloor(int floor)
    {
        if (floorConfigured)
            return;

        floorConfigured = true;

        // Apply once: scene reloads must not multiply the boss's health or speed.
        int depth = Mathf.Max(0, floor - 1);

        health.SetStartingHealth(health.maxHealth - 20 + depth * extraHealthPerFloor);
        attackInterval = Mathf.Max(0.9f, attackInterval + 0.5f - depth * 0.35f);
        windupDuration = Mathf.Max(0.7f, 1.1f - depth * 0.1f);

        if (floor >= 4)
        {
            // Late-floor bosses attack sooner, but retain their post-charge recovery.
            int lateDepth = floor - 4;

            attackInterval = Mathf.Max(0.65f, 0.85f - lateDepth * 0.2f);
            windupDuration = Mathf.Max(0.45f, 0.55f - lateDepth * 0.1f);
            chargeWindup = Mathf.Max(0.55f, 0.65f - lateDepth * 0.1f);
        }

        ringShots = Mathf.Min(12, 8 + depth * 2);
        fanHalf = floor == 1 ? 1 : floor < 4 ? 2 : 3;
        projectileScale = Mathf.Min(1.15f, 0.8f + depth * 0.09f);
        fastProjectiles = floor >= 3;
        CanCharge = floor >= 2 && chargeWarningTemplate != null;

        if (floor >= 3)
        {
            hazards = gameObject.AddComponent<BossHazards>();
            hazards.Initialize(this, floor);
        }
    }

    private void FixedUpdate()
    {
        if (room == null || !room.IsFighting || player == null)
        {
            body.linearVelocity = Vector2.zero;

            if (room == null || !room.Paused)
                HideChargeVisuals();

            return;
        }

        if (hazards != null && hazards.Tick(!windingUp && chargeState == RangedAttackState))
        {
            body.linearVelocity = Vector2.zero;
            HideChargeVisuals();
            nextAttack = Time.time + attackInterval;
            return;
        }

        // A beam hit can end the fight before the normal attack cycle runs.
        if (!room.IsFighting)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 direction = ((Vector2)player.transform.position - body.position).normalized;

        if (chargeState != RangedAttackState)
        {
            UpdateCharge();
            return;
        }

        body.linearVelocity = windingUp ? Vector2.zero : Navigate(direction) * Mathf.Min(moveSpeed, RunState.Speed * 0.8f);

        if (!windingUp && Time.time >= nextAttack)
        {
            if (CanCharge && rangedAttacks >= 2 && !Physics2D.Linecast(body.position, player.transform.position, walls)
                && !WallAhead(direction, 0.5f))
            {
                chargeDirection = direction;
                chargeState = ChargeWarningState;
                chargeUntil = Time.time + chargeWindup;
                body.linearVelocity = Vector2.zero;

                HideChargeVisuals();
                ShowChargePath();
                return;
            }

            windingUp = true;
            lockedAim = direction;

            fireAt = Time.time + windupDuration;
        }

        if (!windingUp)
            return;

        bool enraged = health.Current <= health.maxHealth / 2d;

        if (Time.time < fireAt)
        {
            UpdateChargeVisuals(enraged);
            return;
        }

        FireRangedAttack(enraged);
        ringAttack = !ringAttack;
        rangedAttacks++;

        windingUp = false;
        HideChargeVisuals();

        nextAttack = Time.time + attackInterval * (enraged ? 0.7f : 1f);
    }

    private void FireRangedAttack(bool enraged)
    {
        // Alternate a full ring with a fan aimed at the player's warning position.
        GameSfx.Play(SoundCue.EnemyShot);

        if (ringAttack)
        {
            for (int i = 0; i < ringShots; i++)
                Fire(RingDirection(i, enraged), 4.5f * projectileScale);
        }
        else
        {
            for (int i = -fanHalf; i <= fanHalf; i++)
                Fire(Quaternion.Euler(0, 0, i * 14f) * lockedAim, 6f * projectileScale);
        }
    }

    private void UpdateCharge()
    {
        body.linearVelocity = Vector2.zero;

        if (chargeState == ChargeWarningState)
        {
            ShowChargePath();

            if (Time.time < chargeUntil)
                return;

            HideChargeVisuals();
            chargeState = ChargingState;
            chargeUntil = Time.time + chargeDuration;

            GameSfx.Play(SoundCue.EnemyCharge);
        }

        if (chargeState == ChargingState)
        {
            if (Time.time >= chargeUntil || WallAhead(chargeDirection, chargeSpeed * Time.fixedDeltaTime + 0.03f))
                EndCharge();
            else
                body.linearVelocity = chargeDirection * chargeSpeed;
        }
        else if (chargeState == ChargeRecoveryState && Time.time >= chargeUntil)
        {
            chargeState = RangedAttackState;
            rangedAttacks = 0;
            nextAttack = Time.time + attackInterval;
        }
    }

    private void EndCharge()
    {
        body.linearVelocity = Vector2.zero;
        chargeState = ChargeRecoveryState;
        chargeUntil = Time.time + chargeRecovery;

        HideChargeVisuals();
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
        if (bodyCollider == null)
            return false;

        int count = bodyCollider.Cast(direction, wallFilter, wallHits, distance);

        for (int i = 0; i < count; i++)
        {
            var hit = wallHits[i];

            if (hit.distance <= 0.001f)
            {
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

    private void ShowChargePath()
    {
        chargeTelegraph?.Show(chargeDirection, chargeSpeed * chargeDuration);
    }

    private Vector2 RingDirection(int index, bool enraged)
    {
        float spacing = 360f / ringShots;
        return Quaternion.Euler(0, 0, index * spacing + (enraged ? spacing * 0.5f : 0)) * Vector2.up;
    }

    private void CreateChargeVisuals()
    {
        if (warning == null || chargeOrbs != null)
            return;

        // One shared pool serves both attacks; no particles are spawned per shot.
        chargeOrbs = new SpriteRenderer[12];
        chargeOrbs[0] = warning;

        for (int i = 1; i < chargeOrbs.Length; i++)
            chargeOrbs[i] = MakeChargeSprite("Burst orb " + i, warning.sortingOrder);

        chargeSparks = new SpriteRenderer[36];

        for (int i = 0; i < chargeSparks.Length; i++)
            chargeSparks[i] = MakeChargeSprite("Charge spark " + (i + 1), warning.sortingOrder - 1);

        HideChargeVisuals();
    }

    private SpriteRenderer MakeChargeSprite(string label, int sortingOrder)
    {
        var child = new GameObject(label);
        child.layer = warning.gameObject.layer;
        child.transform.SetParent(transform, false);

        var visual = child.AddComponent<SpriteRenderer>();
        visual.sprite = warning.sprite;
        visual.sharedMaterial = warning.sharedMaterial;
        visual.sortingLayerID = warning.sortingLayerID;
        visual.sortingOrder = sortingOrder;
        visual.enabled = false;

        return visual;
    }

    private void UpdateChargeVisuals(bool enraged)
    {
        if (warning == null || chargeOrbs == null)
        {
            HideChargeVisuals();
            return;
        }

        float progress = Mathf.Clamp01(1f - (fireAt - Time.time) / windupDuration);
        float formation = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.92f, progress));

        Color energy = ringAttack ? new Color(1f, 0.4f, 0.12f) : new Color(0.8f, 0.3f, 1f);
        Color bright = ringAttack ? new Color(1f, 0.85f, 0.45f) : new Color(1f, 0.8f, 1f);

        int orbCount = ringAttack ? ringShots : 1;
        int sparksPerOrb = ringAttack ? 3 : 7;

        HideChargeVisuals();

        for (int i = 0; i < orbCount; i++)
        {
            Vector2 direction = ringAttack ? RingDirection(i, enraged) : lockedAim;
            Vector3 centre = transform.position + (Vector3)(direction * ShotOffset);

            var orb = chargeOrbs[i];
            orb.enabled = true;
            orb.transform.position = centre;

            float size = Mathf.Lerp(0.04f, ringAttack ? 0.22f : 0.55f, formation);
            orb.transform.localScale = new Vector3(size, size, 1);

            Color coreColor = Color.Lerp(energy, bright, formation);
            coreColor.a = Mathf.Lerp(0.35f, 1f, formation);
            orb.color = coreColor;

            float aimAngle = Mathf.Atan2(direction.y, direction.x);

            for (int j = 0; j < sparksPerOrb; j++)
            {
                var spark = chargeSparks[i * sparksPerOrb + j];

                float arrival = Mathf.Lerp(0.7f, 0.97f, (float)j / (sparksPerOrb - 1));
                float travel = Mathf.Clamp01(progress / arrival);

                if (travel >= 1f)
                    continue;

                spark.enabled = true;

                float angle = aimAngle + j * 2.399963f + travel * 2.5f;
                float radius = (ringAttack ? 0.16f : 0.48f + (j % 3) * 0.055f) * (1f - travel);

                spark.transform.position = centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;

                float sparkSize = Mathf.Lerp(ringAttack ? 0.07f : 0.09f + (j % 3) * 0.025f, 0.025f, travel);
                spark.transform.localScale = new Vector3(sparkSize, sparkSize, 1);
                spark.color = Color.Lerp(energy, bright, travel);
            }
        }
    }

    private void HideChargeVisuals()
    {
        if (warning != null)
            warning.enabled = false;

        if (chargeOrbs != null)
            foreach (var orb in chargeOrbs)
                if (orb != null)
                    orb.enabled = false;

        if (chargeSparks != null)
            foreach (var spark in chargeSparks)
                if (spark != null)
                    spark.enabled = false;

        chargeTelegraph?.Hide();
    }

    private void OnDisable()
    {
        HideChargeVisuals();

        if (body != null)
            body.linearVelocity = Vector2.zero;
    }

    private void Fire(Vector2 direction, float speed)
    {
        var shot = Instantiate(projectilePrefab, transform.position + (Vector3)(direction * ShotOffset), Quaternion.identity);
        shot.Launch(room, direction, fastProjectiles ? RunState.DeepProjectileSpeed : speed, 1, false);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (hazards != null && hazards.PulseActive)
            return;

        collision.collider.GetComponent<PlayerHealth>()?.TakeDamage(1, true);

        if (IsCharging && room != null && room.IsFighting)
            EndCharge();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        OnCollisionStay2D(collision);
    }
}
