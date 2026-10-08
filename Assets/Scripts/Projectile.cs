using UnityEngine;

public class Projectile : MonoBehaviour
{
    // Room and visual references
    public SpriteRenderer visual;
    private RoomController room;

    // Movement and lifetime
    public float lifetime = 4f;
    private Vector2 velocity;

    // Damage and allegiance
    private double damage;
    private bool friendly;
    public bool IsHostile => !friendly;

    // Collision checks and impact state
    public float radius = 0.09f;
    private int mask;
    private bool impacted;

    public void Launch(RoomController owner, Vector2 direction, float speed, double power, bool fromPlayer)
    {
        room = owner;
        velocity = direction.normalized * speed;
        damage = power;
        friendly = fromPlayer;

        mask = LayerMask.GetMask("Walls", friendly ? "Enemies" : "Player");

        if (visual != null)
            visual.color = friendly ? new Color(0.45f, 1f, 0.88f) : new Color(1f, 0.35f, 0.2f);

        transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    private void FixedUpdate()
    {
        if (impacted)
            return;

        if (room == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!room.IsFighting)
            return;

        lifetime -= Time.fixedDeltaTime;

        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float distance = velocity.magnitude * Time.fixedDeltaTime;

        // Check the whole next movement step so fast shots cannot skip thin walls.
        RaycastHit2D hit = Physics2D.CircleCast(transform.position, radius, velocity.normalized, distance, mask);

        if (hit.collider != null)
        {
            impacted = true;
            GameSfx.Play(SoundCue.ShotImpact);

            if (friendly)
                hit.collider.GetComponent<EnemyHealth>()?.TakeDamage(damage);
            // Hostile attacks still deal whole HP; only player projectiles split damage.
            else
                hit.collider.GetComponent<PlayerHealth>()?.TakeDamage((int)damage);

            Destroy(gameObject);

            return;
        }

        transform.position += (Vector3)(velocity * Time.fixedDeltaTime);
    }
}
