using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    // Scene reference
    public RoomController room;

    // Health and death state
    public int maxHealth = 3;
    public double Current { get; private set; }
    private bool dead;

    // Health bar and damage flash
    public SpriteRenderer visual;
    public Transform healthFill;
    private float flashUntil;

    private void Awake()
    {
        Current = maxHealth;
    }

#if UNITY_EDITOR
    public void SetEditorHealthPercent(float percent)
    {
        if (dead || room == null || room.Phase != RoomPhase.Fighting || float.IsNaN(percent))
            return;

        // Never silently resurrect or kill an enemy; death still uses TakeDamage.
        Current = maxHealth * (double)Mathf.Clamp(percent, 1f, 100f) / 100d;
        flashUntil = 0;
    }
#endif

    public void SetStartingHealth(int value)
    {
        maxHealth = Mathf.Max(1, value);
        Current = maxHealth;
        dead = false;

        flashUntil = 0;
    }

    public void TakeDamage(double amount)
    {
        if (dead || room == null || !room.IsFighting || amount <= 0 || double.IsNaN(amount) || double.IsInfinity(amount))
            return;

        Current = System.Math.Max(0, Current - amount);

        // Repeated thirds/fifths must not leave an invisible sliver of health.
        if (Current < 0.000000001d)
            Current = 0;

        flashUntil = Time.time + 0.09f;

        if (Current == 0)
        {
            dead = true;

            GameSfx.Play(GetComponent<BossController>() != null ? SoundCue.BossDeath : SoundCue.EnemyDeath);

            gameObject.SetActive(false);

            room.EnemyDefeated(this);

            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (visual != null)
            visual.color = Time.time < flashUntil ? new Color(1f, 0.35f, 0.25f) : Color.white;

        if (healthFill != null)
            healthFill.localScale = new Vector3((float)Current / maxHealth, 1, 1);
    }
}
