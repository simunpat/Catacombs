using UnityEngine;

public class HallwayController : MonoBehaviour
{
    // Player and hallway spawn points
    public PlayerMovement player;
    public Transform startPoint;
    public Transform[] returnPoints;

    // Door progress indicators
    public HallwayDoor[] doors;

    private void Start()
    {
        RunState.EnsureStarted();

        foreach (var door in doors)
            if (door != null)
                door.Refresh();

        int index = RunState.ReturnDoorIndex;
        Transform spawn = index >= 0 && index < returnPoints.Length ? returnPoints[index] : startPoint;

        player.PlaceAt(spawn.position);
    }
}
