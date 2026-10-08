using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerWeapon : MonoBehaviour
{
    // Room and aiming references
    public RoomController room;
    public PlayerMovement movement;

    // Projectile settings
    public Projectile projectilePrefab;
    public float projectileSpeed = 17f;

    // Shot count and firing cooldown
    public int ShotsFired { get; private set; }
    private float nextShot;

    private void Update()
    {
        if (room == null || !room.IsFighting || Mouse.current == null || !Mouse.current.leftButton.isPressed || Time.time < nextShot)
            return;

        nextShot = Time.time + RunState.FireInterval;
        ShotsFired++;

        GameSfx.Play(SoundCue.PlayerShot);

        FireVolley();
    }

    private void FireVolley()
    {
        // Centre the spread around the mouse aim. Damage is shared by the volley.
        for (int i = 0; i < RunState.Projectiles; i++)
        {
            float angle = (i - (RunState.Projectiles - 1) * 0.5f) * 12f;

            Vector2 aim = Quaternion.Euler(0, 0, angle) * movement.Aim;

            Projectile shot = Instantiate(projectilePrefab, transform.position + (Vector3)(aim * 0.48f), Quaternion.identity);

            shot.Launch(room, aim, projectileSpeed, RunState.DamagePerProjectile, true);
        }
    }
}
