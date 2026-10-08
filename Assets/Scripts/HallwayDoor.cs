using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class HallwayDoor : MonoBehaviour
{
    // Room reference and door destination
    public RoomController room;
    [Range(0, 3)] public int doorIndex;
    public bool isBossDoor;

    // Door visuals and progress indicators
    public GameObject clearedMarker;
    public GameObject lockedBars;
    public SpriteRenderer glow;
    public SpriteRenderer[] seals = new SpriteRenderer[0];

    // Entry is blocked until the destination is available
    public bool IsBlocked => isBossDoor ? !RunState.BossUnlocked
        : RunState.DestinationForDoor(doorIndex) == null || RunState.IsRoomCleared(RunState.DestinationForDoor(doorIndex));

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    public void Refresh()
    {
        bool cleared = !isBossDoor && RunState.IsRoomCleared(RunState.DestinationForDoor(doorIndex));

        if (clearedMarker != null)
            clearedMarker.SetActive(cleared);

        if (lockedBars != null)
            lockedBars.SetActive(IsBlocked);

        if (glow != null)
            glow.color = cleared || (isBossDoor && RunState.BossUnlocked)
                ? new Color(0.17f, 0.48f, 0.4f) : new Color(0.035f, 0.05f, 0.065f);

        for (int i = 0; i < seals.Length; i++)
            if (seals[i] != null)
                seals[i].color = i < RunState.CompletedCount
                    ? new Color(0.48f, 0.94f, 0.79f) : new Color(0.3f, 0.27f, 0.22f);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsBlocked && other.GetComponent<PlayerMovement>() != null && room != null)
            room.EnterFromHallway(doorIndex, isBossDoor);
    }
}
