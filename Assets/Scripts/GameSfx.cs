using UnityEngine;

public enum SoundCue
{
    PlayerShot,
    EnemyShot,
    BossDeath,
    EnemyDeath,
    PlayerDeath,
    BossVictory,
    ShotImpact,
    MeleeImpact,
    DoorOpen,
    EnemyCharge,
    PlayerDash,
    UpgradeSelected,
    Heal,
    PlayerHurt,
    NextFloor,
    BeamLoop,
    PulseWindup,
    PulseExplosion
}

// Persistent voices let death and door sounds finish after objects or scenes disappear.
public class GameSfx : MonoBehaviour
{
    // Sound effects and shared volume
    public AudioClip[] clips;
    [Range(0, 1)] public float volume = 0.65f;
    public static float Volume => instance != null ? instance.volume : 0.65f;

    // Playback sources, start times and impact cooldown
    public AudioSource[] voices;
    private float[] started;
    private float nextImpact;

    // Player footsteps
    public AudioClip footsteps;
    [Range(0, 1)] public float footstepVolume = 0.2f;
    public AudioSource footstepSource;

    // Room, player and pause state
    private RoomController room;
    private PlayerMovement player;
    private bool paused;

    // Shared sound player that survives scene changes
    private static GameSfx instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        instance = null;
    }

    private static GameSfx Ensure()
    {
        if (!Application.isPlaying)
            return null;

        if (instance != null)
            return instance;

        var prefab = Resources.Load<GameObject>("CatacombsSfx");

        if (prefab == null)
        {
            Debug.LogError("Missing CatacombsSfx prefab. Run Catacombs/Set up sound effects.");
            return null;
        }

        Instantiate(prefab);

        return instance;
    }

    public static void Follow(RoomController currentRoom)
    {
        var audio = Ensure();

        if (audio == null)
            return;

        audio.room = currentRoom;
        audio.player = currentRoom.hud != null ? currentRoom.hud.player : null;

        audio.footstepSource.Stop();
        audio.SetPaused(currentRoom.Paused);
    }

    public static void Play(SoundCue cue)
    {
        var audio = Ensure();

        if (audio != null)
            audio.PlayCue(cue);
    }

    public static void StopForMenu()
    {
        if (instance == null)
            return;

        instance.room = null;
        instance.player = null;

        instance.SetPaused(false);
        instance.footstepSource.Stop();

        foreach (var source in instance.voices)
            source.Stop();
    }

    // Boss-owned sources can be stopped immediately when a hazard ends or dies.
    public static AudioClip ClipFor(SoundCue cue)
    {
        var audio = Ensure();

        return audio != null && (int)cue < audio.clips.Length ? audio.clips[(int)cue] : null;
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

        started = new float[voices.Length];
        footstepSource.clip = footsteps;
    }

    private void PlayCue(SoundCue cue)
    {
        if (room != null && room.Paused)
            return;

        int clipIndex = (int)cue;

        if (clips == null || clipIndex >= clips.Length || clips[clipIndex] == null)
            return;

        // Simultaneous pellets must not produce a deafening stack of identical impacts.
        if (cue == SoundCue.ShotImpact)
        {
            if (Time.unscaledTime < nextImpact)
                return;

            nextImpact = Time.unscaledTime + 0.035f;
        }

        // Reserve voices for boss death, its fanfare, and player death.
        int index = cue == SoundCue.BossDeath ? 0 : cue == SoundCue.BossVictory ? 1 : cue == SoundCue.PlayerDeath ? 2 : -1;

        if (index < 0)
        {
            index = 3;

            for (int i = 3; i < voices.Length; i++)
            {
                if (!voices[i].isPlaying)
                {
                    index = i;
                    break;
                }

                if (started[i] < started[index])
                    index = i;
            }
        }

        var source = voices[index];
        source.Stop();

        source.clip = clips[clipIndex];
        source.volume = volume * (cue == SoundCue.ShotImpact ? 0.45f : cue == SoundCue.PlayerShot || cue == SoundCue.EnemyShot ? 0.65f : 1f);

        source.Play();
        started[index] = Time.unscaledTime;
    }

    private void LateUpdate()
    {
        SetPaused(room != null && room.Paused);

        bool walking = !paused && room != null && room.CanMove && player != null && player.IsWalking && !player.IsDashing;

        footstepSource.volume = footstepVolume;

        if (walking)
        {
            if (!footstepSource.isPlaying)
                footstepSource.Play();
        }
        else if (footstepSource.isPlaying)
            footstepSource.Stop();
    }

    private void SetPaused(bool value)
    {
        if (paused == value)
            return;

        paused = value;

        foreach (var source in voices)
            if (paused)
                source.Pause();
            else
                source.UnPause();

        if (paused)
            footstepSource.Stop();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
