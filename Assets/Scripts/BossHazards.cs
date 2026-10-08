using UnityEngine;

// Driven by BossController: beams run alongside attacks, pulses exclusively own the boss.
public sealed class BossHazards : MonoBehaviour
{
    // Scene references and shared collision range
    private BossController boss;
    private Collider2D playerCollider;
    private int walls;
    private int playerLayer;
    private const float Reach = 30f;

    // Beam settings: size, rotation and cycle timing
    public float beamWidth = 1f;
    public float rotationSpeed = 30f;
    public float beamWarning = 1.2f;
    [Range(0f, 90f)] public float beamStartOffset = 25f;
    public float beamBreak = 4f;
    public float beamFade = 0.2f;

    // Beam cycle state
    public enum BeamPhase
    {
        Dormant,
        Warning,
        Active,
        Off
    }

    public BeamPhase CurrentBeamPhase { get; private set; }
    public int CycleBeamCount { get; private set; }
    private float angle;
    private float beamUntil;
    private float fadeUntil;
    private double activeDegrees;

    // Beam unlocks at the boss's health thresholds
    public int BeamCount => secondBeam ? 2 : firstBeam ? 1 : 0;
    private bool firstBeam;
    private bool secondBeam;
    private double firstBeamThreshold;
    private double secondBeamThreshold;

    // Beam visuals
    private readonly BossBeamSurface[] beams = new BossBeamSurface[2];

    // Pulse timing and attack state
    public float pulseRecovery = 0.8f;
    public float PulseInterval { get; private set; }
    public float PulseWindup { get; private set; }
    public bool PulseActive => pulseState != PulseIdleState;
    public bool PulseWindingUp => pulseState == PulseWarningState;

    private const int PulseIdleState = 0;
    private const int PulseWarningState = 1;
    private const int PulseRecoveryState = 2;
    private int pulseState;
    private float nextPulse;
    private float pulseUntil;

    // Pulse visuals: gathering orb and exposed-area mesh
    private SpriteRenderer pulseOrb;
    private Mesh pulseMesh;
    private MeshRenderer pulseRenderer;
    private const int Segments = 192;
    private readonly Vector3[] vertices = new Vector3[Segments + 2];
    private readonly Color[] colors = new Color[Segments + 2];

    // Hazard audio and pause tracking
    [Range(0f, 1f)] public float beamVolume = 0.5f;
    private AudioSource beamAudio;
    private AudioSource windupAudio;
    private bool audioPaused;

    public void Initialize(BossController owner, int floor)
    {
        boss = owner;
        playerCollider = boss.player.GetComponent<Collider2D>();

        walls = LayerMask.GetMask("Walls");
        playerLayer = LayerMask.GetMask("Player");

        PulseInterval = floor >= 5 ? 10f : floor >= 3 ? 12f : 0f;
        PulseWindup = floor >= 5 ? 1.6f : 2f;
        nextPulse = Time.time + PulseInterval;

        firstBeamThreshold = floor >= 5 ? 0.75d : floor == 4 ? 0.5d : 0d;
        secondBeamThreshold = floor >= 5 ? 0.4d : 0d;

        for (int i = 0; i < 2; i++)
            beams[i] = new BossBeamSurface(transform, boss.pulseMaterial, boss.chargeWarningTemplate.sortingLayerID, i);

        pulseOrb = MakeSprite("Pulse gathering energy", boss.warning, 13);

        beamAudio = MakeAudio(SoundCue.BeamLoop, true);
        windupAudio = MakeAudio(SoundCue.PulseWindup, false);

        CreatePulseMesh();
    }

    // True means the normal shooting/charging cycle must wait.
    public bool Tick(bool betweenAttacks)
    {
        if (boss == null || !boss.room.IsFighting)
            return PulseActive;

        if (!firstBeam && firstBeamThreshold > 0 && boss.health.Current <= boss.health.maxHealth * firstBeamThreshold)
            firstBeam = true;

        if (!secondBeam && secondBeamThreshold > 0 && boss.health.Current <= boss.health.maxHealth * secondBeamThreshold)
            secondBeam = true;

        bool pulseDue = PulseInterval > 0 && Time.time >= nextPulse;

        // An active sweep always gets its complete turn before a pulse can take over.
        if (!PulseActive && pulseDue && CurrentBeamPhase != BeamPhase.Active && betweenAttacks)
            BeginPulse();

        if (PulseActive)
        {
            if (Time.time >= pulseUntil)
            {
                if (pulseState == PulseWarningState)
                    Explode();
                else
                {
                    pulseState = PulseIdleState;
                    BeginBeamBreak(false);
                    pulseRenderer.enabled = pulseOrb.enabled = false;
                }
            }

            return true;
        }

        // A queued pulse takes priority over starting another sweep, even while
        // waiting for the boss to finish its current shot or movement charge.
        if (pulseDue && CurrentBeamPhase != BeamPhase.Active)
            return false;

        TickBeamCycle();

        if (CurrentBeamPhase != BeamPhase.Active)
            return false;

        for (int i = 0; i < CycleBeamCount; i++)
        {
            beams[i].Sample(Direction(i), beamWidth, Reach, walls);

            if (beams[i].HitsPlayer(boss.player, playerLayer))
                boss.player.TakeDamage(1);
        }

        return false;
    }

    private void TickBeamCycle()
    {
        if (BeamCount == 0)
            return;

        if (CurrentBeamPhase == BeamPhase.Dormant || (CurrentBeamPhase == BeamPhase.Off && Time.time >= beamUntil))
        {
            CurrentBeamPhase = BeamPhase.Warning;
            CycleBeamCount = BeamCount; // Mid-cycle unlocks wait for the next full warning.
            beamUntil = Time.time + beamWarning;
            activeDegrees = 0;
            AimBeamWarning();
        }

        if (CurrentBeamPhase == BeamPhase.Warning)
        {
            if (Time.time >= beamUntil)
            {
                CurrentBeamPhase = BeamPhase.Active;
                activeDegrees = 0; // The full turn starts only when the beam becomes harmful.
            }

            // Hold the announced direction: no tracking or last-moment aim snap.
        }
        else if (CurrentBeamPhase == BeamPhase.Active)
        {
            double remaining = 360d - activeDegrees;
            double step = System.Math.Min(System.Math.Abs((double)rotationSpeed) * Time.fixedDeltaTime, remaining);

            // Snap the last tiny floating-point remainder into this step, still
            // rotating all the way to 360° rather than adding an extra physics tick.
            if (remaining - step < 0.001d)
                step = remaining;

            activeDegrees += step;
            angle = Mathf.Repeat(angle + (float)step * (rotationSpeed < 0 ? -1 : 1), 360f);

            if (activeDegrees >= 360d)
                BeginBeamBreak(true);
        }
    }

    private void AimBeamWarning()
    {
        Vector2 target = playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)boss.player.transform.position;
        Vector2 toPlayer = target - (Vector2)transform.position;

        // A fixed angle alone can overlap the player when they are close. Allow
        // room for the full beam width and collider, plus a small safety margin.
        float playerRadius = playerCollider != null ? ((Vector2)playerCollider.bounds.extents).magnitude : 0.4f;
        float clearance = beamWidth * 0.5f + playerRadius + 0.1f;

        float safeOffset = Mathf.Asin(Mathf.Clamp01(clearance / Mathf.Max(toPlayer.magnitude, 0.001f))) * Mathf.Rad2Deg;
        float offset = Mathf.Clamp(Mathf.Max(beamStartOffset, safeOffset), 0f, 90f);

        float playerAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
        angle = Mathf.Repeat(playerAngle - offset * (rotationSpeed < 0 ? -1f : 1f), 360f);
    }

    private void BeginBeamBreak(bool fade)
    {
        CurrentBeamPhase = BeamPhase.Off;
        beamUntil = Time.time + beamBreak;
        fadeUntil = fade ? Time.time + beamFade : 0;

        beamAudio.Stop(); // No damage or loop audio during the visual fade.
    }

    private void BeginPulse()
    {
        pulseState = PulseWarningState;
        pulseUntil = Time.time + PulseWindup;
        nextPulse = Time.time + PulseInterval;

        CurrentBeamPhase = BeamPhase.Off;
        CycleBeamCount = 0;
        fadeUntil = 0;

        HideBeams();

        beamAudio.Stop();

        foreach (var shot in FindObjectsByType<Projectile>())
            if (shot.IsHostile)
            {
                shot.gameObject.SetActive(false);
                Destroy(shot.gameObject);
            }

        // Fit the selected sound to the warning duration.
        windupAudio.pitch = windupAudio.clip != null ? Mathf.Clamp(windupAudio.clip.length / PulseWindup, 0.1f, 3f) : 1f;
        windupAudio.Play();

        boss.room.hud?.ShowNotice("SØG DÆKNING\nGem dig bag en grav før pulsen!", PulseWindup);
    }

    private void Explode()
    {
        windupAudio.Stop();

        pulseState = PulseRecoveryState;
        pulseUntil = Time.time + pulseRecovery;

        GameSfx.Play(SoundCue.PulseExplosion);

        if (!Physics2D.Linecast(transform.position, boss.player.transform.position, walls))
            boss.player.TakeDamage(1);
    }

    private Vector2 Direction(int index)
    {
        float radians = (angle + index * 180f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private void LateUpdate()
    {
        if (boss == null || boss.room == null)
            return;

        bool paused = boss.room.Paused;

        if (paused != audioPaused)
        {
            audioPaused = paused;

            if (paused)
            {
                beamAudio.Pause();
                windupAudio.Pause();
            }
            else
            {
                beamAudio.UnPause();
                windupAudio.UnPause();
            }
        }

        if (paused)
            return;

        if (!boss.room.IsFighting)
        {
            StopHazards();
            return;
        }

        beamAudio.volume = GameSfx.Volume * beamVolume;
        windupAudio.volume = GameSfx.Volume;

        if (PulseActive)
        {
            DrawPulse();
            return;
        }

        pulseRenderer.enabled = pulseOrb.enabled = false;

        bool warning = CurrentBeamPhase == BeamPhase.Warning;
        bool active = CurrentBeamPhase == BeamPhase.Active;
        float fade = CurrentBeamPhase == BeamPhase.Off && beamFade > 0 ? Mathf.Clamp01((fadeUntil - Time.time) / beamFade) : 0;

        for (int i = 0; i < 2; i++)
        {
            HideBeam(i);

            if (i >= CycleBeamCount || (!warning && !active && fade <= 0))
                continue;

            float opacity = active || warning ? 1f : fade;

            beams[i].Sample(Direction(i), beamWidth, Reach, walls);

            beams[i].Draw(warning, opacity, Time.time);
        }

        if (active)
        {
            if (!beamAudio.isPlaying)
                beamAudio.Play();
        }
        else if (beamAudio.isPlaying)
            beamAudio.Stop();
    }

    private SpriteRenderer MakeSprite(string label, SpriteRenderer template, int order)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);

        var sprite = go.AddComponent<SpriteRenderer>();
        sprite.sprite = template.sprite;
        sprite.sharedMaterial = template.sharedMaterial;
        sprite.sortingLayerID = template.sortingLayerID;
        sprite.sortingOrder = order;
        sprite.enabled = false;

        return sprite;
    }

    private AudioSource MakeAudio(SoundCue cue, bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        source.loop = loop;
        source.clip = GameSfx.ClipFor(cue);
        source.volume = GameSfx.Volume * (loop ? beamVolume : 1f);

        return source;
    }

    private void CreatePulseMesh()
    {
        var go = new GameObject("Pulse exposed area");
        go.transform.SetParent(transform, false);

        pulseMesh = new Mesh { name = "Pulse cover mask" };
        pulseMesh.MarkDynamic();

        var triangles = new int[Segments * 3];

        for (int i = 0; i < Segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 2;
            triangles[i * 3 + 2] = i + 1;
        }

        pulseMesh.vertices = vertices;
        pulseMesh.triangles = triangles;

        go.AddComponent<MeshFilter>().sharedMesh = pulseMesh;

        pulseRenderer = go.AddComponent<MeshRenderer>();
        pulseRenderer.sharedMaterial = boss.pulseMaterial;
        pulseRenderer.sortingLayerID = boss.chargeWarningTemplate.sortingLayerID;
        pulseRenderer.sortingOrder = 4;
        pulseRenderer.enabled = false;
    }

    private void DrawPulse()
    {
        float progress = PulseWindingUp ? 1f - Mathf.Clamp01((pulseUntil - Time.time) / PulseWindup)
            : Mathf.Clamp01((pulseUntil - Time.time) / pulseRecovery);

        var color = PulseWindingUp ? new Color(0.75f, 0.16f, 1f, 0.08f + progress * 0.2f)
            : new Color(1f, 0.65f, 1f, progress * 0.6f);

        vertices[0] = Vector3.zero;
        colors[0] = color;

        for (int i = 0; i <= Segments; i++)
        {
            float radians = i * Mathf.PI * 2f / Segments;
            var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            var hit = Physics2D.Raycast(transform.position, direction, Reach, walls);
            Vector2 endpoint = (Vector2)transform.position + direction * (hit.collider != null ? hit.distance : Reach);

            vertices[i + 1] = transform.InverseTransformPoint(endpoint);
            colors[i + 1] = color;
        }

        pulseMesh.vertices = vertices;
        pulseMesh.colors = colors;
        pulseMesh.RecalculateBounds();
        pulseRenderer.enabled = true;

        pulseOrb.enabled = PulseWindingUp;
        pulseOrb.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 2.4f, progress);
        pulseOrb.color = new Color(0.9f, 0.3f, 1f, 0.5f + progress * 0.4f);
    }

    private void HideBeams()
    {
        for (int i = 0; i < 2; i++)
            HideBeam(i);
    }

    private void HideBeam(int i)
    {
        beams[i]?.Hide();
    }

    private void StopHazards()
    {
        HideBeams();

        if (pulseRenderer != null)
            pulseRenderer.enabled = false;
        if (pulseOrb != null)
            pulseOrb.enabled = false;

        if (beamAudio != null)
            beamAudio.Stop();

        if (windupAudio != null)
            windupAudio.Stop();
    }

    private void OnDisable()
    {
        StopHazards();
    }

    private void OnDestroy()
    {
        if (pulseMesh != null)
            Destroy(pulseMesh);

        foreach (var beam in beams)
            beam?.Dispose();
    }
}
