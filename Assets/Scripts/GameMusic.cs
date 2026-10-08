using UnityEngine;

// One shared player follows the room state and survives scene transitions.
public class GameMusic : MonoBehaviour
{
    // Music tracks
    public AudioClip exploration;
    public AudioClip combat;
    public AudioClip boss;

    // Volume and transition settings
    [Range(0, 1)] public float volume = 0.25f;
    [Min(0.01f)] public float fadeDuration = 0.65f;

    // Crossfade sources and their current gains
    public AudioSource firstSource;
    public AudioSource secondSource;
    private AudioSource activeSource;
    private float firstGain;
    private float secondGain;

    // Track selection and room state
    public AudioClip CurrentTrack { get; private set; }
    private RoomController room;

    // Shared music player that survives scene changes
    private static GameMusic instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        instance = null;
    }

    public static void Follow(RoomController currentRoom)
    {
        if (instance == null)
        {
            var prefab = Resources.Load<GameObject>("CatacombsMusic");

            if (prefab == null || prefab.GetComponent<GameMusic>() == null)
            {
                Debug.LogError("Missing CatacombsMusic prefab. Run Catacombs/Set up music.");
                return;
            }

            Instantiate(prefab);
        }

        instance.room = currentRoom;
        instance.SelectTrack();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        Prepare(firstSource);
        Prepare(secondSource);
    }

    private static void Prepare(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0;
        source.volume = 0;

        source.Stop();
    }

    private void Update()
    {
        SelectTrack();

        // Pausing gameplay must neither change the song nor freeze a transition.
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration);

        Fade(firstSource, ref firstGain, step);
        Fade(secondSource, ref secondGain, step);
    }

    private void SelectTrack()
    {
        bool fighting = room != null && room.Phase == RoomPhase.Fighting;

        AudioClip next = fighting && room.kind == RoomKind.Boss ? boss
            : fighting && (room.kind == RoomKind.Combat || room.kind == RoomKind.TutorialCombat) ? combat
            : exploration;

        if (next == CurrentTrack)
            return;

        CurrentTrack = next;

        if (next == null)
        {
            activeSource = null;
            return;
        }

        // A quick return during a crossfade resumes the still-playing track.
        if (firstSource.clip == next && firstSource.isPlaying)
            activeSource = firstSource;
        else if (secondSource.clip == next && secondSource.isPlaying)
            activeSource = secondSource;
        else
        {
            activeSource = activeSource == firstSource ? secondSource : firstSource;
            activeSource.Stop();
            activeSource.volume = 0;

            if (activeSource == firstSource)
                firstGain = 0;
            else
                secondGain = 0;

            activeSource.clip = next;
            activeSource.Play();
        }
    }

    private void Fade(AudioSource source, ref float gain, float step)
    {
        gain = Mathf.MoveTowards(gain, source == activeSource ? 1 : 0, step);
        source.volume = gain * volume;

        if (source != activeSource && gain == 0 && source.clip != null)
        {
            source.Stop();
            source.clip = null;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
