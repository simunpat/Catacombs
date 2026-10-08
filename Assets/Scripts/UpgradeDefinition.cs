using UnityEngine;

public enum UpgradeKind
{
    Damage,
    FireRate,
    Vitality,
    Speed,
    Spread,
    Dash
}

[CreateAssetMenu(menuName = "Catacombs/Opgradering")]
public class UpgradeDefinition : ScriptableObject
{
    // Upgrade text and appearance
    public string title;
    [TextArea] public string description;
    public Color color = Color.white;

    // Upgrade effect and availability
    public UpgradeKind kind;
    public bool Available => kind != UpgradeKind.Spread || RunState.Projectiles < 5;

    public void Apply()
    {
        switch (kind)
        {
            case UpgradeKind.Damage:
                RunState.Damage++;
                break;

            case UpgradeKind.FireRate:
                RunState.FireInterval *= 0.8f;
                break;

            case UpgradeKind.Vitality:
                RunState.Health = Mathf.Min(RunState.MaxHealth, RunState.Health + 2);
                break;

            case UpgradeKind.Speed:
                RunState.Speed *= 1.12f;
                break;

            case UpgradeKind.Spread:
                RunState.Projectiles = Mathf.Min(5, RunState.Projectiles + 1);
                break;

            case UpgradeKind.Dash:
                RunState.DashCooldown = Mathf.Max(0.4f, RunState.DashCooldown * 0.8f);
                break;
        }

        RunState.Upgrades.Add(title);
    }
}
