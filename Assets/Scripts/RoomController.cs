using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum RoomPhase
{
    Fighting,
    Upgrade,
    Exit,
    Victory,
    Defeat,
    Exploring
}

public enum RoomKind
{
    Combat,
    Boss,
    Hallway,
    TutorialStart,
    TutorialCombat
}

// Owns room progress: combat, rewards, exits, pause and scene transitions.
public class RoomController : MonoBehaviour
{
    // Room identity
    [Range(1, 5)] public int roomNumber = 1;
    public string roomTitle;
    public RoomKind kind;
    public string RoomId => gameObject.scene.name;

    // Door and HUD references
    public RoomDoor door;
    public GameHUD hud;

    // Room phase, pause and scene transitions
    public RoomPhase Phase { get; private set; } = RoomPhase.Fighting;
    public bool Paused { get; private set; }
    private bool loading;
    private bool redirectToHallway;

    // Actions allowed in the current room state
    public bool IsFighting => Phase == RoomPhase.Fighting && !Paused && !loading;
    public bool EnemiesCanAct => IsFighting && (tutorial == null || tutorial.EnemiesReady);
    public bool CanMove => !Paused && !loading &&
        (Phase == RoomPhase.Fighting || Phase == RoomPhase.Exit || Phase == RoomPhase.Exploring);
    public bool CanDash => CanMove;

    // Enemy tracking and room completion
    public EnemyHealth[] enemies = new EnemyHealth[0];
    public int Remaining { get; private set; }
    private readonly HashSet<EnemyHealth> alive = new HashSet<EnemyHealth>();

    // Boss reward options
    public UpgradeDefinition[] upgradePool;
    private readonly List<UpgradeDefinition> choices = new List<UpgradeDefinition>();

    // Tutorial state
    public bool IsTutorial => kind == RoomKind.TutorialStart || kind == RoomKind.TutorialCombat;
    private TutorialController tutorial;

#if UNITY_EDITOR
    // Direct boss testing in the Editor
    [HideInInspector, Range(1, RunState.TotalFloors)] public int bossTestFloor = 1;
#endif

    private void Awake()
    {
        Time.timeScale = 1;

        tutorial = GetComponent<TutorialController>();

#if UNITY_EDITOR
        RunState.TryStartQueuedEditorTest(RoomId);

        // A fresh Play session in the boss scene may bypass progression for testing.
        // An existing run (including normal boss entry) must never use the selector.
        if (kind == RoomKind.Boss && !RunState.Active && SceneManager.GetActiveScene() == gameObject.scene)
            RunState.StartEditorBossTest(bossTestFloor);
#endif

        RunState.EnsureStarted(kind == RoomKind.Combat ? RoomId : null);

        if (kind == RoomKind.TutorialStart)
        {
            Phase = RoomPhase.Exploring;
            return;
        }

        if (kind == RoomKind.Hallway)
        {
            Phase = RoomPhase.Exploring;
            return;
        }

        // Også direkte sceneloads skal respektere låsen og gennemførte rum.
        if ((kind == RoomKind.Boss && !RunState.BossUnlocked) ||
            (kind == RoomKind.Combat && (RunState.IsRoomCleared(RoomId) || !RunState.IsRoomInRun(RoomId))))
        {
            loading = redirectToHallway = true;
            return;
        }

        if (kind == RoomKind.Combat)
            RunState.SetReturnDoorForRoom(RoomId);

        if (kind == RoomKind.Combat && TryGetComponent<RoomEncounter>(out var encounter))
            enemies = encounter.Spawn(this);

        if (kind == RoomKind.Boss)
        {
            if (RunState.BossDefeatedThisFloor)
            {
                foreach (var enemy in enemies)
                    if (enemy != null)
                        enemy.gameObject.SetActive(false);

                if (!RunState.HasNextFloor)
                    Phase = RoomPhase.Victory;

                else if (RunState.BossRewardClaimed)
                    FinishBossReward(false);
                else
                    OfferBossReward();

                return;
            }

            foreach (var enemy in enemies)
                if (enemy != null && enemy.TryGetComponent<BossController>(out var boss))
                    boss.ConfigureForFloor(RunState.CurrentFloor);
        }

        foreach (var enemy in enemies)
            if (enemy != null && enemy.gameObject.activeSelf)
                alive.Add(enemy);

        Remaining = alive.Count;
    }

    private void Start()
    {
        if (redirectToHallway)
        {
            SceneManager.LoadScene(RunState.HallwayScene);
            return;
        }

        GameMusic.Follow(this);
        GameSfx.Follow(this);

        if (kind == RoomKind.Hallway && RunState.TryAcknowledgeBossUnlock())
            GameSfx.Play(SoundCue.DoorOpen);
    }

    private void Update()
    {
        var keys = Keyboard.current;

        if (keys == null || loading)
            return;

        if (keys.rKey.wasPressedThisFrame)
        {
            if (Phase == RoomPhase.Victory)
                ReturnToMainMenu();
            else
                RestartRun();

            return;
        }

        if (keys.escapeKey.wasPressedThisFrame &&
            (Phase == RoomPhase.Fighting || Phase == RoomPhase.Exit || Phase == RoomPhase.Exploring))
        {
            Paused = !Paused;
            Time.timeScale = Paused ? 0 : 1;
        }

        if (Phase != RoomPhase.Upgrade)
            return;

        if (keys.digit1Key.wasPressedThisFrame)
            ChooseUpgrade(0);
        else if (keys.digit2Key.wasPressedThisFrame)
            ChooseUpgrade(1);
        else if (keys.digit3Key.wasPressedThisFrame)
            ChooseUpgrade(2);
    }

    public void EnemyDefeated(EnemyHealth enemy)
    {
        if (Phase != RoomPhase.Fighting || !alive.Remove(enemy))
            return;

        Remaining = alive.Count;

        if (Remaining > 0)
            return;

        foreach (var shot in FindObjectsByType<Projectile>())
            Destroy(shot.gameObject);

        if (kind == RoomKind.TutorialCombat)
        {
            Phase = RoomPhase.Exit;
            door.Open();
            tutorial.CombatCleared();
            return;
        }

        if (kind == RoomKind.Boss)
        {
            if (!RunState.TryDefeatBoss())
                return;

            GameSfx.Play(SoundCue.BossVictory);

            if (RunState.HasNextFloor)
                OfferBossReward();

            else
                Phase = RoomPhase.Victory;

            return;
        }

        if (!RunState.TryCompleteRoom(RoomId))
            return;

        Phase = RoomPhase.Exit;
        door.Open();
    }

    private void OfferBossReward()
    {
        Phase = RoomPhase.Upgrade;

        choices.Clear();
        choices.AddRange(RunState.PrepareBossReward(upgradePool));

        hud.ShowChoices(choices);
    }

    public void ChooseUpgrade(int index)
    {
        if (kind != RoomKind.Boss || Phase != RoomPhase.Upgrade || index < 0 || index >= choices.Count)
            return;

        if (!RunState.TryClaimBossUpgrade(choices[index]))
            return;

        GameSfx.Play(choices[index].kind == UpgradeKind.Vitality ? SoundCue.Heal : SoundCue.UpgradeSelected);
        FinishBossReward();
    }

    private void FinishBossReward(bool playDoorSound = true)
    {
        Phase = RunState.HasNextFloor ? RoomPhase.Exit : RoomPhase.Victory;

        if (RunState.HasNextFloor)
            door.Open(playDoorSound);
    }

    public void Lose()
    {
        if (IsFighting)
            Phase = RoomPhase.Defeat;
    }

    public void ReturnToHallway()
    {
        if (loading || Paused || kind != RoomKind.Combat || Phase != RoomPhase.Exit)
            return;

        loading = true;

        SceneManager.LoadScene(RunState.HallwayScene);
    }

    public void UseExit()
    {
        if (IsTutorial)
        {
            LeaveTutorialRoom();
            return;
        }

        if (kind == RoomKind.Combat)
        {
            ReturnToHallway();
            return;
        }

        if (loading || Paused || kind != RoomKind.Boss || Phase != RoomPhase.Exit)
            return;

        if (!RunState.TryDescend())
            return;

        GameSfx.Play(SoundCue.NextFloor);
        loading = true;
        SceneManager.LoadScene(RunState.HallwayScene);
    }

    private void LeaveTutorialRoom()
    {
        if (loading || Paused || door == null || !door.IsOpen || !CanMove)
            return;

        loading = true;

        if (kind == RoomKind.TutorialStart)
        {
            SceneManager.LoadScene(RunState.TutorialCombatScene);
        }
        else
        {
            // The practice fight should not cost health in the actual run.
            TutorialProgress.Complete();
            RunState.Health = RunState.MaxHealth;

            SceneManager.LoadScene(RunState.HallwayScene);
        }
    }

    public void EnterFromHallway(int doorIndex, bool bossDoor)
    {
        if (kind != RoomKind.Hallway || Phase != RoomPhase.Exploring || !CanMove)
            return;

        string destination;

        if (bossDoor)
        {
            if (!RunState.BossUnlocked)
                return;

            destination = RunState.BossScene;
        }
        else
        {
            destination = RunState.DestinationForDoor(doorIndex);

            if (destination == null || RunState.IsRoomCleared(destination))
                return;

            RunState.SetReturnDoorForRoom(destination);
        }

        loading = true;
        SceneManager.LoadScene(destination);
    }

    public void RestartRun()
    {
        if (loading)
            return;

        loading = true;

        Time.timeScale = 1;

        RunState.Reset();

        SceneManager.LoadScene(TutorialProgress.ShouldPlay ? RunState.TutorialStartScene : RunState.HallwayScene);
    }

    public void ReturnToMainMenu()
    {
        if (loading)
            return;

        loading = true;
        Time.timeScale = 1;

        RunState.Reset();

        TutorialProgress.Replaying = false;

        SceneManager.LoadScene(RunState.MainMenuScene);
    }

    public void QuitGame()
    {
        if (loading)
            return;

        loading = true;
        Time.timeScale = 1;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }

    public void SkipTutorial()
    {
        if (!IsTutorial || loading)
            return;

        loading = true;
        Time.timeScale = 1;

        TutorialProgress.Complete();
        RunState.Reset();

        SceneManager.LoadScene(RunState.HallwayScene);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1;
    }
}
