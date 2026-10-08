using UnityEngine;
using UnityEngine.InputSystem;

public class TutorialController : MonoBehaviour
{
    // Room and player references
    public RoomController room;
    private PlayerMovement player;
    private PlayerWeapon weapon;

    // Tutorial steps and current progress
    public enum Step
    {
        Move,
        Dash,
        LeaveStart,
        Aim,
        Shoot,
        Fight,
        LeaveCombat
    }

    public Step CurrentStep { get; private set; }

    // Enemy activation after the first shot
    public bool EnemiesReady => room.kind == RoomKind.TutorialCombat && Time.time >= wakeAt;
    private float wakeAt = float.PositiveInfinity;

    // Starting values used to detect movement and dashing
    private float startingDistance;
    private int startingDashCount;

    private void Start()
    {
        if (room.kind == RoomKind.TutorialStart && !TutorialProgress.ShouldPlay)
        {
            room.SkipTutorial();
            return;
        }

        player = room.hud.player;
        weapon = player.GetComponent<PlayerWeapon>();

        startingDistance = player.DistanceTravelled;
        startingDashCount = player.DashCount;

        SetStep(room.kind == RoomKind.TutorialStart ? Step.Move : Step.Aim);
    }

    private void Update()
    {
        if (player == null || !room.CanMove)
            return;

        switch (CurrentStep)
        {
            case Step.Move:
                if (player.DistanceTravelled - startingDistance >= 1.2f)
                {
                    startingDashCount = player.DashCount;
                    SetStep(Step.Dash);
                }
                break;

            case Step.Dash:
                if (player.DashCount > startingDashCount)
                {
                    room.door.Open();
                    SetStep(Step.LeaveStart);
                }
                break;

            case Step.Aim:
                if (weapon.ShotsFired > 0 || (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 1f))
                    SetStep(Step.Shoot);
                break;

            case Step.Shoot:
                if (weapon.ShotsFired > 0)
                {
                    wakeAt = Time.time + 1f;
                    SetStep(Step.Fight);
                }
                break;
        }
    }

    public void CombatCleared()
    {
        SetStep(Step.LeaveCombat);
    }

    private void SetStep(Step step)
    {
        CurrentStep = step;

        string message = GetStepMessage(step);

        // Tutorial instructions stay visible until the next step replaces them.
        room.hud.ShowNotice(message, float.PositiveInfinity);
    }

    private static string GetStepMessage(Step step)
    {
        switch (step)
        {
            case Step.Move:
                return "BEVÆG DIG\nBrug W, A, S og D til at gå rundt.";

            case Step.Dash:
                return "UNDVIG\nTryk SPACE, mens du bevæger dig, for at dashe.";

            case Step.LeaveStart:
                return "DØREN ER ÅBEN\nGå gennem den lysende dør mod nord.";

            case Step.Aim:
                return "SIGT\nBevæg musen for at pege mod fjenden.";

            case Step.Shoot:
                return "SKYD\nHold venstre museknap nede for at skyde.";

            case Step.Fight:
                return "DIN FØRSTE KAMP\nSkyd fjenden, og hold afstand. SPACE undviger.";

            default:
                return "RUMMET ER RYDDET · DØREN ER ÅBEN\nGå gennem døren. I resten af spillet viser T et tip.";
        }
    }
}
