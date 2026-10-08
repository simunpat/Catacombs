using System.Collections.Generic;
using UnityEngine;

// Holds the current run between scenes. Reset removes all progress and upgrades.
public static class RunState
{
    // Scene names and available room layouts
    public const string MainMenuScene = "MainMenu";
    public const string HallwayScene = "00-Hallway";
    public const string BossScene = "07-Warden-Chamber";
    public const string TutorialStartScene = "Tutorial-Arrival";
    public const string TutorialCombatScene = "Tutorial-Grave-Chamber";
    public static readonly IReadOnlyList<string> NormalRoomScenes = System.Array.AsReadOnly(new[]
    {
        "01-Pillar-Hall", "02-Ritual-Chamber", "03-Burial-Corridors", "04-Forgotten-Crossroads", "05-Empty-Ossuary", "06-Grave-Chamber"
    });

    // Run structure and overall progress
    public const int RoomsPerRun = 4; // Normal rooms per floor, excluding the boss.
    public const int TotalFloors = 5;
    public static bool Active { get; private set; }
    public static int CurrentFloor { get; private set; } = 1;
    public static int TotalRoomsCleared { get; private set; }
    public static int BossesDefeated { get; private set; }
    public static bool HasNextFloor => CurrentFloor < TotalFloors;

    // Current floor: room destinations, encounters and return door
    public static int ReturnDoorIndex { get; private set; } = -1;
    public static int CompletedCount => clearedRooms.Count;
    private static readonly HashSet<string> clearedRooms = new HashSet<string>();
    private static string[] doorDestinations;
    private static readonly Dictionary<string, FloorEncounter> encounters = new Dictionary<string, FloorEncounter>();

    // Boss unlock and reward state
    public static bool BossUnlocked => Active && CompletedCount == RoomsPerRun;
    public static bool BossDefeatedThisFloor { get; private set; }
    public static bool BossRewardClaimed { get; private set; }
    private static bool bossUnlockAcknowledged;
    private static readonly List<UpgradeDefinition> bossChoices = new List<UpgradeDefinition>();

    // Player health
    public static int Health;
    public static int MaxHealth;

    // Player shooting stats
    public static int Damage;
    public static int Projectiles;
    public static float FireInterval;

    // Damage is the shared budget for one volley, not for each projectile.
    public static double DamagePerProjectile => (double)Damage / Mathf.Max(1, Projectiles);

    // Player movement and collected upgrades
    public static float Speed;
    public static float DashCooldown;
    public static readonly List<string> Upgrades = new List<string>();

    // Projectile speed used by enemies on deeper floors
    public const float DeepProjectileSpeed = 7.5f;

    public static void EnsureStarted(string startingRoom = null)
    {
        if (Active)
            return;

        Reset();
        PrepareFloor(startingRoom);
        Active = true;
    }

#if UNITY_EDITOR
    public static bool EditorInvincible { get; set; }
    public const string EditorTestSceneKey = "Catacombs.Testing.PendingScene";
    public const string EditorTestFloorKey = "Catacombs.Testing.PendingFloor";

    public static bool IsEditorTestScene(string scene)
    {
        if (scene == BossScene || scene == HallwayScene)
            return true;

        foreach (string normalRoom in NormalRoomScenes)
            if (scene == normalRoom)
                return true;

        return false;
    }

    // Only the testing window queues this; ordinary scene entry is unchanged.
    public static void TryStartQueuedEditorTest(string scene)
    {
        if (Active || UnityEditor.SessionState.GetString(EditorTestSceneKey, "") != scene)
            return;

        int floor = UnityEditor.SessionState.GetInt(EditorTestFloorKey, 1);
        UnityEditor.SessionState.EraseString(EditorTestSceneKey);

        StartEditorEncounter(floor, scene);
    }

    public static bool StartEditorEncounter(int floor, string scene)
    {
        if (!IsEditorTestScene(scene))
            return false;

        Reset();
        CurrentFloor = Mathf.Clamp(floor, 1, TotalFloors);
        PrepareFloor(scene);
        Active = true;

        if (scene == BossScene)
        {
            foreach (string room in doorDestinations)
                TryCompleteRoom(room);

            bossUnlockAcknowledged = true;
        }

        return true;
    }

    public static void StartEditorBossTest(int floor)
    {
        if (Active)
            return;

        StartEditorEncounter(floor, BossScene);
    }

#endif

    private static void PrepareFloor(string startingRoom = null)
    {
        // Floor-local progress resets here; player stats carry on to the next floor.
        clearedRooms.Clear();
        encounters.Clear();
        ReturnDoorIndex = -1;

        BossDefeatedThisFloor = false;
        BossRewardClaimed = false;
        bossUnlockAcknowledged = false;

        bossChoices.Clear();

        var candidates = new string[NormalRoomScenes.Count];

        for (int i = 0; i < candidates.Length; i++)
            candidates[i] = NormalRoomScenes[i];

        for (int i = candidates.Length - 1; i > 0; i--)
        {
            int other = Random.Range(0, i + 1);
            string temporary = candidates[i];

            candidates[i] = candidates[other];
            candidates[other] = temporary;
        }

        doorDestinations = new string[RoomsPerRun];
        System.Array.Copy(candidates, doorDestinations, RoomsPerRun);

        // Direct Play from any normal scene must still count its clear and return door.
        if (startingRoom != null && System.Array.IndexOf(candidates, startingRoom) >= 0 && !IsRoomInRun(startingRoom))
            doorDestinations[0] = startingRoom;
    }

    public static FloorEncounter EncounterForRoom(string roomId)
    {
        if (!IsRoomInRun(roomId))
            return null;

        // First entry fixes this encounter; door position must not decide learning order.
        if (!encounters.TryGetValue(roomId, out var encounter))
        {
            encounter = new FloorEncounter(CurrentFloor, Random.Range(0, int.MaxValue), CompletedCount);
            encounters.Add(roomId, encounter);
        }

        return encounter;
    }

    // Announce the newly unlocked entrance once when the hallway is shown.
    public static bool TryAcknowledgeBossUnlock()
    {
        if (!BossUnlocked || bossUnlockAcknowledged || BossDefeatedThisFloor)
            return false;

        bossUnlockAcknowledged = true;

        return true;
    }

    public static bool TryDefeatBoss()
    {
        if (!BossUnlocked || BossDefeatedThisFloor)
            return false;

        BossDefeatedThisFloor = true;
        BossesDefeated++;

        return true;
    }

    public static bool TryDescend()
    {
        if (!Active || !BossDefeatedThisFloor || !BossRewardClaimed || !HasNextFloor)
            return false;

        CurrentFloor++;
        PrepareFloor();

        return true;
    }

    public static string DestinationForDoor(int index)
    {
        return doorDestinations != null && index >= 0 && index < doorDestinations.Length
            ? doorDestinations[index] : null;
    }

    public static bool IsRoomCleared(string roomId) => roomId != null && clearedRooms.Contains(roomId);
    public static bool IsRoomInRun(string roomId) => roomId != null && doorDestinations != null && System.Array.IndexOf(doorDestinations, roomId) >= 0;

    public static void SetReturnDoorForRoom(string roomId)
    {
        if (doorDestinations != null)
            ReturnDoorIndex = System.Array.IndexOf(doorDestinations, roomId);
    }

    // Normal rooms unlock the boss, but never grant an upgrade.
    public static bool TryCompleteRoom(string roomId)
    {
        if (!Active || IsRoomCleared(roomId))
            return false;

        if (doorDestinations == null || System.Array.IndexOf(doorDestinations, roomId) < 0)
            return false;

        clearedRooms.Add(roomId);
        TotalRoomsCleared++;

        return true;
    }

    public static IReadOnlyList<UpgradeDefinition> PrepareBossReward(UpgradeDefinition[] pool)
    {
        if (!Active || !HasNextFloor || !BossDefeatedThisFloor || BossRewardClaimed)
            return System.Array.Empty<UpgradeDefinition>();

        if (bossChoices.Count > 0)
            return bossChoices.AsReadOnly();

        // Cache the choices so reloading the room cannot reroll the reward.
        var available = new List<UpgradeDefinition>();
        UpgradeDefinition healing = null;

        if (pool != null)
            foreach (var upgrade in pool)
            {
                if (upgrade == null || !upgrade.Available)
                    continue;

                if (upgrade.kind == UpgradeKind.Vitality)
                {
                    if (healing == null)
                        healing = upgrade;
                }
                else if (!available.Contains(upgrade))
                    available.Add(upgrade);
            }

        // Healing is always offered, even at full HP; the other two choices are random.
        if (healing != null)
            bossChoices.Add(healing);

        while (bossChoices.Count < 3 && available.Count > 0)
        {
            int index = Random.Range(0, available.Count);

            bossChoices.Add(available[index]);
            available.RemoveAt(index);
        }

        return bossChoices.AsReadOnly();
    }

    public static bool TryClaimBossUpgrade(UpgradeDefinition upgrade)
    {
        if (!Active || !HasNextFloor || !BossDefeatedThisFloor || BossRewardClaimed || upgrade == null || !upgrade.Available || !bossChoices.Contains(upgrade))
            return false;

        upgrade.Apply();
        BossRewardClaimed = true;

        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
#if UNITY_EDITOR
        EditorInvincible = false;
#endif

        Active = false;
        CurrentFloor = 1;
        TotalRoomsCleared = BossesDefeated = 0;

        BossDefeatedThisFloor = false;
        BossRewardClaimed = false;
        bossUnlockAcknowledged = false;

        bossChoices.Clear();

        encounters.Clear();
        doorDestinations = null;
        clearedRooms.Clear();
        ReturnDoorIndex = -1;

        Health = MaxHealth = 5;

        Damage = Projectiles = 1;

        Speed = 4.5f;
        FireInterval = 0.32f;
        DashCooldown = 1.1f;

        Upgrades.Clear();
    }
}
