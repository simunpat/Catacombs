using UnityEngine;

public class RoomDoor : MonoBehaviour
{
    // Room reference and door state
    public RoomController room;
    public bool IsOpen { get; private set; }

    // Door visuals and next-floor marker
    public GameObject bars;
    public SpriteRenderer glow;
    public GameObject descentMarker;

    public void Open(bool playSound = true)
    {
        if (IsOpen)
            return;

        IsOpen = true;

        if (playSound)
            GameSfx.Play(SoundCue.DoorOpen);

        if (bars != null)
            bars.SetActive(false);

        if (descentMarker != null)
            descentMarker.SetActive(room.kind == RoomKind.Boss && RunState.HasNextFloor);

        if (glow != null)
            glow.color = new Color(0.28f, 0.9f, 0.76f, 0.65f);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (IsOpen && other.GetComponent<PlayerMovement>() != null)
            room.UseExit();
    }
}
