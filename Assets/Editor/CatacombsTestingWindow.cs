using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// This whole window lives in the Editor assembly. Runtime hooks are also
// UNITY_EDITOR-only, so neither the menu nor cheats can ship in a player build.
[InitializeOnLoad]
public sealed class CatacombsTestingWindow : EditorWindow
{
    // Restore the editor's previous Play Mode start scene after testing
    private const string RestoreStartSceneKey = "Catacombs.Testing.RestoreStartScene";
    private const string PreviousStartSceneKey = "Catacombs.Testing.PreviousStartScene";

    // Test options retained by the editor window
    [SerializeField] private int floor = 4;
    [SerializeField] private int destination;
    [SerializeField] private bool keepUpgrades = true;
    [SerializeField] private float bossPercent = 50f;

    // Window state and available upgrades
    private Vector2 scroll;
    private UpgradeDefinition[] upgrades;

    // Scene choices and their readable dropdown labels
    private static readonly string[] Scenes = new[] { RunState.BossScene, RunState.HallwayScene }
        .Concat(RunState.NormalRoomScenes).ToArray();
    private static readonly string[] Labels = Scenes.Select(s => s.Substring(3).Replace('-', ' ')).ToArray();

    static CatacombsTestingWindow()
    {
        EditorApplication.playModeStateChanged += PlayModeChanged;
    }

    [MenuItem("Catacombs/Testing Mode", false, 0)]
    public static void Open()
    {
        GetWindow<CatacombsTestingWindow>("Catacombs Testing").Show();
    }

    private void OnEnable()
    {
        minSize = new Vector2(340, 480);
        RefreshUpgrades();
        EditorApplication.projectChanged += RefreshUpgrades;
        EditorApplication.update += Repaint;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= RefreshUpgrades;
        EditorApplication.update -= Repaint;
    }

    private void RefreshUpgrades()
    {
        upgrades = AssetDatabase.FindAssets("t:UpgradeDefinition", new[] { "Assets/Catacombs" })
            .Select(g => AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(u => u != null).OrderBy(u => u.kind).ToArray();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("TESTING MODE · EDITOR ONLY", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Launch a fight without playing through the run. Changes affect this Play session only, not scene assets or tutorial completion.",
            MessageType.Info);
        floor = EditorGUILayout.IntSlider("Floor", floor, 1, RunState.TotalFloors);
        destination = EditorGUILayout.Popup("Room", Mathf.Clamp(destination, 0, Scenes.Length - 1), Labels);

        bool playing = EditorApplication.isPlaying;
        bool ready = playing && !EditorApplication.isCompiling;

        using (new EditorGUI.DisabledScope(!ready))
            keepUpgrades = EditorGUILayout.ToggleLeft("Keep current upgrades when switching rooms", keepUpgrades);

        using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || (!playing && EditorApplication.isPlayingOrWillChangePlaymode)))
            if (GUILayout.Button(playing ? "Launch selected encounter" : "Start testing in Play Mode", GUILayout.Height(28)))
                LaunchSelected();

        var room = ready ? Object.FindAnyObjectByType<RoomController>() : null;
        bool live = room != null && RunState.Active;
        EditorGUILayout.Space();

        if (!live)
            EditorGUILayout.HelpBox("Start Play Mode to use the controls below. New tests start with 5/5 HP and no upgrades.", MessageType.None);
        else
            EditorGUILayout.LabelField("Current", "Floor " + RunState.CurrentFloor + " · " + room.roomTitle);

        using (new EditorGUI.DisabledScope(!live))
        {
            bool invincible = EditorGUILayout.Toggle("Invincibility", live && RunState.EditorInvincible);

            if (live)
                RunState.EditorInvincible = invincible;

            if (live && invincible)
                EditorGUILayout.HelpBox("INVINCIBILITY ON — incoming damage is ignored.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(!live || RunState.Health <= 0))
                if (GUILayout.Button("Restore player HP"))
                    RunState.Health = RunState.MaxHealth;

            using (new EditorGUI.DisabledScope(!live || (room.kind != RoomKind.Boss && room.kind != RoomKind.Combat)))
                if (GUILayout.Button("Restart current fight · keep upgrades, restore HP"))
                    LaunchEncounter(RunState.CurrentFloor, room.RoomId, true);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Boss mechanics", EditorStyles.boldLabel);

        var boss = live && room.kind == RoomKind.Boss ? Object.FindAnyObjectByType<BossController>() : null;
        bool fightingBoss = boss != null && room.Phase == RoomPhase.Fighting;

        using (new EditorGUI.DisabledScope(!fightingBoss))
        {
            EditorGUILayout.LabelField("Boss HP", boss != null ? boss.health.Current.ToString("0.#") + " / " + boss.health.maxHealth : "No active boss");
            bossPercent = EditorGUILayout.Slider("HP percentage", bossPercent, 1, 100);

            if (GUILayout.Button("Apply boss HP"))
                boss.health.SetEditorHealthPercent(bossPercent);

            EditorGUILayout.BeginHorizontal();

            foreach (float percent in new[] { 100f, 75f, 50f, 40f, 10f })
                if (GUILayout.Button(percent + "%"))
                {
                    bossPercent = percent;
                    boss.health.SetEditorHealthPercent(percent);
                }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.HelpBox("Floor 3: pulse. Floor 4: beam at 50%. Floor 5: beams at 75% and 40%. Unlocked beams stay unlocked if HP is raised; restart the fight to reset them. A second beam waits for the next warning cycle.",
            MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Add upgrades", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!live || RunState.Health <= 0))
        {
            foreach (var upgrade in upgrades ?? System.Array.Empty<UpgradeDefinition>())
                using (new EditorGUI.DisabledScope(!upgrade.Available))
                    if (GUILayout.Button(new GUIContent(upgrade.title, upgrade.description)))
                        upgrade.Apply();
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!playing))
            if (GUILayout.Button("Stop testing / exit Play Mode"))
                EditorApplication.ExitPlaymode();

        EditorGUILayout.EndScrollView();
    }

    private void LaunchSelected()
    {
        string scene = Scenes[destination];
        var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/" + scene + ".unity");

        if (asset == null)
        {
            Debug.LogError("Testing scene is missing: " + scene);

            return;
        }

        if (EditorApplication.isPlaying)
        {
            LaunchEncounter(floor, scene, keepUpgrades);

            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // Use Unity's temporary Play start scene instead of replacing the user's
        // open scene setup. Restore any previous start-scene override afterwards.
        SessionState.SetString(PreviousStartSceneKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(RestoreStartSceneKey, true);
        SessionState.SetString(RunState.EditorTestSceneKey, scene);
        SessionState.SetInt(RunState.EditorTestFloorKey, floor);
        EditorSceneManager.playModeStartScene = asset;
        EditorApplication.EnterPlaymode();
    }

    public static bool LaunchEncounter(int floor, string scene, bool preserveUpgrades)
    {
        if (!EditorApplication.isPlaying || !RunState.IsEditorTestScene(scene) ||
            !Application.CanStreamedLevelBeLoaded(scene))
            return false;

        bool preserve = preserveUpgrades && RunState.Active;
        int maxHP = RunState.MaxHealth, damage = RunState.Damage, projectiles = RunState.Projectiles;
        float speed = RunState.Speed, fireInterval = RunState.FireInterval, dashCooldown = RunState.DashCooldown;
        string[] titles = RunState.Upgrades.ToArray();
        bool invincible = RunState.EditorInvincible;

        if (!RunState.StartEditorEncounter(floor, scene))
            return false;

        if (preserve)
        {
            RunState.Health = RunState.MaxHealth = maxHP;
            RunState.Damage = damage;
            RunState.Projectiles = projectiles;
            RunState.Speed = speed;
            RunState.FireInterval = fireInterval;
            RunState.DashCooldown = dashCooldown;
            RunState.Upgrades.AddRange(titles);
        }

        RunState.EditorInvincible = invincible;
        // Also recover from an Editor pause, game pause or defeat.
        EditorApplication.isPaused = false;
        Time.timeScale = 1;
        SceneManager.LoadScene(scene);

        return true;
    }

    private static void PlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode && state != PlayModeStateChange.EnteredEditMode)
            return;

        if (SessionState.GetBool(RestoreStartSceneKey, false))
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousStartSceneKey, ""));
            SessionState.EraseBool(RestoreStartSceneKey);
            SessionState.EraseString(PreviousStartSceneKey);
        }

        // Fallback for Enter Play Mode configurations that don't reload scenes.
        string pending = SessionState.GetString(RunState.EditorTestSceneKey, "");

        if (state == PlayModeStateChange.EnteredPlayMode && !string.IsNullOrEmpty(pending))
            LaunchEncounter(SessionState.GetInt(RunState.EditorTestFloorKey, 1), pending, false);

        SessionState.EraseString(RunState.EditorTestSceneKey);
        SessionState.EraseInt(RunState.EditorTestFloorKey);

        if (state == PlayModeStateChange.EnteredEditMode)
            RunState.EditorInvincible = false;
    }
}
