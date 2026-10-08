using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// Engangsmigrering: eksisterende kamprum bevares, og gangen gemmes som en normal scene.
public static class CatacombsHallwaySetup
{
    private const string HallwayPath = "Assets/Scenes/00-Hallway.unity";
    private const string Art = "Assets/Catacombs/Art/";
    private static Material material;
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private static Color Stone => new Color(0.2f, 0.25f, 0.27f);
    private static Color Gold => new Color(0.94f, 0.73f, 0.39f);
    private static Color Mint => new Color(0.48f, 0.94f, 0.79f);

    [MenuItem("Catacombs/Add hallway")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode før opsætning af gangen.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        foreach (var name in RunState.NormalRoomScenes)
            SetKind(name, RoomKind.Combat);
        SetKind(RunState.BossScene, RoomKind.Boss);
        if (!File.Exists(HallwayPath))
            BuildHallway();

        var scenes = new List<EditorBuildSettingsScene>();
        foreach (var tutorial in new[] { RunState.TutorialStartScene, RunState.TutorialCombatScene })
        {
            string path = "Assets/Scenes/" + tutorial + ".unity";
            if (File.Exists(path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        scenes.Add(new EditorBuildSettingsScene(HallwayPath, true));
        foreach (var name in RunState.NormalRoomScenes)
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/" + name + ".unity", true));
        scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/" + RunState.BossScene + ".unity", true));
        foreach (var existing in EditorBuildSettings.scenes)
            if (!scenes.Exists(scene => scene.path == existing.path))
                scenes.Add(existing);
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(HallwayPath);
        Debug.Log("CATACOMBS_HALLWAY_SETUP_OK: Existing rooms and tutorial entry order preserved.");
    }

    private static void SetKind(string name, RoomKind kind)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
        var room = UnityEngine.Object.FindAnyObjectByType<RoomController>();
        if (room == null)
            throw new InvalidOperationException("RoomController mangler i " + name);
        room.kind = kind;
        EditorUtility.SetDirty(room);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BuildHallway()
    {
        // Kopiér kun kamera og UI fra det første rum, så samme visuelle stil bevares.
        var source = EditorSceneManager.OpenScene("Assets/Scenes/" + RunState.NormalRoomScenes[0] + ".unity");
        var template = UnityEngine.Object.FindAnyObjectByType<RoomController>();
        var templateCamera = Camera.main;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var camera = UnityEngine.Object.Instantiate(templateCamera.gameObject);
        camera.name = "Main Camera";

        var room = new GameObject("Spilregler").AddComponent<RoomController>();
        room.kind = RoomKind.Hallway;
        room.roomTitle = "Gangen";
        room.enemies = new EnemyHealth[0];
        room.upgradePool = template.upgradePool;
        var playerObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Catacombs/Prefabs/Player.prefab"));
        var player = playerObject.GetComponent<PlayerMovement>();
        player.room = room;
        playerObject.GetComponent<PlayerHealth>().room = room;
        playerObject.GetComponent<PlayerWeapon>().room = room;
        playerObject.transform.position = new Vector3(0, -3.7f, 0);

        var canvas = UnityEngine.Object.Instantiate(template.hud.gameObject);
        canvas.name = "Canvas";
        var hud = canvas.GetComponent<GameHUD>();
        hud.room = room;
        hud.player = player;
        hud.boss = null;
        room.hud = hud;
        hud.bossPanel.SetActive(false);
        hud.upgradePanel.SetActive(false);
        hud.resultPanel.SetActive(false);
        hud.pausePanel.SetActive(false);
        hud.roomText.text = "GANGEN     ·     VÆLG EN DØR";
        hud.objectiveText.text = "RUM RYDDET  0 / 4   ·   BOSSDØREN ER LÅST";
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        material = AssetDatabase.LoadAssetAtPath<Material>(Art + "DungeonSprites.mat");
        foreach (var name in new[] { "Square", "Circle", "Ring", "Torch", "Stone0", "Stone1", "Stone2", "Stone3" })
            sprites[name] = AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");

        var environment = new GameObject("Gangen").transform;
        var floor = new GameObject("Stengulv").transform;
        floor.SetParent(environment, false);
        var random = new System.Random(117);
        for (int x = -9; x <= 9; x++)
            for (int y = -5; y <= 5; y++)
                Sprite("Sten " + x + "," + y, "Stone" + random.Next(4), floor, new Vector2(x, y), Vector2.one, Color.white, -5);

        // En diskret sti forbinder dørene; ingen symboler afslører rummenes indhold.
        Sprite("Midtersti", "Square", environment, Vector2.zero, new Vector2(2.1f, 9.5f), new Color(0.23f, 0.3f, 0.29f, 0.22f), -4);
        foreach (float y in new[] { -2.4f, 2.4f })
            Sprite("Tværsti", "Square", environment, new Vector2(0, y), new Vector2(18, 1.8f), new Color(0.23f, 0.3f, 0.29f, 0.16f), -4);
        Sprite("Gammelt segl", "Ring", environment, Vector2.zero, Vector2.one * 2.1f, new Color(0.3f, 0.38f, 0.32f, 0.6f), -3);
        Sprite("Indre segl", "Ring", environment, Vector2.zero, Vector2.one * 1.8f, new Color(0.3f, 0.38f, 0.32f, 0.4f), -3);

        Wall("Syd", environment, new Vector2(0, -5.2f), new Vector2(18.9f, 0.5f));
        Wall("Nord vest", environment, new Vector2(-5.3f, 5.2f), new Vector2(8.3f, 0.5f));
        Wall("Nord øst", environment, new Vector2(5.3f, 5.2f), new Vector2(8.3f, 0.5f));
        Wall("Bag bossdøren", environment, new Vector2(0, 5.8f), new Vector2(2.5f, 0.4f));
        foreach (float x in new[] { -9.2f, 9.2f })
        {
            Wall("Sidemur syd", environment, new Vector2(x, -4.3f), new Vector2(0.5f, 1.8f));
            Wall("Sidemur midt", environment, new Vector2(x, 0), new Vector2(0.5f, 2.8f));
            Wall("Sidemur nord", environment, new Vector2(x, 4.3f), new Vector2(0.5f, 1.8f));
            foreach (float y in new[] { -2.4f, 2.4f })
                Wall("Bag kammerdør", environment, new Vector2(Mathf.Sign(x) * 9.8f, y), new Vector2(0.35f, 2.4f));
        }
        for (int x = -9; x <= 9; x++)
        {
            Sprite("Mursten", "Stone2", environment, new Vector2(x, -5.2f), new Vector2(0.94f, 0.5f), new Color(1.4f, 1.5f, 1.5f), 7);
            if (Mathf.Abs(x) > 1)
                Sprite("Mursten", "Stone1", environment, new Vector2(x, 5.2f), new Vector2(0.94f, 0.5f), new Color(1.6f, 1.7f, 1.6f), 7);
        }
        foreach (float x in new[] { -6.7f, 6.7f })
            foreach (float y in new[] { -4.1f, 0f, 4.1f })
            {
                Sprite("Fakkellys", "Circle", environment, new Vector2(x, y), Vector2.one * 2, new Color(1, 0.55f, 0.2f, 0.055f), 1);
                Sprite("Fakkel", "Torch", environment, new Vector2(x, y), Vector2.one * 0.7f, Color.white, 8);
            }
        foreach (float x in new[] { -3.9f, 3.9f })
            foreach (float y in new[] { -0.7f, 0.7f })
            {
                Wall("Søjle", environment, new Vector2(x, y), Vector2.one * 0.6f);
                Sprite("Søjletop", "Square", environment, new Vector2(x, y + 0.07f), Vector2.one * 0.46f, new Color(0.32f, 0.37f, 0.36f), 7);
            }

        var hallway = room.gameObject.AddComponent<HallwayController>();
        hallway.player = player;
        hallway.startPoint = Point("Startpunkt", new Vector2(0, -3.7f));
        hallway.returnPoints = new Transform[4];
        hallway.doors = new HallwayDoor[5];
        for (int i = 0; i < 4; i++)
        {
            float side = i < 2 ? -1 : 1;
            float y = i % 2 == 0 ? -2.4f : 2.4f;
            hallway.doors[i] = Door(room, i, false, new Vector2(side * 9.1f, y), side * -90f);
            hallway.returnPoints[i] = Point("Returpunkt " + i, new Vector2(side * 7.5f, y));
        }
        hallway.doors[4] = Door(room, 0, true, new Vector2(0, 4.95f), 0);
        EditorSceneManager.SaveScene(scene, HallwayPath);
        EditorSceneManager.CloseScene(source, true);
    }

    private static Transform Point(string name, Vector2 position)
    {
        var point = new GameObject(name).transform;
        point.position = position;
        return point;
    }

    private static HallwayDoor Door(RoomController room, int index, bool boss, Vector2 position, float angle)
    {
        var go = new GameObject(boss ? "Bossdør" : "Kammerdør " + index);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0, 0, angle);
        var trigger = go.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.8f, 0.95f);
        var door = go.AddComponent<HallwayDoor>();
        door.room = room;
        door.doorIndex = index;
        door.isBossDoor = boss;
        door.glow = Sprite("Mørk åbning", "Square", go.transform, Vector2.zero, new Vector2(1.95f, 1.05f), new Color(0.035f, 0.05f, 0.065f), 1);
        foreach (float x in new[] { -1.07f, 1.07f })
            Sprite("Dørkarm", "Square", go.transform, new Vector2(x, 0), new Vector2(0.18f, 1.25f), boss ? Gold * 0.7f : Stone * 1.3f, 8);
        Sprite("Overligger", "Square", go.transform, new Vector2(0, 0.55f), new Vector2(2.35f, 0.2f), boss ? Gold * 0.7f : Stone * 1.3f, 8);
        var bars = new GameObject("Låst gitter");
        bars.transform.SetParent(go.transform, false);
        bars.layer = LayerMask.NameToLayer("Walls");
        bars.AddComponent<BoxCollider2D>().size = new Vector2(1.95f, 0.25f);
        for (int i = -3; i <= 3; i++)
            Sprite("Jernstang", "Square", bars.transform, new Vector2(i * 0.26f, 0), new Vector2(0.07f, 0.95f), boss ? Gold * 0.65f : Stone, 9);
        door.lockedBars = bars;
        bars.SetActive(boss);
        door.clearedMarker = Sprite("Ryddet", "Ring", go.transform, new Vector2(0, -0.65f), Vector2.one * 0.4f, Mint, 10).gameObject;
        door.clearedMarker.SetActive(false);
        if (boss)
        {
            door.seals = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var seal = Sprite("Segl " + i, "Square", go.transform, new Vector2((i - 1.5f) * 0.43f, -0.78f), Vector2.one * 0.15f, new Color(0.3f, 0.27f, 0.22f), 10);
                seal.transform.localRotation = Quaternion.Euler(0, 0, 45);
                door.seals[i] = seal;
            }
        }
        return door;
    }

    private static void Wall(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var wall = Sprite(name, "Square", parent, position, size, Stone, 6);
        wall.gameObject.layer = LayerMask.NameToLayer("Walls");
        wall.gameObject.AddComponent<BoxCollider2D>();
    }

    private static SpriteRenderer Sprite(string name, string art, Transform parent, Vector2 position, Vector2 size, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1);
        var visual = go.AddComponent<SpriteRenderer>();
        visual.sprite = sprites[art];
        visual.sharedMaterial = material;
        visual.color = color;
        visual.sortingOrder = order;
        return visual;
    }
}
