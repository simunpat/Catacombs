using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Additive migration: copy the original grave room, preserve all existing geometry.
public static class CatacombsTutorialSetup
{
    [MenuItem("Catacombs/Add tutorial and clean HUD")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before setup.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        CreateTutorial(RunState.TutorialStartScene, false);
        CreateTutorial(RunState.TutorialCombatScene, true);
        var names = new List<string> { RunState.TutorialStartScene, RunState.TutorialCombatScene, RunState.HallwayScene };
        names.AddRange(RunState.NormalRoomScenes);
        names.Add(RunState.BossScene);
        foreach (string name in names)
        {
            var scene = EditorSceneManager.OpenScene(PathFor(name));
            ConfigureHUD(UnityEngine.Object.FindAnyObjectByType<GameHUD>());
            EditorSceneManager.SaveScene(scene);
        }
        var build = new List<EditorBuildSettingsScene>();
        if (File.Exists(PathFor(RunState.MainMenuScene)))
            build.Add(new EditorBuildSettingsScene(PathFor(RunState.MainMenuScene), true));
        foreach (string name in names)
            build.Add(new EditorBuildSettingsScene(PathFor(name), true));
        foreach (var old in EditorBuildSettings.scenes)
            if (!build.Exists(entry => entry.path == old.path))
                build.Add(old);
        EditorBuildSettings.scenes = build.ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(PathFor(RunState.TutorialStartScene));
        Debug.Log("TUTORIAL_SETUP_OK");
    }

    private static string PathFor(string name) => "Assets/Scenes/" + name + ".unity";

    private static void CreateTutorial(string name, bool combat)
    {
        if (File.Exists(PathFor(name)))
            return;
        var scene = EditorSceneManager.OpenScene(PathFor("06-Grave-Chamber"));
        var room = UnityEngine.Object.FindAnyObjectByType<RoomController>();
        var encounter = room.GetComponent<RoomEncounter>();
        if (encounter != null)
            UnityEngine.Object.DestroyImmediate(encounter);
        var spawnRoot = room.transform.Find("Encounter spawn points");
        if (spawnRoot != null)
            UnityEngine.Object.DestroyImmediate(spawnRoot.gameObject);
        foreach (var old in room.enemies)
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old.gameObject);
        room.enemies = new EnemyHealth[0];
        room.kind = combat ? RoomKind.TutorialCombat : RoomKind.TutorialStart;
        room.roomTitle = combat ? "Den første kamp" : "Indgangen";
        room.hud.player.transform.position = new Vector3(0, -3.7f, 0);
        var tutorial = room.gameObject.AddComponent<TutorialController>();
        tutorial.room = room;
        if (combat)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Catacombs/Prefabs/Crawler.prefab"));
            go.transform.SetParent(GameObject.Find("Fjender").transform, false);
            go.transform.position = new Vector3(0, 1.7f, 0);
            var health = go.GetComponent<EnemyHealth>();
            health.room = room;
            var ai = go.GetComponent<EnemyController>();
            ai.room = room;
            ai.player = room.hud.player.GetComponent<PlayerHealth>();
            ai.speed = 1.4f;
            room.enemies = new[] { health };
        }
        else
        {
            var layout = GameObject.Find("Katakomber").transform.Find("Room layout");
            if (layout != null)
                UnityEngine.Object.DestroyImmediate(layout.gameObject);
        }
        EditorSceneManager.SaveScene(scene, PathFor(name));
    }

    private static void ConfigureHUD(GameHUD hud)
    {
        var controls = hud.transform.Find("Controls");
        if (controls != null)
            UnityEngine.Object.DestroyImmediate(controls.gameObject);
        var bottom = hud.transform.Find("Bottom") as RectTransform;
        if (bottom != null)
        {
            bottom.sizeDelta = new Vector2(0, 64);
            bottom.anchoredPosition = new Vector2(0, 32);
        }
        SetY(hud.buildText.rectTransform, 45);
        SetY(hud.statsText.rectTransform, 22);
        SetY(hud.dashText.rectTransform, 45);
        SetY(hud.dashFill.rectTransform, 20);
        var track = hud.transform.Find("Dash track") as RectTransform;
        if (track != null)
            SetY(track, 20);
        hud.dashText.text = "DASH KLAR";
        hud.objectiveText.text = hud.room.IsTutorial ? "" : "RUM RYDDET  0 / 4";
        hud.roomText.text = hud.room.IsTutorial ? "INTRODUKTION  ·  " + hud.room.roomTitle.ToUpperInvariant()
            : "ETAGE 1 / " + RunState.TotalFloors + "  ·  " + hud.room.roomTitle.ToUpperInvariant();
        if (hud.noticePanel == null)
        {
            var panel = Rect("Learning and event notice", hud.transform, new Vector2(0.5f, 1), new Vector2(0, -195), new Vector2(840, 88));
            var image = panel.gameObject.AddComponent<Image>();
            image.color = new Color(0.025f, 0.055f, 0.065f, 0.94f);
            image.raycastTarget = false;
            hud.noticePanel = panel.gameObject;
            hud.noticeText = Label("Message", panel, "", new Vector2(790, 76), 22);
            hud.noticePanel.SetActive(false);
        }
        if (hud.skipTutorialButton == null)
        {
            hud.skipTutorialButton = Button("Skip introduction", hud.transform, new Vector2(1, 1), new Vector2(-165, -116), new Vector2(280, 40), "SPRING INTRO OVER");
            hud.skipTutorialButton.gameObject.SetActive(hud.room.IsTutorial);
        }
        var hint = hud.pausePanel.transform.Find("Pause hint").GetComponent<Text>();
        hint.text = "WASD  BEVÆG  ·  MUS  SIGT  ·  VENSTREKLIK  SKYD\nSPACE  DASH  ·  T  VIS TIP  ·  1 / 2 / 3  VÆLG OPGRADERING\nESC  FORTSÆT  ·  R  NYT RUN";
        hint.rectTransform.sizeDelta = new Vector2(950, 100);
        hint.rectTransform.anchoredPosition = new Vector2(0, -65);
        CatacombsPauseMenuSetup.Configure(hud);
        EditorUtility.SetDirty(hud);
    }

    private static void SetY(RectTransform rect, float y)
    {
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
    private static Text Label(string name, Transform parent, string text, Vector2 size, int fontSize)
    {
        var rect = Rect(name, parent, Vector2.one * 0.5f, Vector2.zero, size);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.75f, 0.96f, 0.88f);
        label.raycastTarget = false;
        return label;
    }
    private static Button Button(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string title)
    {
        var rect = Rect(name, parent, anchor, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.23f, 0.24f, 0.98f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Label("Label", rect, title, size - new Vector2(16, 4), 18);
        return button;
    }
}
