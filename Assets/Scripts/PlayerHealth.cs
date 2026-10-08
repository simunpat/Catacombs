using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    // Scene references
    public RoomController room;
    public PlayerMovement movement;

    // Damage and dash feedback
    public SpriteRenderer bodySprite;

    // Temporary immunity after taking damage
    public float invulnerability = 0.85f;
    private float immuneUntil;

    public void TakeDamage(int damage, bool melee = false)
    {
#if UNITY_EDITOR
        if (RunState.EditorInvincible)
            return;
#endif

        // Dashing and the short grace period after a hit both prevent damage.
        if (damage <= 0 || RunState.Health <= 0 || room == null || !room.EnemiesCanAct || movement.IsDashing || Time.time < immuneUntil)
            return;

        RunState.Health = Mathf.Max(0, RunState.Health - damage);

        immuneUntil = Time.time + invulnerability;

        if (melee)
            GameSfx.Play(SoundCue.MeleeImpact);

        GameSfx.Play(SoundCue.PlayerHurt);

        if (RunState.Health == 0)
        {
            GameSfx.Play(SoundCue.PlayerDeath);
            room.Lose();
        }
    }

    private void Update()
    {
        if (bodySprite == null)
            return;

        bodySprite.color = Time.time < immuneUntil
            ? (Mathf.Sin(Time.time * 45) > 0 ? new Color(1, 0.35f, 0.3f) : Color.white)
            : movement.IsDashing ? new Color(0.4f, 1f, 1f) : Color.white;
    }
}
